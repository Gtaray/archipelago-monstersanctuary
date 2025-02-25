using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static SaveGameMenu;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        /// <summary>
        /// Replaces the Talk menu option with "Return to Start" 
        /// </summary>
        [HarmonyPatch(typeof(IngameBaseMenu), "Start")]
        public static class IngameBaseMenu_Start
        {
            public static void Prefix(IngameBaseMenu __instance)
            {
                // Creating a brand new menu item is probably not going to work, so lets change an existing one
                __instance.MenuItemTalk.SetText("Return to Start");
            }
        }

        /// <summary>
        /// Replaces the standard handling for the "Talk" menu item with a teleport to the start of the game.
        /// </summary>
        [HarmonyPatch(typeof(IngameBaseMenu), "OnItemSelected")]
        public static class IngameBaseMenu_OnItemSelected
        {
            public static bool Prefix(IngameBaseMenu __instance, MenuListItem item)
            {
                if (item == __instance.MenuItemTalk)
                {
                    __instance.Close();

                    var position = new Vector2(145, 76);
                    PlayerController.Instance.StartTeleport();
                    GameController.Instance.StartSceneChange(
                        "MountainPath_North1", 
                        position, 
                        GameController.SceneChangeType.Teleport, 
                        1.8f, 
                        "",
                        false);
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Adds custom handling for when a player selects "Return to Start" so that they correctly teleport
        /// to the first zone of the gamne. This is necessary to avoid soft-locks where a player gets into a place
        /// that they can't escape
        /// </summary>
        [HarmonyPatch(typeof(GameController), "OnSceneLoaded")]
        public static class GameController_OnSceneLoaded
        {
            public static bool Prefix(GameController __instance, ref Scene scene)
            {
                if (__instance.ChangeType == GameController.SceneChangeType.Teleport && scene.name == "MountainPath_North1")
                {
                    Patcher.Logger.LogDebug("Teleporting to Beginning of Game");
                    GameObject.FindWithTag("MapSettings").GetComponent<MapSettings>().UpdateMinimap();

                    PlayerController.Instance.gameObject.SetActive(true);
                    PlayerController.Instance.Sprite.color = GameDefines.WhiteZeroA;

                    ColorTween.StartTween(PlayerController.Instance.gameObject, GameDefines.WhiteZeroA, GameDefines.White, 0.25f, ColorTween.Type.Linear, 0f, false, 0, false);
                    ColorTween.QueueTween(PlayerController.Instance.gameObject, GameDefines.White, GameDefines.Gray, 0.25f, ColorTween.Type.Linear, 0f, false, 0);

                    OverlayController.Instance.StartFadeIn(Color.black, 0.2f);
                    Vector3 position = new Vector3(145, 76, 0);
                    PlayerController.Instance.Physics.SetPlayerOnGround(position, false);
                    CameraController.Instance.AssignMap();
                    CameraController.Instance.FocusPlayer();
                    PlayerController.Instance.SetDirection(1);

                    while (PlayerController.Instance.Physics.PhysObject.CheckGroundCollision())
                    {
                        position.y += 1f;
                        PlayerController.Instance.Physics.SetPlayerPosition(position, true, 0f, false);
                    }

                    __instance.GameState.SetState(GameStateManager.GameStates.AfterTeleportFadein, 0f);
                    UIController.Instance.Minimap.UpdateKeys();

                    if (PlayerController.Instance.Physics.IsRiding)
                    {
                        PlayerController.Instance.Follower.Monster.ExploreActionMountAbility.UpdateAction();
                        ColorTween.StartTween(PlayerController.Instance.Follower.gameObject, GameDefines.WhiteZeroA, GameDefines.White, 0.25f, ColorTween.Type.Linear, 0f, false, 0, false);
                        ColorTween.QueueTween(PlayerController.Instance.Follower.gameObject, GameDefines.White, GameDefines.Gray, 0.25f, ColorTween.Type.Linear, 0f, false, 0);
                    }

                    UIController.Instance.AreaTitle.Show();
                    __instance.SaveGameManager.SaveGame(false);
                    PlayerController.Instance.Minimap.UpdatePlayerPosition();
                    GC.Collect();

                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SaveGameMenu), "OnItemSelected")]
        public static class SaveGameMenu_OnItemSelected
        {
            public static bool Prefix(SaveGameMenu __instance, MenuListItem item)
            {
                // If we're already connected, just go through the normal method so we doing all this reflection
                if (APState.IsConnected)
                    return true;

                SaveGameSlot component = item.GetComponent<SaveGameSlot>();
                var menu = Traverse.Create(__instance);
                int currentPage = menu.Field("currentPage").GetValue<int>();
                var saveGameSlots = menu.Field("saveGameSlots").GetValue<List<SaveGameSlot>>();

                var slotToBeLoaded = currentPage * 10 + saveGameSlots.IndexOf(component);
                menu.Field("slotToBeLoaded").SetValue(slotToBeLoaded);

                var choosingCopySlot = menu.Field("choosingCopySlot").GetValue<bool>();
                var openType = menu.Field("openType").GetValue<EOpenType>();
                if (choosingCopySlot)
                {
                    return true;
                }
                else if (openType == EOpenType.NewGame)
                {
                    if (component.SaveData != null)
                    {
                        // Holy shit this is an absolute travesty of delegates, but it works
                        PopupController.Instance.ShowRequest(Utils.LOCA("Delete?"), Utils.LOCA("Delete existing progress?"),
                            () => PromptConnectToArchipelago(__instance,
                                () => menu.Method("StartNewGame").GetValue(),
                                () => ConfirmWithoutArchipelago(
                                    __instance,
                                    "Create a new save file while not connected to Archipelago?",
                                    () => menu.Method("StartNewGame").GetValue()), 
                                true),
                            () => menu.Method("OnNewGameCancelled").GetValue());
                        __instance.MenuList.SetLocked(locked: true);
                    }
                    else
                    {
                        PromptConnectToArchipelago(__instance,
                            () => menu.Method("StartNewGame").GetValue(),
                            () => ConfirmWithoutArchipelago(
                                __instance,
                                "Create a new save file while not connected to Archipelago?",
                                () => menu.Method("StartNewGame").GetValue()),
                            true);
                        __instance.MenuList.SetLocked(locked: true);
                    }
                }
                else if (openType == EOpenType.NewGamePlus)
                {
                    return true;
                }
                else
                {
                    // Either we load the game after connect to AP
                    // Or we prompt the user to load 
                    PromptConnectToArchipelago(__instance,
                        () => LoadGame(__instance),
                        () => ConfirmWithoutArchipelago(
                            __instance,
                            "Load this save file while not connected to Archipelago?",
                            () => LoadGame(__instance)),
                        false);
                    __instance.MenuList.SetLocked(locked: true);
                }

                return false;
            }

            private static void PromptConnectToArchipelago(SaveGameMenu __instance, PopupController.PopupDelegate postConnectionAction, PopupController.PopupDelegate disconnectedAction, bool withTimer = false)
            {
                if (withTimer)
                {
                    Timer.StartTimer(__instance.MainMenu.gameObject, 0.25f, () => PopupController.Instance.ShowRequest(
                        "Connect to Archipleago", 
                        "You are not connected to archipelago. Would you like to connect with your current info?",
                        () => ConnectToArchipelago(__instance, postConnectionAction),
                        () => disconnectedAction.Invoke()));
                    return;
                }

                Timer.StartTimer(__instance.MainMenu.gameObject, 0.25f, () => PopupController.Instance.ShowRequest(
                    "Connect to Archipleago",
                    "You are not connected to archipelago. Would you like to connect with your current info?",
                    () => ConnectToArchipelago(__instance, postConnectionAction),
                    () => disconnectedAction.Invoke()));

            }

            private static void ConfirmWithoutArchipelago(SaveGameMenu __instance, string message, PopupController.PopupDelegate confirm)
            {
                Timer.StartTimer(__instance.MainMenu.gameObject, 0.3f, () => PopupController.Instance.ShowRequest("Disconnected", message,
                    () => confirm.Invoke(),
                    () => __instance.MenuList.SetLocked(false),
                    true));
            }

            private static void ConnectToArchipelago(SaveGameMenu __instance, PopupController.PopupDelegate postConnectionAction)
            {
                if (APState.Connect())
                {
                    postConnectionAction.Invoke();
                    return;
                }

                PopupController.Instance.Close();
                Timer.StartTimer(__instance.MainMenu.gameObject, 0.3f, () => ShowConnectionFailedMessage(__instance));
                // If we failed, just throw up an error and then return back to the load game screen
                
                __instance.MenuList.SetLocked(false);
            }

            private static void ShowConnectionFailedMessage(SaveGameMenu __instance)
            {
                __instance.MenuList.SetLocked(true);
                PopupController.Instance.ShowMessage("Connection Failed", "Failed to connect to Archipelago. Check your connection settings and try again.",
                    () => __instance.MenuList.SetLocked(false));
            }

            private static void LoadGame(SaveGameMenu __instance)
            {
                // If we connected, then simply load the game.
                var menu = Traverse.Create(__instance);
                Timer.StartTimer(__instance.MainMenu.gameObject, 1f, () => menu.Method("LoadGame").GetValue());
                OverlayController.Instance.StartFadeOut(Color.black, 1f);
                menu.Method("Close").GetValue();
                __instance.MainMenu.MenuList.Close();
            }

        }

        [HarmonyPatch(typeof(SaveGameMenu), "ConfirmHeroVisuals")]
        public static class SaveGameMenu_ConfirmHeroVisuals
        {
            private static bool Prefix(SaveGameMenu __instance)
            {
                var menu = Traverse.Create(__instance);

                string name = string.Empty;
                if (APState.IsConnected)
                {
                    name = APState.ConnectionInfo.slot_name;
                }

                UIController.Instance.NameMenu.Open(
                    Utils.LOCA("Name your character"), 
                    name,
                    new NameMenu.ConfirmNameDelegate((name) => menu.Method("ConfirmHeroName", name).GetValue(name)), 
                    new Action(() => menu.Method("OnNameCancelled").GetValue()), 
                    NameMenu.ENameType.PlayerName);
                return false;
            }
        }

        [HarmonyPatch(typeof(PopupController), "ShowRequest")]
        public static class PopupController_ShowRequest
        {
            private static void Prefix(PopupController __instance, string headline)
            {
                var confirm = __instance.gameObject.transform.Find("MessageBox").Find("MenuRoot").Find("Confirm").Find("Text").GetComponent<tk2dTextMesh>();
                var cancel = __instance.gameObject.transform.Find("MessageBox").Find("MenuRoot").Find("Cancel").Find("Text").GetComponent<tk2dTextMesh>();

                if (headline == "Connect to Archipleago" || headline == "Disconnected")
                {
                    confirm.text = "Yes";
                    cancel.text = "No";
                }
                else
                {
                    confirm.text = "Ok";
                    cancel.text = "Cancel";
                }
            }
        }

        [HarmonyPatch(typeof(IngameBaseMenu), "GoBackToMainMenu")]
        public static class IngameBaseMenu_GoBackToMainMenu
        {
            private static void Postfix()
            {
                APState.InitiateDisconnect();
            }
        }
    }

    public class ReturnToStartMenuItem : MenuListItem
    {
        public void SetMenuText(string textValue)
        {
            SetText(textValue);
        }
    }
}
