#nullable disable

using UI;
using UI.Builder;
using UI.Common;
using UnityEngine;
#if RR2026
using UI.Windows;
#endif

namespace SignalsEverywhere.Panel;

#if RR2026
// Railroader 2026.1: WindowManager builds a builder window from its identifier and WindowConfig.
public abstract class WindowBase : MonoBehaviour, IBuilderWindow
#else
public abstract class WindowBase : MonoBehaviour, IProgrammaticWindow
#endif
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

#if RR2026
    string IBuilderWindow.Identifier => WindowIdentifier;

    public WindowConfig Config => new WindowConfig(DefaultSize, DefaultPosition, Sizing);
#endif

    public void Rebuild()
    {
        if (_panel != null) _panel.Dispose();
        Window.Title = Title;
        _panel = UIPanel.Create(Window.contentRectTransform, BuilderAssets, Populate);
#if RR2026
        if (this is CTCPanel)
            PanelScrolling.Apply(this);
#endif
    }
}