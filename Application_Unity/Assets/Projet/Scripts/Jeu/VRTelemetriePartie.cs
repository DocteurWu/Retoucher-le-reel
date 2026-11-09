// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRTelemetriePartie.cs — ENREGISTREMENT CONTINU DE LA POSITION, DU REGARD ET DES ACTIONS
//
//  RÔLE :
//    S'injecte automatiquement dès qu'on lance le jeu en Play mode (RuntimeInitializeOnLoadMethod).
//    Ne nécessite aucune modification de scène.
//    Trace en continu (toutes les 0.5s) :
//      - Position monde de la tête (casque VR / Camera.main)
//      - Direction du regard et orientation (yaw, pitch, roll)
//      - État des mains (suivies ou non, positions, objets tenus)
//      - Événements majeurs (téléportations, saisies, lâchers, épreuves réussies, changement de scène)
//    Chaque relevé va dans un fichier CSV horodaté (Application_Unity/Journaux_PCVR/
//    telemetrie_<date>.csv). La console (Editor.log, préfixe [Telemetrie]) ne reçoit que les
//    ÉVÉNEMENTS : deux lignes par seconde noyaient les autres journaux ([Limites], [Tutoriel]…).
// =====================================================================================
using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    [AddComponentMenu("")]
    public class VRTelemetriePartie : MonoBehaviour
    {
        private static VRTelemetriePartie _instance;

        private StreamWriter _csvWriter;
        private string _csvPath;
        private float _prochainLog;
        private const float IntervalleLog = 0.5f; // 2 fois par seconde
        private float _prochainFlush;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoDemarrer()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("VRTelemetriePartie_Auto");
            _instance = go.AddComponent<VRTelemetriePartie>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            InitialiserFichierCSV();

            VRTeleportation.Teleporte += OnTeleporte;
            VRGrabHand.ObjetSaisi += OnObjetSaisi;
            VRGrabHand.ObjetLache += OnObjetLache;
            Epreuve.UneEpreuveReussie += OnEpreuveReussie;
            ProgressionEtapes.Terminees += OnProgressionTerminees;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Debug.Log("[Telemetrie] Télémétrie active. Fichier CSV : " + _csvPath);
        }

        private void OnDestroy()
        {
            VRTeleportation.Teleporte -= OnTeleporte;
            VRGrabHand.ObjetSaisi -= OnObjetSaisi;
            VRGrabHand.ObjetLache -= OnObjetLache;
            Epreuve.UneEpreuveReussie -= OnEpreuveReussie;
            ProgressionEtapes.Terminees -= OnProgressionTerminees;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            FermerFichierCSV();
        }

        private void InitialiserFichierCSV()
        {
            try
            {
                string dossier = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Journaux_PCVR"));
                Directory.CreateDirectory(dossier);
                string nomFichier = "telemetrie_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".csv";
                _csvPath = Path.Combine(dossier, nomFichier);

                _csvWriter = new StreamWriter(_csvPath, false, Encoding.UTF8);
                _csvWriter.WriteLine("Horodatage;TempsJeu;Scene;Tete_X;Tete_Y;Tete_Z;Tete_Yaw;Tete_Pitch;Tete_Roll;Regard_X;Regard_Y;Regard_Z;MainG_Suivie;MainG_X;MainG_Y;MainG_Z;MainG_Objet;MainD_Suivie;MainD_X;MainD_Y;MainD_Z;MainD_Objet;Evenement");
                _csvWriter.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Telemetrie] Impossible d'initialiser le fichier CSV : " + ex.Message);
            }
        }

        private void FermerFichierCSV()
        {
            if (_csvWriter != null)
            {
                try
                {
                    _csvWriter.Flush();
                    _csvWriter.Close();
                }
                catch { }
                _csvWriter = null;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _prochainLog) return;
            _prochainLog = Time.unscaledTime + IntervalleLog;

            EnregistrerSnapshot("");
        }

        private void EnregistrerSnapshot(string evenement)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 posTete = cam.transform.position;
            Vector3 rotTete = cam.transform.eulerAngles;
            Vector3 regard = cam.transform.forward;

            // Suivi des mains
            bool gSuivie = VRHandGestures.TryGetHand(XRNode.LeftHand, out XRHand handG);
            Vector3 gPos = Vector3.zero;
            if (gSuivie && VRHandGestures.TryGetJointPose(handG, XRHandJointID.Wrist, out Pose wG))
                gPos = VRHandGestures.ToWorld(wG.position);

            bool dSuivie = VRHandGestures.TryGetHand(XRNode.RightHand, out XRHand handD);
            Vector3 dPos = Vector3.zero;
            if (dSuivie && VRHandGestures.TryGetJointPose(handD, XRHandJointID.Wrist, out Pose wD))
                dPos = VRHandGestures.ToWorld(wD.position);

            // Objets tenus
            VRGrabHand grabG = VRGrabHand.GetHandForNode(XRNode.LeftHand);
            string gTient = grabG != null && grabG.GrabbedBody != null ? grabG.GrabbedBody.name : "";

            VRGrabHand grabD = VRGrabHand.GetHandForNode(XRNode.RightHand);
            string dTient = grabD != null && grabD.GrabbedBody != null ? grabD.GrabbedBody.name : "";

            string sceneNom = SceneManager.GetActiveScene().name;

            // Console / Editor.log : les événements seulement (les relevés réguliers vont dans le CSV).
            if (!string.IsNullOrEmpty(evenement))
            {
                string logTexte = string.Format(
                    "[Telemetrie] Pos=({0:F2}, {1:F2}, {2:F2}) | Regard=({3:F2}, {4:F2}, {5:F2}) Yaw={6:F0}° | G:{7} | D:{8}{9}",
                    posTete.x, posTete.y, posTete.z,
                    regard.x, regard.y, regard.z,
                    rotTete.y,
                    gSuivie ? (!string.IsNullOrEmpty(gTient) ? "tient " + gTient : "libre") : "non_suivie",
                    dSuivie ? (!string.IsNullOrEmpty(dTient) ? "tient " + dTient : "libre") : "non_suivie",
                    " | EVENT: " + evenement
                );
                Debug.Log(logTexte);
            }

            // Écriture CSV
            if (_csvWriter != null)
            {
                try
                {
                    string ligne = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0};{1:F2};{2};{3:F3};{4:F3};{5:F3};{6:F1};{7:F1};{8:F1};{9:F3};{10:F3};{11:F3};{12};{13:F3};{14:F3};{15:F3};{16};{17};{18:F3};{19:F3};{20:F3};{21};{22}",
                        DateTime.Now.ToString("HH:mm:ss.fff"),
                        Time.time,
                        sceneNom,
                        posTete.x, posTete.y, posTete.z,
                        rotTete.y, rotTete.x, rotTete.z,
                        regard.x, regard.y, regard.z,
                        gSuivie, gPos.x, gPos.y, gPos.z, gTient,
                        dSuivie, dPos.x, dPos.y, dPos.z, dTient,
                        evenement
                    );
                    _csvWriter.WriteLine(ligne);
                    // Écriture disque groupée : un Flush à chaque relevé (2 fois par seconde, sur le
                    // fil principal) pouvait faire de petites saccades. Les événements partent tout de
                    // suite, le reste toutes les 5 s (et à la fermeture).
                    if (!string.IsNullOrEmpty(evenement) || Time.unscaledTime >= _prochainFlush)
                    {
                        _prochainFlush = Time.unscaledTime + 5f;
                        _csvWriter.Flush();
                    }
                }
                catch { }
            }
        }

        private void OnTeleporte(Vector3 arrivee)
        {
            EnregistrerSnapshot("Téléportation vers " + arrivee.ToString("F2"));
        }

        private void OnObjetSaisi(VRGrabHand main, Rigidbody corps)
        {
            EnregistrerSnapshot("Saisie " + (corps != null ? corps.name : "?") + " par " + (main != null ? main.name : "?"));
        }

        private void OnObjetLache(VRGrabHand main, Rigidbody corps)
        {
            EnregistrerSnapshot("Lâcher " + (corps != null ? corps.name : "?") + " par " + (main != null ? main.name : "?"));
        }

        private void OnEpreuveReussie(Epreuve etape)
        {
            EnregistrerSnapshot("Épreuve réussie : " + (etape != null ? etape.nomEtape : "?"));
        }

        private void OnProgressionTerminees(ProgressionEtapes p)
        {
            EnregistrerSnapshot("Toutes les épreuves terminées !");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnregistrerSnapshot("Scène chargée : " + scene.name);
        }
    }
}
