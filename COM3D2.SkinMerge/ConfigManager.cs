using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    using FU = FileUtils;
    using GU = GraphicUtils;
    using GS = GuiStyles;
    using ConfigurationManager;
    using static Localization;

    internal class ConfigManager
    {
        private static ConfigManager _instance;
        internal static ConfigManager Instance => _instance ??= new ConfigManager();
        private static SkinMerge Sm => SkinMerge.Instance;
        private static MergeContext Ctx => Sm.MergeContext;
        private static DialogManager Dm => DialogManager.Instance;

        internal string SavePath => Path.Combine(UTY.gameProjectPath.Replace("/", @"\"), SaveDir.Value);
        internal Dictionary<MPN, string> DelMenuMap;

        private const string GenerateDelNippleMenuName = "skinmerge_del_nipple.menu";
        private const string GenerateDelNippleLabel = GenerateDelNippleMenuName + " (Generate)";
        private ConfigFile _config;

        internal static readonly List<MPN> PrimaryMpn = new List<MPN> {
            MPN.hokuro, MPN.lip, MPN.facegloss, MPN.nose, MPN.acctatoo, MPN.accnail, MPN.chikubicolor
        };
        internal static readonly List<MPN> OrderMpn = new List<MPN> {
            MPN.acctatoo, MPN.accnail, MPN.chikubicolor, MPN.hokuro, MPN.lip, MPN.facegloss, MPN.nose, MPN.null_mpn
        };

        internal static ConfigurationManager ConfigurationManagerInstance { get; set; }
        private static int ColWidth => GetColWidth();
        
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
        internal ConfigEntry<bool> AutoMaidLoaderRefresh;
        internal ConfigEntry<string> Language;
        private ConfigEntry<string> _languageForPreBind;
        internal ConfigEntry<int> SaveIconSize;

        internal void Init(ConfigFile config)
        {
            _config = config;

            var defaultLanguage = Product.isJapan ? "ja" : "en";
            SetLanguage(defaultLanguage);  // for configFile generation

            // Hidden parameters
            _languageForPreBind = _config.Bind("_Hidden Parameters", "language_for_pre_bind", defaultLanguage,
                new ConfigDescription(
                    _L("cfg.language_for_pre_bind.desc"),
                    null, new ConfigurationManagerAttributes { Browsable = false }));
            SetLanguage(_languageForPreBind.Value);  // for configuration GUI
            
            // General
            const string section1 = "General";
            var sectionT1 = _L("cfg.general");
            Language = _config.Bind(
                section1,
                "language",
                defaultLanguage,
                new ConfigDescription(
                    _L("cfg.language.desc"),
                    new AcceptableValueList<string>(GetLanguageCodes()),
                    new ConfigurationManagerAttributes {
                        Order = -1,
                        Category = sectionT1,
                        DispName = _L("cfg.language"),
                        CustomDrawer = LanguageDrawer
                    }));
            ShortCutKey = _config.Bind(
                section1,
                "shortcut_key",
                new KeyboardShortcut(KeyCode.E, KeyCode.LeftControl),
                new ConfigDescription(
                    _L("cfg.shortcut_key.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -2,
                        Category = sectionT1,
                        DispName = _L("cfg.shortcut_key")
                    }));
            GUIIconsPerRow = _config.Bind(
                section1, 
                "icons_per_row",
                5,
                new ConfigDescription(
                    _L("cfg.icons_per_row.desc"),
                    new AcceptableValueRange<int>(3, 12),
                    new ConfigurationManagerAttributes {
                        Order = -3,
                        Category = sectionT1,
                        DispName = _L("cfg.icons_per_row"),
                        IsAdvanced = true
                    }));
            GUIIconSize = _config.Bind(
                section1,
                "icon_size",
                90,
                new ConfigDescription(
                    _L("cfg.icon_size.desc"),
                    new AcceptableValueRange<int>(50, 120),
                    new ConfigurationManagerAttributes {
                        Order = -4,
                        Category = sectionT1,
                        DispName = _L("cfg.icon_size"),
                        IsAdvanced = true
                    }));
            GUISkinSize = _config.Bind(
                section1,
                "preview_size",
                400,
                new ConfigDescription(
                    _L("cfg.preview_size.desc"),
                    new AcceptableValueRange<int>(300, 1000),
                    new ConfigurationManagerAttributes {
                        Order = -5,
                        Category = sectionT1,
                        DispName = _L("cfg.preview_size"),
                        IsAdvanced = true
                    }));
            SourceFilter = _config.Bind(
                section1,
                "target_category",
                SrcFilter.acctatoo | SrcFilter.hokuro,
                new ConfigDescription(
                    _L("cfg.target_category.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -6,
                        Category = sectionT1,
                        DispName = _L("cfg.target_category"),
                        CustomDrawer = SourceFilterDrawer,
                        IsAdvanced = true }));
            NippleDelMenuName = _config.Bind(
                section1,
                "del_nipple_menu",
                "",
                new ConfigDescription(
                    _L("cfg.del_nipple_menu.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -7,
                        Category = sectionT1,
                        DispName = _L("cfg.del_nipple_menu"),
                        IsAdvanced = true,
                        CustomDrawer = NippleDelMenuNameDrawer
                    }));

            // Save options
            const string section2 = "Save Options";
            var sectionT2 = _L("cfg.save_options");
            SaveDir = _config.Bind(
                section2,
                "save_directory",
                @"Mod\SkinMerge",
                new ConfigDescription(
                    _L("cfg.save_directory.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -1,
                        Category = sectionT2,
                        DispName = _L("cfg.save_directory")
                    }));
            MenuPriority = _config.Bind(
                section2,
                "menu_priority",
                99999f,
                new ConfigDescription(
                    _L("cfg.menu_priority.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -2,
                        Category = sectionT2,
                        DispName = _L("cfg.menu_priority"),
                        CustomDrawer = MenuPriorityDrawer
                    }));
            SaveNameStyle = _config.Bind(
                section2,
                "maid_name_style",
                nameof(NameStyle.Jp),
                new ConfigDescription(
                    _L("cfg.maid_name_style.desc"),
                    new AcceptableValueList<string>(_nameStyleMap.Keys.ToArray()),
                    new ConfigurationManagerAttributes {
                        Order = -3,
                        Category = sectionT2,
                        DispName = _L("cfg.maid_name_style"),
                        IsAdvanced = true,
                        CustomDrawer = SaveNameStyleDrawer
                    }));
            SaveFilePrefix = _config.Bind(
                section2,
                "name_prefix",
                "SkinMerge_",
                new ConfigDescription(
                    _L("cfg.name_prefix.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -4,
                        Category = sectionT2,
                        DispName = _L("cfg.name_prefix"),
                        IsAdvanced = true
                    }));
            MainMaxSize = _config.Bind(
                section2,
                "main_resolution",
                0,
                new ConfigDescription(
                    _L("cfg.main_resolution.desc"),
                    new AcceptableValueList<int>(_resolutionMap.Select(x => x.Key).ToArray()),
                    new ConfigurationManagerAttributes {
                        Order = -5,
                        Category = sectionT2,
                        DispName = _L("cfg.main_resolution"),
                        IsAdvanced = true,
                        CustomDrawer = ResolutionDrawer
                    }));
            OutlineMaxSize = _config.Bind(
                section2,
                "outline_resolution",
                0,
                new ConfigDescription(
                    _L("cfg.outline_resolution.desc"),
                    new AcceptableValueList<int>(_resolutionMap.Select(x => x.Key).ToArray()),
                    new ConfigurationManagerAttributes {
                        Order = -6,
                        Category = sectionT2,
                        DispName = _L("cfg.outline_resolution"),
                        IsAdvanced = true,
                        CustomDrawer = ResolutionDrawer
                    }));
            SaveIconSize = _config.Bind(
                section2,
                "icon_resolution",
                160,
                new ConfigDescription(
                    _L("cfg.icon_resolution.desc"),
                    new AcceptableValueRange<int>(80, 300),
                    new ConfigurationManagerAttributes {
                        Order = -7,
                        Category = sectionT2,
                        DispName = _L("cfg.icon_resolution"),
                        IsAdvanced = true
                    }));
            AutoMaidLoaderRefresh = _config.Bind(
                section2,
                "auto_refresh",
                true,
                new ConfigDescription(
                    _L("cfg.auto_refresh.desc"),
                    null,
                    new ConfigurationManagerAttributes {
                        Order = -8,
                        Category = sectionT2,
                        DispName = _L("cfg.auto_refresh"),
                        CustomDrawer = AutoMaidLoaderRefreshDrawer
                    }));

            UpdateDelMenuMap();
        }

        private static void MenuPriorityDrawer(ConfigEntryBase entry)
        {
            if (!(entry is ConfigEntry<float> configEntry)) return;
            using (new GUILayout.HorizontalScope())
            {
                var newValue = GUILayout.TextField(configEntry.Value.ToCompactString(), GUILayout.Width(ColWidth));
                if (float.TryParse(newValue, out var parsedValue))
                    configEntry.Value = parsedValue;
            }
        }

        private void LanguageDrawer(ConfigEntryBase entry)
        {
            if (!(entry is ConfigEntry<string> configEntry)) return;
            var map = GetLanguageNamesMap();
            var keys = map.Keys.ToList();
            var values = map.Values.ToList();
            DrawToggles(entry, keys, values, idx =>
            {
                _languageForPreBind.Value = configEntry.Value;
                SetLanguage(configEntry.Value);
                Dm.ShowDialog(_L("cfg.language.changed"));
            });
        }
        
        private void ResolutionDrawer(ConfigEntryBase entry)
        {
            var keys = _resolutionMap.Select(x => x.Key).ToList();
            var values = _resolutionMap.Select(x => x.Value).Select(x => _L(x)).ToList();
            DrawToggles(entry, keys, values);
        }
        
        private void SaveNameStyleDrawer(ConfigEntryBase entry)
        {
            var keys = _nameStyleMap.Keys.ToList();
            var values = _nameStyleMap.Values.Select(x => _L(x)).ToList();
            DrawToggles(entry, keys, values);
        }
        
        private static void AutoMaidLoaderRefreshDrawer(ConfigEntryBase entry)
        {
            if (!(entry is ConfigEntry<bool> configEntry)) return;
            if (Sm.HasMaidLoader)
                configEntry.Value = GUILayout.Toggle(configEntry.Value, _L("cfg.auto_refresh.enable"));
            else
                GUILayout.Label(_L("cfg.auto_refresh.unavailable"), GS.ConfigLabel);
        }
        
        private static void SourceFilterDrawer(ConfigEntryBase entry)
        {
            var keys = Enum.GetValues(typeof(SrcFilter)).Cast<SrcFilter>().ToList();
            var values = keys.Select(x => _L((MPN)Enum.Parse(typeof(MPN), x.ToString()))).ToList();
            DrawToggles(entry, keys, values, _ => Ctx.LoadSources());
        }
        
        private void NippleDelMenuNameDrawer(ConfigEntryBase entry)
        {
            if (Sm.CurrentScene != 5)
            {
                GUILayout.Label(_L("cfg.del_nipple_menu.unavailable"));
                return;
            }

            var found = SearchDelNippleMenu();
            var keys = found.Select(x => x.Key).ToList();
            var values =  found.Select(x => x.Value).ToList();
            DrawToggles(entry, keys, values, idx =>
            {
                if (keys[idx] == GenerateDelNippleLabel)
                    Dm.ShowDialog(_L("cfg.del_nipple_menu.confirmation"),
                        GenerateDelNippleMenu, () => NippleDelMenuName.Value = "");
                else
                    UpdateDelMenuMap();
            }, GS.ConfigFancyToggle);
        }
        
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

        private static List<KeyValuePair<string, GUIContent>> _delNippleMenuCache;
        private static List<KeyValuePair<string, GUIContent>> SearchDelNippleMenu()
        {
            if (_delNippleMenuCache != null)
                return _delNippleMenuCache;
            var body = SceneEdit.Instance.CategoryList.Find(x => x.m_eCategory == SceneEditInfo.EMenuCategory.身体);
            var nippleMenus = body.m_listPartsType.Find(x => x.m_mpn == MPN.chikubi).m_listMenu;
            var delMenus = nippleMenus.Where(x => x.m_strCateName == "chikubi" && x.m_eColorSetMPN == MPN.null_mpn);
            var validDelMenus = new List<KeyValuePair<string, GUIContent>>
            {
                new KeyValuePair<string, GUIContent>("", new GUIContent($"   ( {_L("cfg.del_nipple_menu.not_selected")} )"))
            };
            foreach (var menu in delMenus)
            {
                var mi = FU.LoadMenu(menu.m_strMenuFileName);
                if (!mi.DelItems.Contains("chikubi")) continue;
                var icon = FU.LoadTexture(mi.IconName)?.Squared() ?? GU.GetEmbeddedTexture("unknown.png");
                validDelMenus.Add(
                    new KeyValuePair<string, GUIContent>(
                        mi.FileName,
                        new GUIContent(
                            $"{mi.Name}\n {mi.Description}\n {mi.FileName}",
                            icon.Resized(40, 40)
                        )));
            }
            if (validDelMenus.All(x => x.Key != GenerateDelNippleMenuName))
                validDelMenus.Add(
                    new KeyValuePair<string, GUIContent>(
                        GenerateDelNippleLabel,
                        new GUIContent(
                            _L("cfg.del_nipple_menu.generate_new"),
                            GU.GetEmbeddedTexture("unknown.png").Resized(40, 40)
                        )));
            _delNippleMenuCache = validDelMenus;
            return validDelMenus;
        }

        private void GenerateDelNippleMenu()
        {
            FU.GenerateDelMenu(SavePath, GenerateDelNippleMenuName);
            Sm.TaskRunner.Add(WaitForMenuReflected(GenerateDelNippleMenuName));
            if (!AutoMaidLoaderRefresh.Value)
                Dm.ShowDialog(_L("cfg.del_nipple_menu.generated_manual"));
            else
            {
                Sm.TaskRunner.Add(MaidLoader.MaidLoader.refreshMod.RefreshCo());
                Dm.ShowDialog(_L("cfg.del_nipple_menu.generated_auto"));
            }
        }

        private IEnumerator WaitForMenuReflected(string fileName)
        {
            // 生成されたmenuがModLoaderなどで反映されるのを待つ
            while (!SceneEdit.Instance.m_menuRidDic.ContainsKey(fileName.GetRid()))
                yield return new WaitForSeconds(2);
            // 選択肢を更新して、選択状態にする
            NippleDelMenuName.Value = GenerateDelNippleMenuName;
            UpdateDelMenuMap();
            _delNippleMenuCache = null;
            yield return null;
        }
        
        private static void DrawToggles<T1, T2>(ConfigEntryBase entry, List<T1> keys, List<T2> values, Action<int> onChanged = null, GUIStyle gs = null)
        {
            if (!(entry is ConfigEntry<T1> configEntry)) return;
            var isFlags = typeof(T1).IsDefined(typeof(FlagsAttribute), false);
            var maxWidth = ColWidth;
            using (new GUILayout.VerticalScope())
            {
                for (var i = 0; i < keys.Count;)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        var lineWidth = 0f;
                        for (; i < keys.Count; i++)
                        {
                            var key = keys[i];
                            var value = ToGUIContent(values[i]);
                            var toggleWidth = gs != null ? gs.CalcSize(value).x : (int)GUI.skin.toggle.CalcSize(value).x;
                            lineWidth += toggleWidth;
                            if (lineWidth > maxWidth) break;
                            var isChecked = isFlags
                                ? (Convert.ToInt32(configEntry.Value) & Convert.ToInt32(key)) != 0
                                : configEntry.Value.ToString() == key.ToString();
                            var isCheckedNew = GUILayout.Toggle(isChecked, value);
                            if (isChecked == isCheckedNew) continue;
                            if (isFlags)
                                configEntry.Value = (T1)Enum.ToObject(typeof(T1), Convert.ToInt32(configEntry.Value) ^ Convert.ToInt32(key));
                            else
                                configEntry.Value = key;
                            onChanged?.Invoke(i);
                        }
                    }
                }
            }
        }

        private static GUIContent ToGUIContent<T>(T content)
        {
            if (content is GUIContent gc) return gc;
            return new GUIContent(content?.ToString());
        }
        
        private static int GetColWidth()
        {
            try
            {
                return (int)AccessTools.Property(typeof(ConfigurationManager), "RightColumnWidth")
                    .GetValue(ConfigurationManagerInstance, null);
            }
            catch (Exception)
            {
                return 275;
            }
        }
    }

    internal static class ConfigManagerExtensions
    {
        internal static bool Contains(this ConfigEntry<ConfigManager.SrcFilter> entry, MPN mpn)
        {
            var flag = (ConfigManager.SrcFilter)Enum.Parse(typeof(ConfigManager.SrcFilter), mpn.ToString());
            return (entry.Value & flag) != 0;
        }
    }

#pragma warning disable 0169, 0414, 0649
    internal sealed class ConfigurationManagerAttributes
    {
        public bool? ShowRangeAsPercent;
        public Action<ConfigEntryBase> CustomDrawer;
        public CustomHotkeyDrawerFunc CustomHotkeyDrawer;
        public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);
        public bool? Browsable;
        public string Category;
        public object DefaultValue;
        public bool? HideDefaultButton;
        public bool? HideSettingName;
        public string Description;
        public string DispName;
        public int? Order;
        public bool? ReadOnly;
        public bool? IsAdvanced;
        public Func<object, string> ObjToStr;
        public Func<string, object> StrToObj;
    }
}