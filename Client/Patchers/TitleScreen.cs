using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MonsterSanctuary.Client.Options;
using Archipelago.MonsterSanctuary.Client.Persistence;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Policy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SaveGameMenu;

namespace Archipelago.MonsterSanctuary.Client
{
    public partial class Patcher
    {
        private const string host_name_debug = "localhost:38281";
        private const string slot_name_debug = "Saagael";
        private static string host_name;
        private static string slot_name;
        private static string password;

        #region Main Menu
        [HarmonyPatch(typeof(MainMenu), "OnItemSelected")]
        public static class MainMenu_OnItemSelected
        {
            public static bool Prefix(MainMenu __instance, MenuListItem item)
            {
                // We should never be connected when we're on the main menu
                if (ApState.IsConnected)
                {
                    ApState.InitiateDisconnect();
                }

                // We only want to handle the New Game button, everything else goes through the normal flow
                if (item == __instance.ButtonNewGame)
                {
                    PromptToEnterArchipelagoConnectionInfo(
                        __instance,
                        OpenNewGameMenu,
                        MainMenuSetupCancelled);
                    __instance.MenuList.SetLocked(true);
                    return false;
                }

                // If we're continuing, then don't connect from the main menu
                // Instead we'll connect when the file is selected, because that file will have the connection information for that AP server

                return true;
            }

            private static void OpenNewGameMenu(MainMenu __instance)
            {
                if (__instance.SaveGameMenu.NewGamePlusAvailable || OptionsManager.Instance.OptionsData.AlternateGameModes)
                {
                    Traverse.Create(__instance).Method("OpenNewGamePopup").GetValue();
                }
                else
                {
                    PlayerController.Instance.TimerEnabled = false;
                    GameModeManager.Instance.BraveryMode = false;
                    GameModeManager.Instance.RandomizerMode = false;
                    GameModeManager.Instance.PermadeathMode = false;
                    GameModeManager.Instance.RelicMode = false;
                    __instance.SaveGameMenu.Open(SaveGameMenu.EOpenType.NewGame);
                    __instance.MenuList.SetLocked(true);
                }
            }

            private static void OpenContinueMenu(MainMenu __instance)
            {
                __instance.SaveGameMenu.Open(SaveGameMenu.EOpenType.LoadGame);
                __instance.MenuList.SetLocked(true);
            }
        }

        [HarmonyPatch(typeof(MainMenu), "ReturnFromSubMenu")]
        public static class MainMenu_ReturnFromSubMenu
        {
            // We never want to be connected to AP while on the main menu.
            private static void Prefix()
            {
                ApState.InitiateDisconnect();
            }
        }
        #endregion

        #region New Game Menu
        [HarmonyPatch(typeof(NewGameMenu), "UpdateButtonText")]
        public static class NewGameMenu_UpdateButtonText
        {
            private static void Prefix(NewGameMenu __instance, ref bool ___relicMode)
            {
                if (!ApState.IsConnected)
                    return;

                ___relicMode = SlotData.IncludeChaosRelics;
            }
        }

        [HarmonyPatch(typeof(NewGameMenu), "Open")]
        public static class NewGameMenu_Open
        {
            private static void Postfix(NewGameMenu __instance, ref bool ___timer)
            {
                if (!ApState.IsConnected)
                    return;

                __instance.BraveryItem.SetDisabled(true);
                __instance.RandomizerItem.SetDisabled(true);
                __instance.RelicItem.SetDisabled(true);
                __instance.NewGamePlusItem.SetDisabled(true);

                // Force timer to be on by default, but don't disable it.
                ___timer = true;
                __instance.TimerItem.Text.text = GetOnOffText(true);
            }

            private static string GetOnOffText(bool on)
            {
                return !on ? Utils.LOCA("Off") : GameDefines.FormatTextAsGreen(Utils.LOCA("On"), false);
            }
        }
        #endregion

