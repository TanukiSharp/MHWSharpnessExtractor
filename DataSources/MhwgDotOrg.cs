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
        public string Name { get; } = "mhwg.org";

        public async Task<IList<WeaponInfo>> ProduceWeaponsAsync()
        {
            var httpClient = new HttpClient();
            var result = new List<WeaponInfo>();

            var tasks = new Task<IList<WeaponInfo>>[]
            {
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4000.html"), WeaponType.GreatSword),
                GetWeaponsAsync(httpClient.GetStringAsync("http://mhwg.org/data/4001.html"), WeaponType.LongSword),
            };

            await Task.WhenAll(tasks);

            foreach (Task<IList<WeaponInfo>> task in tasks)
                result.AddRange(task.Result);

            return result;
        }

        private async Task<IList<WeaponInfo>> GetWeaponsAsync(Task<string> contentProvider, WeaponType weaponType)
        {
            string content = await contentProvider;

            int currentPosition = 0;

            var weapons = new List<WeaponInfo>();

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

                TryGetWeaponElement(weaponName, weaponElementContent, out int affinity, out int defense, out ElementInfo elementInfo);

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
                        ranks.Add((rank, rankValue.Length));
                }

                ranks.Sort((a, b) => a.rank.CompareTo(b.rank));

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

                weapons.Add(new WeaponInfo(
                    weaponName,
                    weaponType,
                    attack,
                    affinity,
                    defense,
                    ranks.Select(x => x.value).ToArray(),
                    elementInfo,
                    slots
                ));
            }

            return weapons;
        }

        private FormatException BadFormat(string message)
        {
            return new FormatException(message);
        }

        private string GetWeaponName(string content)
        {
            int currentPosition = 0;

            Markup markup = HtmlUtils.Until(content, ref currentPosition, m => m.Name == "a" && m.Properties.ContainsKey("href") && m.Properties["href"].StartsWith("/ida/"));
            if (markup != null)
                return HtmlUtils.GetMarkupContent(content, markup);

            return content.Trim();
        }

        private void TryGetWeaponElement(string weaponName, string content, out int affinity, out int defense, out ElementInfo elementInfo)
        {
            affinity = 0;
            defense = 0;

            int localAffinity = 0;
            int localDefense = 0;

            ElementType elementType = ElementType.None;
            bool isHidden = true;
            int elementValue = 0;
            EldersealLevel eldersealLevel = EldersealLevel.None;

            IList<string> lines = content
                .Split("\n")
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

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
                    string elementClassName = markup.Classes.First(x => x.StartsWith("type_"));
                    if (int.TryParse(elementClassName.Substring(5), out int numericElementType) == false)
                        throw BadFormat($"Invalid element class '{(elementClassName ?? "(null)")}' for weapon '{weaponName}' (expected 'type_X' where X is a numeric value)");

                    elementType = (ElementType)numericElementType;

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

                    int digitIndex = Array.FindIndex(elementValueString.ToCharArray(), char.IsNumber);
                    if (digitIndex < 0)
                        throw BadFormat($"Could not find element numeric value in '{(elementValueString ?? "(null)")}' for weapon '{weaponName}'");

                    elementValueString = elementValueString.Substring(digitIndex, elementValueString.Length - digitIndex); // remove the leading kanji(s)

                    if (int.TryParse(elementValueString, out elementValue) == false)
                        throw BadFormat($"Invalid element numeric value '{(elementValueString ?? "(null)")}' value for weapon '{weaponName}'");
                }
            }

            affinity = localAffinity;
            defense = localDefense;
            elementInfo = new ElementInfo(elementType, isHidden, elementValue, eldersealLevel);
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
