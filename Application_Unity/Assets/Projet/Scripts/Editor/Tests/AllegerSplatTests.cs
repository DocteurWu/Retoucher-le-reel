// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  AllegerSplatTests.cs — TESTS EDITMODE DE L'ALLÈGEMENT DES SPLATS
//
//  RÔLE
//    Vérifie AllegerSplat.Importance, qui décide quels splats retirer :
//      • un splat long et fin (cordon, tige) compte comme une ligne visible, pas comme un point ;
//      • l'opacité pondère le poids ; un splat sous le pixel ne vaut pas moins qu'un pixel.
// =====================================================================================
using NUnit.Framework;
using UnityEngine;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class AllegerSplatTests
    {
        private const float Pixel = 0.01f;

        [Test]
        public void Importance_SplatLongEtFin_PlusQuUnPoint()
        {
            float aiguille = AllegerSplat.Importance(1f, new Vector3(0.5f, 0.0001f, 0.0001f), Pixel);
            float point = AllegerSplat.Importance(1f, new Vector3(0.0001f, 0.0001f, 0.0001f), Pixel);
            Assert.Greater(aiguille, point * 10f);
        }

        [Test]
        public void Importance_SousLePixel_PlancherDUnPixel()
        {
            float minuscule = AllegerSplat.Importance(1f, new Vector3(1e-6f, 1e-6f, 1e-6f), Pixel);
            Assert.AreEqual(Pixel * Pixel, minuscule, 1e-9f);
        }

        [Test]
        public void Importance_ProportionnelleALOpacite()
        {
            Vector3 s = new Vector3(0.2f, 0.1f, 0.05f);
            Assert.AreEqual(AllegerSplat.Importance(1f, s, Pixel) * 0.25f, AllegerSplat.Importance(0.25f, s, Pixel), 1e-6f);
        }

        [Test]
        public void Importance_IndependanteDeLOrdreDesAxes()
        {
            float a = AllegerSplat.Importance(0.5f, new Vector3(0.3f, 0.02f, 0.1f), Pixel);
            float b = AllegerSplat.Importance(0.5f, new Vector3(0.02f, 0.1f, 0.3f), Pixel);
            Assert.AreEqual(a, b, 1e-7f);
            Assert.AreEqual(0.5f * 0.3f * 0.1f, a, 1e-7f); // deux plus grands axes
        }
    }
}
