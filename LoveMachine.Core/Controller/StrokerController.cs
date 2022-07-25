﻿using System.Collections;
using UnityEngine;

namespace LoveMachine.Core
{
    public class StrokerController : ButtplugController
    {
        protected override bool IsDeviceSupported(Device device) => device.IsStroker;

        protected override IEnumerator Run(Device device)
        {
            while (true)
            {
                if (game.IsIdle(device.Settings.GirlIndex))
                {
                    yield return new WaitForSeconds(.1f);
                    continue;
                }
                if (game.IsOrgasming(device.Settings.GirlIndex))
                {
                    yield return HandleCoroutine(EmulateOrgasm(device));
                    continue;
                }
                yield return HandleCoroutine(EmulateStroking(device));
            }
        }

        protected IEnumerator EmulateStroking(Device device)
        {
            int girlIndex = device.Settings.GirlIndex;
            var bone = device.Settings.Bone;
            if (!analyzer.TryGetWaveInfo(girlIndex, bone, out var waveInfo))
            {
                yield break;
            }
            int updateFrequency = device.Settings.UpdatesHz;
            float animTimeSecs = GetAnimationTimeSecs(girlIndex);
            // min number of subdivisions
            int turns = 2 * waveInfo.Frequency;
            // max number of subdivisions given the update frequency
            int subdivisions = turns * (int)Mathf.Max(1f, animTimeSecs * updateFrequency / turns);
            int segments = device.Settings.StrokerSettings.SmoothStroking ? subdivisions : turns;
            float startNormTime = GetLatencyCorrectedNormalizedTime(device);
            int getSegment(float time) => (int)((time - waveInfo.Phase) * segments);
            // != because time can also go down when changing animation
            yield return new WaitUntil(() =>
                getSegment(GetLatencyCorrectedNormalizedTime(device)) != getSegment(startNormTime));
            animTimeSecs = GetAnimationTimeSecs(girlIndex);
            float refreshTimeSecs = animTimeSecs / segments;
            float refreshNormTime = 1f / segments;
            float currentNormTime = GetLatencyCorrectedNormalizedTime(device);
            float nextNormTime = currentNormTime + refreshNormTime;
            float currentPosition = Sinusoid(currentNormTime, waveInfo);
            float nextPosition = Sinusoid(nextNormTime, waveInfo);
            bool movingUp = currentPosition < nextPosition;
            GetStrokeZone(animTimeSecs, device, waveInfo, out float bottom, out float top);
            float targetPosition = movingUp ? top : bottom;
            float speed = (nextPosition - currentPosition) / refreshTimeSecs;
            speed *= movingUp ? 1f : 1f + game.StrokingIntensity;
            float timeToTargetSecs = (targetPosition - currentPosition) / speed;
            MoveStroker(device, targetPosition, timeToTargetSecs);
        }

        protected IEnumerator EmulateOrgasm(Device device)
        {
            float bottom = StrokerConfig.OrgasmDepth.Value;
            float time = 0.5f / StrokerConfig.OrgasmShakingFrequency.Value;
            float top = bottom + device.Settings.StrokerSettings.MaxStrokesPerMin / 60f / 2f * time;
            while (game.IsOrgasming(device.Settings.GirlIndex))
            {
                MoveStroker(device, top, time);
                yield return new WaitForSecondsRealtime(time);
                MoveStroker(device, bottom, time);
                yield return new WaitForSecondsRealtime(time);
            }
        }

        private static float Sinusoid(float x, AnimationAnalyzer.WaveInfo waveInfo) =>
            Mathf.InverseLerp(1f, -1f,
                Mathf.Cos(2 * Mathf.PI * waveInfo.Frequency * (x - waveInfo.Phase)));

        private void GetStrokeZone(float strokeTimeSecs, Device device,
            AnimationAnalyzer.WaveInfo waveInfo, out float min, out float max)
        {
            // decrease stroke length gradually as speed approaches the device limit
            float rate = 60f / device.Settings.StrokerSettings.MaxStrokesPerMin / strokeTimeSecs;
            float relativeLength = waveInfo.Amplitude / game.PenisSize;
            float scale = Mathf.Lerp(
                1f - StrokerConfig.StrokeLengthRealism.Value,
                1f,
                t: relativeLength);
            min = scale * Mathf.Lerp(
                device.Settings.StrokerSettings.SlowStrokeZoneMin,
                device.Settings.StrokerSettings.FastStrokeZoneMin,
                t: rate);
            max = scale * Mathf.Lerp(
                device.Settings.StrokerSettings.SlowStrokeZoneMax,
                device.Settings.StrokerSettings.FastStrokeZoneMax,
                t: rate);
        }

        protected void MoveStroker(Device device, float position, float durationSecs) =>
            client.LinearCmd(device, position, durationSecs);
    }
}
