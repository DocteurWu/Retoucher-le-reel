// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  NarrationVoixTests.cs — TESTS EDITMODE DU NOM DES FICHIERS DE VOIX
//
//  RÔLE
//    Les MP3 sont nommés par Outils/VoixNarration/generer.mjs, et retrouvés par
//    NarrationBulle.CleVoix : les deux calculs doivent donner le même résultat.
//    Valeur attendue obtenue avec generer.mjs (MD5 de l'UTF-8, accents compris).
// =====================================================================================
using NUnit.Framework;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.EditorTools.Tests
{
    public class NarrationVoixTests
    {
        [Test]
        public void CleVoix_MemeCalculQueLeGenerateur()
        {
            Assert.AreEqual("ca274ecb1e0401bc0c6287034cc0453d", NarrationBulle.CleVoix("Une cl\u00E9, cach\u00E9e derri\u00E8re le tableau !"));
        }

        [Test]
        public void CleVoix_IgnoreLesEspacesAutour()
        {
            Assert.AreEqual(NarrationBulle.CleVoix("Le bac est plein."), NarrationBulle.CleVoix("  Le bac est plein.\n"));
        }
    }
}
