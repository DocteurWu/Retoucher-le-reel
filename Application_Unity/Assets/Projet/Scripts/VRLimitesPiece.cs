// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRLimitesPiece.cs — SOL, MURS ET PLAFOND DE CHAQUE SCÈNE (créés au lancement)
//
//  RÔLE
//    Un décor Gaussian Splat n'a aucune collision. Dans chaque scène, ce script gère :
//      • un SOL épais (calque Ground, dessus à y = 0) qui couvre toute la pièce :
//        la téléportation le vise et la gravité s'y pose, il ne se traverse pas ;
//      • quatre MURS et un PLAFOND (calque Environment) aux bords de la pièce :
//        les objets rebondissent dessus, l'escalade s'y arrête, la visée s'y bloque ;
//      • une LIMITE : si la tête du joueur sort de la pièce (marche réelle, stick,
//        escalade), le rig est ramené à l'intérieur.
//    Une chute sous le sol est rattrapée (retour au dernier point sûr) et notée dans le
//    journal : « [Limites] ».
//
//  DÉTECTION DYNAMIQUE :
//    Inspecte la hiérarchie pour trouver « Sol », « Mur_Nord », « Mur_Sud », « Mur_Est »,
//    « Mur_Ouest » et « Plafond ». L'utilisateur peut ainsi les déplacer ou les redimensionner
//    directement dans l'éditeur. Si aucun n'est trouvé, utilise les dimensions par défaut.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RemodelerLeReel.VR
{
    /// <summary>Sol, murs, plafond et limite de déplacement de la pièce de la scène.</summary>
    [AddComponentMenu("")]
    public class VRLimitesPiece : MonoBehaviour
    {
        /// <summary>Intérieur de la pièce (x, z ; y ignoré) de chaque scène, mesuré sur son décor.</summary>
        private static readonly Dictionary<string, Rect> Pieces = new Dictionary<string, Rect>
        {
            // Céramique : x de -5,1 à 5,4 ; z de -1,55 à 1,1 (mesures du « Sol » de l'installeur).
            { "Scene.de.base1", Rect.MinMaxRect(-5.1f, -1.55f, 5.4f, 1.1f) },
            // Salle « central2 » : sol du splat de x -13,2 à 18,8 ; z -9,3 à 8,6.
            { "Scene2", Rect.MinMaxRect(-13.2f, -9.3f, 18.8f, 8.6f) },
            // Menu (Forge Haute) : sol agrandi à 24 m × 24 m (x de -12 à 12, z de -12 à 12) entouré de murs.
            { "Menu", Rect.MinMaxRect(-12f, -12f, 12f, 12f) },
        };

        /// <summary>
        /// Plafond du SCAN (m) quand il est plus bas que les murs de sécurité (6 m) et qu'aucun
        /// « Plafond » n'est posé dans la scène. Grange (Scene3) : 3,4 m (mesuré sur le PLY) ;
        /// sans lui, un objet lancé traversait le plafond visible et retombait.
        /// </summary>
        private static readonly Dictionary<string, float> PlafondsScan = new Dictionary<string, float>
        {
            { "Scene3", 3.4f },
        };

        private const float TaillePieceParDefaut = 24f;
        private const float EpaisseurSol = 0.4f, DebordSol = 0f;
        private const float HauteurMurs = 6f, EpaisseurMurs = 0.3f;
        private const float MargeTete = 0.15f;
        private const float HauteurChute = -1f;

        /// <summary>Intérieur de la pièce de la scène active (x, z), une fois connu.</summary>
        public static Rect? Interieur => s_instance != null ? s_instance._piece : (Rect?)null;

        /// <summary>Hauteur sous plafond détectée dans la scène active.</summary>
        public static float HauteurPlafondScene => s_instance != null ? s_instance._hauteurPlafond : HauteurMurs;

        private static VRLimitesPiece s_instance;

        private Rect _piece;
        private float _hauteurPlafond = HauteurMurs;
        private Transform _rig;
        private Vector3 _dernierPointSur;
        private bool _pret;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialiser()
        {
            Creer(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= SurSceneChargee;
            SceneManager.sceneLoaded += SurSceneChargee;
        }

        private static void SurSceneChargee(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single) Creer(scene);
        }

        private static void Creer(Scene scene)
        {
            if (s_instance != null && s_instance.gameObject.scene == scene) return;
            GameObject go = new GameObject("Limites_Piece_Auto");
            SceneManager.MoveGameObjectToScene(go, scene); // disparaît avec la scène
            s_instance = go.AddComponent<VRLimitesPiece>();
        }

        private void Start()
        {
            Camera cam = Camera.main;
            _rig = cam != null && cam.transform.parent != null
                ? (cam.transform.parent.parent != null ? cam.transform.parent.parent : cam.transform.parent)
                : null;

            string nom = gameObject.scene.name;
            if (DetecterLimitesScene(out Rect detectee, out bool mursPoses))
            {
                _piece = detectee;
                // Murs posés dans la hiérarchie (prefab Limites_Piece) : pas de doublons créés par code.
                // « Sol » seul (prefab Kit Scene VR) : ses bornes donnent la pièce, les murs restent à créer.
                if (!mursPoses) ConstruireSolEtMurs();
            }
            else
            {
                if (Pieces.TryGetValue(nom, out Rect piece)) _piece = piece;
                else _piece = PieceParDefaut();

                ConstruireSolEtMurs();
            }
            PoserPlafondScan(nom, plafondPose: _hauteurPlafond != HauteurMurs);

            _dernierPointSur = _rig != null ? _rig.position : Vector3.zero;
            _pret = true;
            Debug.Log("[Limites] Scène « " + nom + " » : pièce x " + _piece.xMin.ToString("F2") + " → " + _piece.xMax.ToString("F2")
                      + ", z " + _piece.yMin.ToString("F2") + " → " + _piece.yMax.ToString("F2")
                      + ", plafond y=" + _hauteurPlafond.ToString("F2")
                      + (_rig != null ? ", joueur en " + _rig.position.ToString("F2") : ", pas de rig trouvé"));
        }

        /// <summary>
        /// Détecte les murs, le sol et le plafond déjà placés dans la hiérarchie de la scène.
        /// Permet au concepteur de déplacer les limites librement dans l'éditeur.
        /// </summary>
        private bool DetecterLimitesScene(out Rect piece, out bool mursPoses)
        {
            piece = default;
            mursPoses = false;
            Collider nord = null, sud = null, est = null, ouest = null;
            Collider sol = null;
            Collider plafond = null;

            foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!c.enabled) continue;
                string n = c.gameObject.name;
                if (n == "Mur_Nord" || n.EndsWith("_Nord")) nord = c;
                else if (n == "Mur_Sud" || n.EndsWith("_Sud")) sud = c;
                else if (n == "Mur_Est" || n.EndsWith("_Est")) est = c;
                else if (n == "Mur_Ouest" || n.EndsWith("_Ouest")) ouest = c;
                else if (n == "Sol") sol = c;
                else if (n == "Plafond" || n.EndsWith("_Plafond")) plafond = c;
            }

            if (plafond != null)
            {
                _hauteurPlafond = plafond.bounds.min.y;
            }

            if (nord != null && sud != null && est != null && ouest != null)
            {
                float xMin = ouest.bounds.max.x;
                float xMax = est.bounds.min.x;
                float zMin = sud.bounds.max.z;
                float zMax = nord.bounds.min.z;
                if (xMin < xMax && zMin < zMax)
                {
                    piece = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
                    mursPoses = true;
                    return true;
                }
            }

            if (sol != null)
            {
                Bounds b = sol.bounds;
                piece = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                return true;
            }

            return false;
        }

        /// <summary>Collider « Sol » de la scène s'il existe, sinon un carré autour du joueur.</summary>
        private Rect PieceParDefaut()
        {
            foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.gameObject.name != "Sol") continue;
                Bounds b = c.bounds;
                return Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            }
            Vector3 centre = _rig != null ? _rig.position : Vector3.zero;
            float m = TaillePieceParDefaut * 0.5f;
            return Rect.MinMaxRect(centre.x - m, centre.z - m, centre.x + m, centre.z + m);
        }

        private void ConstruireSolEtMurs()
        {
            int ground = LayerMask.NameToLayer("Ground");
            int environment = LayerMask.NameToLayer("Environment");
            Vector2 centre = _piece.center;
            float largeur = _piece.width, profondeur = _piece.height;

            Boite("Sol_Securite", ground >= 0 ? ground : 6,
                  new Vector3(centre.x, -EpaisseurSol * 0.5f, centre.y),
                  new Vector3(largeur + DebordSol * 2f, EpaisseurSol, profondeur + DebordSol * 2f));

            int calqueMur = environment >= 0 ? environment : 0;
            float y = HauteurMurs * 0.5f, e = EpaisseurMurs;
            Boite("Mur_Ouest", calqueMur, new Vector3(_piece.xMin - e * 0.5f, y, centre.y), new Vector3(e, HauteurMurs, profondeur + e * 2f));
            Boite("Mur_Est", calqueMur, new Vector3(_piece.xMax + e * 0.5f, y, centre.y), new Vector3(e, HauteurMurs, profondeur + e * 2f));
            Boite("Mur_Sud", calqueMur, new Vector3(centre.x, y, _piece.yMin - e * 0.5f), new Vector3(largeur + e * 2f, HauteurMurs, e));
            Boite("Mur_Nord", calqueMur, new Vector3(centre.x, y, _piece.yMax + e * 0.5f), new Vector3(largeur + e * 2f, HauteurMurs, e));
            Boite("Plafond_Securite", calqueMur, new Vector3(centre.x, HauteurMurs + e * 0.5f, centre.y), new Vector3(largeur + e * 2f, e, profondeur + e * 2f));
            Physics.SyncTransforms(); // autoSyncTransforms = 0 : les colliders doivent exister dès cette frame
        }

        /// <summary>
        /// Plafond à la hauteur du scan (table PlafondsScan), sur le calque Ignore Raycast : les
        /// objets rebondissent dessus mais l'arc de téléportation le traverse (il monte jusqu'à
        /// ~4 m quand on vise haut : arrêté à 3,4 m, il rendrait les grands sauts impossibles).
        /// La tête est aussi bloquée dessous.
        /// </summary>
        private void PoserPlafondScan(string nomScene, bool plafondPose)
        {
            if (plafondPose || !PlafondsScan.TryGetValue(nomScene, out float h)) return;
            _hauteurPlafond = h;
            int calque = LayerMask.NameToLayer("Ignore Raycast");
            float e = EpaisseurMurs;
            Boite("Plafond_Scan", calque >= 0 ? calque : 2, new Vector3(_piece.center.x, h + e * 0.5f, _piece.center.y),
                  new Vector3(_piece.width + e * 2f, e, _piece.height + e * 2f));
            Physics.SyncTransforms();
        }

        private void Boite(string nom, int calque, Vector3 centre, Vector3 taille)
        {
            GameObject go = new GameObject(nom);
            go.layer = calque;
            go.transform.SetParent(transform, false);
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = taille;
        }

        private void LateUpdate()
        {
            if (!_pret || _rig == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 rig = _rig.position;

            // Chute sous le sol (ne devrait plus arriver) : retour au dernier point sûr, noté pour comprendre.
            if (rig.y < HauteurChute)
            {
                Debug.LogWarning("[Limites] Chute rattrapée : joueur en " + rig.ToString("F2") + ", ramené en " + _dernierPointSur.ToString("F2") + ".", this);
                VRUtils.MoveRig(_rig, _dernierPointSur);
                return;
            }

            // La tête reste dans la pièce : on décale le rig du dépassement horizontal et vertical.
            Vector3 tete = cam.transform.position;
            Vector2 dedans = Contraindre(new Vector2(tete.x, tete.z), _piece, MargeTete);
            Vector3 decalage = new Vector3(dedans.x - tete.x, 0f, dedans.y - tete.z);

            // Bloquer la tête sous le plafond
            if (tete.y > _hauteurPlafond - MargeTete)
            {
                decalage.y = (_hauteurPlafond - MargeTete) - tete.y;
            }

            if (decalage.sqrMagnitude > 1e-8f)
            {
                rig += decalage;
                VRUtils.MoveRig(_rig, rig);
            }

            // Point sûr : posé au sol, la tête dans la pièce (l'origine du rig, elle, peut être
            // dehors : c'est le centre de l'espace de jeu, parfois à plusieurs mètres du joueur).
            if (Mathf.Abs(rig.y) < 0.05f) _dernierPointSur = rig;
        }

        /// <summary>Ramène un point (x, z) dans le rectangle, réduit d'une marge sur chaque bord.</summary>
        public static Vector2 Contraindre(Vector2 point, Rect piece, float marge)
        {
            float xMin = piece.xMin + marge, xMax = piece.xMax - marge;
            float zMin = piece.yMin + marge, zMax = piece.yMax - marge;
            if (xMin > xMax) xMin = xMax = piece.center.x;
            if (zMin > zMax) zMin = zMax = piece.center.y;
            return new Vector2(Mathf.Clamp(point.x, xMin, xMax), Mathf.Clamp(point.y, zMin, zMax));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            float h = _hauteurPlafond > 0f ? _hauteurPlafond : HauteurMurs;
            Vector3 center = new Vector3(_piece.center.x, h * 0.5f, _piece.center.y);
            Vector3 size = new Vector3(_piece.width, h, _piece.height);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
