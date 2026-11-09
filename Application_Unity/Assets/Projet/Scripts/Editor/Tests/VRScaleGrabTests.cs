// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRScaleGrabTests.cs — TESTS EDITMODE DU SON DE REDIMENSIONNEMENT
//
//  RÔLE
//    Vérifie le sens de la note : petit = aigu, grand = grave, taille d'origine = note de base.
// =====================================================================================
using NUnit.Framework;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class VRScaleGrabTests
    {
        [Test]
        public void Hauteur_TailleOrigine_NoteDeBase()
        {
            Assert.AreEqual(1f, VRScaleGrab.HauteurPourTaille(1f, 0.5f), 1e-4f);
        }

        [Test]
        public void Hauteur_ObjetGrand_PlusGrave_ObjetPetit_PlusAigu()
        {
            Assert.AreEqual(0.5f, VRScaleGrab.HauteurPourTaille(4f, 0.5f), 1e-4f);
            Assert.AreEqual(2f, VRScaleGrab.HauteurPourTaille(0.25f, 0.5f), 1e-4f);
        }

        [Test]
        public void Hauteur_RestePlafonnee()
        {
            Assert.LessOrEqual(VRScaleGrab.HauteurPourTaille(0.0001f, 1.5f), 4f);
            Assert.GreaterOrEqual(VRScaleGrab.HauteurPourTaille(1000f, 1.5f), 0.25f);
        }
    }
}
