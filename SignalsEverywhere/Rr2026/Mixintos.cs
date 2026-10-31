using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SignalsEverywhere.Rr2026;

// What Railloader handed SignalsEverywhere on Legacy: a mod's file for a mixinto identifier ("signals", "ctcPanel").
public class ModMixinto(string mixinto, ModMixinto.ModSource source)
{
    public string Mixinto { get; } = mixinto;

    public ModSource Source { get; } = source;

    public class ModSource(string id)
    {
        public string Id { get; } = id;
    }
}

// Railroader 2026.1 has no Railloader to collect mixintos, so SignalsEverywhere reads them itself, in each mod folder
// the game has loaded: a "mixintos" object in Info.json, or in a Definition.json next to it (signal mods keep theirs,
// "mixintos": { "signals": [ "file(signals.json)" ] }). Mods come in dependency order, as Railloader ordered them
// (a mod's patches build on the ones it requires or loads after), then by identifier.
public static class Mixintos
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext(typeof(Mixintos));

    private static readonly Regex FileRef = new(@"^file\((.+)\)$", RegexOptions.CultureInvariant);

    private sealed class Mod(string id, string directory, JObject mixintos, HashSet<string> after)
    {
        public string Id { get; } = id;
        public string Directory { get; } = directory;
        public JObject Mixintos { get; } = mixintos;
        public HashSet<string> After { get; } = after;
    }

    public static IEnumerable<ModMixinto> Find(string modsDirectory, string identifier, Func<string, bool> isLoaded)
    {
        foreach (Mod mod in Ordered(Mods(modsDirectory, isLoaded)))
        {
            if (mod.Mixintos[identifier] is not JArray files)
            {
                continue;
            }
            foreach (JToken entry in files)
            {
                string text = entry.Type == JTokenType.String ? (string)entry! : "";
                Match match = FileRef.Match(text.Trim());
                string relative = match.Success ? match.Groups[1].Value : text.Trim();
                if (relative.Length == 0)
                {
                    continue;
                }
                yield return new ModMixinto(Path.Combine(mod.Directory, relative), new ModMixinto.ModSource(mod.Id));
            }
        }
    }

    private static List<Mod> Mods(string modsDirectory, Func<string, bool> isLoaded)
    {
        var mods = new List<Mod>();
        foreach (string directory in System.IO.Directory.GetDirectories(modsDirectory))
        {
            try
            {
                JObject? info = Read(Path.Combine(directory, "Info.json"));
                string? id = (string?)info?["identifier"];
                if (info == null || string.IsNullOrEmpty(id) || !isLoaded(id!))
                {
                    continue;
                }
                JObject? definition = Read(Path.Combine(directory, "Definition.json"));
                JObject? mixintos = info["mixintos"] as JObject ?? definition?["mixintos"] as JObject;
                if (mixintos == null)
                {
                    continue;
                }
                var after = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddIds(after, info["dependencies"]?["required"]);
                AddIds(after, info["dependencies"]?["optional"]);
                AddIds(after, info["dependencies"]?["loadAfter"]);
                AddIds(after, definition?["requires"]);
                AddIds(after, definition?["loadAfter"]);
                mods.Add(new Mod(id!, directory, mixintos, after));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Couldn't read the mod in {directory}", directory);
            }
        }
        return mods;
    }

    // Dependency ids are strings, possibly with a version ("tetz95/x@1.0"), or Railloader's { "id": ... } objects.
    private static void AddIds(HashSet<string> ids, JToken? list)
    {
        if (list is not JArray array)
        {
            return;
        }
        foreach (JToken item in array)
        {
            string? id = item.Type == JTokenType.Object ? (string?)item["id"] : item.Type == JTokenType.String ? (string?)item : null;
            if (!string.IsNullOrEmpty(id))
            {
                ids.Add(id!.Split('@')[0].Trim());
            }
        }
    }

    private static IEnumerable<Mod> Ordered(List<Mod> mods)
    {
        var byId = mods.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Mod>();
        void Visit(Mod mod)
        {
            if (done.Contains(mod.Id) || !visiting.Add(mod.Id))
            {
                return;
            }
            foreach (string dependency in mod.After.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                if (byId.TryGetValue(dependency, out Mod other))
                {
                    Visit(other);
                }
            }
            visiting.Remove(mod.Id);
            done.Add(mod.Id);
            result.Add(mod);
        }
        foreach (Mod mod in mods.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase))
        {
            Visit(mod);
        }
        return result;
    }

    // Whether a mod folder is one the game has loaded (its Info.json identifier).
    public static bool IsLoadedMod(string directory, Func<string, bool> isLoaded)
    {
        try
        {
            string? id = (string?)Read(Path.Combine(directory, "Info.json"))?["identifier"];
            return !string.IsNullOrEmpty(id) && isLoaded(id!);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static JObject? Read(string path)
    {
        return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
    }
}
