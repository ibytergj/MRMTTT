using NUnit.Framework;
using UnityEngine;
using UnityEngine.XR.Templates.MRTTabletopAssets;

namespace MRTTT.Tests.EditMode
{
    public class ContrastColorTests
    {
        [Test]
        public void For_Black_ReturnsWhite()
        {
            Assert.That(ContrastColor.For(Color.black), Is.EqualTo(Color.white));
        }

        [Test]
        public void For_White_ReturnsBlack()
        {
            Assert.That(ContrastColor.For(Color.white), Is.EqualTo(Color.black));
        }

        [Test]
        public void For_LightPastel_ReturnsBlack()
        {
            // Template seat 4 green — light.
            var green = new Color(0.32156864f, 0.94117653f, 0.53333336f);

            Assert.That(ContrastColor.For(green), Is.EqualTo(Color.black));
        }

        [Test]
        public void For_DarkSaturatedColor_ReturnsWhite()
        {
            var navy = new Color(0.05f, 0.1f, 0.4f);

            Assert.That(ContrastColor.For(navy), Is.EqualTo(Color.white));
        }

        [Test]
        public void For_IgnoresAlpha()
        {
            var transparentWhite = new Color(1f, 1f, 1f, 0f);

            Assert.That(ContrastColor.For(transparentWhite), Is.EqualTo(Color.black));
        }
    }
}
