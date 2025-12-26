using System;
using System.Collections.Generic;
using System.Linq;
using KeyValue.Runtime;
using Network;
using Serilog;
using SignalsEverywhere.Patches;
using TMPro;
using Track.Signals.Panel;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalsEverywhere.Panel;

public class PanelMarker : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    
    public static string CreateMarkerId(string branch) => branch == "Mainline" ? 
        $"marker-{NetworkTime.systemTick.ToString()}" : 
        $"markerbranch-{branch.Replace(" ", "_").Replace("-", "_")}-{NetworkTime.systemTick.ToString()}";

    private static float CanvasMin = 3f;
    private static float CanvasFirst = 31.5f; // between Brooks and McClain
    private static float CanvasSecond = 68.5f; // between Whittier and Thomas Valley
    private static float CanvasMax = 94f;
    public static float ToMainlineValue(float value) {
        if (value < CanvasFirst)
            return (value - CanvasMin) / (CanvasFirst - CanvasMin);
        if (value < CanvasSecond)
            return 1 + (value - CanvasFirst) / (CanvasSecond - CanvasFirst);
        return 2 + (value - CanvasSecond) / (CanvasMax - CanvasSecond);
    }
    
    public static float ToCanvasValue(float value) {
        if (value < 1)
        {
            return value * CanvasFirst;
        }

        if (value < 2)
        {
            return (value - 1) * (CanvasSecond - CanvasFirst) + CanvasFirst;
        }
        return (value - 2) * (CanvasMax - CanvasSecond) + CanvasSecond;
    }
    
    public string Id { get; set; }
    
    private string _text;
    private TextMeshProUGUI? _textMesh;
    private Image? _image;
    private KeyValueObject? _kvo;
    private float MaxY { get; set; }
    private float MinY { get; set; }

    private Vector2 ?_lastPosition;
    
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            if (_textMesh != null)
            {
                _textMesh.text = value;
                UpdateWidth();
            }
            if (_image != null) _image.color = InferColorFromText(value);
        }
    }

    private void UpdateWidth()
    {
        if (_textMesh == null || RectTransform == null) return;
        _textMesh.ForceMeshUpdate();
        float preferredWidth = _textMesh.preferredWidth;
        float minWidth = ViewCreator.GridCellSizeX * 1.2f;
        float padding = 8f;
        float newWidth = Mathf.Max(minWidth, preferredWidth + padding);
        RectTransform.sizeDelta = new Vector2(newWidth, ViewCreator.GridCellSize * 1.2f);
    }

    public string Branch { get; set; } = "Mainline";
    public bool MainLine => Branch == "Mainline";

    public RectTransform? RectTransform { get; set; }

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
        _textMesh = GetComponentInChildren<TextMeshProUGUI>();
        UpdateWidth();
    }

    private void OnEnable()
    {
        _kvo = FindAnyObjectByType<CTCPanelMarkerManager>().GetComponentInParent<KeyValueObject>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            CTCPanelMarker_Patch.ShowContextMenu(Id, Text, Branch);
        }
    }

    private static Color InferColorFromText(string text)
    {
        if (text.StartsWith(">") || text.EndsWith(">")) return new Color(0.45f, 0.69f, 0.35f); // #72B159
        if (text.StartsWith("<") || text.EndsWith("<")) return new Color(0.69f, 0.35f, 0.35f); // #B15959
        return new Color(0.35f, 0.51f, 0.69f); // #5A82B1
    }

    public static PanelMarker CreateMarker(Transform parent, string id, string text, int minY, int maxY, string branch)
    {
        var go = new GameObject("Marker_" + id);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ViewCreator.GridCellSizeX * 1.2f, ViewCreator.GridCellSize * 1.2f);

        go.AddComponent<CanvasRenderer>();
        var bg = go.AddComponent<Image>();
        bg.color = InferColorFromText(text);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var txt = textGo.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 12 * 1.2f;
        txt.alignment = TextAlignmentOptions.Center;
        txt.horizontalAlignment = HorizontalAlignmentOptions.Center;
        txt.verticalAlignment = VerticalAlignmentOptions.Middle;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.raycastTarget = false;
        txt.text = text;
        
        var marker = go.AddComponent<PanelMarker>();
        marker.Id = id;
        marker.Branch = branch;
        marker.MinY = minY;
        marker.MaxY = maxY;
        marker.Text = text;
        return marker;
    }

    public void UpdatePosition(IReadOnlyDictionary<string, Value> dict, float parentHeight)
    {
        if (_isDragging) return;
        var text = dict.TryGetValue("text", out var value) ? value.StringValue : "???";
        if (Text != text) Text = text;
        Vector2 pos = new Vector2(dict["x"].FloatValue, dict["y"].FloatValue);
        if (pos == _lastPosition) return;

        if (RectTransform == null) RectTransform = GetComponent<RectTransform>();
        var parentRect = RectTransform.parent as RectTransform;
        float scaledGridCellSizeX = ViewCreator.GridCellSizeX;
        float scaledGridCellSize = ViewCreator.GridCellSize;
        if (parentRect != null)
        {
            scaledGridCellSizeX *= parentRect.sizeDelta.y / ViewCreator.FixedSchematicHeight;
            scaledGridCellSize *= parentRect.sizeDelta.y / ViewCreator.FixedSchematicHeight;
        }

        if (Mathf.Approximately(dict["x"].FloatValue, -1) && Mathf.Approximately(dict["y"].FloatValue, -1))
        {
            var initPos = new Vector2(scaledGridCellSizeX * 2, -scaledGridCellSize * (MaxY - MinY + 1) / 2);
            RectTransform.anchoredPosition = initPos;
            Log.Information($"Resetting marker {Id} to {initPos}");
            ValidateAndClampPosition();
            return;
        }
        
        if (MainLine) pos.x = ToCanvasValue(pos.x);
        pos = new Vector2(
            pos.x * scaledGridCellSizeX + scaledGridCellSizeX / 2, 
            -(1 - pos.y) * scaledGridCellSize * (MaxY - MinY + 1));
        RectTransform.anchoredPosition = pos;
        ValidateAndClampPosition();
    }

    private bool _isDragging;
    private Vector2 _dragOffset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        if (RectTransform == null) RectTransform = GetComponent<RectTransform>();
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            RectTransform.parent as RectTransform, 
            eventData.position, 
            eventData.pressEventCamera, 
            out var localPoint);
        
        _dragOffset = RectTransform.anchoredPosition - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransform == null) return;
        var parentRect = RectTransform.parent as RectTransform;
        if (parentRect == null) return;
        
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, 
            eventData.position, 
            eventData.pressEventCamera, 
            out var localPoint);
        
        Vector2 targetPos = localPoint + _dragOffset;
        RectTransform.anchoredPosition = targetPos;
        ValidateAndClampPosition();
    }

    public void ValidateAndClampPosition()
    {
        if (RectTransform == null) RectTransform = GetComponent<RectTransform>();
        var parentRect = RectTransform.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 targetPos = RectTransform.anchoredPosition;
        
        // Clamp to parent rect
        Rect parentBounds = parentRect.rect;
        Vector2 size = RectTransform.sizeDelta;
        Vector2 pivot = RectTransform.pivot;

        float minX = parentBounds.xMin + (size.x * pivot.x);
        float maxX = parentBounds.xMax - (size.x * (1f - pivot.x));
        float minY = parentBounds.yMin + (size.y * pivot.y);
        float maxY = parentBounds.yMax - (size.y * (1f - pivot.y));

        targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
        targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);
        
        RectTransform.anchoredPosition = targetPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;
        SavePositionToKVO();
    }

    private void SavePositionToKVO()
    {
        if (RectTransform == null) return;
        var pos = RectTransform.anchoredPosition;

        var parentRect = RectTransform.parent as RectTransform;
        float scaledGridCellSizeX = ViewCreator.GridCellSizeX;
        float scaledGridCellSize = ViewCreator.GridCellSize;
        if (parentRect != null)
        {
            scaledGridCellSizeX *= parentRect.sizeDelta.y / ViewCreator.FixedSchematicHeight;
            scaledGridCellSize *= parentRect.sizeDelta.y / ViewCreator.FixedSchematicHeight;
        }
        
        float xCanvas = (pos.x - scaledGridCellSizeX / 2) / scaledGridCellSizeX;
        float x = MainLine ? ToMainlineValue(xCanvas) : xCanvas;
        float y = 1 - (-pos.y / (scaledGridCellSize * (MaxY - MinY + 1)));

        _lastPosition = new Vector2(x, y);
        UpdateInDictionary(a =>
        {
            a["x"] = Value.Float(x);
            a["y"] = Value.Float(y);
        });
    }

    private void UpdateInDictionary(Action<Dictionary<string, Value>> action)
    {
        var dictionary = _kvo[Id].DictionaryValue.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        action(dictionary);
        _kvo[Id] = Value.Dictionary(dictionary);
    }
}
