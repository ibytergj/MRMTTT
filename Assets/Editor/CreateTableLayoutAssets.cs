using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.EditorTools
{
    /// <summary>
    /// One-shot batchmode utility: creates the shipped TableLayoutConfig
    /// assets. Deleted after use (PR hygiene) — re-run via
    /// -executeMethod MRTTT.EditorTools.CreateTableLayoutAssets.Create
    /// </summary>
    public static class CreateTableLayoutAssets
    {
        const string k_Dir = "Assets/MRTabletopAssets/Settings";

        static readonly float[] k_FourSeatYaws = { 0f, 180f, 270f, 90f };
        static readonly float[] k_EightSeatYaws = { 0f, 180f, 270f, 90f, 45f, 135f, 225f, 315f };

        public static void Create()
        {
            CreateAsset("TableLayout_Default", new[] { 4 }, CollapsePolicy.Never);
            CreateAsset("TableLayout_4or8", new[] { 4, 8 }, CollapsePolicy.Never);
            CreateAsset("TableLayout_Dynamic", new[] { 3, 4, 5, 6, 7, 8 }, CollapsePolicy.WhenRemainingFit);
            AssetDatabase.SaveAssets();
            Debug.Log("CreateTableLayoutAssets: done");
        }

        static void CreateAsset(string name, int[] supportedCounts, CollapsePolicy policy)
        {
            string path = $"{k_Dir}/{name}.asset";
            var config = AssetDatabase.LoadAssetAtPath<TableLayoutConfig>(path);
            bool isNew = config == null;
            if (isNew)
                config = ScriptableObject.CreateInstance<TableLayoutConfig>();

            var serialized = new SerializedObject(config);

            var counts = serialized.FindProperty("m_SupportedSeatCounts");
            counts.arraySize = supportedCounts.Length;
            for (int i = 0; i < supportedCounts.Length; i++)
                counts.GetArrayElementAtIndex(i).intValue = supportedCounts[i];

            serialized.FindProperty("m_CollapsePolicy").enumValueIndex = (int)policy;
            serialized.FindProperty("m_BaseRadius").floatValue = 0.75f;

            var presets = serialized.FindProperty("m_Presets");
            presets.arraySize = 2;
            WritePreset(presets.GetArrayElementAtIndex(0), 4, k_FourSeatYaws, 0f);
            // The 8-seat scale is pinned to 2 (the formula gives 1.85) to keep
            // the established doubled-table look.
            WritePreset(presets.GetArrayElementAtIndex(1), 8, k_EightSeatYaws, 2f);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (isNew)
                AssetDatabase.CreateAsset(config, path);
            EditorUtility.SetDirty(config);
            Debug.Log($"CreateTableLayoutAssets: {(isNew ? "created" : "updated")} {path}");
        }

        static void WritePreset(SerializedProperty preset, int seatCount, float[] yaws, float scale)
        {
            preset.FindPropertyRelative("seatCount").intValue = seatCount;
            preset.FindPropertyRelative("scale").floatValue = scale;
            var yawsProperty = preset.FindPropertyRelative("yaws");
            yawsProperty.arraySize = yaws.Length;
            for (int i = 0; i < yaws.Length; i++)
                yawsProperty.GetArrayElementAtIndex(i).floatValue = yaws[i];
        }
    }
}
