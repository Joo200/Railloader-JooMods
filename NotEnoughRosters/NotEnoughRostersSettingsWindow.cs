using NotEnoughRosters.Windows;
using UI.Builder;
using UI.Common;
#if RR2026
using UI.Windows;
#endif
using UnityEngine;

#nullable disable
namespace NotEnoughRosters;

public class NotEnoughRostersSettingsWindow : WindowBase
{
    public static NotEnoughRostersSettingsWindow Shared;

    public override string WindowIdentifier => "NERSettings";
    public override string Title => "Not Enough Rosters Filters";
    public override Vector2Int DefaultSize => new(700, 600);
    public override Window.Position DefaultPosition => Window.Position.Center;
    public override Window.Sizing Sizing => Window.Sizing.Resizable(new Vector2Int(500, 300));

    public static void CreateInstance()
    {
        WindowHelper.CreateWindow<NotEnoughRostersSettingsWindow>(null);
        Shared = WindowManager.Shared.GetWindow<NotEnoughRostersSettingsWindow>();
    }

    public void Show()
    {
        Rebuild();
        Window.ShowWindow();
    }

    public override void Populate(UIPanelBuilder builder)
    {
        NotEnoughRosters.Shared.BuildFilterEditor(builder, Rebuild);
    }
}
