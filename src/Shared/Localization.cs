using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace COM3D2.SkinMerge
{
    using SM = SkinMerge;
    using static PluginInfo;
    
    internal static class Localization
    {
        private static readonly Dictionary<string, Language> AllLanguages = new Dictionary<string, Language>();
        private static readonly List<string> FallbackLangCodes = new List<string>();
        private static string LocConfigPath => Path.Combine(SM.ConfigPath, PluginName);

        static Localization()
        {
            Setup();
            Init();
        }

        /// <summary>
        /// 多言語設定のセットアップ
        /// 存在していなければ同梱の翻訳ファイルをConfigフォルダにコピーする
        /// </summary>
        private static void Setup()
        {
            if (!Directory.Exists(LocConfigPath))
                Directory.CreateDirectory(LocConfigPath);
            SM.ListResourceNames(@"localization\").ForEach(x =>
            {
                var filePath = Path.Combine(LocConfigPath, Path.GetFileName(x));
                if (File.Exists(filePath)) return;
                using var writer = new BinaryWriter(File.OpenWrite(filePath));
                writer.Write(SM.GetResourceBytes(x));
            });
        }

        /// <summary>
        /// 翻訳設定の初期化
        /// Configフォルダ内の翻訳ファイルをロードする
        /// </summary>
        private static void Init()
        {
            var translationFiles = Directory.GetFiles(LocConfigPath, "*.json");
            foreach (var file in translationFiles)
            {
                var langCode = Path.GetFileNameWithoutExtension(file);
                var lang = JsonConvert.DeserializeObject<Language>(File.ReadAllText(Path.Combine(LocConfigPath, file)));
                AllLanguages[langCode] = lang;
            }

            SetLanguage();
        }
        
        /// <summary>
        /// 利用可能な言語コード一覧を取得する
        /// </summary>
        internal static string[] GetLanguageCodes()
        {
            return AllLanguages.Keys.OrderBy(x => x).ToArray();
        }
        
        /// <summary>
        /// 言語名辞書を取得する
        /// </summary>
        internal static Dictionary<string, string> GetLanguageNamesMap()
        {
            return AllLanguages.ToDictionary(x => x.Key, x => x.Value.LanguageName);
        }

        /// <summary>
        /// 利用言語コードの設定
        /// </summary>
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

        /// <summary>
        /// 翻訳テキスト(format)を取得する
        /// objectの場合は「型名.オブジェクト名」を翻訳キーとする
        /// </summary>
        /// <param name="obj">翻訳キー文字列またはオブジェクト</param>
        private static string GetTranslation(object obj)
        {
            if (obj == null) return string.Empty;
            var type = obj.GetType();
            var key = type == typeof(string) ? obj.ToString() : $"{type.ToString().ToLowerInvariant()}.{obj}";
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

        /// <summary>
        /// 翻訳テキストを取得する
        /// 引数はstring.Formatの引数として使用する
        /// </summary>
        internal static string GetText(object obj, params object[] args)
        {
            var format = GetTranslation(obj);
            return string.IsNullOrEmpty(format) ? string.Empty : string.Format(format, args);
        }

        /// <summary>
        /// 簡易翻訳テキスト取得関数
        /// </summary>
        internal static string _L(object obj, params object[] args)
        {
            return GetText(obj, args);
        }
    }
    
    /// <summary>
    /// 翻訳設定情報クラス
    /// </summary>
    public class Language
    {
        public string LanguageName { get; set; }
        public Dictionary<string, string> Translations { get; set; }
    }
}