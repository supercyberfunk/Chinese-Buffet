using System;
using System.Collections.Generic;
using BuffetSim.Core;
using UnityEngine;

namespace BuffetSim.Gambling
{
    /// <summary>One slip of paper: the text, the kind, and the effect id the <see cref="FortuneTeller"/> knows how to apply.</summary>
    [Serializable]
    public sealed class FortuneEntry
    {
        [SerializeField] private int id;
        [SerializeField] private string text;
        [SerializeField] private FortuneKind kind;
        [Tooltip("What the teller does with it: none, sprint-off, drop, small-hands, maxed-orders, follower, comp-next, slow-cookers, slip-every, lose-cash, fast-feet, big-hands, old-debt, boost-next, dishwasher, tray-refill, dashers-trip, forgive, or event:<ChaosEventId>.")]
        [SerializeField] private string effectId = "none";
        [SerializeField] private float value;
        [SerializeField] private float seconds;
        [SerializeField] private int count;
        [SerializeField] private string summary;

        public int Id => id;
        public string Text => text;
        public FortuneKind Kind => kind;
        public string EffectId => effectId;
        public float Value => value;
        public float Seconds => seconds;
        public int Count => count;
        public string Summary => summary ?? string.Empty;

        public static FortuneEntry Create(int id, string text, FortuneKind kind, string effectId = "none", float value = 0f, float seconds = 0f, int count = 0, string summary = "")
        {
            return new FortuneEntry { id = id, text = text, kind = kind, effectId = effectId, value = value, seconds = seconds, count = count, summary = summary };
        }
    }

    /// <summary>
    /// The forty fortunes: sixteen that do nothing, nine that hurt, nine that help, six that start an
    /// event. Data only; the teller applies them and the wall collects them.
    /// </summary>
    [CreateAssetMenu(menuName = "Buffet Sim/Fortune Catalog", fileName = "FortuneCatalog")]
    public sealed class FortuneCatalog : ScriptableObject
    {
        [SerializeField] private List<FortuneEntry> fortunes = new List<FortuneEntry>();

        public IReadOnlyList<FortuneEntry> Fortunes => fortunes;
        public int Count => fortunes.Count;

        public FortuneEntry Find(int id)
        {
            for (int i = 0; i < fortunes.Count; i++)
                if (fortunes[i].Id == id) return fortunes[i];
            return null;
        }

