using System.Collections.Generic;
using KeyValue.Runtime;
using SignalsEverywhere.Patches;
using UI;
using UI.Common;
using UI.ContextMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using ContextMenu = UI.ContextMenu.ContextMenu;

namespace SignalsEverywhere.Panel;

public class ClickHandler : MonoBehaviour, IPointerClickHandler
{
    public string Branch => Schematic.Name;
    public Schematic Schematic { get; set; }
    private enum MenuButtons
    {
        Apply,
        Cancel
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        var screenPos = eventData.position;
        var rt = transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rt,
            screenPos,
            eventData.pressEventCamera,
            out var lp);
        if (eventData.button != PointerEventData.InputButton.Right) return;

        float scale = rt.sizeDelta.y / ViewCreator.FixedSchematicHeight;
        float scaledGridCellSizeX = ViewCreator.GridCellSizeX * scale;
        float scaledGridCellSize = ViewCreator.GridCellSize * scale;
        
        ContextMenu shared = ContextMenu.Shared;
        if (ContextMenu.IsShown)
            shared.Hide();
        shared.Clear();
        shared.AddButton(ContextMenuQuadrant.General, "Create", SpriteName.Inspect, () => {
            ModalAlertController.Present("Create Marker", null, "", [
                (MenuButtons.Cancel, "Cancel"),
                (MenuButtons.Apply, "Apply")
            ], output => {
                if (output.Item1 == MenuButtons.Cancel)
                    return;

                var rt2 = transform as RectTransform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt2,
                    screenPos,
                    eventData.pressEventCamera,
                    out var localPoint);

                float xCanvas = (localPoint.x - scaledGridCellSizeX / 2) / scaledGridCellSizeX;
                float x = Branch == "Mainline" ? PanelMarker.ToMainlineValue(xCanvas) : xCanvas;
                float y = 1 - (-localPoint.y / (scaledGridCellSize * Schematic.MaxY));
                
                Dictionary<string, Value> dict = new();
                dict["x"] = Value.Float(x);
                dict["y"] = Value.Float(y);
                dict["text"] = Value.String(output.Item2);
                CTCPanelMarker_Patch.GetKvo()[PanelMarker.CreateMarkerId(Branch)] = Value.Dictionary(dict);
            });
        });
        shared.Show("Panel Marker");
    }
}