// =====================================================================================
//  VRLocomotionSettings.cs — MODE DE LOCOMOTION (état partagé menu ↔ jeu)
//
//  RÔLE
//    Le menu écrit le mode choisi, la scène de jeu le lit :
//      • Smooth  (défaut) : le stick gauche translate le joueur en continu.
//      • Teleport          : le stick gauche vise et téléporte.
//
//  Le mode est PERSISTÉ en PlayerPrefs (clé « RemodelerLeReel_LocomotionMode »)
//  : il est donc conservé d'une session à l'autre.
//
//  ⚠️ Ce réglage est lu par VRSmoothLocomotion, VRTeleportation et le menu :
//     ne pas le dupliquer ailleurs.
//
//  RÉFÉRENCES : voir README.md (sections 3 et 4)
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// MODES DE LOCOMOTION proposed au joueur dans le menu principal.
    ///
    /// TelegraphAndSmooth : stick G avant = arc de téléportation,
    ///   stick D avant = translation continue.
    /// SmoothOnly : pas de téléportation, stick G avant = translation continue.
    ///
    /// Le stick latéral gauche et la rotation par crans (stick D côté) sont
    /// identiques dans les deux modes : on n'apprend le contrôle qu'une fois.
    /// </summary>
    public enum LocomotionMode
    {
        /// <summary>Stick gauche = Téléportation balistique.</summary>
        Teleport = 0,

        /// <summary>Stick gauche = Mouvement continu 2D (avant/arrière/latéral).</summary>
        Smooth = 1,

        // Alias rétrocompatibilité
        TeleportAndSmooth = Teleport,
        SmoothOnly = Smooth
    }

    /// <summary>
    /// DÉPLACEMENT EN MODE MAINS (suivi des mains, sans manette). Un seul des deux à
    /// la fois : le poing fermé sert aux deux gestes, ils se gêneraient.
    /// La rotation (glissement du pouce sur l'index) marche dans les deux modes.
    /// </summary>
    public enum HandLocomotionMode
    {
        /// <summary>Microgeste Meta : tap du pouce sur l'index = arc, 2e tap = départ.</summary>
        Teleport = 0,

        /// <summary>Escalade : poing fermé main vide puis tirer.</summary>
        Climb = 1
    }

    /// <summary>
    /// RÉGLAGES DE LOCOMOTION — état de session partagé entre le menu et le jeu.
    ///
    /// Le menu écrit le mode choisi, la scène de jeu le lit au chargement.
    ///   - Téléportation : Stick gauche = visée balistique et saut à la destination.
    ///   - Mouvement Continu : Stick gauche = translation 2D fluide (avant/arrière/strafe).
    /// Dans les deux modes :
    ///   - Stick droit = SnapTurn (ou Scale quand un objet est tenu).
    ///   - Bouton A = Sauter.
    ///   - Bouton X = Dupliquer l'objet tenu.
    ///   - Bouton Y = Supprimer l'objet tenu.
    /// </summary>
    public static class VRLocomotionSettings
    {
        /// <summary>Mode appliqué au lancement de l'application.</summary>
        public const LocomotionMode DefaultMode = LocomotionMode.Smooth;

        private const string PrefsKey = "RemodelerLeReel_LocomotionMode";
        private static LocomotionMode? _cachedMode;

        /// <summary>
        /// Mode actuellement sélectionné (persisté entre le menu et le jeu).
        /// </summary>
        public static LocomotionMode Mode
        {
            get
            {
                if (!_cachedMode.HasValue)
                {
                    _cachedMode = (LocomotionMode)PlayerPrefs.GetInt(PrefsKey, (int)DefaultMode);
                }
                return _cachedMode.Value;
            }
            set
            {
                _cachedMode = value;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Nombre de modes disponibles.</summary>
        public const int ModeCount = 2;

        // --- Mode MAINS ------------------------------------------------------------

        /// <summary>Mode mains appliqué au premier lancement.</summary>
        public const HandLocomotionMode DefaultHandMode = HandLocomotionMode.Teleport;

        private const string HandPrefsKey = "RemodelerLeReel_HandLocomotionMode";
        private static HandLocomotionMode? _cachedHandMode;

        /// <summary>Déplacement en mode mains (persisté comme <see cref="Mode"/>).</summary>
        public static HandLocomotionMode HandMode
        {
            get
            {
                if (!_cachedHandMode.HasValue)
                {
                    _cachedHandMode = (HandLocomotionMode)PlayerPrefs.GetInt(HandPrefsKey, (int)DefaultHandMode);
                }
                return _cachedHandMode.Value;
            }
            set
            {
                _cachedHandMode = value;
                PlayerPrefs.SetInt(HandPrefsKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Passe au mode mains suivant et le renvoie.</summary>
        public static HandLocomotionMode CycleHand()
        {
            HandMode = HandMode == HandLocomotionMode.Teleport ? HandLocomotionMode.Climb : HandLocomotionMode.Teleport;
            return HandMode;
        }

        /// <summary>Libellé lisible du mode mains (FR).</summary>
        public static string HandLabel(HandLocomotionMode mode)
        {
            return mode == HandLocomotionMode.Teleport ? "Téléportation (mains)" : "Escalade (mains)";
        }

        /// <summary>Passe au mode suivant et renvoie le nouveau mode.</summary>
        public static LocomotionMode Cycle()
        {
            Mode = Mode == LocomotionMode.Teleport
                ? LocomotionMode.Smooth
                : LocomotionMode.Teleport;
            return Mode;
        }

        /// <summary>Libellé lisible du mode (FR), affiché sur le bouton du menu.</summary>
        public static string Label(LocomotionMode mode)
        {
            return mode == LocomotionMode.Teleport
                ? "Téléportation"
                : "Mouvement Continu";
        }

        /// <summary>Libellé du mode actuellement sélectionné.</summary>
        public static string Label()
        {
            return Label(Mode);
        }

        /// <summary>
        /// Réinitialise le mode (utile pour les tests et pour revenir au
        /// réglage par défaut sans relancer l'application).
        /// </summary>
        public static void Reset()
        {
            Mode = DefaultMode;
        }

        /// <summary>Position 1-based du mode courant, pour un affichage « 1/2 ».</summary>
        public static int ModeIndex()
        {
            return (int)Mode + 1;
        }
    }
}