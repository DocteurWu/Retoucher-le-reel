// =====================================================================================
//  VRHandGesturesTests.cs — TESTS EDITMODE DES CALCULS DE GESTES
//
//  RÔLE
//    Vérifie sans casque les calculs PURS de VRHandGestures (pincement, flexion,
//    hystérésis de la saisie, signe du pistolet). Permet de modifier un seuil sans
//    devoir tout revalider en casque.
//
//  UTILISATION
//    Window > General > Test Runner > EditMode > Run All.
//    Dossier « Editor » : compilé dans Assembly-CSharp-Editor, qui référence déjà
//    NUnit via com.unity.test-framework (aucun asmdef nécessaire).
// =====================================================================================
using NUnit.Framework;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class VRHandGesturesTests
    {
        // --- Pincement -----------------------------------------------------------

        [Test]
        public void Pincement_DoigtsColles_VautUn()
        {
            Assert.AreEqual(1f, VRHandGestures.PinchFromDistance(0.010f), 1e-4f);
        }

        [Test]
        public void Pincement_DoigtsEcartes_VautZero()
        {
            Assert.AreEqual(0f, VRHandGestures.PinchFromDistance(0.080f), 1e-4f);
        }

        [Test]
        public void Pincement_DecroitAvecLaDistance()
        {
            float proche = VRHandGestures.PinchFromDistance(0.020f);
            float loin = VRHandGestures.PinchFromDistance(0.040f);
            Assert.Greater(proche, loin);
        }

        // --- Flexion -------------------------------------------------------------

        [Test]
        public void Flexion_DoigtTendu_ProcheDeZero()
        {
            // Ratio paume→bout / paume→phalange ≈ 2,8 : doigt tendu.
            Assert.Less(VRHandGestures.CurlFromDistances(0.084f, 0.030f), 0.05f);
        }

        [Test]
        public void Flexion_PoingFerme_ProcheDeUn()
        {
            // Ratio ≈ 0,9 : bout du doigt replié dans la paume.
            Assert.Greater(VRHandGestures.CurlFromDistances(0.027f, 0.030f), 0.95f);
        }

        [Test]
        public void Flexion_ArticulationDegeneree_VautZero()
        {
            Assert.AreEqual(0f, VRHandGestures.CurlFromDistances(0.05f, 0.001f));
        }

        [Test]
        public void Flexion_IndependanteDeLaTailleDeMain()
        {
            float petite = VRHandGestures.CurlFromDistances(0.040f, 0.025f);
            float grande = VRHandGestures.CurlFromDistances(0.064f, 0.040f);
            Assert.AreEqual(petite, grande, 1e-4f);
        }

        // --- Saisie (hystérésis) ---------------------------------------------------

        [Test]
        public void Saisie_MainOuverte_Refusee()
        {
            Assert.IsFalse(VRHandGestures.IsGrabFromValues(0.1f, 0.1f, wasActive: false));
        }

        [Test]
        public void Saisie_PoingFerme_Acceptee()
        {
            Assert.IsTrue(VRHandGestures.IsGrabFromValues(0.9f, 0f, wasActive: false));
        }

        [Test]
        public void Saisie_Pincement_Accepte()
        {
            Assert.IsTrue(VRHandGestures.IsGrabFromValues(0f, 0.95f, wasActive: false));
        }

        [Test]
        public void Saisie_ZoneIntermediaire_DependDeLEtatPrecedent()
        {
            // Entre le seuil de relâchement et celui d'appui : pas de nouvelle saisie,
            // mais une saisie en cours est conservée (pas de lâcher au moindre tremblement).
            Assert.IsFalse(VRHandGestures.IsGrabFromValues(0.55f, 0f, wasActive: false));
            Assert.IsTrue(VRHandGestures.IsGrabFromValues(0.55f, 0f, wasActive: true));
        }

        // --- Signe du pistolet -------------------------------------------------------

        [Test]
        public void Pistolet_IndexTenduAutresReplies_Detecte()
        {
            Assert.IsTrue(VRHandGestures.IsFingerGunPose(0.1f, 0.8f, 0.8f));
        }

        [Test]
        public void Pistolet_MainOuverte_NonDetecte()
        {
            Assert.IsFalse(VRHandGestures.IsFingerGunPose(0.1f, 0.1f, 0.1f));
        }

        [Test]
        public void Pistolet_Poing_NonDetecte()
        {
            Assert.IsFalse(VRHandGestures.IsFingerGunPose(0.9f, 0.9f, 0.9f));
        }

        // --- Microgeste (téléportation façon Meta) ------------------------------------

        [Test]
        public void Microgeste_Poing_Detecte()
        {
            Assert.IsTrue(VRHandGestures.IsMicrogesturePose(0.6f, 0.8f, 0.8f));
        }

        [Test]
        public void Microgeste_PistoletOuMainOuverte_NonDetecte()
        {
            Assert.IsFalse(VRHandGestures.IsMicrogesturePose(0.1f, 0.8f, 0.8f));
            Assert.IsFalse(VRHandGestures.IsMicrogesturePose(0.1f, 0.1f, 0.1f));
        }

        [Test]
        public void Microgeste_PouceSurIndex_AppuiPuisRelachement()
        {
            Assert.IsTrue(VRHandGestures.IsThumbTapPressed(0.015f, wasPressed: false));
            Assert.IsFalse(VRHandGestures.IsThumbTapPressed(0.050f, wasPressed: false));
        }

        [Test]
        public void Microgeste_Hysteresis_EntreLesSeuils()
        {
            // 2,8 cm : ne déclenche pas un appui, mais ne relâche pas un appui en cours.
            Assert.IsFalse(VRHandGestures.IsThumbTapPressed(0.028f, wasPressed: false));
            Assert.IsTrue(VRHandGestures.IsThumbTapPressed(0.028f, wasPressed: true));
        }

        [Test]
        public void Glissement_SensSelonLeDeplacementLateral()
        {
            Assert.AreEqual(1, VRThumbMicrogestures.ClassifySwipe(0.02f, 0.015f));
            Assert.AreEqual(-1, VRThumbMicrogestures.ClassifySwipe(-0.02f, 0.015f));
            Assert.AreEqual(0, VRThumbMicrogestures.ClassifySwipe(0.01f, 0.015f));
        }

        [Test]
        public void Tap_PasApresUnGlissementNiUnAppuiLong()
        {
            Assert.IsTrue(VRThumbMicrogestures.IsTapRelease(swiped: false, pressDuration: 0.2f, maxDuration: 0.6f));
            Assert.IsFalse(VRThumbMicrogestures.IsTapRelease(swiped: true, pressDuration: 0.2f, maxDuration: 0.6f));
            Assert.IsFalse(VRThumbMicrogestures.IsTapRelease(swiped: false, pressDuration: 1.5f, maxDuration: 0.6f));
        }

        [Test]
        public void DistanceAuSegment_ProjeteSurLeSegment()
        {
            var a = UnityEngine.Vector3.zero;
            var b = new UnityEngine.Vector3(1f, 0f, 0f);
            Assert.AreEqual(0.5f, VRHandGestures.DistanceToSegment(new UnityEngine.Vector3(0.5f, 0.5f, 0f), a, b), 1e-4f);
            Assert.AreEqual(1f, VRHandGestures.DistanceToSegment(new UnityEngine.Vector3(2f, 0f, 0f), a, b), 1e-4f);
        }
    }
}
