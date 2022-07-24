﻿using BepInEx.Configuration;
using UnityEngine;

namespace LoveMachine.Core
{
    internal static class GUIUtil
    {
        public static void DrawRangeSlider(ConfigEntry<int> min, ConfigEntry<int> max)
        {
            float labelWidth = GUI.skin.label.CalcSize(new GUIContent("100%")).x;
            GUILayout.BeginHorizontal();
            {
                float lower = min.Value;
                float upper = max.Value;
                GUILayout.Label(lower + "%", GUILayout.Width(labelWidth));
                RangeSlider.Create(ref lower, ref upper, 0, 100);
                GUILayout.Label(upper + "%", GUILayout.Width(labelWidth));
                if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                {
                    lower = (int)min.DefaultValue;
                    upper = (int)max.DefaultValue;
                }
                min.Value = (int)lower;
                max.Value = (int)upper;
            }
            GUILayout.EndHorizontal();
        }

        public static int IntSlider(string label, string tooltip,
            int value, int defaultValue, int min, int max)
        {
            GUILayout.BeginHorizontal();
            {
                LabelWithTooltip(label, tooltip);
                value = (int)GUILayout.HorizontalSlider(value, min, max);
                value = int.Parse(GUILayout.TextField(value.ToString(), GUILayout.Width(50)));
                if (ResetButton)
                {
                    value = defaultValue;
                }
                value = Mathf.Clamp(value, min, max);
            }
            GUILayout.EndHorizontal();
            SingleSpace();
            return value;
        }

        public static bool Toggle(string label, string tooltip, bool value, bool defaultValue)
        {
            GUILayout.BeginHorizontal();
            {
                LabelWithTooltip(label, tooltip);
                value = GUILayout.Toggle(value, value ? "On" : "Off");
                if (ResetButton)
                {
                    value = defaultValue;
                }
            }
            GUILayout.EndHorizontal();
            SingleSpace();
            return value;
        }

        public static int MultiChoice(string label, string tooltip, string[] choices, int value)
        {
            GUILayout.BeginHorizontal();
            {
                LabelWithTooltip(label, tooltip);
                value = GUILayout.SelectionGrid(
                    selected: value,
                    texts: choices,
                    xCount: 4);
            }
            GUILayout.EndHorizontal();
            SingleSpace();
            return value;
        }

        public static void SingleSpace() => GUILayout.Space(10);

        private static bool ResetButton => GUILayout.Button("Reset", GUILayout.ExpandWidth(false));

        public static void LabelWithTooltip(string label, string tooltip)
        {
            float labelWidth = GUI.skin.label.CalcSize(new GUIContent("Some pretty long text")).x;
            var content = new GUIContent
            {
                text = label,
                tooltip = tooltip
            };
            GUILayout.Label(content, GUILayout.Width(labelWidth));
        }

        private static void PercentLabel(float value)
        {
            float labelWidth = GUI.skin.label.CalcSize(new GUIContent("100%")).x;
            string text = (int)(value * 100) + "%";
            GUILayout.Label(text, GUILayout.Width(labelWidth));
        }
    }
}
