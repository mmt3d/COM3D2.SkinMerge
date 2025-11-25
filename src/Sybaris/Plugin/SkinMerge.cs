using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityInjector;
using UnityInjector.Attributes;

namespace COM3D2.SkinMerge
{
    using Managed;
    using GU = GraphicUtils;
    using static PluginInfo;

    [PluginFilter("COM3D2x64"),
     PluginFilter("COM3D2OHx64"),
     PluginName(PluginTarget + PluginName),
     PluginVersion(PluginVersion)]

    public class SkinMerge : PluginBase
    {
        private static SkinMerge _instance;
        internal static SkinMerge Instance => _instance ??= FindObjectOfType<SkinMerge>();
        private static ConfigManager Cm => ConfigManager.Instance;
        private static WindowManager Wm => WindowManager.Instance;
        private static DialogManager Dm => DialogManager.Instance;
        private static SceneEdit SceneEdit => SceneEdit.Instance;
        internal static string ConfigPath => Paths.ConfigPath;
        
        internal bool IsDeletingTattoo;
        internal bool NeedsLoadSkin;
        internal bool NeedsLoadSources;
        internal int CurrentScene;

        internal MergeContext MergeContext;
        internal readonly MergeContextMap MergeContexts = new MergeContextMap();
        internal SequentialTaskRunner TaskRunner;
        private bool _guiOpen;
        internal bool EnableHook;
        internal bool HasMaidLoader;
        
        public void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(this);
            GU.LoadAssetBundle("asset_bundle");
            InitHooks();
        }

        public void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TaskRunner = gameObject.AddComponent<SequentialTaskRunner>();
            HasMaidLoader = false;
            
            // Gear icon 
            var gearIconPng = GetResourceBytes("gear_icon.png");
            GUIExtBase.GUIExt.Add(PluginName, PluginName, gearIconPng, go => ToggleGUI());
            
            // Initialize
            Cm.Init(new ConfigFile(PluginName));
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
        /// 非同期タスクエントリクラス
        /// </summary>
        private class TaskEntry
        {
            internal IEnumerator Task { get; }
            internal bool Cancellable { get; }
            internal TaskEntry(IEnumerator task, bool cancellable)
            {
                Task = task;
                Cancellable = cancellable;
            }
        }
        
        /// <summary>
        /// 非同期タスクを並列数1で順次実行するランナー
        /// </summary>
        internal class SequentialTaskRunner : MonoBehaviour
        {
            private readonly Queue<TaskEntry> _queue = new Queue<TaskEntry>();
            private Coroutine _currentCoroutine;
            private bool _isRunning;
            private bool _currentCancellable;

            internal void Add(IEnumerator task, bool cancellable = false)
            {
                if (_isRunning && _currentCancellable && _currentCoroutine != null)
                    StopCoroutine(_currentCoroutine);

                _queue.Enqueue(new TaskEntry(task, cancellable));
                if (!_isRunning)
                    StartCoroutine(RunTasks());
            }

