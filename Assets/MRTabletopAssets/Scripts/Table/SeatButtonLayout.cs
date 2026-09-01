using UnityEngine.UI;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Sizes the seat-button scroll content to the current seat layout so the
    /// horizontal scrollbar only appears when there are more seat cards than
    /// fit the viewport. Follows <see cref="TableTop.seatLayoutChanged"/>;
    /// seat card visibility itself is owned by NetworkTableTopManager.
    /// </summary>
    public class SeatButtonLayout : MonoBehaviour
    {
        [SerializeField] RectTransform m_ContentPanel;

        // Wired by the scene migration; retained so re-running it stays valid.
        [SerializeField] TableTopSeatButton[] m_SeatButtons;

        [SerializeField, Tooltip("Horizontal distance between seat card origins; content width = pitch * seat count.")]
        float m_ButtonPitch = 50f;

        TableTop m_TableTop;
        ScrollRect m_ScrollRect;

        void OnEnable()
        {
            m_ScrollRect = GetComponentInParent<ScrollRect>(true);
            m_TableTop = FindAnyObjectByType<TableTop>(FindObjectsInactive.Include);
            if (m_TableTop != null)
            {
                m_TableTop.seatLayoutChanged += SetContentWidth;
                SetContentWidth(m_TableTop.currentSeatCount);
            }
        }

        void OnDisable()
        {
            if (m_TableTop != null)
                m_TableTop.seatLayoutChanged -= SetContentWidth;
        }

        /// <summary>
        /// Sets the content panel width to fit the seat cards of the current
        /// layout, and rewinds the scroll position when everything fits.
        /// </summary>
        void SetContentWidth(int seatCount)
        {
            if (m_ContentPanel == null)
                return;

            // Collapse the horizontal anchors to the left edge so sizeDelta.x
            // is the absolute content width (with stretched anchors it would
            // only be extra width on top of the viewport's).
            m_ContentPanel.anchorMin = new Vector2(0f, m_ContentPanel.anchorMin.y);
            m_ContentPanel.anchorMax = new Vector2(0f, m_ContentPanel.anchorMax.y);

            float width = m_ButtonPitch * seatCount;
            m_ContentPanel.sizeDelta = new Vector2(width, m_ContentPanel.sizeDelta.y);

            if (m_ScrollRect != null && m_ScrollRect.viewport != null &&
                width <= m_ScrollRect.viewport.rect.width)
            {
                m_ScrollRect.horizontalNormalizedPosition = 0f;
            }
        }
    }
}
