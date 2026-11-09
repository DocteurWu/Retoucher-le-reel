// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  TutorielGestesTests.cs — TESTS DES DÉMONSTRATIONS ET DES POSES DES MAINS FANTÔMES
//
//  Les poses sont calculées sur un squelette de test reprenant les mesures du visuel de
//  main gauche (axes XR Hands : doigts vers +Z, dos de la main vers +Y).
// =====================================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RemodelerLeReel.Jeu;

namespace RemodelerLeReel.Tests
{
    public class TutorielGestesTests
    {
        private GameObject _racine;
        private Transform _poignet;

        private Transform Os(string nom, Transform parent, float x, float y, float z, Quaternion? rotation = null)
        {
            Transform t = new GameObject("L_" + nom).transform;
            t.SetParent(parent, false);
            t.localPosition = new Vector3(x, y, z);
            t.localRotation = rotation ?? Quaternion.identity;
            return t;
        }

        private void Doigt(string nom, float x, float y, float z, float l1, float l2, float l3, float l4)
        {
            // Comme le visuel : au repos, les doigts sont déjà un peu pliés.
            Transform p = Os(nom + "Proximal", _poignet, x, y, z, Quaternion.Euler(18f, 0f, 0f));
            Transform i = Os(nom + "Intermediate", p, 0f, 0f, l1, Quaternion.Euler(18f, 0f, 0f));
            Transform d = Os(nom + "Distal", i, 0f, 0f, l2);
            Os(nom + "Tip", d, 0f, 0f, l3 + l4);
        }

        [SetUp]
        public void CreerSquelette()
        {
            _racine = new GameObject("Squelette");
            _poignet = Os("Wrist", _racine.transform, 0f, 0f, 0f);
            Doigt("Index", 0.0235f, -0.0073f, 0.096f, 0.0379f, 0.0243f, 0.0224f, 0f);
            Doigt("Middle", 0.0017f, -0.0025f, 0.0956f, 0.0429f, 0.0275f, 0.025f, 0f);
            Doigt("Ring", -0.0175f, -0.0065f, 0.0887f, 0.039f, 0.0266f, 0.0243f, 0f);
            Doigt("Little", -0.033f, -0.0094f, 0.078f, 0.0307f, 0.0203f, 0.0219f, 0f);
            Transform m = Os("ThumbMetacarpal", _poignet, 0.0302f, -0.0161f, 0.0345f, new Quaternion(-0.0667f, 0.3969f, -0.575f, 0.7123f));
            Transform p = Os("ThumbProximal", m, 0f, 0f, 0.0325f);
            Transform d = Os("ThumbDistal", p, 0f, 0f, 0.0338f);
            Os("ThumbTip", d, 0f, 0f, 0.0246f);
        }

        [TearDown]
        public void Detruire()
        {
            UnityEngine.Object.DestroyImmediate(_racine);
        }

        private readonly List<Transform> _osPilotes = new List<Transform>();

