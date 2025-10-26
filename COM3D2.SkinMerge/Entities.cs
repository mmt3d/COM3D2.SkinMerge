using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    using SlotID = TBody.SlotID;
    using PARTS_COLOR = MaidParts.PARTS_COLOR;
    using static Localization;

    internal class ColorSetField
    {
        internal MPN Mpn;
        internal string FileNamePattern;
    }

    internal class MenuInfo
    {
        internal MPN Mpn;
        internal string FileName;
        internal string Category;
        internal float Priority;
        internal string Name;
        internal string Description;
        internal string IconName;
        internal readonly List<TextureBlend> TextureBlends = new List<TextureBlend>();
        internal readonly List<TextureChange> TextureChanges = new List<TextureChange>();
        internal ColorSetField ColorSet;
        internal readonly List<string> DelItems = new List<string>();
        internal string ModBaseMenu;
        internal readonly Dictionary<string, byte[]> ModRawData = new Dictionary<string, byte[]>();

        internal bool TryGetBlendable(out TextureBlend textureBlendMain, out TextureBlend textureBlendShadow)
        {
            var textureBlends = TextureBlends.Where(x =>
                x.MatNo == 0 && x.SlotId == SlotID.body || x.MatNo == 5 && x.SlotId == SlotID.head).ToList();
            textureBlendMain = textureBlends.Find(x => x.TexName ==  "_MainTex");
            textureBlendShadow = textureBlends.Find(x => x.TexName == "_ShadowTex");
            return (textureBlendMain != null && textureBlendShadow != null) || TextureBlends.Count > 0;
        }
        
        internal List<TextureChange> GetTexChanges(SlotID slot)
        {
            var matNo = slot == SlotID.body ? 0 : 5;
            return TextureChanges
                .Where(x => x.SlotId == slot && x.MatNo == matNo)
                .Where(x =>
                    x.TexName == "_MainTex" || x.TexName == "_ShadowTex" || x.TexName == "_OutlineTex" ||
                    x.PartsColor == PARTS_COLOR.SKIN || x.PartsColor == PARTS_COLOR.SKIN_OUTLINE)
                .OrderBy(x => x.TexName).ToList();
        }

        internal void OverrideModMenu(MenuInfo modMenu)
        {
            FileName = modMenu.FileName;
            Priority = modMenu.Priority;
            Name = modMenu.Name;
            Description = modMenu.Description;
            IconName = modMenu.IconName;
            ColorSet = modMenu.ColorSet;
            foreach (var tc in modMenu.TextureChanges)
            {
                var baseTc = TextureChanges.Find(x =>
                    x.SlotId == tc.SlotId && x.MatNo == tc.MatNo && x.TexName == tc.TexName);
                if (baseTc == null)
                    TextureChanges.Add(tc);
                else
                {
                    baseTc.FileName = tc.FileName;
                    baseTc.PartsColor = tc.PartsColor;
                    baseTc.FixedTexName = tc.FixedTexName;
                }
            }
            foreach (var rawData in modMenu.ModRawData)
            {
                ModRawData.Add(rawData.Key, rawData.Value);
            }
            ModBaseMenu = modMenu.ModBaseMenu;
        }
    }

    internal class TextureBlend
    {
        internal int Index;
        internal SlotID SlotId;
        internal int MatNo;
        internal string TexName;
        internal int LayerNo;
        internal string FileName;
        internal BlendMode BlendMode = BlendMode.Alpha;
    }

    internal enum BlendMode
    {
        None,
        Alpha,
        Multiply
    }

    internal class TextureChange
    {
        internal SlotID SlotId;
        internal int MatNo;
        internal string TexName;
        internal string FileName;
        internal PARTS_COLOR PartsColor = PARTS_COLOR.NONE;
        internal string FixedTexName;
        
        internal string[] GetMenuArgs() => new[]
        {
            "テクスチャ変更", SlotId.ToString(), MatNo.ToString(), TexName, FileName
        };
    }

    public class Backup
    {
        public string SkinMergeVersion;
        public string SkinFolderFileName;
        public string SkinFileName;
        public MaidParts.PartsColor SkinColor;
        public MaidParts.PartsColor SkinOutlineColor;
        public List<MergeSource> MergeSources = new List<MergeSource>();
        public string SaveName;
        public float SavePriority;

        internal void SetColor(PARTS_COLOR colorType, MaidParts.PartsColor color)
        {
            if (colorType == PARTS_COLOR.SKIN)
                SkinColor = color;
            else if (colorType == PARTS_COLOR.SKIN_OUTLINE)
                SkinOutlineColor = color;
        }
        
        internal MaidParts.PartsColor GetColor(PARTS_COLOR colorType)
        {
            return colorType == PARTS_COLOR.SKIN ? SkinColor : SkinOutlineColor;
        }
    }

    internal enum NameStyle
    {
        Jp,
        En,
    }
    
    public class MergeSource
    {
        internal SlotID SlotID;
        public MPN Mpn;
        internal string Name;
        internal Texture2D Icon;
        internal int MatNo;
        internal int LayerNo;
        internal BlendMode BlendMode;
        internal string TextureFileMain;
        internal string TextureFileShadow;
        internal bool IsShared => TextureFileMain == TextureFileShadow;
        internal bool DisableAlpha;
        internal int MenuLayerNo;
        public float MenuAlpha;
        internal BlendMode MenuBlendMode;
        internal bool IsSelected;
        internal bool IsVisible;
        public string MenuFileName;
        internal MergeSource SiblingSource;

        private enum MergeStatus
        {
            None = 0,
            Done = 1,
            Error = 2,
        }
        private MergeStatus _mergeStatus = MergeStatus.None;
        internal bool IsError
        {
            get => _mergeStatus == MergeStatus.Error;
            set => _mergeStatus = value ? MergeStatus.Error : MergeStatus.None;
        }

        internal bool IsDone
        {
            get => _mergeStatus == MergeStatus.Done;
            set => _mergeStatus = value ? MergeStatus.Done : MergeStatus.None;
        }

        internal float Alpha => DisableAlpha ? 1f : MenuAlpha;

        internal string FixedAlpha
        {
            get
            {
                if (Mpn != MPN.acctatoo && Mpn != MPN.hokuro)
                    return "-";
                var alphaText = MenuAlpha.ToF2();
                var fixedAlphaText = Alpha.ToF2();
                if (alphaText != fixedAlphaText)
                    alphaText += " ▶ " + fixedAlphaText;
                return alphaText;
            }
        }

        private string FixedLayerNo => MenuLayerNo == LayerNo ? LayerNo.ToString() : MenuLayerNo + " ▶ " + LayerNo;

        private string FixedBlendMode => MenuBlendMode == BlendMode ?
            BlendMode.ToString().ToLowerInvariant() : (MenuBlendMode + " ▶ " + BlendMode).ToLowerInvariant();

        internal string FixedBlendModeShort => MenuBlendMode == BlendMode ? BlendMode.ToString().ToLowerInvariant() :
            (MenuBlendMode.ToString().Substring(0, 1) + " ▶ " +
             BlendMode.ToString().Substring(0, 1)).ToLowerInvariant();

        internal string Tooltip => $"{Name}\n" +
                                   $" {_L("gui.label.layer")}: {FixedLayerNo}\n" +
                                   $" {_L("gui.label.blend_mode")}: {FixedBlendMode}\n" +
                                   $" {_L("gui.label.opacity")}: {FixedAlpha}";

        internal void Toggle()
        {
            IsSelected = !IsSelected;
            if (SiblingSource != null) SiblingSource.IsSelected = IsSelected;
        }
    }

    internal class MergeResult
    {
        internal SlotID SlotID;
        internal string TexName;
        internal string DisplayTexName;
        internal RenderTexture Texture;
        internal Texture2D OriginalTexture;
        internal PARTS_COLOR PartsColor;
        internal bool InUse = true;
        internal bool IsSelected;

        internal string Color => PartsColor == PARTS_COLOR.NONE ?
            _L("gui.label.color_fixed") :
            _L("gui.label.color_free") + " ▶ " + _L("gui.label.color_fixed");

        private static ConfigManager Cm => ConfigManager.Instance;

        internal string Size
        {
            get
            {
                var sideSize = TexName == "_MainTex" || TexName == "_ShadowTex" ? Cm.MainMaxSize.Value :
                    TexName == "_OutlineTex" ? Cm.OutlineMaxSize.Value : 0;
                var size = $"{Texture.width} x {Texture.height}";
                if (sideSize == 0 || Texture.width <= sideSize && Texture.height <= sideSize)
                    return size;
                return $"{size} ▶ {sideSize} x {sideSize}";
            }
        }
    }
}