using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Model.Definition;
using Model.Ops;
using TMPro;
using UI.Map;
using UnityEngine;
using UnityEngine.UI;

namespace ColorPatcher.Patches;

public class MapEnhancer_Patch
{
    public static void Patch(Harmony harmony)
    {
        var type = AccessTools.TypeByName("MapEnhancer.MapEnhancer");
        if (type == null)
        {
            return;
        }

        var method = AccessTools.Method(type, "TraincarColorUpdater");
        if (method == null)
        {
            return;
        }

        var prefix = AccessTools.Method(typeof(MapEnhancer_Patch), nameof(PrefixTraincarColorUpdater));
        harmony.Patch(method, prefix: new HarmonyMethod(prefix));
    }

    public static bool PrefixTraincarColorUpdater(object __instance, ref IEnumerator __result)
    {
        __result = TraincarColorUpdater(__instance);
        return false;
    }

    private static AccessTools.FieldRef<MapBuilder, HashSet<MapIcon>> mapIcons = AccessTools.FieldRefAccess<MapBuilder, HashSet<MapIcon>>("_mapIcons");

    private static IEnumerator TraincarColorUpdater(object instance)
    {
        while (true)
        {
            var tmpIcons = mapIcons(MapBuilder.Shared);
            if (tmpIcons == null || tmpIcons.Count == 0)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }
            var mapIconArray = new HashSet<MapIcon>(tmpIcons);
            foreach (var icon in mapIconArray)
            {
                if (icon == null)
                    continue;
                var car = icon.transform.parent.GetComponent<Model.Car>();
                if (car == null)
                    continue;
                var image = icon.GetComponentInChildren<Image>();
                var text = icon.GetComponentInChildren<TMP_Text>();
                if (text != null) text.gameObject.SetActive(!car.IsInBardo);
                if (image != null) image.gameObject.SetActive(!car.IsInBardo);
                if (!car.Archetype.IsFreight() || image == null)
                    continue;
                
                OpsController shared = OpsController.Shared;
                if (shared != null && shared.TryGetDestinationInfo(car, out string _, out var isAtDestination,
                        out Vector3 _, out var destination))
                {
                    Area ?area = shared.AreaForCarPosition(destination);
                    if (area == null)
                    {
                        SetColor(image, false, Color.white);
                    }
                    else if (AreaColor_GradientChange_Patch.ColorSetup.TryGetValue(area.name.ToLower(), out var colors))
                    {
                        
                        SetColor(image, isAtDestination, colors.First, colors.Second);
                    }
                    else
                    {
                        SetColor(image, isAtDestination, area.tagColor);
                    }
                }
                else
                {
                    SetColor(image, false, Color.white);
                }
                    
                yield return null;
            }
            yield return null;
        }
    }

    private static (Color, Color) CalculateEffectiveColors(Color a, Color b, bool isAtDestination)
    {
        var maxColorComponent = Mathf.Max(a.maxColorComponent, b.maxColorComponent);
        bool isModdedArea = maxColorComponent >= 0.99f;
        bool shouldBrighten = isModdedArea ? isAtDestination : !isAtDestination;
        if (shouldBrighten)
        {
            if (maxColorComponent < 0.99f)
            {
                float intensity = 1f / maxColorComponent;
                intensity = Math.Min(intensity, 2f);
                a *= intensity;
                b *= intensity;
                a.a = 1f;
                b.a = 1f;
            }
            else
            {
                Color.RGBToHSV(a, out var ha, out var sa, out var v1a);
                float v2a = v1a * 0.6f;
                a = Color.HSVToRGB(ha, sa, v2a);
                a.a = 1f;
                Color.RGBToHSV(a, out var h, out var s, out var v1);
                float v2 = v1 * 0.6f;
                b = Color.HSVToRGB(h, s, v2);
                b.a = 1f;
            }
        }

        return (a, b);
    }

    private static Color CalculateEffectiveColor(Color rgbColor, bool isAtDestination)
    {
        var maxColorComponent = rgbColor.maxColorComponent;
        bool isModdedArea = maxColorComponent >= 0.99f;
        bool shouldBrighten = isModdedArea ? isAtDestination : !isAtDestination;
        if (shouldBrighten)
        {
            if (maxColorComponent < 0.99f)
            {
                float intensity = 1f / maxColorComponent;
                intensity = Math.Min(intensity, 2f);
                rgbColor *= intensity;
                rgbColor.a = 1f;
            }
            else
            {
                Color.RGBToHSV(rgbColor, out var h, out var s, out var v1);
                float v2 = v1 * 0.6f;
                rgbColor = Color.HSVToRGB(h, s, v2);
                rgbColor.a = 1f;
            }
        }
        return rgbColor;
    }

    private static Material? DefaultMaterial = null;
    private static void SetColor(Image image, bool isAtDestination, Color first, Color ?second = null)
    {
        if (DefaultMaterial == null)
        {
            DefaultMaterial = image.material;
        }

        if (second != null)
        {
            var (a, b) = CalculateEffectiveColors(second.Value, first, isAtDestination);
            
            var mat = new Material(ColorPatcherMod.ColorPatcherMod.Shader);
            image.color = Color.white;
            mat.SetColor("_Color", Color.white);
            mat.SetColor("_ColorA", a);
            mat.SetColor("_ColorB", b);
            mat.SetFloat("_Axis", 0f);
            var r = image.rectTransform.rect;
            mat.SetVector("_GradientRect", new Vector4(r.xMin, r.yMin, r.xMax, r.yMax));
            image.material = mat;
        }
        else
        {
            first = CalculateEffectiveColor(first, isAtDestination);
            image.material = DefaultMaterial;
            image.color = first;
        }
    }
}