using System;
using System.Linq;
using System.Reflection;
using Helpers;
using Serilog;
using TMPro;
using UI.Builder;
using UnityEngine;
using UnityEngine.UI;

namespace SignalsEverywhere.Panel;

public static class ViewCreator
{
    public static readonly float GridCellSize = 24f;
    public static readonly float GridCellSizeX = 1.5f * 24f;
    public static readonly float FixedSchematicHeight = 8 * 24f;
    public static readonly float FixedControlRowHeight = 250f;
    public static readonly Color TrackColor = new Color(0.4f, 0.4f, 0.4f);

    public static RectTransform CreateSchematic(Schematic schematic, UIPanelBuilder builder, float scale)
    {
        float scaledGridCellSize = GridCellSize * scale;
        float scaledGridCellSizeX = GridCellSizeX * scale;
        
        var elements = schematic.Elements;
        // 1. Create the dark background panel
        var type = typeof(UIPanelBuilder);
        var rootRect = type.GetMethod("CreateRectView", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(builder, ["DisplayRow", 50, 50]) as RectTransform;
        rootRect.anchorMin = new Vector2(0, 1);
        rootRect.anchorMax = new Vector2(0, 1);
        rootRect.pivot = new Vector2(0, 1);
        rootRect.gameObject.layer = Layers.UI;
        
        // Background Image (Dark Gray CTC style)
        var bg = rootRect.gameObject.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        bg.raycastTarget = true;
        
        // Calculate bounds for size
        float maxX = schematic.MaxX;
        float minY = schematic.MinY;
        float maxY = schematic.MaxY;
        float finalWidth = maxX * scaledGridCellSizeX;
        float totalCellsY = maxY - minY + 1;
        float finalHeight = totalCellsY * scaledGridCellSize;
        rootRect.sizeDelta = new Vector2(finalWidth, finalHeight);

        // Add LayoutElement to tell UIPanelBuilder how big we are
        var layout = rootRect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = finalWidth;
        layout.preferredHeight = finalHeight;
        layout.minWidth = finalWidth; // Added to prevent squeezing
        layout.minHeight = finalHeight;
        var ch = rootRect.gameObject.AddComponent<ClickHandler>();
        ch.Schematic = schematic;

        // 2. Place elements
        foreach (var element in elements.OrderBy(e => e.Type != CTCPanelLayout.LayoutType.Light ? 0 : 1))
        {
            // Position relative to top of schematic (minY corresponds to the top-most row)
            float relativeY = element.Y - minY;
            Vector2 centerPos = new Vector2(element.X * scaledGridCellSizeX + scaledGridCellSizeX / 2, -relativeY * scaledGridCellSize - scaledGridCellSize / 2);
            float trackWidth = 4f * scale;
            float diagLength = Mathf.Sqrt(scaledGridCellSizeX * scaledGridCellSizeX + scaledGridCellSize * scaledGridCellSize);
            float angleDeg = Mathf.Atan2(scaledGridCellSize, scaledGridCellSizeX) * Mathf.Rad2Deg;
            
            switch (element.Type)
            {
                case CTCPanelLayout.LayoutType.Label:
                    var label = CreateLabel(rootRect.gameObject.transform, element.Block ?? element.Id, scale);
                    var labelRect = label.rectTransform;
                    
                    // Force the label to be centered on the point
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.anchorMin = new Vector2(0, 1); // Stay consistent with root coordinate system
                    labelRect.anchorMax = new Vector2(0, 1);
                    labelRect.sizeDelta = new Vector2(scaledGridCellSizeX * 4, scaledGridCellSize); // Large enough to not wrap
                    labelRect.anchoredPosition = centerPos;
                    
                    label.alignment = TextAlignmentOptions.Center;
                    label.fontSize = 14 * scale;
                    label.fontStyle = FontStyles.Bold;
                    label.color = Color.white;
                    if (element.Color.ToLower() == "red") label.color = Color.red;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                    label.overflowMode = TextOverflowModes.Overflow;
                    break;
                
                case CTCPanelLayout.LayoutType.Light:
                    if (element.ShowTrack)
                    {
                        var track = CreateImage(rootRect.gameObject.transform, "TrackH", new Vector2(scaledGridCellSizeX, trackWidth), TrackColor, true);
                        track.anchoredPosition = centerPos;    
                        AddHighlight(track, element, schematic);
                    }

                    if (element.Block == null)
                    {
                        Log.Warning($"Skipping light {element.X}, {element.Y} because it has no block");
                        break;
                    }
                    
                    var light = CreateImage(rootRect.gameObject.transform, $"Light_{element.Block}", new Vector2(12 * scale, 12 * scale), Color.black, true);
                    light.anchoredPosition = centerPos;
                    light.SetAsLastSibling();
                    
                    var outline = light.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.gray;
                    outline.effectDistance = new Vector2(1 * scale, 1 * scale);

                    var ctcLight = light.gameObject.AddComponent<CTCLight>();
                    ctcLight.OnColor = Color.white;
                    if (element.Color.ToLower() == "red") ctcLight.OnColor = Color.red;
                    ctcLight.OffColor = Color.black;
                    ctcLight.Initialize(element.Block);
                    break;
                
                case CTCPanelLayout.LayoutType.Track:
                    var trackImage = CreateImage(rootRect.gameObject.transform, "Main", new Vector2(scaledGridCellSizeX, trackWidth), TrackColor, true);
                    trackImage.anchoredPosition = centerPos;
                    AddHighlight(trackImage, element, schematic);
                    break;
                
                case CTCPanelLayout.LayoutType.TrackRise:
                    var rise = CreateImage(rootRect.gameObject.transform, "Rise", new Vector2(diagLength, trackWidth), TrackColor, true);
                    rise.anchoredPosition = centerPos + new Vector2(-scaledGridCellSizeX/2, 0); // Start from left
                    rise.pivot = new Vector2(0, 0.5f); // Rotate from start
                    rise.localEulerAngles = new Vector3(0, 0, angleDeg);
                    AddHighlight(rise, element, schematic);
                    break;
                
                case CTCPanelLayout.LayoutType.TrackFall:
                    var fall = CreateImage(rootRect.gameObject.transform, "Fall", new Vector2(diagLength, trackWidth), TrackColor, true);
                    fall.anchoredPosition = centerPos + new Vector2(-scaledGridCellSizeX/2, 0);
                    fall.pivot = new Vector2(0, 0.5f);
                    fall.localEulerAngles = new Vector3(0, 0, -angleDeg);
                    AddHighlight(fall, element, schematic);
                    break;

                case CTCPanelLayout.LayoutType.SwitchLeftTop: // Main track + Diverge to Top-Right
                    var sl = CreateImage(rootRect.gameObject.transform, "Main", new Vector2(scaledGridCellSizeX, trackWidth), TrackColor, true);
                    sl.anchoredPosition = centerPos;
                    AddHighlight(sl, element, schematic);
                    var slt = CreateImage(rootRect.gameObject.transform, "Diverge", new Vector2(diagLength, trackWidth), TrackColor, true);
                    slt.anchoredPosition = centerPos + new Vector2(-scaledGridCellSizeX/2, 0); // Start from left
                    slt.pivot = new Vector2(0, 0.5f); // Rotate from start
                    slt.localEulerAngles = new Vector3(0, 0, angleDeg);
                    AddHighlight(slt, element, schematic);
                    break;

                case CTCPanelLayout.LayoutType.SwitchRightTop: // Main track + Diverge to Top-Left
                    var sr = CreateImage(rootRect.gameObject.transform, "Main", new Vector2(scaledGridCellSizeX, trackWidth), TrackColor, true);
                    sr.anchoredPosition = centerPos;
                    AddHighlight(sr, element, schematic);
                    var srt = CreateImage(rootRect.gameObject.transform, "Diverge", new Vector2(diagLength, trackWidth), TrackColor, true);
                    srt.anchoredPosition = centerPos + new Vector2(scaledGridCellSizeX/2, 0); // Start from right
                    srt.pivot = new Vector2(1, 0.5f); // Rotate from end
                    srt.localEulerAngles = new Vector3(0, 0, -angleDeg);
                    AddHighlight(srt, element, schematic);
                    break;

                case CTCPanelLayout.LayoutType.SwitchRightBottom: // Main track + Diverge to Bottom-Right
                    var sb = CreateImage(rootRect.gameObject.transform, "Main", new Vector2(scaledGridCellSizeX, trackWidth), TrackColor, true);
                    sb.anchoredPosition = centerPos;
                    AddHighlight(sb, element, schematic);
                    var slb = CreateImage(rootRect.gameObject.transform, "Diverge", new Vector2(diagLength, trackWidth), TrackColor, true);
                    slb.anchoredPosition = centerPos + new Vector2(-scaledGridCellSizeX/2, 0);
                    slb.pivot = new Vector2(0, 0.5f);
                    slb.localEulerAngles = new Vector3(0, 0, -angleDeg);
                    AddHighlight(slb, element, schematic);
                    break;

                case CTCPanelLayout.LayoutType.SwitchLeftBottom: // Main track + Diverge to Bottom-Left
                    var sx = CreateImage(rootRect.gameObject.transform, "Main", new Vector2(scaledGridCellSizeX, trackWidth), TrackColor, true);
                    sx.anchoredPosition = centerPos;
                    AddHighlight(sx, element, schematic);
                    var srb = CreateImage(rootRect.gameObject.transform, "Diverge", new Vector2(diagLength, trackWidth), TrackColor, true);
                    srb.anchoredPosition = centerPos + new Vector2(scaledGridCellSizeX/2, 0);
                    srb.pivot = new Vector2(1, 0.5f);
                    srb.localEulerAngles = new Vector3(0, 0, angleDeg);
                    AddHighlight(srb, element, schematic);
                    break;
            }

            if (!string.IsNullOrEmpty(element.SwitchLabel))
            {
                var label = CreateLabel(rootRect.gameObject.transform, element.SwitchLabel, scale);
                var labelRect = label.rectTransform;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchorMin = new Vector2(0, 1);
                labelRect.anchorMax = new Vector2(0, 1);
                labelRect.sizeDelta = new Vector2(scaledGridCellSizeX * 2, scaledGridCellSize);
                
                Vector2 offset = new Vector2(element.LabelOffsetX ?? 0, element.LabelOffsetY ?? 0);
                Vector2 scaledOffset = new Vector2(offset.x * scaledGridCellSizeX, -offset.y * scaledGridCellSize);
                labelRect.anchoredPosition = centerPos + scaledOffset;

                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 10 * scale;
                label.fontStyle = FontStyles.Bold;
                label.color = Color.blue;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
            }
        }

        return rootRect;
    }
    
    private static void AddHighlight(RectTransform rect, CTCPanelLayout.SchematicElement element, Schematic schematic)
    {
        if (string.IsNullOrEmpty(element.Id)) return;
        var hl = rect.gameObject.AddComponent<TrackHighlighter>();
        hl.id = element.Id ?? "";
        hl.Schematic = schematic;
    }

    public static RectTransform CreateControlRow(UIPanelBuilder builder, Schematic schematic)
    {
        var type = typeof(UIPanelBuilder);
        var rootRect = type.GetMethod("CreateRectView", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(builder, ["DisplayRow", 50, 50]) as RectTransform;
        // FIX: Set pivot to top-left so (0,0) is exactly the top-left corner of the background
        rootRect.anchorMin = new Vector2(0, 1);
        rootRect.anchorMax = new Vector2(0, 1);
        rootRect.pivot = new Vector2(0, 1);
        
        // Calculate bounds for size
        float maxX = schematic.MaxX;
        float finalWidth = maxX * GridCellSizeX;
        float finalHeight = FixedControlRowHeight;
        rootRect.sizeDelta = new Vector2(finalWidth, finalHeight);

        // Add LayoutElement to tell UIPanelBuilder how big we are
        var layout = rootRect.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = finalWidth;
        layout.preferredHeight = finalHeight;
        layout.minWidth = finalWidth; // Added to prevent squeezing
        layout.minHeight = finalHeight;

        return rootRect;
    }
    
    public static TMP_Text CreateLabel(Transform parent, string text, float scale)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(parent, false);
        go.layer = Layers.UI;
        var rect = go.AddComponent<RectTransform>();
            
        // Fix: Anchor to top-center of the LabelArea
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(40 * scale, 20 * scale);

        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 12 * scale;
        txt.alignment = TextAlignmentOptions.Center;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.raycastTarget = false;
        return txt;
    }

    public static RectTransform CreateImage(Transform parent, string name, Vector2 size, Color color, bool alignCentral = false, bool raycastTarget = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = Layers.UI;
        
        var rect = (RectTransform)go.transform;
        go.transform.SetParent(parent, false);
        
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        if (alignCentral)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
        
        rect.sizeDelta = size;

        return rect;
    }
}