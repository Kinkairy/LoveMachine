﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using LitJson;
using UnityEngine;
using WebSocket4Net;

namespace LoveMachine.Core
{
    public class ButtplugWsClient : MonoBehaviour
    {
        private WebSocket websocket;
        private readonly System.Random random = new System.Random();
        public List<Device> Devices { get; private set; }

        public bool IsConnected { get; private set; }

        private void Start() => Open();

        private void OnDestroy()
        {
            StopScan();
            Close();
        }

        public void Open()
        {
            IsConnected = false;
            Devices = new List<Device>();
            string address = ButtplugConfig.WebSocketAddress.Value;
            CoreConfig.Logger.LogInfo($"Connecting to Intiface server at {address}");
            websocket = new WebSocket(address);
            websocket.Opened += OnOpened;
            websocket.MessageReceived += OnMessageReceived;
            websocket.Error += OnError;
            websocket.Open();
            StartCoroutine(KillSwitch.RunLoop());
        }

        public void Close()
        {
            StopAllCoroutines();
            IsConnected = false;
            CoreConfig.Logger.LogInfo("Disconnecting from Intiface server.");
            websocket.Close();
            websocket.Dispose();
            DeviceManager.SaveDeviceSettings(Devices);
        }

        public void LinearCmd(double position, float durationSecs, int girlIndex, Bone bone)
        {
            if (KillSwitch.Pushed)
            {
                return;
            }
            var commands = (
                from device in Devices
                where device.IsStroker
                    && device.Settings.GirlIndex == girlIndex
                    && device.Settings.Bone == bone
                select new
                {
                    LinearCmd = new
                    {
                        Id = random.Next(),
                        DeviceIndex = device.DeviceIndex,
                        Vectors = (
                            from featureIndex in Enumerable.Range(0,
                                device.DeviceMessages.LinearCmd.FeatureCount)
                            select new
                            {
                                Index = featureIndex,
                                Duration = (int)(durationSecs * 1000),
                                Position = position
                            }
                        ).ToList()
                    }
                }
            ).ToList();
            if (commands.Count > 0)
            {
                websocket.Send(JsonMapper.ToJson(commands));
            }
        }

        public void VibrateCmd(double intensity, int girlIndex, Bone bone)
        {
            if (KillSwitch.Pushed && intensity != 0f)
            {
                VibrateCmd(0f, girlIndex, bone);
                return;
            }
            var commands = (
                from device in Devices
                where device.IsVibrator
                    && device.Settings.GirlIndex == girlIndex
                    && device.Settings.Bone == bone
                select new
                {
                    VibrateCmd = new
                    {
                        Id = random.Next(),
                        DeviceIndex = device.DeviceIndex,
                        Speeds = (
                            from featureIndex in Enumerable.Range(0,
                                device.DeviceMessages.VibrateCmd.FeatureCount)
                            select new
                            {
                                Index = featureIndex,
                                Speed = intensity
                            }
                        ).ToList()
                    }
                }
            ).ToList();
            if (commands.Count > 0)
            {
                websocket.Send(JsonMapper.ToJson(commands));
            }
        }

        public void RotateCmd(float speed, bool clockwise, int girlIndex, Bone bone)
        {
            if (KillSwitch.Pushed && speed != 0f)
            {
                RotateCmd(0f, true, girlIndex, bone);
                return;
            }
            var commands = (
                from device in Devices
                where device.IsRotator
                    && device.Settings.GirlIndex == girlIndex
                    && device.Settings.Bone == bone
                select new
                {
                    RotateCmd = new
                    {
                        Id = random.Next(),
                        DeviceIndex = device.DeviceIndex,
                        Rotations = (
                            from featureIndex in Enumerable.Range(0,
                                device.DeviceMessages.RotateCmd.FeatureCount)
                            select new
                            {
                                Index = featureIndex,
                                Speed = speed,
                                Clockwise = clockwise
                            }
                        ).ToList()
                    }
                }
            ).ToList();
            if (commands.Count > 0)
            {
                websocket.Send(JsonMapper.ToJson(commands));
            }
        }

        private void OnOpened(object sender, EventArgs e)
        {
            CoreConfig.Logger.LogInfo("Succesfully connected to Intiface.");
            var handshake = new
            {
                RequestServerInfo = new
                {
                    Id = random.Next(),
                    ClientName = Paths.ProcessName,
                    MessageVersion = 1
                }
            };
            websocket.Send(JsonMapper.ToJson(new object[] { handshake }));
        }

        private void RequestDeviceList()
        {
            var deviceListRequest = new
            {
                RequestDeviceList = new
                {
                    Id = random.Next()
                }
            };
            websocket.Send(JsonMapper.ToJson(new object[] { deviceListRequest }));
        }

        public void StartScan()
        {
            var scanRequest = new
            {
                StartScanning = new
                {
                    Id = random.Next()
                }
            };
            websocket.Send(JsonMapper.ToJson(new object[] { scanRequest }));
        }

        private void StopScan()
        {
            var scanRequest = new
            {
                StopScanning = new
                {
                    Id = random.Next()
                }
            };
            websocket.Send(JsonMapper.ToJson(new object[] { scanRequest }));
        }

        public void Connect()
        {
            Close(); // close previous connection just in case
            Open();
        }

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            foreach (JsonData data in JsonMapper.ToObject(e.Message))
            {
                if (data.ContainsKey("Error"))
                {
                    CoreConfig.Logger.LogWarning($"Error from Intiface: {data.ToJson()}");
                }
                else if (data.ContainsKey("ServerInfo") || data.ContainsKey("DeviceAdded")
                    || data.ContainsKey("DeviceRemoved"))
                {
                    RequestDeviceList();
                }
                else if (data.ContainsKey("DeviceList"))
                {
                    Devices = JsonMapper.ToObject<DeviceListMessage>(data.ToJson())
                        .DeviceList.Devices;
                    DeviceManager.LoadDeviceSettings(Devices);
                    LogDevices();
                }

                if (data.ContainsKey("ServerInfo"))
                {
                    IsConnected = true;
                    CoreConfig.Logger.LogInfo("Handshake successful.");
                    StartScan();
                }
            }
        }

        private void OnError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            CoreConfig.Logger.LogWarning($"Websocket error: {e.Exception.Message}");
            if (e.Exception.Message.Contains("unreachable"))
            {
                CoreConfig.Logger.LogMessage("Error: Failed to connect to Intiface server.");
            }
        }

        private void LogDevices()
        {
            CoreConfig.Logger.LogInfo($"List of devices: {JsonMapper.ToJson(Devices)}");
            if (Devices.Count == 0)
            {
                CoreConfig.Logger.LogMessage("Warning: No devices connected to Intiface.");
            }
            else
            {
                CoreConfig.Logger.LogMessage($"{Devices.Count} device(s) connected to Intiface.");
            }
            foreach (var device in Devices)
            {
                if (!device.IsStroker && !device.IsVibrator && !device.IsRotator)
                {
                    CoreConfig.Logger.LogMessage(
                        $"Warning: device \"{device.DeviceName}\" not supported.");
                }
            }
        }

        private static class KillSwitch
        {
            public static bool Pushed { get; private set; }

            public static IEnumerator RunLoop()
            {
                while (true)
                {
                    Pushed &= !KillSwitchConfig.ResumeSwitch.Value.IsPressed();
                    if (KillSwitchConfig.KillSwitch.Value.IsDown())
                    {
                        CoreConfig.Logger.LogMessage("LoveMachine: Emergency stop pressed.");
                        Pushed = true;
                    }
                    yield return null;
                }
            }
        }
    }
}
