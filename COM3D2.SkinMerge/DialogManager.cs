using System;
using System.Collections.Generic;
using UnityEngine;

namespace COM3D2.SkinMerge
{
	using GS = GuiStyles;
	using FU = FileUtils;
	using static Localization;

	internal class DialogManager
    {
        private static DialogManager _instance;
        internal static DialogManager Instance => _instance ??= new DialogManager();
        private static MergeContext Ctx => SkinMerge.Instance.MergeContext;
        private static ConfigManager Cm => ConfigManager.Instance;
        private static WindowManager Wm => WindowManager.Instance;
        
        internal class Dialog
        {
            internal string Message;
            internal Action<Dialog> GUIFunc;
            internal Action OkFunc;
            internal Action CancelFunc;
            internal bool EnableOk = true;
            internal Rect Rect = new Rect(-10000, -10000, 0, 0);
            internal bool IsInit;
        }
        
        private readonly List<Dialog> _dialogs = new List<Dialog>();
        private readonly int _guiIdBase = SkinMerge.PluginFullName.GetHashCode() + 1;

        internal void OnGUI()
        {
	        foreach (var dialog in _dialogs)
	        {
		        var guiId = _guiIdBase + _dialogs.IndexOf(dialog);
		        dialog.Rect = GUILayout.Window(guiId, dialog.Rect, _ => DialogGuiFunc(dialog, guiId),
			        SkinMerge.PluginName, GS.DialogWindow);
		        GUI.BringWindowToFront(guiId);
		        // マウスクリックイベントを透過させない
		        if (dialog.Rect.Contains(Event.current.mousePosition) && Input.GetMouseButton(0))
		        {
			        Input.ResetInputAxes();
		        }
	        }
        }

        /// <summary>
        /// ダイアログを表示する
        /// </summary>
		internal void ShowDialog(string message, Action okFunc = null, Action cancelFunc = null, Action<Dialog> guiFunc = null)
		{
			_dialogs.Add(new Dialog
			{
				Message = message,
				GUIFunc = guiFunc,
				OkFunc = okFunc,
				CancelFunc = cancelFunc
			});
			Wm.GUIEnabled = false;
		}
		
        /// <summary>
        /// ダイアログのGUI関数
        /// </summary>
		private void DialogGuiFunc(Dialog dialog, int guiId)
		{
			GUI.enabled = true;
			GUILayout.Label(dialog.Message, GS.DialogMessage, GUILayout.MinWidth(400));
			dialog.GUIFunc?.Invoke(dialog);
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				GUI.enabled = dialog.EnableOk;
				if (GUILayout.Button(_L("dlg.btn.ok"), GS.Button))
				{
					dialog.OkFunc?.Invoke();
					CloseDialog(dialog);
				}
				GUI.enabled = true;
				if (dialog.OkFunc != null)
				{
					// OK実行関数がない場合、キャンセルボタンがないタイプとする
					if (GUILayout.Button(_L("dlg.btn.cancel"), GS.Button))
					{
						dialog.CancelFunc?.Invoke();
						CloseDialog(dialog);
					}
				}
				GUILayout.FlexibleSpace();
			}
			if (Event.current.type == EventType.Repaint)
			{
				// 初回描画時ウィンドウ位置調整
				if (!dialog.IsInit)
				{
					if (dialog.Rect.width > 0 && dialog.Rect.height > 0)
					{
						// 1回目のGUIオープン、中央に配置
						dialog.Rect.x = (int)((Screen.width - dialog.Rect.width) / 2f);
						dialog.Rect.y = (int)((Screen.height - dialog.Rect.height) / 2f);
						dialog.IsInit = true;
					}
					// フォーカスを取る
					GUI.FocusWindow(guiId);
					return;
				}
				dialog.Rect.width = 0;
				dialog.Rect.height = 0;
			}
			GUI.DragWindow();
		}
		
        /// <summary>
        /// ダイアログを閉じる
        /// </summary>
		private void CloseDialog(Dialog dialog)
		{
			_dialogs.Remove(dialog);
			if (_dialogs.Count == 0)
				Wm.GUIEnabled = true;
		}

        /// <summary>
        /// 保存ダイアログのGUI関数
        /// </summary>
		internal void SaveDialogGuiFunc(Dialog dialog)
		{
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.Label(_L("dlg.label.menu_prefix") + ": ");
				var preName = Ctx.SaveName;
				Ctx.SaveName = GUILayout.TextField(Ctx.SaveName, 100, GUILayout.ExpandWidth(true));
				if (preName != Ctx.SaveName) Ctx.Restored = false;
			}
			if (Ctx.Restored)
				GUILayout.Label(_L("dlg.msg.overwrite_last"), GS.Info);
			else if (FU.ExistsFolder(Cm.SavePath, Ctx.SaveName))
				GUILayout.Label(_L("dlg.msg.overwrite_exists"), GS.Warning);
			else if (Ctx.ExistsSaveMenu(Ctx.SaveName))
			{
				GUILayout.Label(_L("dlg.msg.exists_same_name"), GS.Warning);
				dialog.EnableOk = false;
			}
			else if (!dialog.EnableOk)
			{
				dialog.EnableOk = true;
			}

			using (new GUILayout.HorizontalScope())
			{
				GUILayout.Label(_L("dlg.label.menu_priority") + ": ");
				var inputPriority = GUILayout.TextField(Ctx.SavePriority?.ToCompactString(), 8);
				if (float.TryParse(inputPriority, out var priority))
					Ctx.SavePriority = priority;
			}
		}

        /// <summary>
        /// 復元ダイアログのGUI関数
        /// </summary>
		internal void RestoreDialogGuiFunc(List<MergeSource> restoreSources)
		{
			using (new GUILayout.VerticalScope())
			{
				const int maxColNum = 2;
				foreach (var chunkSources in restoreSources.ChunkList(maxColNum))
				{
					var isFirst = true;
					using (new GUILayout.HorizontalScope())
					{
						foreach (var src2 in chunkSources)
						{
							if (isFirst)
								isFirst = false;
							else
								GUILayout.Space(60);
							var src1 = src2.Mpn == MPN.acctatoo || src2.Mpn == MPN.hokuro ?
								Ctx.Sources.Find(x => x.MenuFileName == src2.MenuFileName) :
								Ctx.Sources.Find(x => x.Mpn == src2.Mpn);
							GS.GUISourceIcon(src1, GS.ThumbLabel, false);
							GUILayout.Label("▶", GS.Arrow, GUILayout.ExpandHeight(true));
							GS.GUISourceIcon(src2, GS.ThumbLabel, false);
						}
						GUILayout.FlexibleSpace();
					}
				}
			}
		}
    }
}