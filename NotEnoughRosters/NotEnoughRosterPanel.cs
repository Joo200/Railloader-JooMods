using System.Collections;
using System.Collections.Generic;
using System.Linq;
#if !RR2026
using GalaSoft.MvvmLight.Messaging;
#endif
using Game.Events;
using Game.State;
using Model;
using NotEnoughRosters.Windows;
using Serilog;
using UI.Builder;
using UI.Common;
using UI.LazyScrollList;
#if RR2026
using UI.Windows;
#endif
using UnityEngine;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;

#nullable disable
namespace NotEnoughRosters;

public class NotEnoughRosterPanel : WindowBase
{
    public static NotEnoughRosterPanel Shared;
    private readonly ILogger logger = Log.ForContext<NotEnoughRosterPanel>();
    private HashSet<string> _cachedFavorites;

    private Coroutine _coroutine;

    private Dictionary<string, List<NotEnoughRosterRowData>> _data = new();
    private Dictionary<string, LocomotiveFilter> _filters;
    private int _hash;
    private RectTransform _headerClone;

    private GameObject _modifiedCell;
    private readonly List<LazyScrollList> _scrollList = new();
    private bool _splitView;

    private const float SplitWidth = 800f;
    private const float SplitHysteresis = 20f;

    public override string WindowIdentifier => "NER";
    public override string Title => "Not Enough Rosters";
#if RR2026
    // The game's own roster opens 560 wide in 2026.1: narrower, its status column has no room.
    public override Vector2Int DefaultSize => new(560, 800);
#else
    public override Vector2Int DefaultSize => new(400, 800);
#endif
    public override Window.Position DefaultPosition => Window.Position.UpperRight;
    public override Window.Sizing Sizing => Window.Sizing.Resizable(new Vector2Int(250, 150));

    public static void CreateInstance(Dictionary<string, LocomotiveFilter> data)
    {
        WindowHelper.CreateWindow<NotEnoughRosterPanel>(inst => { inst._filters = data; });
        Shared = WindowManager.Shared.GetWindow<NotEnoughRosterPanel>();
    }

    public void Toggle()
    {
        if (Window.IsShown)
            Close();
        else
            Show();
    }

    public void Show()
    {
        Window.OnShownWillChange -= WindowShownWillChange;
        Window.OnShownWillChange += WindowShownWillChange;

        _splitView = Window.GetContentSize().x >= SplitWidth;

        _modifiedCell = ReflectionUtils.CloneScrollListPrefab();
        _headerClone = ReflectionUtils.FindEngineRosterPanelHeader();
        DataRebuild();
        Rebuild();
        Window.ShowWindow();

#if RR2026
        EventBus.Subscribe<CarTrainCrewChanged>(this, ml => { _hash = -1; });
#else
        Messenger.Default.Register<CarTrainCrewChanged>(this, ml => { _hash = -1; });
#endif
    }

    public void Close()
    {
        Window.CloseWindow();

#if RR2026
        EventBus.Unsubscribe(this);
#else
        Messenger.Default.Unregister(this);
#endif
    }

    // Window.OnDidResize only fires when the drag ends, so poll the width to switch layouts while resizing.
    // The gap between the two thresholds keeps it from flickering at the boundary.
    private void Update()
    {
        if (!Window.IsShown) return;
        var width = Window.GetContentSize().x;
        var shouldSplit = _splitView ? width >= SplitWidth - SplitHysteresis : width >= SplitWidth;
        if (_splitView != shouldSplit)
        {
            _splitView = shouldSplit;
            Rebuild();
        }
    }

    private void WindowShownWillChange(bool shown)
    {
        if (shown)
        {
            if (_coroutine != null)
                return;
            _coroutine = StartCoroutine(UpdateCoroutine());
        }
        else
        {
            if (_coroutine != null)
                StopCoroutine(_coroutine);
            _coroutine = null;
        }
    }

