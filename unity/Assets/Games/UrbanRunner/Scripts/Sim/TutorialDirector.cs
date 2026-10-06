using GameFactory.Core.Input;

namespace UrbanRunner.Sim
{
    public enum TutorialPhase { Running, WaitingForInput, Ready, Done }

    /// <summary>
    /// First-run tutorial (HANDOVER §3.9): four prompts (left, right, jump, slide). Before each obstacle the run
    /// freezes until the player gives the right swipe; obstacles can't hurt; then "You're ready!" and the normal
    /// track takes over in the same run. Pure C#: the session asks it whether to advance the simulation.
    /// </summary>
    public sealed class TutorialDirector
    {
        public const float SpeedFactor = 0.65f;
        public const float ReadySeconds = 1.6f;

        struct Step
        {
            public Obstacle A, B, C;
            public SwipeDirection Required;
            public string Prompt;
        }

        static readonly Step[] Steps =
        {
            new Step { A = Obstacle.None, B = Obstacle.Stall, C = Obstacle.Stall, Required = SwipeDirection.Left, Prompt = "Swipe left" },
            new Step { A = Obstacle.Stall, B = Obstacle.None, C = Obstacle.Stall, Required = SwipeDirection.Right, Prompt = "Swipe right" },
            new Step { A = Obstacle.Stall, B = Obstacle.Crate, C = Obstacle.Stall, Required = SwipeDirection.Up, Prompt = "Swipe up to jump" },
            new Step { A = Obstacle.Stall, B = Obstacle.Banner, C = Obstacle.Stall, Required = SwipeDirection.Down, Prompt = "Swipe down to slide" },
        };

        readonly RunSimulation _sim;
        int _step;
        double _rowZ;
        float _readyLeft;

        public TutorialPhase Phase { get; private set; } = TutorialPhase.Running;
        public int StepIndex => _step;
        public int StepCount => Steps.Length;
        public string Prompt => Phase == TutorialPhase.WaitingForInput ? Steps[_step].Prompt : Phase == TutorialPhase.Ready ? "You're ready!" : null;
        public SwipeDirection Required => Phase == TutorialPhase.WaitingForInput ? Steps[_step].Required : SwipeDirection.None;
        public bool Completed => Phase == TutorialPhase.Done;

        public TutorialDirector(RunSimulation sim)
        {
            _sim = sim;
            sim.GodMode = true;
            sim.Track.AutoGenerate = false;
            sim.Track.Rows.Clear();
            sim.Track.Sparks.Clear();
            sim.Track.PowerUps.Clear();
            PlaceStep(sim.Distance + 28);
        }

        void PlaceStep(double z)
        {
            var s = Steps[_step];
            _rowZ = z;
            _sim.Track.PlaceRow(z, s.A, s.B, s.C);
        }

        /// <summary>
        /// Freeze point (distance from the runner to the row start) where the required action, made instantly,
        /// clears the obstacle: lane changes need room to move; a jump must peak over the crate's middle
        /// (u = 0.5 of the jump); a slide must already be low when the banner arrives.
        /// </summary>
        double TriggerDistance
        {
            get
            {
                var v = _sim.Speed;
                switch (Steps[_step].Required)
                {
                    case SwipeDirection.Up: return v * TrackRules.JumpDuration * 0.5 - TrackRules.Length(Obstacle.Crate) * 0.5;
                    case SwipeDirection.Down: return v * 0.25 + TrackRules.PlayerHalfDepth;
                    default: return v * 0.6 + TrackRules.PlayerHalfDepth + 1.0;
                }
            }
        }

        /// <summary>Returns true when the simulation should advance this frame.</summary>
        public bool Tick(float dt)
        {
            switch (Phase)
            {
                case TutorialPhase.Running:
                    if (_step < Steps.Length && _rowZ - _sim.Distance <= TriggerDistance)
                    {
                        Phase = TutorialPhase.WaitingForInput;
                        return false;
                    }
                    if (_step >= Steps.Length && _sim.Distance > _rowZ + 6)
                    {
                        Phase = TutorialPhase.Ready;
                        _readyLeft = ReadySeconds;
                    }
                    return true;
                case TutorialPhase.WaitingForInput:
                    return false;
                case TutorialPhase.Ready:
                    _readyLeft -= dt;
                    if (_readyLeft <= 0) Finish();
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>Player input while the tutorial runs. Wrong swipes are ignored while waiting.</summary>
        public void Input(SwipeDirection d)
        {
            if (Phase != TutorialPhase.WaitingForInput || d != Steps[_step].Required) return;
            _sim.Input(d);
            Phase = TutorialPhase.Running;
            _step++;
            if (_step < Steps.Length) PlaceStep(_rowZ + 26);
        }

        void Finish()
        {
            Phase = TutorialPhase.Done;
            _sim.GodMode = false;
            _sim.Track.ResumeAt(_sim.Distance + 30);
        }
    }
}
