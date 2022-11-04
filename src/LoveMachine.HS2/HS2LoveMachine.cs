﻿using BepInEx;
using LoveMachine.Core;

namespace LoveMachine.HS2
{
    [BepInProcess("HoneySelect2")]
    [BepInProcess("HoneySelect2VR")]
    [BepInPlugin(CoreConfig.GUID, CoreConfig.PluginName, CoreConfig.Version)]
    internal class HS2LoveMachine : BaseUnityPlugin
    {
        private void Start()
        {
            this.Initialize<HoneySelect2Game>(Logger);
            Hooks.InstallHooks();
        }
    }

    // To avoid conflict with the old plugin
    // 2.1.0 was the last ButtPlugin version so this should get priority from BepInEx
    [BepInPlugin("Sauceke.ButtPlugin", "ButtPlugin", "2.1.1")]
    internal class EmptyPlugin : BaseUnityPlugin
    { }
}