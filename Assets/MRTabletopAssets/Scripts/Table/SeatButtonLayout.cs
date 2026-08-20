namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Ensures the content panel is sized correctly for horizontal scrolling of seat buttons.
    /// </summary>
    public class SeatButtonLayout : MonoBehaviour
    {
        [SerializeField] RectTransform m_ContentPanel;
        [SerializeField] TableTopSeatButton[] m_SeatButtons;
        [SerializeField] float m_TotalContentWidth = 800f; // Set this to accommodate all buttons
        [SerializeField] float m_ButtonSpacing = 10f; // Spacing between buttons

        void Start()
        {
            EnsureButtonsAreVisible();
            SetContentWidth();
        }

        /// <summary>
        /// Makes sure all seat buttons are active and visible.
        /// </summary>
        void EnsureButtonsAreVisible()
        {
            // Make sure all buttons are active
            foreach (var button in m_SeatButtons)
            {
                if (button != null)
                    button.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Sets the content panel width to accommodate all buttons.
        /// </summary>
        void SetContentWidth()
        {
            // Set content width to accommodate all buttons
            if (m_ContentPanel != null)
            {
                // If m_TotalContentWidth is set to 0, calculate it based on buttons
                if (m_TotalContentWidth <= 0 && m_SeatButtons.Length > 0)
                {
                    float buttonWidth = 0;

                    // Get the width of the first button
                    if (m_SeatButtons[0] != null)
                    {
                        RectTransform buttonRect = m_SeatButtons[0].GetComponent<RectTransform>();
                        if (buttonRect != null)
                        {
                            buttonWidth = buttonRect.rect.width;
                        }
                    }

                    // Calculate total width needed
                    if (buttonWidth > 0)
                    {
                        m_TotalContentWidth = (buttonWidth * m_SeatButtons.Length) +
                                             (m_ButtonSpacing * (m_SeatButtons.Length - 1));
                    }
                    else
                    {
                        // Default width if button width couldn't be determined
                        m_TotalContentWidth = 800f;
                    }
                }

                // Set the content panel width
                m_ContentPanel.sizeDelta = new Vector2(m_TotalContentWidth, m_ContentPanel.sizeDelta.y);
            }
        }
    }
}