        #region Save Game Menu
        [HarmonyPatch(typeof(SaveGameMenu), "OnItemSelected")]
        public static class SaveGameMenu_OnItemSelected
        {
            private static bool Prefix(SaveGameMenu __instance, MenuListItem item, EOpenType ___openType, bool ___choosingCopySlot, int ___currentPage, List<SaveGameSlot> ___saveGameSlots, ref int ___slotToBeLoaded)
            {
                if (___choosingCopySlot)
                {
                    return true;
                }

                SaveGameSlot component = item.GetComponent<SaveGameSlot>();
                ___slotToBeLoaded = ___currentPage * 10 + ___saveGameSlots.IndexOf(component);

                if (___openType == EOpenType.NewGamePlus)
                {
                    return true;
                }

                if (___openType == EOpenType.NewGame)
                {
                    // If we're not connected to AP, we can simply end early and let the normal process take over.
                    // This is because we should be connected to AP before selecting a New Game save file. 
                    // If we're not connected, it means we create this save outside of AP.
                    if (!ApState.IsConnected)
                        return true;

                    if (component.SaveData != null)
                    {
                        PromptToDeleteExistingFile(__instance, ___slotToBeLoaded);
                        return false;
                    }
                    else
                    {
                        StartGame(__instance);
                        return false;
                    }
                }
                else
                {
                    // We never ever, ever want to be connected to AP while selecting a save file to Continue
                    if (ApState.IsConnected)
                        ApState.InitiateDisconnect();

                    // When continuing, we wait until the file is selected before we prompt the user to connect to AP
                    // This is because we can pull the connection info from the ApDataFile associated with the save slot

                    // If there's no AP data for this save slot, then we can load the game without connecting to AP.
                    if (!ApData.ApDataExistsForSaveSlot(___slotToBeLoaded))
                    {
                        return true;
                    }

                    // Load the data for the selected save file
                    if (!ApData.LoadFileForSaveSlot(___slotToBeLoaded))
                    {
                        // If we failed to load a file for this slot (for some weird reason). Show an error and back out.
                        ShowMessage(__instance,
                            "Error",
                            $"Failed to load Archipelago data file for slot {___slotToBeLoaded}. Check the logs for more details.",
                            OnLoadCancelled);
                        __instance.MenuList.SetLocked(true);
                        return false;
                    }

                    // TODO: We could make it so that if either of these things are null/empty, we prompt the player to enter them again
                    // But I would rather throw errors for now, as that will help bubble any underlying problems up to the surface.
                    // Because in theory this should never happen. Save files should not have data removed from them, ever.
                    if (string.IsNullOrEmpty(ApData.CurrentFile.ConnectionInfo.HostName))
                    {
                        Patcher.Logger.LogError($"Saved Host Name for save slot {___slotToBeLoaded} is null or empty. Something has happened to the data file for this slot, and must be manually fixed");
                        ShowMessage(__instance,
                            "Error",
                            $"Saved Host Name was empty. Something has happened to the data file for this save, and must be manually fixed",
                            OnLoadCancelled);
                        __instance.MenuList.SetLocked(true);
                        return false;
                    }
                    if (string.IsNullOrEmpty(ApData.CurrentFile.ConnectionInfo.SlotName))
                    {
                        Patcher.Logger.LogError($"Saved Slot Name for save slot {___slotToBeLoaded} is null or empty. Something has happened to the data file for this slot, and must be manually fixed");
                        ShowMessage(__instance,
                            "Error",
                            $"Saved Slot Name was empty. Something has happened to the data file for this save, and must be manually fixed",
                            OnLoadCancelled);
                        __instance.MenuList.SetLocked(true);
                        return false;
                    }

                    // If we're not connected to AP, we simply pull the connection info from the data file and have the user confirm they want to connect.
                    // Prompt to connect using the info saved in the datafile
                    host_name = ApData.CurrentFile.ConnectionInfo.HostName;
                    slot_name = ApData.CurrentFile.ConnectionInfo.SlotName;
                    password = ApData.CurrentFile.ConnectionInfo.Password;

                    PromptConfirmConnectionInformation<SaveGameMenu>(
                        __instance,
                        LoadGame, // If the player accepts and the connection is successful
                        OnLoadCancelled, // If the player accepts and the connectoin fails
                        (connected, notconnected, cancelled) => OnLoadCancelled(__instance)); // If the player cancels
                    __instance.MenuList.SetLocked(true);

                    return false;
                }
            }

