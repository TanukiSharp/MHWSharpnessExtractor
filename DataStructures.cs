using System;

namespace MHWSharpnessExtractor
{
    public enum EldersealLevel
    {
        None,
        Low,
        Average,
        High
    }

    public enum ElementType
    {
        None,
        Fire,
        Water,
        Thunder,
        Ice,
        Dragon,
        Poison,
        Sleep,
        Paralysis,
        Blast,
        Stun
    }

    public enum WeaponType
    {
        GreatSword,
        LongSword,
        SwordAndShield,
        DualBlades,
        Hammer,
        HuntingHorn,
        Lance,
        Gunlance,
        SwitchAxe,
        ChargeBlade,
        InsectGlaive
    }

    public struct ElementInfo
    {
        public ElementType Type { get; }
        public bool IsHidden { get; }
        public int Value { get; }
        public EldersealLevel Elderseal { get; }

        public static readonly ElementInfo None = new ElementInfo(ElementType.None, true, 0, EldersealLevel.None);

        public ElementInfo(ElementType type, bool isHidden, int value, EldersealLevel elderseal)
        {
            Type = type;
            IsHidden = isHidden;
            Value = value;
            Elderseal = elderseal;
        }

        public override bool Equals(object obj)
        {
            if (obj is ElementInfo other)
            {
                return
                    other.Type == Type &&
                    other.IsHidden == IsHidden &&
                    other.Value == Value &&
                    other.Elderseal == Elderseal;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{(int)Type}-{(IsHidden ? "1" : "0")}-{Value}-{(int)Elderseal}".GetHashCode();
        }

        public static bool operator ==(ElementInfo left, ElementInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ElementInfo left, ElementInfo right)
        {
            return !(left == right);
        }
    }

    public class Weapon
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public WeaponType Type { get; }
        public int Attack { get; }
        public int Affinity { get; }
        public int Defense { get; }
        public int[] SharpnessRanks { get; }
        public ElementInfo Element { get; }
        public int[] Slots { get; }

        public Weapon(
            string name,
            WeaponType type,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            ElementInfo element,
            int[] slots
        )
        {
            Id = -1;
            Name = name;
            Type = type;
            Attack = attack;
            Affinity = affinity;
            Defense = defense;
            SharpnessRanks = sharpnessRanks;
            Element = element;
            Slots = slots;
        }

        public void UpdateId(int newId)
        {
            Id = newId;
        }

        public override bool Equals(object obj)
        {
            // Id and Name are not taken into account on purpose,
            // matching against all other domain and language-independent parameters to find the matching weapon in DB.

            // Sharpness is not taken into account on purpose,
            // this is the value known to be different from the DB.

            if (obj is Weapon other)
            {
                if (other.Slots.Length != Slots.Length)
                    return false;

                for (int i = 0; i < Slots.Length; i++)
                {
                    if (other.Slots[i] != Slots[i])
                        return false;
                }

                return
                    other.Type == Type &&
                    other.Attack == Attack &&
                    other.Affinity == Affinity &&
                    other.Defense == Defense &&
                    other.Element == Element;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{Attack}-{Element.GetHashCode()}".GetHashCode();
        }

        public static bool operator ==(Weapon left, Weapon right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Weapon left, Weapon right)
        {
            return !(left == right);
        }
    }
}
