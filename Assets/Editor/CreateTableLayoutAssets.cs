using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.EditorTools
{
    /// <summary>
    /// Re-runnable batchmode utility: creates/updates the TableLayoutConfig
    /// assets - the three shipped ones plus the validation configs used to
    /// prove the layout engine generalises beyond the 4-or-8 case.
    /// Dev tool; delete before any upstream PR.
    /// -executeMethod MRTTT.EditorTools.CreateTableLayoutAssets.Create
    /// </summary>
    public static class CreateTableLayoutAssets
    {
        const string k_Dir = "Assets/MRTabletopAssets/Settings";

        static readonly float[] k_FourSeatYaws = { 0f, 180f, 270f, 90f };

        // Seat order for the 8-seat table: the stock four cardinals first so
        // players 1-4 keep their seats through expansion, then the corners.
        static readonly float[] k_EightSeatYaws = { 0f, 180f, 270f, 90f, 45f, 135f, 225f, 315f };

        struct LayoutSpec
        {
            public string name;
            public int[] counts;
            public CollapsePolicy policy;
            public SeatLayoutPreset[] presets;
            public string summary;
        }

        public static void Create()
        {
            var specs = new[]
            {
                // ---- Shipped ----
                new LayoutSpec
                {
                    name = "TableLayout_Default",
                    counts = new[] { 4 },
                    policy = CollapsePolicy.Never,
                    presets = new[] { Preset(4, k_FourSeatYaws, 0f) },
                    summary = "Stock template parity: four seats, never grows.",
                },
                new LayoutSpec
                {
                    name = "TableLayout_4or8",
                    counts = new[] { 4, 8 },
                    policy = CollapsePolicy.Never,
                    // 8-seat scale pinned to 1.5 (user decision 2026-08-27);
                    // the constant-spacing formula would give ~1.85.
                    presets = new[] { Preset(4, k_FourSeatYaws, 0f), Preset(8, k_EightSeatYaws, 1.5f) },
                    summary = "Pegs-and-Jokers shape: 4 seats, expands to 8, never collapses.",
                },
                new LayoutSpec
                {
                    name = "TableLayout_Dynamic",
                    counts = new[] { 3, 4, 5, 6, 7, 8 },
                    policy = CollapsePolicy.WhenRemainingFit,
                    // Fully derived: regular polygons and formula scale at
                    // every count. Exercises the no-preset path end to end.
                    presets = Array.Empty<SeatLayoutPreset>(),
                    summary = "Every count 3-8 as a regular polygon, derived scale.",
                },

                // ---- Validation configs (see Documentation/8Player/Testing.md) ----
                new LayoutSpec
                {
                    name = "TableLayout_6Max",
                    counts = new[] { 4, 6 },
                    policy = CollapsePolicy.WhenRemainingFit,
                    presets = new[] { Preset(4, k_FourSeatYaws, 0f) },
                    summary = "Partial growth to a hexagon with derived scale; collapses when the remaining players fit.",
                },
                new LayoutSpec
                {
                    name = "TableLayout_Immediate",
                    counts = new[] { 4, 6, 8 },
                    policy = CollapsePolicy.Immediate,
                    presets = new[] { Preset(4, k_FourSeatYaws, 0f), Preset(8, k_EightSeatYaws, 1.5f) },
                    summary = "Multi-step growth 4-6-8 and shrink-on-every-leave (relocates seated players).",
                },
                new LayoutSpec
                {
                    name = "TableLayout_Odd",
                    counts = new[] { 3, 5, 7 },
                    policy = CollapsePolicy.Never,
                    presets = Array.Empty<SeatLayoutPreset>(),
                    summary = "Odd polygons with a minimum seat count of 3 - proves nothing hard-codes 4.",
                },
            };

            foreach (var spec in specs)
                CreateAsset(spec);

            AssetDatabase.SaveAssets();
            Debug.Log($"CreateTableLayoutAssets: done ({specs.Length} configs)");
        }

        static SeatLayoutPreset Preset(int seatCount, float[] yaws, float scale)
        {
            return new SeatLayoutPreset { seatCount = seatCount, yaws = yaws, scale = scale };
        }

        static void CreateAsset(LayoutSpec spec)
        {
            string path = $"{k_Dir}/{spec.name}.asset";
            var config = AssetDatabase.LoadAssetAtPath<TableLayoutConfig>(path);
            bool isNew = config == null;
            if (isNew)
                config = ScriptableObject.CreateInstance<TableLayoutConfig>();

            var serialized = new SerializedObject(config);

            var counts = serialized.FindProperty("m_SupportedSeatCounts");
            counts.arraySize = spec.counts.Length;
            for (int i = 0; i < spec.counts.Length; i++)
                counts.GetArrayElementAtIndex(i).intValue = spec.counts[i];

            serialized.FindProperty("m_CollapsePolicy").enumValueIndex = (int)spec.policy;
            serialized.FindProperty("m_BaseRadius").floatValue = 0.75f;

            var presets = serialized.FindProperty("m_Presets");
            presets.arraySize = spec.presets.Length;
            for (int i = 0; i < spec.presets.Length; i++)
                WritePreset(presets.GetArrayElementAtIndex(i), spec.presets[i]);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
                AssetDatabase.CreateAsset(config, path);
            EditorUtility.SetDirty(config);
            Debug.Log($"CreateTableLayoutAssets: {(isNew ? "created" : "updated")} {path} - {spec.summary}");
        }

        static void WritePreset(SerializedProperty property, SeatLayoutPreset preset)
        {
            property.FindPropertyRelative("seatCount").intValue = preset.seatCount;
            property.FindPropertyRelative("scale").floatValue = preset.scale;
            var yawsProperty = property.FindPropertyRelative("yaws");
            yawsProperty.arraySize = preset.yaws.Length;
            for (int i = 0; i < preset.yaws.Length; i++)
                yawsProperty.GetArrayElementAtIndex(i).floatValue = preset.yaws[i];
        }
    }
}