        /// <summary>Applique une pose (mêmes indices que MainFantome : doigts puis pouce).</summary>
        private void Appliquer(Quaternion[][] poses, PoseMain pose)
        {
            _osPilotes.Clear();
            foreach (string doigt in new[] { "Index", "Middle", "Ring", "Little" })
            {
                foreach (string phalange in new[] { "Proximal", "Intermediate", "Distal" }) _osPilotes.Add(Trouver(doigt + phalange));
            }
            foreach (string nom in new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal" }) _osPilotes.Add(Trouver(nom));
            for (int i = 0; i < _osPilotes.Count; i++) _osPilotes[i].localRotation = poses[(int)pose][i];
        }

        private Transform Trouver(string nom) => RemodelerLeReel.VR.VRUtils.FindDeep(_poignet, "L_" + nom);

        private float Ecart(string a, string b) => Vector3.Distance(Trouver(a).position, Trouver(b).position);

        [Test]
        public void Poses_UnePoseParValeur()
        {
            Quaternion[][] poses = MainFantome.CalculerPoses(_poignet, "L_");
            Assert.AreEqual(Enum.GetValues(typeof(PoseMain)).Length, poses.Length);
        }

        [Test]
        public void Poses_SqueletteIncomplet_Null()
        {
            UnityEngine.Object.DestroyImmediate(Trouver("ThumbTip").gameObject);
            Assert.IsNull(MainFantome.CalculerPoses(_poignet, "L_"));
        }

        [Test]
        public void Poses_LesOsRetrouventLeurEtatInitial()
        {
            Quaternion avant = Trouver("IndexProximal").localRotation;
            MainFantome.CalculerPoses(_poignet, "L_");
            Assert.AreEqual(avant, Trouver("IndexProximal").localRotation);
        }

        [Test]
        public void Poing_LeBoutDesDoigtsRevientVersLaPaume()
        {
            Appliquer(MainFantome.CalculerPoses(_poignet, "L_"), PoseMain.Poing);
            Assert.Less(Trouver("MiddleDistal").position.y, -0.03f, "le doigt se plie côté paume (-Y)");
            Assert.Less(Trouver("MiddleTip").position.z, 0.09f, "le bout du majeur revient vers le poignet");
        }

        [Test]
        public void Pince_LePouceTouchantLIndex()
        {
            Appliquer(MainFantome.CalculerPoses(_poignet, "L_"), PoseMain.Pince);
            Assert.Less(Ecart("ThumbTip", "IndexTip"), 0.015f);
        }

        [Test]
        public void Pointe_IndexTenduAutresReplies()
        {
            Appliquer(MainFantome.CalculerPoses(_poignet, "L_"), PoseMain.Pointe);
            Assert.Greater(Trouver("IndexTip").position.z, 0.17f);
            Assert.Less(Trouver("MiddleTip").position.z, 0.12f);
        }

        [Test]
        public void SigneV_IndexEtMajeurTendusEtEcartes_PouceSurLAnnulaire()
        {
            float ecartRepos = Ecart("IndexTip", "MiddleTip");
            Appliquer(MainFantome.CalculerPoses(_poignet, "L_"), PoseMain.SigneV);
            Assert.Greater(Trouver("IndexTip").position.z, 0.16f, "index tendu");
            Assert.Greater(Trouver("MiddleTip").position.z, 0.16f, "majeur tendu");
            Assert.Greater(Ecart("IndexTip", "MiddleTip"), ecartRepos + 0.02f, "les deux doigts s'ouvrent en V");
            Assert.Less(Trouver("RingTip").position.z, 0.12f, "annulaire replié");
            Assert.Less(Ecart("ThumbTip", "RingIntermediate"), 0.02f, "le pouce tient l'annulaire, pas dans le vide");
            Assert.Less(Mathf.Abs(Trouver("IndexTip").position.y - Trouver("MiddleTip").position.y), 0.02f, "V dans le plan de la paume");
        }

        [Test]
        public void Microgeste_LePouceSeLeveEtTape()
        {
            Quaternion[][] poses = MainFantome.CalculerPoses(_poignet, "L_");
            Appliquer(poses, PoseMain.MicroTap);
            float tap = Ecart("ThumbTip", "IndexIntermediate");
            Appliquer(poses, PoseMain.MicroLeve);
            float leve = Ecart("ThumbTip", "IndexIntermediate");
            Assert.Less(tap, 0.015f);
            Assert.Greater(leve, tap + 0.015f);
        }

        [Test]
        public void Piste_InterpoleEtResteAuxBornes()
        {
            PisteAccessoire piste = new PisteAccessoire(FormeAccessoire.Cube)
                .Cle(1f, Vector3.zero, 1f)
                .Cle(3f, Vector3.one, 2f);

            piste.Encadrer(0f, out CleAccessoire a, out CleAccessoire b, out float k);
            Assert.AreEqual(1f, a.temps);
            Assert.AreEqual(1f, b.temps);

            piste.Encadrer(2f, out a, out b, out k);
            Assert.AreEqual(1f, a.temps);
            Assert.AreEqual(3f, b.temps);
            Assert.AreEqual(0.5f, k, 1e-4f);

            piste.Encadrer(9f, out a, out b, out k);
            Assert.AreEqual(3f, a.temps);
            Assert.AreEqual(3f, b.temps);
        }

        [TestCase("Tableau_A_Agrandir", "Le tableau")]
        [TestCase("Pot", "Le pot")]
        [TestCase("Porte", "La porte")]
        [TestCase("Zone_Detection", "Le bac")]
        [TestCase("Cle", "La clé")]
        [TestCase("Objet inconnu", "C'est ici !")]
        public void MainGuide_Etiquette(string nom, string attendu)
        {
            Assert.AreEqual(attendu, MainGuide.Etiquette(nom));
        }

        [Test]
        public void MainGuide_Perche_ObjetEnHauteur_AuPiedDeLObjetCoteJoueur()
        {
            Bounds objet = new Bounds(new Vector3(2f, 1.5f, 0f), new Vector3(0.2f, 0.4f, 0.2f));
            Vector3 p = MainGuide.PositionPerche(objet, new Vector3(0f, 1.6f, 0f), 0.2f, 0f, 0.3f);
            Assert.AreEqual(1.3f, p.y, 1e-4f);
            Assert.AreEqual(2f - 0.1f - 0.2f, p.x, 1e-4f);
        }

        [Test]
        public void MainGuide_Perche_ObjetBas_ResteAuSol()
        {
            Bounds objet = new Bounds(new Vector3(2f, 0.2f, 0f), new Vector3(0.2f, 0.2f, 0.2f));
            Assert.AreEqual(0f, MainGuide.PositionPerche(objet, Vector3.up, 0.2f, 0f, 0.3f).y, 1e-4f);
        }

        [Test]
        public void MainGuide_Saut_PartEtArriveAuBonEndroitEnPassantAuDessus()
        {
            Vector3 a = Vector3.zero, b = new Vector3(0f, 1f, 0f);
            Assert.AreEqual(a, MainGuide.PositionSaut(a, b, 0f, 0.25f));
            Assert.AreEqual(b, MainGuide.PositionSaut(a, b, 1f, 0.25f));
            Assert.Greater(MainGuide.PositionSaut(a, a, 0.5f, 0.25f).y, 0.2f);
        }

        [Test]
        public void MainGuide_ChampDeVision_DevantOui_DerriereEtTropLoinNon()
        {
            Vector3 tete = new Vector3(0f, 1.6f, 0f);
            Assert.IsTrue(MainGuide.EstDansLeChamp(tete, Vector3.forward, new Vector3(0.3f, 0f, 1.5f), 45f, 6f), "au sol devant");
            Assert.IsFalse(MainGuide.EstDansLeChamp(tete, Vector3.forward, new Vector3(0f, 0f, -1.5f), 45f, 6f), "derrière");
            Assert.IsFalse(MainGuide.EstDansLeChamp(tete, Vector3.forward, new Vector3(0f, 1.6f, 8f), 45f, 6f), "trop loin");
        }

        [Test]
        public void MainGuide_ClamperSurTable_ResteSurPlateau()
        {
            GameObject go = new GameObject("TableTest");
            BoxCollider col = go.AddComponent<BoxCollider>();
            go.transform.position = new Vector3(0f, 1f, 0f);
            col.center = Vector3.zero;
            col.size = new Vector3(2f, 0.1f, 1f);

            try
            {
                // Point en dehors a droite (+X) : ramene au bord avec la marge
                Vector3 dehorsX = new Vector3(5f, 0f, 0f);
                Vector3 p1 = MainGuide.ClamperSurTable(col, dehorsX, 0.05f);
                Assert.AreEqual(1.05f, p1.y, 1e-4f, "la hauteur est celle du plateau");
                Assert.AreEqual(1f - 0.05f, p1.x, 1e-4f, "ramene au bord X");
                Assert.AreEqual(0f, p1.z, 1e-4f);

                // Point au centre du plateau
                Vector3 centre = new Vector3(0.2f, 2f, -0.1f);
                Vector3 p2 = MainGuide.ClamperSurTable(col, centre, 0.05f);
                Assert.AreEqual(1.05f, p2.y, 1e-4f);
                Assert.AreEqual(0.2f, p2.x, 1e-4f);
                Assert.AreEqual(-0.1f, p2.z, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MainGuide_DestinationSurTable_ObjetAuLoin_SePlaceAuBordFaceALObjet()
        {
            GameObject goTable = new GameObject("TableTest");
            BoxCollider table = goTable.AddComponent<BoxCollider>();
            goTable.transform.position = new Vector3(0f, 1f, 0f);
            table.size = new Vector3(2f, 0.1f, 1f);

            GameObject goCible = new GameObject("CibleLoin");
            BoxCollider cible = goCible.AddComponent<BoxCollider>();
            goCible.transform.position = new Vector3(-5f, 1.6f, 0f);
            cible.size = new Vector3(0.3f, 0.3f, 0.3f);
            Physics.SyncTransforms(); // autoSyncTransforms = 0 : sinon les bornes des colliders restent à l'origine

            try
            {
                Vector3 dest = MainGuide.DestinationSurTable(table, cible, new Vector3(0f, 1.6f, -1f), 0.2f, 0.05f);
                Assert.AreEqual(1.05f, dest.y, 1e-4f, "sur le plateau");
                Assert.AreEqual(-1f + 0.05f, dest.x, 1e-4f, "au bord ouest de la table face a la cible");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goTable);
                UnityEngine.Object.DestroyImmediate(goCible);
            }
        }


        [Test]
        public void MainGuide_PointAleatoireSurTable_ResteStrictementDansLesBornes()
        {
            GameObject goTable = new GameObject("TableTest");
            BoxCollider table = goTable.AddComponent<BoxCollider>();
            goTable.transform.position = new Vector3(-0.92f, 1f, -0.16f);
            goTable.transform.localScale = new Vector3(1.83f, 0.03f, 0.91f);
            table.size = Vector3.one;
            Physics.SyncTransforms(); // autoSyncTransforms = 0 : sinon les bornes des colliders restent à l'origine

            try
            {
                Vector3 posInitiale = goTable.transform.position;
                Bounds b = table.bounds;
                for (int i = 0; i < 20; i++)
                {
                    Vector3 point = MainGuide.PointAleatoireSurTable(table, posInitiale, 0.05f);
                    Assert.GreaterOrEqual(point.x, b.min.x - 1e-4f, "X doit etre dans la table");
                    Assert.LessOrEqual(point.x, b.max.x + 1e-4f, "X doit etre dans la table");
                    Assert.GreaterOrEqual(point.z, b.min.z - 1e-4f, "Z doit etre dans la table");
                    Assert.LessOrEqual(point.z, b.max.z + 1e-4f, "Z doit etre dans la table");
                    Assert.AreEqual(b.max.y, point.y, 1e-4f, "Y doit etre sur la surface superieure");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(goTable);
            }
        }

        [Test]
        public void LimitesPiece_RameneLaTeteDansLaPiece()
        {
            Rect piece = Rect.MinMaxRect(-5f, -1.5f, 5f, 1f);
            Vector2 dehors = RemodelerLeReel.VR.VRLimitesPiece.Contraindre(new Vector2(7f, 3f), piece, 0.15f);
            Assert.AreEqual(4.85f, dehors.x, 1e-4f);
            Assert.AreEqual(0.85f, dehors.y, 1e-4f);
            Vector2 dedans = new Vector2(1f, 0f);
            Assert.AreEqual(dedans, RemodelerLeReel.VR.VRLimitesPiece.Contraindre(dedans, piece, 0.15f));
        }

        private static IEnumerable<ClipGeste> TousLesClips()
        {
            yield return GestesTutoriel.Saisir();
            yield return GestesTutoriel.Agrandir();
            yield return GestesTutoriel.Ranger();
            yield return GestesTutoriel.Dupliquer();
            yield return GestesTutoriel.Recuperer();
            yield return GestesTutoriel.Supprimer();
            yield return GestesTutoriel.TeleporterMicrogeste();
            yield return GestesTutoriel.TeleporterPistolet();
            yield return GestesTutoriel.Escalader();
        }

        [Test]
        public void Clips_UneMainAuMoinsEtTempsCroissants()
        {
            foreach (ClipGeste clip in TousLesClips())
            {
                Assert.IsTrue(clip.gauche != null || clip.droite != null);
                foreach (PisteMain piste in new[] { clip.gauche, clip.droite })
                {
                    if (piste == null) continue;
                    for (int i = 1; i < piste.cles.Count; i++) Assert.Greater(piste.cles[i].temps, piste.cles[i - 1].temps);
                    Assert.LessOrEqual(piste.cles[piste.cles.Count - 1].temps, clip.duree);
                }
            }
        }
    }
}
