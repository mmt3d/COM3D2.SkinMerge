using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    using FU = FileUtils;
    using static Localization;

    internal class ConfigManager
    {
        private static ConfigManager _instance;
        internal static ConfigManager Instance => _instance ??= new ConfigManager();

        internal string SavePath => Path.Combine(UTY.gameProjectPath.Replace("/", @"\"), SaveDir.Value);
        internal Dictionary<MPN, string> DelMenuMap;

        private ConfigFile _config;

        internal static readonly List<MPN> PrimaryMpn = new List<MPN> {
            MPN.hokuro, MPN.lip, MPN.facegloss, MPN.nose, MPN.acctatoo, MPN.accnail, MPN.chikubicolor
        };
        internal static readonly List<MPN> OrderMpn = new List<MPN> {
            MPN.acctatoo, MPN.accnail, MPN.chikubicolor, MPN.hokuro, MPN.lip, MPN.facegloss, MPN.nose, MPN.null_mpn
        };
        
        [Flags]
        internal enum SrcFilter
        {
            acctatoo = 1,
            accnail = 2,
            chikubicolor = 4,
            hokuro = 8,
            facegloss = 16,
            lip = 32,
            nose = 64,
            null_mpn = 128
        }
        
        private readonly Dictionary<string, string> _nameStyleMap = new Dictionary<string, string>
        {
            { nameof(NameStyle.Jp), "cfg.name_style.last_first" },
            { nameof(NameStyle.En), "cfg.name_style.first_last" }
        };

        private readonly List<KeyValuePair<int, string>> _resolutionMap = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(0, "cfg.resolution.as_is"),
            new KeyValuePair<int, string>(512, "cfg.resolution.512"),
            new KeyValuePair<int, string>(1024, "cfg.resolution.1024"),
            new KeyValuePair<int, string>(2048, "cfg.resolution.2048"),
            new KeyValuePair<int, string>(4096, "cfg.resolution.4096")
        };

        internal ConfigEntry<int> GUIIconSize;
        internal ConfigEntry<int> GUIIconsPerRow;
        internal ConfigEntry<int> GUISkinSize;
        internal ConfigEntry<float> MenuPriority;
        internal ConfigEntry<string> SaveDir;
        internal ConfigEntry<int> MainMaxSize;
        internal ConfigEntry<int> OutlineMaxSize;
        internal ConfigEntry<KeyboardShortcut> ShortCutKey;
        internal ConfigEntry<SrcFilter> SourceFilter;
        internal ConfigEntry<string> NippleDelMenuName;
        internal ConfigEntry<string> SaveNameStyle;
        internal ConfigEntry<string> SaveFilePrefix;
        internal ConfigEntry<bool> AutoMaidLoaderRefresh = new ConfigEntry<bool>(false);
        internal ConfigEntry<string> Language;
        internal ConfigEntry<int> SaveIconSize;

        internal void Init(ConfigFile config)
        {
            _config = config;

            var defaultLanguage = Product.isJapan ? "ja" : "en";

            // General
            const string section1 = "General";
            Language = _config.Bind(
                section1,
                "language",
                defaultLanguage,
                _L("cfg.language.desc"),
                new AcceptableValueList<string>(GetLanguageCodes()));
            SetLanguage(Language.Value);
            ShortCutKey = _config.Bind(
                section1,
                "shortcut_key",
                new KeyboardShortcut(KeyCode.E, KeyCode.LeftControl),
                _L("cfg.shortcut_key.desc"));
            GUIIconsPerRow = _config.Bind(
                section1, 
                "icons_per_row",
                5,
                _L("cfg.icons_per_row.desc"),
                new AcceptableValueRange<int>(3, 12));
            GUIIconSize = _config.Bind(
                section1,
                "icon_size",
                90,
                _L("cfg.icon_size.desc"),
                new AcceptableValueRange<int>(50, 120));
            GUISkinSize = _config.Bind(
                section1,
                "preview_size",
                400,
                _L("cfg.preview_size.desc"),
                new AcceptableValueRange<int>(300, 1000));
            SourceFilter = _config.Bind(
                section1,
                "target_category",
                SrcFilter.acctatoo | SrcFilter.hokuro,
                _L("cfg.target_category.desc"),
                new AcceptableValueList<SrcFilter>((SrcFilter[])Enum.GetValues(typeof(SrcFilter))));
            NippleDelMenuName = _config.Bind(
                section1,
                "del_nipple_menu",
                "",
                _L("cfg.del_nipple_menu.desc"));

            // Save options
            const string section2 = "Save Options";
            SaveDir = _config.Bind(
                section2,
                "save_directory",
                @"Mod\SkinMerge",
                _L("cfg.save_directory.desc"));
            MenuPriority = _config.Bind(
                section2,
                "menu_priority",
                99999f,
                _L("cfg.menu_priority.desc"));
            SaveNameStyle = _config.Bind(
                section2,
                "maid_name_style",
                nameof(NameStyle.Jp),
                _L("cfg.maid_name_style.desc"),
                new AcceptableValueList<string>(_nameStyleMap.Keys.ToArray()));
            SaveFilePrefix = _config.Bind(
                section2,
                "name_prefix",
                "SkinMerge_",
                _L("cfg.name_prefix.desc"));
            MainMaxSize = _config.Bind(
                section2,
                "main_resolution",
                0,
                _L("cfg.main_resolution.desc"),
                new AcceptableValueList<int>(_resolutionMap.Select(x => x.Key).ToArray()));
            OutlineMaxSize = _config.Bind(
                section2,
                "outline_resolution",
                0,
                _L("cfg.outline_resolution.desc"),
                new AcceptableValueList<int>(_resolutionMap.Select(x => x.Key).ToArray()));
            SaveIconSize = _config.Bind(
                section2,
                "icon_resolution",
                160,
                _L("cfg.icon_resolution.desc"),
                new AcceptableValueRange<int>(80, 300));

            UpdateDelMenuMap();
        }

        /// <summary>
        /// カテゴリ別削除メニュー辞書を更新する
        /// </summary>
        private void UpdateDelMenuMap()
        {
            DelMenuMap = new Dictionary<MPN, string>
            {
                { MPN.lip, "_i_lip_del.menu" },
                { MPN.nose, "nose_del_i_.menu" },
                { MPN.facegloss, "facegloss_del_i_.menu" },
                { MPN.chikubi, NippleDelMenuName.Value },
                { MPN.chikubicolor, string.Empty }
            };
        }

        /// <summary>
        /// エディットシーン用初期化
        /// </summary>
        internal void InitForEdit()
        {
            SearchDelNippleMenu();
        }

        /// <summary>
        /// 指定メニューが削除メニュー扱いかどうかを返す
        /// </summary>
        internal bool ContainsDelMenu(string menuFileName)
        {
            return DelMenuMap.Values.Contains(menuFileName) ||
                   SearchDelNippleMenu().Contains(menuFileName);
        }

        /// <summary>
        /// 乳首削除メニュー候補を検索する
        /// </summary>
        private static List<string> _delNippleMenuCache;
        private static List<string> SearchDelNippleMenu()
        {
            if (_delNippleMenuCache != null)
                return _delNippleMenuCache;
            var body = SceneEdit.Instance.CategoryList.Find(x => x.m_eCategory == SceneEditInfo.EMenuCategory.身体);
            var nippleMenus = body.m_listPartsType.Find(x => x.m_mpn == MPN.chikubi).m_listMenu;
            var delMenus = nippleMenus.Where(x => x.m_strCateName == "chikubi" && x.m_eColorSetMPN == MPN.null_mpn);
            var validDelMenus = new List<string>();
            foreach (var menu in delMenus)
            {
                var mi = FU.LoadMenu(menu.m_strMenuFileName);
                if (!mi.DelItems.Contains("chikubi")) continue;
                validDelMenus.Add(mi.FileName);
            }
            _delNippleMenuCache = validDelMenus;
            return validDelMenus;
        }
    }

    internal static class ConfigManagerExtensions
    {
        /// <summary>
        /// 指定MPNがフィルターに含まれているか返すExtensionメソッド
        /// </summary>
        internal static bool Contains(this ConfigEntry<ConfigManager.SrcFilter> entry, MPN mpn)
        {
            var flag = (ConfigManager.SrcFilter)Enum.Parse(typeof(ConfigManager.SrcFilter), mpn.ToString());
            return (entry.Value & flag) != 0;
        }
    }
    
    /// <summary>
    /// BepInEx.Configuration.ConfigEntry の代替クラス
    /// </summary>
    internal class ConfigEntry<T>
    {
        internal ConfigEntry(T defaultValue)
        {
            Value = defaultValue;
        }

        internal T Value { get; private set; }
    }

    /// <summary>
    /// BepInEx.Configuration.ConfigFile の代替クラス
    /// </summary>
    internal class ConfigFile
    {
        private readonly IniFile _ini;

        internal ConfigFile(string pluginName)
        {
            _ini = new IniFile(Path.Combine(Paths.ConfigPath, pluginName + ".cfg"));
        }

        internal ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description, object acceptableValues = null)
        {
            var str = _ini.Get(section, key, defaultValue.ToString());
            T value;
            try
            {
                if (typeof(T) == typeof(KeyboardShortcut))
                    value = (T)((object)KeyboardShortcut.Parse(str) ?? defaultValue);
                else if (typeof(T).IsEnum)
                    value = (T)Enum.Parse(typeof(T), str, true);
                else
                    value = (T)Convert.ChangeType(str, typeof(T));

                if (acceptableValues != null)
                {
                    var isValid = acceptableValues switch
                    {
                        AcceptableValueRange<T> range => range.IsValid(value),
                        AcceptableValueList<T> list => list.IsValid(value),
                        _ => true
                    };
                    if (!isValid)
                        value = defaultValue;
                }
            }
            catch
            {
                value = defaultValue;
            }

            // コメントを生成
            var comments = new List<string>
            {
                $"## {description}",
                $"# Setting type: {typeof(T).Name}"
            };

            if (defaultValue is KeyboardShortcut ks)
                comments.Add($"# Default value: {ks}");
            else
                comments.Add($"# Default value: {defaultValue}");
            
            switch (acceptableValues)
            {
                case AcceptableValueRange<T> range:
                    comments.Add($"# Acceptable value range: From {range.MinValue} to {range.MaxValue}");
                    break;
                case AcceptableValueList<T> list:
                    comments.Add($"# Acceptable values: {string.Join(", ", list.AcceptableValues.Select(x => x.ToString()).ToArray())}");
                    break;
            }

            // ini に書き戻す
            _ini.Set(section, key, value.ToString(), comments);
            _ini.Save();

            return new ConfigEntry<T>(value);
        }
    }

    /// <summary>
    /// BepInEx.Configuration.AcceptableValueRange の代替クラス
    /// </summary>
    internal class AcceptableValueRange<T>
    {
        internal T MinValue { get; }
        internal T MaxValue { get; }

        internal AcceptableValueRange(T min, T max)
        {
            MinValue = min;
            MaxValue = max;
        }

        internal bool IsValid(T value)
        {
            try
            {
                var comp = Comparer<T>.Default;
                return comp.Compare(value, MinValue) >= 0 && comp.Compare(value, MaxValue) <= 0;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>
    /// BepInEx.Configuration.AcceptableValueList の代替クラス
    /// </summary>
    internal class AcceptableValueList<T>
    {
        internal List<T> AcceptableValues { get; }

        internal AcceptableValueList(params T[] values)
        {
            AcceptableValues = new List<T>(values);
        }

        internal bool IsValid(T value)
        {
            if (value == null) return false;

            var t = typeof(T);
            // Flags 属性付き enum の場合はビット集合として判定
            if (t.IsEnum && Attribute.IsDefined(t, typeof(FlagsAttribute)))
            {
                var union = AcceptableValues.Aggregate<T, long>(0, (current, a) => current | Convert.ToInt64(a));
                var v = Convert.ToInt64(value);
                // v のビットが union に含まれていれば有効（余分なビットがない）
                return (v & ~union) == 0;
            }

            // それ以外は通常の包含判定
            return AcceptableValues.Contains(value);
        }
    }

    /// <summary>
    /// BepInEx.Configuration.KeyboardShortcut の代替クラス
    /// </summary>
    internal class KeyboardShortcut
    {
        private KeyCode MainKey { get; }
        private KeyCode[] Modifiers { get; }

        internal KeyboardShortcut(KeyCode mainKey, params KeyCode[] modifiers)
        {
            MainKey = mainKey;
            Modifiers = modifiers ?? new KeyCode[0];
        }

        internal bool IsDown()
        {
            return Input.GetKeyDown(MainKey) && Modifiers.All(Input.GetKey);
        }

        public override string ToString()
        {
            return Modifiers.Length == 0 ?
                MainKey.ToString() :
                $"{MainKey} + {string.Join(" + ", Modifiers.Select(x => x.ToString()).ToArray())}";
        }
        
        internal static KeyboardShortcut Parse(string str)
        {
            var parts = str.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            // 最初の要素を MainKey、それ以降を Modifiers とする
            var main = (KeyCode)Enum.Parse(typeof(KeyCode), parts[0], true);
            var mods = parts.Skip(1)
                .Select(p => (KeyCode)Enum.Parse(typeof(KeyCode), p, true))
                .ToArray();

            return new KeyboardShortcut(main, mods);
        }
    }

    /// <summary>
    /// 簡易 INI ファイル読み書きクラス
    /// BepInEx.Configuration.ConfigFile の代替実装
    /// </summary>
    internal class IniFile
    {
        private readonly string _path;
        private readonly Dictionary<string, Dictionary<string, string>> _data
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Dictionary<string, List<string>>> _comments
            = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);

        internal IniFile(string path)
        {
            _path = path;
            if (File.Exists(_path))
                Load();
            else
                Save();
        }

        internal string Get(string section, string key, string defaultValue = "")
        {
            if (_data.TryGetValue(section, out var sec) && sec.TryGetValue(key, out var val))
                return val;
            Set(section, key, defaultValue);
            Save();
            return defaultValue;
        }

        internal void Set(string section, string key, string value, List<string> comments = null)
        {
            if (!_data.ContainsKey(section))
                _data[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _data[section][key] = value;

            if (comments == null) return;
            if (!_comments.ContainsKey(section))
                _comments[section] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            _comments[section][key] = comments;
        }

        internal void Save()
        {
            using var sw = new StreamWriter(_path);
            foreach (var section in _data)
            {
                sw.WriteLine($"[{section.Key}]\n");
                foreach (var kv in section.Value)
                {
                    if (_comments.TryGetValue(section.Key, out var secComments) &&
                        secComments.TryGetValue(kv.Key, out var lines))
                    {
                        foreach (var c in lines)
                            sw.WriteLine(c);
                    }
                    sw.WriteLine($"{kv.Key} = {kv.Value}");
                    sw.WriteLine();
                }
            }
        }

        private void Load()
        {
            var currentSection = "";
            var pendingComments = new List<string>();

            foreach (var line in File.ReadAllLines(_path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed.StartsWith("#"))
                {
                    pendingComments.Add(trimmed);
                    continue;
                }

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    if (!_data.ContainsKey(currentSection))
                        _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (!_comments.ContainsKey(currentSection))
                        _comments[currentSection] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                var parts = trimmed.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();
                    Set(currentSection, key, val, pendingComments.Count > 0 ? new List<string>(pendingComments) : null);
                    pendingComments.Clear();
                }
            }
        }
    }
}