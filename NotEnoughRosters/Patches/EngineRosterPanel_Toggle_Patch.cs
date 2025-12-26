using HarmonyLib;
using UI.EngineRoster;

namespace NotEnoughRosters.Patches;

[HarmonyPatch(typeof(EngineRosterPanel))]
[HarmonyPatch(nameof(EngineRosterPanel.Toggle))]
[HarmonyPatchCategory("NotEnoughRosters")]
public class EngineRosterPanel_Toggle_Patch
{
    public static bool DisablePatch = false;

    private static bool Prefix()
    {
        if (DisablePatch) return true;
        NotEnoughRosterPanel.Shared.Toggle();
        return false;
    }
}