// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRCloneOriginTests.cs — TESTS EDITMODE DES FAMILLES DE CLONES
//
//  RÔLE
//    Vérifie que l'original, ses copies et les copies de copies forment UNE
//    famille, et qu'elle survit à la suppression de l'original. C'est ce qui
//    garantit le plafond de duplication (protection GPU), la narration « objet
//    cible » et le compteur de l'épreuve du récipient.
//
//  Les clones sont fabriqués comme dans VRDuplicateDelete : Instantiate (qui
//  recopie le marqueur d'une copie) puis VRCloneOrigin.Attach.
// =====================================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class VRCloneOriginTests
    {
        private readonly List<GameObject> _crees = new List<GameObject>();

        [TearDown]
        public void Nettoyer()
        {
            foreach (GameObject go in _crees)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _crees.Clear();
        }

        private GameObject Nouveau(string nom)
        {
            GameObject go = new GameObject(nom);
            _crees.Add(go);
            return go;
        }

        /// <summary>Duplique comme VRDuplicateDelete.TryDuplicate.</summary>
        private GameObject Dupliquer(GameObject source)
        {
            GameObject clone = Object.Instantiate(source);
            _crees.Add(clone);
            VRCloneOrigin.Attach(clone, source);
            return clone;
        }

        [Test]
        public void CopieDeCopie_ResteDansLaFamilleDeLOriginal()
        {
            GameObject fraise = Nouveau("Fraise");
            GameObject copie = Dupliquer(fraise);
            GameObject copieDeCopie = Dupliquer(copie);

            Assert.IsTrue(VRCloneOrigin.MemeFamille(fraise, copie));
            Assert.IsTrue(VRCloneOrigin.MemeFamille(fraise, copieDeCopie));
            Assert.AreSame(fraise, copieDeCopie.GetComponent<VRCloneOrigin>().source,
                           "une copie de copie doit pointer vers l'original");
            Assert.AreEqual(3, VRCloneOrigin.CountInstances(copieDeCopie));
        }

        [Test]
        public void SupprimerLOriginal_GardeLaFamilleDesCopies()
        {
            GameObject fraise = Nouveau("Fraise");
            GameObject a = Dupliquer(fraise);
            GameObject b = Dupliquer(fraise);
            int famille = VRCloneOrigin.FamilleDe(fraise);

            Object.DestroyImmediate(fraise);

            Assert.IsTrue(VRCloneOrigin.MemeFamille(a, b));
            Assert.AreEqual(famille, VRCloneOrigin.FamilleDe(a));
            Assert.AreEqual(2, VRCloneOrigin.CountInstances(a), "les deux copies restent comptées ensemble");

            // Une nouvelle copie faite APRÈS la suppression rejoint toujours la famille.
            GameObject c = Dupliquer(a);
            Assert.AreEqual(famille, VRCloneOrigin.FamilleDe(c));
            Assert.AreEqual(3, VRCloneOrigin.CountInstances(b), "le plafond ne doit pas se contourner");
        }

        [Test]
        public void DeuxObjetsDifferents_FamillesDistinctes()
        {
            GameObject fraise = Nouveau("Fraise");
            GameObject cylindre = Nouveau("Fraise"); // même nom : seul le lien compte
            GameObject copieFraise = Dupliquer(fraise);

            Assert.IsFalse(VRCloneOrigin.MemeFamille(fraise, cylindre));
            Assert.IsFalse(VRCloneOrigin.MemeFamille(copieFraise, cylindre));
            Assert.AreEqual(1, VRCloneOrigin.CountInstances(cylindre));
        }

        [Test]
        public void ObjetNull_FamilleZero()
        {
            Assert.AreEqual(0, VRCloneOrigin.FamilleDe(null));
            Assert.IsFalse(VRCloneOrigin.MemeFamille(null, null));
            Assert.AreEqual(0, VRCloneOrigin.CountInstances(null));
        }
    }
}
