using System;
using System.Reflection;
using GalaSoft.MvvmLight.Messaging;
using Game.Events;
using Railloader;
using Serilog;
using HarmonyLib;
using UnityEngine;
using ILogger = Serilog.ILogger;

namespace ColorPatcherMod;

public class ColorPatcherMod : PluginBase
{
    private static ILogger logger = Log.ForContext<ColorPatcherMod>();

    private readonly IModDefinition _modDefinition;

    public static Shader? Shader = null;
        
    public ColorPatcherMod(IModdingContext moddingContext, IModDefinition self)
    {
        _modDefinition = self;
        
        var asset = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("ColorPatcher.Resources.tagcalloutshader"));
        logger.Information($"Loaded asset bundle: {string.Join(", ", asset.GetAllAssetNames())}");
        var shader = asset.LoadAsset<Shader>("Assets/TagCalloutGradient/TagCalloutShader.shader");
        if (shader == null)
        {
            logger.Error("Couldn't load shader");
            return;
        }
        Shader = shader;
    }

    public override void OnEnable()
    {
        if (!Shader)
        {
            return;
        }
        var harmony = new Harmony(_modDefinition.Id);
        harmony.PatchCategory("ColorPatcherMod");
        
        Messenger.Default.Register<MapDidLoadEvent>(this, _ =>
        {
            try
            {
                ColorPatcher.Patches.MapEnhancer_Patch.Patch(harmony);
            }
            catch (Exception e)
            {
                logger.Information($"Failed to patch MapEnhancer: {e}");
            }
        });
    }

    public override void OnDisable()
    {
        var harmony = new Harmony(_modDefinition.Id);
        harmony.UnpatchCategory("ColorPatcherMod");
        harmony.UnpatchAll(_modDefinition.Id);
    }
}