    private IEnumerator UpdateCoroutine()
    {
        while (true)
        {
            var hash = _hash;
            DataRebuild();
            if (hash != _hash)
                Rebuild();
            else
                Refresh();

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private void Refresh()
    {
        foreach (var cell in _scrollList)
        foreach (NotEnoughRosterRow visibleCell in cell.VisibleCells)
            visibleCell.Refresh();
    }

    private void DataRebuild()
    {
        var shared = TrainController.Shared;

#if RR2026
        _cachedFavorites = PlayerPropertiesManager.Shared.LocalProperties.FavoriteEngineIds;
#else
        _cachedFavorites = PlayerPropertiesManager.Shared.MyProperties.FavoriteEngineIds;
#endif

        var selectedCar = shared.SelectedCar;
        var result = new Dictionary<string, List<NotEnoughRosterRowData>>();


        if (shared.Cars.OfType<BaseLocomotive>().ToList().Count == 0) logger.Warning("No locomotives found.");

        var storedLocomotives = new List<BaseLocomotive>();

        foreach (var kvp in _filters)
        {
            var groupKey = kvp.Key;
            var filter = kvp.Value;

            var locomotives = shared.Cars
                .OfType<BaseLocomotive>()
                .Where(e => filter.Matches(e))
                .ToList();

            var rows = locomotives
                .OrderBy(e => !_cachedFavorites.Contains(e.id) ? 1 : 0)
                .ThenBy(e => e.SortName)
                .Select(e =>
                    new NotEnoughRosterRowData(e, _cachedFavorites.Contains(e.id), ReferenceEquals(selectedCar, e),
                        this))
                .ToList();

            storedLocomotives.AddRange(locomotives);

            result[groupKey] = rows;
        }

        var unMatched = shared.Cars.OfType<BaseLocomotive>()
            .Where(e => !storedLocomotives.Contains(e))
            .OrderBy(e => !_cachedFavorites.Contains(e.id) ? 1 : 0)
            .ThenBy(e => e.SortName)
            .Select(e =>
                new NotEnoughRosterRowData(e, _cachedFavorites.Contains(e.id), ReferenceEquals(selectedCar, e), this))
            .ToList();

        result["Other"] = unMatched;

        _hash = GetRowDataHash(result);
        _data = result;
    }

    public void SplitData(UIPanelBuilder builder)
    {
        var first = new Dictionary<string, List<NotEnoughRosterRowData>>();
        var second = new Dictionary<string, List<NotEnoughRosterRowData>>();

        // Keep sections in filter order: fill the left column until it holds about half the rows,
        // then continue in the right column.
        var sections = _data.Where(e => e.Value.Count > 0).ToList();
        var half = sections.Sum(e => e.Value.Count) / 2f;
        var countA = 0;
        var inSecond = false;

        foreach (var kvp in sections)
        {
            if (!inSecond && (countA == 0 || countA + kvp.Value.Count / 2f <= half))
            {
                first[kvp.Key] = kvp.Value;
                countA += kvp.Value.Count;
            }
            else
            {
                inSecond = true;
                second[kvp.Key] = kvp.Value;
            }
        }

        builder.HStack(ui =>
        {
            CreateLists(ui, first);
            CreateLists(ui, second);
        });
    }

    public override void Populate(UIPanelBuilder builder)
    {
        _scrollList.Clear();
#if RR2026
        // In 2026.1 the builder's Spacer stretches vertically too, and the row took the panel's spare height: the spacer
        // only pushes the button right.
        builder.HStack(h =>
        {
            var spacer = h.Spacer().GetComponent<LayoutElement>();
            if (spacer != null) spacer.flexibleHeight = 0f;
            h.AddButtonCompact("Edit Filters", () => NotEnoughRostersSettingsWindow.Shared?.Show());
        }).Height(30f);
#else
        builder.HStack(h =>
        {
            h.Spacer();
            h.AddButtonCompact("Edit Filters", () => NotEnoughRostersSettingsWindow.Shared?.Show());
        });
#endif
        if (_splitView && _data.Count > 1)
            SplitData(builder);
        else
            CreateLists(builder, _data);
        LayoutRebuilder.ForceRebuildLayoutImmediate(Window.GetComponent<RectTransform>());
    }

    private void CreateLists(UIPanelBuilder builder, Dictionary<string, List<NotEnoughRosterRowData>> list)
    {
        builder.VStack(vb =>
        {
            var clonedHeader = ReflectionUtils.InstantiateInBuilder(vb, _headerClone);
            clonedHeader.Height(15);

            foreach (var data in list)
            {
                if (data.Value.Count == 0) continue;

                vb.AddSection(data.Key);
                vb.VStack(ib => BuildRows(ib, data.Key, data.Value));
            }
        });
    }

    private void BuildRows(UIPanelBuilder builder, string key, List<NotEnoughRosterRowData> data)
    {
        if (data.Count == 0) return;

        // logger.Information("Building row with entries {0}", string.Join(", ", data.Select(e => e.Engine.DisplayName)));

        var scrollRect = ReflectionUtils.InstantiateInBuilder(builder, BuilderAssets.scrollRectVertical);
        var rectTransform = scrollRect.GetComponent<RectTransform>();
        var layoutElement = rectTransform.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 900f;
        layoutElement.preferredHeight =
            Mathf.Max(1f, _modifiedCell.GetComponent<RectTransform>().rect.height) * (data.Count + 1);
        scrollRect.content.GetComponent<ContentSizeFitter>().enabled = false;
        scrollRect.content.GetComponent<VerticalLayoutGroup>().enabled = false;
        scrollRect.gameObject.SetActive(false);
        var lazyScrollList = scrollRect.gameObject.AddComponent<LazyScrollList>();
        lazyScrollList.cellPrefab = _modifiedCell;
        lazyScrollList.SetData(data.Cast<object>().ToList());
        scrollRect.gameObject.SetActive(true);
        _scrollList.Add(lazyScrollList);

        StartCoroutine(LazyUpdate(lazyScrollList, data));
    }

    private IEnumerator LazyUpdate(LazyScrollList lazy, List<NotEnoughRosterRowData> data)
    {
        yield return null;
        lazy.SetData(data.Cast<object>().ToList());
        LayoutRebuilder.ForceRebuildLayoutImmediate(lazy.transform.parent as RectTransform);
    }

    public void ToggleFavorite(BaseLocomotive engine, bool favorite)
    {
        if (!_cachedFavorites.Add(engine.id)) _cachedFavorites.Remove(engine.id);

        SaveCachedFavorites();
        DataRebuild();
        Rebuild();
    }

    private void SaveCachedFavorites()
    {
#if RR2026
        PlayerPropertiesManager.Shared.SetFavoriteEngineIds(_cachedFavorites);
#else
        PlayerPropertiesManager.Shared.UpdateMyProperties(props =>
        {
            props.FavoriteEngineIds = _cachedFavorites;
            return props;
        });
#endif
    }

    public void SelectEngine(BaseLocomotive engine, bool select)
    {
        TrainController.Shared.SelectedCar = select ? engine : (Car)null;
        Refresh();
    }

    private int GetRowDataHash(Dictionary<string, List<NotEnoughRosterRowData>> data)
    {
        var rowDataHash = 19;
        foreach (var datas in data)
        {
            rowDataHash = rowDataHash * 31 + datas.Key.GetHashCode() * 31;
            foreach (var entry in datas.Value)
                rowDataHash = rowDataHash * 31 + entry.GetHashCode() * 31;
        }

        return rowDataHash;
    }
}