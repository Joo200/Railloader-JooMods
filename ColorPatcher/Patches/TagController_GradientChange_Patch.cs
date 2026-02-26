using System.Collections.Generic;
using HarmonyLib;
using Model;
using Model.Ops;
using Serilog;
using StrangeCustoms.Tracks;
using UI;
using UI.Tags;
using UnityEngine;
using Color = UnityEngine.Color;
using ILogger = Serilog.ILogger;

namespace ColorPatcher.Patches;

[HarmonyPatchCategory("ColorPatcherMod")]
public class AreaColor_GradientChange_Patch
{
    private static readonly ILogger logger = Log.ForContext<ColorPatcherMod.ColorPatcherMod>();

    public struct TagCalloutColor {
        public Color First;
        public Color Second;
    }

    private static Shader? DefaultShader;
    private static Material? DefaultMaterial;
    
    public static readonly Dictionary<string, TagCalloutColor> ColorSetup = new();

    [HarmonyPatch(typeof(SerializedArea), "set_TagColor")]
    [HarmonyPrefix]
    private static void SerializedArea_TagColor_Set_Prefix(SerializedArea __instance, float[] value)
    {
        // Ensure indexes 4,5,6 exist
        if (value == null || value.Length < 6)
            return;

        // Attach or replace color data
        int offset = value.Length == 9 ? 3 : 0;
        var setup = new TagCalloutColor();
        setup.First = new Color(value[offset + 0], value[offset + 1], value[offset + 2]);
        setup.Second = new Color(value[offset + 3], value[offset + 4], value[offset + 5]);
        ColorSetup[__instance.Name.ToLower()] = setup;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TagController), "UpdateTag")]
    private static bool Update(TagController __instance, Car car, TagCallout tagCallout, OpsController opsController)
    {
        if (car == null)
            return false;
        tagCallout.gameObject.SetActive(true);
        if (opsController != null && opsController.TryGetDestinationInfo(car, out var destinationName,
                out var isAtDestination,
                out var destinationPosition, out var destination))
        {
            tagCallout.callout.Text = destinationName;
            tagCallout.callout.Layout();
            
            tagCallout.locationIndicatorHoverArea.spanIds.Clear();
            foreach (var span in destination.Spans)
                tagCallout.locationIndicatorHoverArea.spanIds.Add(span.id);
            tagCallout.locationIndicatorHoverArea.descriptors.Clear();
            tagCallout.locationIndicatorHoverArea.descriptors.Add(
                new LocationIndicatorController.Descriptor(destinationPosition, destinationName, null));
            var area = opsController.AreaForCarPosition(destination);
            var color = Color.gray;
            if (area != null)
                color = area.tagColor;
            ApplyImageColor(tagCallout, area, color, isAtDestination);
        }
        else
        {
            string trainName;
            tagCallout.callout.Text = !car.TryGetTrainName(out trainName) ? null : trainName;
            tagCallout.callout.Layout();
            ApplyImageColor(tagCallout, null, Color.gray, false);
        }

        return false;
    }
    
    private static void ApplyImageColor(TagCallout tagCallout, Area? area, Color color, bool isAtDestination)
    {
        var first = true;
        foreach (var colorImage in tagCallout.colorImages)
            if (first)
            {
                if (DefaultMaterial == null)
                {
                    DefaultMaterial = Object.Instantiate(colorImage.material);
                }
                
                if (ColorSetup.TryGetValue(area?.name.ToLower() ?? "", out var setup))
                {
                    Color firstColor = isAtDestination ? setup.First * 0.5f : setup.First;
                    Color secondColor = isAtDestination ? setup.Second * 0.5f : setup.Second;
                    
                    var mat = new Material(ColorPatcherMod.ColorPatcherMod.Shader);
                    colorImage.color = isAtDestination ? Color.white * 0.5f : Color.white;
                    mat.SetColor("_Color", isAtDestination ? Color.white * 0.5f : Color.white);
                    mat.SetColor("_ColorA", secondColor);
                    mat.SetColor("_ColorB", firstColor);
                    mat.SetFloat("_Axis", 0f);
                    var r = colorImage.rectTransform.rect;
                    mat.SetVector("_GradientRect", new Vector4(r.xMin, r.yMin, r.xMax, r.yMax));
                    colorImage.material = mat;
                }
                else
                {
                    colorImage.material = Object.Instantiate(DefaultMaterial);
                    colorImage.color = isAtDestination ? color * 0.5f : color;
                }
                
                first = false;
            }
            else
            {
                if (isAtDestination)
                    color *= 0.5f;
                colorImage.color = color;
            }
    }
}