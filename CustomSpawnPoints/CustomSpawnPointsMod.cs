using System;
using System.Collections.Generic;
using System.IO;
using CustomSpawnPoints.Config;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Railloader;
using Serilog;
using StrangeCustoms.Tracks;

namespace CustomSpawnPoints;

public class CustomSpawnPointsMod : SingletonPluginBase<CustomSpawnPointsMod>
{
    IModdingContext _moddingContext;
    IModDefinition _modDefinition;

    public List<SerializedSetupDescriptor> SpawnPoints { get; private set; } = new();
    
    public string ModDirectory => _modDefinition.Directory;
    
    public CustomSpawnPointsMod(IModdingContext moddingContext, IModDefinition self)
    {
        _moddingContext = moddingContext;
        _modDefinition = self;
        
        _moddingContext.RegisterConsoleCommand(new SpawnPointsCommand());
    }

    public override void OnEnable()
    {
        ConvertMixintos();
        
        var harmony = new Harmony(_modDefinition.Id);
        harmony.PatchCategory("CustomSpawnPoints");
    }

    public override void OnDisable()
    {
        var harmony = new Harmony(_modDefinition.Id);
        harmony.UnpatchAll();
    }

    public void ConvertMixintos()
    {
        foreach (var mixinto in _moddingContext.GetMixintos("spawnPoints"))
        {
            try
            {
                Log.Information($"Processing mixinto {mixinto.Mixinto}");
                ExtractMixinto(mixinto);
            } catch (Exception ex)
            {
                Log.Error($"Failed to process mixinto {mixinto.Mixinto}: {ex.Message}");
            }
        }
    }

    private void ExtractMixinto(ModMixinto mixinto)
    {
        var path = Path.GetFullPath(mixinto.Mixinto);
        if (!path.StartsWith(_moddingContext.ModsBaseDirectory))
            Log.Warning($"Mixinto {mixinto.Mixinto} is not in the mods directory.");
        var json = JObject.Parse(File.ReadAllText(mixinto.Mixinto));
        json.Remove("$schema");
        var converted = json.ToObject<SerializedSetupDescriptor>();
        if (converted == null)
        {
            Log.Error($"Failed to convert mixinto {mixinto.Mixinto}");
            return;
        }

        SpawnPoints.Add(converted);
    }
}