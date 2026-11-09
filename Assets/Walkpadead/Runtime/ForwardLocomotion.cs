using UnityEngine;

namespace Walkpadead.Game
{
    /// <summary>One Unity unit is one metre; gaze steering changes direction, never speed.</summary>
    public sealed class ForwardLocomotion : MonoBehaviour
    {
        public Vector3 Heading { get; private set; } = Vector3.forward;
        public void Advance(double metres) => Advance(metres, Vector3.forward);

        public void Advance(double metres, Vector3 lookDirection)
        {
            if (double.IsNaN(metres) || double.IsInfinity(metres) || metres < 0) return;
            var flat = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
            // Preserve the last heading when looking almost straight up or down.
            if (float.IsFinite(flat.sqrMagnitude) && flat.sqrMagnitude > 0.0025f)
                Heading = flat.normalized;
            transform.position += Heading * (float)metres;
        }
    }
}
