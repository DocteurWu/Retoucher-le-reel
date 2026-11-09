// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  ChangementsTailleMemoireTests.cs — TESTS DES CHANGEMENTS DE TAILLE (EditMode)
//
//  Couvre le choix des objets et des facteurs (ChangementsTailleMemoire) et l'évaluation de la
//  taille retrouvée (EvaluationMemoire.TailleRetrouvee / Evaluer avec ratio).
//  Lancer : Window > General > Test Runner > EditMode.
// =====================================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RemodelerLeReel.Memoire;

namespace RemodelerLeReel.Tests
{
    public class ChangementsTailleMemoireTests
    {
        private static readonly Vector2 Agrandi = new Vector2(1.8f, 2.4f);
        private static readonly Vector2 Reduit = new Vector2(0.4f, 0.55f);

        /// <summary>8 objets de 20 cm, sauf le dernier (5 cm : trop petit pour changer de taille).</summary>
        private static List<float> Tailles()
        {
            var t = new List<float>();
            for (int i = 0; i < 7; i++) t.Add(0.2f);
            t.Add(0.05f);
            return t;
        }

        [Test]
        public void MemeGraine_DonneLeMemeResultat()
        {
            List<ChangementTaille> a = ChangementsTailleMemoire.Calculer(Tailles(), new List<int> { 0, 1 }, 2, 0.12f, Agrandi, Reduit, 99);
            List<ChangementTaille> b = ChangementsTailleMemoire.Calculer(Tailles(), new List<int> { 0, 1 }, 2, 0.12f, Agrandi, Reduit, 99);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].objet, b[i].objet);
                Assert.AreEqual(a[i].ratio, b[i].ratio, 1e-6f);
            }
        }

        [Test]
        public void ChoisitLeNombreDemande_SansDoublon()
        {
            for (int graine = 1; graine <= 30; graine++)
            {
                List<ChangementTaille> r = ChangementsTailleMemoire.Calculer(Tailles(), null, 3, 0.12f, Agrandi, Reduit, graine);
                Assert.AreEqual(3, r.Count);
                var vus = new HashSet<int>();
                foreach (ChangementTaille c in r) Assert.IsTrue(vus.Add(c.objet), "graine " + graine);
            }
        }

        [Test]
        public void LesPetitsObjetsNeChangentJamaisDeTaille()
        {
            for (int graine = 1; graine <= 30; graine++)
            {
                foreach (ChangementTaille c in ChangementsTailleMemoire.Calculer(Tailles(), null, 8, 0.12f, Agrandi, Reduit, graine))
                    Assert.AreNotEqual(7, c.objet, "graine " + graine + " : l'objet de 5 cm a changé de taille");
            }
        }

        [Test]
        public void OnEvitePourCommencerLesObjetsDejaDeplaces()
        {
            // 0 à 4 déjà déplacés : avec 2 changements, on doit choisir parmi 5, 6 (7 est trop petit).
            var deplaces = new List<int> { 0, 1, 2, 3, 4 };
            for (int graine = 1; graine <= 30; graine++)
            {
                foreach (ChangementTaille c in ChangementsTailleMemoire.Calculer(Tailles(), deplaces, 2, 0.12f, Agrandi, Reduit, graine))
                    Assert.IsTrue(c.objet == 5 || c.objet == 6, "graine " + graine + " : objet " + c.objet);
            }
        }

        [Test]
        public void SiPasAssezDeLibres_OnCompleteAvecDesObjetsDejaDeplaces()
        {
            var deplaces = new List<int> { 0, 1, 2, 3, 4 };
            List<ChangementTaille> r = ChangementsTailleMemoire.Calculer(Tailles(), deplaces, 4, 0.12f, Agrandi, Reduit, 5);
            Assert.AreEqual(4, r.Count); // 5 et 6 libres + 2 déjà déplacés
        }

        [Test]
        public void LesChangementsAlternentAgrandiEtReduit()
        {
            for (int graine = 1; graine <= 30; graine++)
            {
                List<ChangementTaille> r = ChangementsTailleMemoire.Calculer(Tailles(), null, 4, 0.12f, Agrandi, Reduit, graine);
                for (int i = 0; i < r.Count; i++)
                {
                    if (i % 2 == 0) Assert.That(r[i].ratio, Is.InRange(Agrandi.x, Agrandi.y), "graine " + graine + " rang " + i);
                    else Assert.That(r[i].ratio, Is.InRange(Reduit.x, Reduit.y), "graine " + graine + " rang " + i);
                }
            }
        }

        [Test]
        public void LesChangementsSontClairs_JamaisProchesDeUn()
        {
            for (int graine = 1; graine <= 30; graine++)
            {
                foreach (ChangementTaille c in ChangementsTailleMemoire.Calculer(Tailles(), null, 6, 0.12f, Agrandi, Reduit, graine))
                    Assert.IsTrue(c.ratio >= 1.8f || c.ratio <= 0.55f, "graine " + graine + " : ratio " + c.ratio);
            }
        }

        [Test]
        public void ZeroChangement_OuListeNulle_NeDonneRien()
        {
            Assert.AreEqual(0, ChangementsTailleMemoire.Calculer(Tailles(), null, 0, 0.12f, Agrandi, Reduit, 5).Count);
            Assert.AreEqual(0, ChangementsTailleMemoire.Calculer(null, null, 3, 0.12f, Agrandi, Reduit, 5).Count);
        }

        // ------------------------------------------------------------------
        // Évaluation de la taille retrouvée
        // ------------------------------------------------------------------
        [Test]
        public void TailleNonEvaluee_QuandLaMargeEstZero()
        {
            Assert.IsTrue(EvaluationMemoire.TailleRetrouvee(5f, 0f));
            Assert.IsTrue(EvaluationMemoire.TailleRetrouvee(0.1f, 0f));
        }

        [Test]
        public void TailleRetrouvee_DansLaMargeSymetrique()
        {
            Assert.IsTrue(EvaluationMemoire.TailleRetrouvee(1.0f, 0.3f));
            Assert.IsTrue(EvaluationMemoire.TailleRetrouvee(1.25f, 0.3f));
            Assert.IsTrue(EvaluationMemoire.TailleRetrouvee(0.8f, 0.3f));
            Assert.IsFalse(EvaluationMemoire.TailleRetrouvee(1.4f, 0.3f));
            Assert.IsFalse(EvaluationMemoire.TailleRetrouvee(0.7f, 0.3f));
        }

        [Test]
        public void UnObjetDeuxFoisTropGrandOuTropPetit_EstRate()
        {
            Assert.IsFalse(EvaluationMemoire.TailleRetrouvee(2f, 0.3f));
            Assert.IsFalse(EvaluationMemoire.TailleRetrouvee(0.5f, 0.3f));
        }

        [Test]
        public void TailleNulleOuNegative_EstRatee()
        {
            Assert.IsFalse(EvaluationMemoire.TailleRetrouvee(0f, 0.3f));
            Assert.IsFalse(EvaluationMemoire.TailleRetrouvee(-1f, 0.3f));
        }

        [Test]
        public void BonnePositionMaisMauvaiseTaille_EstRate()
        {
            ResultatObjet r = EvaluationMemoire.Evaluer(0, Vector3.zero, Vector3.zero, 0.2f, 0.3f, 0.6f, 2f, 0.3f);
            Assert.IsTrue(r.positionOk);
            Assert.IsFalse(r.tailleOk);
            Assert.IsFalse(r.ok);
            Assert.AreEqual(2f, r.ratioTaille, 1e-6f);
        }

        [Test]
        public void BonneTailleMaisMauvaisePosition_EstRate()
        {
            ResultatObjet r = EvaluationMemoire.Evaluer(0, Vector3.zero, new Vector3(2f, 0f, 0f), 0.2f, 0.3f, 0.6f, 1f, 0.3f);
            Assert.IsFalse(r.positionOk);
            Assert.IsTrue(r.tailleOk);
            Assert.IsFalse(r.ok);
        }

        [Test]
        public void PositionEtTailleBonnes_EstCorrect()
        {
            ResultatObjet r = EvaluationMemoire.Evaluer(0, Vector3.zero, new Vector3(0.1f, 0f, 0f), 0.2f, 0.3f, 0.6f, 1.1f, 0.3f);
            Assert.IsTrue(r.ok);
        }

        [Test]
        public void SansMargeDeTaille_LaTailleNeComptePas()
        {
            // Appel comme au niveau 1 (arguments de taille omis) : un objet ×3 bien placé reste correct.
            ResultatObjet r = EvaluationMemoire.Evaluer(0, Vector3.zero, Vector3.zero, 0.2f, 0.5f, 1f, 3f, 0f);
            Assert.IsTrue(r.ok);
            ResultatObjet ancien = EvaluationMemoire.Evaluer(0, Vector3.zero, Vector3.zero, 0.2f, 0.5f, 1f);
            Assert.IsTrue(ancien.ok);
            Assert.AreEqual(1f, ancien.ratioTaille, 1e-6f);
        }
    }
}
