using System.Linq;
using System.Reflection;
using Game.Messages;
using Model;
using Model.AI;
using Model.Ops;
using Serilog;
using TMPro;
using Track;
using UI.CarInspector;
using UI.EngineRoster;
using UI.LazyScrollList;
using UI.Map;
using UI.Tooltips;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;

#nullable disable

namespace NotEnoughRosters;

public class NotEnoughRosterRow : MonoBehaviour, ILazyScrollListCell
{
    public RectTransform rectTransform;
    public TMP_Text nameLabel;
    public TMP_Text infoLabel;
    public TMP_Text crewLabel;
    public Toggle favoriteToggle;
    public Toggle selectedToggle;
    public UITooltipProvider nameTooltip;
    public UITooltipProvider crewTooltip;
    public UITooltipProvider infoTooltip;
    public Button jumpButton;
    public Button inspectButton;
    public Button mapButton;
    private readonly ILogger logger = Log.ForContext<NotEnoughRosterRow>();

    private BaseLocomotive _engine;
    private NotEnoughRosterPanel _parent;
    private AutoEngineerPersistence _persistence;

    public void Configure(int listIndex, object data)
    {
        ListIndex = listIndex;
        var rosterRowData = (NotEnoughRosterRowData)data;
        Configure(rosterRowData.Engine, rosterRowData.IsFavorite, rosterRowData.IsSelected, rosterRowData.Parent);
    }

    public int ListIndex { get; private set; }

    public RectTransform RectTransform => rectTransform;

    public void InjectFrom(EngineRosterRow original)
    {
        var type = typeof(EngineRosterRow);
        rectTransform =
            type.GetField("rectTransform", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(original) as RectTransform;
        nameLabel =
            type.GetField("nameLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(original) as TMP_Text;
        infoLabel =
            type.GetField("infoLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(original) as TMP_Text;
        crewLabel =
            type.GetField("crewLabel", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(original) as TMP_Text;
        favoriteToggle = type.GetField("favoriteToggle", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(original) as Toggle;
        selectedToggle = type.GetField("selectedToggle", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(original) as Toggle;
        nameTooltip =
            type.GetField("nameTooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(original) as
                UITooltipProvider;
        crewTooltip =
            type.GetField("crewTooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(original) as
                UITooltipProvider;
        infoTooltip =
            type.GetField("infoTooltip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(original) as
                UITooltipProvider;

        jumpButton = original.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.name == "Jump Button");
        mapButton = original.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "Map Button");
        inspectButton = original.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.name == "Inspect Button");
    }

    public void Configure(BaseLocomotive engine, bool isFavorite, bool isSelected, NotEnoughRosterPanel parent)
    {
        _parent = parent;
        _engine = engine;
        _persistence = new AutoEngineerPersistence(_engine.KeyValueObject);
        selectedToggle.SetIsOnWithoutNotify(isSelected);
        favoriteToggle.SetIsOnWithoutNotify(isFavorite);

        favoriteToggle.onValueChanged.RemoveAllListeners();
        favoriteToggle.onValueChanged.AddListener(ActionFavorite);
        selectedToggle.onValueChanged.RemoveAllListeners();
        selectedToggle.onValueChanged.AddListener(ActionSelect);
        jumpButton.onClick.RemoveAllListeners();
        jumpButton.onClick.AddListener(ActionJumpTo);
        mapButton.onClick.RemoveAllListeners();
        mapButton.onClick.AddListener(ActionMap);
        inspectButton.onClick.RemoveAllListeners();
        inspectButton.onClick.AddListener(ActionInspect);
        Refresh();
    }

    public void Refresh()
    {
        nameLabel.text = _engine.DisplayName;
        var title1 = "";
        var enabled = _persistence.Orders.Enabled;
        if (enabled)
            title1 += "<sup>AE </sup>";
        if (_engine.KeyValueObject[PropertyChange.KeyForControl(PropertyChange.Control.Mu)].BoolValue)
            title1 += "<sup>MU </sup>";
        string trainName;
        if (_engine.TryGetTrainName(out trainName))
            title1 += trainName;
        crewLabel.text = title1;
        var num = Mathf.RoundToInt(_engine.VelocityMphAbs);
        var title2 = !_engine.IsDerailed
            ? !((num == 0) & enabled) ? num != 0 ? $"{num} mph" : "Stopped" : _persistence.PlannerStatus
            : "Derailed";
        infoLabel.text = title2;
        nameTooltip.TooltipInfo = new TooltipInfo(_engine.DisplayName, null);
        crewTooltip.TooltipInfo = new TooltipInfo(title1, null);
        infoTooltip.TooltipInfo = new TooltipInfo(title2, null);
        selectedToggle.SetIsOnWithoutNotify(ReferenceEquals(TrainController.Shared.SelectedCar, _engine));
    }

    public void ActionJumpTo()
    {
        CameraSelector.shared.FollowCar(_engine);
    }

    public void ActionSelect(bool select)
    {
        _parent.SelectEngine(_engine, selectedToggle.isOn);
    }

    public void ActionFavorite(bool favorite)
    {
        _parent.ToggleFavorite(_engine, favorite);
    }

    public void ActionInspect()
    {
        CarInspector.Show(_engine);
    }

    public void ActionMap()
    {
        MapWindow.Show(_engine.GetCenterPosition(Graph.Shared));
    }
}