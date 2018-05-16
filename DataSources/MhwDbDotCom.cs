using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MHWSharpnessExtractor.DataSources
{
    public class MhwDbDotCom : IDataSource
    {
        public string Name => "mhw-db.com";

        public async Task<IList<Weapon>> ProduceWeaponsAsync()
        {
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3.0);

            var result = new List<Weapon>();

            var tasks = new Task<IList<Weapon>>[]
            {
                CreateWeapons(httpClient, "great-sword"),
                CreateWeapons(httpClient, "long-sword"),
                CreateWeapons(httpClient, "sword-and-shield"),
                CreateWeapons(httpClient, "dual-blades"),
                CreateWeapons(httpClient, "hammer"),
                CreateWeapons(httpClient, "hunting-horn"),
                CreateWeapons(httpClient, "lance"),
                CreateWeapons(httpClient, "gunlance"),
                CreateWeapons(httpClient, "switch-axe"),
                CreateWeapons(httpClient, "charge-blade"),
                CreateWeapons(httpClient, "insect-glaive"),
            };

            await Task.WhenAll(tasks);

            foreach (Task<IList<Weapon>> task in tasks)
                result.AddRange(task.Result);

            return result;
        }

        private async Task<IList<Weapon>> CreateWeapons(HttpClient httpClient, string weaponType)
        {
            JArray inputWeapons = await Download(httpClient, weaponType);

            WeaponType typedWeaponType = ConvertWeaponType(weaponType);

            var outputWeapons = new List<Weapon>();

            for (int i = 0; i < inputWeapons.Count; i++)
                outputWeapons.Add(CreateWeapon((JObject)inputWeapons[i], typedWeaponType));

            return outputWeapons;
        }

        private Weapon CreateWeapon(JObject weapon, WeaponType type)
        {
            int attack;
            int affinity;
            int defense;
            EldersealLevel eldersealLevel;

            string name = (string)weapon["name"];
            JObject attributes = (JObject)weapon["attributes"];

            ExtractAttributes(attributes, out attack, out affinity, out defense, out eldersealLevel);

            ElementInfo[] elementInfo = ExtractElementInfo((JArray)weapon["elements"]);
            int[] slots = ExtractSlots((JArray)weapon["slots"]);

            Weapon outputWeapon;

            if (type == WeaponType.ChargeBlade)
            {
                ChargeBladePhialType phialType = ExtractChargeBladePhialType(attributes);
                outputWeapon = new ChargeBlade(name, phialType, attack, affinity, defense, null, eldersealLevel, elementInfo, slots);
            }
            else if (type == WeaponType.HuntingHorn)
            {
                // mhw-db.com is missing melodies info
                outputWeapon = new HuntingHorn(name, new Melody[0], attack, affinity, defense, null, eldersealLevel, elementInfo, slots);
            }
            else if (type == WeaponType.SwitchAxe)
            {
                ExtractSwitchAxePhialInfo(attributes, out SwitchAxePhialType phialType, out int phialValue);
                outputWeapon = new SwitchAxe(name, phialType, phialValue, attack, affinity, defense, null, eldersealLevel, elementInfo, slots);
            }
            else if (type == WeaponType.Gunlance)
            {
                ExtractGunlanceShellingInfo(attributes, out GunlanceShellingType shellingType, out int shellingLevel);
                outputWeapon = new Gunlance(name, shellingType, shellingLevel, attack, affinity, defense, null, eldersealLevel, elementInfo, slots);
            }
            else if (type == WeaponType.InsectGlaive)
            {
                KinsectBonusType kinsectBonusType = ExtractInsectGlaiveKinsectBonusType(attributes);
                outputWeapon = new InsectGlaive(name, kinsectBonusType, attack, affinity, defense, null, eldersealLevel, elementInfo, slots);
            }
            else
                outputWeapon = new Weapon(name, type, attack, affinity, defense, null, eldersealLevel, elementInfo, slots);

            return outputWeapon.UpdateId((int)weapon["id"]);
        }

        private KinsectBonusType ExtractInsectGlaiveKinsectBonusType(JObject attributes)
        {
            if (attributes.TryGetValue("boostType", out JToken value))
                return ConvertInsectGlaiveKinsectBonusType((string)value);
            else
                throw new FormatException($"An Insect Glaive is missing kinsect bonus type.");
        }

        private void ExtractGunlanceShellingInfo(JObject attributes, out GunlanceShellingType shellingType, out int shellingLevel)
        {
            shellingType = GunlanceShellingType.None;
            shellingLevel = 0;

            if (attributes.TryGetValue("shellingType", out JToken value))
            {
                string shellingInfoContent = (string)value;
                string[] shellingInfoParts = shellingInfoContent.Split(' ');

                if (shellingInfoParts.Length != 2 || shellingInfoParts[1].StartsWith("LV") == false)
                    throw new FormatException($"Unsupported '{shellingInfoContent}' Gunlance shelling info.");

                shellingType = ConvertGunlanceShellingType(shellingInfoParts[0]);
                if (int.TryParse(shellingInfoParts[1].Substring(2), out shellingLevel) == false)
                    throw new FormatException($"Unsupported '{shellingInfoParts}' Gunlance shelling level.");
            }
            else
                throw new FormatException($"A Gunlance is missing shelling info.");
        }

        private void ExtractSwitchAxePhialInfo(JObject attributes, out SwitchAxePhialType phialType, out int phialValue)
        {
            phialType = SwitchAxePhialType.None;
            phialValue = 0;

            if (attributes.TryGetValue("phialType", out JToken value))
            {
                string phialTypeContent = (string)value;

                if (phialTypeContent == "Power Phial")
                    phialType = SwitchAxePhialType.Power;
                else if (phialTypeContent == "Power Element Phial")
                    phialType = SwitchAxePhialType.PowerElement;
                else
                {
                    int i = 0;
                    int index = -1;
                    foreach (char c in phialTypeContent)
                    {
                        if (char.IsNumber(c))
                        {
                            index = i;
                            break;
                        }
                        i++;
                    }

                    phialType = ConvertSwitchAxePhialType(phialTypeContent.Substring(0, index - 1));
                    if (int.TryParse(phialTypeContent.Substring(index), out phialValue) == false)
                        throw new FormatException($"Unsupported '{phialTypeContent}' Switch Axe phial type.");
                }
            }
        }

        private ChargeBladePhialType ExtractChargeBladePhialType(JObject attributes)
        {
            if (attributes.TryGetValue("phialType", out JToken value))
                return ConvertChargeBladePhialType((string)value);

            return ChargeBladePhialType.None;
        }

        private void ExtractAttributes(JObject attributes, out int attack, out int affinity, out int defense, out EldersealLevel elderseal)
        {
            attack = (int)attributes["attack"];
            affinity = 0;
            defense = 0;
            elderseal = EldersealLevel.None;

            JToken value;

            if (attributes.TryGetValue("affinity", out value))
                affinity = (int)value;

            if (attributes.TryGetValue("defense", out value))
                defense = (int)value;

            if (attributes.TryGetValue("elderseal", out value))
                elderseal = ConvertEldersealLevel((string)value);
        }

        private ElementInfo[] ExtractElementInfo(JArray elements)
        {
            if (elements.Count == 0)
                return new ElementInfo[0];

            return elements
                .Select(elem => new ElementInfo(
                    ConvertElementType((string)elem["type"]),
                    (bool)elem["hidden"],
                    (int)elem["damage"]
                ))
                .ToArray();
        }

        private int[] ExtractSlots(JArray slots)
        {
            int[] result = new int[slots.Count];

            for (int i = 0; i < result.Length; i++)
                result[i] = (int)slots[i]["rank"];

            return result;
        }

        private WeaponType ConvertWeaponType(string weaponType)
        {
            switch (weaponType)
            {
                case "great-sword": return WeaponType.GreatSword;
                case "long-sword": return WeaponType.LongSword;
                case "sword-and-shield": return WeaponType.SwordAndShield;
                case "dual-blades": return WeaponType.DualBlades;
                case "hammer": return WeaponType.Hammer;
                case "hunting-horn": return WeaponType.HuntingHorn;
                case "lance": return WeaponType.Lance;
                case "gunlance": return WeaponType.Gunlance;
                case "switch-axe": return WeaponType.SwitchAxe;
                case "charge-blade": return WeaponType.ChargeBlade;
                case "insect-glaive": return WeaponType.InsectGlaive;
            }

            throw new FormatException($"Unsupported '{weaponType}' weapon type.");
        }

        private ElementType ConvertElementType(string elementType)
        {
            switch (elementType)
            {
                case "fire": return ElementType.Fire;
                case "water": return ElementType.Water;
                case "thunder": return ElementType.Thunder;
                case "ice": return ElementType.Ice;
                case "dragon": return ElementType.Dragon;
                case "poison": return ElementType.Poison;
                case "sleep": return ElementType.Sleep;
                case "paralysis": return ElementType.Paralysis;
                case "blast": return ElementType.Blast;
            }

            throw new FormatException($"Unsupported '{elementType}' element type.");
        }

        private EldersealLevel ConvertEldersealLevel(string eldersealLevel)
        {
            switch (eldersealLevel)
            {
                case "low": return EldersealLevel.Low;
                case "average": return EldersealLevel.Average;
                case "high": return EldersealLevel.High;
            }

            throw new FormatException($"Unsupported '{eldersealLevel}' elderseal level.");
        }

        private ChargeBladePhialType ConvertChargeBladePhialType(string chargeBladePhialType)
        {
            switch (chargeBladePhialType)
            {
                case "Impact Phial": return ChargeBladePhialType.Impact;
                case "Power Element Phial": return ChargeBladePhialType.Elemental;
            }

            throw new FormatException($"Unsupported '{chargeBladePhialType}' Charge Blade phial type.");
        }

        private SwitchAxePhialType ConvertSwitchAxePhialType(string switchAxePhialType)
        {
            switch (switchAxePhialType)
            {
                case "Element Phial": return SwitchAxePhialType.PowerElement;
                case "Power Phial": return SwitchAxePhialType.Power;
                case "Dragon Phial": return SwitchAxePhialType.Dragon;
                case "Exhaust Phial": return SwitchAxePhialType.Exhaust;
                case "Para Phial": return SwitchAxePhialType.Paralysis;
                case "Poison Phial": return SwitchAxePhialType.Poison;
            }

            throw new FormatException($"Unsupported '{switchAxePhialType}' Switch Axe phial type.");
        }

        private GunlanceShellingType ConvertGunlanceShellingType(string gunlanceShellingType)
        {
            switch (gunlanceShellingType)
            {
                case "Normal": return GunlanceShellingType.Normal;
                case "Long": return GunlanceShellingType.Long;
                case "Wide": return GunlanceShellingType.Wide;
            }

            throw new FormatException($"Unsupported '{gunlanceShellingType}' Gunlance shelling type.");
        }

        private KinsectBonusType ConvertInsectGlaiveKinsectBonusType(string insectGlaiveKinsectBonusType)
        {
            switch (insectGlaiveKinsectBonusType)
            {
                case "Speed Boost": return KinsectBonusType.Speed;
                case "Element Boost": return KinsectBonusType.Element;
                case "Health Boost": return KinsectBonusType.Health;
                case "Stamina Boost": return KinsectBonusType.Stamina;
                case "Blunt Boost": return KinsectBonusType.Blunt;
                case "Sever Boost": return KinsectBonusType.Sever;
            }

            throw new FormatException($"Unsupported '{insectGlaiveKinsectBonusType}' Insect Glaive kinsect bonus type.");
        }

        private async Task<JArray> Download(HttpClient httpClient, string weaponType)
        {
            return (JArray)JsonConvert.DeserializeObject(await httpClient.GetStringAsync($"https://mhw-db.com/weapons?q={{%22type%22:%22{weaponType}%22}}"));
        }
    }
}
