using NUnit.Framework;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.Tests.EditMode
{
    public class SeatGeometryTests
    {
        [Test]
        public void AngularOrder_FourSeatPreset_MatchesTemplateClockwiseOrder()
        {
            // Template layout: seat 0 at 0°, 1 at 180°, 2 at 270°, 3 at 90°.
            var order = SeatGeometry.AngularOrder(new[] { 0f, 180f, 270f, 90f });

            Assert.That(order, Is.EqualTo(new[] { 0, 3, 1, 2 }));
        }

        [Test]
        public void AngularOrder_EightSeatPreset_InterleavesCornerSeats()
        {
            var order = SeatGeometry.AngularOrder(new[] { 0f, 180f, 270f, 90f, 45f, 135f, 225f, 315f });

            Assert.That(order, Is.EqualTo(new[] { 0, 4, 3, 5, 1, 6, 2, 7 }));
        }

        [Test]
        public void AngularOrder_RegularPolygon_IsIdentity()
        {
            var order = SeatGeometry.AngularOrder(new[] { 0f, 72f, 144f, 216f, 288f });

            Assert.That(order, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        }

        [Test]
        public void AngularOrder_NormalizesYawsOutsideRange()
        {
            var order = SeatGeometry.AngularOrder(new[] { 360f, -90f, 180f });

            // 360 → 0, -90 → 270.
            Assert.That(order, Is.EqualTo(new[] { 0, 2, 1 }));
        }

        [Test]
        public void SeatLocalPosition_YawZero_IsBehindCenterAtRadius()
        {
            var position = SeatGeometry.SeatLocalPosition(0f, 0.75f);

            Assert.That(position.x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(position.y, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(position.z, Is.EqualTo(-0.75f).Within(1e-5f));
        }

        [Test]
        public void SeatLocalPosition_Yaw90_IsOnPositiveXSideFacingCenter()
        {
            var position = SeatGeometry.SeatLocalPosition(90f, 1f);

            Assert.That(position.x, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(position.z, Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
