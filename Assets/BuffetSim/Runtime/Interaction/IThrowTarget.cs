using UnityEngine;

namespace BuffetSim.Interaction
{
    /// <summary>What the player can throw out of the apron pocket.</summary>
    public enum ThrowableKind
    {
        /// <summary>Homes on the most deserving target within range and knocks it out.</summary>
        Rock,
        /// <summary>Flies straight; a short stun on anything that takes it, then it can be picked up again.</summary>
        Dodgeball,
        /// <summary>A fortune cookie. Whatever it hits, the fortune comes back to the thrower.</summary>
        Cookie,
    }

    /// <summary>
    /// Anything a thrown object can hit. Homing throws pick the highest <see cref="HomingPriority"/>
    /// in range (zero means "never home on me"); straight throws hit whatever accepts them first.
    /// </summary>
    public interface IThrowTarget
    {
        bool AcceptsThrow(ThrowableKind kind);

        int HomingPriority { get; }

        /// <summary>Where a homing throw aims (usually the chest).</summary>
        Vector3 AimPoint { get; }

        void OnThrowHit(ThrowableKind kind);
    }
}