            private static void ShowMessage(SaveGameMenu __instance, string header, string message, Action<SaveGameMenu> callback)
            {
                Timer.StartTimer(__instance.gameObject, 0.25f, () => PopupController.Instance.ShowMessage(
                    header,
                    message,
                    () => callback.Invoke(__instance)));
            }

            private static void PromptToDeleteExistingFile(SaveGameMenu __instance, int slotToBeLoaded)
            {
                string warning = ApData.ApDataExistsForSaveSlot(slotToBeLoaded)
                    ? Utils.LOCA("This file has Archipelago data. Delete existing progress?")
                    : Utils.LOCA("Delete existing progress?");

                PopupController.Instance.ShowRequest(
                    Utils.LOCA("Delete?"),
                    warning,
                    () => StartGame(__instance),
                    () => __instance.MenuList.SetLocked(false));

                __instance.MenuList.SetLocked(true);
            }

            private static void StartGame(SaveGameMenu __instance)
            {
                Traverse
                    .Create(__instance)
                    .Method("StartNewGame")
                    .GetValue();
            }

            private static void LoadGame(SaveGameMenu __instance)
            {
                var menu = Traverse.Create(__instance);
                var loadgame = menu.Method("LoadGame");
                var close = menu.Method("Close");

                Timer.StartTimer(__instance.MainMenu.gameObject, 1f, () => loadgame.GetValue());
                OverlayController.Instance.StartFadeOut(Color.black, 1f);
                close.GetValue();
                __instance.MainMenu.MenuList.Close();
            }

            private static void OnLoadCancelled(SaveGameMenu __instance) => __instance.MenuList.SetLocked(false);
        }

        [HarmonyPatch(typeof(SaveGameMenu), "ConfirmHeroVisuals")]
        public static class SaveGameMenu_ConfirmHeroVisuals
        {
            private static bool Prefix(SaveGameMenu __instance)
            {
                var menu = Traverse.Create(__instance);

                UIController.Instance.NameMenu.Open(
                    Utils.LOCA("Name your character"),
                    GetSlotName(),
                    new NameMenu.ConfirmNameDelegate((name) => menu.Method("ConfirmHeroName", name).GetValue(name)),
                    new Action(() => menu.Method("OnNameCancelled").GetValue()),
                    NameMenu.ENameType.PlayerName);

                return false;
            }

            private static string GetSlotName()
            {
                return string.IsNullOrEmpty(slot_name) ? string.Empty : slot_name;
            }
        }

        [HarmonyPatch(typeof(SaveGameMenu), "ConfirmHeroName")]
        public static class SaveGameMenu_ConfirmHeroName
        {
            // This makes sure that when we start a new file (after entering character name)
            // That any existing save file for that slot gets deleted
            private static void Prefix(SaveGameMenu __instance, int ___slotToBeLoaded)
            {
                if (ApData.ApDataExistsForSaveSlot(___slotToBeLoaded))
                {
                    ApData.DeleteFileForSaveSlot(___slotToBeLoaded);
                }
            }
        }

        [HarmonyPatch(typeof(SaveGameMenu), "ConfirmDeleteSavegame2")]
        public static class SaveGameMenu_ConfirmDeleteSavegame2
        {
            // This makes sure that when we start a new file (after entering character name)
            // That any existing save file for that slot gets deleted
            private static void Prefix(SaveGameMenu __instance, int ___currentPage, List<SaveGameSlot> ___saveGameSlots)
            {
                var comp = __instance.MenuList.CurrentSelected.GetComponent<SaveGameSlot>();

                var saveSlot = ___currentPage * 10 + ___saveGameSlots.IndexOf(comp);

                // We should never be connected to AP when deleting a file, since its from the Continue menu
                if (ApState.IsConnected)
                {
                    Patcher.Logger.LogError("Something went wrong. You are connected to AP after selecting the 'Continue' from the main menu, which shouldn't happen.");
                    return;
                }

                if (ApData.ApDataExistsForSaveSlot(saveSlot))
                {
                    // We're guaranteed not connected to AP at this point, so its safe to load the file data for the slot we are to delete
                    ApData.DeleteFileForSaveSlot(saveSlot);
                }
            }
        }

