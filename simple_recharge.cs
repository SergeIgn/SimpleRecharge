using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using System;

namespace simple_recharge;


//TODO: Update inventory to show the recharge amounts
//TODO: Add a list of items that can be recharged
//TODO: Delete F1 and F2 keys, they are only for debugging purposes
//TODO: Multiplayer?
[BepInPlugin("FroggittheRandomHopper.simple_recharge", "simple_recharge", "1.0")]
public class simple_recharge : BaseUnityPlugin
{
    public static List<ItemBattery> ChargableItems = [];
    private static class Recharge
    {
        // Define constants for recharge amounts
        public const int SMALL = 25;
        public const int LARGE = 500;
    }
    private ConfigEntry<int> configRechargeAmountSmall;
    private ConfigEntry<int> configRechargeAmountLarge;
    //
    internal static simple_recharge Instance { get; private set; } = null!;
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
        configRechargeAmountLarge = Config.Bind("General",
                                                "RechargeAmountLarge",
                                                Recharge.LARGE,
                                                "Recharge amount upon extracting.");

        configRechargeAmountSmall = Config.Bind("General",
                                                "RechargeAmountSmall",
                                                Recharge.SMALL,
                                                "Recharge amount over time.");
        //
        /*List<string> tempList = new List<string>();
        foreach (var itemName in Recharge.ChargableItemNames)
        {
            tempList.Add("Item " + itemName + "(Clone)");
        }
        ChargableItemNames.Clear();
        ChargableItemNames.AddRange(tempList);
        tempList.Clear();
        */

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
        // Every 30 seconds, add a small charge to all items in the ChargableItems list
        if (MathF.Round(Time.timeSinceLevelLoad % 30) == 0)
        {
            if (ChargableItems.Count != 0)
            {
                Add_Charge(Instance.configRechargeAmountSmall.Value);
            }
        }
        // Code that runs every frame goes here
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
        //
    }
    [HarmonyPatch(typeof(ExtractionPoint))]
    [HarmonyPatch("DestroyAllPhysObjectsInHaulList")]
    class Patch_extraction
    {
        // This method is called when the extraction point is activated
        // It will charge all items in the scene when the extraction is complete
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
    class Patch_extraction2
    {
        // This method is called when the extraction point is activated for the first time
        // It will initialize the ChargableItems list
        static void Postfix(ExtractionPoint __instance)
        {
            ChargableItems.Clear();
            ChargableItems.AddRange(GameObject.FindObjectsOfType<ItemBattery>());
            if (ChargableItems.Count == 0)
            {
                Logger.LogInfo("No chargable items found in the scene.");
            }
            else
            {
                Logger.LogInfo($"{ChargableItems.Count} chargable items found in the scene.");
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