using System.Reflection;
using HarmonyLib;
using Helpers;
using StrangeCustoms.Tracks;
using Track;
using Track.Signals;
using UnityEngine;

namespace SignalsEverywhere.Signals;

public abstract class SerializedCTCSignal
{
    private static readonly AccessTools.FieldRef<TrackMarker, SerializableLocation> _location = AccessTools.FieldRefAccess<TrackMarker, SerializableLocation>(nameof (_location));
    
    private static readonly AccessTools.FieldRef<CTCSignalModelController, bool[]> _activeByDefault = AccessTools.FieldRefAccess<CTCSignalModelController, bool[]>(nameof(_activeByDefault));
    
    public string Id { get; set; }
    public string ModelType { get; set; } = SignalPrefabStore.DefaultType;
    public SignalHeadConfiguration HeadConfiguration { get; set; }
    public CTCDirection Direction { get; set; }
    public SerializedLocation Location { get; set; }

    public bool LeftSide { get; set; } = false;
    public int Offset { get; set; } = 3;
    
    public SerializedCTCSignal() {}

    public SerializedCTCSignal(CTCSignal signal)
    {
        Id = signal.id;
        HeadConfiguration = signal.headConfiguration;
        Direction = signal.direction;
        Location = new SerializedLocation(_location(signal.GetComponent<TrackMarker>()));
    }

    internal void ApplyTo(CTCSignal signal, PatchingContext ctx)
    {
        if (Location == null) throw new SCPatchingException("Signal " + Id + " is missing location", "signals." + Id + ".location");
        CallValidate(Location, Id, ctx);

        signal.transform.name = Id;
        signal.id = Id;
        signal.headConfiguration = HeadConfiguration;
        signal.direction = Direction;

        var marker = signal.GetComponent<TrackMarker>();
        marker.id = "signal_" + Id;
        _location(marker) = Location;

        SerializableLocation l = Location;
        var loc = new Location(ctx.SegmentsById[l.segmentId], l.distance, l.end);
        if (loc.segment != null)
        {
            var posRot = Graph.Shared.GetPositionRotation(loc);
            Vector3 offset = posRot.Rotation * Vector3.right * (Offset * (LeftSide ? -1.2F : 1));
            signal.transform.position = posRot.Position.GameToWorld() + offset;

            var rotation = posRot.Rotation * Quaternion.Euler(0, 180, 0);
            if (System.DateTime.Now.Month == 4 && System.DateTime.Now.Day == 1)
            {
                rotation *= Quaternion.Euler(
                    Random.Range(0f, 10f),
                    Random.Range(0f, 10f),
                    Random.Range(0f, 10f)
                );
            }
            signal.transform.rotation = rotation;
        }
        
        RemoveSignalColorizers(signal.gameObject, ctx);

        signal.modelController = signal.gameObject.GetComponentInChildren<CTCSignalModelController>();
        _activeByDefault(signal.modelController) = null; // reset head configuration
        signal.gameObject.SetActive(true);
    }
    
    private static void RemoveSignalColorizers(GameObject root, PatchingContext ctx)
    {
        // Get all MonoBehaviour components in this object and its children
        // (We use MonoBehaviour because we can't reference the specific class type)
        var components = root.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (var comp in components)
        {
            if (comp == null) continue;

            // Check if the type name matches the one we want to remove
            if (comp.GetType().FullName == "MapEnhancer.MapEnhancer+SignalIconColorizer")
            {
                ctx.Logger.Information($"Compatibility Fix: Removing {comp.GetType().FullName} from {root.name}");
                Object.DestroyImmediate(comp);
            }
        }
    }

    static void CallValidate(SerializedLocation location, string id, PatchingContext ctx)
    {
        location.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(location, [
            "Location", id, ctx
        ]);
    }
}