﻿using System.Collections;
using UnityEngine;

namespace LoveMachine.Core
{
    public class StrokerController : ButtplugController
    {
        protected override IEnumerator Run(int girlIndex, Bone bone)
        {
            while (true)
            {
                if (game.IsIdle(girlIndex))
                {
                    yield return new WaitForSeconds(.1f);
                    continue;
                }
                if (game.IsOrgasming(girlIndex))
                {
                    yield return HandleCoroutine(EmulateOrgasm(girlIndex, bone));
                    continue;
                }
                yield return HandleCoroutine(EmulateStroking(girlIndex, bone));
            }
        }

        protected override void StopDevices(int girlIndex, Bone bone) { }

        protected IEnumerator EmulateStroking(int girlIndex, Bone bone)
        {
            if (!analyzer.TryGetWaveInfo(girlIndex, bone, out var waveInfo))
            {
                yield break;
            }
            int updateFrequency = 10;
            float strokeTimeSecs = GetStrokeTimeSecs(girlIndex, bone);
            // min number of subdivisions
            int turns = 2 * waveInfo.Frequency;
            // max number of subdivisions given the update frequency
            int subdivisions = turns * (int)Mathf.Max(1f, strokeTimeSecs * updateFrequency / turns);
            int segments = StrokerConfig.SmoothStroking.Value ? subdivisions : turns;
            CoreConfig.Logger.LogInfo(segments);
            float startNormTime = GetNormalizedTime(girlIndex);
            int getSegment(float time) => (int)((time - waveInfo.Phase) * segments);
            // != because time can also go down when changing animation
            yield return new WaitUntil(() =>
                getSegment(GetNormalizedTime(girlIndex)) != getSegment(startNormTime));
            strokeTimeSecs = GetStrokeTimeSecs(girlIndex, bone);
            float refreshTimeSecs = strokeTimeSecs / segments;
            float refreshNormTime = 1f / segments;
            float currentNormTime = GetNormalizedTime(girlIndex);
            float nextNormTime = currentNormTime + refreshNormTime;
            float currentPosition = Sigmoid(currentNormTime, waveInfo);
            float nextPosition = Sigmoid(nextNormTime, waveInfo);
            bool movingUp = currentPosition < nextPosition;
            GetStrokeZone(strokeTimeSecs, waveInfo, out float bottom, out float top);
            float targetPosition = movingUp ? top : bottom;
            float speed = (nextPosition - currentPosition) / refreshTimeSecs;
            speed *= movingUp ? 1f : 1f + game.StrokingIntensity;
            float timeToTargetSecs = (targetPosition - currentPosition) / speed;
            MoveStroker(targetPosition, timeToTargetSecs, girlIndex, bone);
        }

        protected IEnumerator EmulateOrgasm(int girlIndex, Bone bone)
        {
            float bottom = StrokerConfig.OrgasmDepth.Value;
            float time = 0.5f / StrokerConfig.OrgasmShakingFrequency.Value;
            float top = bottom + StrokerConfig.MaxStrokesPerMinute.Value / 60f / 2 * time;
            while (game.IsOrgasming(girlIndex))
            {
                MoveStroker(top, time, girlIndex, bone);
                yield return new WaitForSecondsRealtime(time);
                MoveStroker(bottom, time, girlIndex, bone);
                yield return new WaitForSecondsRealtime(time);

            }
        }

        private float GetNormalizedTime(int girlIndex)
        {
            game.GetAnimState(girlIndex, out float currentNormTime, out _, out _);
            return currentNormTime;
        }

        private static float Sigmoid(float x, AnimationAnalyzer.WaveInfo waveInfo) =>
            Mathf.InverseLerp(1f, -1f,
                Mathf.Cos(2 * Mathf.PI * waveInfo.Frequency * (x - waveInfo.Phase)));

        private void GetStrokeZone(float strokeTimeSecs, AnimationAnalyzer.WaveInfo waveInfo,
            out float min, out float max)
        {
            float minSlow = Mathf.InverseLerp(0, 100, StrokerConfig.SlowStrokeZoneMin.Value);
            float maxSlow = Mathf.InverseLerp(0, 100, StrokerConfig.SlowStrokeZoneMax.Value);
            float minFast = Mathf.InverseLerp(0, 100, StrokerConfig.FastStrokeZoneMin.Value);
            float maxFast = Mathf.InverseLerp(0, 100, StrokerConfig.FastStrokeZoneMax.Value);
            // decrease stroke length gradually as speed approaches the device limit
            float rate = 60f / StrokerConfig.MaxStrokesPerMinute.Value / strokeTimeSecs;
            float relativeLength = (waveInfo.Crest - waveInfo.Trough) / game.PenisSize;
            float scale = Mathf.Lerp(1f - StrokerConfig.StrokeLengthRealism.Value, 1f,
                relativeLength);
            min = Mathf.Lerp(minSlow, minFast, rate) * scale;
            max = Mathf.Lerp(maxSlow, maxFast, rate) * scale;
        }

        protected virtual float GetStrokeTimeSecs(int girlIndex, Bone bone)
        {
            game.GetAnimState(girlIndex, out _, out float length, out float speed);
            int freq = analyzer.TryGetWaveInfo(girlIndex, bone, out var result)
                ? result.Frequency
                : 1;
            float strokeTimeSecs = length / speed / freq;
            // sometimes the length of an animation becomes Infinity in KK
            // sometimes the speed becomes 0 in HS2
            // this is a catch-all for god knows what other things that can
            // possibly go wrong and cause the stroking coroutine to hang
            if (strokeTimeSecs > 10 || strokeTimeSecs < 0.001f
                || float.IsNaN(strokeTimeSecs))
            {
                return .01f;
            }
            return strokeTimeSecs;
        }

        protected void MoveStroker(float position, float durationSecs, int girlIndex, Bone bone) =>
            client.LinearCmd(position, durationSecs, girlIndex, bone);
    }
}
