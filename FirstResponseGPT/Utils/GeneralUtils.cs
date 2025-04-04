using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstResponseGPT.Utils
{
    public static class GeneralUtils
    {
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        public static string StripSSMLTags(string ssmlText)
        {
            if (string.IsNullOrEmpty(ssmlText)) return string.Empty;

            // Remove anything between and including <> tags
            string stripped = System.Text.RegularExpressions.Regex.Replace(ssmlText, "<[^>]+>", "");

            // Clean up any extra whitespace
            stripped = stripped.Trim();
            while (stripped.Contains("  "))
            {
                stripped = stripped.Replace("  ", " ");
            }

            return stripped;
        }
    }

    public static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue = default)
        {
            return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
        }
    }

    public static class ListExtensions
    {
        public static List<T> TakeLast<T>(this List<T> source, int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (count <= 0)
                return new List<T>();

            return source.Skip(Math.Max(0, source.Count - count)).Take(count).ToList();
        }
    }

}
