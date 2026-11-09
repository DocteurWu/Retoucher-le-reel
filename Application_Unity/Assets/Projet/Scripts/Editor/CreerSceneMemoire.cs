// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  CreerSceneMemoire.cs — CRÉE LA SCENE3 « JEU DE MÉMOIRE » (La Grange, bibliothèque)
//
//  Menu : Retoucher le Reel > Outils > Créer la scène du jeu de mémoire (Scene3)
//    • copie Scene.de.base1 → Scene3 (rig, narration, EventSystem, lumière) si Scene3
//      n'existe pas, puis retire tout le niveau céramique (décor, objets, épreuves,
//      porte, main guide, murs) ;
//    • remplace la narration par l'accueil du jeu de mémoire ;
//    • installe la Grange si ses assets existent (dossier Gaussian/Grange) :
//        - décor « grange_decor » à la racine ;
//        - « Mobilier_Grange » : meubles fixes (splat + boîte de collision) ;
//        - « Objets_Memoire » : les objets du jeu (PLY suffixés « M » par la segmentation),
//          enveloppés au lancement par EpreuveMemoire ;
//        - « Sol » (collider seul, calque Ground) borné à la pièce ;
//    • ajoute « Epreuve_Memoire » (au milieu de la pièce = là où tombe le tas),
//      « Progression » (ProgressionEtapes sans porte : consigne, félicitation,
//      commandes de démo) et place le joueur face au tas ;
//    • ajoute Scene3 aux Build Settings.
//  Relançable : ne recrée que ce qui manque.
//
//  REPÈRE DE LA GRANGE (mesuré sur scene_seule.ply) : SAGA/COLMAP a l'axe Y vers le BAS.
//  Décor tourné de 180° en Z, échelle 0,3 (plafond à 3,4 m, canapé de 2,2 m), sol du scan
//  à y_ply = -1,481 ramené à y = 0. Pièce ≈ x [-6,1 ; 6,15], z [-3,33 ; 3,38].
//
//  DÉCOR : scene_seule.ply (1 Go, 4,4 M splats) fait planter l'import du plugin par manque
//  de mémoire. On importe « grange_decor.ply » : mêmes splats, harmoniques sphériques
//  gardées jusqu'à l'ordre 1 seulement (405 Mo).
// =====================================================================================
using System.Collections.Generic;
using System.Linq;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RemodelerLeReel.Jeu;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.EditorTools
{
    public static class CreerSceneMemoire
    {
        private const string Source = "Assets/Projet/Scenes_Niveaux/Scene.de.base1.unity";
        private const string Cible = "Assets/Projet/Scenes_Niveaux/Scene3.unity";
        private const string DossierGrange = "Assets/Projet/Scenes_Niveaux/Gaussian/Grange";

        // Repère de la Grange (voir en-tête).
        private const float Echelle = 0.3f;
        private const float SolPly = -1.481f;
        private static readonly Rect Piece = Rect.MinMaxRect(-6.1f, -3.33f, 6.15f, 3.38f);
        private static readonly Vector3 CentreTas = new Vector3(-0.6f, 0f, -1.6f);  // zone libre au sol
        private static readonly Vector3 Depart = new Vector3(1.6f, 0f, -0.6f);      // joueur, face au tas

        private const string Decor = "grange_decor";

        /// <summary>Prefab de la main guide (le même que dans Kit Scene VR et Scene.de.base1).</summary>
        private const string PrefabMainGuide = "Assets/Projet/Resources/Prefabs/Main Guide.prefab";
        /// <summary>Où elle surgit : au sol, entre le joueur et le tas (pas de table dans la Grange).</summary>
        private static readonly Vector3 PositionMainGuide = new Vector3(0.9f, 0f, -1.0f);

        /// <summary>Objets du jeu : ceux que la segmentation a suffixés « M ».</summary>
        private static readonly string[] ObjetsJeu =
        {
            "BoxM", "Chaise3M", "ChinesePlate1M", "ChinesePlate2M", "Lampe3M",
            "Painting1M", "Painting2M", "chaise2M", "lampeLongM", "statusAngleM",
        };

        /// <summary>Meubles fixes. Retirés : « coffre » (même objet que BoxM), « precomputed_mask » (= canape-vert).</summary>
        private static readonly string[] Mobilier =
        {
            "Books", "canape-vert", "canape_coussin_vert", "canape_droite", "canapevert2",
            "chaise", "extincteur", "lampe", "lampe2", "lustre", "tabouret",
        };

        /// <summary>Meubles sans boîte de collision (au plafond, ou segmentation trop large).</summary>
        private static readonly string[] SansCollision = { "lustre", "extincteur", "Books" };

        // Textes dits par la narration (voix : relancer « npm run generer » dans Outils/VoixNarration).
        private const string Accueil = "Bienvenue à la bibliothèque. Ici, pas besoin de force : c'est ta mémoire qui compte.";
        private const string Consigne = "Regarde bien la pièce et retiens où se trouve chaque objet. Dans quelques secondes, tout va bouger !";
        private const string Felicitation = "Bravo, tout est revenu à sa place ! Quelle mémoire !";

        /// <summary>Racines du niveau céramique, retirées de la copie de la scène 1.</summary>
        private static readonly string[] RacinesCeramique =
        {
            "Objets_Ceramique", "Limites_Piece", "Gaussian TEST", "Object", "Epreuve",
        };

        [MenuItem("Retoucher le Reel/Outils/Créer la scène du jeu de mémoire (Scene3)")]
        public static void Creer()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool nouvelle = AssetDatabase.LoadAssetAtPath<SceneAsset>(Cible) == null;
            if (nouvelle && !AssetDatabase.CopyAsset(Source, Cible))
            {
                Debug.LogError("[Memoire] Impossible de copier " + Source + " vers " + Cible + ".");
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(Cible, OpenSceneMode.Single);

            if (nouvelle)
            {
                RetirerNiveauCeramique(scene);
                RemplacerNarration();
            }

            InstallerGrange(scene);
            GameObject objets = Racine(scene, "Objets_Memoire") ?? new GameObject("Objets_Memoire");

            // Épreuve, au milieu de la pièce (là où tombe le tas)
            GameObject epreuveGo = Racine(scene, "Epreuve_Memoire");
            if (epreuveGo == null)
            {
                epreuveGo = new GameObject("Epreuve_Memoire");
                epreuveGo.transform.position = CentreTas;
            }
            EpreuveMemoire memoire = epreuveGo.GetComponent<EpreuveMemoire>() ?? epreuveGo.AddComponent<EpreuveMemoire>();
            memoire.conteneur = objets.transform;
            memoire.nomEtape = "Remettre les objets à leur place";
            memoire.consigne = Consigne;
            memoire.felicitation = Felicitation;
            memoire.dureeObservation = 30f; // 10 objets à retenir

            // Joueur face au tas
            GameObject rig = Racine(scene, "XR Origin (VR)");
            if (rig != null && nouvelle)
            {
                Vector3 vers = CentreTas - Depart;
                rig.transform.SetPositionAndRotation(Depart, Quaternion.LookRotation(new Vector3(vers.x, 0f, vers.z)));
            }

            // Progression sans porte : annonce consigne et félicitation, commandes de démo (H, 1, N…)
            GameObject progGo = Racine(scene, "Progression") ?? new GameObject("Progression");
            ProgressionEtapes prog = progGo.GetComponent<ProgressionEtapes>() ?? progGo.AddComponent<ProgressionEtapes>();
            prog.etapes = new List<Epreuve> { memoire };
            prog.porte = null;
            prog.sceneSuivante = "";
            prog.mainGuide = false;      // elle montrerait les objets : ce serait tricher
            prog.afficherListe = false;  // pas de porte au-dessus de laquelle l'afficher
            prog.commandesDemo = true;
            prog.delaiPremiereConsigne = 1f;
            NarrationSequence accueil = Object.FindObjectsByType<NarrationSequence>(FindObjectsSortMode.None)
                .FirstOrDefault(s => s.nom == "Accueil mémoire");
            prog.apresLaNarration = accueil;
            memoire.delaiDepart = accueil != null ? 12f : 6f; // l'accueil puis la consigne sont dits avant le compte à rebours

            PoserMainGuide(scene);

            AjouterAuxBuildSettings();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = objets;
            Debug.Log("[Memoire] Scene3 prête (" + objets.transform.childCount + " objets à retenir).");
        }

        // ------------------------------------------------------------------
        // Nettoyage de la copie de la scène 1
        // ------------------------------------------------------------------
        private static void RetirerNiveauCeramique(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (RacinesCeramique.Contains(go.name.Trim())) Object.DestroyImmediate(go);
            }
            // Ce qui resterait ailleurs dans la hiérarchie : épreuves, progression, portes, main guide.
            foreach (Epreuve e in Object.FindObjectsByType<Epreuve>(FindObjectsSortMode.None)) if (e != null) Object.DestroyImmediate(e.gameObject);
            foreach (ProgressionEtapes p in Object.FindObjectsByType<ProgressionEtapes>(FindObjectsSortMode.None)) if (p != null) Object.DestroyImmediate(p.gameObject);
            foreach (VRPorte p in Object.FindObjectsByType<VRPorte>(FindObjectsSortMode.None)) if (p != null) Object.DestroyImmediate(p.gameObject);
            // Splats restés à la racine (décor de la scène 1)
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.GetComponent<GaussianSplatRenderer>() != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>Narration de la scène 1 retirée, remplacée par l'accueil du jeu de mémoire.</summary>
        private static void RemplacerNarration()
        {
            NarrationSequence[] anciennes = Object.FindObjectsByType<NarrationSequence>(FindObjectsSortMode.None);
            GameObject porteur = anciennes.Length > 0 ? anciennes[0].gameObject : null;
            if (porteur == null)
            {
                NarrationBulle bulle = Object.FindFirstObjectByType<NarrationBulle>();
                if (bulle == null) return;
                porteur = bulle.gameObject;
            }
            foreach (NarrationSequence s in anciennes) Object.DestroyImmediate(s);
            NarrationSequence accueil = porteur.AddComponent<NarrationSequence>();
            accueil.nom = "Accueil mémoire";
            accueil.declencheur = DeclencheurNarration.AuDemarrage;
            accueil.delai = 3f;
            accueil.repliques = new List<Replique> { new Replique { texte = Accueil } };
        }

        // ------------------------------------------------------------------
        // La Grange
        // ------------------------------------------------------------------
        private static void InstallerGrange(Scene scene)
        {
            // Décor
            if (Racine(scene, Decor) == null)
            {
                // Version allégée (AllegerSplat, 60 % des splats) si elle existe : le décor complet
                // (4,4 M splats) est trop lourd pour le casque.
                GaussianSplatAsset asset = Charger(Decor + "_leger");
                if (asset == null) asset = Charger(Decor);
                if (asset != null) CreerSplat(Decor, asset, null);
                else Debug.LogWarning("[Memoire] Décor « " + Decor + " » pas encore importé dans " + DossierGrange + " : relancer le menu après l'import.");
            }

            // Mobilier fixe
            GameObject mobilier = Racine(scene, "Mobilier_Grange") ?? new GameObject("Mobilier_Grange");
            foreach (string nom in Mobilier)
            {
                if (mobilier.transform.Find(nom) != null) continue;
                GaussianSplatAsset asset = Charger(nom);
                if (asset == null) continue;
                GameObject go = CreerSplat(nom, asset, mobilier.transform);
                if (!SansCollision.Contains(nom)) AjouterBoite(go, asset);
            }

            // Objets du jeu : splat seul, EpreuveMemoire l'enveloppe au lancement (Rigidbody + boîte)
            GameObject objets = Racine(scene, "Objets_Memoire") ?? new GameObject("Objets_Memoire");
            foreach (string nom in ObjetsJeu)
            {
                if (objets.transform.Find(nom) != null) continue;
                GaussianSplatAsset asset = Charger(nom);
                if (asset != null) CreerSplat(nom, asset, objets.transform);
            }

            // Sol borné à la pièce : la téléportation et VRLimitesPiece s'y calent.
            if (Racine(scene, "Sol") == null)
            {
                GameObject sol = new GameObject("Sol");
                int calque = LayerMask.NameToLayer("Ground");
                if (calque >= 0) sol.layer = calque;
                sol.transform.position = new Vector3(Piece.center.x, -0.05f, Piece.center.y);
                BoxCollider b = sol.AddComponent<BoxCollider>();
                b.size = new Vector3(Piece.width, 0.1f, Piece.height);
            }
        }

        /// <summary>
        /// Main guide = le prefab (modèle et matériaux réglés), posé au sol. Elle ne se montre que si le
        /// dev l'allume (case guideVersLaPlace d'Epreuve_Memoire, ou touche G du panneau de démo).
        /// </summary>
        private static void PoserMainGuide(Scene scene)
        {
            MainGuide existante = Object.FindFirstObjectByType<MainGuide>(FindObjectsInactive.Include);
            GameObject go = existante != null ? existante.gameObject : null;
            if (go == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabMainGuide);
                if (prefab == null)
                {
                    Debug.LogWarning("[Memoire] Prefab introuvable : " + PrefabMainGuide);
                    return;
                }
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            }
            go.transform.SetPositionAndRotation(PositionMainGuide, Quaternion.Euler(90f, 0f, 0f));
        }

        private static GaussianSplatAsset Charger(string nom) =>
            AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(DossierGrange + "/" + nom + ".asset");

        /// <summary>Splat posé dans le repère de la Grange (tous les PLY segmentés partagent celui du décor).</summary>
        private static GameObject CreerSplat(string nom, GaussianSplatAsset asset, Transform parent)
        {
            GameObject go = new GameObject(nom);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(0f, Echelle * SolPly, 0f), Quaternion.Euler(0f, 0f, 180f));
            go.transform.localScale = Vector3.one * Echelle;
            GaussianSplatRenderer r = go.AddComponent<GaussianSplatRenderer>();
            r.m_Asset = asset;
            return go;
        }

        /// <summary>Boîte de collision (enfant aligné sur le monde) calée sur les bornes du splat.</summary>
        private static void AjouterBoite(GameObject splat, GaussianSplatAsset asset)
        {
            Vector3 min = asset.boundsMin, max = asset.boundsMax;
            Bounds b = default;
            for (int i = 0; i < 8; i++)
            {
                Vector3 coin = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 p = splat.transform.TransformPoint(coin);
                if (i == 0) b = new Bounds(p, Vector3.zero); else b.Encapsulate(p);
            }
            GameObject boite = new GameObject("Collision");
            boite.transform.SetParent(splat.transform, false);
            boite.transform.SetPositionAndRotation(b.center, Quaternion.identity);
            boite.transform.localScale = new Vector3(1f / Echelle, 1f / Echelle, 1f / Echelle); // annule l'échelle du splat
            BoxCollider c = boite.AddComponent<BoxCollider>();
            c.size = b.size;
        }

        private static GameObject Racine(Scene scene, string nom) =>
            scene.GetRootGameObjects().FirstOrDefault(g => g.name.Trim() == nom);

        private static void AjouterAuxBuildSettings()
        {
            // Par GUID : une scène supprimée puis recréée garde sinon l'ancien GUID dans la liste.
            var liste = EditorBuildSettings.scenes.Where(s => s.path != Cible).ToList();
            liste.Add(new EditorBuildSettingsScene(new GUID(AssetDatabase.AssetPathToGUID(Cible)), true));
            EditorBuildSettings.scenes = liste.ToArray();
        }
    }
}
