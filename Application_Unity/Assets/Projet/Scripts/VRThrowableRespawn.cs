using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace RemodelerLeReel.VR
{
    /// <summary>
    /// Replace automatiquement le cylindre sur son socle s'il tombe hors du sol
    /// ou si le joueur appuie sur le bouton A / X de la manette VR (ou touche R au clavier).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VRThrowableRespawn : MonoBehaviour
    {
        public float minHeightBeforeRespawn = -2.0f;

        private Rigidbody _rb;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private bool _wasRecallPressed;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
        }

        private void Update()
        {
            if (transform.position.y < minHeightBeforeRespawn)
            {
                Respawn();
                return;
            }

            bool recallPressed = IsRecallButtonPressed();
            if (recallPressed && !_wasRecallPressed)
            {
                Respawn();
            }
            _wasRecallPressed = recallPressed;
        }

        private bool IsRecallButtonPressed()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                return true;

            XRInputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed) && aPressed)
                return true;

            XRInputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftHand.isValid && leftHand.TryGetFeatureValue(CommonUsages.primaryButton, out bool xPressed) && xPressed)
                return true;

            return false;
        }

        public void Respawn()
        {
            // Détacher si une main le tient encore
            VRGrabHand[] hands = FindObjectsByType<VRGrabHand>(FindObjectsSortMode.None);
            foreach (var hand in hands)
            {
                if (transform.IsChildOf(hand.transform))
                {
                    hand.ReleaseObject(applyThrowVelocity: false);
                }
            }

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        }
    }
}
