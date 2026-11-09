namespace Walkpadead.Core
{
    /// <summary>A fresh release is required after startup, suspension or tracking loss.</summary>
    public sealed class ReleaseToPress
    {
        private bool released;
        public void Reset() => released = false;

        public bool Read(float value, bool active)
        {
            if (!active || float.IsNaN(value) || float.IsInfinity(value))
            { Reset(); return false; }
            if (value <= 0.3f) released = true;
            if (!released || value < 0.7f) return false;
            released = false;
            return true;
        }
    }
}
