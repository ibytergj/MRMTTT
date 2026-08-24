using System;

namespace UnityEngine.XR.Templates.MRTTabletopAssets
{
    /// <summary>
    /// Pure seat-geometry helpers. Seat positions are always derived from the
    /// seat's yaw; angular order is derived from the seat transforms, so there
    /// are no hand-written remap tables.
    /// </summary>
    public static class SeatGeometry
    {
        /// <summary>
        /// Returns seat indices in clockwise angular order (ascending yaw,
        /// starting at 0°). Ties keep ascending index order. Yaws outside
        /// [0, 360) are normalized. For the template's 4-seat layout
        /// (0/180/270/90) this yields 0, 3, 1, 2 — the template's clockwise
        /// player order.
        /// </summary>
        public static int[] AngularOrder(float[] yaws)
        {
            if (yaws == null)
                throw new ArgumentNullException(nameof(yaws));

            var order = new int[yaws.Length];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;

            // Stable sort by normalized yaw.
            Array.Sort(order, (a, b) =>
            {
                int byYaw = NormalizeYaw(yaws[a]).CompareTo(NormalizeYaw(yaws[b]));
                return byYaw != 0 ? byYaw : a.CompareTo(b);
            });
            return order;
        }

        /// <summary>Angular order read from live seat transforms (local yaw).</summary>
        public static int[] AngularOrder(TableSeat[] seats)
        {
            if (seats == null)
                throw new ArgumentNullException(nameof(seats));

            var yaws = new float[seats.Length];
            for (int i = 0; i < seats.Length; i++)
                yaws[i] = seats[i].seatTransform != null ? seats[i].seatTransform.localEulerAngles.y : 0f;
            return AngularOrder(yaws);
        }

        /// <summary>
        /// A seat's derived local position: facing the table center from
        /// <paramref name="yaw"/> at <paramref name="radius"/> (the template's
        /// -forward × radius rule).
        /// </summary>
        public static Vector3 SeatLocalPosition(float yaw, float radius)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            return -(rotation * Vector3.forward) * radius;
        }

        static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            return yaw < 0f ? yaw + 360f : yaw;
        }
    }
}
