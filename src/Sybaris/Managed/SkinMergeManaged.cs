using System;

namespace COM3D2.SkinMerge.Managed
{
    public class SkinMergeManaged
    {
        public const string SubPluginFullName = "COM3D2.SkinMerge.Managed";

        public static event EventHandler<OnMaidSetPropEventArgs> OnMaidSetProp;
        public static event EventHandler<OnMaidSetSubPropEventArgs> OnMaidSetSubProp;
        public static event EventHandler<OnMaidSubPropAlphaEventArgs> OnMaidSubPropAlpha;
        public static event EventHandler<OnTBodyMulTexProcEventArgs> OnTBodyMulTexProc;
        public static event EventHandler<OnUpdateInfinityColorEventArgs> OnUpdateInfinityColor;

        /// <summary>
        /// Maid.SetProp(prefix)時のイベント発生器
        /// </summary>
        public static void Maid_SetProp_Prefix(Maid instance, MaidProp maidProp, string filename, int rid, bool temp, bool noScale)
        {
            OnMaidSetProp?.Invoke(null, new OnMaidSetPropEventArgs(instance, maidProp));
        }
       
        /// <summary>
        /// Maid.SetSubProp(postfix)時のイベント発生器
        /// </summary>
        public static void Maid_SetSubProp_Postfix(Maid instance, MPN mpn, int subNo, string filename)
        {
            OnMaidSetSubProp?.Invoke(null, new OnMaidSetSubPropEventArgs(instance, mpn, subNo, filename));
        }
       
        /// <summary>
        /// Maid.SubPropAlpha(postfix)時のイベント発生器
        /// </summary>
        public static void Maid_SubPropAlpha_Postfix(Maid instance, MPN mpn, int subNo, float alpha)
        {
            OnMaidSubPropAlpha?.Invoke(null, new OnMaidSubPropAlphaEventArgs(instance, mpn, subNo, alpha));
        }
        
        /// <summary>
        /// TBody.MulTexProc(postfix)時のイベント発生器
        /// </summary>
        public static void TBody_MulTexProc_Postfix(TBody instance, string slotName)
        {
            OnTBodyMulTexProc?.Invoke(null, new OnTBodyMulTexProcEventArgs(instance, slotName));
        }
       
        /// <summary>
        /// InfinityColorTextureCache.UpdateColor(postfix)時のイベント発生器
        /// </summary>
        public static void InfinityColorTextureCache_UpdateColor_Postfix(
            InfinityColorTextureCache instance, MaidParts.PARTS_COLOR partsColor, bool result) 
        {
            OnUpdateInfinityColor?.Invoke(null, new OnUpdateInfinityColorEventArgs(instance, partsColor, result));
        }
    }

    public class OnMaidSetPropEventArgs : EventArgs
    {
        public Maid Instance { get; }
        public MaidProp MaidProp { get; }
        public OnMaidSetPropEventArgs(Maid instance, MaidProp maidProp)
        {
            Instance = instance;
            MaidProp = maidProp;
        }
    }
    
    public class OnMaidSetSubPropEventArgs : EventArgs
    {
        public Maid Instance { get; }
        public MPN Mpn { get; }
        public int SubNo { get; }
        public string FileName { get; }
    
        public OnMaidSetSubPropEventArgs(Maid instance, MPN mpn, int subNo, string fileName)
        {
            Instance = instance;
            Mpn = mpn;
            SubNo = subNo;
            FileName = fileName;
        }
    }
    
    public class OnMaidSubPropAlphaEventArgs : EventArgs
    {
        public Maid Instance { get; }
        public MPN Mpn { get; }
        public int SubNo { get; }
        public float Alpha { get; }
    
        public OnMaidSubPropAlphaEventArgs(Maid instance, MPN mpn, int subNo, float alpha)
        {
            Instance = instance;
            Mpn = mpn;
            SubNo = subNo;
            Alpha = alpha;
        }
    }
    
    public class OnTBodyMulTexProcEventArgs : EventArgs
    {
        public TBody Instance { get; }
        public string SlotName { get; }
    
        public OnTBodyMulTexProcEventArgs(TBody instance, string slotName)
        {
            Instance = instance;
            SlotName = slotName;
        }
    }
    
    public class OnUpdateInfinityColorEventArgs : EventArgs
    {
        public InfinityColorTextureCache Instance { get; }
        public MaidParts.PARTS_COLOR PartsColor { get; }
        public bool Result { get; }
    
        public OnUpdateInfinityColorEventArgs(InfinityColorTextureCache instance, MaidParts.PARTS_COLOR partsColor, bool result)
        {
            Instance = instance;
            PartsColor = partsColor;
            Result = result;
        }
    }
}