// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EpreuveTableau.cs — ÉPREUVE « AGRANDIR LE TABLEAU ET L'ACCROCHER AU MUR »
//
//  RÔLE
//    Le joueur doit :
//      1. AGRANDIR le tableau (pincement à deux mains, voir VRScaleGrab) jusqu'à la
//         taille voulue (« ratioCible », ex. ×2,5 la taille de départ) ;
//      2. le poser contre le mur, dans le cadre vert qui marque l'emplacement.
//    Une COPIE du tableau convient aussi : original et copies (même famille, voir
//    VRCloneOrigin) sont tous mesurés par rapport à la taille de départ de l'original.
//    Quand la taille ET la position sont bonnes, le tableau s'accroche tout seul
//    (il se plaque au mur, figé), puis : son, panneau « Bravo », événement pour la
//    narration (déclencheur « Epreuve Reussie ») et UnityEvent « A La Reussite ».
//
//  À POSER SUR : un objet vide « Epreuve_Tableau ». Le tableau doit avoir un Rigidbody,
//    un BoxCollider et un VRScalable (l'échelle du GameObject du tableau est ce qui
//    est mesuré). « Emplacement » = objet vide placé sur le mur, AVEC LA MÊME
//    ORIENTATION que le tableau quand il est accroché ; son gizmo (cadre vert à la
//    taille cible) se voit dans la vue Scene.
//
//  FEEDBACK EN JEU : un cadre (blanc → orange quand la position est bonne mais pas la
//    taille → vert quand tout est bon) et un panneau « Taille ×1,0 / ×2,5 ».
// =====================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Épreuve : agrandir un tableau puis l'accrocher au mur.</summary>
    [AddComponentMenu("Remodeler le Reel/Jeu/Épreuve - Tableau à agrandir")]
    public class EpreuveTableau : Epreuve
    {

        [Header("Objets")]
        [Tooltip("Le tableau (Rigidbody + BoxCollider + VRScalable).")]
        public Rigidbody tableau;

        [Tooltip("Emplacement sur le mur : même orientation que le tableau accroché, centre du cadre.")]
        public Transform emplacement;

        [Header("Objectif")]
        [Min(1.01f)]
        [Tooltip("Taille voulue, en multiple de la taille de départ du tableau.")]
        public float ratioCible = 2.5f;

        [Range(0.02f, 0.5f)]
        [Tooltip("Tolérance sur la taille (0,15 = ±15 %).")]
        public float toleranceTaille = 0.20f;

        [Min(0.05f)]
        [Tooltip("Distance max (m) entre le centre du tableau et le centre du cadre.")]
        public float distanceAccroche = 0.35f;

        [Range(5f, 90f)]
        [Tooltip("Écart d'orientation max (degrés) avec le cadre.")]
        public float angleMax = 45f;

        [Header("Suite")]
        [Tooltip("Scène chargée après la réussite. Vide = rester ici.")]
        public string sceneSuivante = "";

        [Min(0f)] public float delaiAvantScene = 4f;
        public UnityEvent aLaReussite = new UnityEvent();


        private int _famille;           // famille du tableau : original + copies
        private float _tailleReference; // taille « ×1 » : celle de l'original au départ
        private BoxCollider _boite;     // boîte de l'original au départ (cadre, axe mince)
        private VRScalable _scalable;
        private LineRenderer _cadre;
        private TextMeshPro _panneau;
        private int _axeEpais;          // axe local le plus mince du tableau (= normale du mur)
        private Vector2 _tailleDepart;  // largeur / hauteur du tableau au départ (m)
        private float _prochainControle;

        private void Start()
        {
            if (tableau == null || emplacement == null)
            {
                Debug.LogWarning("[Epreuve] « " + name + " » : renseigner « Tableau » et « Emplacement ».", this);
                enabled = false;
                return;
            }

            _boite = tableau.GetComponent<BoxCollider>();
            _scalable = tableau.GetComponent<VRScalable>();
            if (_boite == null || _scalable == null)
            {
                Debug.LogWarning("[Epreuve] Le tableau a besoin d'un BoxCollider et d'un VRScalable.", this);
                enabled = false;
                return;
            }

            _famille = VRCloneOrigin.FamilleDe(tableau.gameObject);
            _tailleReference = _scalable.InitialWorldSize;

            // Axe mince = normale du tableau ; les deux autres donnent le cadre.
            Vector3 s = _boite.size;
            _axeEpais = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
            Vector2 plan = PlanLocal(s);
            float echelle = Mathf.Max(0.0001f, tableau.transform.lossyScale.x); // départ = échelle courante
            _tailleDepart = plan * echelle;

            CreerCadre();
            CreerPanneau();
        }

        private void Update()
        {
            if (EstReussie) return;
            OrienterPanneau();
            if (Time.time < _prochainControle) return;
            _prochainControle = Time.time + 0.1f;
            Controler();
        }

        /// <summary>Validé depuis le PC : le tableau prend la bonne taille et s'accroche dans le cadre.</summary>
        protected override void Forcer()
        {
            if (emplacement == null || _tailleReference <= 0f) return;

            // L'original s'il existe encore, sinon une copie (l'original a pu être supprimé).
            Rigidbody corps = tableau;
            if (corps == null)
            {
                foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
                {
                    if (copie == null || copie.famille != _famille) continue;
                    corps = copie.GetComponent<Rigidbody>();
                    if (corps != null) break;
                }
            }
            if (corps == null) return;

            Liberer(corps);
            float actuel = RatioDe(corps.transform);
            if (actuel > 0f) corps.transform.localScale *= ratioCible / actuel;
            Physics.SyncTransforms(); // autoSyncTransforms = 0
            Accrocher(corps);
        }

        /// <summary>Le tableau (son emplacement a déjà son cadre lumineux).</summary>
        protected override void AjouterObjetsAMontrer(List<Collider> liste)
        {
            Collider c = ColliderDe(tableau);
            if (c != null) liste.Add(c);
            foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
            {
                if (copie == null || copie.famille != _famille) continue;
                c = ColliderDe(copie);
                if (c != null) liste.Add(c);
            }
        }

        // ------------------------------------------------------------------
        // Contrôle
        // ------------------------------------------------------------------
        /// <summary>
        /// Taille en multiple de la taille de départ de l'ORIGINAL. Pas celle du VRScalable de la
        /// copie : une copie d'un tableau déjà agrandi a pris cette taille-là comme « ×1 ».
        /// </summary>
        private float RatioDe(Transform t)
        {
            if (_tailleReference <= 0f) return 1f;
            Vector3 e = t.lossyScale;
            return Mathf.Max(Mathf.Abs(e.x), Mathf.Max(Mathf.Abs(e.y), Mathf.Abs(e.z))) / _tailleReference;
        }

        private void Controler()
        {
            // Le panneau et le cadre décrivent le tableau tenu en main, sinon le plus proche du cadre.
            Rigidbody suivi = null;
            bool suiviTenu = false;
            float suiviDistance = float.MaxValue;

            VerifierCandidat(tableau, ref suivi, ref suiviTenu, ref suiviDistance);
            if (EstReussie) return;
            foreach (VRCloneOrigin copie in FindObjectsByType<VRCloneOrigin>(FindObjectsSortMode.None))
            {
                if (copie == null || copie.famille != _famille) continue;
                VerifierCandidat(copie.GetComponent<Rigidbody>(), ref suivi, ref suiviTenu, ref suiviDistance);
                if (EstReussie) return;
            }

            if (suivi == null)
            {
                MettreAJourFeedback(1f, false, false); // tous rangés dans la bulle ou supprimés
                return;
            }
            float ratio = RatioDe(suivi.transform);
            MettreAJourFeedback(ratio, TailleOk(ratio), PlaceOk(suivi, suiviDistance));
        }

        /// <summary>Accroche le candidat s'il remplit tout ; sinon, retient celui à décrire au joueur.</summary>
        private void VerifierCandidat(Rigidbody corps, ref Rigidbody suivi, ref bool suiviTenu, ref float suiviDistance)
        {
            if (corps == null || !corps.gameObject.activeInHierarchy) return;
            BoxCollider boite = corps.GetComponent<BoxCollider>();
            if (boite == null || !boite.enabled) return; // rangé dans la bulle : colliders coupés

            float distance = Vector3.Distance(boite.transform.TransformPoint(boite.center), emplacement.position);
            bool tenu = EstTenu(corps);

            if (TailleOk(RatioDe(corps.transform)) && PlaceOk(corps, distance))
            {
                if (tenu) Liberer(corps);
                Accrocher(corps);
                return;
            }

            if (suivi == null || (tenu && !suiviTenu) || (tenu == suiviTenu && distance < suiviDistance))
            {
                suivi = corps;
                suiviTenu = tenu;
                suiviDistance = distance;
            }
        }

        private bool TailleOk(float ratio) => Mathf.Abs(ratio - ratioCible) <= ratioCible * toleranceTaille;

        private bool PlaceOk(Rigidbody corps, float distance)
        {
            return distance <= distanceAccroche && Quaternion.Angle(corps.rotation, emplacement.rotation) <= angleMax;
        }

        // ------------------------------------------------------------------
        // Accrochage
        // ------------------------------------------------------------------
        private void Accrocher(Rigidbody corps)
        {
            StartCoroutine(Plaquer(corps));

            Debug.Log("[Epreuve] Tableau « " + corps.name + " » accroché à la bonne taille (×" + RatioDe(corps.transform).ToString("0.00") + ").", this);
            VRSons.Jouer(VRSon.Reussite, emplacement.position);
            if (_panneau != null) _panneau.text = "<color=#5dff7a><b>Bravo !</b></color>";
            if (_cadre != null) CouleurCadre(new Color(0.36f, 1f, 0.48f));

            SignalerReussite(); // définitif : décrocher ensuite le tableau ne l'annule pas
            aLaReussite?.Invoke();
            if (!string.IsNullOrEmpty(sceneSuivante)) StartCoroutine(PasserALaSuite());
        }

        /// <summary>Glisse le tableau contre le mur en 0,25 s puis le fige.</summary>
        private IEnumerator Plaquer(Rigidbody corps)
        {
            if (!corps.isKinematic)
            {
                corps.linearVelocity = Vector3.zero;
                corps.angularVelocity = Vector3.zero;
            }
            corps.isKinematic = true;

            Transform t = corps.transform;
            BoxCollider boite = corps.GetComponent<BoxCollider>();
            Vector3 centre = boite != null ? boite.transform.TransformPoint(boite.center) : t.position;
            Vector3 decalage = emplacement.position - centre;
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;

            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.25f)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                t.SetPositionAndRotation(Vector3.Lerp(p0, p0 + decalage, e), Quaternion.Slerp(r0, emplacement.rotation, e));
                yield return null;
            }
            t.SetPositionAndRotation(p0 + decalage, emplacement.rotation);
            Physics.SyncTransforms(); // autoSyncTransforms = 0

            // Une frame plus tard (le tri a tourné) : état réel, pour diagnostiquer un tableau invisible.
            yield return null;
            Debug.Log("[Epreuve] Tableau accroché en " + t.position.ToString("F3") + ", échelle " + t.lossyScale.ToString("F2")
                      + ", cinématique " + corps.isKinematic + ", parent " + (t.parent != null ? t.parent.name : "aucun"), this);
            VROrdreSplats.Journaliser("Tableau accroché");
        }

        private IEnumerator PasserALaSuite()
        {
            yield return new WaitForSeconds(delaiAvantScene);
            if (!Application.CanStreamedLevelBeLoaded(sceneSuivante))
            {
                Debug.LogError("[Epreuve] La scène « " + sceneSuivante + " » n'est pas dans les Build Settings.", this);
                yield break;
            }
            SceneManager.LoadSceneAsync(sceneSuivante);
        }

        // ------------------------------------------------------------------
        // Feedback : cadre + panneau
        // ------------------------------------------------------------------
        /// <summary>Deux plus grandes dimensions locales (hors axe mince) du tableau, en (largeur, hauteur).</summary>
        private Vector2 PlanLocal(Vector3 s)
        {
            switch (_axeEpais)
            {
                case 0: return new Vector2(s.z, s.y);
                case 1: return new Vector2(s.x, s.z);
                default: return new Vector2(s.x, s.y);
            }
        }

        private void MettreAJourFeedback(float ratio, bool tailleOk, bool placeOk)
        {
            Color couleur = placeOk ? (tailleOk ? new Color(0.36f, 1f, 0.48f) : new Color(1f, 0.7f, 0.2f)) : Color.white;
            CouleurCadre(couleur);

            if (_panneau == null) return;
            string etat = tailleOk ? "<color=#5dff7a>taille OK</color>" : (ratio < ratioCible ? "agrandis-le" : "trop grand");
            _panneau.text = "Tableau  ×" + ratio.ToString("0.0") + " / ×" + ratioCible.ToString("0.0") + "  (" + etat + ")";
        }

        private void CouleurCadre(Color c)
        {
            if (_cadre == null) return;
            _cadre.startColor = _cadre.endColor = c;
        }

        /// <summary>Rectangle de la taille cible, dans le plan du mur, autour de l'emplacement.</summary>
        private void CreerCadre()
        {
            GameObject go = new GameObject("Cadre_Tableau");
            go.transform.SetParent(emplacement, false);
            _cadre = go.AddComponent<LineRenderer>();
            _cadre.useWorldSpace = false;
            _cadre.loop = true;
            _cadre.positionCount = 4;
            _cadre.widthMultiplier = 0.012f;
            _cadre.material = VRUtils.CreateUnlitMaterial(Color.white);
            _cadre.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cadre.receiveShadows = false;

            Vector2 demi = _tailleDepart * ratioCible * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(emplacement.lossyScale.x));
            Vector3[] coins = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float a = (i == 0 || i == 3) ? -demi.x : demi.x;
                float b = (i < 2) ? -demi.y : demi.y;
                coins[i] = AxePlan(a, b);
            }
            _cadre.SetPositions(coins);
            CouleurCadre(Color.white);
        }

        /// <summary>Point du plan du tableau (a le long de la largeur, b le long de la hauteur) en repère local.</summary>
        private Vector3 AxePlan(float a, float b)
        {
            switch (_axeEpais)
            {
                case 0: return new Vector3(0f, b, a);
                case 1: return new Vector3(a, 0f, b);
                default: return new Vector3(a, b, 0f);
            }
        }

        private void CreerPanneau()
        {
            GameObject go = new GameObject("Panneau_Epreuve_Tableau");
            go.transform.SetParent(transform, false);
            _panneau = go.AddComponent<TextMeshPro>();
            _panneau.enableAutoSizing = true;
            _panneau.fontSizeMin = 0.01f;
            _panneau.fontSizeMax = 10f;
            _panneau.alignment = TextAlignmentOptions.Center;
            _panneau.textWrappingMode = TextWrappingModes.NoWrap;
            _panneau.rectTransform.sizeDelta = new Vector2(0.6f, 0.1f);
            OrienterPanneau();
        }

        private void OrienterPanneau()
        {
            if (_panneau == null || emplacement == null) return;
            float haut = _tailleDepart.y * ratioCible * 0.5f;
            Vector3 position = emplacement.position + Vector3.up * (haut + 0.12f);
            _panneau.transform.position = position;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 regard = position - cam.transform.position;
            regard.y = 0f;
            if (regard.sqrMagnitude > 1e-6f) _panneau.transform.rotation = Quaternion.LookRotation(regard, Vector3.up);
        }

        // ------------------------------------------------------------------
        // Aperçu dans l'éditeur : cadre vert à la taille cible
        // ------------------------------------------------------------------
        private void OnDrawGizmosSelected()
        {
            if (emplacement == null || tableau == null) return;
            BoxCollider boite = tableau.GetComponent<BoxCollider>();
            if (boite == null) return;

            Vector3 s = boite.size;
            int axe = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
            Vector2 plan = axe == 0 ? new Vector2(s.z, s.y) : (axe == 1 ? new Vector2(s.x, s.z) : new Vector2(s.x, s.y));
            plan *= Mathf.Abs(tableau.transform.lossyScale.x) * ratioCible;

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.matrix = emplacement.localToWorldMatrix;
            Vector3 taille = axe == 0 ? new Vector3(0.01f, plan.y, plan.x) : (axe == 1 ? new Vector3(plan.x, 0.01f, plan.y) : new Vector3(plan.x, plan.y, 0.01f));
            Gizmos.DrawWireCube(Vector3.zero, taille);
        }
    }
}
