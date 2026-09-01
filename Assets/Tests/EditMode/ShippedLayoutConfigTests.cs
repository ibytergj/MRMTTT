using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.Tests.EditMode
{
    /// <summary>
    /// Coherence guards for the TableLayoutConfig assets that ship in the
    /// project (see Assets/Editor/CreateTableLayoutAssets.cs). These catch
    /// authoring drift - a preset for an unsupported seat count, a yaw list
    /// whose length does not match its count, unsorted supported counts -
    /// without needing a live multiplayer session.
    /// </summary>
    public class ShippedLayoutConfigTests
    {
        const string k_Dir = "Assets/MRTabletopAssets/Settings";

        static TableLayoutConfig Load(string name)
        {
            var config = AssetDatabase.LoadAssetAtPath<TableLayoutConfig>($"{k_Dir}/{name}.asset");
            Assert.IsNotNull(config, $"{name}.asset is missing - regenerate with CreateTableLayoutAssets.Create");
            return config;
        }

        static IEnumerable<string> AllConfigNames()
        {
            yield return "TableLayout_Default";
            yield return "TableLayout_4or8";
            yield return "TableLayout_Dynamic";
            yield return "TableLayout_6Max";
            yield return "TableLayout_Immediate";
            yield return "TableLayout_Odd";
        }

        [Test]
        public void EveryConfig_SupportedCounts_AreSortedAndSane([ValueSource(nameof(AllConfigNames))] string name)
        {
            var counts = Load(name).supportedSeatCounts;

            Assert.IsNotEmpty((System.Collections.ICollection)counts, $"{name} supports no seat counts");
            CollectionAssert.AllItemsAreUnique(counts, $"{name} repeats a seat count");
            CollectionAssert.IsOrdered(counts, $"{name} supported counts must ascend (minimumSeatCount takes [0])");
            Assert.GreaterOrEqual(counts[0], 3, $"{name} allows a table below 3 seats");
            Assert.LessOrEqual(counts[counts.Count - 1], 8, $"{name} exceeds the 8 authored seats on TableTop");
        }

        [Test]
        public void EveryConfig_PresetsMatchSupportedCounts([ValueSource(nameof(AllConfigNames))] string name)
        {
            var config = Load(name);
            var supported = config.supportedSeatCounts;

            foreach (var count in supported)
            {
                var yaws = config.YawsFor(count);
                Assert.AreEqual(count, yaws.Length,
                    $"{name}: YawsFor({count}) returned {yaws.Length} yaws");
                Assert.Greater(config.ScaleFor(count), 0f,
                    $"{name}: ScaleFor({count}) must be positive");
            }
        }

        [Test]
        public void EveryConfig_GrowthLadderIsReachable([ValueSource(nameof(AllConfigNames))] string name)
        {
            var config = Load(name);
            var supported = config.supportedSeatCounts;

            // Every supported count must be the answer for some occupancy,
            // otherwise it is dead configuration the table can never adopt.
            for (int i = 0; i < supported.Count; i++)
            {
                int occupancy = i == 0 ? 1 : supported[i - 1] + 1;
                Assert.AreEqual(supported[i], config.CountForOccupancy(occupancy),
                    $"{name}: {occupancy} players should seat at a {supported[i]}-seat table");
            }

            Assert.AreEqual(-1, config.CountForOccupancy(supported[supported.Count - 1] + 1),
                $"{name}: overflowing the largest table must refuse a seat (-1)");
        }

        [Test]
        public void Default_IsStockParity()
        {
            var config = Load("TableLayout_Default");

            CollectionAssert.AreEqual(new[] { 4 }, config.supportedSeatCounts);
            Assert.AreEqual(4, config.minimumSeatCount);
            Assert.AreEqual(1f, config.ScaleFor(4), 1e-4f, "stock table must not be scaled");
            CollectionAssert.AreEqual(new[] { 0f, 180f, 270f, 90f }, config.YawsFor(4),
                "stock seat yaws must match the template");
        }

        [Test]
        public void FourOrEight_PinsTheTunedScale()
        {
            var config = Load("TableLayout_4or8");

            CollectionAssert.AreEqual(new[] { 4, 8 }, config.supportedSeatCounts);
            Assert.AreEqual(1f, config.ScaleFor(4), 1e-4f);
            Assert.AreEqual(1.5f, config.ScaleFor(8), 1e-4f,
                "8-seat scale is pinned to 1.5 by design; the derived value would be ~1.85");

            // Seats 0-3 keep their stock yaws so nobody moves on expansion.
            var eight = config.YawsFor(8);
            CollectionAssert.AreEqual(new[] { 0f, 180f, 270f, 90f }, eight.Take(4).ToArray(),
                "expansion must not move the original four players");
        }

        [Test]
        public void Dynamic_IsFullyDerived()
        {
            var config = Load("TableLayout_Dynamic");

            for (int count = 3; count <= 8; count++)
            {
                var yaws = config.YawsFor(count);
                Assert.AreEqual(0f, yaws[0], 1e-4f, $"seat 0 should face yaw 0 at {count} seats");
                for (int i = 0; i < count; i++)
                    Assert.AreEqual(i * 360f / count, yaws[i], 1e-3f,
                        $"derived yaw {i} of {count} should be a regular polygon vertex");
            }

            // Derived scale must rise monotonically with seat count.
            for (int count = 4; count <= 8; count++)
                Assert.Greater(config.ScaleFor(count), config.ScaleFor(count - 1),
                    $"scale should grow from {count - 1} to {count} seats");
        }

        [Test]
        public void Odd_StartsAtThreeSeats()
        {
            var config = Load("TableLayout_Odd");

            CollectionAssert.AreEqual(new[] { 3, 5, 7 }, config.supportedSeatCounts);
            Assert.AreEqual(3, config.minimumSeatCount,
                "a fresh session must open as a triangle, not a square");
            Assert.AreEqual(3, config.YawsFor(3).Length);
        }

        [Test]
        public void Immediate_ShrinksOnEveryLeave()
        {
            var config = Load("TableLayout_Immediate");

            Assert.AreEqual(CollapsePolicy.Immediate, config.collapsePolicy);

            // Six seats, three players in seats 0/1/2: Immediate should pull
            // the table back to the smallest count that fits them.
            int occupied = (1 << 0) | (1 << 1) | (1 << 2);
            Assert.AreEqual(4, config.TargetSeatCount(occupied, 6),
                "Immediate should collapse 6 -> 4 once only three players remain");
        }

        [Test]
        public void SixMax_RefusesASeventhPlayer()
        {
            var config = Load("TableLayout_6Max");

            CollectionAssert.AreEqual(new[] { 4, 6 }, config.supportedSeatCounts);
            Assert.AreEqual(6, config.CountForOccupancy(5), "a 5th player should grow the table to 6");
            Assert.AreEqual(-1, config.CountForOccupancy(7), "a 7th player must be refused");
        }
    }
}