            private IEnumerator RunTasks()
            {
                _isRunning = true;
                while (_queue.Count > 0)
                {
                    var entry = _queue.Dequeue();
                    _currentCancellable = entry.Cancellable;
                    _currentCoroutine = StartCoroutine(entry.Task);
                    yield return _currentCoroutine;
                }
                _isRunning = false;
                _currentCancellable = false;
                _currentCoroutine = null;
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
                Log.Error($"Resource '{resourceName}' not found.");
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
        /// MaidLoader のMODリフレッシュをリフレクション経由で実行する (Sybaris 環境では何もしない)
        /// </summary>
        internal bool MaidLoaderRefresh()
        {
            return false;
        }

        /// <summary>
        /// フックの初期化
        /// Managedで発生したイベントを受け取るメソッドを登録
        /// </summary>
        private void InitHooks()
        {
            SkinMergeManaged.OnMaidSetProp += OnMaidSetPropExecuted;
            SkinMergeManaged.OnMaidSetSubProp += OnMaidSetSubPropExecuted;
            SkinMergeManaged.OnMaidSubPropAlpha += OnMaidSubPropAlphaExecuted;
            SkinMergeManaged.OnTBodyMulTexProc += OnTBodyMulTexProcExecuted;
            SkinMergeManaged.OnUpdateInfinityColor += OnUpdateInfinityColorExecuted;
        }
        
        /// <summary>
        /// メイド装着アイテム変更時のメインGUI反映のためのフック
        /// タトゥー・ほくろ削除時は毎回全削除してからの追加をしておりここでのフックは全削除を意味する
        /// ※ Maid.SetPropのみPreFix扱い
        /// </summary>
        private void OnMaidSetPropExecuted(object sender, OnMaidSetPropEventArgs e)
        {
            if (!EnableHook || !MergeContexts.TryGet(e.Instance, out var ctx)) return;
            if (e.MaidProp.idx == (int)MPN.skin)
                NeedsLoadSkin = true;
            if (e.MaidProp.idx == (int)MPN.acctatoo || e.MaidProp.idx == (int)MPN.hokuro)
                IsDeletingTattoo = true;
            else
                NeedsLoadSources = true;
        }
        
        /// <summary>
        /// タトゥー・ほくろ追加時のメインGUI反映のためのフック
        /// 削除時は毎回全削除してからの追加をしており、その追加では無視してMulTexProc後処理で反映する
        /// </summary>
        private void OnMaidSetSubPropExecuted(object sender, OnMaidSetSubPropEventArgs e)
        {
            if (!EnableHook || !MergeContexts.TryGet(e.Instance, out var ctx)) return;
            if (!IsDeletingTattoo)
                ctx.LoadSources(null, true);
        }
        
        /// <summary>
        /// タトゥー・ほくろの不透明度変更時に変更値を取得するためのフック
        /// </summary>
        private void OnMaidSubPropAlphaExecuted(object sender, OnMaidSubPropAlphaEventArgs e)
        {
            if (!EnableHook || !MergeContexts.TryGet(e.Instance, out var ctx)) return;
            var fileName = ctx.Maid.GetProp(e.Mpn).listSubProp[e.SubNo].strFileName;
            var source = ctx.Sources.Find(x => x.MenuFileName == fileName);
            if (source != null)
                source.MenuAlpha = e.Alpha;
        }
        
        /// <summary>
        /// タトゥー・ほくろ削除時は毎回全削除してからの追加をしておりここでまとめて反映する
        /// Maid.SetPropがPreFix扱いのため肌・ソース両方の読み込み要求を貯めておきここで実行する
        /// </summary>
        private void OnTBodyMulTexProcExecuted(object sender, OnTBodyMulTexProcEventArgs e)
        {
            if (!EnableHook || !MergeContexts.TryGet(e.Instance.maid, out var ctx)) return;
            if (IsDeletingTattoo)
            {
                IsDeletingTattoo = false;
                ctx.LoadSources(null, true);
            }
            if (NeedsLoadSkin)
            {
                NeedsLoadSkin = false;
                ctx.LoadSkin();
            }
            if (NeedsLoadSources)
            {
                NeedsLoadSources = false;
                ctx.LoadSources();
            }
        }
        
        /// <summary>
        /// フリーカラースキンのカラーパレット操作後にメインGUIのテクスチャ更新するためのフック
        /// </summary>
        private void OnUpdateInfinityColorExecuted(object sender, OnUpdateInfinityColorEventArgs e)
        {
            if (!e.Result) return;
            if (e.PartsColor != MaidParts.PARTS_COLOR.SKIN && e.PartsColor != MaidParts.PARTS_COLOR.SKIN_OUTLINE) return;
            var maidFieldInfo = typeof(InfinityColorTextureCache).GetField("maid_", BindingFlags.NonPublic | BindingFlags.Instance);
            var maid = maidFieldInfo?.GetValue(e.Instance) as Maid;
            if (!MergeContexts.TryGet(maid, out var ctx)) return;
            ctx.UpdateSkinColor(e.PartsColor);
        }
    }

    /// <summary>
    /// Sybaris 環境用の簡易ログクラス
    /// </summary>
    internal static class Log
    {
        internal static void Info(object data) => Write("INFO", data);
        internal static void Warn(object data) => Write("WARN", data);
        internal static void Error(object data) => Write("ERROR", data);
        private static void Write(string level, object data)
        {
            Console.WriteLine($"[{level}] {PluginName}: {data}");
        }
    }
    
    /// <summary>
    /// BepInEx の Paths.ConfigPath の模倣クラス
    /// Sybaris 環境では単純に Mod フォルダ配下を返す
    /// </summary>
    internal static class Paths
    {
        internal static string ConfigPath =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Config");
    }

}