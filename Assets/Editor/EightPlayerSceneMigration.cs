using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;
using UnityEngine.XR.Templates.MRTTabletopAssets;

/// <summary>
/// One-shot migration: rebuilds the 8-player scene wiring from the legacy 8Player branch
/// onto the template-V2 SampleScene. Safe to re-run (skips work it has already done).
/// Run headless: Unity.exe -batchmode -executeMethod EightPlayerSceneMigration.Run -quit
/// </summary>
public static class EightPlayerSceneMigration
{
    const string k_Log = "8PMIG: ";
    const string k_ScenePath = "Assets/Scenes/SampleScene.unity";
    const string k_PlayerMenuPrefabPath = "Assets/MRTabletopAssets/Prefabs/UIPrefabs/PlayerMenu/Player Menu UI.prefab";
    const string k_HoverPrefabPath = "Assets/MRTabletopAssets/Prefabs/Seat Hover Visuals.prefab";
    const string k_GridColorMatPath = "Assets/MRTabletopAssets/Materials/TableMaterials/TableGridMaterialColor.mat";
    const string k_SeatHoverMatPathFmt = "Assets/MRTabletopAssets/Materials/SeatHoverMaterials/TableSeatHover - {0}.mat";

    // Seat transform overrides (octagon layout) — authoritative: V2 TableTop no longer auto-places seats.
    static readonly (string name, Vector3 pos, Quaternion rot)[] k_SeatOverrides =
    {
        ("-Seat (4)", new Vector3(-0.75f, 0f, -0.65f), new Quaternion(0f, 0.38268343f, 0f, 0.92387956f)),
        ("-Seat (5)", new Vector3(-0.75f, 0f, 0.65f), new Quaternion(0f, 0.92387956f, 0f, 0.38268343f)),
        ("-Seat (6)", new Vector3(0.75f, 0f, 0.65f), new Quaternion(0f, 0.9238796f, 0f, -0.38268325f)),
        ("-Seat (7)", new Vector3(0.75f, 0f, -0.65f), new Quaternion(0f, 0.3826836f, 0f, -0.92387944f)),
    };

    static readonly Vector3[] k_HoverPositions =
    {
        new Vector3(-0.7f, 0f, -0.6f),
        new Vector3(-0.7f, 0f, 0.5f),
        new Vector3(0.7f, 0f, 0.5f),
        new Vector3(0.7f, 0f, -0.5f),
    };

    static readonly Color[] k_PlayerColors =
    {
        new Color(1f, 0f, 0.5009947f, 1f),
        new Color(1f, 0.5019608f, 0f, 1f),
        new Color(1f, 1f, 0f, 1f),
        new Color(0.5019608f, 0f, 0.5019608f, 1f),
        new Color(1f, 0f, 0f, 1f),
        new Color(0f, 0.7176471f, 0f, 1f),
        new Color(0f, 0f, 0f, 1f),
        new Color(0.70440245f, 0.67117584f, 0.67117584f, 1f),
    };

    public static void Run()
    {
        WirePlayerMenuPrefab();
        MigrateScene();
        AssetDatabase.SaveAssets();
        Debug.Log(k_Log + "SUCCESS");
    }

