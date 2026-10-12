using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Gère la rotation par crans (Snap Turn) avec la manette droite.
    /// Tourne le repère du joueur autour de sa tête par incrément de 45° sans provoquer de cinétose.
    /// </summary>
    public class VRSnapTurn : MonoBehaviour
    {
        [Header("Références")]
        public Transform xrOriginTransform;
        public Camera headCamera;

        [Header("Configuration")]
        public XRNode handNode = XRNode.RightHand;
        public float turnAngle = 45f;
        public float activationThreshold = 0.6f;
        public float resetThreshold = 0.2f;

        private bool _canTurn = true;
        private InputAction _thumbstickAction;

        private void Awake()
        {
            if (xrOriginTransform == null) xrOriginTransform = transform;
            if (headCamera == null) headCamera = Camera.main;

            string handTag = handNode == XRNode.RightHand ? "{RightHand}" : "{LeftHand}";
            _thumbstickAction = new InputAction("SnapTurnThumbstick", InputActionType.Value, $"<XRController>{handTag}/thumbstick");
        }

        private void OnEnable()
        {
            _thumbstickAction?.Enable();
        }

        private void OnDisable()
        {
            _thumbstickAction?.Disable();
        }

        private void Update()
        {
            float axisX = ReadThumbstickX();

            if (_canTurn)
            {
                if (axisX > activationThreshold)
                {
                    PerformTurn(turnAngle);
                    _canTurn = false;
                }
                else if (axisX < -activationThreshold)
                {
                    PerformTurn(-turnAngle);
                    _canTurn = false;
                }
            }
            else
            {
                // Réarmer lorsque le stick revient au centre
                if (Mathf.Abs(axisX) < resetThreshold)
                {
                    _canTurn = true;
                }
            }
        }

        private float ReadThumbstickX()
        {
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis.x;
            }

            if (_thumbstickAction != null && _thumbstickAction.controls.Count > 0)
            {
                return _thumbstickAction.ReadValue<Vector2>().x;
            }

            // Fallback Clavier (Q / D ou Flèches gauche / droite)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.qKey.isPressed) return -1f;
                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) return 1f;
            }

            return 0f;
        }

        private void PerformTurn(float angle)
        {
            if (xrOriginTransform == null) return;

            Vector3 pivotPoint = headCamera != null ? headCamera.transform.position : xrOriginTransform.position;
            xrOriginTransform.RotateAround(pivotPoint, Vector3.up, angle);
        }
    }
}
