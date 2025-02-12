﻿using BepInEx;
using LoveMachine.Core;

namespace LoveMachine.VRK
{
    [BepInProcess("VR_Kanojo")]
    [BepInPlugin(CoreConfig.GUID, CoreConfig.PluginName, CoreConfig.Version)]
    internal class Plugin : LoveMachinePlugin<VRKanojoGame>
    { }
}