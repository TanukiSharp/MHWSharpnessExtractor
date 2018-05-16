using System;
using System.Collections.Generic;
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
            var result = new List<Weapon>();

            var tasks = new Task<IList<Weapon>>[]
            {
                CreateWeapons(httpClient, "great-sword"),
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

        private Weapon CreateWeapon(JObject weapon, WeaponType weaponType)
        {
            int attack;
            int affinity;
            int defense;
            EldersealLevel eldersealLevel;

            ExtractAttributes((JObject)weapon["attributes"], out attack, out affinity, out defense, out eldersealLevel);

            return new Weapon(
                (string)weapon["name"],
                weaponType,
                attack,
                affinity,
                defense,
                null,
                ExtractElementInfo((JArray)weapon["elements"], eldersealLevel),
                ExtractSlots((JArray)weapon["slots"])
            )
            .UpdateId((int)weapon["id"]);
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

        private ElementInfo ExtractElementInfo(JArray elements, EldersealLevel eldersealLevel)
        {
            if (elements.Count == 0)
                return ElementInfo.None;

            if (elements.Count > 1)
                throw new NotSupportedException("A weapon has multiple elements");

            JObject element = (JObject)elements[0];

            return new ElementInfo(
                ConvertElementType((string)element["type"]),
                (bool)element["hidden"],
                (int)element["damage"],
                eldersealLevel
            );
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

        private async Task<JArray> Download(HttpClient httpClient, string weaponType)
        {
            return (JArray)JsonConvert.DeserializeObject(await httpClient.GetStringAsync($"https://mhw-db.com/weapons?q={{%22type%22:%22{weaponType}%22}}"));
        }
    }
}
