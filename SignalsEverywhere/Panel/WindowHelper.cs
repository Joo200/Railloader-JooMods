using System;
using System.Reflection;
using UI;
using UI.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Panel;

#if RR2026
internal static class WindowHelper
{
    // Railroader 2026.1 creates the window (closed) with the component on it.
    public static void CreateWindow<TWindow>() where TWindow : Component, UI.Windows.IBuilderWindow
    {
        UI.Windows.WindowManager.Shared.CreateWindowWithComponent<TWindow>();
    }
}
#else
internal static class WindowHelper
{
    private static ProgrammaticWindowCreator _programmaticWindowCreator;

    private static ProgrammaticWindowCreator ProgrammaticWindowCreator
    {
        get
        {
            if (_programmaticWindowCreator == null)
                _programmaticWindowCreator = Object.FindObjectOfType<ProgrammaticWindowCreator>(true);
            return _programmaticWindowCreator;
        }
    }

    private static Window CreateWindow()
    {
        var createWindow = typeof(ProgrammaticWindowCreator).GetMethod("CreateWindow",
            BindingFlags.NonPublic | BindingFlags.Instance, null, [], null);
        return (Window)createWindow.Invoke(ProgrammaticWindowCreator, null);
    }

    public static void CreateWindow<TWindow>() where TWindow : Component, IProgrammaticWindow
    {
        var window = CreateWindow();
        window.name = typeof(TWindow).ToString();
        var twindow = window.gameObject.AddComponent<TWindow>();
        twindow.BuilderAssets = ProgrammaticWindowCreator.builderAssets;
        window.CloseWindow();
        window.SetInitialPositionSize(twindow.WindowIdentifier, twindow.DefaultSize, twindow.DefaultPosition,
            twindow.Sizing);
    }
}
#endif
