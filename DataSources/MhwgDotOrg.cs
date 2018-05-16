using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MHWSharpnessExtractor.DataSources
{
    public class MhwgDotOrg : IDataSource
    {
        private static class Constants
        {
            public const int SharpnessMultiplier = 10; // input values are stored out of 40, output is desired out of 400
        }

        public string Name { get; } = "mhwg.org";

        public async Task<IList<Weapon>> ProduceWeaponsAsync()
        {
            var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(3.0);

            var result = new List<Weapon>();

            var tasks = new Task<IList<Weapon>>[]
            {
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4000.html"), WeaponType.GreatSword),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4001.html"), WeaponType.LongSword),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4002.html"), WeaponType.SwordAndShield),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4003.html"), WeaponType.DualBlades),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4004.html"), WeaponType.Hammer),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4005.html"), WeaponType.HuntingHorn),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4006.html"), WeaponType.Lance),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4007.html"), WeaponType.Gunlance),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4008.html"), WeaponType.SwitchAxe),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4009.html"), WeaponType.ChargeBlade),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4010.html"), WeaponType.InsectGlaive),
            };

            await Task.WhenAll(tasks);

            foreach (Task<IList<Weapon>> task in tasks)
                result.AddRange(task.Result);

            return result;
        }

        private async Task<IList<Weapon>> GetWeaponsAsync(Task<string> contentProvider, WeaponType weaponType)
        {
            string content = await contentProvider;

            int currentPosition = 0;

            var weapons = new List<Weapon>();

            while (currentPosition < content.Length)
            {
                // === name =========================================================

                Markup weaponImageMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "img" && m.Classes.Contains("wp_img"));
                if (weaponImageMarkup == null)
                    break;

                int closingTdIndex = content.IndexOf("</td>", currentPosition);
                if (closingTdIndex < 0)
                    throw BadFormat($"Missing closing 'td' for weapon name at position {currentPosition}");

                string weaponNameContent = content.Substring(currentPosition, closingTdIndex - currentPosition);

                string weaponName = GetWeaponName(weaponNameContent);
                if (weaponName == null)
                    throw BadFormat($"Could not determine weapon name at position {currentPosition}");

                // === attack =========================================================

                Markup attackMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Contains("b"));
                if (attackMarkup == null)
                    throw BadFormat($"Could not find attack markup for weapon '{weaponName}'");

                int attack;
                string attackStringValue = HtmlUtils.GetMarkupContent(content, attackMarkup);
                if (int.TryParse(attackStringValue, out attack) == false)
                    throw BadFormat($"Invalid attack numeric value '{(attackStringValue ?? "(null)")}' for weapon '{weaponName}'");

                // === element + misc =========================================================

                Markup weaponElementMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Length > 0);
                if (weaponElementMarkup == null)
                    throw BadFormat($"Could not find element markup for weapon '{weaponName}'");

                closingTdIndex = content.IndexOf("</td>", currentPosition);
                if (closingTdIndex < 0)
                    throw BadFormat($"Missing closing 'td' of element for weapon '{weaponName}'");

                string weaponElementContent = content.Substring(currentPosition, closingTdIndex - currentPosition);

                TryGetWeaponElement(weaponName, weaponElementContent, out int affinity, out int defense, out ElementInfo[] elementInfo, out EldersealLevel eldersealLevel);

                // === weapon specific info =============================================================

                ChargeBladePhialType chargeBladePhialType = ChargeBladePhialType.None;
                Melody[] huntingHornMelodies = null;
                SwitchAxePhialType switchAxePhialType = SwitchAxePhialType.None;
                int switchAxePhialValue = 0;
                GunlanceShellingType gunlanceShellingType = GunlanceShellingType.None;
                int gunlanceShellingValue = 0;
                KinsectBonusType insectGlaiveKinsectBonusType = KinsectBonusType.None;

                if (weaponType == WeaponType.ChargeBlade)
                {
                    // === charge blade phial type =============================================================
                    TryGetChargeBladePhialType(weaponName, content, ref currentPosition, out chargeBladePhialType);
                }
                else if (weaponType == WeaponType.HuntingHorn)
                {
                    // === hunting horn melodies =============================================================
                    TryGetHuntingHornMelodies(weaponName, content, ref currentPosition, out huntingHornMelodies);
                }
                else if (weaponType == WeaponType.SwitchAxe)
                {
                    // === hunting horn melodies =============================================================
                    TryGetSwitchAxePhialInfo(weaponName, content, ref currentPosition, out switchAxePhialType, out switchAxePhialValue);
                }
                else if (weaponType == WeaponType.Gunlance)
                {
                    // === gunlance shelling info =============================================================
                    TryGetGunlanceShellingInfo(weaponName, content, ref currentPosition, out gunlanceShellingType, out gunlanceShellingValue);
                }
                else if (weaponType == WeaponType.InsectGlaive)
                {
                    // === insect glaive kinsect bonus type =============================================================
                    TryGetInsectGlaiveKinsectBonus(weaponName, content, ref currentPosition, out insectGlaiveKinsectBonusType);
                }

                // === sharpness ranks =========================================================

                Markup weaponSharpnessMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "div" && m.Classes.Contains("kireage"));
                if (weaponSharpnessMarkup == null)
                    throw BadFormat($"Could not find outter sharpness markup reference for weapon '{weaponName}'");

                weaponSharpnessMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "span" && m.Classes.Contains("kr7"));
                if (weaponSharpnessMarkup == null)
                    throw BadFormat($"Could not find inner sharpness markup reference for weapon '{weaponName}'");

                var ranks = new List<(int rank, int value)>();

                for (int i = 0; i < 7; i++)
                {
                    Markup sharpnessRankMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "span" && m.Classes.Any(x => x.StartsWith("kr")));
                    if (sharpnessRankMarkup == null)
                        break;

                    string matchingClassName = sharpnessRankMarkup.Classes.First(x => x.StartsWith("kr"));

                    if (int.TryParse(matchingClassName.Substring(2), out int rank) == false)
                        throw BadFormat($"Invalid shrpness class name '{matchingClassName}' for weapon '{weaponName}' (expected 'krX' where X is a numeric value)");

                    if (rank >= 7)
                        break;

                    string rankValue = HtmlUtils.GetMarkupContent(content, sharpnessRankMarkup);
                    if (rankValue == null)
                        throw BadFormat($"Could not find inner sharpness markup content for weapon '{weaponName}'");

                    if (rankValue.Length > 0)
                        ranks.Add((rank, rankValue.Length * Constants.SharpnessMultiplier));
                }

                ranks.Sort((a, b) => a.rank.CompareTo(b.rank));

                int[] sharpnessRanks = ranks.Select(x => x.value).ToArray();

                // === slots =========================================================

                Markup slotsMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Length > 0);
                if (slotsMarkup == null)
                    throw BadFormat($"Could not find slots markup reference for weapon '{weaponName}'");

                string slotsContent = HtmlUtils.GetMarkupContent(content, slotsMarkup);
                if (slotsContent == null)
                    throw BadFormat($"Could not slots markup content for weapon '{weaponName}'");

                string cleanedSlotsContent = slotsContent.Replace(" ", string.Empty).Replace("-", string.Empty);
                if (cleanedSlotsContent.Length > 3)
                    throw BadFormat($"Invalid slots value '{slotsContent}' value for weapon '{weaponName}'");

                int[] slots = new int[cleanedSlotsContent.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    switch (cleanedSlotsContent[i])
                    {
                        case '①':
                            slots[i] = 1;
                            break;
                        case '②':
                            slots[i] = 2;
                            break;
                        case '③':
                            slots[i] = 3;
                            break;
                        default:
                            throw BadFormat($"Invalid single slot value '{slots[i]}' for weapon '{weaponName}' (expected ①, ② or ③)");
                    }
                }

                // ==================================================================================

                Weapon weapon;

                if (weaponType == WeaponType.ChargeBlade)
                {
                    weapon = new ChargeBlade(
                        weaponName,
                        chargeBladePhialType,
                        attack,
                        affinity,
                        defense,
                        sharpnessRanks,
                        eldersealLevel,
                        elementInfo,
                        slots
                    );
                }
                else if (weaponType == WeaponType.HuntingHorn)
                {
                    weapon = new HuntingHorn(
                        weaponName,
                        huntingHornMelodies,
                        attack,
                        affinity,
                        defense,
                        sharpnessRanks,
                        eldersealLevel,
                        elementInfo,
                        slots
                    );
                }
                else if (weaponType == WeaponType.SwitchAxe)
                {
                    weapon = new SwitchAxe(
                        weaponName,
                        switchAxePhialType,
                        switchAxePhialValue,
                        attack,
                        affinity,
                        defense,
                        sharpnessRanks,
                        eldersealLevel,
                        elementInfo,
                        slots
                    );
                }
                else if (weaponType == WeaponType.Gunlance)
                {
                    weapon = new Gunlance(
                        weaponName,
                        gunlanceShellingType,
                        gunlanceShellingValue,
                        attack,
                        affinity,
                        defense,
                        sharpnessRanks,
                        eldersealLevel,
                        elementInfo,
                        slots
                    );
                }
                else if (weaponType == WeaponType.InsectGlaive)
                {
                    weapon = new InsectGlaive(
                        weaponName,
                        insectGlaiveKinsectBonusType,
                        attack,
                        affinity,
                        defense,
                        sharpnessRanks,
                        eldersealLevel,
                        elementInfo,
                        slots
                    );
                }
                else
                {
                    weapon = new Weapon(
                        weaponName,
                        weaponType,
                        attack,
                        affinity,
                        defense,
                        sharpnessRanks,
                        eldersealLevel,
                        elementInfo,
                        slots
                    );
                }

                weapons.Add(weapon);
            }

            return weapons;
        }

        private FormatException BadFormat(string message)
        {
            return new FormatException(message);
        }

        private void TryGetChargeBladePhialType(string weaponName, string content, ref int currentPosition, out ChargeBladePhialType phialType)
        {
            Markup cbPhialMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Contains("type_0"));
            if (cbPhialMarkup == null)
                throw BadFormat($"Could not find Charge Blade phial markup for weapon '{weaponName}'");

            string cbPhialContent = HtmlUtils.GetMarkupContent(content, cbPhialMarkup);
            if (cbPhialContent == null)
                throw BadFormat($"Could not find Charge Blade phial markup content for weapon '{weaponName}'");

            if (cbPhialContent == "榴弾")
                phialType = ChargeBladePhialType.Impact;
            else if (cbPhialContent == "強属性")
                phialType = ChargeBladePhialType.Elemental;
            else
                throw BadFormat($"Invalid Charge Blade phial value '{cbPhialContent}' for weapon '{weaponName}'");
        }

        private void TryGetHuntingHornMelodies(string weaponName, string content, ref int currentPosition, out Melody[] melodies)
        {
            melodies = null;

            Markup hhMelodiesMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "a" && m.Properties.ContainsKey("href") && m.Properties["href"].StartsWith("/data/4243.html#"));
            if (hhMelodiesMarkup == null)
                throw BadFormat($"Could not find Hunting Horn melodies markup for weapon '{weaponName}'");

            var localMelodies = new List<Melody>(3);

            for (int i = 0; i < 3; i++)
            {
                Markup melodyMarkup = HtmlUtils.GetNextMarkup(content, ref currentPosition);
                if (melodyMarkup == null)
                    throw BadFormat($"Could not find Hunting Horn note markup for weapon '{weaponName}'");

                if (melodyMarkup.Properties.ContainsKey("style") == false)
                    throw BadFormat($"Hunting Horn note markup is missing 'style' property for weapon '{weaponName}'");

                string stylesValue = melodyMarkup.Properties["style"];
                IReadOnlyDictionary<string, string> styles = HtmlUtils.ParseStyle(stylesValue);
                if (styles == null)
                    throw BadFormat($"Invalid styles value '{(stylesValue ?? "(null)")}' for Hunting Horn weapon '{weaponName}'");

                if (styles.TryGetValue("color", out string colorValue) == false)
                    throw BadFormat($"Missing 'color' style property for Hunting Horn weapon '{weaponName}'");

                switch (colorValue.ToLower())
                {
                    case "#f3f3f3":
                        localMelodies.Add(Melody.White);
                        break;
                    case "#e0002a":
                        localMelodies.Add(Melody.Red);
                        break;
                    case "blue":
                        localMelodies.Add(Melody.Blue);
                        break;
                    case "#c778c7":
                        localMelodies.Add(Melody.Purple);
                        break;
                    case "#00cc00":
                        localMelodies.Add(Melody.Green);
                        break;
                    case "#ef810f":
                        localMelodies.Add(Melody.Orange);
                        break;
                    case "#99f8f8":
                        localMelodies.Add(Melody.Cyan);
                        break;
                    case "#eeee00":
                        localMelodies.Add(Melody.Yellow);
                        break;
                    default:
                        throw BadFormat($"Invalid color value '{colorValue}' for Hunting Horn weapon '{weaponName}'");
                }
            }

            melodies = localMelodies.ToArray();
        }

        private void TryGetSwitchAxePhialInfo(string weaponName, string content, ref int currentPosition, out SwitchAxePhialType phialType, out int phialValue)
        {
            phialType = SwitchAxePhialType.None;
            phialValue = 0;

            Markup saPhialMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Any(x => x.StartsWith("type_")));
            if (saPhialMarkup == null)
                throw BadFormat($"Could not find Switch Axe phial markup for weapon '{weaponName}'");

            string saPhialContent = HtmlUtils.GetMarkupContent(content, saPhialMarkup);
            if (saPhialContent == null)
                throw BadFormat($"Could not find Switch Axe phial markup content for weapon '{weaponName}'");

            if (saPhialContent == "強撃")
                phialType = SwitchAxePhialType.Power;
            else if (saPhialContent.StartsWith("毒"))
            {
                phialType = SwitchAxePhialType.Poison;
                if (TryGetNumericValueAfterCharacters(saPhialContent, out phialValue) == false)
                    throw BadFormat($"Invalid Switch Axe poison phial value '{saPhialContent}' for weapon '{weaponName}'");
            }
            else if (saPhialContent == "強属性")
                phialType = SwitchAxePhialType.PowerElement;
            else if (saPhialContent.StartsWith("減気"))
            {
                phialType = SwitchAxePhialType.Exhaust;
                if (TryGetNumericValueAfterCharacters(saPhialContent, out phialValue) == false)
                    throw BadFormat($"Invalid Switch Axe exhaust phial value '{saPhialContent}' for weapon '{weaponName}'");
            }
            else if (saPhialContent.StartsWith("滅龍"))
            {
                phialType = SwitchAxePhialType.Dragon;
                if (TryGetNumericValueAfterCharacters(saPhialContent, out phialValue) == false)
                    throw BadFormat($"Invalid Switch Axe dragon phial value '{saPhialContent}' for weapon '{weaponName}'");
            }
            else if (saPhialContent.StartsWith("麻痺"))
            {
                phialType = SwitchAxePhialType.Paralysis;
                if (TryGetNumericValueAfterCharacters(saPhialContent, out phialValue) == false)
                    throw BadFormat($"Invalid Switch Axe paralysis phial value '{saPhialContent}' for weapon '{weaponName}'");
            }
            else
                throw BadFormat($"Invalid Switch Axe phial value '{saPhialContent}' for weapon '{weaponName}'");
        }

        private void TryGetGunlanceShellingInfo(string weaponName, string content, ref int currentPosition, out GunlanceShellingType shellingType, out int shellingValue)
        {
            shellingType = GunlanceShellingType.None;
            shellingValue = 0;

            Markup glShellingMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Contains("type_0"));
            if (glShellingMarkup == null)
                throw BadFormat($"Could not find Gunlance shelling markup for weapon '{weaponName}'");

            string glShellingContent = HtmlUtils.GetMarkupContent(content, glShellingMarkup);
            if (glShellingContent == null)
                throw BadFormat($"Could not find Gunlance shelling markup content for weapon '{weaponName}'");

            if (glShellingContent.StartsWith("通常"))
                shellingType = GunlanceShellingType.Normal;
            else if (glShellingContent.StartsWith("拡散"))
                shellingType = GunlanceShellingType.Wide;
            else if (glShellingContent.StartsWith("放射"))
                shellingType = GunlanceShellingType.Long;
            else
                throw BadFormat($"Invalid Gunlance shelling value '{glShellingContent}' for weapon '{weaponName}'");

            TryGetNumericValueAfterCharacters(glShellingContent, out shellingValue);
        }

        private void TryGetInsectGlaiveKinsectBonus(string weaponName, string content, ref int currentPosition, out KinsectBonusType kinsectBonusType)
        {
            kinsectBonusType = KinsectBonusType.None;

            Markup igBonusMarkup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "td" && m.Classes.Contains("type_0"));
            if (igBonusMarkup == null)
                throw BadFormat($"Could not find Insect Glaive kinsect bonus markup for weapon '{weaponName}'");

            string igBonusContent = HtmlUtils.GetMarkupContent(content, igBonusMarkup);
            if (igBonusContent == null)
                throw BadFormat($"Could not find Insect Glaive kinsect bonus markup content for weapon '{weaponName}'");

            switch (igBonusContent)
            {
                case "攻撃強化【切断】":
                    kinsectBonusType = KinsectBonusType.Sever;
                    break;
                case "スピード強化":
                    kinsectBonusType = KinsectBonusType.Speed;
                    break;
                case "攻撃強化【属性】":
                    kinsectBonusType = KinsectBonusType.Element;
                    break;
                case "回復強化【体力】":
                    kinsectBonusType = KinsectBonusType.Health;
                    break;
                case "回復強化【スタミナ】":
                    kinsectBonusType = KinsectBonusType.Stamina;
                    break;
                case "攻撃強化【打撃】":
                    kinsectBonusType = KinsectBonusType.Blunt;
                    break;
                default:
                    throw BadFormat($"Invalid Insect Glaive kinsect bonus value '{igBonusContent}' for weapon '{weaponName}'");
            }
        }

        private string GetWeaponName(string content)
        {
            int currentPosition = 0;

            Markup markup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "a" && m.Properties.ContainsKey("href") && m.Properties["href"].StartsWith("/ida/"));
            if (markup != null)
                return HtmlUtils.GetMarkupContent(content, markup);

            return content.Trim();
        }

        private void TryGetWeaponElement(string weaponName, string content, out int affinity, out int defense, out ElementInfo[] elementInfo, out EldersealLevel eldersealLevel)
        {
            affinity = 0;
            defense = 0;

            int localAffinity = 0;
            int localDefense = 0;

            EldersealLevel localEldersealLevel = EldersealLevel.None;

            IList<string> lines = content
                .Split("\n")
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            var elements = new List<ElementInfo>();

            foreach (string line in lines)
            {
                Markup markup = HtmlUtils.Until(line, IsValidWeaponElementMarkup);

                if (markup == null)
                {
                    // handle special cases...

                    if (line.StartsWith("会心")) // affinity
                        TryGetAffinityValue(weaponName, line, out localAffinity);
                    else if (line.StartsWith("防御")) // defense
                        TryGetDefenseValue(weaponName, line, out localDefense);
                    else
                        break;
                }
                else if (IsEldersealMarkup(markup))
                {
                    string eldersealStringContent = HtmlUtils.GetMarkupContent(line, markup);
                    if (eldersealStringContent.Contains("小"))
                        eldersealLevel = EldersealLevel.Low;
                    else if (eldersealStringContent.Contains("中"))
                        eldersealLevel = EldersealLevel.Average;
                    else if (eldersealStringContent.Contains("大"))
                        eldersealLevel = EldersealLevel.High;
                    else
                        throw BadFormat($"Could not determine elderseal value for weapon '{weaponName}'");
                }
                else if (IsElementMarkup(markup))
                {
                    ElementType elementType = ElementType.None;
                    bool isHidden = true;
                    int elementValue = 0;

                    string elementValueString = HtmlUtils.GetMarkupContent(line, markup);
                    if (elementValueString == null)
                        throw BadFormat($"Could not find element markup content for weapon '{weaponName}'");

                    elementValueString = elementValueString.Trim();

                    bool startParenthesis = elementValueString.StartsWith("(");
                    bool endParenthesis = elementValueString.EndsWith(")");

                    if (startParenthesis && endParenthesis)
                    {
                        isHidden = true;
                        elementValueString = elementValueString.Substring(1, elementValueString.Length - 2); // remove leading and trailing parenthesis
                    }
                    else if (startParenthesis == false && endParenthesis == false)
                        isHidden = false;
                    else
                        throw BadFormat($"Invalid element value '{(elementValueString ?? "(null)")}' for weapon '{weaponName}'");

                    TryGetElementTypeAndValue(weaponName, elementValueString, out elementType, out elementValue);

                    elements.Add(new ElementInfo(elementType, isHidden, elementValue));
                }
            }

            if (elements.Count > 1)
            {
            }

            affinity = localAffinity;
            defense = localDefense;
            elementInfo = elements.ToArray();
            eldersealLevel = localEldersealLevel;
        }

        private bool TryGetNumericValueAfterCharacters(string content, out int value)
        {
            value = 0;

            int digitIndex = Array.FindIndex(content.ToCharArray(), char.IsNumber);
            if (digitIndex < 0)
                return false;

            return int.TryParse(content.Substring(digitIndex), out value);
        }

        private void TryGetElementTypeAndValue(string weaponName, string content, out ElementType elementType, out int value)
        {
            value = 0;
            elementType = ElementType.None;

            int digitIndex = Array.FindIndex(content.ToCharArray(), char.IsNumber);
            if (digitIndex < 0)
                throw BadFormat($"Invalid element value '{content}' for weapon '{weaponName}'");

            switch (content.Substring(0, digitIndex))
            {
                case "火":
                    elementType = ElementType.Fire;
                    break;
                case "水":
                    elementType = ElementType.Water;
                    break;
                case "雷":
                    elementType = ElementType.Thunder;
                    break;
                case "氷":
                    elementType = ElementType.Ice;
                    break;
                case "龍":
                    elementType = ElementType.Dragon;
                    break;
                case "毒":
                    elementType = ElementType.Poison;
                    break;
                case "睡眠":
                    elementType = ElementType.Sleep;
                    break;
                case "麻痺":
                    elementType = ElementType.Paralysis;
                    break;
                case "爆破":
                    elementType = ElementType.Blast;
                    break;
                //case "":
                //    elementType = ElementType.Stun;
                //    break;
                default:
                    throw BadFormat($"Invalid element value '{content}' for weapon '{weaponName}'");
            }

            int.TryParse(content.Substring(digitIndex), out value);
        }

        private bool IsEldersealMarkup(Markup markup)
        {
            return markup.Name == "span" && markup.Classes.Contains("c_p") && markup.Classes.Contains("b");
        }

        private bool IsElementMarkup(Markup markup)
        {
            return markup.Name == "span" && markup.Classes.Any(x => x.StartsWith("type_"));
        }

        private void TryGetAffinityValue(string weaponName, string line, out int value)
        {
            value = 0;

            Markup markup = Markup.FromString(line);

            if (markup == null)
                throw BadFormat($"Could not find affinity markup reference for weapon '{weaponName}'");

            if (markup.Name != "span")
                throw BadFormat($"Invalid affinity markup reference for weapon '{weaponName}'");

            string strValue = HtmlUtils.GetMarkupContent(line, markup);

            if (int.TryParse(strValue, out value) == false)
                throw BadFormat($"Invalid affinity numeric value '{(strValue ?? "(null)")}' for weapon '{weaponName}'");
        }

        private void TryGetDefenseValue(string weaponName, string line, out int value)
        {
            value = 0;

            char[] chars = line.ToCharArray();

            int start = Array.FindIndex(chars, c => char.IsNumber(c));
            int end = Array.FindIndex(chars, start, c => char.IsNumber(c) == false);

            string temp = line.Substring(start, end - start);

            if (int.TryParse(temp, out value) == false)
                throw BadFormat($"Invalid defense numeric value '{(temp ?? "(null)")}' for weapon '{weaponName}'");
        }

        private bool IsValidWeaponElementMarkup(Markup markup)
        {
            return
                IsEldersealMarkup(markup) ||
                IsElementMarkup(markup) ||
                // add more checks here is needed
                false;
        }
    }
}
