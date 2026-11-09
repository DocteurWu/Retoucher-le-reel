// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  InstallerObjetsCeramique.cs — INSTALLE LE NIVEAU « CÉRAMIQUE » DANS LA SCÈNE 1 (outil d'éditeur)
//
//  RÔLE
//    La scène « céramique » a été segmentée avec SAGA : un décor SANS les objets
//    (ceramic_scene_seule) + les objets séparés. Cet outil, en un clic :
//      • remplace le décor de la scène ouverte par « ceramic_scene_seule » ;
//      • crée les objets saisissables (plante, pot, statue, tableau, tableau à agrandir) :
//        un parent (Rigidbody + BoxCollider ajusté) avec le splat de l'objet en enfant,
//        recalé EXACTEMENT sur le décor. Ils sont figés (kinematic) jusqu'à la première
//        saisie : sinon ceux posés sur une étagère tomberaient au sol dès le lancement ;
//      • la PORTE (charnière à droite vue de l'intérieur, s'ouvre vers l'intérieur) ;
//      • le COFFRE : caisse fixe + couvercle qui s'ouvre (charnière à l'arrière) ;
//      • la CLÉ (modèle simple en primitives dorées) cachée derrière le tableau ;
//      • les ÉTAPES : bac (épreuve déjà dans la scène), tableau à agrandir, plante dans le
//        pot, statue à supprimer, clé à trouver. Toutes faites → la porte se déverrouille ;
//        l'ouvrir en grand → Scene2 ;
//      • le SOL (collider « Sol », calque Ground) ramené aux dimensions de la pièce : la
//        téléportation ne vise que le sol et ignore les murs du splat ; avec un sol de
//        20 m on pouvait sortir par la porte verrouillée ou à travers un mur ;
//      • la NARRATION : présentation après l'introduction, puis la consigne de chaque
//        étape à son tour et une félicitation à chaque réussite (textes ci-dessous),
//        et « porte déverrouillée » à la fin. Les anciennes « Consigne » / « Bravo » de
//        la scène 1 sont retirées (elles parlaient du bac comme fin du niveau).
//    Relancer l'outil refait tout proprement (l'ancien groupe « Objets_Ceramique » est remplacé).
//
//  AVANT DE LANCER : créer les GaussianSplatAsset (Tools > Gaussian Splats > Create
//    GaussianSplatAsset) depuis Assets/Projet/Prefabs_Objets/Gaussian/ceramic_*.ply, dans
//    Assets/Projet/Scenes_Niveaux/Gaussian, puis OUVRIR Scene.de.base1.
//
//  LES MESURES (centre, taille, orientation de chaque boîte) ont été calculées depuis les PLY
//    dans le repère de la scène 1 (décor tourné de 180° autour de Z, y = -0,05). Elles se
//    retouchent ensuite à la main (Edit Collider). Le coffre a été coupé en deux PLY
//    (caisse / couvercle) à 0,375 m de haut.
// =====================================================================================
using System.Collections.Generic;
using System.Linq;
using GaussianSplatting.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RemodelerLeReel.Jeu;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.EditorTools
{
    public static class InstallerObjetsCeramique
    {
        private const string NomDecor = "ceramic_scene_seule";
        private const string NomGroupe = "Objets_Ceramique";
        private const string CheminMateriauCle = "Assets/Projet/Art_Graphismes/Materials/Mat_Cle_Or.mat";
        private const string NarrationPorte = "Porte déverrouillée";

        /// <summary>Anciennes narrations de la scène 1 remplacées par les consignes des étapes.</summary>
        private static readonly string[] NarrationsRemplacees = { "Consigne", "Bravo", "Clé trouvée", NarrationPorte };

        // ------------------------------------------------------------------
        // TEXTES DE LA NARRATION (à retoucher ici, puis relancer l'outil)
        // ------------------------------------------------------------------
        private const string TextePresentation =
            "Cette pièce ne te laissera pas partir comme ça. La porte du fond est fermée à clé : "
            + "remets tout en ordre, la liste au-dessus de la porte te montre ce qu'il reste à faire.";

        private const string ConsigneBac =
            "Commence par le bac, à ta droite : il lui faut 4 fraises. Pour copier un objet, "
            + "range-le dans la bulle de ton poignet (paume gauche tournée vers toi), puis touche ton index gauche avec l'index droit.";
        private const string BravoBac = "Le bac est plein.";

        private const string ConsigneTableau =
            "Ce petit tableau, au mur, est bien trop petit. Décroche-le, agrandis-le en le pinçant avec tes deux mains "
            + "(l'autre main doit le toucher), puis accroche-le dans le cadre lumineux.";
        private const string BravoTableau = "Voilà un tableau à sa juste mesure.";

        private const string ConsignePlante =
            "La plante étouffe sur son étagère. Rends-la au moins deux fois plus grande, puis pose-la sur le pot.";
        private const string BravoPlante = "La plante a trouvé sa place.";

        private const string ConsigneStatue =
            "Cette statue n'a rien à faire ici. Range-la dans la bulle de ton poignet, "
            + "puis touche ton majeur gauche avec l'index droit pour la faire disparaître.";
        private const string BravoStatue = "Plus de statue.";

        private const string ConsigneCle =
            "Il ne manque plus que la clé de la porte... Les tableaux cachent parfois des choses.";
        private const string BravoCle = "Une clé, cachée derrière le tableau !";

        private const string TextePorte = "Clic. La serrure a cédé : ouvre la porte en grand pour passer à la suite.";

        /// <summary>Un objet segmenté : nom de l'asset, boîte (m) dans le repère de la scène, masse.</summary>
        private struct Objet
        {
            public string asset, nom;
            public Vector3 centre;
            public float longueur, largeur, hauteur; // le long de z local, x local, y local
            public float angleY;
            public float masse;

            public Objet(string asset, string nom, float cx, float cy, float cz, float longueur, float largeur, float hauteur, float angleY, float masse)
            {
                this.asset = asset; this.nom = nom; centre = new Vector3(cx, cy, cz);
                this.longueur = longueur; this.largeur = largeur; this.hauteur = hauteur;
                this.angleY = angleY; this.masse = masse;
            }
        }

        private static readonly Objet Plante = new Objet("ceramic_plante", "Plante", -3.26f, 0.78f, -1.55f, 0.37f, 0.19f, 0.69f, -44f, 1.5f);
        private static readonly Objet Pot = new Objet("ceramic_pot2", "Pot", 2.67f, 1.14f, -1.44f, 0.26f, 0.18f, 0.18f, -11f, 1.0f);
        private static readonly Objet Statue = new Objet("ceramic_statue", "Statue", -1.05f, 1.47f, -1.54f, 0.09f, 0.07f, 0.24f, -87f, 1.0f);
        private static readonly Objet Tableau = new Objet("ceramic_tableau", "Tableau", -2.63f, 1.65f, -1.65f, 0.38f, 0.07f, 0.37f, -89f, 1.0f);
        private static readonly Objet TableauAgrandir = new Objet("ceramic_tableau_a_agrandir", "Tableau_A_Agrandir", -0.34f, 1.72f, 1.18f, 0.29f, 0.10f, 0.33f, -91f, 0.8f);

        // Porte : vantail de 0,95 m le long de z, épais de 0,20 m, haut de 1,93 m.
        private static readonly Objet Porte = new Objet("ceramic_Porte", "Porte", -5.26f, 1.09f, 0.67f, 0.95f, 0.20f, 1.93f, 1f, 0f);

        // Coffre (mesuré sur le PLY) : x 0..0,14 ; caisse y 0,24..0,375 ; couvercle 0,375..0,43 ; z -1,66..-1,205.
        private const string AssetCaisse = "ceramic_coffre_caisse";
        private const string AssetCouvercle = "ceramic_coffre_couvercle";

        /// <summary>Marge ajoutée à chaque dimension : les petits objets restent faciles à attraper.</summary>
        private const float Marge = 0.02f;

        [MenuItem("Retoucher le Reel/Outils/Installer les objets segmentés (scène céramique)")]
        public static void Installer()
        {
            // --- 1. Trouver le décor actuel et tous les assets ---
            GaussianSplatRenderer decor = Object.FindObjectsByType<GaussianSplatRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.GetComponentInParent<Rigidbody>() == null && r.transform.parent == null);
            if (decor == null)
            {
                EditorUtility.DisplayDialog("Niveau céramique", "Aucun décor Gaussian trouvé dans la scène ouverte. Ouvre Scene.de.base1.", "OK");
                return;
            }

            string[] noms = { NomDecor, Porte.asset, AssetCaisse, AssetCouvercle, Plante.asset, Pot.asset, Statue.asset, Tableau.asset, TableauAgrandir.asset };
            var assets = new Dictionary<string, GaussianSplatAsset>();
            var manquants = new List<string>();
            foreach (string nom in noms)
            {
                GaussianSplatAsset a = TrouverAsset(nom);
                if (a == null) manquants.Add(nom); else assets[nom] = a;
            }
            if (manquants.Count > 0)
            {
                EditorUtility.DisplayDialog("Niveau céramique",
                    "Assets Gaussian manquants :\n  • " + string.Join("\n  • ", manquants)
                    + "\n\nLes créer avec Tools > Gaussian Splats > Create GaussianSplatAsset "
                    + "(à partir des .ply de Assets/Projet/Prefabs_Objets/Gaussian), puis relancer.", "OK");
                return;
            }

            // --- 2. Nettoyer une éventuelle installation précédente ---
            foreach (GameObject ancien in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                         .Where(t => t.name == NomGroupe && t.parent == null).Select(t => t.gameObject).ToList())
            {
                Undo.DestroyObjectImmediate(ancien);
            }

            // --- 3. Le décor devient « la scène sans les objets » ---
            Undo.RecordObject(decor, "Décor sans objets");
            decor.m_Asset = assets[NomDecor];
            EditorUtility.SetDirty(decor);

            LimiterSolALaPiece();

            // --- 4. Les objets ---
            GameObject groupe = new GameObject(NomGroupe);
            Undo.RegisterCreatedObjectUndo(groupe, "Niveau céramique");
            Transform g = groupe.transform;
            int calque = Mathf.Max(0, LayerMask.NameToLayer("Grabbable"));

            VRPorte porte = CreerPorte(g, decor, assets[Porte.asset]);
            CreerCoffre(g, decor, assets[AssetCaisse], assets[AssetCouvercle]);

            Rigidbody plante = CreerObjetSaisissable(g, decor, assets[Plante.asset], Plante, calque);
            Rigidbody pot = CreerObjetSaisissable(g, decor, assets[Pot.asset], Pot, calque);
            Rigidbody statue = CreerObjetSaisissable(g, decor, assets[Statue.asset], Statue, calque);
            Rigidbody tableau = CreerObjetSaisissable(g, decor, assets[Tableau.asset], Tableau, calque);
            Rigidbody tableauAgrandir = CreerObjetSaisissable(g, decor, assets[TableauAgrandir.asset], TableauAgrandir, calque);
            Rigidbody cle = CreerCle(g, calque);

            // --- 5. Les étapes ---
            var etapes = new List<Epreuve>();

            EpreuveRecipient bac = Object.FindFirstObjectByType<EpreuveRecipient>();
            if (bac != null)
            {
                // Le bac n'est plus la fin du niveau : c'est la première étape.
                Undo.RecordObject(bac, "Bac = étape");
                bac.nomEtape = "Remplir le bac de fraises";
                bac.objetsRequis.RemoveAll(o => o.objet == null); // le cylindre a été retiré de la scène
                bac.consigne = ConsigneBac;
                bac.felicitation = BravoBac;
                bac.sceneSuivante = "";
                EditorUtility.SetDirty(bac);
                etapes.Add(bac);
            }

            EpreuveTableau epTableau = Ajouter<EpreuveTableau>(g, "Epreuve_Tableau", "Agrandir le tableau et l'accrocher");
            epTableau.tableau = tableauAgrandir;
            epTableau.emplacement = CreerEmplacementTableau(g);
            epTableau.ratioCible = 2.5f;
            Textes(epTableau, ConsigneTableau, BravoTableau);
            etapes.Add(epTableau);

            EpreuvePlante epPlante = Ajouter<EpreuvePlante>(g, "Epreuve_Plante", "Agrandir la plante (x2) et la mettre dans le pot");
            epPlante.plante = plante;
            epPlante.pot = pot;
            epPlante.ratioMin = 2f;
            Textes(epPlante, ConsignePlante, BravoPlante);
            etapes.Add(epPlante);

            EpreuveSuppression epStatue = Ajouter<EpreuveSuppression>(g, "Epreuve_Statue", "Faire disparaître la statue");
            epStatue.cible = statue.gameObject;
            Textes(epStatue, ConsigneStatue, BravoStatue);
            etapes.Add(epStatue);

            EpreuveCle epCle = Ajouter<EpreuveCle>(g, "Epreuve_Cle", "Trouver la clé");
            epCle.objet = cle;
            epCle.cachette = tableau.GetComponent<Collider>();
            Textes(epCle, ConsigneCle, BravoCle);
            etapes.Add(epCle);

            ProgressionEtapes progression = Ajouter<ProgressionEtapes>(g, "Progression", null);
            progression.etapes = etapes;
            progression.porte = porte;
            progression.sceneSuivante = "Scene2";
            GameObject ancre = new GameObject("Ancre_Panneau_Etapes");
            ancre.transform.SetParent(g, false);
            ancre.transform.position = Porte.centre + Vector3.up * 1.25f + Vector3.right * 0.15f; // au-dessus du vantail, côté pièce
            progression.ancrePanneau = ancre.transform;
            progression.presentation = TextePresentation;

            // --- 6. Narration ---
            progression.apresLaNarration = InstallerNarrations();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeObject = groupe;
            Debug.Log("[Niveau céramique] Installé : décor « " + NomDecor + " », porte, coffre, 5 objets saisissables, clé et "
                      + etapes.Count + " étapes. Sauvegarde la scène (Ctrl+S).");
        }

        // ------------------------------------------------------------------
        // Outils
        // ------------------------------------------------------------------
        private static GaussianSplatAsset TrouverAsset(string nom)
        {
            foreach (string guid in AssetDatabase.FindAssets(nom + " t:GaussianSplatAsset"))
            {
                GaussianSplatAsset a = AssetDatabase.LoadAssetAtPath<GaussianSplatAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && a.name == nom) return a;
            }
            return null;
        }

        /// <summary>
        /// Le plan « Sol » (Plane Unity = 10 × 10 m) couvre exactement l'intérieur de la pièce,
        /// mesuré sur le décor : x de -5,1 à 5,4 ; z de -1,55 à 1,1 (marge de 10 cm aux murs).
        /// </summary>
        private static void LimiterSolALaPiece()
        {
            Transform sol = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.name == "Sol" && c.gameObject.layer == LayerMask.NameToLayer("Ground"))
                .Select(c => c.transform).FirstOrDefault();
            if (sol == null)
            {
                Debug.LogWarning("[Niveau céramique] Pas de « Sol » (calque Ground) : la téléportation n'est pas limitée à la pièce.");
                return;
            }
            Undo.RecordObject(sol, "Sol à la taille de la pièce");
            sol.position = new Vector3(0.15f, 0f, -0.225f);
            sol.rotation = Quaternion.identity;
            sol.localScale = new Vector3(1.05f, 1f, 0.265f);
        }

        private static void Textes(Epreuve e, string consigne, string felicitation)
        {
            e.consigne = consigne;
            e.felicitation = felicitation;
        }

        /// <summary>Crée un objet vide avec le composant demandé (et son nom d'étape pour une épreuve).</summary>
        private static T Ajouter<T>(Transform groupe, string nom, string nomEtape) where T : MonoBehaviour
        {
            GameObject go = new GameObject(nom);
            go.transform.SetParent(groupe, false);
            Undo.RegisterCreatedObjectUndo(go, nom);
            T c = go.AddComponent<T>();
            if (c is Epreuve e && nomEtape != null) e.nomEtape = nomEtape;
            return c;
        }

        /// <summary>Copie du décor (mêmes shaders / réglages) pour un autre asset, à la MÊME pose monde.</summary>
        private static GameObject SplatEnfant(Transform parent, GaussianSplatRenderer decor, GaussianSplatAsset asset, string nom)
        {
            GameObject go = Object.Instantiate(decor.gameObject);
            go.name = nom;
            go.transform.SetParent(null);
            go.transform.SetPositionAndRotation(decor.transform.position, decor.transform.rotation);
            go.transform.localScale = decor.transform.lossyScale;
            go.transform.SetParent(parent, true);
            go.GetComponent<GaussianSplatRenderer>().m_Asset = asset;
            Undo.RegisterCreatedObjectUndo(go, "Splat " + nom);
            return go;
        }

        private static Rigidbody CreerObjetSaisissable(Transform groupe, GaussianSplatRenderer decor, GaussianSplatAsset asset, Objet o, int calque)
        {
            GameObject parent = new GameObject(o.nom);
            parent.transform.SetParent(groupe, false);
            parent.transform.SetPositionAndRotation(o.centre, Quaternion.Euler(0f, o.angleY, 0f));
            parent.layer = calque;

            SplatEnfant(parent.transform, decor, asset, o.nom + "_Splat").layer = calque;

            BoxCollider boite = parent.AddComponent<BoxCollider>();
            boite.size = new Vector3(o.largeur + Marge, o.hauteur + Marge, o.longueur + Marge);

            Rigidbody rb = RendreSaisissable(parent, o.masse);
            Undo.RegisterCreatedObjectUndo(parent, "Objet " + o.nom);
            return rb;
        }

        /// <summary>Rigidbody figé jusqu'à la 1re saisie + redimensionnement, respawn, haptique.</summary>
        private static Rigidbody RendreSaisissable(GameObject go, float masse)
        {
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = masse;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true; // figé jusqu'à la première saisie (VRGrabHand le rend dynamique au lâcher)

            VRScalable scalable = go.AddComponent<VRScalable>();
            scalable.body = rb;
            scalable.maxSizeRatio = 4f;
            go.AddComponent<VRThrowableRespawn>().minHeightBeforeRespawn = -2f;
            go.AddComponent<VRHapticInteractable>();
            return rb;
        }

        private static VRPorte CreerPorte(Transform groupe, GaussianSplatRenderer decor, GaussianSplatAsset asset)
        {
            Objet o = Porte;
            // Le joueur, dans la pièce, regarde la porte vers -x : sa DROITE est +z.
            // Charnière = bord +z ; l'origine regarde vers le bord libre (-z), d'où le demi-tour.
            Vector3 axe = Quaternion.Euler(0f, o.angleY, 0f) * Vector3.forward;
            Vector3 charniere = o.centre - axe * (o.longueur * 0.5f);
            charniere.y = 0f;

            GameObject racine = new GameObject("Porte");
            racine.transform.SetParent(groupe, false);
            racine.transform.SetPositionAndRotation(charniere, Quaternion.Euler(0f, o.angleY, 0f));

            SplatEnfant(racine.transform, decor, asset, "Porte_Splat");

            BoxCollider boite = racine.AddComponent<BoxCollider>();
            boite.size = new Vector3(o.largeur, o.hauteur, o.longueur);
            boite.center = new Vector3(0f, o.centre.y, o.longueur * 0.5f);

            VRPorte porte = racine.AddComponent<VRPorte>();
            porte.zonePrise = boite;
            porte.angleOuverture = 90f;
            // -1 : le bord libre (poignée, à gauche) part vers l'intérieur de la pièce (+x).
            // Si elle s'ouvre vers l'extérieur en casque : mettre +1.
            porte.sens = 1f;
            porte.verrouillee = true; // ProgressionEtapes la déverrouille quand toutes les étapes sont faites

            Undo.RegisterCreatedObjectUndo(racine, "Porte");
            return porte;
        }

        private static void CreerCoffre(Transform groupe, GaussianSplatRenderer decor, GaussianSplatAsset caisse, GaussianSplatAsset couvercle)
        {
            // Caisse : fixe (pas de Rigidbody), collider pour qu'on puisse poser des objets dessus.
            GameObject coffre = new GameObject("Coffre");
            coffre.transform.SetParent(groupe, false);
            coffre.transform.position = new Vector3(0.065f, 0.31f, -1.43f);
            SplatEnfant(coffre.transform, decor, caisse, "Coffre_Caisse_Splat");
            BoxCollider boite = coffre.AddComponent<BoxCollider>();
            boite.size = new Vector3(0.16f, 0.14f, 0.46f);
            Undo.RegisterCreatedObjectUndo(coffre, "Coffre");

            // Couvercle : charnière horizontale le long de x, sur l'arête arrière du dessus (côté mur).
            // Axe Y local = +x monde (axe de la charnière), Z local = vers l'avant (+z), X local = vers le bas.
            GameObject racine = new GameObject("Coffre_Couvercle");
            racine.transform.SetParent(coffre.transform, false);
            racine.transform.SetPositionAndRotation(new Vector3(0.065f, 0.43f, -1.655f), Quaternion.LookRotation(Vector3.forward, Vector3.right));
            SplatEnfant(racine.transform, decor, couvercle, "Coffre_Couvercle_Splat");

            BoxCollider zone = racine.AddComponent<BoxCollider>();
            zone.size = new Vector3(0.07f, 0.17f, 0.46f);
            zone.center = new Vector3(0.035f, 0f, 0.23f);

            VRPorte charniere = racine.AddComponent<VRPorte>();
            charniere.zonePrise = zone;
            charniere.angleOuverture = 100f;
            charniere.sens = -1f; // -1 : le bord avant monte
            charniere.rayonPrise = 0.1f;
            Undo.RegisterCreatedObjectUndo(racine, "Couvercle du coffre");
        }

        /// <summary>Clé dorée en primitives, à plat contre le mur, derrière le tableau.</summary>
        private static Rigidbody CreerCle(Transform groupe, int calque)
        {
            Material or = AssetDatabase.LoadAssetAtPath<Material>(CheminMateriauCle);
            if (or == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                or = new Material(lit != null ? lit : Shader.Find("Standard")) { name = "Mat_Cle_Or" };
                or.SetColor("_BaseColor", new Color(1f, 0.78f, 0.25f));
                or.color = new Color(1f, 0.78f, 0.25f);
                if (or.HasProperty("_Metallic")) or.SetFloat("_Metallic", 0.9f);
                if (or.HasProperty("_Smoothness")) or.SetFloat("_Smoothness", 0.75f);
                AssetDatabase.CreateAsset(or, CheminMateriauCle);
            }

            // Derrière le tableau (le tableau est contre le mur z ≈ -1,65, face vers +z) : 3 cm plus au fond.
            GameObject cle = new GameObject("Cle");
            cle.transform.SetParent(groupe, false);
            cle.transform.position = new Vector3(Tableau.centre.x, Tableau.centre.y - 0.03f, Tableau.centre.z - 0.065f);
            cle.layer = calque;

            // Repère local : longueur le long de x, face vers z.
            Morceau(cle.transform, PrimitiveType.Cylinder, "Anneau", new Vector3(-0.045f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.035f, 0.003f, 0.035f), or, calque);
            Morceau(cle.transform, PrimitiveType.Cylinder, "Tige", new Vector3(0.005f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.008f, 0.035f, 0.008f), or, calque);
            Morceau(cle.transform, PrimitiveType.Cube, "Dent_1", new Vector3(0.03f, -0.009f, 0f), Quaternion.identity, new Vector3(0.006f, 0.014f, 0.004f), or, calque);
            Morceau(cle.transform, PrimitiveType.Cube, "Dent_2", new Vector3(0.017f, -0.007f, 0f), Quaternion.identity, new Vector3(0.006f, 0.010f, 0.004f), or, calque);

            BoxCollider boite = cle.AddComponent<BoxCollider>();
            boite.size = new Vector3(0.115f, 0.04f, 0.02f);
            boite.center = new Vector3(-0.01f, 0f, 0f);

            Rigidbody rb = cle.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true; // accrochée au mur jusqu'à ce qu'on la prenne
            cle.AddComponent<VRThrowableRespawn>().minHeightBeforeRespawn = -2f;
            cle.AddComponent<VRHapticInteractable>();

            Undo.RegisterCreatedObjectUndo(cle, "Clé");
            return rb;
        }

        private static void Morceau(Transform parent, PrimitiveType type, string nom, Vector3 position, Quaternion rotation, Vector3 echelle, Material mat, int calque)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = nom;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // un seul collider : celui de la clé
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = echelle;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.layer = calque;
        }

        /// <summary>Cadre où accrocher le tableau agrandi : même mur que le petit tableau, sur une portion dégagée. Se déplace dans la vue Scene.</summary>
        private static Transform CreerEmplacementTableau(Transform groupe)
        {
            Objet o = TableauAgrandir;
            GameObject cadre = new GameObject("Emplacement_Tableau");
            cadre.transform.SetParent(groupe, false);
            Quaternion rot = Quaternion.Euler(0f, o.angleY, 0f);
            // Mesuré sur le décor : mur à z ≈ 1,10 et dégagé entre x = -2,8 et -2,1 (un élément du
            // décor dépasse entre -1,5 et -1). Agrandi ×2,5, le tableau a environ ±11 cm d'épaisseur
            // de splats : son centre doit rester devant le mur, sinon il passe dedans.
            cadre.transform.SetPositionAndRotation(new Vector3(-2.45f, 1.65f, 0.98f), rot);
            Undo.RegisterCreatedObjectUndo(cadre, "Emplacement tableau");
            return cadre.transform;
        }

        /// <summary>
        /// Retire les anciennes narrations remplacées, ajoute « porte déverrouillée », et renvoie
        /// l'introduction (les consignes démarrent quand elle se termine).
        /// </summary>
        private static NarrationSequence InstallerNarrations()
        {
            NarrationBulle bulle = Object.FindFirstObjectByType<NarrationBulle>();
            if (bulle == null)
            {
                Debug.LogWarning("[Niveau céramique] Pas d'objet Narration dans la scène : consignes après 4 s, sans introduction.");
                return null;
            }
            GameObject go = bulle.gameObject;

            foreach (NarrationSequence s in go.GetComponents<NarrationSequence>())
            {
                if (System.Array.IndexOf(NarrationsRemplacees, s.nom) >= 0) Undo.DestroyObjectImmediate(s);
            }

            NarrationSequence porte = Undo.AddComponent<NarrationSequence>(go);
            porte.nom = NarrationPorte;
            porte.declencheur = DeclencheurNarration.EtapesTerminees;
            porte.delai = 3f; // après la félicitation de la dernière étape
            porte.repliques = new List<Replique> { new Replique { texte = TextePorte, duree = 4f } };

            return go.GetComponents<NarrationSequence>().FirstOrDefault(s => s.declencheur == DeclencheurNarration.AuDemarrage);
        }
    }
}
