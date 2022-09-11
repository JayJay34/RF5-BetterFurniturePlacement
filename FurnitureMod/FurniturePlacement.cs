using BepInEx;
using BepInEx.Configuration;
using BepInEx.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Net;
using System.Net.Sockets;
using Il2CppSystem.Collections.Generic;

namespace FurnitureMod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]

    public class FurnitureMod : BasePlugin
    {

        public const string pluginGuid = "BetterFurniturePlacementMod";
        public const string pluginName = "Better Furniture Placement Mod";
        public const string pluginVersion = "0.4";

        internal static new ManualLogSource Log;
        public static PlayerCharacterController playerController;
        public static Character newCharacter;
        public static bool shadowIgnore = false;
        public static GameObject newButton;
        public static float gridSize = 0.25f;

        public static ConfigEntry<bool> bFurnitureSnapping;
        public static ConfigEntry<bool> bAlwaysIgnore;
        public static ConfigEntry<KeyCode> kToggleKey;
        public static ConfigEntry<RF5Input.Key> kToggleButton;
        public static ConfigEntry<KeyCode> kIncreaseGrid;
        public static ConfigEntry<KeyCode> kDecreaseGrid;

        public override void Load()
        {
            Log = base.Log;
            Log.LogInfo($"Plugin {pluginGuid} is loaded!");
            bAlwaysIgnore = Config.Bind("Always Ignore Placement Collison", "AlwaysIgnore", false, "Set to true to always Ignore Furniture Placement Collison");
            bFurnitureSnapping = Config.Bind("Furniture Snap to Grid", "SnapToGrid", true, "Set to true to snap furniture to a grid");
            kToggleKey = Config.Bind("Key to Ignore Placement", "IgnoreKey", KeyCode.LeftShift, "Set the Key you wish to press while Holding Furniture to Ignore Placement Collison (Not required if AlwaysIgnore is true)");
            kToggleButton = Config.Bind("Button to Ignore Placement", "IgnoreButton", RF5Input.Key.R, "Set the Button you wish to press while Holding Furniture to Ignore Placement Collison (Not required if AlwaysIgnore is true)");
            kIncreaseGrid = Config.Bind("Button to Increase Grid Size", "IncreaseGrid", KeyCode.Period, "Set the Key you wish to press to Increase grid size");
            kDecreaseGrid = Config.Bind("Button to Decrease Grid Size", "DecreaseGrid", KeyCode.Comma, "Set the Key you wish to press to Decrease grid size");
            Harmony.CreateAndPatchAll(typeof(FurnitureChanges));
        }

        [HarmonyPatch]
        public class FurnitureChanges
        {
            [HarmonyPatch(typeof(PlayerCharacterController), nameof(Awake), new Type[] { })]
            [HarmonyPostfix]
            public static void Awake(PlayerCharacterController __instance)
            {

                if (playerController == null)
                {
                    Log.LogInfo($"Player Controller Set");
                    playerController = __instance;
                }

            }

            public static void CreateIgnoreBtn()
            {
                if (newButton == null)
                {
                    newButton = GameObject.Instantiate(GameObject.Find("B"), new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), GameObject.Find("OnHandUI").transform);
                    newButton.name = "ShadowIgnoreBtn";
                    newButton.GetComponent<RectTransform>().anchoredPosition = GameObject.Find("B").GetComponent<RectTransform>().anchoredPosition + new Vector2(60, 80);
                    newButton.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 103);
                    newButton.GetChild(0).GetComponent<RectTransform>().anchoredPosition = newButton.GetChild(0).GetComponent<RectTransform>().anchoredPosition + new Vector2(-57.7001f, 0);
                    newButton.GetChild(1).GetComponent<RectTransform>().sizeDelta = new Vector2(400, 30);
                    int testKeyImage = Convert.ToInt32(Enum.Parse(typeof(ButtonSpriteManager.KeyImageType), kToggleButton.Value.ToString()));

                    newButton.GetChild(0).GetComponent<ButtonImageController>().nowButtonType = (ButtonSpriteManager.KeyImageType)testKeyImage;
                    newButton.GetChild(0).GetComponent<ButtonImageController>().startButtonType = (ButtonSpriteManager.KeyImageType)testKeyImage;
                    newButton.GetChild(0).GetComponent<ButtonImageController>().RefreshImage();
                    newButton.GetComponentInChildren<SText>().text = "IGNORE BLOCK";
                }

            }

            [HarmonyPatch(typeof(ItemFurniture.PlayerItemFurniture2), nameof(Update), new Type[] { })]
            [HarmonyPostfix]
            public static void Update(ItemFurniture.PlayerItemFurniture2 __instance)
            {
                if (__instance.CurrentState == ItemFurniture.PlayerItemFurniture2.State.Hold)
                {
                    if (GameObject.Find("B") != null)
                    {
                        CreateIgnoreBtn();
                    }
                    if (bAlwaysIgnore.Value)
                    {
                        shadowIgnore = true;
                    }
                    else
                    {
                        if (UnityEngine.Input.GetKeyDown(kToggleKey.Value) || RF5Input.Pad.Edge(kToggleButton.Value))
                        {
                            shadowIgnore = true;
                        }
                        if (UnityEngine.Input.GetKeyUp(kToggleKey.Value) || RF5Input.Pad.End(kToggleButton.Value))
                        {
                            shadowIgnore = false;
                        }
                        if (UnityEngine.Input.GetKeyDown(kIncreaseGrid.Value))
                        {
                            gridSize = Mathf.Clamp((gridSize + 0.05f), 0.05f, 1f);
                        }
                        if (UnityEngine.Input.GetKeyUp(kDecreaseGrid.Value))
                        {
                            gridSize = Mathf.Clamp((gridSize - 0.05f), 0.05f, 1f);
                        }
                    }
                }
            }

            [HarmonyPatch(typeof(ItemFurniture.FurnitureShadow), nameof(updateDisp), new Type[] { })]
            [HarmonyPostfix]
            public static void updateDisp(ItemFurniture.FurnitureShadow __instance)
            {
                if (__instance.isActive)
                {
                    if (shadowIgnore)
                    {
                        __instance._materials[0].SetColor("_Color", new Color(1, 1, 1, 0.5f));
                    }
                }
            }

            [HarmonyPatch(typeof(ItemFurniture.FurnitureShadow), nameof(CheckHit), new Type[] { })]
            [HarmonyPrefix]
            public static bool CheckHit(ItemFurniture.FurnitureShadow __instance)
            {
                if (__instance.isActive)
                {
                    Vector3 oldLocation = __instance.transform.position;
                    Vector3 oldRotation = __instance.transform.eulerAngles;
                    if (shadowIgnore)
                    {
                        __instance.isHit = !shadowIgnore;
                        if (bFurnitureSnapping.Value)
                        {
                            __instance.transform.position = new Vector3(((Mathf.Round(oldLocation.x / gridSize)) * gridSize), oldLocation.y, ((Mathf.Round(oldLocation.z / gridSize)) * gridSize));
                            __instance.transform.eulerAngles = new Vector3(oldRotation.x, (Mathf.Round(oldRotation.y / 90) * 90), oldRotation.z);
                        }
                    }
                }
                return !shadowIgnore;
            }

            [HarmonyPatch(typeof(ItemFurniture.PlayerItemFurniture2), nameof(OnPutOn), new Type[] { })]
            [HarmonyPrefix]
            public static void OnPutOn(ItemFurniture.PlayerItemFurniture2 __instance)
            {
                if (bFurnitureSnapping.Value)
                {
                    Vector3 oldLocation = __instance.transform.position;
                    Vector3 oldRotation = __instance.transform.eulerAngles;
                    __instance.transform.position = new Vector3(((Mathf.Round(oldLocation.x / 0.25f)) * 0.25f), oldLocation.y, ((Mathf.Round(oldLocation.z / 0.25f)) * 0.25f));
                    __instance.transform.eulerAngles = new Vector3(oldRotation.x, (Mathf.Round(oldRotation.y / 90) * 90), oldRotation.z);
                }

                if (newButton != null)
                {
                    GameObject.Destroy(newButton);
                }
            }
        }
    }
}
