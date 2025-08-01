using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;

namespace simple_recharge;

[BepInPlugin("FroggittheRandomHopper.simple_recharge", "simple_recharge", "1.0")]
public class simple_recharge : BaseUnityPlugin
{
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
                item.ChargeBattery(base.gameObject, 25);
            }
        }
    }
}

[HarmonyPatch(typeof(ExtractionPoint))]
[HarmonyPatch("DestroyAllPhysObjectsInHaulList")]
class Patch_extraction
{
    static void Postfix(ExtractionPoint __instance)
    {

        simple_recharge.Logger.LogInfo("Extraction is complete, charging all batteries.");
        var items = GameObject.FindObjectsOfType<ItemBattery>();
        foreach (var item in items)
        {
            simple_recharge.Logger.LogInfo($"Charging GameObject: {item.name} at position {item.transform.position}");
            item.ChargeBattery(simple_recharge.Instance.gameObject, 2000);
        }

    }
}