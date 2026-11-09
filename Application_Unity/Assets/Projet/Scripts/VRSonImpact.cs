// Authored by Louai HAMLAT - All rights reserved
// =====================================================================================
//  VRSonImpact.cs — SON DE CHOC D'UN OBJET
//
//  RÔLE
//    Joue <see cref="VRSon.Impact"/> quand l'objet heurte quelque chose, avec un
//    volume et une hauteur proportionnels à la violence du choc. Un objet lancé
//    qui retombe « sonne » : c'est le retour qui rend la physique crédible.
//
//  À POSER SUR : un objet saisissable (Rigidbody). Ajouté AUTOMATIQUEMENT par
//    <see cref="VRGrabHand"/> à la première saisie : inutile de le poser à la main,
//    sauf pour régler ses valeurs.
// =====================================================================================
using UnityEngine;

namespace RemodelerLeReel.VR
{
    /// <summary>Son d'impact proportionnel à la vitesse relative du choc.</summary>
    [AddComponentMenu("Remodeler le Reel/VR/Son d'impact")]
    [RequireComponent(typeof(Rigidbody))]
    public class VRSonImpact : MonoBehaviour
    {
        [Tooltip("Vitesse de choc (m/s) en dessous de laquelle rien n'est joué (glissements, roulements).")]
        public float vitesseMin = 0.5f;

        [Tooltip("Vitesse de choc (m/s) qui donne le volume maximal.")]
        public float vitesseMax = 5f;

        [Tooltip("Temps minimal entre deux sons (évite les rafales quand l'objet rebondit).")]
        public float intervalleMin = 0.08f;

        [Tooltip("Son propre à cet objet (vide = son de la banque / procédural).")]
        public AudioClip clip;

        private Rigidbody _corps;
        private float _dernierSon = -10f;
        private AudioSource _sourcePropre;

        private void Awake() => _corps = GetComponent<Rigidbody>();

        private void OnCollisionEnter(Collision collision)
        {
            // Tenu en main (cinématique) : les contacts ne sont pas des chocs.
            if (_corps != null && _corps.isKinematic) return;
            if (Time.time - _dernierSon < intervalleMin) return;

            float vitesse = collision.relativeVelocity.magnitude;
            if (vitesse < vitesseMin) return;

            float force = Mathf.InverseLerp(vitesseMin, vitesseMax, vitesse);
            float volume = Mathf.Lerp(0.15f, 1f, force);
            // Choc violent = un peu plus grave ; objet gros = plus grave aussi.
            float taille = Mathf.Clamp(transform.lossyScale.magnitude, 0.2f, 4f);
            float pitch = Mathf.Lerp(1.15f, 0.9f, force) / Mathf.Sqrt(taille) * VRSons.PitchAleatoire(0.08f);

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            _dernierSon = Time.time;

            if (clip != null)
            {
                if (_sourcePropre == null)
                {
                    _sourcePropre = gameObject.AddComponent<AudioSource>();
                    _sourcePropre.playOnAwake = false;
                    _sourcePropre.spatialBlend = 1f;
                }
                _sourcePropre.pitch = pitch;
                _sourcePropre.PlayOneShot(clip, volume);
                return;
            }

            VRSons.Jouer(VRSon.Impact, point, volume, pitch);
        }
    }
}
