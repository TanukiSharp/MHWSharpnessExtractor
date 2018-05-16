using System;
using System.Linq;

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

        public static readonly ElementInfo None = new ElementInfo(ElementType.None, true, 0);

        public ElementInfo(ElementType type, bool isHidden, int value)
        {
            Type = type;
            IsHidden = isHidden;
            Value = value;
        }

        public override bool Equals(object obj)
        {
            if (obj is ElementInfo other)
            {
                return
                    other.Type == Type &&
                    other.IsHidden == IsHidden &&
                    other.Value == Value;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{(int)Type}-{(IsHidden ? "1" : "0")}-{Value}".GetHashCode();
        }

        public static bool operator ==(ElementInfo left, ElementInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ElementInfo left, ElementInfo right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            string value = $"{Type} {Value}";
            return IsHidden ? $"({value})" : value;
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
        public EldersealLevel Elderseal { get; }
        public ElementInfo[] Elements { get; }
        public int[] Slots { get; }

        public Weapon(
            string name,
            WeaponType type,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            EldersealLevel elderseal,
            ElementInfo[] elements,
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
            Elderseal = elderseal;
            Elements = elements;
            Slots = slots;
        }

        public Weapon UpdateId(int newId)
        {
            Id = newId;
            return this;
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

                if (other.Elements.Length != Elements.Length)
                    return false;

                for (int i = 0; i < Elements.Length; i++)
                {
                    if (other.Elements[i] != Elements[i])
                        return false;
                }

                return
                    other.Type == Type &&
                    other.Attack == Attack &&
                    other.Affinity == Affinity &&
                    other.Defense == Defense &&
                    other.Elderseal == Elderseal;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{Attack}-{(int)Elderseal}-{string.Join('-', Elements.Select(x => x.GetHashCode().ToString()))}".GetHashCode();
        }

        public static bool operator ==(Weapon left, Weapon right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Weapon left, Weapon right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public enum ChargeBladePhialType
    {
        None,
        Impact,
        Elemental
    }

    public class ChargeBlade : Weapon
    {
        public ChargeBladePhialType PhialType { get; }

        public ChargeBlade(
            string name,
            ChargeBladePhialType phialType,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  name,
                  WeaponType.ChargeBlade,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanks,
                  elderseal,
                  elements,
                  slots
            )
        {
            PhialType = phialType;
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) == false)
                return false;

            if (obj is ChargeBlade other)
                return other.PhialType == PhialType;

            return false;
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)PhialType}".GetHashCode();
        }
    }

    public enum Melody
    {
        None,
        White,
        Red,
        Blue,
        Purple,
        Green,
        Orange,
        Cyan,
        Yellow,
    }

    public class HuntingHorn : Weapon
    {
        public Melody[] Melodies { get; }

        public HuntingHorn(
            string name,
            Melody[] melodies,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  name,
                  WeaponType.HuntingHorn,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanks,
                  elderseal,
                  elements,
                  slots
            )
        {
            Melodies = melodies ?? new Melody[0];
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) == false)
                return false;

            if (obj is HuntingHorn other)
            {
                if (other.Melodies.Length != Melodies.Length)
                    return false;

                for (int i = 0; i < Melodies.Length; i++)
                {
                    if (other.Melodies[i] != Melodies[i])
                        return false;
                }

                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{string.Join(":", Melodies)}".GetHashCode();
        }
    }

    public enum SwitchAxePhialType
    {
        None,
        Power,
        PowerElement,
        Poison,
        Exhaust,
        Dragon,
        Paralysis
    }

    public class SwitchAxe : Weapon
    {
        public SwitchAxePhialType PhialType { get; }
        public int PhialValue { get; }

        public SwitchAxe(
            string name,
            SwitchAxePhialType phialType,
            int phialValue,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  name,
                  WeaponType.SwitchAxe,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanks,
                  elderseal,
                  elements,
                  slots
            )
        {
            PhialType = phialType;
            PhialValue = phialValue;
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) == false)
                return false;

            if (obj is SwitchAxe other)
                return other.PhialType == PhialType && other.PhialValue == PhialValue;

            return false;
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)PhialType}-{PhialValue}".GetHashCode();
        }
    }

    public enum GunlanceShellingType
    {
        None,
        Normal,
        Long,
        Wide
    }

    public class Gunlance : Weapon
    {
        public GunlanceShellingType ShellingType { get; }
        public int ShellingLevel { get; }

        public Gunlance(
            string name,
            GunlanceShellingType shellingType,
            int shellingLevel,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  name,
                  WeaponType.Gunlance,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanks,
                  elderseal,
                  elements,
                  slots
            )
        {
            ShellingType = shellingType;
            ShellingLevel = shellingLevel;
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) == false)
                return false;

            if (obj is Gunlance other)
                return other.ShellingType == ShellingType && other.ShellingLevel == ShellingLevel;

            return false;
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)ShellingType}-{ShellingLevel}".GetHashCode();
        }
    }

    public enum KinsectBonusType
    {
        None,
        Sever,
        Speed,
        Element,
        Health,
        Stamina,
        Blunt
    }

    public class InsectGlaive : Weapon
    {
        public KinsectBonusType KinsectBonus { get; }

        public InsectGlaive(
            string name,
            KinsectBonusType kinsectBonus,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanks,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  name,
                  WeaponType.InsectGlaive,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanks,
                  elderseal,
                  elements,
                  slots
            )
        {
            KinsectBonus = kinsectBonus;
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj) == false)
                return false;

            if (obj is InsectGlaive other)
                return other.KinsectBonus == KinsectBonus;

            return false;
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)KinsectBonus}".GetHashCode();
        }
    }
}
