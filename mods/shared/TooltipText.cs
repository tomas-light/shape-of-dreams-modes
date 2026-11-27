#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShapeOfDreamsMods
{
    internal static class TooltipText
    {
        private const string DefaultLanguage = "en-US";
        private const string ResourceName = "ShapeOfDreamsMods.TooltipText.tsv";
        private static readonly Dictionary<string, Dictionary<string, string>> Translations = ReadTranslations();

        internal static CultureInfo GetCulture(string? language)
        {
            if (!string.IsNullOrEmpty(language))
            {
                try
                {
                    return CultureInfo.GetCultureInfo(language);
                }
                catch (CultureNotFoundException)
                {
                    // A language added by the game may be unknown to this version of Mono.
                }
            }
            return CultureInfo.GetCultureInfo(DefaultLanguage);
        }

        internal static string Get(CultureInfo culture, string key)
        {
            if (Translations.TryGetValue(culture.Name, out var locale) && locale.TryGetValue(key, out var translated))
                return translated;

            // Preserve the distinction between simplified and traditional Chinese for regional variants.
            string language = culture.TwoLetterISOLanguageName;
            if (language == "zh")
                language = culture.Name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    culture.Name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                    culture.Name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase) ? "zh-TW" : "zh-CN";
            foreach (var candidate in Translations)
                if ((candidate.Key == language || candidate.Key.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase)) &&
                    candidate.Value.TryGetValue(key, out translated))
                    return translated;

            return Translations[DefaultLanguage][key];
        }

        internal static string Format(CultureInfo culture, string key, params object[] values) =>
            string.Format(culture, Get(culture, key), values);

        internal static bool Matches(CultureInfo culture, string key, string text)
        {
            foreach (string token in Get(culture, key).Split('|'))
                if (culture.CompareInfo.IndexOf(text, token, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                    return true;
            return false;
        }

        private static Dictionary<string, Dictionary<string, string>> ReadTranslations()
        {
            using var stream = typeof(TooltipText).Assembly.GetManifestResourceStream(ResourceName) ??
                throw new InvalidOperationException($"Missing embedded resource '{ResourceName}'.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string[] languages = (reader.ReadLine() ?? throw new FormatException("Translation catalog is empty.")).Split('\t');
            var translations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 1; index < languages.Length; index++)
                translations.Add(languages[index], new Dictionary<string, string>(StringComparer.Ordinal));

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                string[] cells = line.Split('\t');
                if (cells.Length != languages.Length)
                    throw new FormatException($"Translation row '{cells[0]}' has {cells.Length - 1} translations, expected {languages.Length - 1}.");
                for (int index = 1; index < cells.Length; index++)
                    if (!string.IsNullOrWhiteSpace(cells[index]))
                        translations[languages[index]].Add(cells[0], cells[index]);
            }
            return translations;
        }
    }
}
