using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace COM3D2.SkinMerge
{
    using SlotID = TBody.SlotID;
    using PARTS_COLOR = MaidParts.PARTS_COLOR;
    using GU = GraphicUtils;
    using FU = FileUtils;
    using static Localization;

    public class MergeContext
    {
        private static SkinMerge Sm => SkinMerge.Instance;
        private static ConfigManager Cm => ConfigManager.Instance;
        private static DialogManager Dm => DialogManager.Instance;
        private static readonly ManualLogSource Log = SkinMerge.Log;

        internal class SourceFilter
        {
            internal MPN Mpn;
            internal bool IsEnable => IsRestored || Cm.SourceFilter.Contains(Mpn);
            internal bool IsVisible = true;
            internal bool IsRestored;
            internal int Count;
            internal string Name => $"{_L(Mpn)} ({Count})";
        }
        internal readonly List<SourceFilter> SourceFilters = new List<SourceFilter>();
        internal readonly List<MergeSource> Sources = new List<MergeSource>();
        internal readonly List<MergeResult> Results = new List<MergeResult>();
        internal Maid Maid;

        internal bool HasSelected => Sources.Any(x => x.IsSelected);
        internal string MaidGuid => Maid.status.guid;
        
        internal Backup BackupData;
        internal Backup RestoreData;
        internal bool IsRestorable => RestoreData != null;
        internal MenuInfo BaseMenu;
        internal Texture2D BaseIcon;
        internal Texture2D BaseIconShadow;
        internal Texture2D BaseFolderIcon;
        internal string BaseTooltip => $"{BaseMenu?.FileName}\n {BaseMenu?.Name}\n {BaseMenu?.Description}";

        internal string SaveName;
        internal float? SavePriority;
        internal bool Restored;
    
        internal MenuInfo NewMenu;
        internal MenuInfo NewFolderMenu;
        internal Texture2D NewIcon;
        internal string NewTooltip => $"{NewMenu?.FileName}\n {NewMenu?.Name}\n {NewMenu?.Description}";

        [Flags]
        private enum Stat
        {
            Init = 0,
            Merging = 1,
            Merged = 2,
            Saved = 4,
            ModLoaded = 8,
            Restoring = 16
        }
        private Stat _status = Stat.Init;
        internal bool IsMerged => (_status & Stat.Merged) != 0;
        internal bool IsMerging => (_status & Stat.Merging) != 0;
        internal bool IsSaved => (_status & Stat.Saved) != 0;
        internal bool IsModLoaded => (_status & Stat.ModLoaded) != 0;
        internal bool IsRestoring => (_status & Stat.Restoring) != 0;
        
        internal MergeContext(Maid maid)
        {
            Maid = maid;
            LoadFilter();
            LoadSkin();
            LoadSources();
        }

        #region filter-methods

        /// <summary>
        /// 指定MPNがフィルターに含まれているかどうかを返却する
        /// </summary>
        private bool ContainsFilter(MPN mpn, bool? restored = null)
        {
            if (mpn == MPN.chikubi) return false;
            var filterMpn = ConfigManager.PrimaryMpn.Contains(mpn) ? mpn : MPN.null_mpn;
            var filter = SourceFilters.Find(x => x.Mpn == filterMpn);
            if (restored is true) filter.IsRestored = true;
            return filter.IsEnable;
        }
    
        #endregion
        
        #region source-methods
       
        /// <summary>
        /// 指定スロットの合成元リストを返却する
        /// </summary>
        internal List<MergeSource> GetSources(SlotID slot)
        {
            return Sources
                .Where(x => x.SlotID == slot && x.BlendMode != BlendMode.None && x.IsVisible)
                .OrderBy(x => x.LayerNo)
                .ToList();
        }

        /// <summary>
        /// 合成元リストにアイテムを追加する
        /// </summary>
        private void AddSource(MPN mpn, MenuInfo mi, TextureBlend tb, TextureBlend tbShadow, float alpha, bool visible = true, bool? selected = null, List<MergeSource> sources = null)
        {
            // menu情報上のmpnと実際のmpnが異なる場合がある(categoryが採用されてる)
            if (mi.Mpn != mpn)
                Log.LogWarning($"LoadSources: MPN mismatched: {mi.Name}({mi.FileName}), taken `{mi.Mpn}` as `{mpn}`");
            
            sources ??= Sources;
            // chikubicolor は folder にあたる chikubi も登録する
            MergeSource siblingSource = null;
            if (mpn == MPN.chikubicolor)
            {
                var mi2 = Maid.GetProp(MPN.chikubi).GetMenu();
                // chikubi未選択(chikubicolorだけ残っている状態)なので対象外とする ※公式は削除がないので削除MODで発生する
                if (mi2 == null || mi2.FileName.ToLowerInvariant() == Cm.NippleDelMenuName.Value.ToLowerInvariant()) return;
                siblingSource = new MergeSource
                {
                    Mpn = mi2.Mpn, BlendMode = BlendMode.None, MenuFileName = mi2.FileName
                };
            }

            // 対象外アイテムだが交換着脱時の復元用に登録する
            if (tb == null || tbShadow == null)
            {
                Sources.Add(new MergeSource
                {
                    Mpn = mpn,
                    DisableAlpha = true,
                    IsVisible = false,
                    MenuFileName = mi.FileName
                });
                return;
            }

            // カテゴリaccTatoo書き間違いは透過度指定無効となり1に固定されている動作に準拠
            var disableAlpha =
                mpn == MPN.acctatoo && mi.Category == "acctatoo" ||
                mpn != MPN.acctatoo && mpn != MPN.hokuro;
            // 特定MPNでの範囲外レイヤー指定は無効となり1として扱われ合成順も1の追加順となる動作に準拠
            var fixedLayerNo =
                mpn == MPN.acctatoo && tb.LayerNo > 3 || mpn == MPN.hokuro && tb.LayerNo > 1 ? 1 : tb.LayerNo;
            sources.Add(new MergeSource
            {
                SlotID = tb.SlotId,
                Mpn = mpn,
                Name = mi.Name,
                Icon = FU.LoadTexture(mi.IconName)?.Squared() ?? GU.GetEmbeddedTexture("unknown.png"),
                MatNo = tb.MatNo,
                LayerNo = fixedLayerNo,
                BlendMode = tb.BlendMode,
                TextureFileMain = tb.FileName,
                TextureFileShadow = tbShadow.FileName,
                DisableAlpha = disableAlpha,
                MenuLayerNo = tb.LayerNo,
                MenuAlpha = alpha,
                MenuBlendMode = tb.BlendMode,
                IsSelected = selected ?? (alpha > 0f || disableAlpha),
                IsDone = false,
                IsVisible = visible,
                MenuFileName = mi.FileName,
                SiblingSource = siblingSource
            });
            // 同一レイヤーに追加した最終BlendMode(alpha/multiply)で既存追加アイテムのBlendModeが上書きされる動作に準拠
            sources.Where(x => x.SlotID == tb.SlotId && x.LayerNo == fixedLayerNo)
                .ToList()
                .ForEach(x => x.BlendMode = tb.BlendMode);
        }

        /// <summary>
        /// 復元データと競合する合成元リストを返却する
        /// </summary>
        private List<MergeSource> GetConflictSources(Backup restore)
        {
            var currentSources = new HashSet<string>(Sources
                .Where(x => x.Mpn == MPN.acctatoo || x.Mpn == MPN.hokuro)
                .Select(x => x.MenuFileName));
            var currentMpn = new HashSet<MPN>(Sources
                .Where(x => x.Mpn != MPN.acctatoo && x.Mpn != MPN.hokuro)
                .Select(x => x.Mpn));
            var conflictItems = restore.MergeSources
                .Where(x => currentSources.Contains(x.MenuFileName) || currentMpn.Contains(x.Mpn));
            var conflictSources = new List<MergeSource>();
            foreach (var src in conflictItems)
            {
                var mi = FU.GetProperMenuFromRid(src.MenuFileName.GetRid());
                if (mi == null) continue;
                if (mi.TryGetBlendable(out var tbMain, out var tbShadow))
                    AddSource(src.Mpn, mi, tbMain, tbShadow, src.Alpha, true, true, conflictSources);
            }
            return conflictSources;
        }

        /// <summary>
        /// 復元データと競合しない合成元リストを返却する
        /// </summary>
        private List<MergeSource> GetDifferentSources(List<MergeSource> conflictSources)
        {
            var sources = Sources.Where(x =>
                (x.Mpn == MPN.acctatoo || x.Mpn == MPN.hokuro)
                && conflictSources.All(y => y.MenuFileName != x.MenuFileName)
                || conflictSources.All(y => y.Mpn != x.Mpn)).ToList();
            return UnnestSources(sources);
        }

        /// <summary>
        /// 合成済みの合成元リストを返却する
        /// </summary>
        private List<MergeSource> GetMergedSources()
        {
            var sources = Sources.Where(x => x.IsSelected && x.IsVisible).ToList();
            return UnnestSources(sources).ToList();
        }

        /// <summary>
        /// 未合成の合成元リストを返却する
        /// </summary>
        private List<MergeSource> GetUnmergedSources()
        {
            var sources = Sources.Where(x => !x.IsSelected || !x.IsVisible).ToList();
            return UnnestSources(sources).ToList();
        }

        /// <summary>
        /// 全合成元リストを返却する
        /// </summary>
        private List<MergeSource> GetAllSources()
        {
            return UnnestSources(Sources);
        }

        /// <summary>
        /// 合成元リストの兄弟ソースを展開して返却する
        /// </summary>
        private static List<MergeSource> UnnestSources(List<MergeSource> sources)
        {
            var newSources = sources.ToList();
            foreach (var src in sources.Where(src => src.SiblingSource != null).ToList())
                newSources.Add(src.SiblingSource);
            return newSources;
        }
        
        /// <summary>
        /// 指定フィルターに基づき合成元の表示状態を変更する
        /// </summary>
        internal void ChangeFilter(SourceFilter filter)
        {
            foreach (var src in Sources.Where(x => x.Mpn == filter.Mpn))
                src.IsVisible = filter.IsVisible;
        }

        /// <summary>
        /// 合成元の進捗状態をリセットする
        /// </summary>
        private void RevertSources()
        {
            Sources.ForEach(x => x.IsDone = false);
        }

        /// <summary>
        /// 合成元リストをクリアする
        /// </summary>
        private void ClearSources()
        {
            foreach (var item in Sources.Where(x => x.Icon))
                Object.Destroy(item.Icon);
            Sources.Clear();
            SourceFilters.ForEach(x => x.Count = 0);
        }
        
        #endregion
        
        #region result-methods

        /// <summary>
        /// 指定スロット・テクスチャ名の合成結果を取得する
        /// </summary>
        private bool TryGetResult(SlotID slot, string texName, out MergeResult result)
        {
            result = Results.Find(x => x.SlotID == slot && x.TexName == texName);
            return result != null;
        }

        /// <summary>
        /// 指定スロットの合成結果リストを返却する
        /// </summary>
        private List<MergeResult> GetResults(SlotID slot)
        {
            return Results.Where(x => x.SlotID == slot).ToList();
        }
        
        /// <summary>
        /// 合成結果の指定スロット・テクスチャ名を選択状態にする
        /// </summary>
        internal void SelectResult(SlotID slot, string texName)
        {
            GetResults(slot).ForEach(x => x.IsSelected = x.TexName == texName);
        }

        /// <summary>
        /// 指定スロットの選択中合成結果を返却する
        /// </summary>
        internal MergeResult GetSelectedResult(SlotID slot)
        {
            return GetResults(slot).Find(x => x.IsSelected);
        }

        /// <summary>
        /// 指定スロットの表示用合成結果リストを返却する
        /// </summary>
        internal List<MergeResult> GetDisplayResults(SlotID slot)
        {
            return GetResults(slot).Where(x => x.InUse).OrderByTexName().ToList();
        }

        /// <summary>
        /// バックアップXMLファイルを検索してロードする
        /// </summary>
        private void SearchBackupXml(string fileName)
        {
            var xmlPath = FU.SearchBackupXmlPath(Cm.SavePath, fileName);
            if (xmlPath != null)
                RestoreData = FU.LoadBackup(xmlPath);
        }

        /// <summary>
        /// 合成結果を元に戻す
        /// </summary>
        private void RevertResults()
        {
            Results.ForEach(x =>
            {
                var textureRef = x.Texture;
                x.Texture = GU.CreateFixedColorRenderTexture(Maid, x.OriginalTexture, x.PartsColor);
                Object.Destroy(textureRef);
            });
        }

        /// <summary>
        /// 合成前に戻す
        /// </summary>
        internal void Revert()
        {
            RevertResults();
            RevertSources();
            _status &= ~(Stat.Merged | Stat.Saved | Stat.ModLoaded);
        }

        /// <summary>
        /// 合成結果リストをクリアする
        /// </summary>
        private void ClearResults()
        {
            foreach (var item in Results.Where(x => x.Texture))
            {
                Object.Destroy(item.Texture);
                Object.Destroy(item.OriginalTexture);
            }

            Results.Clear();
            BackupData = RestoreData = null;
            BaseMenu = null;
            Object.Destroy(BaseIcon);
            SaveName = null;
            SavePriority = null;
            Restored = false;
            NewMenu = null;
            NewFolderMenu = null;
            Object.Destroy(NewIcon);
            GU.Init();
        }
        
        /// <summary>
        /// コンテキストをクリアする
        /// </summary>
        internal void Clear()
        {
            ClearSources();
            ClearResults();
            Maid = null;
        }
        
        #endregion

        /// <summary>
        /// フィルター設定をロードする
        /// </summary>
        private void LoadFilter()
        {
            SourceFilters.Clear();
            ConfigManager.OrderMpn.ForEach(x => SourceFilters.Add(new SourceFilter { Mpn = x }));
        }
        
        /// <summary>
        /// ベース肌情報をロードする
        /// </summary>
        internal void LoadSkin()
        {
            _status = Stat.Init;
            ClearResults();
            BaseMenu = Maid.GetProp(MPN.skin).GetMenu();
            var isMod = BaseMenu.ModBaseMenu != null;
            BaseIcon = isMod
                ? GU.PngToTexture2D(BaseMenu.ModRawData[BaseMenu.IconName])?.Squared()
                : FU.LoadTexture(BaseMenu.IconName)?.Squared();
            BaseIcon ??= GU.GetEmbeddedTexture("unknown.png");
            BaseIconShadow = GU.CreateShadow(BaseIcon);
            var folderMenu = Maid.GetProp(MPN.folder_skin).GetMenu();
            BaseFolderIcon = isMod
                ? GU.PngToTexture2D(folderMenu.ModRawData[folderMenu.IconName])?.Squared()
                : FU.LoadTexture(folderMenu.IconName)?.Squared();
            BaseFolderIcon ??= GU.GetEmbeddedTexture("unknown.png");
            SearchBackupXml(BaseMenu.FileName);
            
            BackupData = new Backup
            {
                SkinMergeVersion = SkinMerge.PluginVersion,
                SkinFileName = BaseMenu.FileName,
                SkinFolderFileName = Maid.GetProp(MPN.folder_skin).strFileName,
                SkinColor = Maid.Parts.GetPartsColor(PARTS_COLOR.SKIN),
                SkinOutlineColor = Maid.Parts.GetPartsColor(PARTS_COLOR.SKIN_OUTLINE)
            };
            var texMap = new Dictionary<string, string>();
            foreach (var slot in new[] { SlotID.body, SlotID.head })
            {
                var isFirst = true;
                foreach (var tc in BaseMenu.GetTexChanges(slot))
                {
                    var result = new MergeResult
                    {
                        SlotID = tc.SlotId,
                        TexName = tc.TexName,
                        DisplayTexName = tc.TexName.Replace("_", ""),
                        PartsColor = tc.PartsColor,
                        IsSelected = isFirst
                    };
                    Results.Add(result);
                    var fixedFileName = FixWildcardFileName(slot, tc.FileName);
                    if (isMod)
                    {
                        if (BaseMenu.ModRawData.TryGetValue(fixedFileName, out var png))
                        {
                            result.OriginalTexture = GU.PngToTexture2D(png);
                            result.Texture =
                                GU.CreateFixedColorRenderTexture(Maid, result.OriginalTexture, result.PartsColor);
                        }
                        else
                            Sm.TaskRunner.Add(LoadSkinTexture(result, fixedFileName.Replace(".png", ".tex")));
                    }
                    else
                        Sm.TaskRunner.Add(LoadSkinTexture(result, fixedFileName));

                    var texKey = $"{fixedFileName}:{tc.PartsColor}";
                    if (texMap.ContainsKey(texKey) && TryGetResult(slot, texMap[texKey], out var resultFirst))
                    {
                        result.InUse = false;
                        resultFirst.DisplayTexName += " / " + result.DisplayTexName;
                    }
                    else
                        texMap.Add(texKey, tc.TexName);
                    isFirst = false;
                }
            }
        }
        
        /// <summary>
        /// 肌メニュー内のワイルドカードファイル名を固定文字列に変換して返却する
        /// </summary>
        private string FixWildcardFileName(SlotID slot, string fileName)
        {
            if (!fileName.Contains("*")) return fileName.ToLowerInvariant();
            var modelFileName = Maid.body0.GetSlot((int)slot).m_strModelFileName;
            var modelName = Path.GetFileNameWithoutExtension(modelFileName);
            return fileName.Replace("*", modelName).ToLowerInvariant();
        }
        
        /// <summary>
        /// 肌テクスチャをロードして合成結果にセットする
        /// </summary>
        private IEnumerator LoadSkinTexture(MergeResult result, string texFileName)
        {
            var texture = FU.LoadTexture(texFileName);
            if (!texture)
            {
                Dm.ShowDialog(_L("dlg.msg.failed_load_skin") + "\n" +
                              $"Menu: {BaseMenu.Name}\nFileName: {BaseMenu.FileName}\nTexture: {texFileName}");
                yield break;
            }
            yield return null;

            if (!IsRestorable)
            {
                // 公式"アニメ塗り肌・ライトダーク"のように肌有効部にalpha値0以上1以下のピクセルを含むものがあり、
                // ゲーム内ではalpha=1として取り扱われているため、ベース肌テクスチャとしてここでalpha=1に修正する
                // (合成スキンの場合は各種タトゥーによるゴミピクセルが際立ちやすいため修正しない)
                texture.ForceAlpha();
            }
            result.Texture = GU.CreateFixedColorRenderTexture(Maid, texture, result.PartsColor);
            result.OriginalTexture = texture;
            yield return null;
        }

        /// <summary>
        /// 合成元リストをロードする
        /// </summary>
        internal void LoadSources(List<string> restoreSelected = null, bool viaMaidProp = false)
        {
            // 本体の機能で対象アイテムの着脱をした場合は、あとで各種ステータス・透過度を戻すために値を退避
            var sourcesForStatus = viaMaidProp ? Sources.ToList() : null;

            ClearSources();

            // 合成対象読み込み
            foreach (MPN mpn in Enum.GetValues(typeof(MPN)))
            {
                if (mpn == MPN.skin) continue;
                var mp = Maid.GetProp(mpn);
                if (mp == null) continue;
                if (mpn == MPN.acctatoo || mpn == MPN.hokuro)
                {
                    if (mp.listSubProp == null) continue;
                    foreach (var sp in mp.listSubProp)
                    {
                        var mi = sp?.GetMenu();
                        if (mi == null) continue;
                        var selected = restoreSelected?.Contains(mi.FileName.ToLowerInvariant());
                        var visible = ContainsFilter(mi.Mpn, selected);
                        if (mi.TryGetBlendable(out var tbMain, out var tbShadow))
                            AddSource(mpn, mi, tbMain, tbShadow, sp.fTexMulAlpha, visible, selected);
                    }
                }
                else
                {
                    var mi = mp.GetMenu();
                    if (mi == null) continue;
                    var selected = restoreSelected?.Contains(mi.FileName.ToLowerInvariant());
                    var visible = ContainsFilter(mi.Mpn, selected);
                    if (mi.TryGetBlendable(out var tbMain, out var tbShadow))
                        AddSource(mpn, mi, tbMain, tbShadow, 1f, visible, selected);
                }
            }
            
            // SourceFilterのMPN別カウント
            foreach (var src in Sources.Where(x => x.IsVisible).GroupBy(x => x.Mpn)
                         .Select(g => new { Mpn = g.Key, Count = g.Count()}))
            {
                var filterMpn = ConfigManager.PrimaryMpn.Contains(src.Mpn) ? src.Mpn : MPN.null_mpn;
                SourceFilters.Find(x => x.Mpn == filterMpn).Count += src.Count;
            }
            
            // 本体機能での着脱の場合、各種ステータスや透過度を戻す
            if (sourcesForStatus == null) return;
            foreach (var keep in sourcesForStatus)
            {
                var dest = Sources.Find(x => x.MenuFileName == keep.MenuFileName);
                if (dest == null) continue;
                dest.IsDone = keep.IsDone;
                dest.IsSelected = keep.IsSelected;
                dest.IsVisible = keep.IsVisible;
                // アイテム外した場合は透過度反映がなぜか一律1fになるので強制的に戻す(多トゥー使ってれば本体側は正しくなる)
                dest.MenuAlpha = keep.MenuAlpha;
            }
        }
        
        /// <summary>
        /// 肌カラーパレット変更時に合成結果の色を更新する
        /// </summary>
        internal void UpdateSkinColor(PARTS_COLOR partsColor)
        {
            // 合成中・合成後は受け付けない
            if (IsMerging || IsMerged) return;
            // パーツカラーに変化がない場合は何もしない
            var newColor = Maid.Parts.GetPartsColor(partsColor);
            if (BackupData.GetColor(partsColor).IsEqual(newColor)) return;
            // 該当パーツカラー使用のテクスチャの色を更新
            foreach (var result in Results.Where(x => x.PartsColor == partsColor))
                GU.FixInfinityColor(Maid, result.OriginalTexture, result.PartsColor, result.Texture);
            // 新パーツカラーを保存
            BackupData.SetColor(partsColor, newColor);
        }

        /// <summary>
        /// 肌合成を実行する
        /// </summary>
        private IEnumerator _mergeSkin()
        {
            _status |= Stat.Merging;
            _status &= ~Stat.Merged;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            foreach (var slot in new[] { SlotID.body, SlotID.head })
            {
                foreach (var src in Sources
                             .Where(x => x.SlotID == slot && x.IsSelected && x.IsVisible)
                             .OrderBy(x => x.LayerNo))
                {
                    var blendTexMain = FU.LoadTexture(src.TextureFileMain);
                    var errorMessages = new List<string>();
                    if (!blendTexMain) errorMessages.Add($"MainTex: {src.TextureFileMain}");
                    var blendTexShadow = src.IsShared ? blendTexMain : FU.LoadTexture(src.TextureFileShadow);
                    if (!blendTexShadow) errorMessages.Add($"ShadowTex: {src.TextureFileShadow}");
                    if (errorMessages.Count > 0)
                    {
                        Dm.ShowDialog(_L("dlg.msg.failed_load_source") + "\n" +
                                      $"Menu: {src.Name}\nFileName: {src.MenuFileName}\n" +
                                      string.Join("\n", errorMessages.ToArray()));
                        src.IsError = true;
                    }
                    else
                    {
                        if (TryGetResult(slot, "_MainTex", out var resultMain))
                            GU.Blend(ref resultMain.Texture, blendTexMain, src.BlendMode, src.Alpha);
                        if (TryGetResult(slot, "_ShadowTex", out var resultShadow) && resultShadow.InUse)
                            GU.Blend(ref resultShadow.Texture, blendTexShadow, src.BlendMode, src.Alpha);
                        src.IsDone = true;
                    }
                    yield return null;
                    Object.Destroy(blendTexMain);
                    Object.Destroy(blendTexShadow);
                }

                yield return null;
            }

            BackupData.MergeSources = GetMergedSources();
            NewIcon = GU.CreateMenuIcon(Maid.GetThumCard(), Cm.SaveIconSize.Value);
            SetupNewSkinMenu();
            _status |= Stat.Merged;
            _status &= ~Stat.Merging;
            stopwatch.Stop();
            Log.LogInfo($"MergeSkin elapsed: {stopwatch.Elapsed.TotalSeconds} s");
            yield return null;
        }
        
        /// <summary>
        /// 新肌メニュー情報を構成する
        /// </summary>
        private void SetupNewSkinMenu()
        {
            var mi = BaseMenu;
            var maidName = Maid.GetName();
            SaveName ??= $"{Cm.SaveFilePrefix.Value}{maidName}";
            SavePriority ??= Cm.MenuPriority.Value;
            // メニュー名にスペースが入ってはいけない
            var menuName = (_L("menu.name.merged") + maidName).Replace(" ", "_");
            NewMenu = new MenuInfo
            {
                Mpn = mi.Mpn,
                FileName = GetMenuFileName(SaveName),
                Category = mi.Category,
                Name = menuName,
                Description = $"Generated by {SkinMerge.PluginTitleName}",
                IconName = $"{SaveName}.tex",
                Priority = 1,
            };
            NewFolderMenu = new MenuInfo
            {
                Mpn = MPN.folder_skin,
                FileName = GetFolderMenuFileName(SaveName),
                Category = "folder_skin",
                Name = menuName,
                Description = $"Generated by {SkinMerge.PluginTitleName}",
                IconName = $"{SaveName}.tex",
                ColorSet = new ColorSetField
                {
                    Mpn = MPN.skin,
                    FileNamePattern = $"{SaveName}_*.menu"
                },
                Priority = SavePriority ?? Cm.MenuPriority.Value
            };
        }
        
        /// <summary>
        /// 肌メニューのファイル名を返却する
        /// </summary>
        private static string GetMenuFileName(string saveName)
        {
            return $"{saveName}_i_.menu";
        }
        
        /// <summary>
        /// 肌フォルダメニューのファイル名を返却する
        /// </summary>
        private static string GetFolderMenuFileName(string saveName)
        {
            return $"{saveName}.menu";
        }

        /// <summary>
        /// メニューがModLoaderなどで反映されるのを待つコルーチン
        /// </summary>
        private IEnumerator WaitForMenuReflected(string fileName)
        {
            while (!SceneEdit.Instance.m_menuRidDic.ContainsKey(fileName.GetRid()))
                yield return new WaitForSeconds(2);
            _status |= Stat.ModLoaded;
            yield return null;
        }

        /// <summary>
        /// 合成肌メニューを保存する
        /// </summary>
        private void SaveSkinMenu()
        {
            _status &= ~Stat.Saved;
            _status &= ~Stat.ModLoaded;
            var mi = BaseMenu;
            var baseMenuFileName = mi.ModBaseMenu ?? mi.FileName;
            var menu = NewMenu;
            // ダイアログで変更される可能性
            menu.FileName = $"{SaveName}_i_.menu";
            menu.IconName = $"{SaveName}.tex";
            var garbage = new List<Texture2D>();
            var textures = new Dictionary<string, Texture2D> { { menu.IconName, NewIcon } };
            foreach (var t in mi.TextureChanges)
            {
                if (!TryGetResult(t.SlotId, t.FixedTexName, out var result)) continue;
                var texFileName = $"{SaveName}_{t.SlotId}{t.FixedTexName}.tex";
                menu.TextureChanges.Add(new TextureChange
                {
                    SlotId = t.SlotId,
                    MatNo = t.MatNo,
                    TexName = t.TexName,
                    FileName = texFileName
                });
                if (textures.ContainsKey(texFileName)) continue;
                var sizeSize = 0;
                if (t.FixedTexName == "_MainTex" || t.FixedTexName == "_ShadowTex")
                    sizeSize = Cm.MainMaxSize.Value;
                else if (t.FixedTexName == "_OutlineTex")
                    sizeSize = Cm.OutlineMaxSize.Value;
                var texture = result.Texture.CreateTexture2D(sizeSize, sizeSize);
                texture.UnpremultiplyAlpha();
                textures.Add(texFileName, texture);
                garbage.Add(texture);
            }

            var hasNew = FU.SaveMenuInherited(Path.Combine(Cm.SavePath, SaveName), baseMenuFileName, menu, textures);
            foreach (var tex in garbage)
                Object.Destroy(tex);
            hasNew |= SaveFolderMenu();
            SaveBackup();
            if (!hasNew)
            {
                Dm.ShowDialog(_L("dlg.msg.saved_available"));
                _status |= Stat.ModLoaded | Stat.Saved;
                return;
            }

            if (Cm.AutoMaidLoaderRefresh.Value && Sm.HasMaidLoader)
            {
                Sm.TaskRunner.Add(MaidLoader.MaidLoader.refreshMod.RefreshCo());
                Sm.TaskRunner.Add(WaitForMenuReflected(menu.FileName));
                Dm.ShowDialog(_L("dlg.msg.saved_auto_refresh"));
            }
            else
            {
                Dm.ShowDialog(_L("dlg.msg.saved_manual_refresh"));
            }
            _status |= Stat.Saved;
        }

        /// <summary>
        /// 合成肌フォルダメニューを保存する
        /// </summary>
        private bool SaveFolderMenu()
        {
            var menu = NewFolderMenu;
            // ダイアログで変更される可能性
            menu.FileName = $"{SaveName}.menu";
            menu.IconName = $"{SaveName}.tex";
            menu.ColorSet.FileNamePattern = $"{SaveName}_*.menu";
            menu.Priority = SavePriority ?? Cm.MenuPriority.Value;
            return FU.SaveMenuInherited(Path.Combine(Cm.SavePath, SaveName), "skin_folder_normal_i_.menu", menu, null);
        }

        /// <summary>
        /// 合成肌の構成バックアップXMLを保存する
        /// </summary>
        private void SaveBackup()
        {
            BackupData.SaveName = SaveName;
            BackupData.SavePriority = SavePriority ?? Cm.MenuPriority.Value;
            var basePath = Path.Combine(Cm.SavePath, SaveName);
            var filePath = Path.Combine(basePath, $"{SaveName}_i_.skmg.xml");
            FU.SaveBackup(filePath, BackupData);
        }

        /// <summary>
        /// 合成肌構成を復元するコルーチン
        /// </summary>
        private IEnumerator Restore(List<MergeSource> additionalItems)
        {
            _status |= Stat.Restoring;
            // MainWindowのOnGUIで確実に描画されるまでフレームを待つ(2つ必要だった)
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var restore = RestoreData;
            var restoreFileList = restore.MergeSources.Select(x => x.MenuFileName).ToList();
            restore.MergeSources = restore.MergeSources.Concat(additionalItems).ToList();
            AttachAllTattoo(restore);

            LoadFilter();
            LoadSkin();
            LoadSources(restoreFileList);
            SaveName = restore.SaveName;
            SavePriority = restore.SavePriority;
            Restored = true;
            _status &= ~Stat.Restoring;
        }

        /// <summary>
        /// 生成MODに交換着脱する
        /// </summary>
        internal void Replace()
        {
            var replace = new Backup
            {
                SkinFileName = NewMenu.FileName,
                SkinFolderFileName = NewFolderMenu.FileName,
                MergeSources = GetUnmergedSources(),
                SkinColor = Maid.Parts.GetPartsColor(PARTS_COLOR.SKIN),
                SkinOutlineColor = Maid.Parts.GetPartsColor(PARTS_COLOR.SKIN_OUTLINE)
            };
            AttachAllTattoo(replace);

            LoadFilter();
            LoadSkin();
            LoadSources();
        }

        /// <summary>
        /// 指定内容で全着脱する
        /// </summary>
        private void AttachAllTattoo(Backup attach)
        {
            Sm.EnableHook = false;
            ClearAllTattoo();
            Maid.SetProp(MPN.skin, attach.SkinFileName, 0);
            Maid.SetProp(MPN.folder_skin, attach.SkinFolderFileName, 0);
            Maid.GetProp(MPN.skin).boDut = true;
            if (Maid.GetProp(MPN.skin).GetMenu() == null)
                Log.LogError($"Failed to get menu for {MPN.skin}: {attach.SkinFileName}");
            Maid.GetProp(MPN.folder_skin).boDut = true;
            if (Maid.GetProp(MPN.folder_skin).GetMenu() == null)
                Log.LogError($"Failed to get menu for {MPN.folder_skin}: {attach.SkinFolderFileName}");
            Maid.Parts.SetPartsColor(PARTS_COLOR.SKIN, attach.SkinColor);
            Maid.Parts.SetPartsColor(PARTS_COLOR.SKIN_OUTLINE, attach.SkinOutlineColor);
            foreach (var src in attach.MergeSources)
            {
                var mpn = src.Mpn;
                if (mpn == MPN.acctatoo || mpn == MPN.hokuro)
                {
                    var i = Maid.GetProp(mpn).listSubProp?.Count ?? 0;
                    Maid.SetSubProp(mpn, i, src.MenuFileName);
                    Maid.SubPropAlpha(mpn, i, src.Alpha);
                    Maid.GetSubProp(mpn, i).bDut = true;
                    if (Maid.GetSubProp(mpn, i).GetMenu() == null)
                        Log.LogError($"Failed to get menu for {mpn}: {src.MenuFileName}");
                }
                else
                {
                    Maid.SetProp(mpn, src.MenuFileName, 0);
                    if (Maid.GetProp(mpn).GetMenu() == null)
                        Log.LogError($"Failed to get menu for {mpn}: {src.MenuFileName}");
                }

                Maid.GetProp(mpn).boDut = true;
            }

            Sm.EnableHook = true;
            Maid.AllProcProp();
            SceneEdit.Instance.UpdateCurrentItemPanel(true);
            SceneEdit.Instance.customViewWindow.UpdateAllItem();
        }
        
        /// <summary>
        /// 全アイテムを解除する
        /// </summary>
        private void ClearAllTattoo()
        {
            GetAllSources().ForEach(src =>
            {
                if (src.Mpn == MPN.acctatoo || src.Mpn == MPN.hokuro)
                    Maid.DelSubProp(src.Mpn);
                if (Cm.DelMenuMap.TryGetValue(src.Mpn, out var delMenu))
                    Maid.SetProp(src.Mpn, delMenu, 0);
                else
                    Maid.DelProp(src.Mpn);
                Maid.GetProp(src.Mpn).boDut = true;
            });
            Maid.AllProcProp();
        }
        
        /// <summary>
        /// 復元確認ダイアログを表示する
        /// </summary>
        internal void RestoreConfirm()
        {
            var conflictSources = GetConflictSources(RestoreData);
            var safeSources = GetDifferentSources(conflictSources);
            if (conflictSources.Count > 0)
                Dm.ShowDialog(
                    _L("dlg.msg.restore_confirmation"),
                    () => Sm.TaskRunner.Add(Restore(safeSources)),
                    null,
                    _ => Dm.RestoreDialogGuiFunc(conflictSources));
            else
                Sm.TaskRunner.Add(Restore(safeSources));
        }

        /// <summary>
        /// 合成確認ダイアログを表示する
        /// </summary>
        internal void MergeSkinConfirm()
        {
            if (IsRestorable)
            {
                Dm.ShowDialog(
                    _L("dlg.msg.double_merge"),
                    () => Sm.TaskRunner.Add(_mergeSkin()));
            }
            else
                Sm.TaskRunner.Add(_mergeSkin());
        }
        
        /// <summary>
        /// 指定保存名の合成肌メニューが存在するか返却する
        /// </summary>
        internal bool ExistsSaveMenu(string saveName)
        {
            return FU.ExistsMenu(GetMenuFileName(saveName)) ||
                   FU.ExistsMenu(GetFolderMenuFileName(saveName));
        }

        /// <summary>
        /// 合成肌メニュー保存確認ダイアログを表示する
        /// </summary>
        internal void SaveSkinConfirm()
        {
            Dm.ShowDialog(_L("dlg.msg.save_confirmation"),
                SaveSkinMenu, null, Dm.SaveDialogGuiFunc);
        }
    }

    internal class MergeContextMap : Dictionary<string, MergeContext>
    {
        /// <summary>
        /// メイドごとのコンテキストをセットアップする
        /// </summary>
        internal MergeContext Setup(Maid maid)
        {
            var guid = maid.status.guid;
            if (!ContainsKey(guid))
                Add(guid, new MergeContext(maid));
            return this[guid];
        }
        
        /// <summary>
        /// メイドごとのコンテキストを取得する
        /// </summary>
        internal bool TryGet(Maid maid, out MergeContext context)
        {
            var guid = maid.status.guid;
            if (ContainsKey(guid))
            {
                context = this[guid];
                return true;
            }
            context = null;
            return false;
        }
        
        /// <summary>
        /// 全コンテキストをクリアする
        /// </summary>
        internal new void Clear()
        {
            foreach (var context in Values)
                context.Clear();
            base.Clear();
        }
    }
    
    internal static class MaidExtensions
    {
        private static ConfigManager Cm => ConfigManager.Instance;
        /// <summary>
        /// メイドの名前を設定に基づき取得するExtensionメソッド
        /// </summary>
        internal static string GetName(this Maid maid)
        {
            switch (Cm.SaveNameStyle.Value)
            {
                case nameof(NameStyle.Jp):
                    return maid.status.fullNameJpStyle.Trim().Replace(" ", "");
                case nameof(NameStyle.En):
                    return maid.status.fullNameEnStyle.Trim().Replace(" ", "");
                default:
                    return "";
            }
        }
    }

    internal static class MaidPartsExtensions
    {
        /// <summary>
        /// 同一パーツカラーかどうかを返却するExtensionメソッド
        /// </summary>
        internal static bool IsEqual(this MaidParts.PartsColor a, MaidParts.PartsColor b)
        {
            if (a.m_nMainBrightness != b.m_nMainBrightness) return false;
            if (a.m_nMainChroma != b.m_nMainChroma) return false;
            if (a.m_nMainContrast != b.m_nMainContrast) return false;
            if (a.m_nMainHue != b.m_nMainHue) return false;
            if (a.m_nShadowBrightness != b.m_nShadowBrightness) return false;
            if (a.m_nShadowChroma != b.m_nShadowChroma) return false;
            if (a.m_nShadowContrast != b.m_nShadowContrast) return false;
            if (a.m_nShadowHue != b.m_nShadowHue) return false;
            if (a.m_nShadowRate != b.m_nShadowRate) return false;
            return true;
        }
    }

    internal static class MergeContextExtensions
    {
        /// <summary>
        /// 合成結果リストをテクスチャ名でソートして返却するExtensionメソッド
        /// </summary>
        internal static IOrderedEnumerable<MergeResult> OrderByTexName(this IEnumerable<MergeResult> list)
        {
            return list
                .OrderBy<MergeResult, object>(x => SortTexName(x.TexName))
                .ThenBy(x => x.TexName);
        }
        
        /// <summary>
        /// テクスチャ名のソート順を返却するExtensionメソッド
        /// </summary>
        private static int SortTexName(string texName)
        {
            return texName switch
            {
                "_MainTex" => 0,
                "_ShadowTex" => 1,
                "_OutlineTex" => 2,
                _ => 3
            };
        }

        /// <summary>
        /// 指定内容のTextureChangeをリストから取り出して返却するExtensionメソッド
        /// </summary>
        internal static bool TryPop(this List<TextureChange> tcList, string[] menuArgs, out TextureChange tc)
        {
            var slot = (SlotID)Enum.Parse(typeof(SlotID), menuArgs[1]);
            var matNo = int.Parse(menuArgs[2]);
            var texName = menuArgs[3];
            tc = tcList.Find(x => x.SlotId == slot && x.MatNo == matNo && x.TexName == texName);
            if (tc != null) tcList.Remove(tc);
            return tc != null;
        }
    }
}