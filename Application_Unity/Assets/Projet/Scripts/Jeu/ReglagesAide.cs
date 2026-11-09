// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  ReglagesAide.cs — AIDES AU JOUEUR (réglées depuis le panneau de démo)
//
//  RÔLE
//    Le panneau de démo du PC (CommandesDemo) active ou coupe la main guide façon Addams
//    (MainGuide). Activée par défaut, persistée en PlayerPrefs : le choix reste d'une partie
//    à l'autre. La case « Main Guide » de ProgressionEtapes reste prioritaire.
//    (Le tutoriel des gestes est dans le menu d'entrée : bouton « Tutoriel ».)
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.Jeu
{
    /// <summary>Aides activables depuis le panneau de démo.</summary>
    public static class ReglagesAide
    {
        private const string CleMainGuide = "RemodelerLeReel_MainGuide";

        /// <summary>Main guide qui montre les objets de l'étape en cours.</summary>
        public static bool MainGuide
        {
            get => PlayerPrefs.GetInt(CleMainGuide, 1) == 1;
            set => Ecrire(CleMainGuide, value);
        }

        private static void Ecrire(string cle, bool valeur)
        {
            PlayerPrefs.SetInt(cle, valeur ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
