// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  DemoMemoire.cs — TOUCHES PC POUR TESTER LE JEU SANS CASQUE
//
//  RÔLE
//    Ajouté automatiquement par MemoireVisuelle (case « Commandes Demo »).
//      N : phase suivante (démarrer → observation → mélange → valider → rejouer)
//      R : rejouer (depuis l'écran de résultat)
//      F : solution automatique (tout remis en place, résultat affiché)
//      H : masquer / afficher l'aide à l'écran
//
//  ⚠️ La fenêtre Unity doit avoir le focus pour recevoir le clavier : cliquer une fois dans la vue Game.
// =====================================================================================
using UnityEngine;
using UnityEngine.InputSystem;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Raccourcis clavier de test.</summary>
    [AddComponentMenu("")]
    public class DemoMemoire : MonoBehaviour
    {
        private MemoireVisuelle _jeu;
        private bool _visible = true;
        private GUIStyle _style;

        private void Awake()
        {
            _jeu = GetComponent<MemoireVisuelle>();
        }

        private void Update()
        {
            Keyboard clavier = Keyboard.current;
            if (clavier == null || _jeu == null) return;

            if (clavier[Key.H].wasPressedThisFrame) _visible = !_visible;
            if (clavier[Key.N].wasPressedThisFrame) _jeu.PhaseSuivante();
            if (clavier[Key.R].wasPressedThisFrame) _jeu.Recommencer();
            if (clavier[Key.F].wasPressedThisFrame) _jeu.ForcerLaSolution();
        }

        private void OnGUI()
        {
            if (!_visible || _jeu == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
                _style.normal.textColor = Color.white;
            }
            GUI.Box(new Rect(10f, 10f, 330f, 92f),
                "Mémoire visuelle — phase : " + _jeu.Phase
                + "\n[N] phase suivante   [R] rejouer"
                + "\n[F] solution auto     [H] masquer", _style);
        }
    }
}
