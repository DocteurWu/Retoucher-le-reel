using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Gère la téléportation VR par parabole balistique.
    /// Pousser le stick vers l'avant affiche l'arc et la cible au sol.
    /// Relâcher le stick téléporte le joueur avec compensation de la position de la tête.
    /// </summary>
    public class VRTeleportation : MonoBehaviour
    {
        [Header("Références")]
        [Tooltip("Origine du rayon de visée (généralement la manette gauche)")]
        public Transform rayOrigin;
        [Tooltip("La caméra VR (tête du joueur)")]
        public Camera headCamera;
        [Tooltip("Le repère XR Origin à déplacer")]
        public Transform xrOriginTransform;

        [Header("Configuration Entrée")]
        public XRNode handNode = XRNode.LeftHand;
        public float activationThreshold = 0.5f;
        public float releaseThreshold = 0.2f;

        [Header("Paramètres Balistiques")]
        public float initialVelocity = 9.0f;
        public int maxArcPoints = 35;
        public float timeStep = 0.04f;
        public LayerMask teleportLayerMask = ~0; // Tous les layers par défaut
        public float maxSurfaceSlopeAngle = 45f;

        [Header("Visuel")]
        public LineRenderer arcRenderer;
        public Transform reticleTransform;
        public Color validArcColor = new Color(0.1f, 0.8f, 1.0f, 0.8f);
        public Color invalidArcColor = new Color(1.0f, 0.2f, 0.2f, 0.4f);

        private bool _isAiming;
        private bool _hasValidTarget;
        private Vector3 _targetPosition;
        private Vector3 _targetNormal;

        private InputAction _thumbstickAction;

        private void Awake()
        {
            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (headCamera == null) headCamera = Camera.main;

            string handTag = handNode == XRNode.LeftHand ? "{LeftHand}" : "{RightHand}";
            _thumbstickAction = new InputAction("TeleportThumbstick", InputActionType.Value, $"<XRController>{handTag}/thumbstick");

            SetupLineRenderer();
            SetupReticle();
        }

        private void OnEnable()
        {
            _thumbstickAction?.Enable();
        }

        private void OnDisable()
        {
            _thumbstickAction?.Disable();
            SetVisualsActive(false);
        }

        private void Update()
        {
            // Auto-recherche du contrôleur si non renseigné
            if (rayOrigin == null)
            {
                var hands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
                foreach (var h in hands)
                {
                    if (h.handNode == handNode)
                    {
                        rayOrigin = h.transform;
                        break;
                    }
                }
            }

            Vector2 stick = ReadThumbstick();

            // Pousser le stick vers l'avant active la visée
            if (!_isAiming && stick.y > activationThreshold)
            {
                _isAiming = true;
                SetVisualsActive(true);
            }

            if (_isAiming)
            {
                UpdateArcAndTarget();

                // Relâcher le stick déclenche la téléportation
                if (stick.y < releaseThreshold)
                {
                    if (_hasValidTarget)
                    {
                        PerformTeleport(_targetPosition);
                    }

                    _isAiming = false;
                    SetVisualsActive(false);
                }
            }
        }

        private Vector2 ReadThumbstick()
        {
            // 1. Essai via XR InputDevice
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis;
            }

            // 2. Fallback via Input System
            if (_thumbstickAction != null && _thumbstickAction.controls.Count > 0)
            {
                return _thumbstickAction.ReadValue<Vector2>();
            }

            // 3. Fallback Clavier / Éditeur (Flèche haut ou touche T)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    return new Vector2(0f, 1f);
                }
            }

            return Vector2.zero;
        }

        private void UpdateArcAndTarget()
        {
            if (rayOrigin == null) return;

            Vector3 startPos = rayOrigin.position;
            Vector3 startVelocity = rayOrigin.forward * initialVelocity;
            Vector3 gravity = Physics.gravity;

            List<Vector3> points = new List<Vector3> { startPos };
            Vector3 currentPos = startPos;
            Vector3 currentVel = startVelocity;

            _hasValidTarget = false;

            for (int i = 0; i < maxArcPoints; i++)
            {
                Vector3 nextPos = currentPos + currentVel * timeStep + 0.5f * gravity * (timeStep * timeStep);
                currentVel += gravity * timeStep;

                Vector3 segmentDir = nextPos - currentPos;
                float segmentDist = segmentDir.magnitude;

                if (Physics.Raycast(currentPos, segmentDir.normalized, out RaycastHit hit, segmentDist, teleportLayerMask))
                {
                    points.Add(hit.point);

                    // Vérifier si la surface est suffisamment plate pour y marcher
                    float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                    if (slopeAngle <= maxSurfaceSlopeAngle)
                    {
                        _hasValidTarget = true;
                        _targetPosition = hit.point;
                        _targetNormal = hit.normal;
                    }
                    break;
                }

                points.Add(nextPos);
                currentPos = nextPos;
            }

            // Affichage de la ligne
            if (arcRenderer != null)
            {
                arcRenderer.positionCount = points.Count;
                arcRenderer.SetPositions(points.ToArray());

                Color col = _hasValidTarget ? validArcColor : invalidArcColor;
                arcRenderer.startColor = col;
                arcRenderer.endColor = new Color(col.r, col.g, col.b, 0.1f);
            }

            // Positionnement du réticule au sol
            if (reticleTransform != null)
            {
                reticleTransform.gameObject.SetActive(_hasValidTarget);
                if (_hasValidTarget)
                {
                    reticleTransform.position = _targetPosition + _targetNormal * 0.015f;
                    reticleTransform.rotation = Quaternion.FromToRotation(Vector3.up, _targetNormal);
                }
            }
        }

        private void PerformTeleport(Vector3 destination)
        {
            if (xrOriginTransform == null) return;

            // Formule précise : compenser la position de la tête du joueur dans la pièce
            // pour que la caméra arrive pile à la cible, pas le centre de la pièce.
            Vector3 cameraOffset = Vector3.zero;
            if (headCamera != null)
            {
                cameraOffset = headCamera.transform.position - xrOriginTransform.position;
                cameraOffset.y = 0f; // On ne compense que sur le plan horizontal XZ
            }

            Vector3 newPosition = new Vector3(
                destination.x - cameraOffset.x,
                destination.y,
                destination.z - cameraOffset.z
            );

            xrOriginTransform.position = newPosition;
        }

        private void SetVisualsActive(bool active)
        {
            if (arcRenderer != null) arcRenderer.enabled = active;
            if (reticleTransform != null) reticleTransform.gameObject.SetActive(active && _hasValidTarget);
        }

        private void SetupLineRenderer()
        {
            if (arcRenderer != null) return;

            arcRenderer = GetComponent<LineRenderer>();
            if (arcRenderer == null)
            {
                arcRenderer = gameObject.AddComponent<LineRenderer>();
            }

            arcRenderer.useWorldSpace = true;
            arcRenderer.startWidth = 0.02f;
            arcRenderer.endWidth = 0.005f;
            arcRenderer.positionCount = 0;
            arcRenderer.enabled = false;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material lineMat = new Material(unlit);
            lineMat.color = validArcColor;
            arcRenderer.material = lineMat;
        }

        private void SetupReticle()
        {
            if (reticleTransform != null) return;

            // Création d'un réticule discret (disque plat) au sol
            GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "TeleportReticle";
            reticle.transform.SetParent(transform, false);
            reticle.transform.localScale = new Vector3(0.5f, 0.005f, 0.5f);

            // Retirer le collider pour ne pas bloquer les rayons
            Collider c = reticle.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material reticleMat = new Material(unlit);
            reticleMat.color = new Color(0.1f, 0.8f, 1.0f, 0.7f);
            reticle.GetComponent<Renderer>().sharedMaterial = reticleMat;

            reticleTransform = reticle.transform;
            reticle.SetActive(false);
        }
    }
}
