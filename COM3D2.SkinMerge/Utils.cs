using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    public static class Extensions
    {
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var cultureInfo = CultureInfo.CurrentCulture;
            var textInfo = cultureInfo.TextInfo;
            return textInfo.ToTitleCase(input.ToLower());
        }

        public static string TrimUTF8Bom(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return input[0] == '\uFEFF' ? input.Substring(1) : input;
        }

        public static string ToF2(this float input)
        {
            return input.ToString("F2", CultureInfo.CurrentCulture);
        }

        public static string ToCompactString(this float input)
        {
            return Mathf.Approximately(input % 1, 0f)
                ? ((int)input).ToString()
                : input.ToString(CultureInfo.InvariantCulture);
        }

        public static int GetRid(this string input)
        {
            return input.ToLower().GetHashCode();
        }
   
        public static IEnumerable<List<T>> ChunkList<T>(this List<T> list, int chunkSize)
        {
            for (var i = 0; i < list.Count; i += chunkSize)
                yield return list.GetRange(i, Math.Min(chunkSize, list.Count - i));
        }
    }
}