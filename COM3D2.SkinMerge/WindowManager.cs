using BepInEx.Logging;
using System;
using UnityEngine;

namespace COM3D2.SkinMerge
{
	using GS = GuiStyles;
	using SlotID = TBody.SlotID;
	using static Localization;

	internal class WindowManager
	{
		private static WindowManager _instance;
		internal static WindowManager Instance => _instance ??= new WindowManager();
		private static SkinMerge Sm => SkinMerge.Instance;
		private static MergeContext Ctx => Sm.MergeContext;
		private static ConfigManager Cm => ConfigManager.Instance;
		private static readonly ManualLogSource Log = SkinMerge.Log;
		
		private readonly int _guiId = SkinMerge.PluginFullName.GetHashCode();
		private Rect _guiRect;
		private Vector2? _guiPos;
		private bool _initGui;
		private Vector2 _delayedTooltipPos;
		private float _delayedTooltipTime;
		internal bool GUIEnabled;

		internal void Init()
		{
			_initGui = false;
			_guiRect = new Rect(-10000, -10000, 0, 0);
			GUIEnabled = true;
		}

		internal void OnGUI()
		{
			_guiRect = GUILayout.Window(_guiId, _guiRect, GuiFunc, string.Empty, GS.MainWindow);
			// マウスクリックイベントを透過させない
			if (_guiRect.Contains(Event.current.mousePosition) && Input.GetMouseButton(0))
			{
				Input.ResetInputAxes();
			}
		}

		private bool DelayedTooltipButton(string text, string tooltip, GUIStyle style)
		{
			var content = new GUIContent(text);
			var rect = GUILayoutUtility.GetRect(content, style);
			if (GUI.enabled && rect.Contains(Event.current.mousePosition))
			{
				if (_delayedTooltipPos != Event.current.mousePosition)
				{
					_delayedTooltipPos = Event.current.mousePosition;
					_delayedTooltipTime = 0f;
				}
				else
				{
					_delayedTooltipTime += Time.deltaTime;
					if (_delayedTooltipTime > 0.5f)
						content.tooltip = tooltip;
				}
			}
			return GUI.Button(rect, content, style);
		}

