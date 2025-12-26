using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SignalsEverywhere.Patching;

/// <summary>
/// Applies JSON patches (custom instruction-based) to a JObject.
/// Tracks which JSONPaths were modified and which patch source caused the modification.
/// </summary>
public class PatchHelper
{
    private readonly Dictionary<string, string> _touchedByPath = new(StringComparer.Ordinal);

    public PatchHelper(JObject value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The current JSON value being patched.</summary>
    public JObject Value { get; private set; }

    /// <summary>
    ///     Map: JSONPath -> patchSource that last touched it.
    /// </summary>
    public IReadOnlyDictionary<string, string> TouchedByPath => _touchedByPath;

    public JObject ApplyPatch(string patchSource, JObject patch)
    {
        if (patchSource is null) throw new ArgumentNullException(nameof(patchSource));
        if (patch is null) throw new ArgumentNullException(nameof(patch));

        Value = (JObject)MergeObject(
            patchSource,
            Value,
            Value,
            patch,
            out _,
            out _);

        return Value;
    }

    // -----------------------------
    // Touch tracking
    // -----------------------------

    private void Touch(JToken token, string patchSource)
    {
        _touchedByPath[token.Path] = patchSource;
    }

    private void TouchDeep(JToken token, string patchSource)
    {
        // DescendantsAndSelf() works for any JToken, but we keep intent explicit.
        // TODO: Fix this
    //    foreach (var t in token.DescendantsAndSelf())
    //        Touch(t, patchSource);
    }

    // -----------------------------
    // Object merge (core)
    // -----------------------------

    private JToken MergeObject(
        string patchSource,
        JObject root,
        JObject target,
        JObject patch,
        out bool removedFromParent,
        out bool touchWholeResult)
    {
        removedFromParent = false;
        touchWholeResult = false;

        var instructions = ReadInstructionsOrNull(patch);

        // 1) Object-level instructions ($replace / $moveTo)
        if (instructions is { IsValid: true })
        {
            if (instructions.Find is not null)
                throw new JsonException($"$find is not valid on objects (at {patch.Path})");

            if (instructions.Replace is not null)
            {
                TouchDeep(target, patchSource);
                touchWholeResult = true;
                return instructions.Replace;
            }

            if (!string.IsNullOrWhiteSpace(instructions.MoveTo))
                return MovePropertyInto(
                    patchSource,
                    root,
                    target,
                    patch,
                    instructions.MoveTo!,
                    out removedFromParent,
                    out touchWholeResult);

            // If the object is "instruction-valid" but has no supported instruction.
            throw new JsonException($"Unsupported patch instructions at {patch.Path}");
        }

        // 2) Regular per-property merge
        foreach (var prop in patch.Properties())
        {
            if (IsInstructionProperty(prop.Name))
                continue;

            MergeProperty(patchSource, root, target, prop);
        }

        return target;
    }

    private static bool IsInstructionProperty(string propertyName)
    {
        return propertyName.Length > 0 && propertyName[0] == '$';
    }

    private void MergeProperty(string patchSource, JObject root, JObject target, JProperty patchProperty)
    {
        var name = patchProperty.Name;
        var patchValue = patchProperty.Value;
        var existing = target[name];

        switch (patchValue.Type)
        {
            case JTokenType.Object:
                MergeObjectProperty(patchSource, root, target, name, (JObject)patchValue, existing);
                return;

            case JTokenType.Array:
                MergeArrayProperty(patchSource, root, target, name, (JArray)patchValue, existing);
                return;

            default:
                target[name] = patchValue;
                Touch(target[name]!, patchSource);
                return;
        }
    }

    private void MergeObjectProperty(
        string patchSource,
        JObject root,
        JObject target,
        string propertyName,
        JObject patchObject,
        JToken? existing)
    {
        if (existing is null || existing.Type == JTokenType.Null)
        {
            target[propertyName] = patchObject;
            TouchDeep(target[propertyName]!, patchSource);
            return;
        }

        if (existing.Type == JTokenType.Object)
        {
            var merged = MergeObject(
                patchSource,
                root,
                (JObject)existing,
                patchObject,
                out var removedFromParent,
                out var touchWholeResult);

            if (!removedFromParent)
            {
                target[propertyName] = merged;
                if (touchWholeResult)
                    TouchDeep(target[propertyName]!, patchSource);
            }

            return;
        }

        // existing is primitive/array/etc; patch wants an object => only allow $replace or $remove
        var instr = ReadInstructionsOrNull(patchObject);
        if (instr?.Replace is not null)
        {
            target[propertyName] = instr.Replace;
            Touch(target[propertyName]!, patchSource);
            return;
        }

        if (instr?.Remove == true)
        {
            Touch(existing, patchSource);
            target.Remove(propertyName);
            return;
        }

        throw new NotImplementedException(
            $"Cannot merge object into {existing.Type} at '{existing.Path}'. Use $replace (or $remove).");
    }

    private void MergeArrayProperty(
        string patchSource,
        JObject root,
        JObject target,
        string propertyName,
        JArray patchArray,
        JToken? existing)
    {
        if (existing is not null && existing.Type != JTokenType.Array)
            throw new ArgumentException(
                $"Could not set {patchArray.Path}: target is not an array (was {existing.Type}).");

        // Clone existing array so we don't mutate a shared reference unexpectedly.
        var working = existing is null ? null : new JArray((JArray)existing);
        target[propertyName] = working;

        if (working is null)
        {
            // No target array exists: allow limited instructions inside patch array items.
            NormalizeArrayWhenTargetMissing(patchArray);
            target[propertyName] = patchArray;
            Touch(target[propertyName]!, patchSource);
            return;
        }

        foreach (var patchItem in patchArray.Children())
            MergeArrayItem(patchSource, root, working, patchItem);
    }

    private static void NormalizeArrayWhenTargetMissing(JArray patchArray)
    {
        for (var i = 0; i < patchArray.Count; i++)
        {
            if (patchArray[i] is not JObject obj)
                continue;

            var instr = ReadInstructionsOrNull(obj);
            if (instr is null || !instr.IsValid)
                continue;

            if (instr.Add is not null)
            {
                if (instr.Append is { Length: > 0 })
                    throw new JsonException(
                        $"Error adding elements to {obj.Path}: cannot set $add and $append simultaneously");

                patchArray[i] = instr.Add;
                continue;
            }

            if (instr.Append is { Length: > 0 })
            {
                foreach (var t in instr.Append)
                    patchArray.Add(t);
                continue;
            }

            if (instr.Find is not null && instr.Optional)
                // Optional find against a missing target array => effectively no-op.
                continue;

            throw new NotSupportedException(
                $"Unsupported instructions for {patchArray.Path}[{i}] with no existing target array.");
        }
    }

    // -----------------------------
    // Array item merge
    // -----------------------------

    private void MergeArrayItem(string patchSource, JObject root, JArray sourceArray, JToken patchItem)
    {
        if (patchItem is not JObject patchObj)
            throw new JsonException($"Array patch item at {patchItem.Path} must be an object with instructions.");

        var instr = ReadInstructionsOrNull(patchObj);
        if (instr is null || !instr.IsValid)
            throw new JsonException(
                $"Patch {patchItem.Path} does not specify what to do with the array. " +
                "To replace the entire array, use { \"$replace\": [ ... ] }. " +
                "To add items, use $add or $append. " +
                "To edit individual items, each item must have a $find.");

        // $add / $append apply to the array itself (not to a matched element)
        if (instr.Find is null)
        {
            ApplyArrayWideInstruction(patchSource, sourceArray, instr);
            return;
        }

        // $find targets an existing element
        patchObj.Remove("$find");

        var matchIndex = FindFirstMatchIndex(sourceArray, instr.Find);
        if (matchIndex < 0)
        {
            if (instr.Optional)
                return;

            var criteria = string.Join(" && ", instr.Find.Select(f => f.ToString()));
            throw new JsonException($"Patch {patchItem.Path} could not be applied: no matches found for {criteria}");
        }

        ApplyMatchedArrayElementInstruction(
            patchSource,
            root,
            sourceArray,
            matchIndex,
            patchObj,
            instr);
    }

    private static int FindFirstMatchIndex(JArray sourceArray, IReadOnlyList<PatchFind> conditions)
    {
        for (var i = 0; i < sourceArray.Count; i++)
            if (MatchesAll(sourceArray[i], conditions))
                return i;
        return -1;
    }

    private static bool MatchesAll(JToken candidate, IReadOnlyList<PatchFind> conditions)
    {
        foreach (var cond in conditions)
        {
            if (cond is null)
                throw new JsonException($"Condition is null in $find at {candidate.Path}");

            var path = cond.Path ?? string.Empty;
            var token = candidate.SelectToken(path, true);

            if (token is null)
                return false;

            if (!EvaluateCondition(token, cond))
                return false;
        }

        return true;
    }

    private static bool EvaluateCondition(JToken actual, PatchFind cond)
    {
        switch (cond.Comparison)
        {
            case Comparison.Equals:
                return JToken.DeepEquals(actual, cond.Value);

            case Comparison.NotEquals:
                return !JToken.DeepEquals(actual, cond.Value);

            case Comparison.StartsWith:
                return RequireString(actual, cond.Value, cond).StartsWith(cond.Value!.Value<string>());

            case Comparison.EndsWith:
                return RequireString(actual, cond.Value, cond).EndsWith(cond.Value!.Value<string>());

            case Comparison.Contains:
                return RequireString(actual, cond.Value, cond).Contains(cond.Value!.Value<string>());

            default:
                throw new JsonException($"Undefined comparison in $find at {actual.Path}");
        }
    }

    private static string RequireString(JToken actual, JToken? expected, PatchFind cond)
    {
        if (actual.Type != JTokenType.String)
            throw new JsonException($"Value at {actual.Path} is not a string (needed for {cond.Comparison}).");

        if (expected is null || expected.Type != JTokenType.String)
            throw new JsonException(
                $"Condition value for {cond.Comparison} must be a string (at {expected?.Path ?? "<null>"}).");

        return actual.Value<string>()!;
    }

    private void ApplyMatchedArrayElementInstruction(
        string patchSource,
        JObject root,
        JArray sourceArray,
        int index,
        JObject patchObject,
        PatchInstructions instr)
    {
        var source = sourceArray[index];

        if (instr.Clone)
        {
            var cloned = source.DeepClone();
            if (cloned is not JObject clonedObj)
                throw new NotImplementedException(
                    $"Cannot $clone a non-object array element ({source.Type}) at {source.Path}");

            var merged = MergeObject(
                patchSource,
                root,
                clonedObj,
                patchObject,
                out var removedFromParent,
                out var touchWholeResult);

            if (removedFromParent)
                return;

            sourceArray.Add(merged);

            if (touchWholeResult)
                TouchDeep(merged, patchSource);
            else
                Touch(merged, patchSource);

            return;
        }

        if (instr.Replace is not null)
        {
            sourceArray[index] = instr.Replace;
            Touch(sourceArray[index]!, patchSource);
            return;
        }

        if (instr.Remove)
        {
            Touch(sourceArray[index]!, patchSource);
            sourceArray.RemoveAt(index);
            return;
        }

        if (source is not JObject sourceObj)
            throw new NotImplementedException(
                $"Matched array element is {source.Type}, but patch requires object merge (at {source.Path}).");

        var mergedToken = MergeObject(
            patchSource,
            root,
            sourceObj,
            patchObject,
            out var removedFromParent2,
            out var touchWholeResult2);

        if (removedFromParent2)
            return;

        sourceArray[index] = mergedToken;

        if (touchWholeResult2)
            TouchDeep(mergedToken, patchSource);
    }

    private void ApplyArrayWideInstruction(string patchSource, JArray sourceArray, PatchInstructions instr)
    {
        if (instr.Add is not null)
        {
            if (instr.Append is { Length: > 0 })
                throw new JsonException(
                    $"Error adding elements to {sourceArray.Path}: cannot set $add and $append simultaneously");

            sourceArray.Add(instr.Add);
            Touch(instr.Add, patchSource);
            return;
        }

        if (instr.Append is { Length: > 0 })
        {
            foreach (var t in instr.Append)
            {
                sourceArray.Add(t);
                Touch(t, patchSource);
            }

            return;
        }

        throw new NotImplementedException($"Unsupported array instruction at {sourceArray.Path}");
    }

    // -----------------------------
    // $moveTo handling
    // -----------------------------

    private JToken MovePropertyInto(
        string patchSource,
        JObject root,
        JObject target,
        JObject patchObject,
        string moveToPath,
        out bool removedFromParent,
        out bool touchWholeResult)
    {
        removedFromParent = false;
        touchWholeResult = false;

        var destination = root.SelectToken(moveToPath) as JObject;
        patchObject.Remove("$moveTo");

        if (destination is null)
            throw new JsonException(
                $"Cannot move '{target.Path}': failed to find object at '{moveToPath}' (root '{target.Root.Path}').");

        if (target.Parent is not JProperty parentProp)
            throw new JsonException(
                $"Cannot move '{target.Path}' to '{moveToPath}': only property-moving is supported.");

        // Remove from current location
        Touch(parentProp, patchSource);
        parentProp.Remove();
        removedFromParent = true;

        // Add to destination (merge if same key already exists)
        if (destination[parentProp.Name] is null)
        {
            destination.Add(parentProp);
            Touch(parentProp, patchSource);
            return destination[parentProp.Name]!;
        }

        var wrapperPatch = new JObject(new JProperty(parentProp));
        var merged = MergeObject(
            patchSource,
            root,
            destination,
            wrapperPatch,
            out var removedFromParent2,
            out var touchWholeResult2);

        if (touchWholeResult2)
            TouchDeep(merged, patchSource);

        // Note: removedFromParent2 refers to removal inside merge under destination; keep original removedFromParent = true.
        touchWholeResult = touchWholeResult2;
        return merged;
    }

    // -----------------------------
    // Instruction parsing types
    // -----------------------------

    private static PatchInstructions? ReadInstructionsOrNull(JObject obj)
    {
        // "Valid instruction object" means it has at least one recognized instruction property.
        // This avoids treating every regular object as an instruction object.
        var hasAnyInstruction =
            obj.Property("$replace", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$remove", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$moveTo", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$find", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$add", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$append", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$clone", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$optional", StringComparison.OrdinalIgnoreCase) is not null;

        if (!hasAnyInstruction)
            return null;

        var instr = obj.ToObject<PatchInstructions>();
        return instr;
    }
}

public sealed class PatchInstructions
{
    [JsonProperty("$replace")] public JToken? Replace { get; set; }

    [JsonProperty("$remove")] public bool Remove { get; set; }

    [JsonProperty("$moveTo")] public string? MoveTo { get; set; }

    [JsonProperty("$find")] public PatchFind[]? Find { get; set; }

    [JsonProperty("$optional")] public bool Optional { get; set; }

    [JsonProperty("$clone")] public bool Clone { get; set; }

    [JsonProperty("$add")] public JToken? Add { get; set; }

    [JsonProperty("$append")] public JToken[]? Append { get; set; }

    [JsonIgnore]
    public bool IsValid =>
        Replace is not null ||
        Remove ||
        !string.IsNullOrWhiteSpace(MoveTo) ||
        Find is { Length: > 0 } ||
        Add is not null ||
        Append is { Length: > 0 } ||
        Clone ||
        Optional; // optional is only meaningful with $find, but we allow parsing it
}

public sealed class PatchFind
{
    [JsonProperty("path")] public string? Path { get; set; }

    [JsonProperty("comparison")] public Comparison Comparison { get; set; } = Comparison.Equals;

    [JsonProperty("value")] public JToken? Value { get; set; }

    public override string ToString()
    {
        return $"{Path} {Comparison} {Value}";
    }
}

public enum Comparison
{
    Equals = 0,
    NotEquals = 1,
    StartsWith = 2,
    EndsWith = 3,
    Contains = 4
}