﻿using LitJson;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WebSocket4Net;

namespace LoveMachine.Core
{
    public class ButtplugWsClient : CoroutineHandler
    {
        private WebSocket websocket;
        private bool killSwitchThrown = false;
        private ConcurrentQueue<IEnumerator> incoming;

        internal event EventHandler<DeviceListEventArgs> OnDeviceListUpdated;

        public List<Device> Devices { get; private set; }

        public bool IsConnected { get; private set; }

        private void Start() => Open();

        private void OnDestroy()
        {
            StopScan();
            StopAllDevices();
            Close();
        }

        public void Open()
        {
            IsConnected = false;
            Devices = new List<Device>();
            incoming = new ConcurrentQueue<IEnumerator>();
            string address = ButtplugConfig.WebSocketHost.Value
                + ":" + ButtplugConfig.WebSocketPort.Value;
            CoreConfig.Logger.LogInfo($"Connecting to Intiface server at {address}");
            websocket = new WebSocket(address);
            // StartCoroutine is only safe to call inside Unity's main thread
            websocket.Opened += (s, e) => incoming.Enqueue(OnOpened(s, e));
            websocket.MessageReceived += (s, e) => incoming.Enqueue(OnMessageReceived(s, e));
            websocket.Error += (s, e) => incoming.Enqueue(OnError(s, e));
            websocket.Open();
            HandleCoroutine(RunReceiveLoop());
            HandleCoroutine(RunKillSwitchLoop());
            HandleCoroutine(RunBatteryLoop());
        }

        public void Close()
        {
            StopAllCoroutines();
            IsConnected = false;
            CoreConfig.Logger.LogInfo("Disconnecting from Intiface server.");
            websocket.Close();
            websocket.Dispose();
        }

        public void LinearCmd(Device device, float position, float durationSecs) =>
            SendKillable(Buttplug.LinearCmd(device, position, durationSecs));

        public void VibrateCmd(Device device, float intensity) =>
            SendKillable(Buttplug.ScalarCmd(device, intensity, Device.Features.Feature.Vibrate));

        public void ConstrictCmd(Device device, float pressure) =>
            SendKillable(Buttplug.ScalarCmd(device, pressure, Device.Features.Feature.Constrict));

        public void RotateCmd(Device device, float speed, bool clockwise) =>
            SendKillable(Buttplug.RotateCmd(device, speed, clockwise));

        public void BatteryLevelCmd(Device device) => Send(Buttplug.BatteryLevelCmd(device));

        public void StopDeviceCmd(Device device) => Send(Buttplug.StopDeviceCmd(device));

        public void StopAllDevices() => Send(Buttplug.StopAllDevices());

        private void RequestServerInfo() => Send(Buttplug.RequestServerInfo());

        private void RequestDeviceList() => Send(Buttplug.RequestDeviceList());

        public void StartScan() => Send(Buttplug.StartScan());

        private void StopScan() => Send(Buttplug.StopScan());

        public void Connect()
        {
            Close(); // close previous connection just in case
            Open();
        }

        private void Send(object command) => websocket.Send(JsonMapper.ToJson(new[] { command }));

        private void SendKillable(object command)
        {
            if (!killSwitchThrown)
            {
                Send(command);
            }
        }

        private IEnumerator OnOpened(object sender, EventArgs e)
        {
            yield return new WaitForEndOfFrame();
            CoreConfig.Logger.LogInfo("Succesfully connected to Intiface.");
            RequestServerInfo();
        }

        private IEnumerator OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            yield return new WaitForEndOfFrame();
            foreach (JsonData data in JsonMapper.ToObject(e.Message))
            {
                bool _ = CheckErrorMsg(data)
                    || CheckServerInfoMsg(data)
                    || CheckDeviceAddedRemovedMsg(data)
                    || CheckDeviceListMsg(data)
                    || CheckBatteryLevelReadingMsg(data);
            }
        }

