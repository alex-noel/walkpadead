using System;

namespace Walkpadead.Core
{
    public enum RunPhase { Ready, Running, Paused, Escaped, Defeated }

    /// <summary>Gameplay time only advances with fresh input and explicit player consent.</summary>
    public sealed class RunSession
    {
        public const double DurationSeconds = 120;
        public RunPhase Phase { get; private set; } = RunPhase.Ready;
        public string PauseReason { get; private set; } = "";
        public double ElapsedSeconds { get; private set; }
        public double DistanceM { get; private set; }
        public double SpeedMps { get; private set; }
        public double Health { get; private set; } = 100;
        public double HordeGapM { get; private set; } = 18;
        public int Kills { get; private set; }
        private Guid connectionId;
        private long safetyEpoch;

        public bool Resume(InputReading input)
        {
            if (input == null || !input.Available || (Phase != RunPhase.Ready && Phase != RunPhase.Paused))
                return false;
            connectionId = input.ConnectionId;
            safetyEpoch = input.SafetyEpoch;
            SpeedMps = 0;
            PauseReason = "";
            Phase = RunPhase.Running;
            return true;
        }

        public void Pause(string reason)
        {
            if (Phase != RunPhase.Running) return;
            Phase = RunPhase.Paused;
            PauseReason = reason;
            SpeedMps = 0;
        }

        /// <returns>Forward displacement in metres for this frame.</returns>
        public double Tick(InputReading input, double dt)
        {
            if (Phase != RunPhase.Running) return 0;
            if (input == null || !input.Available)
            { Pause(input?.Status ?? "Waiting for input"); return 0; }
            if (input.ConnectionId != connectionId || input.SafetyEpoch != safetyEpoch)
            { Pause("Input reconnected — resume when ready"); return 0; }
            if (double.IsNaN(dt) || double.IsInfinity(dt) || dt < 0 || dt > 0.5)
            { Pause("Game interrupted — resume when ready"); return 0; }
            dt = Math.Min(dt, DurationSeconds - ElapsedSeconds);
            // One Unity unit is one metre. Only reported speed drives movement;
            // a requested bonus target never predicts or overrides telemetry.
            SpeedMps = input.SpeedMps;
            var distance = SpeedMps * dt;
            DistanceM += distance;
            ElapsedSeconds += dt;
            HordeGapM = Math.Max(0, Math.Min(35, HordeGapM + distance - 0.65 * dt));
            if (HordeGapM <= 0) Damage(20 * dt);
            if (ElapsedSeconds >= DurationSeconds && Phase == RunPhase.Running)
            { Phase = RunPhase.Escaped; SpeedMps = 0; }
            return distance;
        }

        public void Damage(double damage)
        {
            if (Phase != RunPhase.Running || damage < 0 || double.IsNaN(damage) || double.IsInfinity(damage)) return;
            Health = Math.Max(0, Health - damage);
            if (Health == 0) { Phase = RunPhase.Defeated; SpeedMps = 0; }
        }

        public void RecordKill() { if (Phase == RunPhase.Running) Kills++; }
    }
}
