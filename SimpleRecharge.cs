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
    private static List<ItemBattery> _chargeableItems = [];
    private readonly HashSet<string> _chargeableItemNames = new();
    private static class Recharge
    {
        // Define constants for recharge amounts
        // Should it be a sctucture instead?
        public const int SMALL = 150;
        public const int LARGE = 750;
        public const float INTERVAL = 30f;
    }
    private float _timer = 0f;
    private ConfigEntry<int> _configRechargeAmountSmall;
    private ConfigEntry<int> _configRechargeAmountLarge;
    private ConfigEntry<float> _configIntervalSeconds;
    private ConfigEntry<string> _configChargeableItemNames;
    private ConfigEntry<bool> _configIsWhitelist;
    internal static SimpleRecharge Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

    private void AddCharge(int amount)
    {
        // This method adds a charge to all items in the _chargeableItems list
        foreach (var item in _chargeableItems)
        {
            item.ChargeBattery(base.gameObject, amount);
        }
    }
    private void UpdateRechargeTimer()
    {
        _timer += Time.deltaTime;
        // Every interval, add a small charge to all items in the _chargeableItems list
        if (_timer >= _configIntervalSeconds.Value)
        {
            _timer -= _configIntervalSeconds.Value; // Reset the timer without losing hanging decimals
            if (_chargeableItems.Count != 0)
            {
                AddCharge(_configRechargeAmountSmall.Value);
            }
        }
    }
    private void HandleDebugInput()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Logger.LogInfo("F1 key pressed, listing all items with charge in the scene.");
            var items = GameObject.FindObjectsOfType<ItemBattery>();
            foreach (var item in items)
            {
                Logger.LogInfo($"Found GameObject: {item.name} at position {item.transform.position}");
                Logger.LogInfo($"  ----> {item} has battery life: {item.batteryLife}");
            }
            Logger.LogInfo("Time since level load: " + Time.timeSinceLevelLoad);
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Logger.LogInfo("F2 key pressed, providing list of valid chargeable items.");
            foreach (var item in _chargeableItems)
            {
                Logger.LogInfo($"Chargeable item: {item.name}");
            }
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
        _configRechargeAmountLarge = Config.Bind("General",
                                                "Recharge Amount Large",
                                                Recharge.LARGE,
                                                "Recharge amount upon extracting. To turn off put 0"
                                                );
        _configRechargeAmountSmall = Config.Bind("General",
                                                "Recharge Amount Small",
                                                Recharge.SMALL,
                                                "Recharge amount over time. To turn off put 0"
                                                );
        _configIntervalSeconds = Config.Bind("General",
                                            "Small recharge interval",
                                            Recharge.INTERVAL,
                                            new ConfigDescription(
                                                "Interval between small recharges in seconds.",
                                                new AcceptableValueRange<float>(0.1f, 3600f))
                                            );
        _configChargeableItemNames = Config.Bind("Advanced",
                                                "ChargableItemNames",
                                                "Drone Torque, Rubber Duck, Phase Bridge, Orb Zero Gravity, Melee Inflatable Hammer, Melee Frying Pan, Gun Shockwave",
                                                "Whitelist of items that can be recharged. Use commas to separate names. Press F1 to print all chargable items in the scene to the console. F2 prints out only valid ones."
                                                );
        _configIsWhitelist = Config.Bind("Advanced",
                                        "Whitelist",
                                        true,
                                        "If true, only items in the ChargableItemNames list will be recharged. False, every other item will be recharged."
                                        );

        foreach (var itemName in _configChargeableItemNames.Value.Split(','))
        {
            // Items in the scene have the following name
            _chargeableItemNames.Add("Item " + itemName.Trim() + "(Clone)");
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
        UpdateRechargeTimer();
        HandleDebugInput();

    }
    [HarmonyPatch(typeof(ExtractionPoint))]
    [HarmonyPatch("DestroyAllPhysObjectsInHaulList")]
    class Patch_extraction_ChargeItems
    {
        // This method is called when an extraction point is activated
        // It will charge all items in the scene when an extraction is completed
        static void Postfix(ExtractionPoint __instance)
        {
            if (_chargeableItems.Count != 0)
            {
                Instance.AddCharge(Instance._configRechargeAmountLarge.Value);
            }
        }
    }
    [HarmonyPatch(typeof(ExtractionPoint))]
    [HarmonyPatch("ActivateTheFirstExtractionPointAutomaticallyWhenAPlayerLeaveTruck")]
    class Patch_extraction_FindItems
    {
        static void Postfix(ExtractionPoint __instance)
        // This method is called when an extraction point is activated for the first time
        // It will initialize the _chargeableItems list
        {
            _chargeableItems.Clear();
            foreach (var item in GameObject.FindObjectsOfType<ItemBattery>())
            {
                // Is there the item in the list?
                bool matches = Instance._chargeableItemNames.Contains(item.name);
                // Instance._configIsWhitelist.Value == Should the items in the list be recharged?
                if (Instance._configIsWhitelist.Value == matches)
                {
                    _chargeableItems.Add(item);
                }
            }
            if (_chargeableItems.Count == 0)
            {
                Logger.LogInfo("No chargable items found in the scene.");
            }
            else
            {
                Logger.LogInfo($"{_chargeableItems.Count} valid chargable items found in the scene.");
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
            // It will clear the _chargeableItems list to prevent accessing non-existent items
            _chargeableItems.Clear();
            Logger.LogInfo("Chargable items list cleared on level change.");
        }
    }
}