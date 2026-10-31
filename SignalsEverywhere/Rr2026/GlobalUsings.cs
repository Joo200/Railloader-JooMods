// Railroader 2026.1 moved the window types (Window, WindowManager, Alert) to UI.Windows.
global using UI.Windows;
global using SignalsEverywhere.Rr2026;
// The graph event SignalsEverywhere rebuilds its signals on.
global using GraphDidChangeEvent = Track.GraphDidRebuildCollections;