    static void WirePlayerMenuPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(k_PlayerMenuPrefabPath);
        try
        {
            var layout = root.GetComponentInChildren<SeatButtonLayout>(true);
            if (layout == null) throw new Exception("SeatButtonLayout not found in Player Menu UI.prefab");

            var buttons = ButtonsBySeatId(root.transform, 8);
            var so = new SerializedObject(layout);
            var arr = so.FindProperty("m_SeatButtons");
            arr.arraySize = 8;
            for (int i = 0; i < 8; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, k_PlayerMenuPrefabPath);
            Debug.Log(k_Log + "Player Menu UI.prefab: SeatButtonLayout wired with 8 buttons");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void MigrateScene()
    {
        var scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);

        var virtualTable = FindInScene(scene, "Virtual Table");
        var seatSystem = FindComponent<TableSeatSystem>();
        var tableSystemRoot = seatSystem.gameObject;
        var ntm = FindComponent<NetworkTableTopManager>();
        var navigationMenu = FindInScene(scene, "Navigation Menu");

        var hoverVisualsT = FindChild(tableSystemRoot.transform, "Hover Visuals");
        var tableTopT = FindChild(tableSystemRoot.transform, "TableTop");
        var planeT = FindChild(tableTopT, "Plane");

        // 1. PlayerColorManager scene object (parity note: legacy scene had no NetworkObject on it).
        var pcm = FindComponentOrNull<PlayerColorManager>();
        if (pcm == null)
        {
            var go = new GameObject("PlayerColorManager");
            go.transform.SetParent(virtualTable.transform, false);
            go.transform.SetSiblingIndex(0);
            pcm = go.AddComponent<PlayerColorManager>();
            var so = new SerializedObject(pcm);
            var colors = so.FindProperty("m_PlayerColors");
            colors.arraySize = 8;
            for (int i = 0; i < 8; i++)
                colors.GetArrayElementAtIndex(i).colorValue = k_PlayerColors[i];
            SetIfPresent(so, "m_ActivePlayerIndex", p => p.intValue = -1);
            SetIfPresent(so, "m_ActivePlayerGlowColor", p => p.colorValue = Color.white);
            SetIfPresent(so, "m_ActivePlayerGlowIntensity", p => p.floatValue = 0.3f);
            SetIfPresent(so, "m_ActivePlayerPulseSpeed", p => p.floatValue = 1f);
            SetIfPresent(so, "m_EnablePulseAnimation", p => p.boolValue = true);
            SetIfPresent(so, "m_HighlightMode", p => p.enumValueIndex = 3);
            SetIfPresent(so, "m_UseColorBlindFriendlyContrast", p => p.boolValue = false);
            SetIfPresent(so, "m_PatternIntensity", p => p.floatValue = 0.5f);
            SetIfPresent(so, "m_AnimationSpeed", p => p.floatValue = 1f);
            SetIfPresent(so, "m_BrightnessPulseIntensity", p => p.floatValue = 0.3f);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log(k_Log + "PlayerColorManager created under Virtual Table");
        }

        // 2. Seat 5-8 transform overrides (octagon).
        foreach (var (name, pos, rot) in k_SeatOverrides)
        {
            var seat = FindChild(tableSystemRoot.transform, name);
            seat.localPosition = pos;
            seat.localRotation = rot;
            seat.localScale = Vector3.one;
        }
        Debug.Log(k_Log + "Seat (4..7) transforms set to octagon layout");

        // 3. Four Seat Hover Visuals instances with TableSeatHover 5-8 materials.
        var hoverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_HoverPrefabPath);
        if (hoverPrefab == null) throw new Exception("Seat Hover Visuals.prefab not found");
        var hoverRoots = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            string instName = $"Seat Hover Visuals ({4 + i})";
            var existing = FindChildOrNull(hoverVisualsT, instName);
            if (existing != null)
            {
                hoverRoots[i] = existing.gameObject;
                continue;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(hoverPrefab, scene);
            inst.transform.SetParent(hoverVisualsT, false);
            inst.name = instName;
            inst.transform.localPosition = k_HoverPositions[i];
            inst.transform.localRotation = Quaternion.identity;
            inst.SetActive(false);
            var fade = FindChild(inst.transform, "Fade_Seat");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(string.Format(k_SeatHoverMatPathFmt, 5 + i));
            if (mat == null) throw new Exception($"TableSeatHover - {5 + i}.mat not found");
            fade.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat };
            hoverRoots[i] = inst;
        }
        Debug.Log(k_Log + "Seat Hover Visuals (4..7) instantiated");

        // 4. PlayerRepositionManager on the TableSystem instance root.
        var prm = tableSystemRoot.GetComponent<PlayerRepositionManager>();
        if (prm == null) prm = tableSystemRoot.AddComponent<PlayerRepositionManager>();
        {
            var so = new SerializedObject(prm);
            var vignette = FindComponentOrNull<TunnelingVignetteController>();
            if (vignette == null) Debug.LogWarning(k_Log + "TunnelingVignetteController not found in scene - left unwired");
            SetIfPresent(so, "m_TunnelingVignetteController", p => p.objectReferenceValue = vignette);
            SetIfPresent(so, "m_FadeDuration", p => p.floatValue = 0.5f);
            SetIfPresent(so, "m_ApertureSize", p => p.floatValue = 0f);
            SetIfPresent(so, "m_FeatheringEffect", p => p.floatValue = 0.1f);
            SetIfPresent(so, "m_TableTop", p => p.objectReferenceValue = tableTopT);
            SetIfPresent(so, "m_HoverVisuals", p => p.objectReferenceValue = hoverVisualsT);
            SetIfPresent(so, "m_PassthroughVolume", p => p.objectReferenceValue = FindChild(tableSystemRoot.transform, "PassthroughVolume"));
            SetIfPresent(so, "m_TableUI", p => p.objectReferenceValue = navigationMenu.transform);
            SetIfPresent(so, "m_TableManipulatorRotation", p => p.objectReferenceValue = FindChild(tableSystemRoot.transform, "TableManipulator - Only Rotation"));
            SetIfPresent(so, "m_TableManipulatorFreeMove", p => p.objectReferenceValue = FindChild(tableSystemRoot.transform, "Seat Move Handle"));
            SetIfPresent(so, "m_HandleVisual", p => p.objectReferenceValue = FindChild(tableSystemRoot.transform, "HandleVisual"));
            SetIfPresent(so, "m_NetworkTableTopManager", p => p.objectReferenceValue = ntm);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log(k_Log + "PlayerRepositionManager added and wired");

        // 5. VirtualSurfaceColorShaderUpdater on TableTop/Plane.
        var vscsu = planeT.GetComponent<VirtualSurfaceColorShaderUpdater>();
        if (vscsu == null) vscsu = planeT.gameObject.AddComponent<VirtualSurfaceColorShaderUpdater>();
        {
            var so = new SerializedObject(vscsu);
            SetIfPresent(so, "m_UsePlayerColorManagerSettings", p => p.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 6. Plane material -> TableGridMaterialColor (scene override; prefab keeps TableGridMaterial).
        var gridColorMat = AssetDatabase.LoadAssetAtPath<Material>(k_GridColorMatPath);
        if (gridColorMat == null) throw new Exception("TableGridMaterialColor.mat not found");
        var planeRenderer = planeT.GetComponent<MeshRenderer>();
        var mats = planeRenderer.sharedMaterials;
        mats[0] = gridColorMat;
        planeRenderer.sharedMaterials = mats;
        Debug.Log(k_Log + "Plane material swapped to TableGridMaterialColor");

        // 7. NetworkTableTopManager: 8 seat buttons + reposition manager + NetworkObject setting.
        var sceneButtons = ButtonsBySeatId(navigationMenu.transform, 8);
        {
            var so = new SerializedObject(ntm);
            var arr = so.FindProperty("m_SeatButtons");
            if (arr == null) throw new Exception("NetworkTableTopManager.m_SeatButtons not found");
            arr.arraySize = 8;
            for (int i = 0; i < 8; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = sceneButtons[i];
            SetIfPresent(so, "m_PlayerRepositionManager", p => p.objectReferenceValue = prm);
            so.ApplyModifiedPropertiesWithoutUndo();

            var netObj = new SerializedObject(ntm.GetComponent<NetworkObject>());
            SetIfPresent(netObj, "SceneMigrationSynchronization", p => p.boolValue = false);
            netObj.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log(k_Log + "NetworkTableTopManager wired with 8 seat buttons");

        // 8. TableSeatSystem -> NetworkTableTopManager (prefab default is None; scene-level wire).
        {
            var so = new SerializedObject(seatSystem);
            SetIfPresent(so, "m_NetworkTableTopManager", p => p.objectReferenceValue = ntm);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 9. Seat buttons 5-8 -> world-space hover visuals.
        for (int seatId = 4; seatId <= 7; seatId++)
        {
            var so = new SerializedObject(sceneButtons[seatId]);
            var arr = so.FindProperty("m_WorldSpaceSeatHoverObjects");
            if (arr == null) throw new Exception("TableTopSeatButton.m_WorldSpaceSeatHoverObjects not found");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = hoverRoots[seatId - 4];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log(k_Log + "Seat buttons 5-8 wired to hover visuals");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Failed to save SampleScene");
        Debug.Log(k_Log + "SampleScene saved");
    }

    static Component[] ButtonsBySeatId(Transform root, int expected)
    {
        var buttons = root.GetComponentsInChildren<TableTopSeatButton>(true);
        var byId = new Dictionary<int, Component>();
        foreach (var b in buttons)
        {
            int id = new SerializedObject(b).FindProperty("m_SeatID").intValue;
            if (!byId.ContainsKey(id)) byId[id] = b;
        }
        if (byId.Count != expected)
            throw new Exception($"Expected {expected} seat buttons with unique m_SeatID under '{root.name}', found ids: [{string.Join(",", byId.Keys.OrderBy(k => k))}]");
        return Enumerable.Range(0, expected).Select(i => byId[i]).ToArray();
    }

    static void SetIfPresent(SerializedObject so, string prop, Action<SerializedProperty> set)
    {
        var p = so.FindProperty(prop);
        if (p == null)
        {
            Debug.LogWarning(k_Log + $"Property '{prop}' not found on {so.targetObject.GetType().Name} - skipped");
            return;
        }
        set(p);
    }

    static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = FindChildOrNull(root.transform, name);
            if (t != null) return t.gameObject;
        }
        throw new Exception($"GameObject '{name}' not found in scene");
    }

    static T FindComponent<T>() where T : Component
    {
        var c = FindComponentOrNull<T>();
        if (c == null) throw new Exception($"Component {typeof(T).Name} not found in scene");
        return c;
    }

    static T FindComponentOrNull<T>() where T : Component
        => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    static Transform FindChild(Transform root, string name)
    {
        var t = FindChildOrNull(root, name);
        if (t == null) throw new Exception($"Child '{name}' not found under '{root.name}'");
        return t;
    }

    static Transform FindChildOrNull(Transform root, string name)
        => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != root && t.name == name);
}