        // Once we get to this method, we're creating the new game
        // So we can initialize a new persistence file
        [HarmonyPatch(typeof(SaveGameMenu), "StartNewGameTransition")]
        public static class SaveGameMenu_StartNewGameTransition
        {
            private static void Prefix(SaveGameMenu __instance, int ___slotToBeLoaded)
            {
                if (!ApState.IsConnected)
                {
                    return;
                }

                ApData.CreateFileForSaveSlot(___slotToBeLoaded);
                ApData.LoadFileForSaveSlot(___slotToBeLoaded);
                ApData.SetConnectionDataCurrentFile(host_name, slot_name, password);
            }
        }
        #endregion

        #region Save Game Slots
        [HarmonyPatch(typeof(SaveGameSlot), "SetData")]
        public static class SaveGameSlot_SetData
        {
            private static void Postfix(SaveGameSlot __instance, int slot)
            {
                var playerGameObject = __instance.PlayerName.transform.parent.gameObject;
                var apmarker = playerGameObject.transform.Find("ArchipelagoMarker");
                if (apmarker == null)
                {
                    var go = GameObject.Instantiate(__instance.PlayerName.gameObject, playerGameObject.transform);
                    go.name = "ArchipelagoMarker";

                    var mesh = go.GetComponent<tk2dTextMesh>();
                    mesh.text = "AP";

                    apmarker = go.transform;
                    apmarker.localPosition = new Vector3(-25, -23, 0f);
                }

                if (apmarker == null)
                {
                    Patcher.Logger.LogError("Failed to create or find AP marker for save slot " + slot);
                    return;
                }

                apmarker.gameObject.SetActive(ApData.ApDataExistsForSaveSlot(slot));
            }
        }
        #endregion

        #region Version information
        [HarmonyPatch(typeof(UIController), "Start")]
        public static class UIController_Start
        {
            private static void Postfix(UIController __instance)
            {
                __instance.VersionString.text = "AP Client v" + Patcher.ClientVersion;
            }
        }
        #endregion

        #region Connection Prompts and Helpers
        private static void PromptToEnterArchipelagoConnectionInfo<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null) where T : MonoBehaviour
        {
            Timer.StartTimer(__instance.gameObject, 0.25f, () => PopupController.Instance.ShowRequest(
                "Connect to Archipelago?",
                "You are not connected to Archipelago. Enter your Archipelago connection information?",
                () => PromptToEnterArchipelagoUrl(__instance, postConnectAction, failedConnectionAction),
                () => PromptToStartWithoutArchipelago(__instance, postConnectAction, failedConnectionAction)));
        }

