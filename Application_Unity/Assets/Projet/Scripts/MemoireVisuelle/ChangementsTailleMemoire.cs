// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  ChangementsTailleMemoire.cs — LOGIQUE PURE : QUELS OBJETS CHANGENT DE TAILLE, ET DE COMBIEN
//
//  RÔLE
//    Dans les niveaux avancés, quelques objets sont agrandis ou rétrécis PENDANT LE NOIR : le
//    joueur doit retrouver leur taille d'origine. Ce calcul choisit les objets et le facteur.
//    Aucune dépendance à la scène : testable sans Unity en jeu.
//
//  RÈGLES
//    • seuls les objets assez GRANDS sont candidats (un petit objet rétréci disparaîtrait) ;
//    • on prend d'abord des objets qui n'ont PAS changé de place (un changement à la fois par
//      objet : le joueur comprend ce qui s'est passé) ; s'il n'y en a pas assez, on complète
//      avec des objets déjà déplacés ;
//    • les changements ALTERNENT agrandi / rétréci : avec 2 objets, un de chaque ;
//    • les facteurs sont tirés dans les plages du niveau (clairs : jamais proches de ×1).
//
//  MÊME GRAINE + MÊMES DONNÉES = MÊME RÉSULTAT. Graine 0 : le hasard est tiré à chaque appel.
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Un objet dont la taille change, et le facteur appliqué (×2 = deux fois plus grand).</summary>
    public struct ChangementTaille
    {
        public int objet;
        public float ratio;
    }

    /// <summary>Calcul des changements de taille.</summary>
    public static class ChangementsTailleMemoire
    {
        /// <summary>
        /// Choisit jusqu'à « nombre » objets dont changer la taille.
        /// « tailles » : taille (m) de chaque objet, indexée comme les objets ;
        /// « dejaDeplaces » : indices des objets déjà déplacés (évités en priorité).
        /// </summary>
        public static List<ChangementTaille> Calculer(IList<float> tailles, IList<int> dejaDeplaces, int nombre,
            float tailleMin, Vector2 agrandi, Vector2 reduit, int graine)
        {
            var resultat = new List<ChangementTaille>();
            if (tailles == null || nombre <= 0) return resultat;

            // Graine décalée : indépendante de celle du mélange (qui utilise la même graine du niveau).
            System.Random hasard = graine == 0 ? new System.Random() : new System.Random(graine * 31 + 7);

            var libres = new List<int>();
            var deplaces = new List<int>();
            for (int i = 0; i < tailles.Count; i++)
            {
                if (tailles[i] < tailleMin) continue;
                if (dejaDeplaces != null && dejaDeplaces.Contains(i)) deplaces.Add(i);
                else libres.Add(i);
            }
            Melanger(libres, hasard);
            Melanger(deplaces, hasard);

            var ordre = new List<int>(libres);
            ordre.AddRange(deplaces);

            for (int k = 0; k < ordre.Count && resultat.Count < nombre; k++)
            {
                Vector2 plage = resultat.Count % 2 == 0 ? agrandi : reduit; // agrandi, réduit, agrandi, …
                float bas = Mathf.Min(plage.x, plage.y);
                float haut = Mathf.Max(plage.x, plage.y);
                float ratio = bas + (float)hasard.NextDouble() * (haut - bas);
                resultat.Add(new ChangementTaille { objet = ordre[k], ratio = ratio });
            }
            return resultat;
        }

        private static void Melanger(List<int> liste, System.Random hasard)
        {
            for (int i = liste.Count - 1; i > 0; i--)
            {
                int j = hasard.Next(i + 1);
                (liste[i], liste[j]) = (liste[j], liste[i]);
            }
        }
    }
}
