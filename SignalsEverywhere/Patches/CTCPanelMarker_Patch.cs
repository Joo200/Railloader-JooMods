using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KeyValue.Runtime;
using Serilog;
using SignalsEverywhere.Panel;
using TMPro;
using Track.Signals.Panel;
using UI;
using UI.Builder;
using UI.Common;
using UI.ContextMenu;
using ContextMenu = UI.ContextMenu.ContextMenu;
using Object = UnityEngine.Object;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(CTCPanelMarker))]
[HarmonyPatchCategory("SignalsEverywhere")]
public class CTCPanelMarker_Patch
{
    [HarmonyPatch(nameof(CTCPanelMarker.ActivationFilter), MethodType.Getter)]
    [HarmonyPrefix]
    public static bool OverrideFilter(CTCPanelMarker __instance, ref PickableActivationFilter __result)
    {
        __result = PickableActivationFilter.Any;
        return false;
    }
    
    private static AccessTools.FieldRef<CTCPanelMarker, string> IdGetter = AccessTools.FieldRefAccess<CTCPanelMarker, string>("_id");
    private static AccessTools.FieldRef<CTCPanelMarker, TMP_Text> LabelGetter = AccessTools.FieldRefAccess<CTCPanelMarker, TMP_Text>("label");
    
    [HarmonyPatch("Activate")]
    [HarmonyPrefix]
    public static bool OverrideActivate(CTCPanelMarker __instance, PickableActivateEvent evt)
    {
        if (evt.Activation == PickableActivation.Secondary)
        {
            var id = "marker-" + IdGetter(__instance);
            var text = LabelGetter(__instance);
            ShowContextMenu(id, text.text, "Mainline");
            return false;
        }
        return true;
    }

    public static KeyValueObject GetKvo()
    {
        return Object.FindAnyObjectByType<CTCPanelMarkerManager>().GetComponentInParent<KeyValueObject>();
    }
    
    public static void ShowContextMenu(string id, string text, string branch)
    {
        ContextMenu shared = ContextMenu.Shared;
        if (ContextMenu.IsShown)
            shared.Hide();
        shared.Clear();
        shared.AddButton(ContextMenuQuadrant.General, "Edit", SpriteName.Inspect, () => {
            ModalAlertController.Present("Edit Marker", null, text, [
                (MenuButtons.Cancel, "Cancel"),
                (MenuButtons.Apply, "Apply")
            ], output => {
                if (output.Item1 == MenuButtons.Cancel)
                {
                    return;
                }
                var dictionary = GetKvo()[id].DictionaryValue.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                dictionary["text"] = Value.String(output.Item2);
                GetKvo()[id] = Value.Dictionary(dictionary);
            });
        });
        shared.AddButton(ContextMenuQuadrant.General, "Delete", SpriteName.Select, () =>
        {
            GetKvo()[id] = Value.Null();
        });
        shared.AddButton(ContextMenuQuadrant.Brakes, "Change Track", SpriteName.Handbrake, () =>
        {
            ModalAlertController.Present((b, a) => MarkerMovementBuilder(b, a, id, branch));
        });
        shared.Show("Panel Marker");
    }

    private static void MarkerMovementBuilder(UIPanelBuilder builder, Action dismiss, string id, string currentBranch)
    {
        builder.Spacing = 16f;
        builder.AddLabel("Move Marker", text =>
        {
            text.fontSize = 22f;
            text.horizontalAlignment = HorizontalAlignmentOptions.Center;
        });
        List<string> values = CTCPanel.Shared.Branches;
        var selected = currentBranch;
        builder.AddDropdown(values, values.IndexOf(currentBranch), s => selected = values[s]);
        builder.AlertButtons(b2 =>
        {
            b2.AddButtonMedium("Cancel", dismiss);
            b2.AddButtonMedium("Apply", () =>
            {
                MoveMarkerToBranch(id, currentBranch, selected);
                dismiss();
            });
        });
    }

    private enum MenuButtons
    {
        Apply,
        Cancel
    }

    private static void MoveMarkerToBranch(string id, string currentBranch, string branch)
    {
        if (branch == currentBranch)
        {
            Log.Warning($"Tried to move marker to same branch: {id}, {branch}");
            return;
        }
        Log.Information($"Move marker {id} from {currentBranch} to {branch}");
        var kvo = GetKvo();
        var dictionary = kvo[id].DictionaryValue.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        kvo[id] = Value.Null();
        dictionary["x"] = -1;
        dictionary["y"] = -1;
        kvo[PanelMarker.CreateMarkerId(branch)] = Value.Dictionary(dictionary);
    }
}