		private void GuiFunc(int id)
		{
			GUI.enabled = GUIEnabled;
			try
			{
				var colNum = Cm.GUIIconsPerRow.Value;
				var skinSize = Cm.GUISkinSize.Value;

				using (new GUILayout.HorizontalScope())
				{
					GUILayout.Label(SkinMerge.PluginTitleName, GS.LabelTitle);
					if (GUILayout.Button("✖", GS.CloseButton))
					{
						Sm.ToggleGUI();
					}
				}
				
				using (new GUILayout.HorizontalScope())
				{
					// 合成対象一覧ペイン(左)
					using (new GUILayout.VerticalScope())
					{
						foreach (var slot in new [] { SlotID.body, SlotID.head })
						{
							var sources = Ctx.GetSources(slot);
							// 合成対象ブロック
							using (new GUILayout.VerticalScope())
							{
								// 合成対象カテゴリラベル(上)
								var paneWidth = 20 + (Cm.GUIIconSize.Value + 5) * Cm.GUIIconsPerRow.Value;
								using (new GUILayout.HorizontalScope(GUILayout.Width(paneWidth)))
								{
									GUILayout.Label(_L($"gui.label.{slot}"), GS.LabelHeader);
								}
								GUILayout.Space(10);
								// 合成対象一覧(下)
								if (sources.Count > 0 && !Ctx.IsRestoring)
								{
									foreach (var chunkSources in sources.ChunkList(colNum))
									{
										// 一覧の一行分
										using (new GUILayout.HorizontalScope())
										{
											GUILayout.Space(20);
											foreach (var src in chunkSources)
											{
												// 合成対象のアイコンボタン
												var gs =
													src.IsDone ? GS.ThumbDone :
													src.IsError ? GS.ThumbError :
													src.IsSelected ? GS.ThumbSelected : GS.ThumbDeselected;
												var isButton = !Ctx.IsMerging && !Ctx.IsMerged;
												GS.GUISourceIcon(src, gs, isButton);
											}
										}
									}
								}
								else
								{
									using (new GUILayout.HorizontalScope())
									{
										GUILayout.Space(20);
										var text = Ctx.IsRestoring ? _L("gui.msg.restoring") : _L("gui.msg.no_items");
										GUILayout.Label(text, GS.PanelMessage);
									}
								}
							}
						}
					}
					GUILayout.Space(20);
					// 合成結果ペイン(中)
					using (new GUILayout.VerticalScope())
					{
						GS.SkinTexture.fixedWidth = GS.SkinTexture.fixedHeight = skinSize;
						foreach (var slot in new[] { SlotID.body, SlotID.head })
						{
							GUILayout.Label(_L($"gui.label.{slot}"), GS.LabelHeader);
				
							using (new GUILayout.HorizontalScope())
							{
								var isFirst = true;
								foreach (var result in Ctx.GetDisplayResults(slot))
								{
									if (!isFirst)
										GUILayout.Label("", GS.TabSpace);
									var gsTab = result.IsSelected ? GS.TabToggled : GS.TabUntoggled;
									if (GUILayout.Button(result.DisplayTexName, gsTab))
										Ctx.SelectResult(slot, result.TexName);
									isFirst = false;
								}
							}
							using (new GUILayout.VerticalScope(GS.TabBox))
							{
								var cr = Ctx.GetSelectedResult(slot);
								if (cr.Texture && !Ctx.IsRestoring)
								{
									GUILayout.Label($"{_L("gui.label.tex_res")}: {cr.Size}");
									GUILayout.Label($"{_L("gui.label.tex_color")}: {cr.Color}");
									GUILayout.Label(new GUIContent(cr.Texture), GS.SkinTexture);
								}
								else
								{
									GUILayout.Label($"{_L("gui.label.tex_res")}: ");
									GUILayout.Label($"{_L("gui.label.tex_color")}: ");
									var text = Ctx.IsRestoring ? _L("gui.msg.restoring") : _L("gui.msg.loading");
									GUILayout.Label(new GUIContent(text), GS.SkinTexture);
								}
							}
							GUILayout.Space(20);
						}
					}
					GUILayout.Space(20);
					// ボタン一覧ペイン(右)
					using (new GUILayout.VerticalScope())
					{
						// skinアイコン
						GUILayout.Label(_L("gui.label.base_skin"), GS.LabelHeader);
						using (new GUILayout.HorizontalScope())
						{
							GS.GUISkinIcon(Ctx.BaseFolderIcon, Ctx.BaseTooltip);
							if (!Ctx.IsRestorable)
							{
								var rect = GUILayoutUtility.GetLastRect();
								var subSize = Cm.GUIIconSize.Value * 2 / 3;
								var subRect = new Rect(rect.xMax - rect.width / 2, rect.yMax - rect.height / 2,
									subSize, subSize);
								var shadowRect = new Rect(subRect.x - 2, subRect.y - 2, subRect.width + 4, subRect.height + 4);
								GUI.DrawTexture(shadowRect, Ctx.BaseIconShadow, ScaleMode.ScaleToFit, true);
								GUI.DrawTexture(subRect, Ctx.BaseIcon, ScaleMode.ScaleToFit, true);
							}
							if (Ctx.IsMerged)
							{
								GUILayout.Space(20);
								GUILayout.Label("▶", GS.Arrow, GUILayout.Height(90));
								GS.GUISkinIcon(Ctx.NewIcon, Ctx.NewTooltip);
							}
							else
							{
								GUILayout.Space(140);
							}
						}
						GUILayout.Space(20);
						
						// カテゴリ選択
						GUILayout.Label(_L("gui.label.filter"), GS.LabelHeader);
						foreach (var filter in Ctx.SourceFilters)
						{
							if (!filter.IsEnable) continue;
							var pre = filter.IsVisible;
							filter.IsVisible = GUILayout.Toggle(filter.IsVisible, filter.Name);
							if (pre != filter.IsVisible)
								Ctx.ChangeFilter(filter);
						}
						GUILayout.Space(20);

						// ボタン一覧
						GUILayout.Label(_L("gui.label.steps"), GS.LabelHeader);
						GUI.enabled = GUIEnabled && Ctx.IsRestorable;
						if (DelayedTooltipButton(_L("gui.btn.restore"), _L("gui.btn.restore.tooltip"), GS.Button))
							Ctx.RestoreConfirm();
						GUI.enabled = GUIEnabled;

						if (!Ctx.IsMerged)
						{
							GUI.enabled = GUIEnabled && !Ctx.IsMerging && Ctx.HasSelected;
							if (DelayedTooltipButton(_L("gui.btn.merge"), _L("gui.btn.merge.tooltip"), GS.Button))
								Ctx.MergeSkinConfirm();
							GUI.enabled = GUIEnabled;
						}
						else
						{
							if (DelayedTooltipButton(_L("gui.btn.revert"), _L("gui.btn.revert.tooltip"), GS.Button))
								Ctx.Revert();
						}

						GUI.enabled = GUIEnabled && Ctx.IsMerged && !Ctx.IsSaved;
						if (DelayedTooltipButton(_L("gui.btn.save"), _L("gui.btn.save.tooltip"), GS.Button))
							Ctx.SaveSkin();
						GUI.enabled = GUIEnabled;

						GUI.enabled = GUIEnabled && Ctx.IsModLoaded;
						if (DelayedTooltipButton(_L("gui.btn.replace"), _L("gui.btn.replace.tooltip"), GS.Button))
							Ctx.Replace();
						GUI.enabled = GUIEnabled;
					}
					GUILayout.Space(20);
				}

				GUILayout.Space(10);

				if (Event.current.type == EventType.Repaint)
				{
					// 初回描画時ウィンドウ位置調整
					if (!_initGui)
					{
						if (_guiPos != null)
						{
							// 2回目以降のGUIオープン、前回の位置に配置
							_guiRect.position = (Vector2)_guiPos;
							_initGui = true;
						}
						else if (_guiRect.width > 0 && _guiRect.height > 0)
						{
							// 1回目のGUIオープン、中央に配置
							_guiRect.x = (int)((Screen.width - _guiRect.width) / 2f);
							_guiRect.y = (int)((Screen.height - _guiRect.height) / 2f);
							_guiPos = _guiRect.position;
							_initGui = true;
						}
						return;
					}

					// ツールチップ描画
					if (GUIEnabled && !string.IsNullOrEmpty(GUI.tooltip))
					{
						var content = new GUIContent(GUI.tooltip);
						var size = GS.Tooltip.CalcSize(content);
						if (size.x > 250)
						{
							var height = GS.Tooltip.CalcHeight(content, 250);
							size = new Vector2(250, height);
						}
						var pos = Event.current.mousePosition;
						if (pos.x + size.x > _guiRect.width)
						    pos.x = _guiRect.width - size.x;
						GUI.Label(new Rect(pos, size), content, GS.Tooltip);
					}
					
					// ウィンドウサイズ再計算
					_guiRect.width = 0;
					_guiRect.height = 0;
				}
				else if (Event.current.type == EventType.MouseUp)
				{
					// ドラッグ終了時、位置を記憶
					_guiPos = _guiRect.position;
				}

				GUI.DragWindow();
			}
			catch (Exception ex)
			{
				Log.LogError("GuiFunc\r\n" + ex);
			}
		}
		
	}
}
