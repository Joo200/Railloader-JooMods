using System.Reflection;
using HarmonyLib;
using UI.Console;

namespace SignalsEverywhere.Rr2026;

// Railroader 2026.1's console finds commands by attribute in the game's own assembly only; SignalsEverywhere's are
// registered when the console starts (below). /signaldebug runs the Legacy debug command (dump signals|panel,
// distances), plus the mod tab's buttons, as the game has no mod tabs: reload (signal definitions) and rebuild-panel.
[ConsoleCommandHandler("/signaldebug", "SignalsEverywhere: dump, reload signals, rebuild the CTC panel")]
public class SignalDebugCommand
{
    private static DebugCommand Legacy => new(SignalsEverywhere.Shared.SignalCreator);

    [ConsoleSubcommand("dump", "Dump the signal data or the CTC panel layout to a file: signals|panel")]
    private static string Dump(string what) => Legacy.Execute(["/signaldebug", "dump", what]);

    [ConsoleSubcommand("distances", "Distances between the passenger stops")]
    private static string Distances() => Legacy.Execute(["/signaldebug", "distances", ""]);

    [ConsoleSubcommand("reload", "Reload the signal definitions from the mods")]
    private static string Reload() => SignalsEverywhere.Shared.ReloadSignals();

    [ConsoleSubcommand("rebuild-panel", "Reload the CTC panel layout and rebuild the panel")]
    private static string RebuildPanel() => SignalsEverywhere.Shared.ReloadPanel();

    [ConsoleSubcommand("show-panel", "Open the CTC panel (also the N key)")]
    private static string ShowPanel()
    {
        if (Panel.CTCPanel.Shared == null)
        {
            return "Requires the CTC feature";
        }
        Panel.CTCPanel.Shared.Show();
        return "CTC panel shown";
    }
}

[HarmonyPatch(typeof(ConsoleCommandHandler), "Awake")]
[HarmonyPatchCategory("SignalsEverywhere")]
internal static class RegisterConsoleCommands
{
    private static void Postfix(ConsoleCommandHandler __instance)
    {
        var processor = Traverse.Create(__instance).Field<CommandProcessor>("_processor").Value;
        processor?.RegisterHandlers(Assembly.GetExecutingAssembly());
    }
}
