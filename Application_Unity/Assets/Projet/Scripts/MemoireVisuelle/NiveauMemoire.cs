// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  NiveauMemoire.cs — LES RÉGLAGES D'UN NIVEAU DU JEU « MÉMOIRE VISUELLE »
//
//  RÔLE
//    Un niveau = un petit fichier de réglages (ScriptableObject), pas du code. Les niveaux
//    suivants (plus d'objets, tolérance plus serrée…) sont de nouveaux fichiers.
//    Créer un niveau : clic droit dans le Project > Create > Retoucher le Reel >
//    Memoire visuelle > Niveau. Les fichiers vont dans Assets/Projet/Donnees_Config/MemoireVisuelle/.
//
//  CE QUE LE JEU FAIT (voir MemoireVisuelle)
//    Observation libre → fondu au noir pendant lequel « nombreADeplacer » objets changent
//    d'emplacement → le joueur les remet à leur place d'origine → résultat.
//
//  TOLÉRANCE
//    Un objet est bien placé s'il est à moins de max(toleranceMetres, toleranceTaille × sa
//    taille) de sa place d'origine : un gros objet a donc une marge plus large qu'un petit.
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.Memoire
{
    /// <summary>Réglages d'un niveau du jeu de mémoire visuelle.</summary>
    [CreateAssetMenu(fileName = "Niveau", menuName = "Retoucher le Reel/Memoire visuelle/Niveau")]
    public class NiveauMemoire : ScriptableObject
    {
        [Header("Présentation")]
        [Tooltip("Nom du niveau, affiché dans le panneau de résultat.")]
        public string titre = "Niveau 1";

        [TextArea(2, 4)]
        [Tooltip("Phrase d'introduction de ce niveau. Vide = l'introduction par défaut de MemoireVisuelle.")]
        public string introduction = "";

        [Header("Observation")]
        [Min(0f)]
        [Tooltip("Temps (s) avant lequel le joueur ne peut pas dire « Je suis prêt ».")]
        public float observationMin = 20f;

        [Min(1f)]
        [Tooltip("Temps (s) au bout duquel l'observation s'arrête d'elle-même.")]
        public float observationMax = 90f;

        [Header("Mélange")]
        [Min(1)]
        [Tooltip("Nombre d'objets déplacés pendant le noir (moins si la scène n'a pas assez d'emplacements valides).")]
        public int nombreADeplacer = 3;

        [Min(0f)]
        [Tooltip("Distance minimale (m) entre la place d'origine d'un objet et sa nouvelle place : le changement doit se voir.")]
        public float distanceMinDeplacement = 1.5f;

        [Tooltip("Graine du hasard : 0 = un mélange différent à chaque partie ; sinon le même mélange à chaque fois (tests, démo).")]
        public int graine = 0;

        [Header("Changements de taille (niveaux avancés)")]
        [Min(0)]
        [Tooltip("Nombre d'objets dont la taille change pendant le noir (0 = aucun). Choisis de préférence parmi ceux qui n'ont pas changé de place.")]
        public int nombreAChangerDeTaille = 0;

        [Tooltip("Facteur (min, max) appliqué à un objet AGRANDI. Doit se voir : ×1,8 à ×2,4 par défaut.")]
        public Vector2 ratioAgrandi = new Vector2(1.8f, 2.4f);

        [Tooltip("Facteur (min, max) appliqué à un objet RÉTRÉCI. Doit se voir : ×0,4 à ×0,55 par défaut.")]
        public Vector2 ratioReduit = new Vector2(0.4f, 0.55f);

        [Min(0f)]
        [Tooltip("Taille minimale (m) d'un objet dont on change la taille : un petit objet rétréci deviendrait invisible.")]
        public float tailleMinPourChanger = 0.12f;

        [Min(0f)]
        [Tooltip("Marge sur la taille retrouvée : 0,3 = entre ×0,77 et ×1,3 de la taille d'origine. 0 = la taille n'est pas évaluée.")]
        public float toleranceRatioTaille = 0f;

        [Tooltip("Laisse le joueur agrandir / rétrécir les objets (indispensable dès qu'on change des tailles).")]
        public bool permettreRedimensionnement = false;

        [Header("Évaluation")]
        [Min(0f)]
        [Tooltip("Marge d'erreur (m) : un objet est bien placé s'il est à moins de cette distance de son origine.")]
        public float toleranceMetres = 0.5f;

        [Min(0f)]
        [Tooltip("Marge proportionnelle : multiple de la taille de l'objet. La plus large des deux marges s'applique.")]
        public float toleranceTaille = 1f;

        [Min(1)]
        [Tooltip("Nombre de validations autorisées.")]
        public int nombreEssais = 1;

        [Range(0f, 1f)]
        [Tooltip("Part des objets à replacer correctement pour réussir l'étape (0,8 = 4 sur 5).")]
        public float seuilReussite = 0.8f;

        [Tooltip("À la fin, montrer pour chaque objet raté l'endroit où il devait être.")]
        public bool afficherSolution = true;
    }
}
