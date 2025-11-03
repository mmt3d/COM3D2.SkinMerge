using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    using SlotID = TBody.SlotID;
    using PARTS_COLOR = MaidParts.PARTS_COLOR;
    using static Localization;

    public static class FileUtils
	{
        private static ConfigManager Cm => ConfigManager.Instance;
        private static SceneEdit SceneEdit => SceneEdit.Instance;
		private static readonly ManualLogSource Log = SkinMerge.Log;
        private static readonly string ModDir = UTY.gameProjectPath + @"\Mod";

        /// <summary>
        /// menuボディ書き込みヘルパークラス
        /// </summary>
		private class MenuBody : List<List<string>>
		{
			public void Add(params string[] args)
			{
				Add(args.ToList());
			}
			public byte[] ToBytes()
			{
				var stream = new MemoryStream();
				var writer = new BinaryWriter(stream);
				foreach (var list in this)
				{
					writer.Write((byte)list.Count);
					foreach (var str in list)
					{
						writer.Write(str);
					}
				}
				writer.Write((byte)0);
				var body = stream.ToArray();
				var stream2 = new MemoryStream();
				var writer2 = new BinaryWriter(stream2);
				writer2.Write(body.Length);
				writer2.Write(body);
				return stream2.ToArray();
			}

			public override string ToString()
			{
				return string.Join("\n", this.Select(x => string.Join("\t", x.ToArray())).ToArray());
			}
		}

        /// <summary>
        /// AFileBaseからbyte[]を返却
        /// </summary>
        private static byte[] ReadAFileBase(string fileName)
        {
            try
            {
                using var aFileBase = GameUty.FileOpen(fileName, GameUty.FileSystem);
                if (aFileBase.IsValid())
                    return aFileBase.ReadAll();
                Log.LogError("コンテナが読めません。 :" + fileName);
                return null;
            }
            catch (Exception ex)
            {
                Log.LogError($"ReadAFileBase Error: file={fileName}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// TEXファイルをTexture2Dで返却
        /// </summary>
        internal static Texture2D LoadTexture(string fileName)
        {
            var buffer = ReadAFileBase(fileName);
            if (buffer == null) return null;
            using var ms = new MemoryStream(buffer);
            using var binaryReader = new BinaryReader(ms, Encoding.UTF8);
            var text = binaryReader.ReadString();
            if (text != "CM3D2_TEX")
            {
                Log.LogError("ヘッダーファイルが不正です。" + text);
                binaryReader.Close();
                return null;
            }

            var version = binaryReader.ReadInt32();
            binaryReader.ReadString();

            var width = 0;
            var height = 0;
            var textureFormat = TextureFormat.ARGB32;
            Rect[] array = null;
            if (1010 <= version)
            {
                if (1011 <= version)
                {
                    var num2 = binaryReader.ReadInt32();
                    if (0 < num2)
                    {
                        array = new Rect[num2];
                        for (var i = 0; i < num2; i++)
                        {
                            var x = binaryReader.ReadSingle();
                            var y = binaryReader.ReadSingle();
                            var width2 = binaryReader.ReadSingle();
                            var height2 = binaryReader.ReadSingle();
                            array[i] = new Rect(x, y, width2, height2);
                        }
                    }
                }

                width = binaryReader.ReadInt32();
                height = binaryReader.ReadInt32();
                textureFormat = (TextureFormat)binaryReader.ReadInt32();
            }

            var num3 = binaryReader.ReadInt32();
            var array2 = binaryReader.ReadBytes(num3);

            if (version == 1000)
            {
                width = (array2[16] << 24) | (array2[17] << 16) | (array2[18] << 8) | array2[19];
                height = (array2[20] << 24) | (array2[21] << 16) | (array2[22] << 8) | array2[23];
            }

            return new TextureResource(width, height, textureFormat, array, array2).CreateTexture2D();
        }

        /// <summary>
        /// MENUファイルをMenuInfoで返却
        /// </summary>
        internal static MenuInfo LoadMenu(string menuFileName, bool debugLocal=false)
        {
            try
            {
                var buffer = debugLocal ? File.ReadAllBytes(menuFileName) : ReadAFileBase(menuFileName);
                if (buffer == null) return null;

                using var reader = new BinaryReader(new MemoryStream(buffer), Encoding.UTF8);
                var fileName = menuFileName.ToLowerInvariant();
                var menu = fileName.StartsWith("mod_") ? LoadMod(reader) : LoadMenu(reader);
                menu.FileName = fileName;
                return menu;
            }
            catch (Exception e)
            {
                Log.LogError($"MENUファイル読み込み中にエラーが発生しました: file={menuFileName}: error={e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// MODファイル版menu情報を読み込み、ベースmenu情報に合体して返却する
        /// </summary>
        private static MenuInfo LoadMod(BinaryReader reader)
        {
            var menu = new MenuInfo();
            if (reader.ReadString() != "CM3D2_MOD") return null;
            reader.ReadInt32();
            menu.IconName = reader.ReadString();
            menu.FileName = reader.ReadString();
            menu.Name = reader.ReadString();
            menu.Category = reader.ReadString();
            menu.Mpn = (MPN)Enum.Parse(typeof(MPN), menu.Category);
            menu.Description = reader.ReadString();
            var colorSetMpn = (MPN)Enum.Parse(typeof(MPN), reader.ReadString());
            if (colorSetMpn != MPN.null_mpn)
            {
                menu.ColorSet = new ColorSetField
                {
                    Mpn = colorSetMpn,
                    FileNamePattern = reader.ReadString()
                };
            }
            var body = reader.ReadString().TrimUTF8Bom();
            foreach (var line in body.Split('\n'))
            {
                var values = line.Trim().Split('\t');
                var key = values[0];
                if (key == "") continue;
                values = values.Skip(1).ToArray();
                switch (key)
                {
                    case "基本アイテム":
                        menu.ModBaseMenu = values[0];
                        break;
                    case "テクスチャ変更":
                        var partsColor = values.Length == 5 ?
                            (PARTS_COLOR)Enum.Parse(typeof(PARTS_COLOR), values[4].ToUpper()) : PARTS_COLOR.NONE;
                        var sharedTexture = menu.TextureChanges.Find(
                            x => x.PartsColor == partsColor && x.FileName == values[3]);
                        var tc = new TextureChange
                        {
                            SlotId = (SlotID)Enum.Parse(typeof(SlotID), values[0]),
                            MatNo = int.Parse(values[1]),
                            TexName = values[2],
                            FileName = values[3],
                            PartsColor = partsColor,
                            FixedTexName = sharedTexture?.TexName ?? values[2]
                        };
                        menu.TextureChanges.Add(tc);
                        break;
                    default:
                        break;
                }
                Console.WriteLine("  " + key + " = " + string.Join(",", values));
            }
            var contentsNum = reader.ReadInt32();
            for (var i = 0; i < contentsNum; i++)
            {
                var fileName = reader.ReadString();
                var bytes = reader.ReadBytes(reader.ReadInt32());
                menu.ModRawData.Add(fileName, bytes);
                Console.WriteLine(fileName + ": " + bytes.Length);
            }
            var baseMenu = LoadMenu(menu.ModBaseMenu);
            baseMenu.OverrideModMenu(menu);
            return baseMenu;
        }

        /// <summary>
        /// menuファイルをMenuInfoで返却する
        /// </summary>
        private static MenuInfo LoadMenu(BinaryReader reader)
        {
            var menu = new MenuInfo();
            if (reader.ReadString() != "CM3D2_MENU") return null;
            reader.ReadInt32();
            menu.FileName = reader.ReadString();
            menu.Name = reader.ReadString();
            menu.Mpn = (MPN)Enum.Parse(typeof(MPN), reader.ReadString());
            menu.Description = reader.ReadString();
            reader.ReadInt32();
            while (true)
            {
                var b = reader.ReadByte();
                var text = string.Empty;
                if (b == 0) break;
                for (var i = 0; i < b; i++)
                    text = text + "\"" + reader.ReadString() + "\"";
                if (string.IsNullOrEmpty(text)) continue;
                var stringList = UTY.GetStringList(text);
                var stringCom = UTY.GetStringCom(text).Trim();
                switch (stringCom)
                {
                    case "category":
                        menu.Category = stringList[1];
                        break;
                    case "priority":
                        menu.Priority = float.Parse(stringList[1]);
                        break;
                    case "name":
                        menu.Name = stringList[1];
                        break;
                    case "icon":
                    case "icons":
                        menu.IconName = stringList[1];
                        break;
                    case "テクスチャ合成":
                        menu.TextureBlends.Add(new TextureBlend
                        {
                            Index = menu.TextureBlends.Count,
                            SlotId = (SlotID)Enum.Parse(typeof(SlotID), stringList[1]),
                            MatNo = int.Parse(stringList[2]),
                            TexName = stringList[3],
                            LayerNo = int.Parse(stringList[4]),
                            FileName = stringList[5],
                            BlendMode = (BlendMode)Enum.Parse(typeof(BlendMode), stringList[6].ToTitleCase()),
                        });
                        break;
                    case "tex":
                    case "テクスチャ変更":
                        var partsColor = stringList.Length == 6 ?
                            (PARTS_COLOR)Enum.Parse(typeof(PARTS_COLOR), stringList[5].ToUpper()) : PARTS_COLOR.NONE;
                        var sharedTexture = menu.TextureChanges.Find(
                            x => x.PartsColor == partsColor && x.FileName == stringList[4]);
                        var tc = new TextureChange
                        {
                            SlotId = (SlotID)Enum.Parse(typeof(SlotID), stringList[1]),
                            MatNo = int.Parse(stringList[2]),
                            TexName = stringList[3],
                            FileName = stringList[4],
                            PartsColor = partsColor,
                            FixedTexName = sharedTexture?.TexName ?? stringList[3]
                        };
                        menu.TextureChanges.Add(tc);
                        break;
                    case "delitem":
                        if (stringList.Length == 2)
                            menu.DelItems.Add(stringList[1]);
                        break;
                    default:
                        break;
                }
            }

            return menu;
        }

        /// <summary>
        /// MENUファイルを保存する(ベースとなるmenuファイルから情報を継承する)
        /// 同時にTexファイルも保存する
        /// </summary>
        /// <param name="basePath">保存先パス</param>
        /// <param name="baseMenuFileName">ベースmenuファイル名</param>
        /// <param name="menu">保存対象MenuInfo</param>
        /// <param name="resources">texファイルで同時保存するテクスチャリスト</param>
		internal static bool SaveMenuInherited(string basePath, string baseMenuFileName, MenuInfo menu, Dictionary<string, Texture2D> resources)
		{
            Directory.CreateDirectory(basePath);
            var hasNew = false;
            foreach (var item in resources ?? new Dictionary<string, Texture2D>())
            {
                var fileName = item.Key;
                var texture = item.Value;
                hasNew |= SaveTexture(basePath, Path.ChangeExtension(fileName, ".tex"), texture);
            }

            var filePath = Path.Combine(basePath, menu.FileName);
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                else
                    hasNew = true;
                
                var buffer = ReadAFileBase(baseMenuFileName);
                using var reader = new BinaryReader(new MemoryStream(buffer), Encoding.UTF8);
                using var file = File.OpenWrite(filePath);
                using var writer = new BinaryWriter(file);
                if (reader.ReadString() != "CM3D2_MENU") throw new InvalidDataException("ヘッダーが不正です。");
                writer.Write("CM3D2_MENU");
                writer.Write(reader.ReadInt32());

                reader.ReadString();
                reader.ReadString();
                reader.ReadString();
                reader.ReadString();
                reader.ReadInt32();

                writer.Write("assets/menu/" + menu.FileName.Replace(".menu", ".txt"));
                writer.Write(menu.Name);
                writer.Write(menu.Mpn.ToString());
                writer.Write(menu.Description);

                var body = new MenuBody();
                var tcList = menu.GetTexChanges();
                while (true)
                {
                    var b = reader.ReadByte();
                    var text = string.Empty;
                    if (b == 0) break;
                    for (var i = 0; i < b; i++)
                        text = text + "\"" + reader.ReadString() + "\"";
                    if (string.IsNullOrEmpty(text)) continue;
                    var stringList = UTY.GetStringList(text);
                    var stringCom = UTY.GetStringCom(text).Trim();
                    switch (stringCom)
                    {
                        case "priority":
                            body.Add("priority", menu.Priority.ToCompactString());
                            break;
                        case "name":
                            body.Add("name", menu.Name);
                            break;
                        case "setumei":
                            body.Add("setumei", menu.Description);
                            break;
                        case "icon":
                        case "icons":
                            body.Add("icons", menu.IconName);
                            break;
                        case "tex":
                        case "テクスチャ変更":
                            body.Add(tcList.TryPop(stringList, out var tc) ? tc.GetMenuArgs() : stringList);
                            break;
                        case "color_set":
                            if (menu.ColorSet != null)
                                body.Add("color_set", menu.ColorSet.Mpn.ToString(), menu.ColorSet.FileNamePattern);
                            else
                                body.Add(stringList);
                            break;
                        default:
                            body.Add(stringList);
                            break;
                    }
                }
                tcList.ForEach(tc => body.Add(tc.GetMenuArgs()));
                writer.Write(body.ToBytes());
                return hasNew;
            }
            catch (Exception e)
            {
                Log.LogError($"MENUファイル保存処理中にエラーが発生しました: file={filePath}: error={e.Message}");
                throw;
            }
		}
		
        /// <summary>
        /// TEXファイルを保存する
        /// </summary>
        /// <param name="basePath">保存先パス</param>
        /// <param name="fileName">ファイル名</param>
        /// <param name="texture">対象Texture2D</param>
        /// <returns></returns>
        private static bool SaveTexture(string basePath, string fileName, Texture2D texture)
        {
            var filePath = Path.Combine(basePath, fileName);
            var isNew = false;
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
                else
                    isNew = true;

                using var writer = new BinaryWriter(File.OpenWrite(filePath));
                var png = texture.EncodeToPNG();
                writer.Write("CM3D2_TEX");
                writer.Write(1010);
                writer.Write("");
                writer.Write(texture.width);
                writer.Write(texture.height);
                writer.Write(5);
                writer.Write(png.Length);
                writer.Write(png);
                return isNew;
            }
            catch (Exception e)
            {
                Log.LogError($"TEXファイル保存処理中にエラーが発生しました: file={filePath}: error={e.Message}");
                throw;
            }
        }

        /// <summary>
        /// 指定セーブディレクトリとModディレクトリから指定ファイル名のバックアップXMLファイルパスを検索する
        /// </summary>
        /// <param name="saveDir">対象menuファイルの保存パス(検索順の優先指定)</param>
        /// <param name="fileName">対象menuファイル名</param>
        internal static string SearchBackupXmlPath(string saveDir, string fileName)
        {
            var xmlFileName = Path.ChangeExtension(fileName, ".skmg.xml").ToLowerInvariant();
            string path = null;
            try
            {
                foreach (var dir in new[] { saveDir, ModDir })
                {
                    if (!Directory.Exists(dir)) continue;
                    path = Directory.GetFiles(dir, "*.skmg.xml", SearchOption.AllDirectories)
                        .FirstOrDefault(x => Path.GetFileName(x).ToLowerInvariant() == xmlFileName);
                    if (path != null) break;
                }
            }
            catch (Exception e)
            {
                Log.LogError($"XMLファイル検索処理中にエラーが発生しました: file={fileName}: error={e.Message}");
            }
            return path;
        }

        /// <summary>
        /// 指定XMLファイルからバックアップ構成情報を読み込む
        /// </summary>
        internal static Backup LoadBackup(string filePath)
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Backup));
                using var reader = new StreamReader(filePath);
                return (Backup)serializer.Deserialize(reader);
            }
            catch (Exception e)
            {
                Log.LogError($"リストア構成の読み込みに失敗しました: {e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 指定XMLファイルにバックアップ構成情報を保存する
        /// </summary>
        internal static void SaveBackup(string filePath, Backup backup)
        {
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);

                var serializer = new XmlSerializer(typeof(Backup));
                using var writer = new StreamWriter(filePath);
                serializer.Serialize(writer, backup);
            }
            catch (Exception e)
            {
                Log.LogError($"リストア構成ファイル保存処理中にエラーが発生しました: file={filePath}: error={e.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 乳首削除用MENUファイルを生成する
        /// </summary>
        internal static void GenerateDelNippleMenu(string basePath, string fileName)
        {
            Directory.CreateDirectory(basePath);
            var filePath = Path.Combine(basePath, fileName);
            try
            {
                using var file = File.OpenWrite(filePath);
                using var writer = new BinaryWriter(file);
                // メニュー名にスペースが入ってはいけない
                var name = _L("menu.name.del_nipple").Replace(" ", "_");
                var desc = $"Generated by {SkinMerge.PluginName} {SkinMerge.PluginVersion}";
                writer.Write("CM3D2_MENU");
                writer.Write(1000);
                writer.Write("assets/menu/" + fileName.Replace(".menu", ".txt"));
                writer.Write(name);
                writer.Write(nameof(MPN.chikubi));
                writer.Write(desc);
                var body = new MenuBody
                {
                    { "メニューフォルダ", "BODY" },
                    { "category", nameof(MPN.chikubi) },
                    { "属性追加", "常時メニュー表示" },
                    { "icons", "_I_del.tex" },
                    { "priority", "-1" },
                    { "name", name },
                    { "setumei", desc },
                    { "onclickmenu" },
                    { "delitem", nameof(MPN.chikubi) },
                    { "delitem", nameof(MPN.chikubicolor) },
                    { "テクスチャ合成", "body", "0", "_MainTex", "5", "Tatoo_del.tex", "Alpha" },
                    { "テクスチャ合成", "body", "0", "_ShadowTex", "5", "Tatoo_del.tex", "Alpha" }
                };
                writer.Write(body.ToBytes());
            }
            catch (Exception e)
            {
                Log.LogError($"MENUファイル保存処理中にエラーが発生しました: file={filePath}: error={e.Message}");
                throw;
            }
        }

        /// <summary>
        /// 指定パスに指定フォルダが存在するかどうか
        /// </summary>
        internal static bool ExistsFolder(string basePath, string folderName)
        {
            return Directory.Exists(Path.Combine(basePath, folderName));
        }

        /// <summary>
        /// 指定メニューファイル名が存在するかどうか
        /// </summary>
        internal static bool ExistsMenu(string menuFileName)
        {
            var rid = menuFileName.ToLowerInvariant().GetHashCode();
            return SceneEdit.m_menuRidDic.ContainsKey(rid);
        }

        /// <summary>
        /// 指定RIDからmenuファイルを特定しMenuInfoで返却する(削除用メニューは除外)
        /// </summary>
        internal static MenuInfo GetProperMenuFromRid(int rid)
        {
            SceneEdit.m_menuRidDic.TryGetValue(rid, out var menu);
            if (menu == null || menu.m_boDelOnly) return null;
            // 削除用メニューなのになぜかm_boDelOnlyがtrueになってないもの
            if (Cm.DelMenuMap.Values.Contains(menu.m_strMenuFileName)) return null;
            return LoadMenu(menu.m_strMenuFileName);
        }
    }

    internal static class MaidPropExtensions
    {
        /// <summary>
        /// MaidPropから対応するMenuInfoを取得するExtensionメソッド
        /// </summary>
        internal static MenuInfo GetMenu(this MaidProp prop)
        {
            return FileUtils.GetProperMenuFromRid(prop.nFileNameRID);
        }

        /// <summary>
        /// SubPropから対応するMenuInfoを返却するExtensionメソッド
        /// </summary>
        internal static MenuInfo GetMenu(this SubProp prop)
        {
            return FileUtils.GetProperMenuFromRid(prop.nFileNameRID);
        }
    }
}
