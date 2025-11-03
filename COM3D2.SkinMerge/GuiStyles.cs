using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.SkinMerge
{
	using GU = GraphicUtils;
	using static Localization;

	public static class GuiStyles
    {
	    private static ConfigManager Cm => ConfigManager.Instance;
	    private static readonly Dictionary<MPN, GUIStyle> MpnLabelStyle = new Dictionary<MPN, GUIStyle>();

	    /// <summary>
	    /// 合成元アイテムのサムネイルスタイルを返却する
	    /// </summary>
	    /// <param name="normal">通常時の背景色</param>
	    /// <param name="hover">マウスホバー時の背景色</param>
		private static GUIStyle GetThumbStyle(Color normal, Color? hover = null)
		{
			var bgTexture = GU.GetIconBgTexture(normal);
			var bgTextureHover = hover != null ? GU.GetIconBgTexture(hover.Value) : bgTexture;
			return new GUIStyle("button")
			{
				alignment = TextAnchor.MiddleCenter,
				fixedWidth = 90,
				fixedHeight = 90,
				padding = new RectOffset(5, 5, 5, 5),
				margin = new RectOffset(5, 5, 0, 0),
				normal = { background = bgTexture },
				hover = { background = bgTextureHover }
			};
		}

	    /// <summary>
	    /// 合成元アイテムのMPNラベルスタイルを返却する(MPNで背景色が変化する)
	    /// </summary>
		private static GUIStyle GetMpnLabelStyle(MPN mpn)
		{
			if (MpnLabelStyle.TryGetValue(mpn, out var guiStyle))
				return guiStyle;

			var hue = (float)ConfigManager.PrimaryMpn.IndexOf(mpn) / ConfigManager.PrimaryMpn.Count;
			var sat = hue < 0 ? 0f : 0.6f;
			var color = Color.HSVToRGB(hue, sat, 0.4f);
			color.a = 0.8f;
			guiStyle = new GUIStyle("label")
			{
				wordWrap = false,
				clipping = TextClipping.Clip,
				alignment = TextAnchor.UpperCenter,
				margin = new RectOffset(5, 5, 5, 0),
				normal = { background = GU.GetBgColor(color) }
			};

			MpnLabelStyle.Add(mpn, guiStyle);
			return guiStyle;
		}

	    /// <summary>
	    /// タブ表現のスタイルを返却する
	    /// </summary>
	    /// <param name="active">選択中のタブかどうか</param>
	    /// <param name="tab">タブかコンテンツボックスか</param>
		private static GUIStyle GetTabStyle(bool active, bool tab)
		{
			var borderBottom = tab ? active ? 0 : 1 : 1;
			var borderTop = tab ? 1 : 0;
			var border = new RectOffset(1, 1, borderTop, borderBottom);
			var nColor = active ? new Color(0,0,0,0f) : new Color(0f,0f,0f,0.4f);
			var nTex = GU.GetBorderedTexture(nColor, Color.black, border);
			if (tab)
			{
				Texture2D hTex, aTex;
				Color textColor;
				if (active)
				{
					hTex = aTex = nTex;
					textColor = Color.white;
				}
				else
				{
					var hColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);
					var aColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
					hTex = GU.GetBorderedTexture(hColor, Color.black, border);
					aTex = GU.GetBorderedTexture(aColor, Color.black, border);
					textColor = new Color(1f, 1f, 1f, 0.5f);
				}
				return new GUIStyle("button")
				{
					alignment = TextAnchor.LowerCenter,
					padding = new RectOffset(3, 3, 5, 5),
					margin = new RectOffset(0, 0, 0, 0),
					border = border,
					normal = { background = nTex, textColor = textColor },
					hover = { background = hTex, textColor = textColor },
					active = { background = aTex, textColor = textColor }
				};
			}
			else
			{
				return new GUIStyle("box")
				{
					margin = new RectOffset(0, 0, 0, 0),
					border = border,
					normal = { background = nTex }
				};
			}
		}

	    /// <summary>
	    /// タブ間のスペーススタイルを返却する
	    /// </summary>
		private static GUIStyle GetTabSpaceStyle()
		{
			var border = new RectOffset(0, 0, 0, 1);
			var nTex = GU.GetBorderedTexture(Color.clear, Color.black, border);
			return new GUIStyle("box")
			{
				fixedWidth = 2,
				padding = new RectOffset(0, 0, 5, 5),
				margin = new RectOffset(0, 0, 0, 0),
				border = border,
				normal = { background = nTex }
			};
		}

	    /// <summary>
	    /// GUI.skin.windowの背景テクスチャを調整して返却する
	    /// </summary>
	    /// <param name="state">GUI.skin.windowの対象state</param>
	    /// <param name="color">色味を付ける指定色</param>
	    /// <param name="alpha">0f=透明化、0.5f=そのまま、1f=不透明化</param>
	    /// <param name="expandHeight">ヘッダ部の高さ増加指定</param>
		private static Texture2D GetWindowBg(GUIStyleState state, Color color, float alpha = 0.5f, int expandHeight = 0)
		{
			var tex = state.background.Copy();
			if (expandHeight > 0)
			{
				const int headerH = 17;
				var w = tex.width;
				var h = tex.height;
				var headerPixels = tex.GetPixels(0, 0, w, headerH);
				var repeatPixels = tex.GetPixels(0, headerH - 1, w, 1);
				var bodyPixels = tex.GetPixels(0, headerH, w, h - headerH);
				var newTex = new Texture2D(w, h + expandHeight);
				newTex.SetPixels(0, 0, w, headerH, headerPixels);
				for (var y = headerH; y < headerH + expandHeight; y++)
					newTex.SetPixels(0, y, w, 1, repeatPixels);
				newTex.SetPixels(0, headerH + expandHeight, w, h - headerH, bodyPixels);
				newTex.Apply();
				newTex.filterMode = FilterMode.Point;
				Object.Destroy(tex);
				tex = newTex;
			}
			tex.Colorize(color);
			tex.ForceAlpha(alpha);
			return tex;
		}

		private static Color MainColor => new Color(0.4f, 0.4f, 0.5f);
		private static Color DialogColor => new Color(0.3f, 0.3f, 0.38f);
		
		internal static readonly GUIStyle MainWindow = new GUIStyle("window")
		{
			alignment = TextAnchor.UpperLeft,
			border = new RectOffset(8, 8, 28, 8),
			padding = new RectOffset(5, 5, 5, 5),
			normal = { background = GetWindowBg(GUI.skin.window.normal, MainColor, 0.95f, 10) },
			onNormal = { background = GetWindowBg(GUI.skin.window.onNormal, MainColor, 0.95f, 10) },
		};

		internal static readonly GUIStyle DialogWindow = new GUIStyle("window")
		{
			normal = { background = GetWindowBg(GUI.skin.window.normal, DialogColor, 1f) },
			onNormal = { background = GetWindowBg(GUI.skin.window.onNormal, DialogColor, 1f) },
		};

		internal static readonly GUIStyle LabelTitle = new GUIStyle("label")
		{
			fontSize = 14,
			fontStyle = FontStyle.Bold,
			margin = new RectOffset( 8, 0, 1, 0 ),
			padding = new RectOffset( 0, 0, 0, 0 ),
		};

		internal static readonly GUIStyle LabelHeader = new GUIStyle("label")
		{
			fontSize = 14,
			fontStyle = FontStyle.Bold,
			padding = new RectOffset( 0, 0, 10, 0 ),
		};
		private static readonly GUIStyle LabelAlpha = new GUIStyle("label")
		{
			fontSize = 11,
			wordWrap = false,
			clipping = TextClipping.Clip,
			alignment = TextAnchor.UpperCenter,
			margin = new RectOffset( 0, 5, 0, 5 ),
			normal = { background = GU.GetBgColor(new Color(0, 0, 0, 0.3f)) }
		};
		private static readonly GUIStyle LabelMode1 = new GUIStyle("label")
		{
			fontSize = 11,
			wordWrap = false,
			clipping = TextClipping.Clip,
			alignment = TextAnchor.UpperCenter,
			margin = new RectOffset( 5, 0, 0, 5 ),
			normal =
			{
				background = GU.GetBgColor(new Color(1, 1, 1, 0.7f)),
				textColor = Color.black
			}
		};
		private static readonly GUIStyle LabelMode2 = new GUIStyle("label")
		{
			fontSize = 11,
			wordWrap = false,
			clipping = TextClipping.Clip,
			alignment = TextAnchor.UpperCenter,
			margin = new RectOffset( 5, 0, 0, 5 ),
			normal =
			{
				background = GU.GetBgColor(new Color(0, 0, 0, 0.7f)),
				textColor = Color.white
			}
		};

		internal static readonly GUIStyle Tooltip = new GUIStyle("label")
		{
			fontSize = 12,
			wordWrap = true,
			padding = new RectOffset(15, 15, 10, 10),
			normal = { background = GU.GetBgColor(new Color(0, 0, 0, 0.75f)) }
		};

		internal static readonly GUIStyle DialogMessage = new GUIStyle("label")
		{
			fontSize = 16,
			padding = new RectOffset(20, 20, 20, 20),
			border = new RectOffset(1, 1, 1, 1),
			normal = { background = GU.GetBorderedTexture(
				new Color(0.2f, 0.2f, 0.2f, 0.8f), Color.gray,
				new RectOffset(1, 1, 1, 1)) }
		};

		internal static readonly GUIStyle PanelMessage = new GUIStyle("label")
		{
			padding = new RectOffset(15, 15, 15, 15),
			margin = new RectOffset( 10, 10, 0, 0 ),
			border = new RectOffset(1, 1, 1, 1),
			normal = { background = GU.GetBorderedTexture(
				new Color(0.2f, 0.2f, 0.2f, 0.8f), Color.gray,
				new RectOffset(1, 1, 1, 1)) }
		};

		internal static readonly GUIStyle Warning = new GUIStyle("label")
		{
			normal = { textColor = new Color(0.8f, 0.2f, 0.2f, 1) }
		};
		
		internal static readonly GUIStyle Info = new GUIStyle("label")
		{
			normal = { textColor = new Color(0f, 0.6f, 0f, 1) }
		};

		internal static readonly GUIStyle Arrow = new GUIStyle("label")
		{
			fontSize = 32,
			alignment = TextAnchor.MiddleCenter,
			padding = new RectOffset(10, 10, 10, 10),
		};

		internal static readonly GUIStyle Button = new GUIStyle("button")
		{
			alignment = TextAnchor.LowerCenter,
			padding = new RectOffset( 0, 0, 10, 10 ),
			fixedWidth = 150
		};
		
		internal static readonly GUIStyle TabToggled = GetTabStyle(true, true);
		internal static readonly GUIStyle TabUntoggled = GetTabStyle(false, true);
		internal static readonly GUIStyle TabBox = GetTabStyle(true, false);
		internal static readonly GUIStyle TabSpace = GetTabSpaceStyle();
		internal static readonly GUIStyle ThumbSelected = GetThumbStyle(Color.white, GU.GrayColor(0.8f));
		internal static readonly GUIStyle ThumbDeselected = GetThumbStyle(GU.GrayColor(0.2f), GU.GrayColor(0.4f));
		internal static readonly GUIStyle ThumbDone = GetThumbStyle(new Color(0.4f, 1f, 0.4f));
		internal static readonly GUIStyle ThumbError = GetThumbStyle(Color.red);
		private static readonly GUIStyle ThumbSkin = GetThumbStyle(Color.white);

		internal static readonly GUIStyle SkinTexture = new GUIStyle("label")
		{
			alignment = TextAnchor.MiddleCenter,
			fontSize = 24,
			fixedWidth = 300,
			fixedHeight = 300,
		};

		internal static readonly GUIStyle CloseButton = new GUIStyle("button")
		{
			alignment = TextAnchor.MiddleCenter,
			fixedWidth = 20,
			fixedHeight = 20,
			margin = new RectOffset(0, 0, 0, 0),
		};

		internal static readonly GUIStyle ThumbLabel = new GUIStyle("label")
		{
			alignment = TextAnchor.MiddleCenter,
			fixedWidth = 90,
			fixedHeight = 90,
			padding = new RectOffset(5, 5, 5, 5),
			margin = new RectOffset(5, 5, 0, 0),
		};

		internal static readonly GUIStyle ConfigLabel = new GUIStyle("label")
		{
			wordWrap = true,
			fontSize = 11,
			stretchWidth = true
		};
		
		internal static readonly GUIStyle ConfigFancyToggle = new GUIStyle("toggle")
		{
			fontSize = 11,
			alignment = TextAnchor.MiddleLeft,
			fixedHeight = 50,
			fixedWidth = 250,
			padding = new RectOffset( 5, 0, 5, 5 ),
			margin = new RectOffset(0, 0, 0, 0),
			stretchWidth = true
		};

		/// <summary>
		/// 合成元アイテム1個分の描画
		/// </summary>
		/// <param name="src">合成元アイテム情報</param>
		/// <param name="gs">背景(枠)種別スタイル</param>
		/// <param name="isButton">ボタン有効化指定</param>
		internal static void GUISourceIcon(MergeSource src, GUIStyle gs, bool isButton)
		{
			var iconSize = Cm.GUIIconSize.Value;
			gs.fixedWidth = gs.fixedHeight = iconSize;
			using (new GUILayout.VerticalScope(GUILayout.Width(iconSize)))
			{
				GUILayout.Label(_L(src.Mpn), GetMpnLabelStyle(src.Mpn), GUILayout.Width(iconSize));
				if (!isButton)
					GUILayout.Label(new GUIContent(src.Icon, src.Tooltip), gs);
				else
					if (GUILayout.Button(new GUIContent(src.Icon, src.Tooltip), gs))
						src.Toggle();
				using (new GUILayout.HorizontalScope())
				{
					var gsBlend = src.BlendMode == BlendMode.Alpha ? LabelMode1 : LabelMode2;
					GUILayout.Label(src.FixedBlendModeShort, gsBlend, GUILayout.Width(iconSize * 0.6f));
					GUILayout.Label(src.FixedAlpha, LabelAlpha, GUILayout.Width(iconSize * 0.4f));
				}
			}
		}

		/// <summary>
		/// スキンメニューのアイコン描画
		/// </summary>
		internal static void GUISkinIcon(Texture2D icon, string tooltip)
		{
			var iconSize = Cm.GUIIconSize.Value;
			ThumbSkin.fixedWidth = ThumbSkin.fixedHeight = iconSize;
			using (new GUILayout.VerticalScope(GUILayout.Width(iconSize)))
			{
				GUILayout.Label(new GUIContent(icon, tooltip), ThumbSkin);
			}
		}
        
    }
}