﻿using System;
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
                int len = slots == null ? 0 : slots.Length;
                for (int i = 0; i < len; i++)
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
            return obj is SlotInfo other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return string.Join("|", Slots).GetHashCode();
        }

        public static bool operator ==(SlotInfo left, SlotInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SlotInfo left, SlotInfo right)
        {
            return !(left == right);
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

        public override bool Equals(object obj)
        {
            return obj is ElementInfo other && other.GetHashCode() == GetHashCode();
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
        public int Rarity { get; }
        public int Attack { get; }
        public int Affinity { get; }
        public int Defense { get; }
        public int[] SharpnessRanksLevel1 { get; private set; }
        public int[] SharpnessRanksLevel5 { get; private set; }
        public EldersealLevel Elderseal { get; }
        public ElementInfo[] Elements { get; }
        public SlotInfo Slots { get; }

        public Weapon(
            IDataSource dataSource,
            string name,
            WeaponType type,
            int rarity,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanksLevel1,
            int[] sharpnessRanksLevel5,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots
        )
        {
            DataSource = dataSource;
            Id = -1;
            Name = name;
            Type = type;
            Rarity = rarity;
            Attack = attack;
            Affinity = affinity;
            Defense = defense;
            SharpnessRanksLevel1 = NormalizeSharpness(sharpnessRanksLevel1);
            SharpnessRanksLevel5 = NormalizeSharpness(sharpnessRanksLevel5);
            Elderseal = elderseal;
            Elements = elements.OrderBy(x => x.Type).ToArray();
            Slots = new SlotInfo(slots);
        }

        private int[] NormalizeSharpness(int[] sharpness)
        {
            if (sharpness == null)
                return new int[0];

            return sharpness.Where(x => x > 0).ToArray();
        }

        public Weapon UpdateId(int newId)
        {
            Id = newId;
            return this;
        }

        public Weapon UpdateSharpnessLevel5(int[] sharpnessRanks)
        {
            SharpnessRanksLevel5 = sharpnessRanks;
            return this;
        }

        public virtual int ComputeMatchingScore(Weapon other)
        {
            int score = 0;

            if ((object)other == null)
                return 0;

            int len = Math.Min(other.Slots.Length, Slots.Length);
            for (int i = 0; i < len; i++)
            {
                if (other.Slots[i] == Slots[i])
                    score++;
            }

            len = Math.Min(other.SharpnessRanksLevel1.Length, SharpnessRanksLevel1.Length);
            for (int i = 0; i < len; i++)
            {
                if (Math.Abs(SharpnessRanksLevel1[i] - other.SharpnessRanksLevel1[i]) <= 2)
                    score++;
            }

            if (other.Elements.Length == Elements.Length)
            {
                score++;

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

            if (other.Rarity == Rarity)
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

        public override bool Equals(object obj)
        {
            return obj is Weapon other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return $"{(int)Type}-{Rarity}-{Attack}-{Affinity}-{Defense}-{(int)Elderseal}-{Slots.GetHashCode()}-{string.Join('|', Elements.Select(x => x.GetHashCode()))}".GetHashCode();
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

        public static readonly string[] SharpnessColors = new string[] { "red", "orange", "yellow", "green", "blue", "white" };

        public object ToJsonObject()
        {
            var ranks = new Dictionary<string, int>();

            for (int i = 0; i < SharpnessRanksLevel5.Length; i++)
                ranks[SharpnessColors[i]] = SharpnessRanksLevel5[i];

            return new
            {
                id = Id,
                sharpness = ranks
            };
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
            int rarity,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanksLevel1,
            int[] sharpnessRanksLevel5,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  dataSource,
                  name,
                  WeaponType.ChargeBlade,
                  rarity,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanksLevel1,
                  sharpnessRanksLevel5,
                  elderseal,
                  elements,
                  slots
            )
        {
            PhialType = phialType;
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

        public override bool Equals(object obj)
        {
            return obj is ChargeBlade other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)PhialType}".GetHashCode();
        }

        public static bool operator ==(ChargeBlade left, ChargeBlade right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChargeBlade left, ChargeBlade right)
        {
            return !(left == right);
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
            int rarity,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanksLevel1,
            int[] sharpnessRanksLevel5,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  dataSource,
                  name,
                  WeaponType.HuntingHorn,
                  rarity,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanksLevel1,
                  sharpnessRanksLevel5,
                  elderseal,
                  elements,
                  slots
            )
        {
            Melodies = melodies ?? new Melody[0];
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

        public override bool Equals(object obj)
        {
            return obj is HuntingHorn other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            //return $"{base.GetHashCode()}-{string.Join(":", Melodies)}".GetHashCode();
            return base.GetHashCode();
        }

        public static bool operator ==(HuntingHorn left, HuntingHorn right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HuntingHorn left, HuntingHorn right)
        {
            return !(left == right);
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
            int rarity,
            int phialValue,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanksLevel1,
            int[] sharpnessRanksLevel5,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  dataSource,
                  name,
                  WeaponType.SwitchAxe,
                  rarity,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanksLevel1,
                  sharpnessRanksLevel5,
                  elderseal,
                  elements,
                  slots
            )
        {
            PhialType = phialType;
            PhialValue = phialValue;
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

        public override bool Equals(object obj)
        {
            return obj is SwitchAxe other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)PhialType}-{PhialValue}".GetHashCode();
        }

        public static bool operator ==(SwitchAxe left, SwitchAxe right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SwitchAxe left, SwitchAxe right)
        {
            return !(left == right);
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
            int rarity,
            int shellingLevel,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanksLevel1,
            int[] sharpnessRanksLevel5,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  dataSource,
                  name,
                  WeaponType.Gunlance,
                  rarity,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanksLevel1,
                  sharpnessRanksLevel5,
                  elderseal,
                  elements,
                  slots
            )
        {
            ShellingType = shellingType;
            ShellingLevel = shellingLevel;
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

        public override bool Equals(object obj)
        {
            return obj is Gunlance other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)ShellingType}-{ShellingLevel}".GetHashCode();
        }

        public static bool operator ==(Gunlance left, Gunlance right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Gunlance left, Gunlance right)
        {
            return !(left == right);
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
            int rarity,
            int attack,
            int affinity,
            int defense,
            int[] sharpnessRanksLevel1,
            int[] sharpnessRanksLevel5,
            EldersealLevel elderseal,
            ElementInfo[] elements,
            int[] slots)
            : base(
                  dataSource,
                  name,
                  WeaponType.InsectGlaive,
                  rarity,
                  attack,
                  affinity,
                  defense,
                  sharpnessRanksLevel1,
                  sharpnessRanksLevel5,
                  elderseal,
                  elements,
                  slots
            )
        {
            KinsectBonus = kinsectBonus;
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

        public override bool Equals(object obj)
        {
            return obj is InsectGlaive other && other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            return $"{base.GetHashCode()}-{(int)KinsectBonus}".GetHashCode();
        }

        public static bool operator ==(InsectGlaive left, InsectGlaive right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InsectGlaive left, InsectGlaive right)
        {
            return !(left == right);
        }
    }
}
