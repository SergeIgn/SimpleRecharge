using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System;

namespace simple_recharge;


//TODO: Update inventory to show the recharge amounts
//TODO: Add a list of items that can be recharged
//TODO: Delete F1 and F2 keys, they are only for debugging purposes
[BepInPlugin("FroggittheRandomHopper.simple_recharge", "simple_recharge", "1.0")]
public class simple_recharge : BaseUnityPlugin
{
    private static class Recharge
        {
            // Define constants for recharge amounts
            public const int SMALL = 100;
            public const int LARGE = 1000;
        }
    private ConfigEntry<int> configRechargeAmountSmall;
    private ConfigEntry<int> configRechargeAmountLarge;
    internal static simple_recharge Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }

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
        if (MathF.Round(Time.timeSinceLevelLoad % 10) == 0)
        {
            Logger.LogInfo($"Time since level load: {Time.timeSinceLevelLoad} seconds");
            var items = GameObject.FindObjectsOfType<ItemBattery>();
            foreach (var item in items)
            {
                item.ChargeBattery(base.gameObject, configRechargeAmountSmall.Value);
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
                Logger.LogInfo($"Found GameObject: {item.name} has battery life: {item.batteryLife}");
                Logger.LogInfo("Time since level load: " + Time.timeSinceLevelLoad);
                float levelTime = Time.timeSinceLevelLoad;
                Logger.LogInfo("Time since level started: " + levelTime + " seconds");
            }
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Logger.LogInfo("F2 key pressed, getting charge.");
            var items = GameObject.FindObjectsOfType<ItemBattery>();
            foreach (var item in items)
            {
                Logger.LogInfo($"Found GameObject: {item.name} at position {item.transform.position}");
                item.ChargeBattery(base.gameObject, configRechargeAmountSmall.Value);
            }
        }
    }
    [HarmonyPatch(typeof(ExtractionPoint))]
    [HarmonyPatch("DestroyAllPhysObjectsInHaulList")]
    class Patch_extraction
    {
        static void Postfix(ExtractionPoint __instance)
        {

            Logger.LogInfo("Extraction is complete, charging all batteries.");
            var items = GameObject.FindObjectsOfType<ItemBattery>();
            foreach (var item in items)
            {
                Logger.LogInfo($"Charging GameObject: {item.name} at position {item.transform.position}");
                item.ChargeBattery(Instance.gameObject, Instance.configRechargeAmountLarge.Value);
            }

        }
    }
}