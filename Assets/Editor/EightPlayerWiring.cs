using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.EditorTools
{
    /// <summary>
    /// One-shot batchmode utility for the Phase 1 prefab/scene wiring:
    /// seats 4-7 move into TableTop.prefab with derived positions, the
    /// TableSystem.prefab added-object seats and stale overrides are removed,
    /// and the scene gets the layout config and scaled-roots wiring.
    /// Re-run via -executeMethod MRTTT.EditorTools.EightPlayerWiring.Apply
    /// </summary>
    public static class EightPlayerWiring
    {
        const string k_TableTopPrefab = "Assets/MRTabletopAssets/Prefabs/TableSystem/TableTop.prefab";
        const string k_TableSystemPrefab = "Assets/MRTabletopAssets/Prefabs/TableSystem/TableSystem.prefab";
        const string k_PlayerMenuPrefab = "Assets/MRTabletopAssets/Prefabs/UIPrefabs/PlayerMenu/Player Menu UI.prefab";
        const string k_Scene = "Assets/Scenes/SampleScene.unity";
        const string k_Config = "Assets/MRTabletopAssets/Settings/TableLayout_4or8.asset";

        const float k_CornerSeatDistance = 0.9925f;
        static readonly float[] k_CornerYaws = { 45f, 135f, 225f, 315f };

        public static void Apply()
        {
            WireTableTopPrefab();
            WireTableSystemPrefab();
            WireScene();
            FixSeatButtonArguments();
            ReportSeatButtonWiring();
            AssetDatabase.SaveAssets();
            Debug.Log("EightPlayerWiring: done");
        }

        /// <summary>
        /// Seat buttons must request their own seat: the hand-added buttons
        /// 5-8 were all wired to RequestSeat(3).
        /// </summary>
        static void FixSeatButtonArguments()
        {
            var root = PrefabUtility.LoadPrefabContents(k_PlayerMenuPrefab);
            try
            {
                int fixedCount = 0;
                foreach (var seatButton in root.GetComponentsInChildren<TableTopSeatButton>(true))
                {
                    var seatSerialized = new SerializedObject(seatButton);
                    if (seatSerialized.FindProperty("m_IsSpectator").boolValue)
                        continue;

                    int seatID = seatSerialized.FindProperty("m_SeatID").intValue;
                    var button = seatButton.GetComponentInChildren<Button>(true);
                    if (button == null)
                        continue;

                    var buttonSerialized = new SerializedObject(button);
                    var calls = buttonSerialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        var call = calls.GetArrayElementAtIndex(i);
                        if (call.FindPropertyRelative("m_MethodName").stringValue != "RequestSeat")
                            continue;

                        var argument = call.FindPropertyRelative("m_Arguments.m_IntArgument");
                        if (argument.intValue != seatID)
                        {
                            argument.intValue = seatID;
                            fixedCount++;
                        }
                    }
                    buttonSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                if (fixedCount > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, k_PlayerMenuPrefab);
                Debug.Log($"EightPlayerWiring: corrected {fixedCount} RequestSeat argument(s)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WireTableTopPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(k_TableTopPrefab);
            try
            {
                var tableTop = root.GetComponent<TableTop>();
                var serialized = new SerializedObject(tableTop);
                var seats = serialized.FindProperty("m_Seats");

                var seatParent = seats.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("seatTransform").objectReferenceValue is Transform seat0
                    ? seat0.parent
                    : root.transform;

                seats.arraySize = 8;
                for (int i = 4; i < 8; i++)
                {
                    string name = $"Seat ({i})";
                    var existing = seatParent.Find(name);
                    var seatTransform = existing != null ? existing : new GameObject(name).transform;
                    seatTransform.SetParent(seatParent, false);

                    float yaw = k_CornerYaws[i - 4];
                    seatTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    seatTransform.localPosition = SeatGeometry.SeatLocalPosition(yaw, k_CornerSeatDistance);

                    var entry = seats.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("seatTransform").objectReferenceValue = seatTransform;
                    entry.FindPropertyRelative("seatID").intValue = i;
                    entry.FindPropertyRelative("seatDistanceOverride").floatValue = k_CornerSeatDistance;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_TableTopPrefab);
                Debug.Log("EightPlayerWiring: TableTop.prefab now owns Seat (4..7) with derived positions");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WireTableSystemPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(k_TableSystemPrefab);
            try
            {
                // Remove the hand-placed added-object seats.
                int removed = 0;
                foreach (var transform in root.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if (transform != null && transform.name.StartsWith("-Seat ("))
                    {
                        Object.DestroyImmediate(transform.gameObject);
                        removed++;
                    }
                }

                // Drop the stale m_Seats/tableUI overrides on the nested
                // TableTop instance (its prefab owns the seats now).
                var tableTop = root.GetComponentInChildren<TableTop>(true);
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(tableTop.gameObject);
                var modifications = PrefabUtility.GetPropertyModifications(instanceRoot);
                if (modifications != null)
                {
                    var kept = modifications.Where(m =>
                        !m.propertyPath.StartsWith("m_Seats.Array") &&
                        !m.propertyPath.StartsWith("tableUI.Array")).ToArray();
                    if (kept.Length != modifications.Length)
                    {
                        PrefabUtility.SetPropertyModifications(instanceRoot, kept);
                        Debug.Log($"EightPlayerWiring: dropped {modifications.Length - kept.Length} stale TableTop instance overrides");
                    }
                }

                // Prefab-level scaled roots: the TableTop root and Hover
                // Visuals live here; the PassthroughVolume is added as a
                // scene override.
                var seatSystem = root.GetComponentInChildren<TableSeatSystem>(true);
                var hoverVisuals = FindDescendant(root.transform, "Hover Visuals");
                var serialized = new SerializedObject(seatSystem);
                var roots = serialized.FindProperty("m_TableScaledRoots");
                roots.arraySize = hoverVisuals != null ? 2 : 1;
                roots.GetArrayElementAtIndex(0).objectReferenceValue = tableTop.transform;
                if (hoverVisuals != null)
                    roots.GetArrayElementAtIndex(1).objectReferenceValue = hoverVisuals;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, k_TableSystemPrefab);
                Debug.Log($"EightPlayerWiring: TableSystem.prefab cleaned ({removed} added-object seats removed), scaled roots wired");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WireScene()
        {
            var scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Single);

            var config = AssetDatabase.LoadAssetAtPath<TableLayoutConfig>(k_Config);
            var manager = Object.FindFirstObjectByType<NetworkTableTopManager>(FindObjectsInactive.Include);
            var managerSerialized = new SerializedObject(manager);
            managerSerialized.FindProperty("m_Config").objectReferenceValue = config;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var seatSystem = Object.FindFirstObjectByType<TableSeatSystem>(FindObjectsInactive.Include);
            var tableTop = Object.FindFirstObjectByType<TableTop>(FindObjectsInactive.Include);
            var hoverVisuals = FindSceneObject("Hover Visuals");
            var passthrough = FindSceneObject("PassthroughVolume");

            var roots = new List<Transform> { tableTop.transform };
            if (hoverVisuals != null)
                roots.Add(hoverVisuals);
            if (passthrough != null)
                roots.Add(passthrough);

            var seatSerialized = new SerializedObject(seatSystem);
            var rootsProperty = seatSerialized.FindProperty("m_TableScaledRoots");
            rootsProperty.arraySize = roots.Count;
            for (int i = 0; i < roots.Count; i++)
                rootsProperty.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            seatSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"EightPlayerWiring: scene wired (config={config != null}, scaledRoots={roots.Count}, passthrough={passthrough != null})");
        }

        static void ReportSeatButtonWiring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_PlayerMenuPrefab);
            foreach (var seatButton in prefab.GetComponentsInChildren<TableTopSeatButton>(true))
            {
                var serialized = new SerializedObject(seatButton);
                int seatID = serialized.FindProperty("m_SeatID").intValue;
                bool isSpectator = serialized.FindProperty("m_IsSpectator").boolValue;

                var button = seatButton.GetComponentInChildren<Button>(true);
                var calls = new List<string>();
                if (button != null)
                {
                    var buttonSerialized = new SerializedObject(button);
                    var persistentCalls = buttonSerialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        int intArgument = persistentCalls.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("m_Arguments.m_IntArgument").intValue;
                        calls.Add($"{button.onClick.GetPersistentTarget(i)?.GetType().Name}.{button.onClick.GetPersistentMethodName(i)}({intArgument})");
                    }
                }

                Debug.Log($"EightPlayerWiring: seat button '{seatButton.name}' seatID={seatID} spectator={isSpectator} onClick=[{string.Join("; ", calls)}]");
            }
        }

        static Transform FindDescendant(Transform root, string name)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == name)
                    return transform;
            }
            return null;
        }

        static Transform FindSceneObject(string name)
        {
            var found = GameObject.Find(name);
            if (found == null)
            {
                foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (transform.name == name)
                        return transform;
                }
                return null;
            }
            return found.transform;
        }
    }
}
