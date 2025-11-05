using HarmonyLib;

namespace COM3D2.SkinMerge
{
    internal class HarmonyPatches
    {
        private static SkinMerge Sm => SkinMerge.Instance;

        /// <summary>
        /// メイド装着アイテム変更時のメインGUI反映のためのフック
        /// タトゥー・ほくろ削除時は毎回全削除してからの追加をしておりここでのフックは全削除を意味する
        /// </summary>
        [HarmonyPatch(typeof(Maid), nameof(Maid.SetProp),
            typeof(MaidProp), typeof(string), typeof(int), typeof(bool), typeof(bool))]
        [HarmonyPostfix]
        private static void Maid_SetProp_Postfix(Maid __instance, MaidProp mp)
        {
            if (!Sm.EnableHook || !Sm.MergeContexts.TryGet(__instance, out var ctx)) return;
            if (mp.idx == (int)MPN.skin)
                ctx.LoadSkin();
            if (mp.idx == (int)MPN.acctatoo || mp.idx == (int)MPN.hokuro)
                Sm.IsDeletingTattoo = true;
            else
                ctx.LoadSources(null, true);
        }
        
        /// <summary>
        /// タトゥー・ほくろ追加時のメインGUI反映のためのフック
        /// 削除時は毎回全削除してからの追加をしており、その追加では無視してMulTexProc後処理で反映する
        /// </summary>
        [HarmonyPatch(typeof(Maid), nameof(Maid.SetSubProp), typeof(MPN), typeof(int), typeof(string), typeof(int))]
        [HarmonyPostfix]
        private static void Maid_SetSubProp_Postfix(Maid __instance, MPN idx, int subno, string filename)
        {
            if (!Sm.EnableHook || !Sm.MergeContexts.TryGet(__instance, out var ctx)) return;
            if (!Sm.IsDeletingTattoo)
                ctx.LoadSources(null, true);
        }
        
        /// <summary>
        /// タトゥー・ほくろの不透明度変更時に変更値を取得するためのフック
        /// </summary>
        [HarmonyPatch(typeof(Maid), nameof(Maid.SubPropAlpha), typeof(MPN), typeof(int), typeof(float))]
        [HarmonyPostfix]
        private static void Maid_SubPropAlpha_Postfix(Maid __instance, MPN f_mpn, int f_nSubNo, float f_fTexMulAlpha)
        {
            if (!Sm.EnableHook || !Sm.MergeContexts.TryGet(__instance, out var ctx)) return;
            var fileName = ctx.Maid.GetProp(f_mpn).listSubProp[f_nSubNo].strFileName;
            var source = ctx.Sources.Find(x => x.MenuFileName == fileName);
            if (source != null)
                source.MenuAlpha = f_fTexMulAlpha;
        }
        
        /// <summary>
        /// タトゥー・ほくろ削除時は毎回全削除してからの追加をしておりここでまとめて反映する
        /// </summary>
        [HarmonyPatch(typeof(TBody), nameof(TBody.MulTexProc), typeof(string))]
        [HarmonyPostfix]
        private static void TBody_MulTexProc_Postfix(TBody __instance, string slotname)
        {
            if (!Sm.EnableHook || !Sm.IsDeletingTattoo || !Sm.MergeContexts.TryGet(__instance.maid, out var ctx)) return;
            Sm.IsDeletingTattoo = false;
            ctx.LoadSources(null, true);
        }
        
        /// <summary>
        /// フリーカラースキンのカラーパレット操作後にメインGUIのテクスチャ更新するためのフック
        /// </summary>
        [HarmonyPatch(typeof(InfinityColorTextureCache), nameof(InfinityColorTextureCache.UpdateColor), typeof(MaidParts.PARTS_COLOR))]
        [HarmonyPostfix]
        private static void InfinityColorTextureCache_UpdateColor_Postfix(InfinityColorTextureCache __instance, MaidParts.PARTS_COLOR parts_color, ref bool __result)
        {
            if (!__result) return;
            if (parts_color != MaidParts.PARTS_COLOR.SKIN && parts_color != MaidParts.PARTS_COLOR.SKIN_OUTLINE) return;
            var maid = AccessTools.Field(__instance.GetType(), "maid_").GetValue(__instance) as Maid;
            if (!Sm.MergeContexts.TryGet(maid, out var ctx)) return;
            ctx.UpdateSkinColor(parts_color);
        }

    }
}