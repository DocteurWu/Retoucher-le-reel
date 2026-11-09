// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MainFantome.cs — UNE MAIN TRANSLUCIDE QUI MONTRE UN GESTE
//
//  RÔLE
//    Copie le visuel de main du rig (même maillage, mêmes os), lui retire tout ce qui la
//    lie au suivi réel et la rend translucide. Elle se pilote ensuite à la main : position
//    et orientation du poignet, pose des doigts. La main guide (MainGuide) en est une
//    version opaque, plus grande, qui sait marcher sur ses doigts et se poser sur un sol.
//
//  LES POSES (voir PoseMain) sont calculées une fois au lancement à partir du squelette :
//    • les doigts se plient autour de l'axe X local de leurs os (+X = vers la paume) ;
//    • le pouce cherche sa cible toute seul (cinématique inverse, méthode CCD) : bout de
//      l'index pour la pince, côté de l'index pour le microgeste. Aucune pose à animer.
//
//  AFFICHAGE : file transparente, dessinée après les splats (qui n'écrivent pas de
//    profondeur) : la main reste visible même devant un mur du scan.
//
//  CRÉÉE PAR : TutorielGestes.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Main translucide pilotée par le code.</summary>
    [AddComponentMenu("")]
    public class MainFantome : MonoBehaviour
    {
        private static readonly string[] Doigts = { "Index", "Middle", "Ring", "Little" };
        private static readonly string[] Phalanges = { "Proximal", "Intermediate", "Distal" };
        private static readonly string[] OsPouce = { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal" };

        /// <summary>Flexion (°) de chaque phalange pour un doigt complètement replié.</summary>
        private static readonly float[] FlexionPhalanges = { 70f, 90f, 50f };

        /// <summary>Flexion d'un doigt tendu : le visuel au repos est légèrement plié.</summary>
        private const float FlexionTendue = -0.2f;

        /// <summary>
        /// Microgeste : pouce posé sur le côté de l'index (côté pouce) pour le tap, et levé
        /// en l'air entre deux taps (m, depuis la phalange du milieu de l'index).
        /// </summary>
        private const float CotePouceTap = 0.009f, CotePouceLeve = 0.022f, HauteurPouceLeve = 0.018f;

        private const int IterationsPouce = 16;

        /// <summary>Marche : balancier avant/arrière de la phalange de base, et repli qui lève le doigt (°).</summary>
        private const float BalancierMarche = 22f, LeveeMarche = 40f;

        private Transform _poignet;
        private List<Transform> _os;
        private readonly List<Transform> _bouts = new List<Transform>();
        /// <summary>Os de chaque articulation XR Hands (ordre XRHandJointID), pour rejouer une prise.</summary>
        private Transform[] _articulations;
        private Quaternion[][] _poses;
        private Material _materiau;
        private bool _materiauPartage;
        private Color _couleur;

        /// <summary>
        /// Crée une main fantôme d'après un visuel du rig (null s'il n'a pas le squelette
        /// attendu). La copie naît sous un parent inactif : les composants de suivi retirés
        /// ne s'exécutent jamais. <paramref name="materiau"/> remplace le matériau translucide
        /// de cette couleur ; il appartient alors à la main, qui le détruira, sauf s'il est
        /// <paramref name="materiauPartage"/> (un asset du projet, ex. celui du prefab Main Guide).
        /// </summary>
        public static MainFantome Creer(GameObject visuel, bool gauche, Color couleur, Transform parent,
                                        Material materiau = null, float echelle = 1f, bool materiauPartage = false)
        {
            if (visuel == null) return null;

            GameObject couveuse = new GameObject("Couveuse_Main");
            couveuse.SetActive(false);
            GameObject copie = Instantiate(visuel, couveuse.transform);
            copie.name = gauche ? "Main_Fantome_G" : "Main_Fantome_D";

            string prefixe = gauche ? "L_" : "R_";
            Transform poignet = VRUtils.FindDeep(copie.transform, prefixe + "Wrist");
            SkinnedMeshRenderer peau = copie.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Quaternion[][] poses = poignet != null ? CalculerPoses(poignet, prefixe) : null;
            if (poses == null || peau == null)
            {
                Debug.LogWarning("[Tutoriel] Le visuel « " + visuel.name + " » n'a pas le squelette attendu : pas de main fantôme.");
                Destroy(couveuse);
                return null;
            }

            // Sans suivi : ni scripts, ni reflets d'interaction (seuls restent les os et la peau).
            for (int i = copie.transform.childCount - 1; i >= 0; i--)
            {
                Transform enfant = copie.transform.GetChild(i);
                if (enfant != poignet && enfant != peau.transform) DestroyImmediate(enfant.gameObject);
            }
            MonoBehaviour[] scripts = copie.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--) DestroyImmediate(scripts[i]);

            MainFantome main = copie.AddComponent<MainFantome>();
            main._poignet = poignet;
            main._os = OsDe(poignet, prefixe);
            main._poses = poses;
            main._couleur = couleur;
            main._materiau = materiau != null ? materiau : VRUtils.CreateUnlitMaterial(couleur);
            main._materiauPartage = materiau != null && materiauPartage;
            main._bouts.Add(VRUtils.FindDeep(poignet, prefixe + "ThumbTip"));
            foreach (string doigt in Doigts) main._bouts.Add(VRUtils.FindDeep(poignet, prefixe + doigt + "Tip"));
            main._articulations = new Transform[XRHandJointID.EndMarker.ToIndex()];
            for (int i = 0; i < main._articulations.Length; i++)
            {
                main._articulations[i] = VRUtils.FindDeep(poignet, prefixe + XRHandJointIDUtility.FromIndex(i));
            }
            Material[] materiaux = new Material[peau.sharedMaterials.Length];
            for (int i = 0; i < materiaux.Length; i++) materiaux[i] = main._materiau;
            peau.sharedMaterials = materiaux;
            peau.enabled = true;
            peau.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            peau.updateWhenOffscreen = true; // le poignet part loin de la boîte d'origine du maillage

            copie.transform.SetParent(parent, false);
            copie.transform.localPosition = Vector3.zero;
            copie.transform.localRotation = Quaternion.identity;
            copie.transform.localScale = Vector3.one * echelle;
            Destroy(couveuse);
            copie.SetActive(true);
            return main;
        }

        /// <summary>Les os pilotés : phalanges des quatre doigts, puis pouce. Null s'il en manque un.</summary>
        private static List<Transform> OsDe(Transform poignet, string prefixe)
        {
            var os = new List<Transform>();
            foreach (string doigt in Doigts)
            {
                foreach (string phalange in Phalanges) os.Add(VRUtils.FindDeep(poignet, prefixe + doigt + phalange));
            }
            foreach (string nom in OsPouce) os.Add(VRUtils.FindDeep(poignet, prefixe + nom));
            return os.Contains(null) ? null : os;
        }

        /// <summary>
        /// Calcule les rotations locales de tous les os pour chaque PoseMain (indexées par sa
        /// valeur). Les os sont rendus dans leur état d'origine. Null si le squelette est incomplet.
        /// </summary>
        public static Quaternion[][] CalculerPoses(Transform poignet, string prefixe)
        {
            List<Transform> os = OsDe(poignet, prefixe);
            Transform boutPouce = VRUtils.FindDeep(poignet, prefixe + "ThumbTip");
            Transform boutIndex = VRUtils.FindDeep(poignet, prefixe + "IndexTip");
            Transform indexMilieu = VRUtils.FindDeep(poignet, prefixe + "IndexIntermediate");
            Transform majeurMilieu = VRUtils.FindDeep(poignet, prefixe + "MiddleIntermediate");
            Transform annulaireMilieu = VRUtils.FindDeep(poignet, prefixe + "RingIntermediate");
            if (os == null || boutPouce == null || boutIndex == null || indexMilieu == null || majeurMilieu == null || annulaireMilieu == null) return null;

            Quaternion[] repos = new Quaternion[os.Count];
            for (int i = 0; i < os.Count; i++) repos[i] = os[i].localRotation;

            int nombre = System.Enum.GetValues(typeof(PoseMain)).Length;
            Quaternion[][] poses = new Quaternion[nombre][];
            for (int p = 0; p < nombre; p++)
            {
                PoseMain pose = (PoseMain)p;
                for (int i = 0; i < os.Count; i++) os[i].localRotation = repos[i];

                PlierDoigts(os, repos, FlexionDoigts(pose));
                switch (pose)
                {
                    case PoseMain.Poing:
                    case PoseMain.Pointe:
                        AmenerPouce(os, boutPouce, majeurMilieu.position);
                        break;
                    case PoseMain.SigneV:
                        // Index et majeur ouverts en V (mesuré, quel que soit le repère des os),
                        // puis le pouce vient tenir l'annulaire replié : dans un V, le majeur est
                        // tendu, le pouce qui le visait restait planté en l'air au milieu de la main.
                        EcarterEnV(os, poignet, prefixe);
                        AmenerPouce(os, boutPouce, annulaireMilieu.position);
                        break;
                    case PoseMain.Pince:
                        AmenerPouce(os, boutPouce, boutIndex.position);
                        break;
                    case PoseMain.MicroLeve:
                        AmenerPouce(os, boutPouce, indexMilieu.position + CotePouce(poignet, prefixe) * CotePouceLeve + poignet.up * HauteurPouceLeve);
                        break;
                    case PoseMain.MicroTap:
                        AmenerPouce(os, boutPouce, indexMilieu.position + CotePouce(poignet, prefixe) * CotePouceTap);
                        break;
                }

                poses[p] = new Quaternion[os.Count];
                for (int i = 0; i < os.Count; i++) poses[p][i] = os[i].localRotation;
            }

            for (int i = 0; i < os.Count; i++) os[i].localRotation = repos[i];
            return poses;
        }

        /// <summary>
        /// Signe V : écarte l'index et le majeur tendus, chacun de <see cref="DemiAngleV"/> degrés,
        /// dans le plan de la paume. Le sens est mesuré sur le squelette (l'index part du côté
        /// opposé au majeur, et inversement), sans supposer l'orientation des axes locaux des os.
        /// </summary>
        private static void EcarterEnV(List<Transform> os, Transform poignet, string prefixe)
        {
            int index = 0, majeur = Phalanges.Length;
            Transform boutIndex = VRUtils.FindDeep(poignet, prefixe + "IndexTip");
            Transform boutMajeur = VRUtils.FindDeep(poignet, prefixe + "MiddleTip");
            if (boutIndex == null || boutMajeur == null) return;

            Vector3 dos = poignet.up; // dos de la main (convention XR Hands), normale au plan de la paume
            Ecarter(os[index], boutIndex, boutIndex.position - boutMajeur.position, dos);
            Ecarter(os[majeur], boutMajeur, boutMajeur.position - boutIndex.position, dos);
        }

        /// <summary>Tourne la phalange de base pour que le bout du doigt parte vers <paramref name="cote"/>.</summary>
        private static void Ecarter(Transform baseDoigt, Transform bout, Vector3 cote, Vector3 dos)
        {
            Vector3 doigt = bout.position - baseDoigt.position;
            Vector3 laterale = Vector3.ProjectOnPlane(cote, doigt);
            laterale = Vector3.ProjectOnPlane(laterale, dos);
            if (laterale.sqrMagnitude < 1e-10f || doigt.sqrMagnitude < 1e-10f) return;
            // Axe = normale au plan (doigt, côté) : tourner autour amène le bout vers le côté voulu.
            Vector3 axe = Vector3.Cross(doigt, laterale).normalized;
            baseDoigt.rotation = Quaternion.AngleAxis(DemiAngleV, axe) * baseDoigt.rotation;
        }

        /// <summary>Demi-ouverture du V (°) : 2 × 13° entre l'index et le majeur, un V franc mais naturel.</summary>
        private const float DemiAngleV = 13f;

        /// <summary>Direction du côté pouce de la main (+X local pour la gauche, -X pour la droite).</summary>
        private static Vector3 CotePouce(Transform poignet, string prefixe)
        {
            return prefixe == "L_" ? poignet.right : -poignet.right;
        }

        /// <summary>Flexion de l'index, du majeur, de l'annulaire et de l'auriculaire (0 = repos, 1 = replié).</summary>
        private static float[] FlexionDoigts(PoseMain pose)
        {
            switch (pose)
            {
                case PoseMain.Poing: return new[] { 1f, 1f, 1f, 1f };
                case PoseMain.Pince: return new[] { 0.55f, 0.15f, 0.15f, 0.15f };
                case PoseMain.Pointe: return new[] { FlexionTendue, 1f, 1f, 1f };
                case PoseMain.SigneV: return new[] { FlexionTendue, FlexionTendue, 1f, 1f };
                case PoseMain.MicroLeve:
                case PoseMain.MicroTap: return new[] { 0.55f, 1f, 1f, 1f };
                case PoseMain.Griffe: return new[] { 0.35f, 0.35f, 0.35f, 0.35f };
                default: return new[] { FlexionTendue, FlexionTendue, FlexionTendue, FlexionTendue };
            }
        }

        private static void PlierDoigts(List<Transform> os, Quaternion[] repos, float[] flexions)
        {
            for (int d = 0; d < Doigts.Length; d++)
            {
                for (int p = 0; p < Phalanges.Length; p++)
                {
                    int i = d * Phalanges.Length + p;
                    os[i].localRotation = repos[i] * Quaternion.Euler(FlexionPhalanges[p] * flexions[d], 0f, 0f);
                }
            }
        }

        /// <summary>Cinématique inverse CCD : tourne les os du pouce, du bout vers la base, pour poser son bout sur la cible.</summary>
        private static void AmenerPouce(List<Transform> os, Transform bout, Vector3 cible)
        {
            int premier = Doigts.Length * Phalanges.Length;
            for (int iteration = 0; iteration < IterationsPouce; iteration++)
            {
                for (int i = os.Count - 1; i >= premier; i--)
                {
                    Vector3 versBout = bout.position - os[i].position;
                    Vector3 versCible = cible - os[i].position;
                    os[i].rotation = Quaternion.FromToRotation(versBout, versCible) * os[i].rotation;
                }
            }
        }

        /// <summary>Place le poignet en monde et applique un fondu entre deux poses.</summary>
        public void Placer(Vector3 position, Quaternion rotation, PoseMain poseA, PoseMain poseB, float melange)
        {
            _poignet.SetPositionAndRotation(position, rotation);
            Quaternion[] a = _poses[(int)poseA];
            Quaternion[] b = _poses[(int)poseB];
            for (int i = 0; i < _os.Count; i++) _os[i].localRotation = Quaternion.Slerp(a[i], b[i], melange);
        }

        /// <summary>
        /// Anime la marche par-dessus la pose appliquée par Placer : les doigts vont par paires
        /// alternées (index + annulaire, majeur + auriculaire). À appeler après Placer.
        /// </summary>
        public void Marcher(float phase, float amplitude)
        {
            for (int d = 0; d < Doigts.Length; d++)
            {
                float p = phase + (d % 2) * Mathf.PI;
                int proximale = d * Phalanges.Length;
                float levee = Mathf.Max(0f, Mathf.Sin(p));
                _os[proximale].localRotation *= Quaternion.Euler(-BalancierMarche * amplitude * Mathf.Cos(p), 0f, 0f);
                _os[proximale + 1].localRotation *= Quaternion.Euler(LeveeMarche * amplitude * levee, 0f, 0f);
            }
        }

        /// <summary>
        /// Replie les quatre doigts par-dessus la pose appliquée par Placer (0 = rien, 1 = poing) :
        /// main debout, paume vers le joueur, c'est le geste « viens ». À appeler après Placer.
        /// </summary>
        public void Replier(float flexion)
        {
            for (int d = 0; d < Doigts.Length; d++)
            {
                for (int p = 0; p < Phalanges.Length; p++)
                {
                    _os[d * Phalanges.Length + p].localRotation *= Quaternion.Euler(FlexionPhalanges[p] * flexion, 0f, 0f);
                }
            }
        }

        /// <summary>Monte ou descend le poignet pour que le bout de doigt le plus bas soit à cette hauteur. À appeler en dernier.</summary>
        public void PoserSur(float hauteur)
        {
            float plusBas = float.MaxValue;
            foreach (Transform bout in _bouts) plusBas = Mathf.Min(plusBas, bout.position.y);
            _poignet.position += Vector3.up * (hauteur - plusBas);
        }

        /// <summary>
        /// Rejoue une prise : chaque os prend la pose enregistrée de son articulation, comme le
        /// fait le suivi réel (XRHandSkeletonDriver), dans le repère donné. Ordre parent → enfants.
        /// </summary>
        public void PlacerArticulations(Pose[] poses, Vector3 origine, Quaternion rotation)
        {
            int n = Mathf.Min(poses.Length, _articulations.Length);
            for (int i = 0; i < n; i++)
            {
                Transform os = _articulations[i];
                if (os != null) os.SetPositionAndRotation(origine + rotation * poses[i].position, rotation * poses[i].rotation);
            }
        }

        /// <summary>Le poignet, pour y accrocher un accessoire.</summary>
        public Transform Poignet => _poignet;

        /// <summary>Opacité de la main (la couleur de départ donne l'opacité maximale).</summary>
        public void Opacite(float facteur)
        {
            if (_materiauPartage) return; // asset du projet : on ne le modifie pas
            Color c = _couleur;
            c.a *= facteur;
            _materiau.color = c;
        }

        private void OnDestroy()
        {
            if (_materiau != null && !_materiauPartage) Destroy(_materiau);
        }
    }
}
