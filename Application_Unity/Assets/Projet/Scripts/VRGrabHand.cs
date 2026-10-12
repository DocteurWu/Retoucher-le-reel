using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Gère le suivi d'une manette VR, la prise (Grab) et le lancer physique (Throw) d'objets Rigidbody.
    /// Pas de changement de couleur - conserve l'apparence authentique de la manette.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class VRGrabHand : MonoBehaviour
    {
        [Header("Configuration Manette")]
        public XRNode handNode = XRNode.RightHand;
        public float grabRadius = 0.12f;
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

        private readonly Collider[] _overlapResults = new Collider[16];

        private void Awake()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = grabRadius;

            string handTag = handNode == XRNode.LeftHand ? "{LeftHand}" : "{RightHand}";
            _gripAction = new InputAction("Grip", InputActionType.Value, $"<XRController>{handTag}/grip");
            _triggerAction = new InputAction("Trigger", InputActionType.Value, $"<XRController>{handTag}/trigger");
            _posAction = new InputAction("Position", InputActionType.Value, $"<XRController>{handTag}/devicePosition");
            _rotAction = new InputAction("Rotation", InputActionType.Value, $"<XRController>{handTag}/deviceRotation");
        }

        private void OnEnable()
        {
            _gripAction?.Enable();
            _triggerAction?.Enable();
            _posAction?.Enable();
            _rotAction?.Enable();

            _lastWorldPos = transform.position;
            _lastWorldRot = transform.rotation;
        }

        private void OnDisable()
        {
            if (_grabbedBody != null)
            {
                ReleaseObject();
            }

            _gripAction?.Disable();
            _triggerAction?.Disable();
            _posAction?.Disable();
            _rotAction?.Disable();
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
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                {
                    transform.localPosition = pos;
                }
                if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                {
                    transform.localRotation = rot;
                }
                return;
            }

            if (_posAction != null && _posAction.controls.Count > 0)
            {
                Vector3 pos = _posAction.ReadValue<Vector3>();
                if (pos != Vector3.zero) transform.localPosition = pos;
            }
            if (_rotAction != null && _rotAction.controls.Count > 0)
            {
                Quaternion rot = _rotAction.ReadValue<Quaternion>();
                if (rot != Quaternion.identity) transform.localRotation = rot;
            }
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
            XRInputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.grip, out float gripVal) && gripVal > 0.5f)
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float triggerVal) && triggerVal > 0.5f)
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool gripBtn) && gripBtn)
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerBtn) && triggerBtn)
                    return true;
            }

            if (_gripAction != null && _gripAction.ReadValue<float>() > 0.5f) return true;
            if (_triggerAction != null && _triggerAction.ReadValue<float>() > 0.5f) return true;

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

                _grabbedBody.linearVelocity = avgVel * throwForceMultiplier;
                _grabbedBody.angularVelocity = avgAngVel;
            }

            _grabbedBody = null;
            _originalParent = null;
        }
    }
}
