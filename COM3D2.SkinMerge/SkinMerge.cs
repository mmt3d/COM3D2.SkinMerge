using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using COM3D2API;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COM3D2.SkinMerge
{
    using GU = GraphicUtils;

    [BepInPlugin(PluginName, PluginName, PluginVersion)]
    [BepInDependency("COM3D2.MaidLoader", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.bepis.bepinex.configurationmanager")]

    public class SkinMerge : BaseUnityPlugin
    {
        public const string PluginTarget = "COM3D2.";
        public const string PluginName = "SkinMerge";
        public const string PluginFullName = PluginTarget + PluginName;
        public const string PluginCopyright = "Copyright © mmt3d 2025";
        public const string PluginVersion = "1.0.1.0";
        internal const string PluginTitleName = PluginName + " " + PluginVersion;

        private static SkinMerge _instance;
        internal static SkinMerge Instance => _instance ??= FindObjectOfType<SkinMerge>();
        private static ConfigManager Cm => ConfigManager.Instance;
        private static WindowManager Wm => WindowManager.Instance;
        private static DialogManager Dm => DialogManager.Instance;
        private static SceneEdit SceneEdit => SceneEdit.Instance;
        internal static ManualLogSource Log => Instance?.Logger;
        
        internal bool IsDeletingTattoo = false;
        internal int CurrentScene;

        internal MergeContext MergeContext;
        internal readonly MergeContextMap MergeContexts = new MergeContextMap();
        internal SequentialTaskRunner TaskRunner;
        private bool _guiOpen;
        internal bool EnableHook;
        internal bool HasMaidLoader;
        private Harmony _harmony;
        internal static Harmony HarmonyInstance => Instance?._harmony;
        
        public void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(this);
            GU.LoadAssetBundle("asset_bundle");
            _harmony = Harmony.CreateAndPatchAll(typeof(HarmonyPatches));
        }

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TaskRunner = gameObject.AddComponent<SequentialTaskRunner>();
            HasMaidLoader = Chainloader.PluginInfos.ContainsKey("COM3D2.MaidLoader");
            
            // Gear icon 
            var gearIconPng = GetResourceBytes("gear_icon.png");
            SystemShortcutAPI.AddButton(PluginName, ToggleGUI, PluginName, gearIconPng);
            
            // Initialize
            Cm.Init(Config);
            Wm.Init();
            GU.Init();
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            CurrentScene = scene.buildIndex;
            if (CurrentScene == 5)
                EnableHook = true;
            else
            {
                EnableHook = false;
                MergeContexts.Clear();
            }
        }

        public void Update()
        {
            if (CurrentScene != 5) // not EditScene
            {
                if (_guiOpen) _guiOpen = false;
                return;
            }

            if (Cm.ShortCutKey.Value.IsDown())
                ToggleGUI();

            if (_guiOpen && MergeContext != null && MergeContext.MaidGuid != SceneEdit.maid.status.guid)
                MergeContext = MergeContexts.Setup(SceneEdit.maid);
        }

        public void OnGUI()
        {
            if (_guiOpen) Wm.OnGUI();
            Dm.OnGUI();
        }

        /// <summary>
        /// メインGUIの表示/非表示を切り替える
        /// </summary>
        internal void ToggleGUI()
        {
            if (CurrentScene != 5) return; // not EditScene
            _guiOpen = !_guiOpen;
            if (!_guiOpen) return;
            MergeContext = MergeContexts.Setup(SceneEdit.maid);
            MergeContext.LoadSources();
        }

        /// <summary>
        /// 非同期タスクを並列数1で順次実行するランナー
        /// </summary>
        internal class SequentialTaskRunner : MonoBehaviour
        {
            private readonly Queue<IEnumerator> _queue = new Queue<IEnumerator>();
            private bool _isRunning;

            internal void Add(IEnumerator task)
            {
                _queue.Enqueue(task);
                if (!_isRunning)
                    StartCoroutine(RunTasks());
            }

            private IEnumerator RunTasks()
            {
                _isRunning = true;
                while (_queue.Count > 0)
                {
                    try
                    {
                        StartCoroutine(_queue.Dequeue());
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"非同期タスク実行中にエラーが発生しました: {ex.Message}\n{ex.StackTrace}");
                    }

                    yield return null;
                }

                _isRunning = false;
            }
        }

        /// <summary>
        /// 同梱埋め込みリソースをバイト配列として取得する
        /// </summary>
        internal static byte[] GetResourceBytes(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Log.LogError($"Resource '{resourceName}' not found.");
                throw new FileNotFoundException($"Resource '{resourceName}' not found.");
            }
            var bytes = new byte[stream.Length];
            _ = stream.Read(bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>
        /// 同梱埋め込みリソース名の一覧を取得する
        /// </summary>
        internal static List<string> ListResourceNames(string prefix = "")
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames().ToList()
                .Where(x => x.StartsWith(prefix)).ToList();
        }

        /// <summary>
        /// MaidLoader のMODリフレッシュをリフレクション経由で実行する
        /// </summary>
        internal bool MaidLoaderRefresh()
        {
            if (!HasMaidLoader) return false;
            
            if (Chainloader.PluginInfos.TryGetValue("COM3D2.MaidLoader", out var pluginInfo))
            {
                var maidLoaderAsm = pluginInfo.Instance.GetType().Assembly;
                var maidLoaderType = maidLoaderAsm.GetType("COM3D2.MaidLoader.MaidLoader");
                var refreshModField = maidLoaderType?.GetField("refreshMod", BindingFlags.Public | BindingFlags.Static);
                var refreshModInstance = refreshModField?.GetValue(null);
                var refreshCoMethod = refreshModInstance?.GetType().GetMethod("RefreshCo", BindingFlags.Public | BindingFlags.Instance);
                var coroutine = refreshCoMethod?.Invoke(refreshModInstance, null);
                if (coroutine is IEnumerator enumerator)
                {
                    TaskRunner.Add(enumerator);
                    return true;
                }
                Logger.LogWarning($"RefreshCo() returned unexpected type: {coroutine?.GetType().FullName ?? "null"}");
            }
            else
            {
                Logger.LogInfo("MaidLoader not installed. Skipping refresh.");
            }
            return false;
        }
    }
}