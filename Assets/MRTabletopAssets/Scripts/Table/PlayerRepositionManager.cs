using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
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

            // 3. Position GameObjects based on seat (Table UI, Manipulators, HandleVisual)
            Debug.Log($"{DEBUG_TAG}Positioning GameObjects based on current seat...");
            PositionGameObjectsForCurrentSeat();

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

        private void PositionGameObjectsForCurrentSeat()
        {
            // All objects use seat-based positioning (not radial movement from current position)
            // Table UI is positioned at the exact user-specified positions
            PositionTableUIForCurrentSeat();

            // Manipulator handles are positioned slightly closer to table than UI so player can reach them
            PositionManipulatorHandlesForCurrentSeat();
        }

        private void PositionTableUIForCurrentSeat()
        {
            if (m_TableUI == null)
            {
                Debug.LogWarning($"{DEBUG_TAG}Table UI is null, cannot position");
                return;
            }

            // Get current seat number from TableTop
            int currentSeat = TableTop.k_CurrentSeat;
            if (currentSeat < 0 || currentSeat >= 8)
            {
                Debug.LogWarning($"{DEBUG_TAG}Invalid current seat: {currentSeat}, cannot position Table UI");
                return;
            }

            // Exact positions and rotations for 8-player mode (provided by user)
            // Format: (x, y, z, rotation)
            Vector4[] tableUIPositions = new Vector4[]
            {
                new Vector4(0f, 0.001f, -0.55f, 0f),      // Seat 0 (Player 1)
                new Vector4(0f, 0.001f, 0.55f, 180f),   // Seat 1 (Player 2)
                new Vector4(0.55f, 0.001f, 0f, 270f),   // Seat 2 (Player 3)
                new Vector4(-0.55f, 0.001f, 0f, 90f),     // Seat 3 (Player 4)
                new Vector4(-0.4f, 0.001f, -0.4f, 45f),  // Seat 4 (Player 5)
                new Vector4(-0.4f, 0.001f, 0.4f, 135f),  // Seat 5 (Player 6)
                new Vector4(0.4f, 0.001f, 0.4f, 225f),   // Seat 6 (Player 7)
                new Vector4(0.4f, 0.001f, -0.4f, 315f)   // Seat 7 (Player 8)
            };

            Vector4 posRot = tableUIPositions[currentSeat];
            Vector3 newPos = new Vector3(posRot.x, posRot.y, posRot.z);
            float rotation = posRot.w;

            m_TableUI.position = newPos;
            m_TableUI.rotation = Quaternion.Euler(0, rotation, 0);

            Debug.Log($"{DEBUG_TAG}Positioned Table UI for seat {currentSeat} at {newPos}, rotation: {rotation}°");
        }

        private void PositionManipulatorHandlesForCurrentSeat()
        {
            // Get current seat number from TableTop
            int currentSeat = TableTop.k_CurrentSeat;
            if (currentSeat < 0 || currentSeat >= 8)
            {
                Debug.LogWarning($"{DEBUG_TAG}Invalid current seat: {currentSeat}, cannot position manipulator handles");
                return;
            }

            // Manipulator handle positions - slightly closer to table than UI (0.55 vs 0.65 for cardinal, 0.35 vs 0.4 for diagonal)
            // Format: (x, y, z, rotation)
            Vector4[] handlePositions = new Vector4[]
            {
                new Vector4(0f, 0.001f, -.45f, 0f),      // Seat 0 (Player 1)
                new Vector4(0f, 0.001f, 0.45f, 180f),   // Seat 1 (Player 2)
                new Vector4(0.45f, 0.001f, 0f, 270f),   // Seat 2 (Player 3)
                new Vector4(-0.45f, 0.001f, 0f, 90f),     // Seat 3 (Player 4)
                new Vector4(-0.3f, 0.001f, -0.3f, 45f),  // Seat 4 (Player 5)
                new Vector4(-0.3f, 0.001f, 0.3f, 135f),  // Seat 5 (Player 6)
                new Vector4(0.3f, 0.001f, 0.3f, 225f),   // Seat 6 (Player 7)
                new Vector4(0.3f, 0.001f, -0.3f, 315f)   // Seat 7 (Player 8)
            };

            Vector4 posRot = handlePositions[currentSeat];
            Vector3 newPos = new Vector3(posRot.x, posRot.y, posRot.z);
            float rotation = posRot.w;

            // Position all three manipulator handles at the same location
            if (m_TableManipulatorRotation != null)
            {
                m_TableManipulatorRotation.position = newPos;
                m_TableManipulatorRotation.rotation = Quaternion.Euler(0, rotation, 0);
            }

            if (m_TableManipulatorFreeMove != null)
            {
                m_TableManipulatorFreeMove.position = newPos;
                m_TableManipulatorFreeMove.rotation = Quaternion.Euler(0, rotation, 0);
            }

            if (m_HandleVisual != null)
            {
                m_HandleVisual.position = newPos;
                m_HandleVisual.rotation = Quaternion.Euler(0, rotation, 0);
            }

            Debug.Log($"{DEBUG_TAG}Positioned manipulator handles for seat {currentSeat} at {newPos}, rotation: {rotation}°");
        }

        private void MoveRadially(Transform obj, float distance, string objectName)
        {
            if (obj == null)
            {
                Debug.LogWarning($"{DEBUG_TAG}{objectName} is null, skipping radial movement");
                return;
            }

            Vector3 currentPos = obj.position;

            // Calculate direction in XZ plane only (preserve Y)
            Vector3 currentPosXZ = new Vector3(currentPos.x, m_TableCenter.y, currentPos.z);
            Vector3 tableCenterXZ = new Vector3(m_TableCenter.x, m_TableCenter.y, m_TableCenter.z);
            Vector3 directionXZ = (currentPosXZ - tableCenterXZ).normalized;

            // Move only in XZ plane, preserve Y
            Vector3 newPos = currentPos + (directionXZ * distance);
            newPos.y = currentPos.y; // Explicitly preserve Y

            obj.position = newPos;
            Debug.Log($"{DEBUG_TAG}Moved {objectName} radially from {currentPos} to {newPos} (directionXZ: {directionXZ}, distance: {distance})");
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

        /// <summary>
        /// Scales table objects only (without vignette or player repositioning).
        /// Used for late-joining clients who join after the table has already expanded to 8-player mode.
        /// </summary>
        public void ScaleTableObjectsOnly()
        {
            Debug.Log($"{DEBUG_TAG}ScaleTableObjectsOnly - Scaling table objects for late-joining client");

            // Scale GameObjects (TableTop, Hover Visuals, PassthroughVolume)
            ScaleGameObjects(2.0f);

            // Position Table UI for the current seat (late-joining clients need this too)
            PositionTableUIForCurrentSeat();

            Debug.Log($"{DEBUG_TAG}ScaleTableObjectsOnly - Complete");
        }
    }
}

