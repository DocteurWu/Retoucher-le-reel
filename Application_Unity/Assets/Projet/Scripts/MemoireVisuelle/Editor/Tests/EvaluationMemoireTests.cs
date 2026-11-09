// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EvaluationMemoireTests.cs — TESTS DE LA TOLÉRANCE ET DU SCORE (EditMode)
//
//  Lancer : Window > General > Test Runner > EditMode.
// =====================================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RemodelerLeReel.Memoire;

namespace RemodelerLeReel.Tests
{
    public class EvaluationMemoireTests
    {
        [Test]
        public void ToleranceDUnPetitObjet_EstLaMargeDeBase()
        {
            // 10 cm × 1 = 0,1 m < 0,5 m : la marge de base s'applique.
            Assert.AreEqual(0.5f, EvaluationMemoire.Tolerance(0.1f, 0.5f, 1f), 1e-5f);
        }

        [Test]
        public void ToleranceDUnGrosObjet_SuitSaTaille()
        {
            // 2 m × 1 = 2 m > 0,5 m : la marge proportionnelle s'applique.
            Assert.AreEqual(2f, EvaluationMemoire.Tolerance(2f, 0.5f, 1f), 1e-5f);
        }

        [Test]
        public void ToleranceIgnoreUneTailleNegative()
        {
            Assert.AreEqual(0.5f, EvaluationMemoire.Tolerance(-3f, 0.5f, 1f), 1e-5f);
        }

        [Test]
        public void ObjetPileSurSaPlace_EstCorrect()
        {
            ResultatObjet r = EvaluationMemoire.Evaluer(0, new Vector3(1f, 1f, 1f), new Vector3(1f, 1f, 1f), 0.2f, 0.5f, 1f);
            Assert.IsTrue(r.ok);
            Assert.AreEqual(0f, r.distance, 1e-5f);
        }

        [Test]
        public void ObjetDansLaMarge_EstCorrect_EtHorsDeLaMarge_EstRate()
        {
            Vector3 origine = Vector3.zero;
            Assert.IsTrue(EvaluationMemoire.Evaluer(0, origine, new Vector3(0.45f, 0f, 0f), 0.2f, 0.5f, 1f).ok);
            Assert.IsFalse(EvaluationMemoire.Evaluer(0, origine, new Vector3(0.6f, 0f, 0f), 0.2f, 0.5f, 1f).ok);
        }

        [Test]
        public void LaDistanceEstEnTroisDimensions()
        {
            // 0,3 en hauteur et 0,5 de côté : 0,58 m > 0,5 m.
            ResultatObjet r = EvaluationMemoire.Evaluer(0, Vector3.zero, new Vector3(0.5f, 0.3f, 0f), 0.2f, 0.5f, 1f);
            Assert.AreEqual(0.583f, r.distance, 1e-3f);
            Assert.IsFalse(r.ok);
        }

        [Test]
        public void Score_EstLaPartDObjetsCorrects()
        {
            var resultats = new List<ResultatObjet>
            {
                new ResultatObjet { ok = true }, new ResultatObjet { ok = true }, new ResultatObjet { ok = true },
                new ResultatObjet { ok = true }, new ResultatObjet { ok = false }
            };
            Assert.AreEqual(0.8f, EvaluationMemoire.Score(resultats), 1e-5f);
            Assert.AreEqual(4, EvaluationMemoire.Corrects(resultats));
        }

        [Test]
        public void ScoreDUneListeVideOuNulle_EstZero()
        {
            Assert.AreEqual(0f, EvaluationMemoire.Score(new List<ResultatObjet>()));
            Assert.AreEqual(0f, EvaluationMemoire.Score(null));
            Assert.AreEqual(0, EvaluationMemoire.Corrects(null));
        }

        [Test]
        public void QuatreSurCinq_AtteintLeSeuilDeQuatreVingtsPourCent()
        {
            // 4/5 = 0,8 : doit passer malgré les arrondis des flottants.
            Assert.IsTrue(EvaluationMemoire.Reussi(4f / 5f, 0.8f, 5));
            Assert.IsFalse(EvaluationMemoire.Reussi(3f / 5f, 0.8f, 5));
        }

        [Test]
        public void SansObjet_OnNeReussitPas()
        {
            Assert.IsFalse(EvaluationMemoire.Reussi(0f, 0f, 0));
        }
    }
}
