// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  EvaluationMemoire.cs — LOGIQUE PURE : UN OBJET EST-IL BIEN REPLACÉ ? QUEL SCORE ?
//
//  RÔLE
//    Compare la position d'un objet à sa place d'origine, avec une marge d'erreur, et calcule
//    le score d'ensemble. Aucune dépendance à la scène : testable sans Unity en jeu.
//
//  MARGE D'ERREUR
//    max(toleranceMetres, toleranceTaille × taille de l'objet). Un gros objet est donc accepté
//    plus loin qu'un petit, et un petit objet garde au moins « toleranceMetres ».
//
//  LA ROTATION ET LA TAILLE NE SONT PAS ÉVALUÉES au niveau 1 (voir NiveauMemoire).
// =====================================================================================
using System.Collections.Generic;
using UnityEngine;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Verdict pour un objet.</summary>
    public struct ResultatObjet
    {
        public int id;
        /// <summary>Distance (m) entre la position actuelle et l'origine.</summary>
        public float distance;
        /// <summary>Marge acceptée (m) pour cet objet.</summary>
        public float tolerance;
        /// <summary>Bien replacé ET à la bonne taille (la taille n'est évaluée que si le niveau le demande).</summary>
        public bool ok;
        /// <summary>Position dans la marge.</summary>
        public bool positionOk;
        /// <summary>Taille d'origine retrouvée (toujours vrai si le niveau n'évalue pas la taille).</summary>
        public bool tailleOk;
        /// <summary>Taille actuelle ÷ taille d'origine (1 = inchangée).</summary>
        public float ratioTaille;
    }

    /// <summary>Calculs d'évaluation.</summary>
    public static class EvaluationMemoire
    {
        /// <summary>Marge d'erreur (m) d'un objet de cette taille.</summary>
        public static float Tolerance(float taille, float toleranceMetres, float toleranceTaille)
        {
            return Mathf.Max(toleranceMetres, toleranceTaille * Mathf.Max(0f, taille));
        }

        /// <summary>
        /// Verdict pour un objet. « ratio » = taille actuelle ÷ taille d'origine ; « toleranceRatio » > 0
        /// active l'évaluation de la taille (0 = ignorée, comme au niveau 1).
        /// </summary>
        public static ResultatObjet Evaluer(int id, Vector3 origine, Vector3 position, float taille,
            float toleranceMetres, float toleranceTaille, float ratio = 1f, float toleranceRatio = 0f)
        {
            float tolerance = Tolerance(taille, toleranceMetres, toleranceTaille);
            float distance = Vector3.Distance(origine, position);
            bool positionOk = distance <= tolerance;
            bool tailleOk = TailleRetrouvee(ratio, toleranceRatio);
            return new ResultatObjet
            {
                id = id, distance = distance, tolerance = tolerance,
                positionOk = positionOk, tailleOk = tailleOk, ratioTaille = ratio,
                ok = positionOk && tailleOk
            };
        }

        /// <summary>
        /// La taille est-elle retrouvée ? Marge symétrique : avec 0,3, entre ×(1/1,3) et ×1,3 (un objet
        /// deux fois trop grand et un objet deux fois trop petit sont également ratés).
        /// toleranceRatio ≤ 0 : la taille n'est pas évaluée, toujours vrai.
        /// </summary>
        public static bool TailleRetrouvee(float ratio, float toleranceRatio)
        {
            if (toleranceRatio <= 0f) return true;
            if (ratio <= 0f) return false;
            float facteur = 1f + toleranceRatio;
            return ratio <= facteur && ratio >= 1f / facteur;
        }

        /// <summary>Part d'objets bien placés (0 à 1). Liste vide : 0.</summary>
        public static float Score(IList<ResultatObjet> resultats)
        {
            if (resultats == null || resultats.Count == 0) return 0f;
            return (float)Corrects(resultats) / resultats.Count;
        }

        /// <summary>Nombre d'objets bien placés.</summary>
        public static int Corrects(IList<ResultatObjet> resultats)
        {
            int n = 0;
            if (resultats == null) return 0;
            for (int i = 0; i < resultats.Count; i++)
            {
                if (resultats[i].ok) n++;
            }
            return n;
        }

        /// <summary>Le score atteint-il le seuil ? (petite marge pour les arrondis : 4/5 vaut 0,8)</summary>
        public static bool Reussi(float score, float seuil, int nombre)
        {
            if (nombre <= 0) return false;
            return score + 1e-4f >= seuil;
        }
    }
}
