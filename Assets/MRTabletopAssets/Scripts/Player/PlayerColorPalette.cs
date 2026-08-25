using System;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// The per-game seat color palette: one default color per seat. The host
    /// seeds the networked palette from this asset at session start; games
    /// ship their own asset to restyle every seat-colored surface (rim,
    /// avatars, seat buttons, hover visuals) without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerColorPalette", menuName = "MRTTT/Player Color Palette")]
    public class PlayerColorPalette : ScriptableObject
    {
        public const int k_SeatCount = 8;

        static readonly Color[] k_FallbackColors =
        {
            // Template colors for seats 1-4 (SeatButton Variant m_SeatColors).
            new Color(0.20784315f, 0.61960787f, 1f),
            new Color(1f, 0.74509805f, 0.08627451f),
            new Color(1f, 0.427451f, 0.45882356f),
            new Color(0.32156864f, 0.94117653f, 0.53333336f),
            // Distinct additions for seats 5-8, including black and white.
            new Color(0.61960787f, 0.36078432f, 0.9529412f),
            new Color(0.95294124f, 0.40784314f, 0.84313726f),
            Color.black,
            Color.white,
        };

        [SerializeField]
        [Tooltip("Default color per seat index. Normalized to 8 opaque entries.")]
        Color[] m_Colors = (Color[])k_FallbackColors.Clone();

        /// <summary>The built-in default colors, for consumers without a palette asset.</summary>
        public static Color[] FallbackColors() => (Color[])k_FallbackColors.Clone();

        /// <summary>The seat's default color (normalized: opaque, fallback-filled).</summary>
        public Color GetColor(int seatIndex)
        {
            var colors = NormalizedColors();
            return colors[Mathf.Clamp(seatIndex, 0, k_SeatCount - 1)];
        }

        /// <summary>
        /// Exactly <see cref="k_SeatCount"/> opaque colors: authored entries
        /// with alpha forced to 1, missing entries filled from the fallback
        /// palette.
        /// </summary>
        public Color[] NormalizedColors()
        {
            var colors = new Color[k_SeatCount];
            for (int i = 0; i < k_SeatCount; i++)
            {
                var color = m_Colors != null && i < m_Colors.Length ? m_Colors[i] : k_FallbackColors[i];
                color.a = 1f;
                colors[i] = color;
            }

            return colors;
        }

        void OnValidate()
        {
            if (m_Colors == null || m_Colors.Length != k_SeatCount)
            {
                var resized = new Color[k_SeatCount];
                for (int i = 0; i < k_SeatCount; i++)
                    resized[i] = m_Colors != null && i < m_Colors.Length ? m_Colors[i] : k_FallbackColors[i];
                m_Colors = resized;
            }
        }
    }
}
