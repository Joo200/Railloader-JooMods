using System;
#if RR2026
using UI.Windows;
#else
using System.Reflection;
using UI;
using UI.Common;
#endif
using UnityEngine;
#if !RR2026
using Object = UnityEngine.Object;
#endif

namespace NotEnoughRosters.Windows;

internal class WindowHelper
{
#if RR2026
    public static void CreateWindow<TWindow>(Action<TWindow> configure) where TWindow : Component, IBuilderWindow
    {
        WindowManager.Shared.CreateWindowWithComponent(configure);
    }
#else
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

    public static void CreateWindow<TWindow>(Action<TWindow> configure) where TWindow : Component, IProgrammaticWindow
    {
        var window = CreateWindow();
        window.name = typeof(TWindow).ToString();
        var twindow = window.gameObject.AddComponent<TWindow>();
        twindow.BuilderAssets = ProgrammaticWindowCreator.builderAssets;
        window.CloseWindow();
        window.SetInitialPositionSize(twindow.WindowIdentifier, twindow.DefaultSize, twindow.DefaultPosition,
            twindow.Sizing);
        if (configure != null) configure(twindow);
    }
#endif
}
