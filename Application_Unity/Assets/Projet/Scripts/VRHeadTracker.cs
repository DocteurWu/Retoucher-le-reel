using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Suit la position et la rotation du casque VR (CenterEye) avec mode Floor et fallback InputSystem multi-niveaux.
    /// </summary>
    public class VRHeadTracker : MonoBehaviour
    {
        private InputAction _posAction;
        private InputAction _rotAction;
        private bool _floorOriginConfigured;
        private readonly List<XRNodeState> _nodeStates = new List<XRNodeState>();

        private void Awake()
        {
            EnsureXRStarted();

            _posAction = new InputAction("HeadPosition", InputActionType.Value, expectedControlType: "Vector3");
            _posAction.AddBinding("<XRHMD>/centerEyePosition");
            _posAction.AddBinding("<XRHMD>/devicePosition");

            _rotAction = new InputAction("HeadRotation", InputActionType.Value, expectedControlType: "Quaternion");
            _rotAction.AddBinding("<XRHMD>/centerEyeRotation");
            _rotAction.AddBinding("<XRHMD>/deviceRotation");
        }

        private void EnsureXRStarted()
        {
            try
            {
                var xrGeneral = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                if (xrGeneral != null && xrGeneral.Manager != null)
                {
                    if (!xrGeneral.Manager.isInitializationComplete)
                    {
                        xrGeneral.Manager.InitializeLoaderSync();
                    }
                    if (xrGeneral.Manager.activeLoader != null)
                    {
                        xrGeneral.Manager.StartSubsystems();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VRHeadTracker] Note initialisation XR: " + ex.Message);
            }
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
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            // 1. XRNodeStates (CenterEye)
            _nodeStates.Clear();
            InputTracking.GetNodeStates(_nodeStates);
            for (int i = 0; i < _nodeStates.Count; i++)
            {
                var state = _nodeStates[i];
                if (state.nodeType == XRNode.CenterEye)
                {
                    bool hasPos = state.TryGetPosition(out pos);
                    bool hasRot = state.TryGetRotation(out rot);
                    if ((hasPos && pos != Vector3.zero) || (hasRot && rot != Quaternion.identity))
                    {
                        if (pos != Vector3.zero) transform.localPosition = pos;
                        if (rot != Quaternion.identity) transform.localRotation = rot;
                        return;
                    }
                }
            }

            // 2. XRInputDevice legacy
            XRInputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (headDevice.isValid)
            {
                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out pos) ||
                    headDevice.TryGetFeatureValue(CommonUsages.devicePosition, out pos))
                {
                    if (pos != Vector3.zero) transform.localPosition = pos;
                }

                if (headDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rot) ||
                    headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rot))
                {
                    if (rot != Quaternion.identity) transform.localRotation = rot;
                }

                if (pos != Vector3.zero || rot != Quaternion.identity) return;
            }

            // 3. Input Actions fallback
            if (_posAction != null && _posAction.enabled)
            {
                Vector3 p = _posAction.ReadValue<Vector3>();
                if (p != Vector3.zero) transform.localPosition = p;
            }

            if (_rotAction != null && _rotAction.enabled)
            {
                Quaternion r = _rotAction.ReadValue<Quaternion>();
                if (r != Quaternion.identity) transform.localRotation = r;
            }
        }
    }
}
