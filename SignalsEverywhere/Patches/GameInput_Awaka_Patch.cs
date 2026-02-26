using System;
using System.Linq;
using Game;
using HarmonyLib;
using Network.Messages;
using UI;
using Serilog;
using SignalsEverywhere.Panel;
using UI.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SignalsEverywhere.Patches;

[HarmonyPatch(typeof(GameInput), "Awake")]
[HarmonyPatchCategory("SignalsEverywhere")]
public static class GameInput_Awake_Patch
{
    static void Postfix(GameInput __instance)
    {
        try
        {
            var map = __instance.inputActions.FindActionMap("Game");
            if (map == null) return;

            // 1. Create or find the action
            InputAction action = map.FindAction("Toggle CTC Panel") ?? map.AddAction("Toggle CTC Panel", binding: "<Keyboard>/n");

            // 2. Register callback with MovementInputEnabled check to avoid triggers while typing in UI
            action.performed -= OnPerformed;
            action.performed += OnPerformed;
            
            string json = PlayerPrefs.GetString("rebinds");
            if (!string.IsNullOrEmpty(json))
            {
                __instance.inputActions.LoadBindingOverridesFromJson(json);
            }
            // 3. Inject into the RebindableActions UI list so it shows in the settings menu
            // Index 2 is the "UI" category in GameInput
            var rebindables = __instance.RebindableActions;
            if (rebindables != null && rebindables.Length > 2)
            {
                var uiCategory = rebindables[2];
                if (uiCategory.actions.All(a => a.name != action.name))
                {
                    var newActions = uiCategory.actions.ToList();
                    newActions.Add(action);
                    rebindables[2] = (uiCategory.title, newActions.ToArray());
                }
            }

            action.Enable();
        }
        catch (Exception e)
        {
            Log.ForContext<SignalsEverywhere>().Error(e, "Error setting up CTC Panel");
        }
    }

    private static void OnPerformed(InputAction.CallbackContext ctx)
    {
        if (GameInput.MovementInputEnabled)
        {
            if (CTCPanel.Shared != null)
            {
                Log.ForContext<SignalsEverywhere>().Information("CTC Panel toggled");
                CTCPanel.Shared.Toggle();    
            }
            else
            {
                Alert alert = new Alert(AlertStyle.Toast, AlertLevel.Error, "Requires activated CTC Feature", TimeWeather.Now.TotalSeconds);
                WindowManager.Shared.Present(alert);
            }
            
        }
    }
}