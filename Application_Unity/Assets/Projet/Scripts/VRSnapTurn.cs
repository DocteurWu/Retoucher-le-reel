using System.Collections.Generic;
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
            string handSide = handNode == XRNode.RightHand ? "rightHand" : "leftHand";
            _thumbstickAction = new InputAction("SnapTurnThumbstick", InputActionType.Value);
            _thumbstickAction.AddBinding($"<XRController>{handTag}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{handTag}/primary2DAxis");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/thumbstick");
            _thumbstickAction.AddBinding($"<XRController>{{{handSide}}}/primary2DAxis");
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
            if (!device.isValid)
            {
                var inputDevices = new List<XRInputDevice>();
                InputDeviceCharacteristics characteristics = InputDeviceCharacteristics.Controller |
                    (handNode == XRNode.LeftHand ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right);
                InputDevices.GetDevicesWithCharacteristics(characteristics, inputDevices);
                if (inputDevices.Count > 0)
                {
                    device = inputDevices[0];
                }
            }

            if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis.x;
            }

            if (_thumbstickAction != null && _thumbstickAction.enabled)
            {
                Vector2 actionVal = _thumbstickAction.ReadValue<Vector2>();
                if (Mathf.Abs(actionVal.x) > 0.05f) return actionVal.x;
            }

            // Fallback direct New Input System Devices
            bool isLeft = handNode == XRNode.LeftHand;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                if (!dev.added) continue;
                bool isTarget = isLeft
                    ? (dev.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand) || dev.name.ToLower().Contains("left"))
                    : (dev.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand) || dev.name.ToLower().Contains("right"));
                if (!isTarget) continue;

                var stick = dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick") ??
                            dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis");
                if (stick != null)
                {
                    Vector2 v = stick.ReadValue();
                    if (Mathf.Abs(v.x) > 0.05f) return v.x;
                }
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
