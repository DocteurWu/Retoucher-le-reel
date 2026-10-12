using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Suit la position et la rotation du casque VR (CenterEye) avec mode Floor et fallback InputSystem.
    /// </summary>
    public class VRHeadTracker : MonoBehaviour
    {
        private InputAction _posAction;
        private InputAction _rotAction;
        private bool _floorOriginConfigured;

        private void Awake()
        {
            _posAction = new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition");
            _rotAction = new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation");
        }

        private void OnEnable()
        {
            _posAction?.Enable();
            _rotAction?.Enable();
            Application.onBeforeRender += OnBeforeRenderHead;
            TryConfigureFloorOrigin();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRenderHead;
            _posAction?.Disable();
            _rotAction?.Disable();
        }

        private void Update()
        {
            if (!_floorOriginConfigured)
            {
                TryConfigureFloorOrigin();
            }
            UpdateHeadPose();
        }

        [BeforeRenderOrder(-30000)]
        private void OnBeforeRenderHead()
        {
            UpdateHeadPose();
        }

        private void TryConfigureFloorOrigin()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var sub in subsystems)
            {
                if (sub != null && sub.running)
                {
                    if (sub.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
                    {
                        _floorOriginConfigured = true;
                        // En mode Floor, le parent (Camera Offset) doit être à y = 0
                        if (transform.parent != null)
                        {
                            transform.parent.localPosition = Vector3.zero;
                        }
                    }
                }
            }
        }

        private void UpdateHeadPose()
        {
            XRInputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (headDevice.isValid)
            {
                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 pos) ||
                    headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                {
                    transform.localPosition = pos;
                }

                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rot) ||
                    headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
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
    }
}
