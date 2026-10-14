using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Contrôleur VR Complet : Gère le suivi spatial 6-DOF (Position & Rotation) de la manette
    /// ainsi que la saisie (Grab) et le lancer physique (Throw) d'objets.
    /// Compatible Meta Quest 3 standalone (OpenXR) et PCVR.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [AddComponentMenu("Remodeler le Reel/VR/VR Grab Hand (Contrôleur et Saisie)")]
    public class VRGrabHand : MonoBehaviour
    {
        [Header("--- Suivi Spatial de la Manette (6-DOF) ---")]
        [Tooltip("Main ciblée (LeftHand ou RightHand)")]
        public XRNode handNode = XRNode.RightHand;

        [Header("--- Interaction Physique & Saisie ---")]
        [Tooltip("Rayon de détection pour attraper les objets")]
        public float grabRadius = 0.18f;
        [Tooltip("Multiplicateur d'impulsion lors du lancer")]
        public float throwForceMultiplier = 1.35f;

        private Rigidbody _grabbedBody;
        private Transform _originalParent;
        private bool _wasGrabPressed;

        // Historique des vitesses pour un lancer fluide et naturel
        private const int VelocitySampleCount = 8;
        private readonly Queue<Vector3> _velocitySamples = new Queue<Vector3>(VelocitySampleCount);
        private readonly Queue<Vector3> _angularVelocitySamples = new Queue<Vector3>(VelocitySampleCount);
        private Vector3 _lastWorldPos;
        private Quaternion _lastWorldRot;

        private InputAction _gripAction;
        private InputAction _triggerAction;
        private InputAction _posAction;
        private InputAction _rotAction;
        private InputAction _posPointerAction;
        private InputAction _rotPointerAction;

        private readonly List<XRNodeState> _nodeStates = new List<XRNodeState>();
        private readonly Collider[] _overlapResults = new Collider[16];

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = grabRadius;

            string handTag = handNode == XRNode.LeftHand ? "{LeftHand}" : "{RightHand}";
            string handSide = handNode == XRNode.LeftHand ? "leftHand" : "rightHand";
            string handPrefix = handNode == XRNode.LeftHand ? "Left" : "Right";

            // Actions Boutons de saisie
            _gripAction = new InputAction("Grip", InputActionType.Value);
            _gripAction.AddBinding($"<XRController>{handTag}/grip");
            _gripAction.AddBinding($"<XRController>{handTag}/gripPressed");
            _gripAction.AddBinding($"<XRController>{{{handSide}}}/grip");
            _gripAction.AddBinding($"<XRController>{{{handSide}}}/gripPressed");
            _gripAction.AddBinding($"<{handPrefix}HandXRController>/grip");
            _gripAction.AddBinding($"<{handPrefix}HandXRController>/gripPressed");

            _triggerAction = new InputAction("Trigger", InputActionType.Value);
            _triggerAction.AddBinding($"<XRController>{handTag}/trigger");
            _triggerAction.AddBinding($"<XRController>{handTag}/triggerPressed");
            _triggerAction.AddBinding($"<XRController>{{{handSide}}}/trigger");
            _triggerAction.AddBinding($"<XRController>{{{handSide}}}/triggerPressed");
            _triggerAction.AddBinding($"<{handPrefix}HandXRController>/trigger");
            _triggerAction.AddBinding($"<{handPrefix}HandXRController>/triggerPressed");

            // Actions Pose (Grip & Pointer)
            _posAction = new InputAction("Position", InputActionType.Value, expectedControlType: "Vector3");
            _posAction.AddBinding($"<XRController>{handTag}/devicePosition");
            _posAction.AddBinding($"<XRController>{{{handSide}}}/devicePosition");
            _posAction.AddBinding($"<{handPrefix}HandXRController>/devicePosition");

            _rotAction = new InputAction("Rotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotAction.AddBinding($"<XRController>{handTag}/deviceRotation");
            _rotAction.AddBinding($"<XRController>{{{handSide}}}/deviceRotation");
            _rotAction.AddBinding($"<{handPrefix}HandXRController>/deviceRotation");

            _posPointerAction = new InputAction("PointerPosition", InputActionType.Value, expectedControlType: "Vector3");
            _posPointerAction.AddBinding($"<XRController>{handTag}/pointerPosition");
            _posPointerAction.AddBinding($"<XRController>{{{handSide}}}/pointerPosition");
            _posPointerAction.AddBinding($"<{handPrefix}HandXRController>/pointerPosition");

            _rotPointerAction = new InputAction("PointerRotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotPointerAction.AddBinding($"<XRController>{handTag}/pointerRotation");
            _rotPointerAction.AddBinding($"<XRController>{{{handSide}}}/pointerRotation");
            _rotPointerAction.AddBinding($"<{handPrefix}HandXRController>/pointerRotation");
        }

        private void OnEnable()
        {
            _gripAction?.Enable();
            _triggerAction?.Enable();
            _posAction?.Enable();
            _rotAction?.Enable();
            _posPointerAction?.Enable();
            _rotPointerAction?.Enable();

            Application.onBeforeRender += OnBeforeRenderHand;

            _lastWorldPos = transform.position;
            _lastWorldRot = transform.rotation;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderHand;

            if (_grabbedBody != null)
            {
                ReleaseObject();
            }

            _gripAction?.Disable();
            _triggerAction?.Disable();
            _posAction?.Disable();
            _rotAction?.Disable();
            _posPointerAction?.Disable();
            _rotPointerAction?.Disable();
        }

        [BeforeRenderOrder(-20000)]
        private void OnBeforeRenderHand()
        {
            UpdateHandTracking();
        }

        private void Update()
        {
            UpdateHandTracking();
            SampleHandVelocity();

            bool isPressed = IsGrabPressed();

            if (isPressed && !_wasGrabPressed)
            {
                TryGrabClosestObject();
            }
            else if (!isPressed && _wasGrabPressed)
            {
                if (_grabbedBody != null)
                {
                    ReleaseObject();
                }
            }

            _wasGrabPressed = isPressed;
        }

        private void UpdateHandTracking()
        {
            Vector3 pos;
            Quaternion rot;

            // Système de suivi à 4 couches prioritaires :
            // 1. XRNodeStates (Subsystem natif bas niveau OpenXR)
            // 2. New Input System Devices (XRController / TrackedDevice)
            // 3. InputDevices legacy XR (si TryGetFeatureValue disponible)
            // 4. Input Actions (Bindings par path)
            if (TryGetPoseFromNodeStates(out pos, out rot) ||
                TryGetPoseFromInputSystemDevices(out pos, out rot) ||
                TryGetPoseFromInputDevice(out pos, out rot) ||
                TryGetPoseFromInputActions(out pos, out rot))
            {
                if (pos != Vector3.zero)
                {
                    transform.localPosition = pos;
                }
                if (rot != Quaternion.identity)
                {
                    transform.localRotation = rot;
                }
            }
#if UNITY_EDITOR
            else
            {
                HandleEditorSimulation();
            }
#endif
        }

        private bool TryGetPoseFromNodeStates(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            _nodeStates.Clear();
            InputTracking.GetNodeStates(_nodeStates);

            for (int i = 0; i < _nodeStates.Count; i++)
            {
                var state = _nodeStates[i];
                if (state.nodeType == handNode)
                {
                    bool hasPos = state.TryGetPosition(out pos);
                    bool hasRot = state.TryGetRotation(out rot);
                    if ((hasPos && pos != Vector3.zero) || (hasRot && rot != Quaternion.identity))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryGetPoseFromInputSystemDevices(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            bool isLeft = handNode == XRNode.LeftHand;

            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                if (!dev.added) continue;

                if (!DeviceMatchesHand(dev, isLeft)) continue;

                var posControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("devicePosition") ??
                                 dev.GetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("pointerPosition");
                var rotControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("deviceRotation") ??
                                 dev.GetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("pointerRotation");

                if (posControl != null)
                {
                    Vector3 p = posControl.ReadValue();
                    if (p != Vector3.zero) pos = p;
                }
                if (rotControl != null)
                {
                    Quaternion r = rotControl.ReadValue();
                    if (r != Quaternion.identity) rot = r;
                }

                if (pos != Vector3.zero || rot != Quaternion.identity)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetPoseFromInputDevice(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;

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

            if (device.isValid)
            {
                bool hasP = device.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
                bool hasR = device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
                if ((hasP && pos != Vector3.zero) || (hasR && rot != Quaternion.identity))
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetPoseFromInputActions(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;

            if (_posAction != null && _posAction.enabled)
            {
                Vector3 p = _posAction.ReadValue<Vector3>();
                if (p != Vector3.zero) pos = p;
            }
            if (pos == Vector3.zero && _posPointerAction != null && _posPointerAction.enabled)
            {
                Vector3 p = _posPointerAction.ReadValue<Vector3>();
                if (p != Vector3.zero) pos = p;
            }

            if (_rotAction != null && _rotAction.enabled)
            {
                Quaternion r = _rotAction.ReadValue<Quaternion>();
                if (r != Quaternion.identity) rot = r;
            }
            if (rot == Quaternion.identity && _rotPointerAction != null && _rotPointerAction.enabled)
            {
                Quaternion r = _rotPointerAction.ReadValue<Quaternion>();
                if (r != Quaternion.identity) rot = r;
            }

            return pos != Vector3.zero || rot != Quaternion.identity;
        }

        private void SampleHandVelocity()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.0001f) return;

            Vector3 velocity = (transform.position - _lastWorldPos) / dt;
            Quaternion deltaRot = transform.rotation * Quaternion.Inverse(_lastWorldRot);
            deltaRot.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
            if (angleInDegrees > 180f) angleInDegrees -= 360f;
            Vector3 angularVelocity = (angleInDegrees * Mathf.Deg2Rad / dt) * rotationAxis.normalized;

            if (_velocitySamples.Count >= VelocitySampleCount) _velocitySamples.Dequeue();
            _velocitySamples.Enqueue(velocity);

            if (_angularVelocitySamples.Count >= VelocitySampleCount) _angularVelocitySamples.Dequeue();
            _angularVelocitySamples.Enqueue(angularVelocity);

            _lastWorldPos = transform.position;
            _lastWorldRot = transform.rotation;
        }

        private bool IsGrabPressed()
        {
            // 1. XRInputDevice legacy
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

            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.grip, out float gripVal) && gripVal > 0.5f) return true;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal) && triggerVal > 0.5f) return true;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripBtn) && gripBtn) return true;
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerBtn) && triggerBtn) return true;
            }

            // 2. Input Actions
            if (_gripAction != null && _gripAction.enabled && _gripAction.ReadValue<float>() > 0.5f) return true;
            if (_triggerAction != null && _triggerAction.enabled && _triggerAction.ReadValue<float>() > 0.5f) return true;

            // 3. New Input System Devices directs
            bool isLeft = handNode == XRNode.LeftHand;
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                var dev = devices[i];
                if (!DeviceMatchesHand(dev, isLeft)) continue;

                var gripControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("grip");
                if (gripControl != null && gripControl.ReadValue() > 0.5f) return true;

                var triggerControl = dev.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                if (triggerControl != null && triggerControl.ReadValue() > 0.5f) return true;

                var gripBtn = dev.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("gripPressed");
                if (gripBtn != null && gripBtn.isPressed) return true;

                var trigBtn = dev.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed");
                if (trigBtn != null && trigBtn.isPressed) return true;
            }

