using System;
using BuffetSim.Bootstrap;
using BuffetSim.Interaction;
using BuffetSim.Player;
using BuffetSim.UI;
using UnityEngine;
using UnityEngine.AI;

namespace BuffetSim.Events
{
    /// <summary>
    /// A generic body for event NPCs: the E key, a hold-E channel and thrown things all forward to
    /// callbacks the runner sets, so each event keeps its own rules without another MonoBehaviour.
    /// </summary>
    public sealed class EventActor : MonoBehaviour, IInteractable, IHoldInteractable, IThrowTarget
    {
        public Func<PlayerInventory, string> Prompt;
        public Action<PlayerInventory> OnInteract;
        public Func<PlayerInventory, float> HoldSeconds;
        public Action<PlayerInventory> OnHoldComplete;
        public Func<ThrowableKind, bool> Accepts;
        public Action<ThrowableKind> OnHit;
        public int Priority = 2;
        public float AimHeight = 1.3f;
        public bool Active = true;

        /// <summary>Adds a trigger capsule (so the interaction ray and thrown things find it) and the actor itself.</summary>
        public static EventActor Attach(GameObject root, float radius, float height)
        {
            CapsuleCollider trigger = root.AddComponent<CapsuleCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;
            trigger.height = height;
            trigger.center = new Vector3(0f, height * 0.5f, 0f);
            return root.AddComponent<EventActor>();
        }

        /// <summary>A capsule person on a NavMeshAgent with a label over their head: the cheapest body an event can wear.</summary>
        public static GameObject BuildPerson(string name, Transform parent, ChaosEventContext ctx, Vector3 start, float speed, Color bodyColor, Color headColor,
            string labelText, Color labelColor, out NavMeshAgent agent, out WanderingNpc npc, out TextMesh label)
        {
            GameObject root = ChaosActors.SpawnAgentRoot(name, parent, start, 0.35f, 1.8f, speed, ctx.Rng, out agent);
            agent.stoppingDistance = 0.4f;
            PrimitiveFactory.Visual("Body", PrimitiveType.Capsule, root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f), MaterialLibrary.Get(bodyColor));
            PrimitiveFactory.Visual("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.42f, MaterialLibrary.Get(headColor));
            label = PrimitiveFactory.Label("Label", root.transform, new Vector3(0f, 2.45f, 0f), labelText, 0.2f, ctx.Font, labelColor);
            label.gameObject.AddComponent<Billboard>();
            npc = root.AddComponent<WanderingNpc>();
            return root;
        }

        public string GetPrompt(PlayerInventory inventory) => Active && Prompt != null ? Prompt(inventory) : string.Empty;

        public void Interact(PlayerInventory inventory)
        {
            if (Active) OnInteract?.Invoke(inventory);
        }

        public float GetHoldSeconds(PlayerInventory inventory) => Active && HoldSeconds != null ? HoldSeconds(inventory) : 0f;

        public void CompleteHold(PlayerInventory inventory)
        {
            if (Active) OnHoldComplete?.Invoke(inventory);
        }

        public bool AcceptsThrow(ThrowableKind kind) => Active && Accepts != null && Accepts(kind);
        public int HomingPriority => Active ? Priority : 0;
        public Vector3 AimPoint => transform.position + Vector3.up * AimHeight;

        public void OnThrowHit(ThrowableKind kind)
        {
            if (Active) OnHit?.Invoke(kind);
        }
    }
}
