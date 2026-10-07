using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// The live half of a chaos event: spawns its actors (as children of this GameObject), ticks and
    /// resolves. It announces itself on the bus once when it begins and once when it ends (resolved
    /// or not), then removes itself. Subclasses implement <see cref="OnBegin"/> and <see cref="Abort"/>
    /// and call <see cref="Finish"/> when the event is over.
    /// </summary>
    public abstract class ChaosEventRunner : MonoBehaviour
    {
        private const float DestroyDelaySeconds = 0.5f;

        private bool _begun;
        private bool _finished;

        public ChaosEventInfo Info { get; private set; }
        public bool IsFinished => _finished;
        /// <summary>
        /// True once the event's actors are done and only a mess the player has to deal with is left
        /// (puddles to mop, a pane to fit, a hole to tape). The scheduler then stops treating it as
        /// the running event so other events can roll; it still finishes on its own and is still
        /// aborted at close. Set it from the runner, never clear it.
        /// </summary>
        public bool WaitingOnPlayer { get; protected set; }
        /// <summary>The definition this runner was started from; cast it in <see cref="OnBegin"/> for tuning values.</summary>
        public ChaosEvent Definition { get; private set; }
        protected ChaosEventContext Ctx { get; private set; }

        /// <summary>Called once by <see cref="ChaosEvent.Begin"/>: publishes ChaosEventStarted, then runs <see cref="OnBegin"/>.</summary>
        public void Begin(ChaosEvent definition, ChaosEventContext ctx)
        {
            if (_begun) return;
            _begun = true;
            Definition = definition;
            Ctx = ctx ?? new ChaosEventContext();
            Info = definition != null
                ? definition.Info
                : new ChaosEventInfo { Id = name, DisplayName = name, Description = string.Empty };
            GameEvents.RaiseChaosEventStarted(Info);
            OnBegin();
        }

        /// <summary>Ends the event exactly once: publishes ChaosEventEnded and removes the runner (with its child actors) shortly after.</summary>
        protected void Finish(bool resolved, string outcome)
        {
            if (_finished) return;
            _finished = true;
            GameEvents.RaiseChaosEventEnded(Info, resolved, outcome);
            Destroy(gameObject, DestroyDelaySeconds);
        }

        /// <summary>Ends the event with no outcome on the bus (day end): the runner and everything under it go away now.</summary>
        protected void EndSilently()
        {
            if (_finished) return;
            _finished = true;
            Destroy(gameObject);
        }

        /// <summary>Day end: clean up every actor and end without publishing an outcome (call <see cref="EndSilently"/>).</summary>
        public abstract void Abort();

        protected abstract void OnBegin();
    }
}
