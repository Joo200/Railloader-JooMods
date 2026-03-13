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
        Value = new JObject(value) ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>The current JSON value being patched.</summary>
    public JObject Value { get; private set; }

    /// <summary>
    ///     Map: JSONPath -> patchSource that last touched it.
    /// </summary>
    public IReadOnlyDictionary<string, string> TouchedByPath => _touchedByPath;

    /// <summary>
    ///     List of all JSONPaths that were modified by any patch.
    /// </summary>
    public IEnumerable<string> TouchedKeys => _touchedByPath.Keys;

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
        var path = NormalizePath(token.Path);
        _touchedByPath[path] = patchSource;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        // 1) Replace ['key'] with .key
        // 2) Replace [0] with .0 (or just 0 if we want to follow the user's "BR-HW.0" style)
        // User wants: BR-HW.BR-HW 1.blocks.bryson-ww1.spans
        // JToken.Path gives: BR-HW['BR-HW 1'].blocks.bryson-ww1.spans

        var result = path
            .Replace("['", ".")
            .Replace("']", "")
            .Replace("[", ".")
            .Replace("]", "");

        if (result.StartsWith("."))
            result = result.Substring(1);

        return result;
    }

    private void TouchDeep(JToken token, string patchSource)
    {
        Touch(token, patchSource);
        if (token is JContainer container)
        {
            foreach (var t in container.Descendants())
                Touch(t, patchSource);
        }
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
        
        // Find existing property case-insensitively
        var existingProp = target.Property(name, StringComparison.OrdinalIgnoreCase);
        var existing = existingProp?.Value;
        var targetName = existingProp?.Name ?? name;

        switch (patchValue.Type)
        {
            case JTokenType.Object:
                MergeObjectProperty(patchSource, root, target, targetName, (JObject)patchValue, existing);
                return;

            case JTokenType.Array:
                MergeArrayProperty(patchSource, root, target, targetName, (JArray)patchValue, existing);
                return;

            default:
                if (existing is null || !JToken.DeepEquals(existing, patchValue))
                {
                    target[targetName] = patchValue;
                    TouchDeep(target[targetName]!, patchSource);
                }
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
        var instr = ReadInstructionsOrNull(patchObject);

        if (existing is null || existing.Type == JTokenType.Null)
        {
            if (instr is { IsValid: true })
            {
                if (instr.Replace is not null)
                {
                    target[propertyName] = instr.Replace;
                    TouchDeep(target[propertyName]!, patchSource);
                    return;
                }

                if (instr.Remove)
                {
                    if (existing is not null)
                        TouchDeep(existing, patchSource);
                    target.Remove(propertyName);
                    return;
                }

                // If it's $add, $append, $find, $index, etc., we can't really apply them to a missing object 
                // unless we want to support creating it. NormalizeArrayWhenTargetMissing handles arrays.
                // For now, let's treat it as a generic instruction that might not be supported.
            }

            if (existing is null || existing.Type == JTokenType.Null)
            {
                target[propertyName] = patchObject;
                TouchDeep(target[propertyName]!, patchSource);
                return;
            }
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
        if (instr?.Replace is not null)
        {
            target[propertyName] = instr.Replace;
            TouchDeep(target[propertyName]!, patchSource);
            return;
        }

        if (instr?.Remove == true)
        {
            TouchDeep(existing, patchSource);
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

        if (working is null)
        {
            // No target array exists: allow limited instructions inside patch array items.
            NormalizeArrayWhenTargetMissing(patchArray);
            target[propertyName] = patchArray;
            TouchDeep(target[propertyName]!, patchSource);
            return;
        }

        foreach (var patchItem in patchArray.Children())
            MergeArrayItem(patchSource, root, working, patchItem);
        
        if (!JToken.DeepEquals(existing, working))
        {
            target[propertyName] = working;
            // Note: We don't TouchDeep(working) here because MergeArrayItem/ApplyMatchedArrayElementInstruction
            // handles touching specific elements.
        }
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

            if (instr.Index.HasValue && instr.Optional)
                // Optional index against a missing target array => effectively no-op.
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
                "To edit individual items, each item must have a $find or $index.");

        // $add / $append apply to the array itself (not to a matched element)
        if (instr.Find is null && !instr.Index.HasValue)
        {
            ApplyArrayWideInstruction(patchSource, sourceArray, instr);
            return;
        }

        if (instr.SelectAll)
        {
            if (instr.Index.HasValue)
                throw new JsonException($"Patch {patchItem.Path} cannot use $selectAll with $index.");
            
            if (instr.Find is null)
                throw new JsonException($"Patch {patchItem.Path} must use $find with $selectAll.");

            patchObj.Remove("$selectAll");
            patchObj.Remove("$find");

            var indices = FindAllMatchIndices(sourceArray, instr.Find);
            if (indices.Count == 0)
            {
                if (instr.Optional)
                    return;

                var criteria = string.Join(" && ", instr.Find.Select(f => f.ToString()));
                throw new JsonException($"Patch {patchItem.Path} could not be applied: no matches found for {criteria}");
            }

            // Apply in reverse to handle removals correctly.
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                ApplyMatchedArrayElementInstruction(
                    patchSource,
                    root,
                    sourceArray,
                    indices[i],
                    patchObj,
                    instr);
            }

            return;
        }

        int matchIndex;
        if (instr.Index.HasValue)
        {
            matchIndex = instr.Index.Value;
            patchObj.Remove("$index");
        }
        else
        {
            // $find targets an existing element
            patchObj.Remove("$find");
            matchIndex = FindFirstMatchIndex(sourceArray, instr.Find!);
        }

        if (matchIndex < 0 || matchIndex >= sourceArray.Count)
        {
            if (instr.Optional)
                return;

            if (instr.Index.HasValue)
                throw new JsonException($"Patch {patchItem.Path} could not be applied: index {matchIndex} out of range (count {sourceArray.Count})");

            var criteria = string.Join(" && ", instr.Find!.Select(f => f.ToString()));
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

    private static List<int> FindAllMatchIndices(JArray sourceArray, IReadOnlyList<PatchFind> conditions)
    {
        var indices = new List<int>();
        for (var i = 0; i < sourceArray.Count; i++)
            if (MatchesAll(sourceArray[i], conditions))
                indices.Add(i);
        return indices;
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
                if (actual.Type == JTokenType.String && cond.Value?.Type == JTokenType.String)
                {
                    return string.Equals(actual.Value<string>(), cond.Value.Value<string>(), StringComparison.OrdinalIgnoreCase);
                }
                return JToken.DeepEquals(actual, cond.Value);

            case Comparison.NotEquals:
                if (actual.Type == JTokenType.String && cond.Value?.Type == JTokenType.String)
                {
                    return !string.Equals(actual.Value<string>(), cond.Value.Value<string>(), StringComparison.OrdinalIgnoreCase);
                }
                return !JToken.DeepEquals(actual, cond.Value);

            case Comparison.StartsWith:
                return RequireString(actual, cond.Value, cond).StartsWith(cond.Value!.Value<string>(), StringComparison.OrdinalIgnoreCase);

            case Comparison.EndsWith:
                return RequireString(actual, cond.Value, cond).EndsWith(cond.Value!.Value<string>(), StringComparison.OrdinalIgnoreCase);

            case Comparison.Contains:
                return RequireString(actual, cond.Value, cond).IndexOf(cond.Value!.Value<string>(), StringComparison.OrdinalIgnoreCase) >= 0;

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

            TouchDeep(merged, patchSource);

            return;
        }

        if (instr.Replace is not null)
        {
            sourceArray[index] = instr.Replace;
            TouchDeep(sourceArray[index]!, patchSource);
            return;
        }

        if (instr.Remove)
        {
            TouchDeep(sourceArray[index]!, patchSource);
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
            TouchDeep(instr.Add, patchSource);
            return;
        }

        if (instr.Append is { Length: > 0 })
        {
            foreach (var t in instr.Append)
            {
                sourceArray.Add(t);
                TouchDeep(t, patchSource);
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
        parentProp.Remove();
        removedFromParent = true;

        // Add to destination (merge if same key already exists case-insensitively)
        var existingProp = destination.Property(parentProp.Name, StringComparison.OrdinalIgnoreCase);
        if (existingProp is null)
        {
            destination.Add(parentProp);
            TouchDeep(parentProp.Value, patchSource);
            touchWholeResult = true;
            return destination[parentProp.Name]!;
        }

        // Rename parentProp to match existing property name so MergeObject finds it correctly
        var wrapperPatch = new JObject(new JProperty(existingProp.Name, parentProp.Value));
        var merged = MergeObject(
            patchSource,
            root,
            destination,
            wrapperPatch,
            out var removedFromParent2,
            out var touchWholeResult2);

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
            obj.Property("$index", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$add", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$append", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$clone", StringComparison.OrdinalIgnoreCase) is not null ||
            obj.Property("$selectAll", StringComparison.OrdinalIgnoreCase) is not null ||
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

    [JsonProperty("$index")] public int? Index { get; set; }

    [JsonProperty("$selectAll")] public bool SelectAll { get; set; }

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
        Index.HasValue ||
        SelectAll ||
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