﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace LoveMachine.Core
{
    public static class DeviceListConfig
    {
        private static readonly GUIStyle deviceControlsStyle = new GUIStyle()
        {
            normal = new GUIStyleState
            {
                background = GetDeviceControlsTexture()
            }
        };
        private static readonly string[] ordinals =
            { "First", "Second", "Third", "Fourth", "Fifth", "Sixth" };
        private static List<Device> cachedDeviceList;

        public static ConfigEntry<bool> SaveDeviceSettings { get; private set; }
        public static ConfigEntry<string> DeviceSettingsJson { get; private set; }

        internal static void Initialize(BaseUnityPlugin plugin)
        {
            string deviceListTitle = "Device List";
            DeviceSettingsJson = plugin.Config.Bind(
                section: deviceListTitle,
                    key: "Connected",
                    defaultValue: "[]",
                    new ConfigDescription(
                        "",
                        tags: new ConfigurationManagerAttributes
                        {
                            CustomDrawer = DeviceListDrawer,
                            HideSettingName = true,
                            HideDefaultButton = true
                        }));
            SaveDeviceSettings = plugin.Config.Bind(
                section: deviceListTitle,
                key: "Save device assignments",
                defaultValue: false);
        }

        private static void DeviceListDrawer(ConfigEntryBase entry)
        {
            var serverController = Chainloader.ManagerObject.GetComponent<ButtplugWsClient>();
            var game = Chainloader.ManagerObject.GetComponent<GameDescriptor>();
            List<Bone> bones = new Bone[] { Bone.Auto }
                .Concat(game.FemaleBoneNames.Keys)
                .OrderBy(bone => bone)
                .ToList();
            string[] boneNames = bones
                .Select(bone => Enum.GetName(typeof(Bone), bone))
                .Select(name => Regex.Replace(name, ".([A-Z])", " $1"))
                .ToArray();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Connect", GUILayout.Width(150)))
                    {
                        serverController.Connect();
                    }
                    if (serverController.IsConnected)
                    {
                        if (GUILayout.Button("Scan", GUILayout.Width(150)))
                        {
                            serverController.StartScan();
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(12);
                // imgui doesn't expect the layout to change outside of layout events
                if (Event.current.type == EventType.Layout)
                {
                    cachedDeviceList = serverController.Devices;
                }
                foreach (var device in cachedDeviceList)
                {
                    GUILayout.BeginVertical(deviceControlsStyle);
                    {
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.Label(device.DeviceName);
                            GUILayout.FlexibleSpace();
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5);
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Features");
                            GUILayout.Toggle(device.IsStroker, "Stroker");
                            GUILayout.Toggle(device.IsVibrator, "Vibrator");
                            GUILayout.Toggle(device.IsRotator, "Rotator");
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5);
                        GUILayout.BeginHorizontal();
                        {
                            if (game.MaxHeroineCount > 1)
                            {
                                GUILayout.Label("Group Role");
                                string[] girlMappingOptions = Enumerable.Range(0, game.MaxHeroineCount)
                                    .Select(index => $"{ordinals[index]} Girl")
                                    .Concat(new string[] { "Off" })
                                    .ToArray();
                                device.Settings.GirlIndex = GUILayout.SelectionGrid(
                                    selected: device.Settings.GirlIndex,
                                    girlMappingOptions,
                                    xCount: 5);
                            }
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Space(5);
                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Label("Body Part");
                            device.Settings.Bone = bones[GUILayout.SelectionGrid(
                                selected: bones.IndexOf(device.Settings.Bone),
                                boneNames,
                                xCount: 5)];
                        }
                        GUILayout.EndHorizontal();
                        if (GUILayout.Button("Test"))
                        {
                            TestDevice(device);
                        }
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(20);
                }
            }
            GUILayout.EndVertical();
        }

        private static void TestDevice(Device device)
        {
            ButtplugController.Test<StrokerController>(device);
            ButtplugController.Test<VibratorController>(device);
            ButtplugController.Test<RotatorController>(device);
        }

        private static Texture2D GetDeviceControlsTexture()
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixels(new Color[] { new Color(0f, 0f, 0f, 0.4f) });
            texture.Apply();
            return texture;
        }
    }
}