        private IEnumerator OnError(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            yield return new WaitForEndOfFrame();
            CoreConfig.Logger.LogWarning($"Websocket error: {e.Exception.Message}");
            if (e.Exception.Message.Contains("unreachable"))
            {
                CoreConfig.Logger.LogMessage("Error: Failed to connect to Intiface server.");
            }
        }

        private bool CheckErrorMsg(JsonData data)
        {
            bool handled = data.ContainsKey("Error");
            if (handled)
            {
                CoreConfig.Logger.LogWarning($"Error from Intiface: {data.ToJson()}");
            }
            return handled;
        }

        private bool CheckServerInfoMsg(JsonData data)
        {
            bool handled = data.ContainsKey("ServerInfo");
            if (handled)
            {
                IsConnected = true;
                CoreConfig.Logger.LogInfo("Handshake successful.");
                StartScan();
                RequestDeviceList();
            }
            return handled;
        }

        private bool CheckDeviceAddedRemovedMsg(JsonData data)
        {
            bool handled = data.ContainsKey("DeviceAdded") || data.ContainsKey("DeviceRemoved");
            if (handled)
            {
                RequestDeviceList();
            }
            return handled;
        }

        private bool CheckDeviceListMsg(JsonData data)
        {
            bool handled = data.ContainsKey("DeviceList");
            if (handled)
            {
                var previousDevices = Devices;
                Devices = JsonMapper.ToObject<DeviceListMessage>(data.ToJson())
                    .DeviceList.Devices;
                var args = new DeviceListEventArgs(before: previousDevices, after: Devices);
                OnDeviceListUpdated.Invoke(this, args);
                LogDevices();
                ReadBatteryLevels();
            }
            return handled;
        }

        private bool CheckBatteryLevelReadingMsg(JsonData data)
        {
            var reading = JsonMapper.ToObject<SensorReadingMessage>(data.ToJson());
            bool handled = reading.SensorReading?.SensorType == Device.Features.Feature.Battery;
            if (handled)
            {
                float level = reading.SensorReading.Data[0] / 100f;
                int index = reading.SensorReading.DeviceIndex;
                Devices.Where(device => device.DeviceIndex == index).ToList()
                    .ForEach(device => device.BatteryLevel = level);
            }
            return handled;
        }

        private void LogDevices()
        {
            CoreConfig.Logger.LogInfo($"List of devices: {JsonMapper.ToJson(Devices)}");
            if (Devices.Count == 0)
            {
                CoreConfig.Logger.LogMessage("Warning: No devices connected to Intiface.");
                return;
            }
            CoreConfig.Logger.LogMessage($"{Devices.Count} device(s) connected to Intiface.");
            Devices
                .Where(device => !device.IsSupported)
                .Select(device => $"Warning: device \"{device.DeviceName}\" not supported.")
                .ToList().ForEach(CoreConfig.Logger.LogMessage);
        }

        private void ReadBatteryLevels() =>
            Devices.Where(device => device.HasBatteryLevel).ToList().ForEach(BatteryLevelCmd);

        private IEnumerator RunReceiveLoop()
        {
            while (true)
            {
                while (incoming.TryDequeue(out var coroutine))
                {
                    HandleCoroutine(coroutine);
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        private IEnumerator RunKillSwitchLoop()
        {
            while (true)
            {
                yield return null;
                killSwitchThrown &= !KillSwitchConfig.ResumeSwitch.Value.IsPressed();
                if (KillSwitchConfig.KillSwitch.Value.IsDown())
                {
                    CoreConfig.Logger.LogMessage("LoveMachine: Emergency stop pressed.");
                    StopAllDevices();
                    killSwitchThrown = true;
                }
            }
        }

        private IEnumerator RunBatteryLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(60f);
                ReadBatteryLevels();
                Devices
                    .Where(device => device.BatteryLevel > 0f && device.BatteryLevel < 0.2f)
                    .Select(device => $"{device.DeviceName}: battery low.")
                    .ToList().ForEach(CoreConfig.Logger.LogMessage);
            }
        }
    }
}