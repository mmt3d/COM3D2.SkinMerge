using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;

namespace COM3D2.SkinMerge
{
    using SM = SkinMerge;
    
    internal static class Localization
    {
        private static readonly Dictionary<string, Language> AllLanguages = new Dictionary<string, Language>();
        private static readonly List<string> FallbackLangCodes = new List<string>();
        private static string ConfigPath => Path.Combine(Paths.ConfigPath, SM.PluginName);

        static Localization()
        {
            Setup();
            Init();
        }

        private static void Setup()
        {
            if (!Directory.Exists(ConfigPath))
                Directory.CreateDirectory(ConfigPath);
            SM.ListResourceNames(@"localization\").ForEach(x =>
            {
                var filePath = Path.Combine(ConfigPath, Path.GetFileName(x));
                if (File.Exists(filePath)) return;
                using var writer = new BinaryWriter(File.OpenWrite(filePath));
                writer.Write(SM.GetResourceBytes(x));
            });
        }

        private static void Init()
        {
            var translationFiles = Directory.GetFiles(ConfigPath, "*.json");
            foreach (var file in translationFiles)
            {
                var langCode = Path.GetFileNameWithoutExtension(file);
                var lang = JsonConvert.DeserializeObject<Language>(File.ReadAllText(Path.Combine(ConfigPath, file)));
                AllLanguages[langCode] = lang;
            }

            SetLanguage();
        }
        
        internal static string[] GetLanguageCodes()
        {
            return AllLanguages.Keys.OrderBy(x => x).ToArray();
        }
        
        internal static Dictionary<string, string> GetLanguageNamesMap()
        {
            return AllLanguages.ToDictionary(x => x.Key, x => x.Value.LanguageName);
        }

        internal static void SetLanguage(string languageCode = null)
        {
            FallbackLangCodes.Clear();
            // 指定言語コードの追加
            if (languageCode != null)
            {
                if (AllLanguages.ContainsKey(languageCode))
                    FallbackLangCodes.Add(languageCode);
                if (languageCode.Contains('-'))
                {
                    var parentLangCode = languageCode.Split('-').GetValue(0).ToString();
                    if (AllLanguages.ContainsKey(parentLangCode))
                        FallbackLangCodes.Add(parentLangCode);
                }
            }
            // OS言語コードの追加
            var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
            if (!FallbackLangCodes.Contains(culture.Name) && AllLanguages.ContainsKey(culture.Name))
                FallbackLangCodes.Add(culture.Name);
            if (culture.Name != culture.Parent.Name)
                if (!FallbackLangCodes.Contains(culture.Parent.Name) && AllLanguages.ContainsKey(culture.Parent.Name))
                    FallbackLangCodes.Add(culture.Parent.Name);
            // 英語(default)の追加
            if (!FallbackLangCodes.Contains("en") && AllLanguages.ContainsKey("en"))
                FallbackLangCodes.Add("en");
        }

        private static string GetTranslation(object obj)
        {
            if (obj == null) return string.Empty;
            var type = obj.GetType();
            var key = type == typeof(string) ? obj.ToString() : $"{type.ToString().ToLower()}.{obj}";
            foreach (var langCode in FallbackLangCodes)
            {
                if (!AllLanguages.TryGetValue(langCode, out var lang)) continue;
                if (lang.Translations.TryGetValue(key, out var text))
                {
                    return text;
                }
            }
            return $"{key}(no translation)";
        }

        internal static string GetText(object obj, params object[] args)
        {
            var format = GetTranslation(obj);
            return string.IsNullOrEmpty(format) ? string.Empty : string.Format(format, args);
        }

        internal static string _L(object obj, params object[] args)
        {
            return GetText(obj, args);
        }
    }
    
    public class Language
    {
        public string LanguageName { get; set; }
        public Dictionary<string, string> Translations { get; set; }
    }
}