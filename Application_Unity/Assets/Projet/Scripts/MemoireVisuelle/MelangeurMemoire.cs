// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  MelangeurMemoire.cs — LOGIQUE PURE : QUEL OBJET VA SUR QUEL EMPLACEMENT
//
//  RÔLE
//    Choisit, avec une graine aléatoire, les objets à déplacer et leur destination. Aucune
//    dépendance à la scène (que des nombres et des Vector3) : c'est ce qui permet de le
//    tester sans Unity en jeu (Scripts/MemoireVisuelle/Editor/Tests).
//
//  TROIS FAÇONS DE CHOISIR UNE DESTINATION (par objet)
//    • FIXE      : l'objet a un emplacement imposé (ObjetMemoire.emplacementFixe) ;
//    • AUTORISÉE : l'objet n'a le droit d'aller que sur certains emplacements ;
//    • ALÉATOIRE : n'importe quel emplacement valide (par défaut).
//
//  UN EMPLACEMENT EST VALIDE POUR UN OBJET SI
//    • il est assez grand (tailleMax <= 0 : sans limite) ;
//    • il est à au moins « distanceMin » de la place d'origine de l'objet ;
//    • il est libre : pas à moins de RayonOccupation de l'origine d'un objet du jeu, ni d'une
//      destination déjà attribuée (pas d'échange entre deux objets, pas d'empilement).
//
//  MÊME GRAINE + MÊMES DONNÉES = MÊME RÉSULTAT. Graine 0 : le hasard est tiré à chaque appel.
// =====================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Un objet candidat au déplacement.</summary>
    public struct ObjetMelange
    {
        public int id;
        public Vector3 origine;
        public float taille;
        /// <summary>Id de l'emplacement imposé, -1 si aucun.</summary>
        public int fixe;
        /// <summary>Ids des emplacements autorisés ; null ou vide = tous.</summary>
        public int[] autorises;
    }

    /// <summary>Une destination possible.</summary>
    public struct EmplacementMelange
    {
        public int id;
        public Vector3 position;
        /// <summary>Taille maximale (m) d'un objet qui y tient ; 0 ou moins = sans limite.</summary>
        public float tailleMax;
    }

    /// <summary>Résultat : « cet objet va sur cet emplacement ».</summary>
    public struct Deplacement
    {
        public int objet;
        public int emplacement;
    }

    /// <summary>Calcul du mélange.</summary>
    public static class MelangeurMemoire
    {
        /// <summary>Distance (m) en dessous de laquelle un emplacement est considéré comme occupé.</summary>
        public const float RayonOccupation = 0.35f;

        /// <summary>
        /// Choisit jusqu'à « nombreADeplacer » déplacements. Peut en renvoyer moins si la scène
        /// n'a pas assez d'emplacements valides (ne lève jamais d'exception pour ça).
        /// </summary>
        public static List<Deplacement> Calculer(IList<ObjetMelange> objets, IList<EmplacementMelange> emplacements,
            int nombreADeplacer, float distanceMin, int graine)
        {
            var resultat = new List<Deplacement>();
            if (objets == null || emplacements == null || nombreADeplacer <= 0) return resultat;

            System.Random hasard = graine == 0 ? new System.Random() : new System.Random(graine);

            // Ordre de passage : les objets à emplacement imposé d'abord, puis les autres, chacun
            // groupe mélangé (Fisher-Yates avec la graine).
            var avecFixe = new List<int>();
            var sansFixe = new List<int>();
            for (int i = 0; i < objets.Count; i++)
            {
                if (objets[i].fixe >= 0) avecFixe.Add(i);
                else sansFixe.Add(i);
            }
            Melanger(avecFixe, hasard);
            Melanger(sansFixe, hasard);
            var ordre = new List<int>(avecFixe);
            ordre.AddRange(sansFixe);

            // Positions déjà prises : origines de tous les objets, puis destinations attribuées.
            var occupees = new List<Vector3>(objets.Count + nombreADeplacer);
            for (int i = 0; i < objets.Count; i++) occupees.Add(objets[i].origine);

            var candidats = new List<int>();
            foreach (int indexObjet in ordre)
            {
                if (resultat.Count >= nombreADeplacer) break;
                ObjetMelange objet = objets[indexObjet];

                int choisi = -1;
                if (objet.fixe >= 0)
                {
                    int imposé = IndexEmplacement(emplacements, objet.fixe);
                    // Imposé par l'auteur du niveau : on respecte son choix (ni distance ni taille),
                    // sauf si une destination déjà attribuée occupe cette place.
                    if (imposé >= 0 && EstLibreDeDestination(emplacements[imposé].position, resultat, emplacements))
                        choisi = imposé;
                }
                else
                {
                    candidats.Clear();
                    for (int e = 0; e < emplacements.Count; e++)
                    {
                        if (EstValide(objet, emplacements[e], distanceMin, occupees)) candidats.Add(e);
                    }
                    if (candidats.Count > 0) choisi = candidats[hasard.Next(candidats.Count)];
                }

                if (choisi < 0) continue; // cet objet ne peut aller nulle part : on passe au suivant

                resultat.Add(new Deplacement { objet = objet.id, emplacement = emplacements[choisi].id });
                occupees.Add(emplacements[choisi].position);
            }
            return resultat;
        }

        /// <summary>Un emplacement convient-il à cet objet ?</summary>
        public static bool EstValide(ObjetMelange objet, EmplacementMelange emplacement, float distanceMin,
            IList<Vector3> positionsOccupees)
        {
            if (emplacement.tailleMax > 0f && objet.taille > emplacement.tailleMax) return false;
            if (Vector3.Distance(emplacement.position, objet.origine) < distanceMin) return false;

            if (objet.autorises != null && objet.autorises.Length > 0 && Array.IndexOf(objet.autorises, emplacement.id) < 0)
                return false;

            for (int i = 0; i < positionsOccupees.Count; i++)
            {
                if (Vector3.Distance(emplacement.position, positionsOccupees[i]) < RayonOccupation) return false;
            }
            return true;
        }

        private static bool EstLibreDeDestination(Vector3 position, List<Deplacement> deja, IList<EmplacementMelange> emplacements)
        {
            for (int i = 0; i < deja.Count; i++)
            {
                int e = IndexEmplacement(emplacements, deja[i].emplacement);
                if (e >= 0 && Vector3.Distance(position, emplacements[e].position) < RayonOccupation) return false;
            }
            return true;
        }

        private static int IndexEmplacement(IList<EmplacementMelange> emplacements, int id)
        {
            for (int i = 0; i < emplacements.Count; i++)
            {
                if (emplacements[i].id == id) return i;
            }
            return -1;
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
