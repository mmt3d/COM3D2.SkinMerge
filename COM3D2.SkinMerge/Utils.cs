using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    public static class Extensions
    {
        /// <summary>
        /// 文字列のタイトルケース変換(キャピタライズ)するExtension
        /// </summary>
        public static string ToTitleCase(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var cultureInfo = CultureInfo.CurrentCulture;
            var textInfo = cultureInfo.TextInfo;
            return textInfo.ToTitleCase(input.ToLowerInvariant());
        }

        /// <summary>
        /// 文字列の先頭のUTF-8 BOMを削除するExtension
        /// </summary>
        public static string TrimUTF8Bom(this string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return input[0] == '\uFEFF' ? input.Substring(1) : input;
        }

        /// <summary>
        /// floatを小数点以下2桁の文字列に変換するExtension
        /// </summary>
        public static string ToF2(this float input)
        {
            return input.ToString("F2", CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// floatを整数なら整数、小数なら小数点以下も含めた文字列に変換するExtension
        /// </summary>
        public static string ToCompactString(this float input)
        {
            return Mathf.Approximately(input % 1, 0f)
                ? ((int)input).ToString()
                : input.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Rid(hash code)を取得（大文字小文字を区別しない）するExtension
        /// </summary>
        public static int GetRid(this string input)
        {
            return input.ToLowerInvariant().GetHashCode();
        }
   
        /// <summary>
        /// リストを指定サイズのチャンクに分割するExtension
        /// </summary>
        public static IEnumerable<List<T>> ChunkList<T>(this List<T> list, int chunkSize)
        {
            for (var i = 0; i < list.Count; i += chunkSize)
                yield return list.GetRange(i, Math.Min(chunkSize, list.Count - i));
        }
    }
}