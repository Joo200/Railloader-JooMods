using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Track;

// Railroader 2026.1 has no StrangeCustoms (on Legacy, RailForge provides it). These are the few of its track types
// SignalsEverywhere uses, behaving as RailForge's do: signal data keeps reading the same, and SignalsEverywhere's
// reflection calls (SerializedSpan.ApplyTo, SerializedLocation.Validate) find the same non-public methods.
namespace StrangeCustoms.Tracks;

public class SCPatchingException(string message, string property = "") : Exception(message)
{
    public string Property { get; } = property;
}

public class PatchingContext(Serilog.ILogger logger, IReadOnlyDictionary<string, string> changedEntries)
{
    public Serilog.ILogger Logger { get; } = logger;

    public IReadOnlyDictionary<string, string> TouchedKeys { get; } = changedEntries;

    public Dictionary<string, TrackNode> NodesById { get; set; } = new();

    public Dictionary<string, TrackSegment> SegmentsById { get; set; } = new();

    public Dictionary<string, TrackSpan> SpansById { get; set; } = new();

    public TrackSpan? GetSpan(string id) => SpansById.TryGetValue(id, out TrackSpan span) ? span : null;
}

public enum SerializedSegmentEnd
{
    Start,
    End
}

public sealed class SerializedLocation
{
    public string SegmentId { get; set; } = "";

    public float Distance { get; set; }

    public SerializedSegmentEnd End { get; set; }

    public SerializedLocation()
    {
    }

    public SerializedLocation(SerializableLocation loc)
    {
        SegmentId = loc.segmentId;
        Distance = loc.distance;
        End = loc.end != TrackSegment.End.A ? SerializedSegmentEnd.End : SerializedSegmentEnd.Start;
    }

    internal void Validate(string property, string id, PatchingContext ctx)
    {
        if (ctx == null)
        {
            throw new ArgumentNullException(nameof(ctx));
        }
        if (string.IsNullOrWhiteSpace(SegmentId) || !ctx.SegmentsById.TryGetValue(SegmentId, out TrackSegment segment) || segment == null)
        {
            throw new SCPatchingException($"Location for '{id}' references missing segment '{SegmentId}'.", property + ".SegmentId");
        }
        if (End != SerializedSegmentEnd.Start && End != SerializedSegmentEnd.End)
        {
            throw new SCPatchingException($"Location for '{id}' has an unrecognized segment end: {End}.", property + ".End");
        }
        if (float.IsNaN(Distance) || float.IsInfinity(Distance) || Distance < 0f)
        {
            throw new SCPatchingException($"Location for '{id}' requires a finite, non-negative distance.", property + ".Distance");
        }
        float length = segment.GetLength();
        if (Distance > length)
        {
            throw new SCPatchingException($"Location for '{id}' is outside segment '{SegmentId}' (distance {Distance}, length {length}).", property + ".Distance");
        }
    }

    public static implicit operator SerializableLocation(SerializedLocation loc)
    {
        if (loc == null)
        {
            return default;
        }
        return new SerializableLocation(loc.SegmentId, loc.Distance, loc.End != SerializedSegmentEnd.Start ? TrackSegment.End.B : TrackSegment.End.A);
    }
}

public sealed class SerializedSpan
{
    private static readonly FieldInfo UpperField = typeof(TrackSpan).GetField("_upper", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo LowerField = typeof(TrackSpan).GetField("_lower", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = { new StringEnumConverter() },
    });

    public object? Upper { get; set; }

    public bool Normalize { get; set; }

    public object? Lower { get; set; }

    public SerializedSpan()
    {
    }

    public SerializedSpan(TrackSpan trackSpan)
    {
        if (trackSpan == null)
        {
            throw new ArgumentNullException(nameof(trackSpan));
        }
        Upper = new SerializedLocation((SerializableLocation)UpperField.GetValue(trackSpan));
        Lower = new SerializedLocation((SerializableLocation)LowerField.GetValue(trackSpan));
    }

    internal void ApplyTo(string id, PatchingContext ctx, TrackSpan trackSpan)
    {
        if (ctx == null)
        {
            throw new ArgumentNullException(nameof(ctx));
        }
        if (trackSpan == null)
        {
            throw new ArgumentNullException(nameof(trackSpan));
        }
        SerializedLocation lower = ConvertLocation(Lower, "Lower", id);
        SerializedLocation upper = ConvertLocation(Upper, "Upper", id);
        lower.Validate("Lower", id, ctx);
        upper.Validate("Upper", id, ctx);
        SerializableLocation l = lower;
        SerializableLocation u = upper;
        trackSpan.id = id;
        trackSpan.lower = new Location(ctx.SegmentsById[l.segmentId], l.distance, l.end);
        trackSpan.upper = new Location(ctx.SegmentsById[u.segmentId], u.distance, u.end);
        if (Normalize)
        {
            trackSpan.NormalizeUpperLower();
        }
    }

    private static SerializedLocation ConvertLocation(object? value, string property, string id)
    {
        if (value is SerializedLocation location)
        {
            return location;
        }
        SerializedLocation? converted = null;
        try
        {
            JToken? token = value as JToken ?? (value == null ? null : JToken.FromObject(value, Serializer));
            if (token is JObject)
            {
                converted = token.ToObject<SerializedLocation>(Serializer);
            }
        }
        catch (JsonException e)
        {
            throw new SCPatchingException($"Invalid {property} location for span '{id}': {e.Message}", property);
        }
        return converted ?? throw new SCPatchingException($"Span '{id}' requires a {property} location object.", property);
    }
}
