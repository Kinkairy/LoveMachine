﻿using System.Collections;
using UnityEngine;

namespace LoveMachine.Core
{
    public abstract class ClassicButtplugController : ButtplugController
    {
        protected abstract IEnumerator HandleAnimation(Device device);

        protected abstract IEnumerator HandleOrgasm(Device device);

        protected override IEnumerator Run(Device device)
        {
            while (true)
            {
                if (game.IsOrgasming(device.Settings.GirlIndex))
                {
                    yield return HandleCoroutine(HandleOrgasm(device));
                    continue;
                }
                if (game.IsIdle(device.Settings.GirlIndex))
                {
                    client.StopDeviceCmd(device);
                    while (game.IsIdle(device.Settings.GirlIndex))
                    {
                        yield return new WaitForSecondsRealtime(0.1f);
                    }
                    continue;
                }
                yield return HandleCoroutine(HandleAnimation(device));
            }
        }
    }
}