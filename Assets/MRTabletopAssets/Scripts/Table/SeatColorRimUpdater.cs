namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Pushes the per-seat palette, active-seat highlight, and shape sides to
    /// the table rim material via a single MaterialPropertyBlock. Event-driven
    /// only — no per-frame updates (the shader animates from _Time).
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class SeatColorRimUpdater : MonoBehaviour
    {
        static readonly int[] s_PlayerColorProperties =
        {
            Shader.PropertyToID("_PlayerColor1"), Shader.PropertyToID("_PlayerColor2"),
            Shader.PropertyToID("_PlayerColor3"), Shader.PropertyToID("_PlayerColor4"),
            Shader.PropertyToID("_PlayerColor5"), Shader.PropertyToID("_PlayerColor6"),
            Shader.PropertyToID("_PlayerColor7"), Shader.PropertyToID("_PlayerColor8"),
        };
        static readonly int s_ActivePlayerIndex = Shader.PropertyToID("_ActivePlayerIndex");
        static readonly int s_GlowColor = Shader.PropertyToID("_ActivePlayerGlowColor");
        static readonly int s_GlowIntensity = Shader.PropertyToID("_ActivePlayerGlowIntensity");
        static readonly int s_ShapeSides = Shader.PropertyToID("_ShapeSides");
        static readonly int s_HighlightMode = Shader.PropertyToID("_HighlightMode");
        static readonly int s_PatternIntensity = Shader.PropertyToID("_PatternIntensity");
        static readonly int s_AnimationSpeed = Shader.PropertyToID("_AnimationSpeed");
        static readonly int s_BrightnessPulseIntensity = Shader.PropertyToID("_BrightnessPulseIntensity");

        Renderer m_Renderer;
        MaterialPropertyBlock m_PropertyBlock;
        int m_ShapeSideCount = 4;

        void Awake()
        {
            m_Renderer = GetComponent<Renderer>();
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnSeatColorChanged += HandleSeatColorChanged;
                PlayerColorManager.Instance.OnActiveSeatChanged += HandleActiveSeatChanged;
                PlayerColorManager.Instance.OnColorPaletteChanged += HandleColorPaletteChanged;
            }
        }

        void OnDisable()
        {
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnSeatColorChanged -= HandleSeatColorChanged;
                PlayerColorManager.Instance.OnActiveSeatChanged -= HandleActiveSeatChanged;
                PlayerColorManager.Instance.OnColorPaletteChanged -= HandleColorPaletteChanged;
            }
        }

        void Start()
        {
            UpdateShaderProperties();
        }

        /// <summary>Sets the rim's segment count (the seat count) and refreshes.</summary>
        public void UpdatePlayerCount(int playerCount)
        {
            m_ShapeSideCount = Mathf.Clamp(playerCount, 2, 8);
            UpdateShaderProperties();
        }

        public void UpdateShaderProperties()
        {
            var colorManager = PlayerColorManager.Instance;
            if (colorManager == null || m_Renderer == null)
                return;

            m_Renderer.GetPropertyBlock(m_PropertyBlock);

            // NOTE: colors are pushed in physical seat order because the
            // current forked shader still remaps segments internally. The
            // switch to SeatGeometry.AngularOrder happens together with the
            // VirtualSurfaceShader integration (SeatColorRim.hlsl), which
            // removes the shader-side remap tables.
            var colors = colorManager.GetAllPlayerColors();
            for (int i = 0; i < colors.Length && i < s_PlayerColorProperties.Length; i++)
                m_PropertyBlock.SetColor(s_PlayerColorProperties[i], colors[i]);

            m_PropertyBlock.SetFloat(s_ActivePlayerIndex, colorManager.ActiveSeat);
            m_PropertyBlock.SetColor(s_GlowColor, colorManager.ActiveGlowColor);
            m_PropertyBlock.SetFloat(s_GlowIntensity, colorManager.GlowIntensity);
            m_PropertyBlock.SetFloat(s_ShapeSides, m_ShapeSideCount);
            m_PropertyBlock.SetFloat(s_HighlightMode, (float)colorManager.HighlightMode);
            m_PropertyBlock.SetFloat(s_PatternIntensity, colorManager.PatternIntensity);
            m_PropertyBlock.SetFloat(s_AnimationSpeed, colorManager.AnimationSpeed);
            m_PropertyBlock.SetFloat(s_BrightnessPulseIntensity, colorManager.BrightnessPulseIntensity);

            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        void HandleSeatColorChanged(int seatIndex, Color newColor) => UpdateShaderProperties();
        void HandleActiveSeatChanged(int activeSeat) => UpdateShaderProperties();
        void HandleColorPaletteChanged() => UpdateShaderProperties();
    }
}
