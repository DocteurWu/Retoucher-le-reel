// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MelangeurMemoireTests.cs — TESTS DE LA LOGIQUE DU MÉLANGE (EditMode)
//
//  Lancer : Window > General > Test Runner > EditMode. Aucune scène nécessaire : le mélangeur
//  ne manipule que des nombres et des positions.
// =====================================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RemodelerLeReel.Memoire;

namespace RemodelerLeReel.Tests
{
    public class MelangeurMemoireTests
    {
        /// <summary>5 objets alignés sur l'axe X (origines 0, 2, 4, 6, 8 m).</summary>
        private static List<ObjetMelange> Objets(float taille = 0.2f)
        {
            var liste = new List<ObjetMelange>();
            for (int i = 0; i < 5; i++)
            {
                liste.Add(new ObjetMelange { id = i, origine = new Vector3(i * 2f, 1f, 0f), taille = taille, fixe = -1, autorises = null });
            }
            return liste;
        }

        /// <summary>8 emplacements alignés sur l'axe Z, espacés de 1 m, à 5 m des objets.</summary>
        private static List<EmplacementMelange> Emplacements()
        {
            var liste = new List<EmplacementMelange>();
            for (int i = 0; i < 8; i++)
            {
                liste.Add(new EmplacementMelange { id = i, position = new Vector3(4f, 1f, 5f + i), tailleMax = 0f });
            }
            return liste;
        }

        [Test]
        public void MemeGraine_DonneLeMemeResultat()
        {
            List<Deplacement> a = MelangeurMemoire.Calculer(Objets(), Emplacements(), 3, 1.5f, 1234);
            List<Deplacement> b = MelangeurMemoire.Calculer(Objets(), Emplacements(), 3, 1.5f, 1234);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].objet, b[i].objet);
                Assert.AreEqual(a[i].emplacement, b[i].emplacement);
            }
        }

        [Test]
        public void DeplaceLeNombreDemande_QuandOnATropDePlace()
        {
            List<Deplacement> r = MelangeurMemoire.Calculer(Objets(), Emplacements(), 3, 1.5f, 7);
            Assert.AreEqual(3, r.Count);
        }

        [Test]
        public void UnObjetNEstDeplaceQuUneFois()
        {
            List<Deplacement> r = MelangeurMemoire.Calculer(Objets(), Emplacements(), 5, 1.5f, 7);
            var vus = new HashSet<int>();
            foreach (Deplacement d in r) Assert.IsTrue(vus.Add(d.objet), "objet " + d.objet + " déplacé deux fois");
        }

        [Test]
        public void DeuxObjetsNeVontJamaisSurLeMemeEmplacement()
        {
            for (int graine = 1; graine <= 50; graine++)
            {
                List<Deplacement> r = MelangeurMemoire.Calculer(Objets(), Emplacements(), 5, 1.5f, graine);
                var pris = new HashSet<int>();
                foreach (Deplacement d in r) Assert.IsTrue(pris.Add(d.emplacement), "graine " + graine + " : emplacement " + d.emplacement + " pris deux fois");
            }
        }

        [Test]
        public void RespecteLaDistanceMinimale()
        {
            List<ObjetMelange> objets = Objets();
            List<EmplacementMelange> emplacements = Emplacements();
            for (int graine = 1; graine <= 30; graine++)
            {
                foreach (Deplacement d in MelangeurMemoire.Calculer(objets, emplacements, 5, 5.5f, graine))
                {
                    float distance = Vector3.Distance(objets[d.objet].origine, emplacements[d.emplacement].position);
                    Assert.GreaterOrEqual(distance, 5.5f, "graine " + graine);
                }
            }
        }

        [Test]
        public void NeDonneRienSiAucunEmplacementNeConvient()
        {
            // Distance minimale impossible à atteindre.
            List<Deplacement> r = MelangeurMemoire.Calculer(Objets(), Emplacements(), 3, 1000f, 5);
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void SansEmplacementOuSansDemande_RienNeBouge()
        {
            Assert.AreEqual(0, MelangeurMemoire.Calculer(Objets(), new List<EmplacementMelange>(), 3, 1.5f, 5).Count);
            Assert.AreEqual(0, MelangeurMemoire.Calculer(Objets(), Emplacements(), 0, 1.5f, 5).Count);
            Assert.AreEqual(0, MelangeurMemoire.Calculer(null, Emplacements(), 3, 1.5f, 5).Count);
        }

        [Test]
        public void UnPetitEmplacementNeRecoitPasUnGrosObjet()
        {
            // Un seul emplacement, limité à 10 cm : l'objet de 20 cm n'y tient pas.
            var emplacements = new List<EmplacementMelange>
            {
                new EmplacementMelange { id = 0, position = new Vector3(4f, 1f, 5f), tailleMax = 0.1f }
            };
            Assert.AreEqual(0, MelangeurMemoire.Calculer(Objets(0.2f), emplacements, 3, 1.5f, 5).Count);
            Assert.AreEqual(1, MelangeurMemoire.Calculer(Objets(0.05f), emplacements, 3, 1.5f, 5).Count);
        }

        [Test]
        public void SeulsLesEmplacementsAutorisesSontUtilises()
        {
            List<ObjetMelange> objets = Objets();
            for (int i = 0; i < objets.Count; i++)
            {
                ObjetMelange o = objets[i];
                o.autorises = new[] { 2, 3 };
                objets[i] = o;
            }
            for (int graine = 1; graine <= 30; graine++)
            {
                foreach (Deplacement d in MelangeurMemoire.Calculer(objets, Emplacements(), 2, 1.5f, graine))
                    Assert.IsTrue(d.emplacement == 2 || d.emplacement == 3, "graine " + graine + " : emplacement " + d.emplacement);
            }
        }

        [Test]
        public void UnObjetAEmplacementFixe_VaToujoursLa()
        {
            List<ObjetMelange> objets = Objets();
            ObjetMelange o = objets[3];
            o.fixe = 6;
            objets[3] = o;

            for (int graine = 1; graine <= 30; graine++)
            {
                List<Deplacement> r = MelangeurMemoire.Calculer(objets, Emplacements(), 1, 1.5f, graine);
                Assert.AreEqual(1, r.Count);
                Assert.AreEqual(3, r[0].objet, "graine " + graine + " : l'objet à emplacement fixe passe en premier");
                Assert.AreEqual(6, r[0].emplacement);
            }
        }

        [Test]
        public void UnEmplacementProcheDUneOrigineNEstJamaisChoisi()
        {
            // Un emplacement posé sur l'origine d'un objet (rayon d'occupation) est refusé.
            var emplacements = new List<EmplacementMelange>
            {
                new EmplacementMelange { id = 0, position = new Vector3(2f, 1f, 0.1f), tailleMax = 0f }, // sur l'objet n°1
            };
            Assert.AreEqual(0, MelangeurMemoire.Calculer(Objets(), emplacements, 3, 0f, 5).Count);
        }
    }
}
