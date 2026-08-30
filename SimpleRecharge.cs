using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System;

namespace SimpleRecharge;

//TODO:
//  Multiplayer test
//  Vehicle test
[BepInPlugin("FroggitTRH.SimpleRecharge", "SimpleRecharge", "1.0")]
public class SimpleRecharge : BaseUnityPlugin
{
    public static List<ItemBattery> ChargableItems = [];
    private List<string> ChargableItemNames = [];
    private static class Recharge
    {
        // Define constants for recharge amounts
        // Should it be a sctucture instead?
        public const int SMALL = 150;
        public const int LARGE = 750;
        public const float INTERVAL = 30f;
    }
    private float timer = 0f;
    private ConfigEntry<int> configRechargeAmountSmall;
    private ConfigEntry<int> configRechargeAmountLarge;
    private ConfigEntry<float> configIntervalSeconds;
    private ConfigEntry<string> configChargableItemNames;
    private ConfigEntry<bool> configIsWhitelist;
    internal static SimpleRecharge Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void Add_Charge(int amount)
    {
        // This method adds a charge to all items in the ChargableItems list
        foreach (var item in ChargableItems)
        {
            item.ChargeBattery(base.gameObject, amount);
        }
    }
    private void Awake()
    {
        Instance = this;

        // Prevent the plugin from being deleted
        this.gameObject.transform.parent = null;
        this.gameObject.hideFlags = HideFlags.HideAndDontSave;

        // Config file setup
        // Bind (Category, name, default value, description)
        configRechargeAmountLarge = Config.Bind("General",
                                                "Recharge Amount Large",
                                                Recharge.LARGE,
                                                "Recharge amount upon extracting. To turn off put 0");

        configRechargeAmountSmall = Config.Bind("General",
                                                "Recharge Amount Small",
                                                Recharge.SMALL,
                                                "Recharge amount over time. To turn off put 0");
        configIntervalSeconds = Config.Bind("General",
                                            "Small recharge interval",
                                            Recharge.INTERVAL,
                                            "Interval between small recharges in SECONDS");
        configChargableItemNames = Config.Bind("Advanced",
                                                "ChargableItemNames",
                                                "Drone Torque, Rubber Duck, Phase Bridge, Orb Zero Gravity, Melee Inflatable Hammer, Melee Frying Pan, Gun Shockwave",
                                                "Whitelist of items that can be recharged. Use commas to separate names. Press F1 to print all chargable items in the scene to the console. F2 prints out only valid ones.");
        configIsWhitelist = Config.Bind("Advanced",
                                        "Whitelist",
                                        true,
                                        "If true, only items in the ChargableItemNames list will be recharged. False, every other item will be recharged.");

        foreach (var itemName in configChargableItemNames.Value.Split(','))
        {
            // Items in the scene have the following name
            ChargableItemNames.Add("Item " + itemName.Trim() + "(Clone)");
        }

        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        // Code that runs every frame goes here
        timer += Time.deltaTime;
        // Every interval, add a small charge to all items in the ChargableItems list
        if (timer >= Instance.configIntervalSeconds.Value)
        {
            timer = 0f; // Reset the timer
            if (ChargableItems.Count != 0)
            {
                Add_Charge(Instance.configRechargeAmountSmall.Value);
            }
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Logger.LogInfo("F1 key pressed, listing all items with charge in the scene.");
            var items = GameObject.FindObjectsOfType<ItemBattery>();
            foreach (var item in items)
            {
                Logger.LogInfo($"Found GameObject: {item.name} at position {item.transform.position}");
                Logger.LogInfo($"Found GameObject: {item} has battery life: {item.batteryLife}");
                Logger.LogInfo("Time since level load: " + Time.timeSinceLevelLoad);
                float levelTime = Time.timeSinceLevelLoad;
                Logger.LogInfo("Time since level started: " + levelTime + " seconds");
            }
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Logger.LogInfo("F2 key pressed, providing list of valid chargable items.");
            foreach (var item in ChargableItemNames)
            {
                Logger.LogInfo($"Chargable item: {item}");
            }
        }
    }
    [HarmonyPatch(typeof(ExtractionPoint))]
    [HarmonyPatch("DestroyAllPhysObjectsInHaulList")]
    class Patch_extraction_ChargeItems
    {
        // This method is called when an extraction point is activated
        // It will charge all items in the scene when an extraction is completed
        static void Postfix(ExtractionPoint __instance)
        {
            if (ChargableItems.Count != 0)
            {
                Instance.Add_Charge(Instance.configRechargeAmountLarge.Value);
            }
        }
    }
    [HarmonyPatch(typeof(ExtractionPoint))]
    [HarmonyPatch("ActivateTheFirstExtractionPointAutomaticallyWhenAPlayerLeaveTruck")]
    class Patch_extraction_FindItems
    {
        static void Postfix(ExtractionPoint __instance)
        // This method is called when an extraction point is activated for the first time
        // It will initialize the ChargableItems list
        {
            List<ItemBattery> templist = [];
            ChargableItems.Clear();
            ChargableItems.AddRange(GameObject.FindObjectsOfType<ItemBattery>());
            foreach (var item in ChargableItems)
            {
                if (Instance.configIsWhitelist.Value)
                {
                    // If the config is set to whitelist, only add items that are in the ChargableItemNames list
                    if (Instance.ChargableItemNames.Contains(item.name))
                    {
                        templist.Add(item);
                    }
                }
                else
                {
                    // If the config is set to blacklist, add all items that are not in the ChargableItemNames list
                    if (!Instance.ChargableItemNames.Contains(item.name))
                    {
                        templist.Add(item);
                    }
                }
            }
            ChargableItems.Clear();
            ChargableItems.AddRange(templist);
            templist.Clear();
            if (ChargableItems.Count == 0)
            {
                Logger.LogInfo("No chargable items found in the scene.");
            }
            else
            {
                Logger.LogInfo($"{ChargableItems.Count} valid chargable items found in the scene.");
            }

        }
    }
    [HarmonyPatch(typeof(RunManager))]
    [HarmonyPatch("ChangeLevel")]
    class Patch_RunManager
    {
        static void Prefix(RunManager __instance)
        {
            // This method is called when the level changes
            // It will clear the ChargableItems list to prevent accessing non-existent items
            ChargableItems.Clear();
            Logger.LogInfo("Chargable items list cleared on level change.");
        }
    }
}