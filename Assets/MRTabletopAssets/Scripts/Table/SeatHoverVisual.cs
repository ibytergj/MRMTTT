using System.Collections.Generic;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// One seat's hover ring on the table. Colors itself from the networked
    /// per-seat palette with a contrast-adaptive outline (a black seat gets a
    /// white edge and vice versa), and repositions to its seat's vertex when
    /// the table layout changes. Replaces the eight baked ring materials with
    /// one material driven by a MaterialPropertyBlock.
    /// </summary>
    public class SeatHoverVisual : MonoBehaviour
    {
        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_Color = Shader.PropertyToID("_Color");
        static readonly List<SeatHoverVisual> s_Instances = new List<SeatHoverVisual>();

        [SerializeField]
        int m_SeatIndex;

        [SerializeField]
        [Tooltip("Ring distance from the table center, as a fraction of the seat distance.")]
        float m_SeatDistanceRatio = 0.9333f;

        [SerializeField]
        Renderer m_Renderer;

        Renderer m_OutlineRenderer;
        MaterialPropertyBlock m_PropertyBlock;

        public int seatIndex => m_SeatIndex;

        /// <summary>The ring registered for <paramref name="seatIndex"/>, if any.</summary>
        public static SeatHoverVisual ForSeat(int seatIndex)
        {
            foreach (var instance in s_Instances)
            {
                if (instance.m_SeatIndex == seatIndex)
                    return instance;
            }

            // Rings start inactive, so Awake may not have registered them yet.
            foreach (var instance in FindObjectsByType<SeatHoverVisual>(FindObjectsInactive.Include))
            {
                if (instance.m_SeatIndex == seatIndex)
                    return instance;
            }
            return null;
        }

        public void SetSeat(int seatIndex)
        {
            m_SeatIndex = seatIndex;
            Refresh();
        }

        void Awake()
        {
            if (!s_Instances.Contains(this))
                s_Instances.Add(this);
            if (m_Renderer == null)
                m_Renderer = GetComponentInChildren<Renderer>(true);
            m_PropertyBlock = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            s_Instances.Remove(this);
        }

        void OnEnable()
        {
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnSeatColorChanged += HandleSeatColorChanged;
                PlayerColorManager.Instance.OnColorPaletteChanged += Refresh;
            }
            Reposition();
            Refresh();
        }

        void OnDisable()
        {
            if (PlayerColorManager.Instance != null)
            {
                PlayerColorManager.Instance.OnSeatColorChanged -= HandleSeatColorChanged;
                PlayerColorManager.Instance.OnColorPaletteChanged -= Refresh;
            }
        }

        void HandleSeatColorChanged(int changedSeat, Color newColor)
        {
            if (changedSeat == m_SeatIndex)
                Refresh();
        }

        void Refresh()
        {
            if (m_Renderer == null)
                return;

            var color = PlayerColorManager.Instance != null
                ? PlayerColorManager.Instance.GetPlayerColor(m_SeatIndex)
                : PlayerColorPalette.FallbackColors()[Mathf.Clamp(m_SeatIndex, 0, PlayerColorPalette.k_SeatCount - 1)];

            ApplyColor(m_Renderer, color);
            EnsureOutline();
            ApplyColor(m_OutlineRenderer, ContrastColor.For(color));
        }

        void ApplyColor(Renderer target, Color color)
        {
            if (target == null)
                return;

            target.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(k_BaseColor, color);
            m_PropertyBlock.SetColor(k_Color, color);
            target.SetPropertyBlock(m_PropertyBlock);
        }

        /// <summary>
        /// The outline is a slightly larger, slightly flatter clone of the
        /// ring mesh in the contrast color, so black and white rings stay
        /// visible against the table.
        /// </summary>
        void EnsureOutline()
        {
            if (m_OutlineRenderer != null)
                return;

            var source = m_Renderer.transform;
            var outline = Instantiate(source.gameObject, source.parent);
            outline.name = "Outline";
            foreach (Transform child in outline.transform)
                Destroy(child.gameObject);
            if (outline.TryGetComponent(out SeatHoverVisual duplicate))
                Destroy(duplicate);

            outline.transform.localPosition = source.localPosition;
            outline.transform.localRotation = source.localRotation;
            var baseScale = source.localScale;
            outline.transform.localScale = new Vector3(baseScale.x * 1.12f, baseScale.y * 0.75f, baseScale.z * 1.12f);
            m_OutlineRenderer = outline.GetComponent<Renderer>();
        }

        void Reposition()
        {
            var tableTop = FindAnyObjectByType<TableTop>(FindObjectsInactive.Include);
            if (tableTop == null || m_SeatIndex < 0 || m_SeatIndex >= tableTop.seats.Length)
                return;

            var seat = tableTop.seats[m_SeatIndex].seatTransform;
            if (seat == null)
                return;

            var seatLocal = seat.localPosition;
            transform.localPosition = new Vector3(seatLocal.x * m_SeatDistanceRatio, transform.localPosition.y, seatLocal.z * m_SeatDistanceRatio);
        }
    }
}
