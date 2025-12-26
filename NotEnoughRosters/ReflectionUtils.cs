using System.Linq;
using System.Reflection;
using UI.Builder;
using UI.Common;
using UI.EngineRoster;
using UI.LazyScrollList;
using UnityEngine;

namespace NotEnoughRosters;

public static class ReflectionUtils
{
    public static RectTransform FindEngineRosterPanelHeader()
    {
        var type = typeof(EngineRosterPanel);
        var window = type.GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(EngineRosterPanel.Shared) as Window;

        return window.contentRectTransform.GetComponentsInChildren<RectTransform>().Where(e => e.name == "Header")
            .FirstOrDefault();
    }

    private static LazyScrollList GetLazyScrollList()
    {
        var type = typeof(EngineRosterPanel);
        return type.GetField("lazyScrollList", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(EngineRosterPanel.Shared) as LazyScrollList;
    }

    public static GameObject CloneScrollListPrefab()
    {
        var originalPrefab = GetLazyScrollList().cellPrefab;
        var clone = Object.Instantiate(originalPrefab);
        clone.SetActive(false);

        var oldCell = clone.GetComponent<EngineRosterRow>();
        var cell = clone.AddComponent<NotEnoughRosterRow>();
        cell.InjectFrom(oldCell);
        Object.DestroyImmediate(oldCell);

        return clone;
    }

    public static T InstantiateInBuilder<T>(UIPanelBuilder builder, T original) where T : Object
    {
        var type = typeof(UIPanelBuilder);
        var transform =
            type.GetField("_container", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(builder) as Transform;
        return Object.Instantiate(original, transform, false);
    }
}