        private static void PromptToStartWithoutArchipelago<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null) where T : MonoBehaviour
        {
            Timer.StartTimer(__instance.gameObject, 0.25f, () => PopupController.Instance.ShowRequest(
                "Start Without Archipelago?",
                "Files created while not connected to Archipelago can never be used with Archipelago. Continue?",
                () =>
                {
                    if (postConnectAction != null)
                        postConnectAction.Invoke(__instance);
                },
                () =>
                {
                    if (failedConnectionAction != null)
                        failedConnectionAction.Invoke(__instance);
                }));
        }

        private static void PromptToEnterArchipelagoUrl<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null) where T : MonoBehaviour
        {
            // Set the text limit for the NameMenus that pop up when entering data
            var inputfield = Traverse.Create(UIController.Instance.NameMenu.DirectKeyboardSupportHandler)
                .Field("InputField")
                .GetValue() as MSInputField;
            inputfield.characterLimit = 20;

            Timer.StartTimer(__instance.gameObject, 0.25f, () => UIController.Instance.NameMenu.Open(
                Utils.LOCA("Enter the host name"),
#if DEBUG
                host_name_debug,
#else
                "archipelago.gg:",
#endif
                (string url) =>
                {
                    Patcher.host_name = url;
                    PromptToEnterArchipelagoSlotName(__instance, postConnectAction, failedConnectionAction);
                },
                () =>
                {
                    if (failedConnectionAction != null)
                        failedConnectionAction.Invoke(__instance);
                },
                NameMenu.ENameType.MapMarker));
        }

        private static void PromptToEnterArchipelagoSlotName<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null) where T : MonoBehaviour
        {
            Timer.StartTimer(__instance.gameObject, 0.25f, () => UIController.Instance.NameMenu.Open(
                Utils.LOCA("Enter your slot name"),
#if DEBUG
                slot_name_debug,
#else
                String.Empty,
#endif
                (string slotname) =>
                {
                    Patcher.slot_name = slotname;
                    PromptToEnterArchipelagoPassword(__instance, postConnectAction, failedConnectionAction);
                },
                () => PromptToEnterArchipelagoUrl(__instance, postConnectAction, failedConnectionAction),
                NameMenu.ENameType.MapMarker));
        }

        private static void PromptToEnterArchipelagoPassword<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null) where T : MonoBehaviour
        {
            Timer.StartTimer(__instance.gameObject, 0.25f, () => UIController.Instance.NameMenu.Open(
                Utils.LOCA("Enter your password"),
                String.Empty,
                (string password) =>
                {
                    Patcher.password = password;
                    PromptConfirmConnectionInformation(__instance, postConnectAction, failedConnectionAction);
                },
                () => PromptToEnterArchipelagoSlotName(__instance, postConnectAction, failedConnectionAction),
                NameMenu.ENameType.MapMarker));
        }

        private static void PromptConfirmConnectionInformation<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null, Action<T, Action<T>, Action<T>> cancelledAction = null) where T : MonoBehaviour
        {
            if (cancelledAction == null) 
            {
                cancelledAction = PromptToEnterArchipelagoUrl;
            }
            Timer.StartTimer(__instance.gameObject, 0.25f, () => PopupController.Instance.ShowRequest(
                "Confirm Connection Details",
                $"Host name: {host_name}\nSlot name: {slot_name}\nPassword: {password}",
                () => ConnectToArchipelagoAndContinue(__instance, postConnectAction, failedConnectionAction),
                () => cancelledAction.Invoke(__instance, postConnectAction, failedConnectionAction)));
        }

        private static void ConnectToArchipelagoAndContinue<T>(T __instance, Action<T> postConnectAction = null, Action<T> failedConnectionAction = null) where T : MonoBehaviour
        {
            Patcher.Logger.LogInfo("ConnectToArchipelagoAndContinue()");
            if (ApState.Connect(host_name, slot_name, password))
            {
                if (SlotData.Version != Patcher.ClientVersion)
                {
                    ShowVersionMismatchErrorAndDisconnect(__instance, failedConnectionAction);
                    return;
                }
                
                if (postConnectAction != null)
                {
                    postConnectAction.Invoke(__instance);
                }
            }
            else
            {
                PopupController.Instance.Close();
                Timer.StartTimer(__instance.gameObject, 0.25f, () => PopupController.Instance.ShowMessage(
                    "Connection Failed",
                    "Failed to connect to Archipelago. Check your connection settings and try again.",
                    () =>
                    {
                        if (failedConnectionAction != null)
                            failedConnectionAction.Invoke(__instance);
                    }));
            }
        }

        private static void ShowVersionMismatchErrorAndDisconnect<T>(T __instance, Action<T> onCloseAction = null) where T : MonoBehaviour
        {
            Timer.StartTimer(__instance.gameObject, 0.25f, () => PopupController.Instance.ShowMessage(
                "Version Mismatch",
                $"Detected a version mismatch between the client ({Patcher.ClientVersion}) and AP world ({SlotData.Version}). Disconnecting.",
                () =>
                {
                    ApState.InitiateDisconnect();
                    if (onCloseAction != null)
                        onCloseAction.Invoke(__instance);
                }));
        }

        private static void MainMenuSetupCancelled(MainMenu __instance) => __instance.MenuList.SetLocked(false);
#endregion
    }
}
