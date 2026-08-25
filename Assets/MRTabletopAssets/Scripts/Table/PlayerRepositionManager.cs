using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Wraps an action in the XR Interaction Toolkit's tunneling vignette so
    /// layout changes (table expansion/collapse) happen while the player's
    /// view is comfortably faded. Requests are queued, never dropped.
    /// </summary>
    public class PlayerRepositionManager : MonoBehaviour, ITunnelingVignetteProvider
    {
        [Header("Vignette Settings")]
        [SerializeField]
        TunnelingVignetteController m_TunnelingVignetteController;

        [SerializeField]
        float m_FadeDuration = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("Aperture size: 0 = full black fade, 0.5 = partial vignette")]
        float m_ApertureSize = 0f;

        [SerializeField, Range(0f, 1f), Tooltip("Feathering effect for vignette edge softness")]
        float m_FeatheringEffect = 0.1f;

        readonly Queue<Action> m_PendingActions = new Queue<Action>();
        VignetteParameters m_VignetteParameters;
        bool m_IsRunning;

        public VignetteParameters vignetteParameters => m_VignetteParameters;

        void Awake()
        {
            m_VignetteParameters = new VignetteParameters
            {
                apertureSize = m_ApertureSize,
                featheringEffect = m_FeatheringEffect,
                easeInTime = m_FadeDuration,
                easeOutTime = m_FadeDuration,
            };

            if (m_TunnelingVignetteController == null)
                m_TunnelingVignetteController = FindAnyObjectByType<TunnelingVignetteController>();
        }

        /// <summary>
        /// Runs <paramref name="action"/> under the vignette fade. If a fade
        /// is already in progress the action is queued and runs inside the
        /// same fade cycle. Without a vignette controller the action runs
        /// immediately.
        /// </summary>
        public void RunWithVignette(Action action)
        {
            if (action == null)
                return;

            m_PendingActions.Enqueue(action);

            if (m_IsRunning)
                return;

            if (m_TunnelingVignetteController == null || !isActiveAndEnabled)
            {
                DrainQueue();
                return;
            }

            StartCoroutine(VignetteSequence());
        }

        IEnumerator VignetteSequence()
        {
            m_IsRunning = true;

            m_TunnelingVignetteController.BeginTunnelingVignette(this);
            yield return new WaitForSeconds(m_FadeDuration);

            DrainQueue();

            m_TunnelingVignetteController.EndTunnelingVignette(this);
            yield return new WaitForSeconds(m_FadeDuration);

            m_IsRunning = false;

            // Anything queued while fading back in gets its own cycle.
            if (m_PendingActions.Count > 0)
                StartCoroutine(VignetteSequence());
        }

        void DrainQueue()
        {
            while (m_PendingActions.Count > 0)
                m_PendingActions.Dequeue().Invoke();
        }
    }
}
