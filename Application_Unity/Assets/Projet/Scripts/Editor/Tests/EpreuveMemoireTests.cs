// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EpreuveMemoireTests.cs — TESTS EDITMODE DU JEU DE MÉMOIRE
//
//  RÔLE
//    Vérifie les calculs purs de EpreuveMemoire :
//      • EstAPortee : quand un objet lâché se cale à sa place (distance, orientation) ;
//      • DecalageTas : les objets du tas tombent dans le rayon, à des endroits différents.
// =====================================================================================
using NUnit.Framework;
using UnityEngine;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class EpreuveMemoireTests
    {
        private static readonly Vector3 Place = new Vector3(1f, 0.8f, -2f);

        /// <summary>
        /// Rotation autour de l'axe vertical, calculée à la main : Quaternion.Euler passe par le
        /// moteur natif, ce calcul pur permet aussi de lancer les tests hors de l'éditeur.
        /// </summary>
        private static Quaternion Lacet(float degres)
        {
            float demi = degres * Mathf.Deg2Rad * 0.5f;
            return new Quaternion(0f, Mathf.Sin(demi), 0f, Mathf.Cos(demi));
        }

        [Test]
        public void APortee_PresDeSaPlace_SeCale()
        {
            Assert.IsTrue(EpreuveMemoire.EstAPortee(Place + new Vector3(0.1f, 0.05f, 0f), Quaternion.identity,
                Place, Quaternion.identity, 0.2f, 0f));
        }

        [Test]
        public void APortee_TropLoin_NeSeCalePas()
        {
            Assert.IsFalse(EpreuveMemoire.EstAPortee(Place + new Vector3(0.3f, 0f, 0f), Quaternion.identity,
                Place, Quaternion.identity, 0.2f, 0f));
        }

        [Test]
        public void APortee_AngleIgnore_SiToleranceNulle()
        {
            Quaternion retourne = Lacet(170f);
            Assert.IsTrue(EpreuveMemoire.EstAPortee(Place, retourne, Place, Quaternion.identity, 0.2f, 0f));
        }

        [Test]
        public void APortee_AngleCompte_SiToleranceDonnee()
        {
            Assert.IsFalse(EpreuveMemoire.EstAPortee(Place, Lacet(90f), Place, Quaternion.identity, 0.2f, 35f));
            Assert.IsTrue(EpreuveMemoire.EstAPortee(Place, Lacet(20f), Place, Quaternion.identity, 0.2f, 35f));
        }

        [Test]
        public void Tolerance_GrosObjet_PlusLarge_MaisPlafonnee()
        {
            float assiette = EpreuveMemoire.ToleranceEffective(0.2f, 0.15f, 0.75f, 0.4f);
            float chaise = EpreuveMemoire.ToleranceEffective(0.2f, 0.55f, 0.75f, 0.4f);
            float geant = EpreuveMemoire.ToleranceEffective(0.2f, 5f, 0.75f, 0.4f);
            Assert.Greater(assiette, 0.2f);
            Assert.Greater(chaise, assiette);
            Assert.AreEqual(0.6f, geant, 1e-4f); // marge plafonnée à 0,4 m
        }

        [Test]
        public void Tolerance_PoseACoteDUneChaise_SeCale()
        {
            // 45 cm à côté du centre d'une chaise : refusé avec 20 cm fixes, accepté avec la marge de taille.
            float tol = EpreuveMemoire.ToleranceEffective(0.2f, 0.55f, 0.75f, 0.4f);
            Vector3 aCote = Place + new Vector3(0.45f, 0f, 0f);
            Assert.IsFalse(EpreuveMemoire.EstAPortee(aCote, Quaternion.identity, Place, Quaternion.identity, 0.2f, 0f));
            Assert.IsTrue(EpreuveMemoire.EstAPortee(aCote, Quaternion.identity, Place, Quaternion.identity, tol, 0f));
        }

        [Test]
        public void Tas_ObjetsDansLeRayon_EtAuDessusDuSol()
        {
            const int total = 10;
            const float rayon = 0.35f;
            for (int i = 0; i < total; i++)
            {
                Vector3 d = EpreuveMemoire.DecalageTas(i, total, rayon);
                Assert.LessOrEqual(new Vector2(d.x, d.z).magnitude, rayon + 1e-4f, "objet " + i);
                Assert.Greater(d.y, 0f, "objet " + i);
            }
        }

        [Test]
        public void Tas_DeuxObjetsVoisins_NeTombentPasAuMemeEndroit()
        {
            for (int i = 0; i < 9; i++)
            {
                Vector3 a = EpreuveMemoire.DecalageTas(i, 10, 0.35f);
                Vector3 b = EpreuveMemoire.DecalageTas(i + 1, 10, 0.35f);
                Assert.Greater(new Vector2(a.x - b.x, a.z - b.z).magnitude, 0.05f, "objets " + i + " et " + (i + 1));
            }
        }

        [Test]
        public void Tas_ZeroObjet_PasDeDivisionParZero()
        {
            Vector3 d = EpreuveMemoire.DecalageTas(0, 0, 0.35f);
            Assert.IsFalse(float.IsNaN(d.x) || float.IsNaN(d.z));
        }

        [Test]
        public void AideNecessaire_ApresLeSeuil_Seulement()
        {
            Assert.IsFalse(EpreuveMemoire.AideNecessaire(19.9f, 20f));
            Assert.IsTrue(EpreuveMemoire.AideNecessaire(20f, 20f));
            Assert.IsTrue(EpreuveMemoire.AideNecessaire(45f, 20f));
        }

        [Test]
        public void Niveau1_TailleInchangeeEtSansImportance()
        {
            for (int i = 0; i < 6; i++) Assert.AreEqual(1f, EpreuveMemoire.RatioTailleMelange(1, i));
            Assert.AreEqual(1f, EpreuveMemoire.RatioTailleMelange(0, 3)); // sans niveaux
            Assert.IsFalse(EpreuveMemoire.TailleACompte(1));
            Assert.IsFalse(EpreuveMemoire.TailleACompte(0));
        }

        [Test]
        public void Niveaux2Et3_TaillesFaussesARetrouver()
        {
            Assert.AreEqual(0.8f, EpreuveMemoire.RatioTailleMelange(2, 0));
            Assert.AreEqual(1.25f, EpreuveMemoire.RatioTailleMelange(2, 1));
            Assert.AreEqual(0.65f, EpreuveMemoire.RatioTailleMelange(3, 2));
            Assert.AreEqual(1.5f, EpreuveMemoire.RatioTailleMelange(3, 3));
            Assert.IsTrue(EpreuveMemoire.TailleACompte(2));
            Assert.IsTrue(EpreuveMemoire.TailleACompte(3));
        }

        [Test]
        public void Niveau3_ObservationDeuxFoisPlusCourte()
        {
            Assert.AreEqual(30f, EpreuveMemoire.DureeObservationNiveau(1, 30f));
            Assert.AreEqual(30f, EpreuveMemoire.DureeObservationNiveau(2, 30f));
            Assert.AreEqual(15f, EpreuveMemoire.DureeObservationNiveau(3, 30f));
        }

        [Test]
        public void AideNecessaire_SeuilNul_Jamais()
        {
            Assert.IsFalse(EpreuveMemoire.AideNecessaire(1000f, 0f));
        }
    }
}
