// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  CommandesDemo.cs — VALIDER DES ÉTAPES DEPUIS LE PC PENDANT UNE DÉMO
//
//  RÔLE
//    Pendant qu'un joueur est dans le casque, la personne au PC peut débloquer la
//    partie si une épreuve est ratée ou impossible :
//      • panneau cliquable en haut à gauche de la vue Game (écran du PC) ;
//      • touches 1 à 9 : valider l'étape de ce numéro ;
//      • N : valider l'étape suivante (la première restante) ;
//      • G : main guide oui / non (choix gardé d'une partie à l'autre, voir ReglagesAide) ;
//      • O : charger la scène suivante tout de suite ;
//      • V : couper / remettre la voix de la narration ;
//      • P : jeu de mémoire, passer l'attente (départ + observation) : le mélange part tout de suite ;
//      • H : masquer / afficher le panneau.
//    La scène reste cohérente : le tableau s'accroche, la plante se plante, la statue
//    disparaît (voir Epreuve.ForcerReussite), et la narration enchaîne sur la suite.
//
//  ⚠️ La fenêtre Unity doit avoir le focus pour recevoir le clavier : cliquer une fois
//    dans la vue Game.
//
//  CRÉÉ PAR : ProgressionEtapes au lancement (case « Commandes Demo »).
// =====================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Panneau et raccourcis de l'opérateur, sur l'écran du PC.</summary>
    [AddComponentMenu("")]
    public class CommandesDemo : MonoBehaviour
    {
        /// <summary>La progression pilotée.</summary>
        public ProgressionEtapes progression;

        private const float Largeur = 430f;
        private const int MaxToucheEtape = 9;

        private bool _visible = false;
        private GUIStyle _titre, _bouton;

        private void Update()
        {
            Keyboard clavier = Keyboard.current;
            if (clavier == null || progression == null) return;

            if (clavier[Key.H].wasPressedThisFrame) _visible = !_visible;
            if (clavier[Key.N].wasPressedThisFrame) progression.ForcerSuivante();
            // Bascule ce qui est VISIBLE (dans Scene3, le réglage est à « oui » mais la main est prévue éteinte).
            if (clavier[Key.G].wasPressedThisFrame) progression.ActiverMainGuide(!progression.MainGuideVisible);
            if (clavier[Key.O].wasPressedThisFrame) progression.ChargerMaintenant();
            if (clavier[Key.V].wasPressedThisFrame) NarrationBulle.VoixActive = !NarrationBulle.VoixActive;
            if (clavier[Key.P].wasPressedThisFrame) Memoire()?.PasserAttente();

            int nombre = Mathf.Min(progression.etapes.Count, MaxToucheEtape);
            for (int i = 0; i < nombre; i++)
            {
                if (clavier[Key.Digit1 + i].wasPressedThisFrame || clavier[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    progression.ForcerEtape(progression.etapes[i]);
                }
            }
        }

        /// <summary>Le jeu de mémoire de la scène, s'il fait partie des étapes (null sinon).</summary>
        private EpreuveMemoire Memoire()
        {
            foreach (Epreuve e in progression.etapes)
            {
                if (e is EpreuveMemoire m) return m;
            }
            return null;
        }

        private void OnGUI()
        {
            if (!_visible || progression == null) return;

            if (_titre == null)
            {
                _titre = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                _bouton = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            }

            GUILayout.BeginArea(new Rect(10f, 10f, Largeur, Screen.height - 20f));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Commandes démo (H pour masquer)", _titre);
            GUILayout.Label("Version du code : " + RemodelerLeReel.VR.VROrdreSplats.VersionCode);

            for (int i = 0; i < progression.etapes.Count; i++)
            {
                Epreuve e = progression.etapes[i];
                if (e == null) continue;
                GUI.enabled = !e.EstReussie;
                string etat = e.EstReussie ? "[fait]  " : "";
                if (GUILayout.Button((i + 1) + ".  " + etat + e.nomEtape, _bouton, GUILayout.Height(34f)))
                {
                    progression.ForcerEtape(e);
                }
            }

            GUI.enabled = true;
            if (GUILayout.Button("G.  Main guide : " + (progression.MainGuideVisible ? "oui" : "non"), _bouton, GUILayout.Height(34f)))
            {
                progression.ActiverMainGuide(!progression.MainGuideVisible);
            }

            GUI.enabled = !progression.EstTerminee;
            if (GUILayout.Button("N.  Valider l'étape suivante", _bouton, GUILayout.Height(34f))) progression.ForcerSuivante();

            GUI.enabled = !string.IsNullOrEmpty(progression.sceneSuivante);
            if (GUILayout.Button("O.  Charger " + progression.sceneSuivante + " maintenant", _bouton, GUILayout.Height(34f))) progression.ChargerMaintenant();

            GUI.enabled = true;
            if (GUILayout.Button("V.  Voix : " + (NarrationBulle.VoixActive ? "activée" : "coupée"), _bouton, GUILayout.Height(34f)))
            {
                NarrationBulle.VoixActive = !NarrationBulle.VoixActive;
            }

            EpreuveMemoire memoire = Memoire();
            if (memoire != null)
            {
                GUI.enabled = memoire.PeutPasserAttente;
                if (GUILayout.Button("P.  Passer l'attente (jeu de mémoire)", _bouton, GUILayout.Height(34f))) memoire.PasserAttente();
                GUI.enabled = true;
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
