#nullable disable

using UI;
using UI.Builder;
using UI.Common;
using UnityEngine;

namespace SignalsEverywhere.Panel;

public abstract class WindowBase : MonoBehaviour, IProgrammaticWindow
{
    private UIPanel _panel;
    protected Window Window => GetComponent<Window>();
    public abstract string Title { get; }
    public UIBuilderAssets BuilderAssets { get; set; }
    public abstract string WindowIdentifier { get; }
    public abstract Vector2Int DefaultSize { get; }
    public abstract Window.Position DefaultPosition { get; }
    public abstract Window.Sizing Sizing { get; }
    public abstract void Populate(UIPanelBuilder builder);

    public void Rebuild()
    {
        if (_panel != null) _panel.Dispose();
        Window.Title = Title;
        _panel = UIPanel.Create(Window.contentRectTransform, BuilderAssets, Populate);
    }
}