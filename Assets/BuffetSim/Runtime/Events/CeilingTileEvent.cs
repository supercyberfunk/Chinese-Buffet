using BuffetSim.Bootstrap;
using BuffetSim.Core;
using BuffetSim.Customers;
using BuffetSim.Player;
using UnityEngine;

namespace BuffetSim.Events
{
    /// <summary>
    /// A ceiling tile lets go over a table and lands on the customer under it, who sits stunned
    /// with the tile on their head. Tape it back up with duct tape (hold E) and they shake it off
    /// and keep eating. Take too long and they leave without paying, their lawyer sends a letter,
    /// and the room thinks less of you.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Chaos Events/Ceiling Tile", fileName = "CeilingTile")]
    public sealed class CeilingTileEvent : ChaosEvent
    {
        [SerializeField] private float patienceSeconds = 60f;
        [SerializeField] private float holdSeconds = 6f;
        [SerializeField] private float reputationIfIgnored = -5f;

        public float PatienceSeconds => patienceSeconds;
        public float HoldSeconds => holdSeconds;
        public float ReputationIfIgnored => reputationIfIgnored;

        public static CeilingTileEvent CreateDefault()
        {
            var e = CreateInstance<CeilingTileEvent>();
            e.Configure("CeilingTile", "Ceiling tile", "A ceiling tile just landed on someone.", 1f, 1, 200f);
            return e;
        }

        public override ChaosEventRunner Begin(ChaosEventContext context, Transform parent)
        {
            return BeginWith<CeilingTileRunner>(context, parent);
        }
    }

    /// <summary>Runner for <see cref="CeilingTileEvent"/>: the drop, the daze, the tape or the lawyer.</summary>
    public sealed class CeilingTileRunner : ChaosEventRunner
    {
        private const float FallSeconds = 0.5f;
        private const float CeilingHeight = 3.1f;
        private static readonly Color TileColor = new Color(0.9f, 0.9f, 0.85f);

        private CeilingTileEvent _event;
        private CustomerAgent _customer;
        private Transform _tile;
        private Vector3 _tileStart;
        private Vector3 _tileRest;
        private float _fall;
        private bool _landed;
        private bool _fixing;
        private float _patience;
        private RepairPoint _repair;

        protected override void OnBegin()
        {
            _event = Definition as CeilingTileEvent;
            _customer = ChaosActors.PickSeatedCustomer(Ctx);
            if (_customer == null)
            {
                GameEvents.RaiseNotice("A ceiling tile fell on an empty table. Somebody should look at the ceiling. Not today.");
                Finish(true, "The tile hit an empty table");
                return;
            }

            Vector3 head = _customer.transform.position;
            _tileStart = new Vector3(head.x, CeilingHeight, head.z);
            _tileRest = head + Vector3.up * 2.05f;
            _tile = PrimitiveFactory.Visual("Ceiling Tile", PrimitiveType.Cube, transform, _tileStart, new Vector3(1.2f, 0.05f, 1.2f), MaterialLibrary.Get(TileColor)).transform;
            PrimitiveFactory.Visual("Stain", PrimitiveType.Cube, _tile, new Vector3(0.2f, 0.51f, -0.1f), new Vector3(0.4f, 0.02f, 0.3f), MaterialLibrary.Get(new Color(0.7f, 0.6f, 0.4f)));
            _patience = _event != null ? _event.PatienceSeconds : 60f;
            GameEvents.RaiseNotice($"The ceiling tile over {_customer.CustomerName} just let go. (audio cue: a dry thud, then silence)");
        }

        private void Update()
        {
            if (IsFinished) return;
            if (_customer == null || (_landed && !_customer.IsHeld))
            {
                // Gone, or something else (a rock, a knock-out) took them out of the hold: no tile, no lawyer.
                Finish(false, "The customer under the tile is gone");
                return;
            }

            float dt = Time.deltaTime;
            if (!_landed)
            {
                _fall += dt;
                float t = Mathf.Clamp01(_fall / FallSeconds);
                _tile.position = Vector3.Lerp(_tileStart, _tileRest, t * t);
                _tile.rotation = Quaternion.Euler(0f, 0f, 12f * t);
                if (t >= 1f) Land();
                return;
            }

            if (_fixing) return;
            _patience -= dt;
            if (_patience <= 0f) GiveUp();
        }

        private void Land()
        {
            _landed = true;
            _customer.Stagger(2.5f, "Stunned by a ceiling tile");
            _customer.Hold("Dazed, wearing a ceiling tile");
            _repair = RepairPoint.Create("Repair - Ceiling Tile", transform, _tileRest + Vector3.up * 0.2f, new Vector3(1.3f, 0.8f, 1.3f),
                CarryItems.DuctTape, "duct tape", _event != null ? _event.HoldSeconds : 6f, "Tape the ceiling tile back up", "the maintenance shelf in the kitchen", OnTaped);
            GameEvents.RaiseNotice($"{_customer.CustomerName} is sitting very still under a ceiling tile. Duct tape is on the maintenance shelf.");
        }

        private void OnTaped()
        {
            if (IsFinished || _customer == null) return;
            _fixing = true;
            _tile.SetParent(transform, true);
            StartCoroutine(RaiseTile());
        }

        private System.Collections.IEnumerator RaiseTile()
        {
            float t = 0f;
            Vector3 from = _tile.position;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.8f;
                _tile.position = Vector3.Lerp(from, _tileStart, t);
                _tile.rotation = Quaternion.Euler(0f, 0f, 12f * (1f - t));
                yield return null;
            }
            if (_customer != null) _customer.ReleaseHold();
            string name = _customer != null ? _customer.CustomerName : "The customer";
            GameEvents.RaiseNotice($"The tile is taped up. {name} shook the dust off and went back to the lo mein.");
            Finish(true, "Tile taped back up");
        }

        private void GiveUp()
        {
            if (IsFinished) return;
            string name = _customer.CustomerName;
            if (!_customer.LeaveWithoutPaying($"{name} stood up, let the tile slide off, and left without paying. Their lawyer is already on the phone."))
            {
                // Already paid, out cold or on the way out: no walkout, so no fine and no grudge.
                Finish(false, $"{name} was already gone");
                return;
            }
            float fine = Ctx.Config != null ? Ctx.Config.LawsuitFine : 50f;
            GameEvents.RaiseExpenseCharged(fine, $"Lawsuit: ceiling tile vs. {name}", Ctx.RegisterPoint + Vector3.up);
            GameEvents.RaiseReputationNudged(_event != null ? _event.ReputationIfIgnored : -5f, "the ceiling tile");
            Finish(false, $"{name} left unpaid; a ${fine:0.00} lawsuit");
        }

        public override void Abort()
        {
            if (_customer != null) _customer.ReleaseHold();
            EndSilently();
        }
    }
}
