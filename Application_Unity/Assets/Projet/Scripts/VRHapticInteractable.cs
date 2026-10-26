// =====================================================================================
//  VRHapticInteractable.cs — PROFIL HAPTIQUE PAR OBJET
//
//  RÔLE
//    Composant optionnel à placer sur un objet interactif (fruit, outil, pièce de machine).
//    Permet de définir une signature tactile personnalisée pour cet objet :
//      - Vibration au survol (hover)
//      - Vibration à la saisie (grab) : intensité, durée, priorité, pattern (clic sec, double pulsation...)
//      - Vibration continue ou d'action (moteur, outil en marche, tension de ressort)
//      - Vibration au relâchement / lancer
//
//    Si l'objet tenu n'a pas ce composant, la manette utilise son profil par défaut.
// =====================================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.XR;

namespace RemodelerLeReel.VR
{
    public enum HapticPattern
    {
        SimplePulse,    // Une impulsion nette (défaut)
        DoublePulse,    // Deux pulsations rapides (ex: loquet mécanique, validation)
        TriplePulse,    // Trois pulsations
        WarningBuzz     // Saccade rapide (ex: alerte, limite atteinte)
    }

    [System.Serializable]
    public struct HapticFeedbackSettings
    {
        [Tooltip("Active cette vibration")]
        public bool enabled;

        [Range(0f, 1f), Tooltip("Intensité de la vibration (0 = rien, 1 = maximum)")]
        public float intensity;

        [Tooltip("Durée de la vibration en secondes")]
        public float duration;

        [Tooltip("Priorité (0 = faible, 10 = critique). Une vibration de priorité inférieure n'écrase pas une vibration en cours")]
        public int priority;

        [Tooltip("Forme de l'impulsion")]
        public HapticPattern pattern;

        public static HapticFeedbackSettings DefaultGrab => new HapticFeedbackSettings
        {
            enabled = true,
            intensity = 0.5f,
            duration = 0.08f,
            priority = 2,
            pattern = HapticPattern.SimplePulse
        };

        public static HapticFeedbackSettings DefaultRelease => new HapticFeedbackSettings
        {
            enabled = true,
            intensity = 0.25f,
            duration = 0.05f,
            priority = 1,
            pattern = HapticPattern.SimplePulse
        };
    }

    [AddComponentMenu("Remodeler le Reel/VR/VR Haptic Interactable (Profil Haptique)")]
    public class VRHapticInteractable : MonoBehaviour
    {
        [Header("--- Événements de Saisie ---")]
        [Tooltip("Signature vibratoire au moment où la main saisit cet objet")]
        public HapticFeedbackSettings onGrab = HapticFeedbackSettings.DefaultGrab;

        [Tooltip("Signature vibratoire au moment où l'objet est relâché ou lancé")]
        public HapticFeedbackSettings onRelease = HapticFeedbackSettings.DefaultRelease;

        [Header("--- Retour Continu / Action ---")]
        [Tooltip("Intensité constante pendant que l'objet est tenu (ex: outil vibrant, moteur)")]
        [Range(0f, 1f)] public float continuousHeldIntensity = 0f;

        // Référence vers la coroutine d'un pattern en cours
        private Coroutine _patternCoroutine;

        /// <summary>
        /// Joue la vibration demandée sur la main spécifiée selon la configuration et la priorité.
        /// </summary>
        public void PlayFeedback(VRGrabHand hand, HapticFeedbackSettings settings)
        {
            if (!settings.enabled || hand == null || settings.intensity <= 0f || settings.duration <= 0f) return;

            if (settings.pattern == HapticPattern.SimplePulse)
            {
                hand.TriggerHaptic(settings.intensity, settings.duration, settings.priority);
            }
            else
            {
                if (_patternCoroutine != null) StopCoroutine(_patternCoroutine);
                _patternCoroutine = StartCoroutine(PlayPatternRoutine(hand, settings));
            }
        }

        private IEnumerator PlayPatternRoutine(VRGrabHand hand, HapticFeedbackSettings settings)
        {
            switch (settings.pattern)
            {
                case HapticPattern.DoublePulse:
                    hand.TriggerHaptic(settings.intensity, settings.duration * 0.4f, settings.priority);
                    yield return new WaitForSeconds(settings.duration * 0.3f);
                    hand.TriggerHaptic(settings.intensity, settings.duration * 0.4f, settings.priority);
                    break;

                case HapticPattern.TriplePulse:
                    float pLen = settings.duration * 0.25f;
                    for (int i = 0; i < 3; i++)
                    {
                        hand.TriggerHaptic(settings.intensity, pLen, settings.priority);
                        yield return new WaitForSeconds(pLen * 0.8f);
                    }
                    break;

                case HapticPattern.WarningBuzz:
                    for (int i = 0; i < 4; i++)
                    {
                        hand.TriggerHaptic(settings.intensity, 0.03f, settings.priority);
                        yield return new WaitForSeconds(0.04f);
                    }
                    break;
            }

            _patternCoroutine = null;
        }

        /// <summary>
        /// Déclenche une vibration libre avec réglages explicites.
        /// </summary>
        public void TriggerCustomFeedback(VRGrabHand hand, float intensity, float duration, int priority = 1, HapticPattern pattern = HapticPattern.SimplePulse)
        {
            var custom = new HapticFeedbackSettings
            {
                enabled = true,
                intensity = intensity,
                duration = duration,
                priority = priority,
                pattern = pattern
            };
            PlayFeedback(hand, custom);
        }
    }
}
