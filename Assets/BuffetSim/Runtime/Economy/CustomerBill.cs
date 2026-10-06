using UnityEngine;

namespace BuffetSim.Economy
{
    /// <summary>
    /// Plain C# running tally for one customer's visit. Created the moment the customer spawns
    /// (base value applied immediately, per the design notes) and reduced as units go unfulfilled.
    /// </summary>
    public sealed class CustomerBill
    {
        private readonly float _deductionPerUnit;

        public float BaseAmount { get; private set; }
        public int UnitsWanted { get; }
        public int UnitsTaken { get; private set; }
        public int UnitsUnfulfilled { get; private set; }
        public float Deductions { get; private set; }

        public CustomerBill(float baseAmount, int unitsWanted, float deductionPerUnit)
        {
            BaseAmount = baseAmount;
            UnitsWanted = unitsWanted;
            _deductionPerUnit = deductionPerUnit;
        }

        /// <summary>Scales the base value (a "Business is booming" suit pays 1.5x). Deductions are untouched.</summary>
        public void ApplyMultiplier(float multiplier)
        {
            if (multiplier > 0f) BaseAmount *= multiplier;
        }

        public void RecordServed(int taken, int unfulfilled)
        {
            UnitsTaken += Mathf.Max(0, taken);
            UnitsUnfulfilled += Mathf.Max(0, unfulfilled);
            Deductions += Mathf.Max(0, unfulfilled) * _deductionPerUnit;
        }

        /// <summary>
        /// Per the notes: if not a single unit could be satisfied the customer leaves without
        /// paying (no change in profit); otherwise the base bill minus deductions, never below zero.
        /// </summary>
        public float Total => UnitsTaken <= 0 ? 0f : Mathf.Max(0f, BaseAmount - Deductions);
    }
}