#if UNITY_EDITOR
            if (!XRSettings.isDeviceActive)
            {
                if (Mouse.current != null)
                {
                    bool isRight = handNode == XRNode.RightHand;
                    if (isRight && Mouse.current.rightButton.isPressed) return true;
                    if (!isRight && Mouse.current.leftButton.isPressed) return true;
                }
                if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
            }
#endif

            return false;
        }

        private Rigidbody FindClosestGrabbable()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, grabRadius, _overlapResults);
            Rigidbody closest = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _overlapResults[i];
                if (col == null || col.transform == transform) continue;

                Rigidbody rb = col.attachedRigidbody;
                if (rb == null) continue;

                float distSq = (col.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    closest = rb;
                }
            }

            return closest;
        }

        private void TryGrabClosestObject()
        {
            Rigidbody target = FindClosestGrabbable();
            if (target == null) return;

            VRGrabHand[] allHands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
            foreach (var otherHand in allHands)
            {
                if (otherHand != this && otherHand._grabbedBody == target)
                {
                    otherHand.ReleaseObject(applyThrowVelocity: false);
                }
            }

            _grabbedBody = target;
            _originalParent = _grabbedBody.transform.parent;

            _grabbedBody.isKinematic = true;
            _grabbedBody.useGravity = false;
            _grabbedBody.transform.SetParent(transform, true);

            _velocitySamples.Clear();
            _angularVelocitySamples.Clear();
        }

        public void ReleaseObject(bool applyThrowVelocity = true)
        {
            if (_grabbedBody == null) return;

            _grabbedBody.transform.SetParent(_originalParent, true);
            _grabbedBody.isKinematic = false;
            _grabbedBody.useGravity = true;

            if (applyThrowVelocity)
            {
                Vector3 avgVel = Vector3.zero;
                foreach (var v in _velocitySamples) avgVel += v;
                if (_velocitySamples.Count > 0) avgVel /= _velocitySamples.Count;

                Vector3 avgAngVel = Vector3.zero;
                foreach (var av in _angularVelocitySamples) avgAngVel += av;
                if (_angularVelocitySamples.Count > 0) avgAngVel /= _angularVelocitySamples.Count;

#if UNITY_6000_0_OR_NEWER
                _grabbedBody.linearVelocity = avgVel * throwForceMultiplier;
#else
                _grabbedBody.velocity = avgVel * throwForceMultiplier;
#endif
                _grabbedBody.angularVelocity = avgAngVel;
            }

            _grabbedBody = null;
            _originalParent = null;
        }

#if UNITY_EDITOR
        private void HandleEditorSimulation()
        {
            // Fallback Éditeur : si aucun casque XR physique n'est branché,
            // positionner la manette de façon ergonomique devant la caméra
            if (!XRSettings.isDeviceActive)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float side = (handNode == XRNode.LeftHand) ? -0.22f : 0.22f;
                    Vector3 defaultOffset = new Vector3(side, -0.15f, 0.45f);
                    transform.position = cam.transform.position + cam.transform.TransformDirection(defaultOffset);
                    transform.rotation = cam.transform.rotation;
                }
            }
        }
#endif

        private static bool DeviceMatchesHand(UnityEngine.InputSystem.InputDevice dev, bool isLeft)
        {
            if (dev == null || !dev.added) return false;

            string targetUsage = isLeft ? "LeftHand" : "RightHand";
            string targetName = isLeft ? "left" : "right";

            var usages = dev.usages;
            for (int j = 0; j < usages.Count; j++)
            {
                if (usages[j].ToString().IndexOf(targetUsage, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (dev.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }
    }
}
