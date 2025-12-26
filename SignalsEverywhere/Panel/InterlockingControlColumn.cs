using System.Linq;
using KeyValue.Runtime;
using Serilog;
using TMPro;
using Track.Signals;
using UnityEngine;
using UnityEngine.UI;

namespace SignalsEverywhere.Panel;

public static class InterlockingControlColumn
{
    static Serilog.ILogger logger = Log.ForContext<Schematic>();
    
    public static void CreateControl(RectTransform parent, CTCPanelLayout.SchematicElement element, Schematic schematic, float scale)
    {
        var id = element.Interlock.Interlock;
        if (!CTCPanelController.Shared.AllInterlockings.TryGetValue(id, out var interlocking) || !interlocking.isActiveAndEnabled)
        {
            logger.Debug($"Interlocking with ID '{id}' not found.");
            return;
        }
        
        GameObject colObj = new GameObject($"Column_{id}");
        colObj.transform.SetParent(parent, false);
        var colRect = colObj.AddComponent<RectTransform>();

        // Layout & Alignment
        colRect.anchorMin = new Vector2(0, 1);
        colRect.anchorMax = new Vector2(0, 1);
        colRect.pivot = new Vector2(0.5f, 1);
        colRect.anchoredPosition = new Vector2(element.X * ViewCreator.GridCellSizeX * scale + (ViewCreator.GridCellSizeX * scale / 2f), 0);
        
        float knobScale = 0.75f * scale;
        float knobWidth = 80f * knobScale;

        int switchKnobCount = interlocking.switchSets.Count;
        int signalKnobCount = 1; // Always 1 direction knob for now in InterlockingControlColumn
        
        int maxKnobsInRow = Mathf.Max(switchKnobCount, signalKnobCount, 1);
        float colWidth = Mathf.Max(ViewCreator.GridCellSizeX * scale, maxKnobsInRow * knobWidth + (maxKnobsInRow - 1) * 5f * scale + 10f * scale); 
        colRect.sizeDelta = new Vector2(colWidth, ViewCreator.FixedControlRowHeight * scale);

        var vLayout = colObj.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlWidth = true;
        vLayout.childForceExpandWidth = false;
        vLayout.spacing = 2 * scale;
        
        var img = colObj.AddComponent<Image>();
        img.color = new Color(0.0f, 0.0f, 0.15f);
        
        SetupControls(id, element, colRect, interlocking, schematic, knobScale, colWidth, scale);
    }

    private static void SetupControls(string id, CTCPanelLayout.SchematicElement element, RectTransform container, CTCInterlocking interlocking, Schematic schematic, float knobScale, float colWidth, float scale)
    {
        var storage = CTCPanelController.Shared.GetComponentInParent<SignalStorage>();
        
        var label = ViewCreator.CreateLabel(container.transform, id, 1.2f * scale);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 14 * scale;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        
        var swRow = CreateRow(container, "SwitchRow", scale);
        var switches = interlocking.switchSets.Select(e => e.switchNodes).ToList();
        for (int i = 0; i < switches.Count(); i++) {
            var switchIndex = i;
            if (element.Interlock.KnobOrder.Count == switches.Count)
            {
                switchIndex = element.Interlock.KnobOrder[i];
            }
            var switchList = switches.ElementAt(switchIndex);
            if (switchList.First() == null)
            {
                logger.Warning($"Interlocking {id} has a null switch node");
                continue;
            }
            var l = element.Interlock.SwitchLabels.Count > i ? element.Interlock.SwitchLabels[i] : "";
            var switchKnob = ThreeWayKnob.AddThreePositionKnob(swRow.transform, "N", l, "R", schematic, knobScale);
            switchKnob.AllowedPositions = [ThreeWayKnob.KnobPosition.Left, ThreeWayKnob.KnobPosition.Right];
            switchKnob.LeftColor = Color.green;
            switchKnob.RightColor = Color.yellow;
            switchKnob.RegisterSwitchListener(storage, switchList, element.Interlock?.SwitchKnobId(switchIndex) ?? "unknown");
        }

        // 2. Direction Knob
        var dirRow = CreateRow(container, "DirectionRow", scale);
        var ls = element.Interlock.SignalLabel ?? "";
        var dirKnob = ThreeWayKnob.AddThreePositionKnob(dirRow.transform, "L", ls, "R", schematic, knobScale, true);
        dirKnob.RegisterSignalListener(storage, id, element.Interlock?.DirKnobId());

        // 3. Execute Button
        var btnGo = new GameObject("ExecuteButton");
        btnGo.transform.SetParent(container, false);
        
        var executeButton = btnGo.AddComponent<Button>();
        executeButton.onClick.AddListener(() =>
        {
            var storage = CTCPanelController.Shared.GetComponentInParent<SignalStorage>();
            var kvo = storage.GetComponent<KeyValueObject>();
            kvo[CTCPanel.Button(element.Interlock.Interlock)] = Value.Bool(true);
        });
        
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.25f, 0.25f);
        
        var btnLayout = btnGo.AddComponent<LayoutElement>();
        btnLayout.preferredHeight = 25 * scale;
        btnLayout.preferredWidth = colWidth * 0.9f;

        CreateButtonText(btnGo.transform, scale);
    }

    private static GameObject CreateRow(Transform parent, string name, float scale)
    {
        GameObject row = new GameObject(name);
        row.transform.SetParent(parent, false);
        var hLayout = row.AddComponent<HorizontalLayoutGroup>();
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.spacing = scale;
        return row;
    }

    private static void CreateButtonText(Transform parent, float scale)
    {
        var txt = new GameObject("Text").AddComponent<TextMeshProUGUI>();
        txt.transform.SetParent(parent, false);
        txt.text = "CODE";
        txt.fontSize = 10 * scale;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;
    }
}