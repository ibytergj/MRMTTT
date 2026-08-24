using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.Tests.EditMode
{
    public class TableLayoutConfigTests
    {
        static TableLayoutConfig Create(int[] supportedCounts, CollapsePolicy policy)
        {
            var config = ScriptableObject.CreateInstance<TableLayoutConfig>();
            var type = typeof(TableLayoutConfig);
            type.GetField("m_SupportedSeatCounts", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, supportedCounts);
            type.GetField("m_CollapsePolicy", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(config, policy);
            return config;
        }

        static void AddPreset(TableLayoutConfig config, SeatLayoutPreset preset)
        {
            var field = typeof(TableLayoutConfig).GetField("m_Presets", BindingFlags.Instance | BindingFlags.NonPublic);
            var presets = (System.Collections.Generic.List<SeatLayoutPreset>)field.GetValue(config);
            presets.Add(preset);
        }

        static int Mask(params int[] seats)
        {
            int mask = 0;
            foreach (var seat in seats)
                mask |= 1 << seat;
            return mask;
        }

        // --- ScaleFor ---

        [Test]
        public void ScaleFor_PresetPin_Wins()
        {
            var config = Create(new[] { 4, 8 }, CollapsePolicy.Never);
            AddPreset(config, new SeatLayoutPreset { seatCount = 8, yaws = null, scale = 2f });

            Assert.That(config.ScaleFor(8), Is.EqualTo(2f));
        }

        [Test]
        public void ScaleFor_NoPreset_UsesConstantSeatSpacingFormula()
        {
            var config = Create(new[] { 3, 4, 5, 6, 7, 8 }, CollapsePolicy.Never);

            Assert.That(config.ScaleFor(4), Is.EqualTo(1f).Within(1e-5f));
            // sin(45°)/sin(30°) = 1.41421
            Assert.That(config.ScaleFor(6), Is.EqualTo(1.41421f).Within(1e-4f));
            // sin(45°)/sin(22.5°) = 1.84776
            Assert.That(config.ScaleFor(8), Is.EqualTo(1.84776f).Within(1e-4f));
        }

        // --- YawsFor ---

        [Test]
        public void YawsFor_Preset_ReturnsAuthoredYaws()
        {
            var config = Create(new[] { 4, 8 }, CollapsePolicy.Never);
            AddPreset(config, new SeatLayoutPreset { seatCount = 4, yaws = new[] { 0f, 180f, 270f, 90f }, scale = 0f });

            Assert.That(config.YawsFor(4), Is.EqualTo(new[] { 0f, 180f, 270f, 90f }));
        }

        [Test]
        public void YawsFor_NoPreset_ReturnsRegularPolygon()
        {
            var config = Create(new[] { 3, 4, 5, 6, 7, 8 }, CollapsePolicy.Never);

            Assert.That(config.YawsFor(5), Is.EqualTo(new[] { 0f, 72f, 144f, 216f, 288f }));
        }

        // --- CountForOccupancy ---

        [Test]
        public void CountForOccupancy_TemplateDefault_RefusesFifthPlayer()
        {
            var config = Create(new[] { 4 }, CollapsePolicy.Never);

            Assert.That(config.CountForOccupancy(4), Is.EqualTo(4));
            Assert.That(config.CountForOccupancy(5), Is.EqualTo(-1));
        }

        [Test]
        public void CountForOccupancy_FourOrEight_JumpsToEight()
        {
            var config = Create(new[] { 4, 8 }, CollapsePolicy.Never);

            Assert.That(config.CountForOccupancy(3), Is.EqualTo(4));
            Assert.That(config.CountForOccupancy(5), Is.EqualTo(8));
            Assert.That(config.CountForOccupancy(9), Is.EqualTo(-1));
        }

        // --- TargetSeatCount: growth ---

        [Test]
        public void TargetSeatCount_TemplateDefault_StaysAtFour()
        {
            var config = Create(new[] { 4 }, CollapsePolicy.Never);

            Assert.That(config.TargetSeatCount(Mask(0, 1, 2, 3), 4), Is.EqualTo(4));
            Assert.That(config.TargetSeatCount(Mask(0), 4), Is.EqualTo(4));
            Assert.That(config.TargetSeatCount(0, 4), Is.EqualTo(4));
        }

        [Test]
        public void TargetSeatCount_GrowsToFitHighestOccupiedSeat()
        {
            var config = Create(new[] { 4, 8 }, CollapsePolicy.Never);

            // A player was just assigned seat 4 (the 5th seat) → 8-seat layout.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 2, 3, 4), 4), Is.EqualTo(8));
        }

        // --- TargetSeatCount: collapse policies (6 players, player 3 leaves) ---

        [Test]
        public void TargetSeatCount_Never_KeepsTableSizeAfterLeave()
        {
            var config = Create(new[] { 3, 4, 5, 6, 7, 8 }, CollapsePolicy.Never);

            // Seats 0-5 occupied minus seat 2 → hole stays, layout stays 6.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 3, 4, 5), 6), Is.EqualTo(6));
            // Even nearly empty, the table never shrinks mid-session.
            Assert.That(config.TargetSeatCount(Mask(0), 8), Is.EqualTo(8));
        }

        [Test]
        public void TargetSeatCount_WhenRemainingFit_ShrinksOnlyWhenNobodyMoves()
        {
            var config = Create(new[] { 3, 4, 5, 6, 7, 8 }, CollapsePolicy.WhenRemainingFit);

            // Player at seat 5 still present → cannot shrink below 6.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 3, 4, 5), 6), Is.EqualTo(6));
            // Highest occupied is seat 3 → shrink to 4 without moving anyone.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 3), 6), Is.EqualTo(4));
        }

        [Test]
        public void TargetSeatCount_Immediate_ShrinksToOccupancy()
        {
            var config = Create(new[] { 3, 4, 5, 6, 7, 8 }, CollapsePolicy.Immediate);

            // 5 players remain → pentagon; the server re-seats seat 5's player first.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 3, 4, 5), 6), Is.EqualTo(5));
            Assert.That(config.TargetSeatCount(Mask(0, 1), 8), Is.EqualTo(3));
        }

        [Test]
        public void TargetSeatCount_Immediate_RespectsSupportedCounts()
        {
            var config = Create(new[] { 4, 8 }, CollapsePolicy.Immediate);

            // 5 players → nothing between 8 and 4 → stay at 8.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 2, 3, 4), 8), Is.EqualTo(8));
            // 4 players, all within the first four seats → back to 4.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 2, 3), 8), Is.EqualTo(4));
            // 4 players but one sits at seat 6 → target is still 4; the server
            // moves that player to the lowest free seat before applying it.
            Assert.That(config.TargetSeatCount(Mask(0, 1, 2, 6), 8), Is.EqualTo(4));
        }
    }
}