        public static FortuneCatalog CreateDefault()
        {
            var catalog = CreateInstance<FortuneCatalog>();
            catalog.name = "FortuneCatalog (runtime default)";
            var f = catalog.fortunes;
            int id = 1;

            // General: sixteen that do nothing.
            string[] general =
            {
                "You will eat today.",
                "A tall person will be near you.",
                "The answer you seek is in a different cookie.",
                "Lucky numbers: 4, 4, 4, 4, 4, 4.",
                "Someone has already read this.",
                "Ignore all previous fortunes.",
                "A refund is not possible.",
                "This fortune intentionally left blank.",
                "The dishwasher knows what you did.",
                "Tuesday.",
                "You will be remembered, briefly, by a man named Doug.",
                "Good news is coming by a slower method.",
                "Learn Chinese: hello is 买单 (mǎi dān).",
                "In bed.",
                "You have been assigned a table.",
                "Everything will be fine for about an hour.",
            };
            foreach (string text in general) f.Add(FortuneEntry.Create(id++, text, FortuneKind.General));

            // Detrimental: nine.
            f.Add(FortuneEntry.Create(id++, "You will go far.", FortuneKind.Detrimental, "sprint-off", 0f, 60f, 0, "No sprinting for 60 s."));
            f.Add(FortuneEntry.Create(id++, "A great weight will be lifted from you.", FortuneKind.Detrimental, "drop", 0f, 0f, 0, "Everything in your hands hits the floor."));
            f.Add(FortuneEntry.Create(id++, "Your hands are your fortune.", FortuneKind.Detrimental, "small-hands", 0f, 120f, 0, "2 plates or 10 units at a time for 2 minutes."));
            f.Add(FortuneEntry.Create(id++, "Many mouths will sing your praise.", FortuneKind.Detrimental, "maxed-orders", 0f, 0f, 5, "The next 5 customers want the maximum."));
            f.Add(FortuneEntry.Create(id++, "Someone close to you is thinking of you.", FortuneKind.Detrimental, "follower", 0f, 60f, 0, "The nearest seated customer follows you around for 60 s instead of eating."));
            f.Add(FortuneEntry.Create(id++, "Generosity is its own reward.", FortuneKind.Detrimental, "comp-next", 0f, 0f, 1, "The next bill is comped to $0."));
            f.Add(FortuneEntry.Create(id++, "Slow down and enjoy the little things.", FortuneKind.Detrimental, "slow-cookers", 0.5f, 180f, 0, "Every cooker runs at half speed for 3 minutes."));
            f.Add(FortuneEntry.Create(id++, "Watch your step.", FortuneKind.Detrimental, "slip-every", 15f, 90f, 0, "You slip on dry floor every 15 s for 90 s."));
            f.Add(FortuneEntry.Create(id++, "A small expense will surprise you.", FortuneKind.Detrimental, "lose-cash", 5f, 0f, 0, "$5 in quarters falls out of your pocket."));

            // Positive: nine.
            f.Add(FortuneEntry.Create(id++, "Your feet will carry you far.", FortuneKind.Positive, "fast-feet", 1.5f, 90f, 0, "Sprint 1.5x and no slipping for 90 s."));
            f.Add(FortuneEntry.Create(id++, "Good things come to those who grab.", FortuneKind.Positive, "big-hands", 8f, 120f, 0, "8 plates at a time for 2 minutes."));
            f.Add(FortuneEntry.Create(id++, "An old debt will be repaid.", FortuneKind.Positive, "old-debt", 20f, 0f, 0, "$20 in quarters at your feet."));
            f.Add(FortuneEntry.Create(id++, "Everyone around you is hungry.", FortuneKind.Positive, "boost-next", 1.25f, 0f, 5, "The next 5 bills are +25%."));
            f.Add(FortuneEntry.Create(id++, "You will not be alone.", FortuneKind.Positive, "event:Cousin", 0f, 180f, 0, "A cousin in a visor is coming to clear tables."));
            f.Add(FortuneEntry.Create(id++, "Cleanliness is a state of mind.", FortuneKind.Positive, "dishwasher", 0f, 0f, 0, "The dishwasher finishes now."));
            f.Add(FortuneEntry.Create(id++, "The pot is deeper than you think.", FortuneKind.Positive, "tray-refill", 0f, 0f, 0, "The nearest buffet tray refills to 20."));
            f.Add(FortuneEntry.Create(id++, "What runs away comes back.", FortuneKind.Positive, "dashers-trip", 0f, 180f, 0, "Dine and dashers trip at the door for 3 minutes."));
            f.Add(FortuneEntry.Create(id++, "Your mistakes are forgiven.", FortuneKind.Positive, "forgive", 0f, 120f, 0, "Broken dishes and missing units cost nothing for 2 minutes."));

            // Events: six.
            f.Add(FortuneEntry.Create(id++, "Help. I am trapped in a fortune cookie factory.", FortuneKind.Event, "event:CookieFactory", 0f, 0f, 0, "The machine jams open."));
            f.Add(FortuneEntry.Create(id++, "Lucky number 8.", FortuneKind.Event, "event:LuckyEight", 0f, 0f, 0, "8-8-8."));
            f.Add(FortuneEntry.Create(id++, "A relative will visit soon.", FortuneKind.Event, "event:Grandma", 0f, 0f, 0, "Someone's grandmother is behind the line."));
            f.Add(FortuneEntry.Create(id++, "A stranger will take your place.", FortuneKind.Event, "event:Stranger", 0f, 0f, 0, "Someone is at your register."));
            f.Add(FortuneEntry.Create(id++, "A dragon will cross your path.", FortuneKind.Event, "event:ParadeDragon", 0f, 0f, 0, "The parade dragon is at the door."));
            f.Add(FortuneEntry.Create(id++, "Do not eat this one.", FortuneKind.Event, "event:MysteryMeat", 0f, 0f, 0, "It bit you."));
            return catalog;
        }
    }
}
