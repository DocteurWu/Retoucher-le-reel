// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MainMenuTests.cs — TESTS EDITMODE DE LA VALIDATION DES BOUTONS DU MENU
//
//  RÔLE
//    Vérifie la règle du front montant (MainMenu.ValideAuFrontMontant) sur des suites
//    d'images, en particulier pendant le tutoriel :
//      • le « tir » qui lance le tutoriel ne l'arrête pas à l'image suivante ;
//      • une entrée restée enfoncée ne revalide jamais (plus de bouton redéclenché à
//        chaque image) ;
//      • relâcher puis réappuyer en visant le bouton valide une seule fois.
// =====================================================================================
using NUnit.Framework;
using RemodelerLeReel.UI;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class MainMenuTests
    {
        /// <summary>
        /// Rejoue une suite d'images (entrée enfoncée ?, bouton visé ?) à partir d'un état de
        /// départ, comme ViserBoutonTutoriel, et compte les validations.
        /// </summary>
        private static int Validations(bool dejaEnfonceeAuDepart, params (bool enfoncee, bool vise)[] images)
        {
            bool avant = dejaEnfonceeAuDepart;
            int n = 0;
            foreach ((bool enfoncee, bool vise) in images)
            {
                if (MainMenu.ValideAuFrontMontant(enfoncee, avant, vise)) n++;
                avant = enfoncee;
            }
            return n;
        }

        [Test]
        public void FrontMontant_EnVisant_Valide()
        {
            Assert.IsTrue(MainMenu.ValideAuFrontMontant(true, false, true));
        }

        [Test]
        public void SansViser_OuDejaEnfoncee_NeValidePas()
        {
            Assert.IsFalse(MainMenu.ValideAuFrontMontant(true, false, false), "bouton pas visé");
            Assert.IsFalse(MainMenu.ValideAuFrontMontant(true, true, true), "entrée déjà enfoncée");
            Assert.IsFalse(MainMenu.ValideAuFrontMontant(false, false, true), "entrée relâchée");
        }

        [Test]
        public void LancementDuTutoriel_LePouceEncoreRabattu_NArretePasLeTutoriel()
        {
            // Le tutoriel vient d'être lancé par un tir : MasquerTousLesLasers marque l'entrée
            // « déjà enfoncée ». Le pouce reste rabattu, le laser toujours sur le bouton.
            Assert.AreEqual(0, Validations(true, (true, true), (true, true), (true, true)));
        }

        [Test]
        public void EntreeMaintenue_SurLeBouton_UneSeuleValidation()
        {
            // Avant la correction, l'état était remis à zéro à chaque image : une validation par image.
            Assert.AreEqual(1, Validations(false, (true, true), (true, true), (true, true), (true, true)));
        }

        [Test]
        public void RelacherPuisReappuyer_ValideDeNouveau()
        {
            Assert.AreEqual(2, Validations(false, (true, true), (false, true), (true, true)));
        }

        [Test]
        public void ApresLancement_RelacherPuisTirer_ArreteLeTutoriel()
        {
            Assert.AreEqual(1, Validations(true, (true, true), (false, true), (true, true)));
        }
    }
}
