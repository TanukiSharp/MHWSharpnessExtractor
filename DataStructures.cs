using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

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

    public struct SlotInfo
    {
        public int[] Slots { get; }

        public SlotInfo(int[] slots)
        {
            if (slots == null || slots.Length < 3)
            {
                int[] temp = new int[3];
                for (int i = 0; i < slots.Length; i++)
                    temp[i] = slots[i];
                slots = temp;
            }

            Slots = slots;
        }

        public int this[int index]
        {
            get
            {
                return Slots[index];
            }
        }

        public int Length => Slots.Length;

        public override string ToString()
        {

            return string.Join("", Slots.Select(x => x > 0 ? x.ToString() : "-"));
        }

        public override bool Equals(object obj)
        {
            if (obj is SlotInfo other)
                return other.GetHashCode() == GetHashCode();

            return false;
        }

        public override int GetHashCode()
        {
            return string.Join("|", Slots).GetHashCode();
        }
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

        //public void PrintMismatches(ElementInfo other, TextWriter sb)
        //{
        //    if (other.Type != Type)
        //        sb.Write($"type is different (should be {other.Type} but is {Type}), ");
        //    if (other.IsHidden != IsHidden)
        //        sb.Write($"hidden is different (should be {other.IsHidden} but is {IsHidden}), ");
        //    if (other.Value != Value)
        //        sb.Write($"value is different (should be {other.Value} but is {Value}), ");
        //    sb.WriteLine();
        //}

        public override bool Equals(object obj)
        {
            if (obj is ElementInfo other)
            {
                return other.GetHashCode() == GetHashCode();

                //return
                //    other.Type == Type &&
                //    other.IsHidden == IsHidden &&
                //    other.Value == Value;
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
        public IDataSource DataSource { get; }
        public int Id { get; private set; }
        public string Name { get; private set; }
        public WeaponType Type { get; }
        public int Attack { get; }
        public int Affinity { get; }
        public int Defense { get; }
        public int[] SharpnessRanks { get; private set; }
        public EldersealLevel Elderseal { get; }
        public ElementInfo[] Elements { get; }
        public SlotInfo Slots { get; }

        public Weapon(
            IDataSource dataSource,
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
            DataSource = dataSource;
            Id = -1;
            Name = name;
            Type = type;
            Attack = attack;
            Affinity = affinity;
            Defense = defense;
            SharpnessRanks = sharpnessRanks;
            Elderseal = elderseal;
            Elements = elements.OrderBy(x => x.Type).ToArray();
            Slots = new SlotInfo(slots);
        }

        public Weapon UpdateId(int newId)
        {
            Id = newId;
            return this;
        }

        public Weapon UpdateSharpness(int[] sharpnessRanks)
        {
            SharpnessRanks = sharpnessRanks;
            return this;
        }

        public override bool Equals(object obj)
        {
            // Id and Name are not taken into account on purpose,
            // matching against all other domain and language-independent parameters to find the matching weapon in DB.

            // Sharpness is not taken into account on purpose,
            // this is the value known to be different from the DB.

            if (obj is Weapon other)
                return other.GetHashCode() == GetHashCode();

            return false;
        }

        public virtual int ComputeMatchingScore(Weapon other)
        {
            int score = 0;

            int len = Math.Max(other.Slots.Length, Slots.Length);
            for (int i = 0; i < len; i++)
            {
                if (other.Slots[i] == Slots[i])
                    score++;
            }

            if (other.Elements.Length == Elements.Length)
            {
                for (int i = 0; i < Elements.Length; i++)
                {
                    if (other.Elements[i].Type == Elements[i].Type)
                        score++;
                    if (other.Elements[i].IsHidden == Elements[i].IsHidden)
                        score++;
                    if (other.Elements[i].Value == Elements[i].Value)
                        score++;
                }
            }

            if (other.Type == Type)
                score++;

            if (other.Attack == Attack)
                score++;

            if (other.Affinity == Affinity)
                score++;

            if (other.Defense == Defense)
                score++;

            if (other.Elderseal == Elderseal)
                score++;

            return score;
        }

        public virtual void PrintMismatches(Weapon other, TextWriter sb)
        {
            if (other.Slots.Equals(Slots) == false)
                sb.WriteLine($"- slot is different (should be '{other.Slots}' but is '{Slots}')");

            if (other.Elements.Length != Elements.Length)
                sb.WriteLine($"- element count is different (should be {other.Elements.Length} but is {Elements.Length})");
            else
            {
                for (int i = 0; i < Elements.Length; i++)
                {
                    if (other.Elements[i] != Elements[i])
                        sb.WriteLine($"- element {i + 1} is different (should be '{other.Elements[i]}' but is '{Elements[i]}')");
                }
            }

            if (other.Type != Type)
                sb.WriteLine($"- weapon type is different (should be {other.Type} but is {Type})");

            if (other.Attack != Attack)
                sb.WriteLine($"- attack is different (should be {other.Attack} but is {Attack})");

            if (other.Affinity != Affinity)
                sb.WriteLine($"- affinity is different (should be {other.Affinity} but is {Affinity})");

            if (other.Defense != Defense)
                sb.WriteLine($"- defense is different (should be {other.Defense} but is {Defense})");

            if (other.Elderseal != Elderseal)
                sb.WriteLine($"- elderseal is different (should be {other.Elderseal} but is {Elderseal})");
        }

        public override int GetHashCode()
        {
            return $"{(int)Type}-{Attack}-{Affinity}-{Defense}-{(int)Elderseal}-{Slots.GetHashCode()}-{string.Join('|', Elements.Select(x => x.GetHashCode()))}".GetHashCode();
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

        private static readonly string[] SharpnessColors = new string[] { "red", "orange", "yellow", "green", "blue", "white" };

        public string ToJson(bool pretify)
        {
            var ranks = new Dictionary<string, int>();

            for (int i = 0; i < SharpnessRanks.Length; i++)
                ranks[SharpnessColors[i]] = SharpnessRanks[i];

            return JsonConvert.SerializeObject(new
            {
                id = Id,
                sharpness = ranks
            }, pretify ? Formatting.Indented : Formatting.None);                
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
            IDataSource dataSource,
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
                  dataSource,
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

        public override void PrintMismatches(Weapon other, TextWriter sb)
        {
            base.PrintMismatches(other, sb);

            if (other is ChargeBlade x)
            {
                if (x.PhialType != PhialType)
                    sb.WriteLine($"- phial type is different (should be {x.PhialType} but is {Type})");
            }
        }

        public override int ComputeMatchingScore(Weapon other)
        {
            int score = base.ComputeMatchingScore(other);

            if (other is ChargeBlade x)
            {
                if (x.PhialType == PhialType)
                    score++;
            }

            return score;
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
            IDataSource dataSource,
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
                  dataSource,
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

        public override void PrintMismatches(Weapon other, TextWriter sb)
        {
            base.PrintMismatches(other, sb);

            if (other is HuntingHorn x)
            {
            }
        }

        public override int ComputeMatchingScore(Weapon other)
        {
            int score = base.ComputeMatchingScore(other);

            if (other is HuntingHorn x)
            {
                //if (x.Melodies.Length == Melodies.Length)
                //    score++;

                //for (int i = 0; i < Melodies.Length; i++)
                //{
                //    if (x.Melodies[i] == Melodies[i])
                //        score++;
                //}
            }

            return score;
        }

        public override int GetHashCode()
        {
            //return $"{base.GetHashCode()}-{string.Join(":", Melodies)}".GetHashCode();
            return base.GetHashCode();
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
            IDataSource dataSource,
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
                  dataSource,
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

        public override void PrintMismatches(Weapon other, TextWriter sb)
        {
            base.PrintMismatches(other, sb);

            if (other is SwitchAxe x)
            {
                if (x.PhialType != PhialType)
                    sb.WriteLine($"- phial type is different (should be {x.PhialType} but is {Type})");

                if (x.PhialValue != PhialValue)
                    sb.WriteLine($"- phial value is different (should be {x.PhialValue} but is {PhialValue})");
            }
        }

        public override int ComputeMatchingScore(Weapon other)
        {
            int score = base.ComputeMatchingScore(other);

            if (other is SwitchAxe x)
            {
                if (x.PhialType == PhialType)
                    score++;

                if (x.PhialValue == PhialValue)
                    score++;
            }

            return score;
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
            IDataSource dataSource,
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
                  dataSource,
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

        public override void PrintMismatches(Weapon other, TextWriter sb)
        {
            base.PrintMismatches(other, sb);

            if (other is Gunlance x)
            {
                if (x.ShellingType != ShellingType)
                    sb.WriteLine($"- shelling type is different (should be {x.ShellingType} but is {ShellingType})");

                if (x.ShellingLevel != ShellingLevel)
                    sb.WriteLine($"- shelling level is different (should be {x.ShellingLevel} but is {ShellingLevel})");
            }
        }

        public override int ComputeMatchingScore(Weapon other)
        {
            int score = base.ComputeMatchingScore(other);

            if (other is Gunlance x)
            {
                if (x.ShellingType == ShellingType)
                    score++;

                if (x.ShellingLevel == ShellingLevel)
                    score++;
            }

            return score;
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
            IDataSource dataSource,
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
                  dataSource,
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

        public override void PrintMismatches(Weapon other, TextWriter sb)
        {
            base.PrintMismatches(other, sb);

            if (other is InsectGlaive x)
            {
                if (x.KinsectBonus != KinsectBonus)
                    sb.WriteLine($"- kinsect bonus is different (should be {x.KinsectBonus} but is {KinsectBonus})");
            }
        }

        public override int ComputeMatchingScore(Weapon other)
        {
            int score = base.ComputeMatchingScore(other);

            if (other is InsectGlaive x)
            {
                if (x.KinsectBonus == KinsectBonus)
                    score++;
            }

            return score;
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)KinsectBonus}".GetHashCode();
        }
    }
}
