using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace MRTabletopAssets
{
    /// <summary>
    /// Manages player repositioning when table expands to 8-player mode.
    /// Handles XR Origin movement, GameObject scaling/repositioning, and tunneling vignette effects.
    /// Implements ITunnelingVignetteProvider to use the XR Interaction Toolkit's vignette system.
    /// </summary>
    public class PlayerRepositionManager : MonoBehaviour, ITunnelingVignetteProvider
    {
        private const string DEBUG_TAG = "[PlayerRepositionManager] ";

        [Header("Vignette Settings")]
        [SerializeField] private TunnelingVignetteController m_TunnelingVignetteController;
        [SerializeField] private float m_FadeDuration = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Aperture size: 0 = full black fade, 0.5 = partial vignette")]
        private float m_ApertureSize = 0f;
        [SerializeField, Range(0f, 1f), Tooltip("Feathering effect for vignette edge softness")]
        private float m_FeatheringEffect = 0.1f;


        [Header("GameObject References - Scale to 2.0")]
        [SerializeField] private Transform m_TableTop;
        [SerializeField] private Transform m_HoverVisuals;
        [SerializeField] private Transform m_PassthroughVolume;

        [Header("GameObject References - Move Radially (No Scale)")]
        [SerializeField] private Transform m_TableUI;
        [SerializeField] private Transform m_TableManipulatorRotation;
        [SerializeField] private Transform m_TableManipulatorFreeMove;
        [SerializeField] private Transform m_HandleVisual;



        [Header("References")]
        [SerializeField] private NetworkTableTopManager m_NetworkTableTopManager;

        private XROrigin m_XROrigin;
        private Vector3 m_TableCenter;
        private bool m_IsRepositioning = false;

        // ITunnelingVignetteProvider implementation
        public VignetteParameters vignetteParameters => new VignetteParameters();

        private void Awake()
        {
            // Find XR Origin in the scene
            m_XROrigin = FindFirstObjectByType<XROrigin>();
            if (m_XROrigin == null)
            {
                Debug.LogError($"{DEBUG_TAG}XR Origin not found in scene!");
            }

            // Find TunnelingVignetteController in the scene

            if (m_TunnelingVignetteController == null)
            {
                m_TunnelingVignetteController = FindFirstObjectByType<TunnelingVignetteController>();
                if (m_TunnelingVignetteController == null)
                {
                    Debug.LogError($"{DEBUG_TAG}TunnelingVignetteController not found in scene! Vignette effects will not work.");
                }
            }
            else
            {
                Debug.Log($"{DEBUG_TAG}TunnelingVignetteController found: {m_TunnelingVignetteController.name}");
            }

            // Auto-find GameObjects if not assigned
            if (m_TableTop == null)
            {
                GameObject tableTopObj = GameObject.Find("TableTop");
                if (tableTopObj != null) m_TableTop = tableTopObj.transform;
            }

            if (m_HoverVisuals == null)
            {
                GameObject hoverVisualsObj = GameObject.Find("Hover Visuals");
                if (hoverVisualsObj != null) m_HoverVisuals = hoverVisualsObj.transform;
            }

            if (m_PassthroughVolume == null)
            {
                GameObject passthroughObj = GameObject.Find("PassthroughVolume");
                if (passthroughObj != null) m_PassthroughVolume = passthroughObj.transform;
            }

            if (m_TableUI == null)
            {
                GameObject tableUIObj = GameObject.Find("Table UI");
                if (tableUIObj != null) m_TableUI = tableUIObj.transform;
            }

            if (m_TableManipulatorRotation == null)
            {
                GameObject manipulatorRotObj = GameObject.Find("TableManipulator - Only Rotation");
                if (manipulatorRotObj != null) m_TableManipulatorRotation = manipulatorRotObj.transform;
            }

            if (m_TableManipulatorFreeMove == null)
            {
                GameObject manipulatorFreeObj = GameObject.Find("TableManipulator - Free Move");
                if (manipulatorFreeObj != null) m_TableManipulatorFreeMove = manipulatorFreeObj.transform;
            }

            if (m_HandleVisual == null)
            {
                GameObject handleVisualObj = GameObject.Find("HandleVisual");
                if (handleVisualObj != null) m_HandleVisual = handleVisualObj.transform;
            }

            if (m_NetworkTableTopManager == null)
            {
                m_NetworkTableTopManager = FindFirstObjectByType<NetworkTableTopManager>();
            }

            Debug.Log($"{DEBUG_TAG}Initialized - XR Origin: {m_XROrigin != null}, TunnelingVignette: {m_TunnelingVignetteController != null}, " +
                      $"TableTop: {m_TableTop != null}, HoverVisuals: {m_HoverVisuals != null}, PassthroughVolume: {m_PassthroughVolume != null}, " +
                      $"TableUI: {m_TableUI != null}, Manipulators: {m_TableManipulatorRotation != null && m_TableManipulatorFreeMove != null}");
        }

        /// <summary>
        /// Triggers player repositioning with tunneling vignette when table expands to 8-player mode.
        /// Only repositions players who are already seated (players 1-4).
        /// New players (5-8) spawn directly at correct positions.
        /// </summary>
        public void RepositionPlayerWithFade()
        {
            if (m_IsRepositioning)
            {
                Debug.LogWarning($"{DEBUG_TAG}Already repositioning, ignoring request");
                return;
            }

            Debug.Log($"{DEBUG_TAG}Starting player repositioning sequence with tunneling vignette");
            StartCoroutine(RepositionSequence());
        }

        private IEnumerator RepositionSequence()
        {
            m_IsRepositioning = true;

            // Store table center BEFORE any transformations
            if (m_TableTop != null)
            {
                m_TableCenter = m_TableTop.position;
                Debug.Log($"{DEBUG_TAG}Table center stored: {m_TableCenter}");
            }
            else
            {
                Debug.LogError($"{DEBUG_TAG}TableTop is null! Cannot determine table center");
                m_IsRepositioning = false;
                yield break;
            }

            // 1. Begin tunneling vignette (fade to black/vignette)
            if (m_TunnelingVignetteController != null)
            {
                Debug.Log($"{DEBUG_TAG}Beginning tunneling vignette (aperture: {m_ApertureSize}, duration: {m_FadeDuration}s)...");
                m_TunnelingVignetteController.BeginTunnelingVignette(this);
                yield return new WaitForSeconds(m_FadeDuration);
            }
            else
            {
                Debug.LogWarning($"{DEBUG_TAG}TunnelingVignetteController is null, skipping vignette effect");
            }

            // 2. Scale GameObjects (TableTop, Hover Visuals, PassthroughVolume)
            Debug.Log($"{DEBUG_TAG}Scaling GameObjects to 2.0...");
            ScaleGameObjects(2.0f);

            // 3. Move GameObjects radially outward (Table UI, Manipulators, HandleVisual)
            Debug.Log($"{DEBUG_TAG}Moving GameObjects radially outward by 0.75f...");
            MoveGameObjectsRadially(0.75f);

            // 4. Calculate and apply XR Origin radial movement
            Debug.Log($"{DEBUG_TAG}Calculating XR Origin radial movement...");
            MoveXROriginRadially(0.75f);

            // 5. End tunneling vignette (fade from black/vignette)
            if (m_TunnelingVignetteController != null)
            {
                Debug.Log($"{DEBUG_TAG}Ending tunneling vignette...");
                m_TunnelingVignetteController.EndTunnelingVignette(this);
                yield return new WaitForSeconds(m_FadeDuration);
            }

            m_IsRepositioning = false;
            Debug.Log($"{DEBUG_TAG}Repositioning sequence complete");
        }

        private void ScaleGameObjects(float scaleFactor)
        {
            if (m_TableTop != null)
            {
                m_TableTop.localScale = Vector3.one * scaleFactor;
                Debug.Log($"{DEBUG_TAG}Scaled TableTop to {scaleFactor}");
            }

            if (m_HoverVisuals != null)
            {
                m_HoverVisuals.localScale = Vector3.one * scaleFactor;
                Debug.Log($"{DEBUG_TAG}Scaled HoverVisuals to {scaleFactor}");
            }

            if (m_PassthroughVolume != null)
            {
                m_PassthroughVolume.localScale = Vector3.one * scaleFactor;
                Debug.Log($"{DEBUG_TAG}Scaled PassthroughVolume to {scaleFactor}");
            }
        }

        private void MoveGameObjectsRadially(float distance)
        {
            MoveRadially(m_TableUI, distance, "Table UI", preserveY: true);
            MoveRadially(m_TableManipulatorRotation, distance, "TableManipulator - Only Rotation", preserveY: false);
            MoveRadially(m_TableManipulatorFreeMove, distance, "TableManipulator - Free Move", preserveY: false);
            MoveRadially(m_HandleVisual, distance, "HandleVisual", preserveY: false);
        }

        private void MoveRadially(Transform obj, float distance, string objectName, bool preserveY = false)
        {
            if (obj == null)
            {
                Debug.LogWarning($"{DEBUG_TAG}{objectName} is null, skipping radial movement");
                return;
            }

            Vector3 currentPos = obj.position;
            Vector3 direction = (currentPos - m_TableCenter).normalized;

            // If preserveY is true, only move in the XZ plane
            if (preserveY)
            {
                // Zero out the Y component of the direction to move only horizontally
                direction.y = 0f;
                direction.Normalize();
            }

            Vector3 newPos = currentPos + (direction * distance);

            // If preserveY is true, restore the original Y value
            if (preserveY)
            {
                newPos.y = currentPos.y;
            }

            obj.position = newPos;
            Debug.Log($"{DEBUG_TAG}Moved {objectName} radially from {currentPos} to {newPos} (direction: {direction}, distance: {distance}, preserveY: {preserveY})");
        }

        private void MoveXROriginRadially(float distance)
        {
            if (m_XROrigin == null)
            {
                Debug.LogError($"{DEBUG_TAG}XR Origin is null, cannot reposition player");
                return;
            }

            Vector3 currentPos = m_XROrigin.transform.position;
            Vector3 direction = (currentPos - m_TableCenter).normalized;
            Vector3 newPos = currentPos + (direction * distance);

            m_XROrigin.transform.position = newPos;
            Debug.Log($"{DEBUG_TAG}Moved XR Origin radially from {currentPos} to {newPos} (direction: {direction}, distance: {distance})");
        }
    }
}

