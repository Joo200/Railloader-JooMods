using System.Linq;
using KeyValue.Runtime;
using Serilog;
using SignalsEverywhere.Signals;
using TMPro;
using Track.Signals;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Panel;

public static class CrossoverControlColumn
{
    static Serilog.ILogger logger = Log.ForContext<Schematic>();
    
    public static void CreateControl(RectTransform parent, CTCPanelLayout.SchematicElement element, Schematic schematic, float scale)
    {
        var id = element.Crossover.Crossover;
        var crossover = Object.FindObjectsOfType<CTCCrossover>(true).FirstOrDefault(c => c.id == id);
        if (crossover == null || !crossover.isActiveAndEnabled)
        {
            logger.Debug($"Crossover with ID '{id}' not found.");
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
        
        int switchKnobCount = crossover.switchSets.Count;
        int signalKnobCount = crossover.signalGroups.Count;
        
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
        
        SetupControls(id, element, colRect, crossover, schematic, knobScale, colWidth, scale);
    }

    private static void SetupControls(string id, CTCPanelLayout.SchematicElement element, RectTransform container, CTCCrossover crossover, Schematic schematic, float knobScale, float colWidth, float scale)
    {
        var storage = CTCPanelController.Shared.GetComponentInParent<SignalStorage>();
        
        var label = ViewCreator.CreateLabel(container.transform, id, 1.2f * scale);
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 14 * scale;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        
        var swRow = CreateRow(container, "SwitchRow", scale);
        var switches = crossover.switchSets.Select(e => e.switchNodes).ToList();
        for (int i = 0; i < switches.Count(); i++)
        {
            var switchIndex = i;
            if (element.Crossover.SwitchKnobOrder.Count == switches.Count)
            {
                switchIndex = element.Crossover.SwitchKnobOrder[i];
            }
            var switchList = switches.ElementAt(switchIndex);
            if (switchList.First() == null)
            {
                logger.Warning($"Interlocking {id} has a null switch node");
                continue;
            }
            
            var l = element.Crossover.SwitchLabels.Count > i ? element.Crossover.SwitchLabels[i] : "";
            var switchKnob = ThreeWayKnob.AddThreePositionKnob(swRow.transform, "N", l, "R", schematic, knobScale);
            switchKnob.AllowedPositions = [ThreeWayKnob.KnobPosition.Left, ThreeWayKnob.KnobPosition.Right];
            switchKnob.LeftColor = Color.green;
            switchKnob.RightColor = Color.yellow;
            switchKnob.RegisterSwitchListener(storage, switchList, element.Crossover?.SwitchKnobId(switchIndex) ?? "unknown");
        }
        
        var dirRow = CreateRow(container, "DirectionRow", scale);
        for (int i = 0; i < crossover.signalGroups.Count; i++)
        {
            var signalIndex = i;
            if (element.Crossover.SignalKnobOrder.Count == crossover.signalGroups.Count)
            {
                signalIndex = element.Crossover.SignalKnobOrder[i];
            }
            var signalGroup = crossover.signalGroups[signalIndex];
            var l = element.Crossover.SignalLabels.Count > i ? element.Crossover.SignalLabels[i] : "";
            var knob = ThreeWayKnob.AddThreePositionKnob(dirRow.transform, signalGroup.allowedDirection == SignalDirection.Left ? "L" : "", l, signalGroup.allowedDirection == SignalDirection.Right ? "R" : "", schematic, knobScale, true);
            knob.RegisterCrossoverSignalListener(storage, signalGroup.groupId, element.Crossover!.SignalKnobId(signalGroup.groupId));
            if (signalGroup.allowedDirection == SignalDirection.Left)
                knob.AllowedPositions = [ThreeWayKnob.KnobPosition.Left, ThreeWayKnob.KnobPosition.Up];
            else
                knob.AllowedPositions = [ThreeWayKnob.KnobPosition.Up, ThreeWayKnob.KnobPosition.Right];
        }
        
        // 3. Execute Button
        var btnGo = new GameObject("ExecuteButton");
        btnGo.transform.SetParent(container, false);
        
        var executeButton = btnGo.AddComponent<Button>();
        executeButton.onClick.AddListener(() =>
        {
            var storage = CTCPanelController.Shared.GetComponentInParent<SignalStorage>();
            var kvo = storage.GetComponent<KeyValueObject>();
            kvo[CTCPanel.Button(element.Crossover.Crossover)] = Value.Bool(true);
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
