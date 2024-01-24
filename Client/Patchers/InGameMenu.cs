using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEngine.SceneManagement;

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
            public static bool Prefix(GameController __instance, Scene scene)
            {
                if (__instance.ChangeType == GameController.SceneChangeType.Teleport && scene.name == "MountainPath_North1")
                {
                    Logger.LogDebug("Teleporting to Beginning of Game");
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
    }

    public class ReturnToStartMenuItem : MenuListItem
    {
        public void SetMenuText(string textValue)
        {
            SetText(textValue);
        }
    }
}
