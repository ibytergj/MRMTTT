namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Black-or-white contrast selection, used for rim-segment and seat-hover
    /// outlines and glow so dark colors (black) and light colors (white) stay
    /// visible against each other.
    /// </summary>
    public static class ContrastColor
    {
        /// <summary>Rec. 709 luminance threshold above which black is the contrast color.</summary>
        const float k_LuminanceThreshold = 0.5f;

        /// <summary>Returns black for light colors and white for dark colors.</summary>
        public static Color For(Color color)
        {
            return Luminance(color) > k_LuminanceThreshold ? Color.black : Color.white;
        }

        /// <summary>Rec. 709 relative luminance of <paramref name="color"/> (alpha ignored).</summary>
        public static float Luminance(Color color)
        {
            return 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
        }
    }
}
