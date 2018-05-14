using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MHWSharpnessExtractor
{
    public class Markup
    {
        public string Name { get; }
        public int MatchPosition { get; }
        public int MatchLength { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }
        public string[] Classes { get; }

        internal Markup(string markupName, int matchPosition, int matchLength, IDictionary<string, string> properties)
        {
            Name = markupName;
            MatchPosition = matchPosition;
            MatchLength = matchLength;
            Properties = new ReadOnlyDictionary<string, string>(properties);

            if (Properties.TryGetValue("class", out string classesString))
            {
                Classes = classesString
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct()
                    .ToArray();
            }
            else
                Classes = new string[0];
        }

        public override string ToString()
        {
            return $"<{Name} {string.Join(" ", Properties.Select(x => $"{x.Key}{(x.Value == null ? string.Empty : $"='{x.Value}'")}"))} />";
        }

        public static Regex RegularExpression { get; } = new Regex(@"\<(?<markupName>\w+)(?<kv>\s+\w+(\s*=\s*(("".*?"")|('.*?')|\w+?))?)*\s*/?\s*\>");

        public static Markup FromString(string htmlExpression, int start = 0)
        {
            if (start < 0 || htmlExpression.Length <= start)
                return null;

            Match m = RegularExpression.Match(htmlExpression, start);

            if (m.Success == false)
                return null;

            CaptureCollection kvs = m.Groups["kv"].Captures;

            var keyValues = new Dictionary<string, string>();
            foreach (Capture capture in kvs)
            {
                int equalIndex = capture.Value.IndexOf('=');
                if (equalIndex < 0)
                    keyValues[capture.Value.Trim()] = null;
                else
                {
                    string k = capture.Value.Substring(0, equalIndex).Trim().ToLower();
                    string v = capture.Value.Substring(equalIndex + 1).Trim();

                    if (v.StartsWith("\"") && v.EndsWith("\""))
                        v = v.Trim('"');
                    else if (v.StartsWith("'") && v.EndsWith("'"))
                        v = v.Trim('\'');

                    keyValues[k] = v;
                }
            }

            string markupName = m.Groups["markupName"].Value.ToLower();

            return new Markup(markupName, m.Index, m.Length, keyValues);
        }
    }

    public static class HtmlUtils
    {
        public static Markup Until(string content, Predicate<Markup> match)
        {
            int currentPosition = 0;
            return Until(content, ref currentPosition, match);
        }

        public static Markup Until(string content, ref int currentPosition, Predicate<Markup> match)
        {
            while (true)
            {
                Markup markup = GetNextMarkup(content, ref currentPosition);
                if (markup == null)
                    return null;

                if (match(markup))
                    return markup;
            }
        }

        public static Markup GetNextMarkup(string content, ref int currentPosition)
        {
            Markup markup = Markup.FromString(content, currentPosition);
            if (markup == null)
                return null;

            currentPosition = markup.MatchPosition + markup.MatchLength;

            return markup;
        }

        public static string GetMarkupContent(string content, Markup markup)
        {
            int startIndex = markup.MatchPosition + markup.MatchLength;
            int endIndex = content.IndexOf("</", startIndex);

            if (endIndex < 0)
                return null;

            return content.Substring(startIndex, endIndex - startIndex);
        }
    }
}
