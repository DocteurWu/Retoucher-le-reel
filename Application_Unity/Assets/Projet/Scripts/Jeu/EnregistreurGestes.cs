// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EnregistreurGestes.cs — ENREGISTRER SES VRAIS GESTES POUR LES MAINS FANTÔMES
//
//  RÔLE
//    Les poses calculées du tutoriel ne ressemblent pas assez à de vrais gestes : on
//    enregistre donc les siens, en casque, avec le suivi des mains (XR Hands). Chaque prise
//    écrit un fichier JSON avec, image par image, la pose des 26 articulations de chaque
//    main. Ces fichiers servent ensuite à rejouer le geste sur les mains fantômes.
//
//  UTILISATION (panneau de démo du menu, touche H) :
//    1. choisir le geste (boutons < >) ;
//    2. appuyer sur R : 3 bips de compte à rebours, puis l'enregistrement commence (bip aigu) ;
//    3. faire le geste, mains bien visibles par le casque, sans bouger la tête ;
//    4. il s'arrête seul au bout de « durée max » (bip grave), ou plus tôt avec R.
//    Refaire autant de prises que voulu : chacune a son fichier.
//
//  REPÈRE : celui de la tête au DÉBUT de la prise (position des yeux, regard à
//    l'horizontale) : x = droite, y = haut, z = devant. C'est celui de l'ancre des mains
//    fantômes (à quelques centimètres près, réglés au moment de rejouer).
//    Version 2 : calculé dans l'espace de suivi (le rig) : une téléportation ou l'escalade
//    pendant la prise ne fait plus sauter les mains ; la tête est aussi notée à chaque image.
//    (Version 1, les premières prises : repère du monde, les déplacements du rig s'y voient.)
//
//  FICHIERS : Application_Unity/Enregistrements_Gestes/<geste>_<date>.json (hors Assets :
//    Unity ne les importe pas). À commiter pour les récupérer sur un autre PC.
// =====================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Hands;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Enregistre les poses des mains (XR Hands) dans un fichier JSON.</summary>
    [AddComponentMenu("")]
    public class EnregistreurGestes : MonoBehaviour
    {
        /// <summary>Une image : pose des articulations des deux mains.</summary>
        [Serializable]
        public class Image
        {
            public float t;
            public bool gaucheSuivie, droiteSuivie;
            /// <summary>Version 2 : pose de la tête (x, y, z, puis rotation x, y, z, w).</summary>
            public float[] tete;
            /// <summary>Par articulation (ordre XRHandJointID, de Wrist à LittleTip) : x, y, z, puis rotation x, y, z, w.</summary>
            public float[] gauche, droite;
        }

        /// <summary>Une prise complète.</summary>
        [Serializable]
        public class Prise
        {
            /// <summary>1 = repère du monde (premières prises), 2 = repère de l'espace de suivi + tête par image.</summary>
            public int version = 1;
            public string geste;
            public string date;
            public string repere = "tête au début de la prise : x droite, y haut, z devant (m)";
            public string[] articulations;
            public float frequence;
            public List<Image> images = new List<Image>();
        }

        /// <summary>Noms des gestes proposés (ceux du tutoriel, plus une prise libre).</summary>
        public static readonly string[] Gestes =
        {
            "Saisir", "Agrandir", "Ranger", "Dupliquer", "Recuperer", "Supprimer",
            "TeleporterMicrogeste", "TeleporterPistolet", "Escalader", "Libre"
        };

        public float dureeMax = 8f;
        public float compteARebours = 3f;
        public float frequence = 30f;

        /// <summary>Geste choisi (indice dans Gestes).</summary>
        public int indiceGeste;

        /// <summary>État lisible pour le panneau de démo.</summary>
        public string Etat { get; private set; } = "prêt";

        /// <summary>Vrai pendant le compte à rebours ou l'enregistrement.</summary>
        public bool Actif => _phase != Phase.Repos;

        /// <summary>Dossier des prises (à la racine du projet Unity, hors Assets).</summary>
        public static string Dossier => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Enregistrements_Gestes"));

        private enum Phase { Repos, CompteARebours, Enregistrement }

        private Phase _phase = Phase.Repos;
        private float _debut, _prochaineImage;
        private int _bipsJoues;
        private Prise _prise;
        private Vector3 _repereOrigine;
        private Quaternion _repereInverse;

        private static int NombreArticulations => XRHandJointID.EndMarker.ToIndex();

        /// <summary>Lance (après le compte à rebours) ou arrête une prise.</summary>
        public void Basculer()
        {
            if (_phase == Phase.Repos)
            {
                _phase = Phase.CompteARebours;
                _debut = Time.time;
                _bipsJoues = 0;
            }
            else if (_phase == Phase.Enregistrement)
            {
                Finir();
            }
            else
            {
                _phase = Phase.Repos;
                Etat = "annulé";
            }
        }

        public void GesteSuivant(int sens)
        {
            if (Actif) return;
            indiceGeste = (indiceGeste + sens + Gestes.Length) % Gestes.Length;
        }

        private void Bip(float pitch)
        {
            Camera cam = Camera.main;
            VRSons.Jouer(VRSon.NarrationLettre, cam != null ? cam.transform.position : transform.position, 1f, pitch);
        }

        private void Update()
        {
            if (_phase == Phase.CompteARebours)
            {
                float ecoule = Time.time - _debut;
                Etat = "départ dans " + Mathf.CeilToInt(compteARebours - ecoule) + " s";
                if (ecoule >= _bipsJoues && _bipsJoues < compteARebours)
                {
                    _bipsJoues++;
                    Bip(0.8f);
                }
                if (ecoule >= compteARebours) Commencer();
            }
            else if (_phase == Phase.Enregistrement)
            {
                float t = Time.time - _debut;
                Etat = "enregistrement « " + Gestes[indiceGeste] + " » : " + t.ToString("F1") + " / " + dureeMax.ToString("F0") + " s";
                if (Time.time >= _prochaineImage)
                {
                    _prochaineImage += 1f / Mathf.Max(1f, frequence);
                    _prise.images.Add(CapturerImage(t));
                }
                if (t >= dureeMax) Finir();
            }
        }

        private void Commencer()
        {
            // Repère dans l'espace de suivi (local au Camera Offset) : le rig peut bouger pendant
            // la prise (téléportation, escalade) sans que les mains enregistrées sautent.
            Camera cam = Camera.main;
            Transform suivi = VRHandGestures.TrackingSpace;
            Vector3 regard = cam != null ? cam.transform.forward : Vector3.forward;
            if (suivi != null) regard = suivi.InverseTransformDirection(regard);
            regard.y = 0f;
            if (regard.sqrMagnitude < 1e-4f) regard = Vector3.forward;
            _repereOrigine = cam != null ? (suivi != null ? suivi.InverseTransformPoint(cam.transform.position) : cam.transform.position) : Vector3.zero;
            _repereInverse = Quaternion.Inverse(Quaternion.LookRotation(regard.normalized, Vector3.up));

            var noms = new string[NombreArticulations];
            for (int i = 0; i < noms.Length; i++) noms[i] = XRHandJointIDUtility.FromIndex(i).ToString();
            _prise = new Prise
            {
                geste = Gestes[indiceGeste],
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                articulations = noms,
                frequence = frequence,
                version = 2
            };
            _phase = Phase.Enregistrement;
            _debut = Time.time;
            _prochaineImage = Time.time;
            Bip(1.6f);
        }

        private Image CapturerImage(float t)
        {
            var image = new Image { t = t };
            XRHandSubsystem sub = VRHandGestures.Subsystem;
            if (sub == null) return image;
            Camera cam = Camera.main;
            Transform suivi = VRHandGestures.TrackingSpace;
            if (cam != null && suivi != null)
            {
                Vector3 p = _repereInverse * (suivi.InverseTransformPoint(cam.transform.position) - _repereOrigine);
                Quaternion r = _repereInverse * (Quaternion.Inverse(suivi.rotation) * cam.transform.rotation);
                image.tete = new[] { p.x, p.y, p.z, r.x, r.y, r.z, r.w };
            }
            image.gaucheSuivie = sub.leftHand.isTracked;
            image.droiteSuivie = sub.rightHand.isTracked;
            image.gauche = image.gaucheSuivie ? Articulations(sub.leftHand) : null;
            image.droite = image.droiteSuivie ? Articulations(sub.rightHand) : null;
            return image;
        }

        /// <summary>Pose de chaque articulation dans le repère de la prise (7 nombres par articulation).</summary>
        private float[] Articulations(XRHand main)
        {
            var valeurs = new float[NombreArticulations * 7];
            for (int i = 0; i < NombreArticulations; i++)
            {
                if (!main.GetJoint(XRHandJointIDUtility.FromIndex(i)).TryGetPose(out Pose pose)) continue;
                // Les poses XR Hands sont déjà dans l'espace de suivi.
                Vector3 p = _repereInverse * (pose.position - _repereOrigine);
                Quaternion r = _repereInverse * pose.rotation;
                int k = i * 7;
                valeurs[k] = p.x; valeurs[k + 1] = p.y; valeurs[k + 2] = p.z;
                valeurs[k + 3] = r.x; valeurs[k + 4] = r.y; valeurs[k + 5] = r.z; valeurs[k + 6] = r.w;
            }
            return valeurs;
        }

        private void Finir()
        {
            _phase = Phase.Repos;
            Bip(0.5f);
            if (_prise == null || _prise.images.Count == 0)
            {
                Etat = "rien enregistré";
                return;
            }

            int suivies = 0;
            foreach (Image i in _prise.images) if (i.gaucheSuivie || i.droiteSuivie) suivies++;
            try
            {
                Directory.CreateDirectory(Dossier);
                string fichier = Path.Combine(Dossier, _prise.geste + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                File.WriteAllText(fichier, JsonUtility.ToJson(_prise));
                Etat = "enregistré : " + Path.GetFileName(fichier) + " (" + _prise.images.Count + " images, mains vues sur " + suivies + ")";
                Debug.Log("[Enregistreur] Geste « " + _prise.geste + " » enregistré : " + _prise.images.Count + " images (mains suivies sur "
                          + suivies + "), fichier " + fichier, this);
            }
            catch (Exception e)
            {
                Etat = "échec de l'écriture : " + e.Message;
                Debug.LogError("[Enregistreur] " + Etat, this);
            }
            _prise = null;
        }
    }
}
