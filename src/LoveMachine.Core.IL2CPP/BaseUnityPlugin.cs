﻿using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace LoveMachine.Core
{
    public class BaseUnityPlugin : BasePlugin
    {
        public ManualLogSource Logger => Log;

        public override void Load() => Traverse.Create(this).Method("Start").GetValue();
    }
}