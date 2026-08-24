using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.Tests.EditMode
{
    public class PlayerColorPaletteTests
    {
        static PlayerColorPalette CreateWithColors(Color[] colors)
        {
            var palette = ScriptableObject.CreateInstance<PlayerColorPalette>();
            typeof(PlayerColorPalette).GetField("m_Colors", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(palette, colors);
            return palette;
        }

        [Test]
        public void NormalizedColors_ReturnsExactlyEightEntries()
        {
            var palette = ScriptableObject.CreateInstance<PlayerColorPalette>();

            Assert.That(palette.NormalizedColors(), Has.Length.EqualTo(PlayerColorPalette.k_SeatCount));
        }

        [Test]
        public void NormalizedColors_ForcesOpaqueAlpha()
        {
            var palette = CreateWithColors(new[] { new Color(1f, 0f, 0f, 0.25f) });

            Assert.That(palette.NormalizedColors()[0].a, Is.EqualTo(1f));
        }

        [Test]
        public void NormalizedColors_FillsMissingEntriesFromFallback()
        {
            var palette = CreateWithColors(new[] { Color.red });
            var colors = palette.NormalizedColors();

            // Seat 0 keeps the authored color; the rest are filled, distinct, opaque.
            Assert.That(colors[0], Is.EqualTo(Color.red));
            for (int i = 1; i < colors.Length; i++)
                Assert.That(colors[i].a, Is.EqualTo(1f));
        }

        [Test]
        public void NormalizedColors_DefaultPalette_HasDistinctColors()
        {
            var palette = ScriptableObject.CreateInstance<PlayerColorPalette>();
            var colors = palette.NormalizedColors();

            for (int i = 0; i < colors.Length; i++)
            {
                for (int j = i + 1; j < colors.Length; j++)
                    Assert.That(colors[i], Is.Not.EqualTo(colors[j]), $"Seats {i} and {j} share a color");
            }
        }

        [Test]
        public void GetColor_ClampsOutOfRangeSeatIndices()
        {
            var palette = ScriptableObject.CreateInstance<PlayerColorPalette>();

            Assert.That(palette.GetColor(-1), Is.EqualTo(palette.GetColor(0)));
            Assert.That(palette.GetColor(99), Is.EqualTo(palette.GetColor(PlayerColorPalette.k_SeatCount - 1)));
        }

        [Test]
        public void DefaultPalette_KeepsTemplateSeatColors()
        {
            var palette = ScriptableObject.CreateInstance<PlayerColorPalette>();
            var colors = palette.NormalizedColors();

            // Seats 1-4 must match the template's SeatButton m_SeatColors so
            // 4-player visuals stay identical to stock.
            Assert.That(colors[0], Is.EqualTo(new Color(0.20784315f, 0.61960787f, 1f)));
            Assert.That(colors[1], Is.EqualTo(new Color(1f, 0.74509805f, 0.08627451f)));
            Assert.That(colors[2], Is.EqualTo(new Color(1f, 0.427451f, 0.45882356f)));
            Assert.That(colors[3], Is.EqualTo(new Color(0.32156864f, 0.94117653f, 0.53333336f)));
        }
    }
}
