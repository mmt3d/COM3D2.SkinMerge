using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    using SM = SkinMerge;
    
    public static class GraphicUtils
	{
		private static readonly ManualLogSource Log = SkinMerge.Log;
        
        private static readonly Dictionary<string, Material> CachedMaterials = new Dictionary<string, Material>();
        private static AssetBundle _assetBundle;
        private static readonly int BaseTex = Shader.PropertyToID("_BaseTex");
        private static readonly int BlendTex = Shader.PropertyToID("_BlendTex");
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private static InfinityColorTextureCache _icTextureCache;

        internal static void Init()
        {
            _icTextureCache = null;
        }

        /// <summary>
        /// RenderTexture.activeを一時的に切り替えるためのDisposableクラス
        /// </summary>
        private class ActivateRenderTexture : IDisposable
        {
            private readonly RenderTexture _prev;

            public ActivateRenderTexture(RenderTexture next = null)
            {
                _prev = RenderTexture.active;
                RenderTexture.active = next;
            }

            public void Dispose()
            {
                RenderTexture.active = _prev;
            }
        }
        
        /// <summary>
        /// 同梱埋め込みリソースから指定シェーダーを取得しMaterialで返却
        /// </summary>
        private static Material GetShaderMaterial(string shaderName)
        {
            if (CachedMaterials.TryGetValue(shaderName, out var material)) return material;

            var shaderPath = $"Assets/Shaders/{shaderName}.shader".ToLowerInvariant();
            var shader = _assetBundle.LoadAsset<Shader>(shaderPath);
            var mat = new Material(shader);
            CachedMaterials.Add(shaderName, mat);
            return mat;
        }

        /// <summary>
        /// 2つのTexture2Dをアルファブレンド合成して返却
        /// 主にアイコン合成用
        /// </summary>
        private static Texture2D AlphaBlend(Texture2D baseTex, Texture2D blendTex, int width = 0, int height = 0)
        {
            var w = width > 0 ? width : Math.Max(baseTex.width, blendTex.width);
            var h = height > 0 ? height : Math.Max(baseTex.height, blendTex.height);
            var rt = baseTex.CreateRenderTexture(w, h);
            
            Blend(ref rt, blendTex, BlendMode.Alpha, 1f);
            var resultTex = rt.CreateTexture2D();
            UnityEngine.Object.Destroy(rt);
            return resultTex;
        }

        /// <summary>
        /// RenderTextureにTexture2Dを指定の合成モード・不透明度指定で合成する
        /// </summary>
        internal static void Blend(ref RenderTexture resultTex, Texture2D blendTex, BlendMode blendMode, float alpha)
        {
            // 結果の解像度変更が必要な場合
            if (resultTex.width < blendTex.width || resultTex.height < blendTex.height)
            {
                var w = Math.Max(resultTex.width, blendTex.width);
                var h = Math.Max(resultTex.height, blendTex.height);
                var newResultTex = resultTex.Copy(w, h);
                resultTex.Release();
                resultTex = newResultTex;
            }
            
            // 合成
            var mat = GetShaderMaterial($"Blend{blendMode}");
            var tempRt = resultTex.Copy();
            mat.SetTexture(BaseTex, tempRt);
            mat.SetTexture(BlendTex, blendTex);
            mat.SetFloat(Alpha, alpha);
            using (new ActivateRenderTexture())
                Graphics.Blit(blendTex, resultTex, mat);
            UnityEngine.Object.Destroy(tempRt);
        }

        /// <summary>
        /// フリーカラーテクスチャの固定色テクスチャキャッシュを更新
        /// </summary>
        internal static void FixInfinityColor(Maid maid, Texture2D mainTex, MaidParts.PARTS_COLOR partsColor, RenderTexture resultTex)
        {
            if (partsColor == MaidParts.PARTS_COLOR.NONE || !mainTex || !resultTex) return;

            _icTextureCache ??= new InfinityColorTextureCache(maid);
            _icTextureCache.UpdateTexture(mainTex, partsColor, resultTex);
        }
        
        /// <summary>
        /// フリーカラーテクスチャを固定色化したRenderTextureを作成して返却
        /// </summary>
        internal static RenderTexture CreateFixedColorRenderTexture(Maid maid, Texture2D mainTex, MaidParts.PARTS_COLOR partsColor)
        {
            if (partsColor == MaidParts.PARTS_COLOR.NONE)
            {
                return mainTex.CreateRenderTexture();
            }
            
            var rt = new RenderTexture(mainTex.width, mainTex.height, 0, RenderTextureFormat.ARGB32);
            rt.Create();
            FixInfinityColor(maid, mainTex, partsColor, rt);
            return rt;
        }
        
        /// <summary>
        /// メイドのサムネイルカード画像からメニューアイコン画像を作成して返却
        /// </summary>
        internal static Texture2D CreateMenuIcon(Texture2D thumbCard, int size)
        {
            const float widthRate = 0.55f;
            const float yPosRate = -0.75f;
            var cropSize = (int)(thumbCard.width * widthRate);
            var cropRect = new Rect(
                (thumbCard.width - cropSize) / 2,
                (thumbCard.height - cropSize) / 2 * (1 + yPosRate),
                cropSize,
                cropSize
            );
            var cropped = new Texture2D(cropSize, cropSize, TextureFormat.ARGB32, false);
          
            var rt = thumbCard.CreateRenderTexture();
            using (new ActivateRenderTexture(rt))
            {
                cropped.ReadPixels(cropRect, 0, 0);
                cropped.Apply();
            }

            var frame = GetEmbeddedTexture("icon_frame.png");
            var resultTex = AlphaBlend(cropped, frame, size, size);
            UnityEngine.Object.Destroy(frame);
            UnityEngine.Object.Destroy(cropped);
            return resultTex;
        }

        /// <summary>
        /// 背景用に指定Colorで塗りつぶした1x1ピクセルのTexture2Dを作成して返却
        /// </summary>
        internal static Texture2D GetBgColor(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }
        
        /// <summary>
        /// グレースケールColorオブジェクトを作成して返却
        /// </summary>
        internal static Color GrayColor(float rgb, float a = 1f)
        {
            return new Color(rgb, rgb, rgb, a);
        }

        /// <summary>
        /// アイコン背景用に指定Colorで塗りつぶしたTexture2Dに影付き合成したTexture2Dを作成して返却
        /// </summary>
        internal static Texture2D GetIconBgTexture(Color color)
        {
            var baseTex = GetBgColor(color);
            var shadow = GetEmbeddedTexture("icon_shadow.png");
            var resultTex = AlphaBlend(baseTex, shadow);
            UnityEngine.Object.Destroy(baseTex);
            UnityEngine.Object.Destroy(shadow);
            return resultTex;
        }

        /// <summary>
        /// 指定背景色・枠色・枠サイズで枠付きTexture2Dを作成して返却
        /// </summary>
        internal static Texture2D GetBorderedTexture(Color bgColor, Color borderColor, RectOffset border)
        {
            var w = border.left + border.right + 1;
            var h = border.top + border.bottom + 1;
            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var isBorder = x < border.left || x >= w - border.right || y < border.bottom || y >= h - border.top;
                tex.SetPixel(x, y, isBorder ? borderColor : bgColor);
            }
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }
        
        /// <summary>
        /// 同梱埋め込みリソースからTexture2Dを取得して返却
        /// </summary>
        internal static Texture2D GetEmbeddedTexture(string resourceName)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(SM.GetResourceBytes(resourceName));
            return texture;
        }

        /// <summary>
        /// 同梱埋め込みリソースからAssetBundleを読み込み
        /// </summary>
        internal static void LoadAssetBundle(string assetBundleName)
        {
            _assetBundle = AssetBundle.LoadFromMemory(SM.GetResourceBytes(assetBundleName));
        }

        /// <summary>
        /// PNGデータ(byte[])からTexture2Dを作成して返却
        /// </summary>
        internal static Texture2D PngToTexture2D(byte[] pngData)
        {
            if (pngData == null || pngData.Length == 0) return null;
            var texture = new Texture2D(2, 2);
            if (texture.LoadImage(pngData)) return texture;
            Log.LogError("Failed to load PNG data into Texture2D.");
            return null;
        }

        /// <summary>
        /// 指定Texture2Dから薄い影付きTexture2Dを作成して返却
        /// </summary>
        internal static Texture2D CreateShadow(Texture2D tex)
        {
            var mask = tex.Copy();
            var pixels = mask.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = pixels[i].g = pixels[i].b = 0;
                pixels[i].a = pixels[i].a > 0 ? 0.05f : 0f;
            }
            mask.SetPixels(pixels);
            mask.Apply();
            
            const int margin = 2;
            var w = tex.width + margin * 2;
            var h = tex.height + margin * 2;
            var result = mask.Resized(w, h);
            UnityEngine.Object.Destroy(mask);
            return result;
        }
       
        #region Extensions
        
        /// <summary>
        /// Texture2Dをコピーして返却するExtension
        /// GetPixels不可なリソースにも対応
        /// </summary>
        internal static Texture2D Copy(this Texture2D texture)
        {
            if (!texture) return null;
            try
            {
                var pixels = texture.GetPixels();
                var copy = new Texture2D(texture.width, texture.height, texture.format, false);
                copy.SetPixels(pixels);
                copy.Apply();
                return copy;
            }
            catch (UnityException)
            {
                // GUI.skinなどのリソースはGetPixels不可能なため、RenderTexture経由でコピー
                var rt2 = texture.CreateRenderTexture();
                var tex = rt2.CreateTexture2D();
                UnityEngine.Object.Destroy(rt2);
                return tex;
            }
        }

        /// <summary>
        /// RenderTextureをコピーして返却するExtension
        /// (任意)解像度指定すればリサイズも可能
        /// </summary>
        private static RenderTexture Copy(this RenderTexture rt, int width = 0, int height = 0)
        {
            if (width == 0) width = rt.width;
            if (height == 0) height = rt.height;
            var newRt = new RenderTexture(width, height, 0, rt.format);
            using (new ActivateRenderTexture())
                Graphics.Blit(rt, newRt);
            return newRt;
        }
        
        /// <summary>
        /// RenderTextureをTexture2Dに変換して返却するExtension
        /// (任意)解像度指定すればリサイズも可能
        /// </summary>
        internal static Texture2D CreateTexture2D(this RenderTexture rt, int width = 0, int height = 0)
        {
            if (!rt) return null;

            // サイズ指定がある場合
            if (width > 0 && height > 0)
            {
                var newRt = rt.Copy(width, height);
                rt.Release();
                rt = newRt;
            }

            var texture = new Texture2D(rt.width, rt.height);
            using (new ActivateRenderTexture(rt))
            {
                texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                texture.Apply();
            }
            return texture;
        }

        /// <summary>
        /// Texture2DをRenderTextureに変換して返却するExtension
        /// (任意)解像度指定すればリサイズも可能
        /// </summary>
        internal static RenderTexture CreateRenderTexture(this Texture2D texture, int width = 0, int height = 0)
        {
            if (!texture) return null;
            if (width == 0) width = texture.width;
            if (height == 0) height = texture.height;
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            using (new ActivateRenderTexture())
                Graphics.Blit(texture, rt);
            return rt;
        }

        /// <summary>
        /// Texture2Dを指定解像度にリサイズして返却するExtension
        /// </summary>
        internal static Texture2D Resized(this Texture2D texture, int width, int height)
        {
            if (!texture || (texture.width == width && texture.height == height)) return texture;
            var rt = RenderTexture.GetTemporary(width, height);
            using (new ActivateRenderTexture())
                Graphics.Blit(texture, rt);
            var tex = rt.CreateTexture2D();
            UnityEngine.Object.Destroy(rt);
            return tex;
        }

        /// <summary>
        /// Texture2Dを正方形に変換して返却するExtension
        /// </summary>
        internal static Texture2D Squared(this Texture2D texture)
        {
            if (texture.width == texture.height) return texture;
            // 正方形ではない場合、長辺に合わせて正方形に引き伸ばす
            var size = Math.Max(texture.width, texture.height);
            var rt = texture.CreateRenderTexture(size, size);
            var result = new Texture2D(size, size);
            using (new ActivateRenderTexture(rt))
            {
                result.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                result.Apply();
            }
            UnityEngine.Object.Destroy(rt);
            return result;
        }
       
        /// <summary>
        /// Texture2Dを全体的にアルファ値を強制調整するExtension
        /// Unity GUI のパーツが透明すぎなので不透明化するのに作成
        /// ※小さい画像用
        /// </summary>
        /// <param name="texture">ExtensionベースTexture2D</param>
        /// <param name="alpha">0f=透明化、0.5f=そのまま、1f=不透明化</param>
        internal static void ForceAlpha(this Texture2D texture, float alpha=1f)
        {
            var pixels = texture.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= 0f) continue;
                var target = (alpha < 0.5f) ? 0f : 1f;
                var t = Mathf.Abs(alpha - 0.5f) * 2f; // 0〜1に正規化
                pixels[i].a = Mathf.Lerp(pixels[i].a, target, t);
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }
        
        /// <summary>
        /// Texture2D(グレースケール画像想定)を色彩統一して色付けするExtension
        /// ※小さい画像用
        /// </summary>
        /// <param name="texture">ExtensionベースTexture2D</param>
        /// <param name="color">色付け指定Colorオブジェクト</param>
        internal static void Colorize(this Texture2D texture, Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            var pixels = texture.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0f) continue;
                Color.RGBToHSV(pixels[i], out _, out _, out var v2);
                var v3 = Mathf.Lerp(v, v2, pixels[i].a);
                var newColor = Color.HSVToRGB(h, s, v3, true);
                newColor.a = pixels[i].a;
                pixels[i] = newColor;
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }
       
        /// <summary>
        /// Texture2Dをアルファ前乗算を解除し標準アルファ形式に戻すExtension
        /// ※RenderTextureからReadPixelsで読み取ったものをPNG保存する用
        /// </summary>
        internal static void UnpremultiplyAlpha(this Texture2D texture)
        {
            var pixels = texture.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a == 0f)
                {
                    pixels[i] = new Color(0f, 0f, 0f, 0f);  
                }
                else
                {
                    pixels[i].r /= pixels[i].a;
                    pixels[i].g /= pixels[i].a;
                    pixels[i].b /= pixels[i].a;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }
        
        #endregion
    }
}
