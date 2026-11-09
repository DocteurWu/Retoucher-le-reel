// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  TutorielMenu.cs — LE TUTORIEL DES GESTES, DANS LE MENU D'ENTRÉE
//
//  RÔLE
//    Le bouton « Tutoriel » du menu (MainMenu) lance ici le tutoriel complet aux mains
//    fantômes (TutorielGestes) : se déplacer, saisir, changer la taille, ranger, dupliquer,
//    récupérer, supprimer. Le joueur entre ensuite dans le jeu en sachant tout faire.
//    Le bouton sert aussi à l'arrêter ; « Jeu de base » / « Jeu de mémoire » restent possibles à tout moment.
//
//  LE MENU POIGNET (bulle de rangement, doigts pour dupliquer / supprimer / récupérer)
//    n'existe pas dans le rig du menu : il est créé ici le temps du tutoriel, comme dans
//    les scènes de jeu (enfant du Camera Offset), puis retiré.
//
//  PC : panneau de démo du menu (H pour l'afficher) : lancer / arrêter le tutoriel,
//    valider le geste en cours, voix, ENREGISTRER SES GESTES (EnregistreurGestes) ; touches
//    N = geste suivant, T = arrêter, R = enregistrer (la fenêtre Unity doit avoir le focus :
//    cliquer une fois dans la vue Game).
//
//  CRÉÉ PAR : MainMenu (case « Bouton Tutoriel »).
// =====================================================================================
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using RemodelerLeReel.VR;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Lance et arrête le tutoriel des gestes depuis le menu d'entrée.</summary>
    [AddComponentMenu("")]
    public class TutorielMenu : MonoBehaviour
    {
        /// <summary>Levé quand le tutoriel démarre ou s'arrête (pour le libellé du bouton).</summary>
        public event Action Change;

        private const string PhraseFinale = "Tu sais tout faire. Vise « Jeu de base » ou « Jeu de mémoire », et tire pour commencer.";

        private TutorielGestes _tutoriel;
        private VRHandMenu _menuPoignetCree;
        private bool _panneauVisible;
        private EnregistreurGestes _enregistreur;
        private GUIStyle _titre, _bouton;

        /// <summary>Vrai pendant le tutoriel.</summary>
        public bool EnCours => _tutoriel != null && _tutoriel.EnCours;

        /// <summary>Lance le tutoriel, ou l'arrête s'il tourne.</summary>
        public void Basculer()
        {
            if (EnCours) _tutoriel.Terminer();
            else Lancer();
        }

        private void Lancer()
        {
            AssurerMenuPoignet();
            if (_tutoriel == null)
            {
                // Sur un objet à part : celui du menu est le canvas, réduit à l'échelle 0,0018.
                GameObject go = new GameObject("Tutoriel_Menu");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
                _tutoriel = go.AddComponent<TutorielGestes>();
                _tutoriel.phraseFinale = PhraseFinale;
                _tutoriel.Termine += SurTermine;
            }
            _tutoriel.Oublier(); // on peut refaire le tutoriel en entier
            if (_tutoriel.Demarrer()) Debug.Log("[TutorielMenu] Tutoriel des gestes lancé depuis le menu.", this);
            Change?.Invoke();
        }

        private void SurTermine()
        {
            if (_menuPoignetCree != null) Destroy(_menuPoignetCree.gameObject);
            _menuPoignetCree = null;
            Change?.Invoke();
        }

        /// <summary>La bulle de rangement et les doigts du menu poignet servent à quatre gestes du tutoriel.</summary>
        private void AssurerMenuPoignet()
        {
            if (FindFirstObjectByType<VRHandMenu>() != null) return;
            Camera cam = Camera.main;
            if (cam == null || cam.transform.parent == null) return;

            GameObject go = new GameObject("VRHandMenu_Tutoriel");
            go.transform.SetParent(cam.transform.parent, false);
            _menuPoignetCree = go.AddComponent<VRHandMenu>();
        }

        private void Update()
        {
            Keyboard clavier = Keyboard.current;
            if (clavier == null) return;
            if (clavier[Key.H].wasPressedThisFrame) _panneauVisible = !_panneauVisible;
            if (clavier[Key.V].wasPressedThisFrame) NarrationBulle.VoixActive = !NarrationBulle.VoixActive;
            if (clavier[Key.R].wasPressedThisFrame) Enregistreur.Basculer();
            if (!EnCours) return;
            if (clavier[Key.N].wasPressedThisFrame) _tutoriel.EtapeSuivante();
            if (clavier[Key.T].wasPressedThisFrame) _tutoriel.Terminer();
        }

        /// <summary>Panneau de démo du menu, sur l'écran du PC (même style que CommandesDemo).</summary>
        private void OnGUI()
        {
            if (!_panneauVisible) return;
            if (_titre == null)
            {
                _titre = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                _bouton = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 430f, Screen.height - 20f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Commandes démo — menu (H pour masquer)", _titre);
            GUILayout.Label("Version du code : " + VROrdreSplats.VersionCode);
            if (GUILayout.Button(EnCours ? "T.  Arrêter le tutoriel" : "Lancer le tutoriel des gestes", _bouton, GUILayout.Height(34f))) Basculer();
            GUI.enabled = EnCours;
            if (GUILayout.Button("N.  Valider le geste en cours", _bouton, GUILayout.Height(34f))) _tutoriel.EtapeSuivante();
            GUI.enabled = true;
            if (GUILayout.Button("V.  Voix : " + (NarrationBulle.VoixActive ? "activée" : "coupée"), _bouton, GUILayout.Height(34f)))
            {
                NarrationBulle.VoixActive = !NarrationBulle.VoixActive;
            }

            GUILayout.Space(12f);
            GUILayout.Label("Enregistrer un geste", _titre);
            GUILayout.BeginHorizontal();
            GUI.enabled = !Enregistreur.Actif;
            if (GUILayout.Button("<", _bouton, GUILayout.Width(40f), GUILayout.Height(34f))) Enregistreur.GesteSuivant(-1);
            GUILayout.Label(EnregistreurGestes.Gestes[Enregistreur.indiceGeste], _titre, GUILayout.Height(34f));
            if (GUILayout.Button(">", _bouton, GUILayout.Width(40f), GUILayout.Height(34f))) Enregistreur.GesteSuivant(1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (GUILayout.Button(Enregistreur.Actif ? "R.  Arrêter" : "R.  Enregistrer (3 bips, puis " + Enregistreur.dureeMax.ToString("F0") + " s max)", _bouton, GUILayout.Height(34f)))
            {
                Enregistreur.Basculer();
            }
            GUILayout.Label(Enregistreur.Etat);
            GUILayout.Label("Fichiers : " + EnregistreurGestes.Dossier);
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private EnregistreurGestes Enregistreur
        {
            get
            {
                if (_enregistreur == null) _enregistreur = gameObject.AddComponent<EnregistreurGestes>();
                return _enregistreur;
            }
        }
    }
}
