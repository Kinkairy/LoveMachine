﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LitJson;
using UnityEngine;

namespace LoveMachine.Core
{
    public sealed class AnimationAnalyzer : CoroutineHandler
    {
        // pose -> result
        private readonly Dictionary<string, WaveInfo> resultCache =
            new Dictionary<string, WaveInfo>();

        private GameDescriptor game;

        private void Start()
        {
            game = gameObject.GetComponent<GameDescriptor>();
            game.OnHStarted += (s, a) => StartAnalyze();
            game.OnHEnded += (s, a) => StopAnalyze();
        }

        private string GetExactPose(int girlIndex, Bone bone) =>
            $"{game.GetPose(girlIndex)}.girl{girlIndex}.{bone}";

        public bool TryGetWaveInfo(int girlIndex, Bone bone, out WaveInfo result)
        {
            try
            {
                return resultCache.TryGetValue(GetExactPose(girlIndex, bone), out result);
            }
            catch (Exception e)
            {
                CoreConfig.Logger.LogError($"Error while trying to get wave info: {e}");
                result = new WaveInfo();
                return false;
            }
        }

        private void StartAnalyze()
        {
            StopAllCoroutines();
            HandleCoroutine(DoAnalyze());
        }

        private void StopAnalyze()
        {
            StopAllCoroutines();
            ClearCache();
        }

        private IEnumerator DoAnalyze()
        {
            yield return HandleCoroutine(game.UntilReady());
            Enumerable.Range(0, game.HeroineCount).ToList()
                .ForEach(girlIndex => HandleCoroutine(RunAnalysisLoop(girlIndex)));
        }

        private IEnumerator RunAnalysisLoop(int girlIndex)
        {
            while (true)
            {
                if (TryGetWaveInfo(girlIndex, Bone.Auto, out var _))
                {
                    yield return new WaitForSecondsRealtime(0.1f);
                    continue;
                }
                CoreConfig.Logger.LogDebug("New animation playing, starting to analyze.");
                yield return HandleCoroutine(AnalyzeAnimation(girlIndex), suppressExceptions: true);
            }
        }

        private IEnumerator AnalyzeAnimation(int girlIndex)
        {
            var boneM = game.GetMaleBone();
            var femaleBones = game.GetFemaleBones(girlIndex);
            string pose = GetExactPose(girlIndex, Bone.Auto);
            var samples = new List<Sample>();
            yield return HandleCoroutine(game.WaitAfterPoseChange());
            game.GetAnimState(girlIndex, out float startTime, out _, out _);
            float currentTime = startTime;
            while (currentTime - 1 < startTime)
            {
                yield return new WaitForEndOfFrame();
                game.GetAnimState(girlIndex, out currentTime, out _, out _);
                foreach (var entry in femaleBones)
                {
                    var boneF = entry.Value;
                    float distance = (boneM.position - boneF.position).magnitude;
                    samples.Add(new Sample
                    {
                        Bone = entry.Key,
                        Time = currentTime,
                        Distance = distance
                    });
                }
            }
            if (pose != GetExactPose(girlIndex, Bone.Auto))
            {
                CoreConfig.Logger.LogWarning($"Pose {pose} interrupted; canceling calibration.");
                yield break;
            }
            var results = new Dictionary<Bone, WaveInfo>();
            foreach (var bone in femaleBones.Keys)
            {
                var bonePlot = samples.Where(entry => entry.Bone == bone);
                results[bone] = new WaveInfo
                {
                    Phase = bonePlot
                        .OrderBy(entry => entry.Distance)
                        .FirstOrDefault()
                        .Time % 1,
                    Frequency = GetFrequency(bonePlot
                        .OrderBy(entry => entry.Time)
                        .Select(entry => entry.Distance)),
                    Crest = bonePlot
                        .Select(entry => entry.Distance)
                        .Max(),
                    Trough = bonePlot
                        .Select(entry => entry.Distance)
                        .Min()
                };
            }
            // Prefer bones that are close and move a lot. Being close is more important.
            var autoBone = results
                .OrderBy(entry => Mathf.Pow(entry.Value.Trough, 3)
                    / (entry.Value.Crest - entry.Value.Trough))
                .FirstOrDefault()
                .Key;
            results[Bone.Auto] = results[autoBone];
            results.ToList()
                .ForEach(kvp => resultCache[GetExactPose(girlIndex, kvp.Key)] = kvp.Value);
            CoreConfig.Logger.LogInfo($"Calibration for pose {pose} completed. " +
                $"{samples.Count / femaleBones.Count} frames inspected. " +
                $"Leading bone: {autoBone}, result: {JsonMapper.ToJson(results[Bone.Auto])}.");
        }

        private static int GetFrequency(IEnumerable<float> samples)
        {
            // catch flatlines
            if (samples.Max() - samples.Min() <= 0.000001f)
            {
                return 1;
            }
            // get frequency using Fourier series
            var dfsMagnitudes = new List<float>();
            // probably no game has more than 10 strokes in a loop
            for (int k = 1; k < 10; k++)
            {
                float freq = 2f * Mathf.PI / samples.Count() * k;
                float re = samples.Select((amp, index) => amp * Mathf.Cos(freq * index)).Sum();
                float im = samples.Select((amp, index) => amp * Mathf.Sin(freq * index)).Sum();
                dfsMagnitudes.Add(re * re + im * im);
            }
            return dfsMagnitudes.IndexOf(dfsMagnitudes.Max()) + 1;
        }

        private void ClearCache() => resultCache.Clear();

        private struct Sample
        {
            public Bone Bone { get; set; }
            public float Time { get; set; }
            public float Distance { get; set; }
        }

        public struct WaveInfo
        {
            public float Phase { get; set; }
            public int Frequency { get; set; }
            public float Crest { get; set; }
            public float Trough { get; set; }
        }
    }
}
