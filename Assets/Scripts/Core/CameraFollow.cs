using UnityEngine;

namespace Swarmblastrr.Core
{
    /// <summary>
    /// Keeps the main camera behind and above the player crowd. Only the target's Z is followed;
    /// X stays pinned to the lane centre so the whole lane is always on screen.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraFollow : MonoBehaviour
    {
        /// <summary>Transform to follow along +Z. Usually the player crowd.</summary>
        public Transform Target;

        /// <summary>Camera position relative to the followed Z (X and Y are absolute).</summary>
        public Vector3 Offset = new Vector3(0f, 9f, -11f);

        /// <summary>How far ahead of the target (along +Z) the camera looks.</summary>
        public float LookAheadZ = 6f;

        /// <summary>Exponential smoothing rate; higher is snappier. Zero or less snaps every frame.</summary>
        public float Smoothing = 6f;

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            Vector3 desired = DesiredPosition();
            if (Smoothing <= 0f)
            {
                transform.position = desired;
            }
            else
            {
                float t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, desired, t);
            }

            ApplyLookRotation();
        }

        /// <summary>
        /// Moves the camera to its ideal position immediately, without smoothing. Used when the
        /// level is (re)built so the camera never swoops across the whole track.
        /// </summary>
        public void SnapToTarget()
        {
            if (Target == null)
            {
                return;
            }

            transform.position = DesiredPosition();
            ApplyLookRotation();
        }

        private Vector3 DesiredPosition()
        {
            return new Vector3(Offset.x, Offset.y, Target.position.z + Offset.z);
        }

        private Vector3 LookPoint()
        {
            return new Vector3(0f, Target.position.y, Target.position.z + LookAheadZ);
        }

        private void ApplyLookRotation()
        {
            Vector3 forward = LookPoint() - transform.position;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
