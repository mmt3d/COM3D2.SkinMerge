using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace COM3D2.SkinMerge.Patcher
{
    using Managed;

    public static class SkinMergePatcher
    {
        public const string SubPluginFullName = "COM3D2.SkinMerge.Patcher";

        /// <summary>
        /// 全パッチ適用
        /// </summary>
        public static void Patch(AssemblyDefinition assembly)
        {
            var dllDir = Path.GetDirectoryName(typeof(SkinMergeManaged).Assembly.Location);
            if (dllDir == null) return;
            var hookDllPath = Path.Combine(dllDir, "COM3D2.SkinMerge.Managed.dll");
            var hookAssembly = AssemblyDefinition.ReadAssembly(hookDllPath);
            var hookType = hookAssembly.MainModule.GetType("COM3D2.SkinMerge.Managed.SkinMergeManaged");

            PatchMaidSetProp(assembly.MainModule, hookType);
            PatchMaidSetSubProp(assembly.MainModule, hookType);
            PatchMaidSubPropAlpha(assembly.MainModule, hookType);
            PatchTBodyMulTexProc(assembly.MainModule, hookType);
            PatchInfinityColorTextureCache(assembly.MainModule, hookType);
        }

        /// <summary>
        /// Maid.SetPropのパッチ(prefix)
        /// </summary>
        private static void PatchMaidSetProp(ModuleDefinition module, TypeDefinition hookType)
        {
            // フックメソッドの取得
            var hookMethod = hookType.Methods.FirstOrDefault(m => m.Name == "Maid_SetProp_Prefix");
            var hookRef = module.ImportReference(hookMethod);
            
            // ターゲットメソッドの取得
            var cacheType = module.GetType("Maid");
            var targetMethod = cacheType.Methods.First(m =>
                    m.Name == "SetProp" && 
                    m.Parameters.Count == 5 &&
                    m.Parameters[0].ParameterType.FullName == "MaidProp" &&
                    m.Parameters[1].ParameterType.Name == "String" &&
                    m.Parameters[2].ParameterType.Name == "Int32" &&
                    m.Parameters[3].ParameterType.Name == "Boolean" &&
                    m.Parameters[4].ParameterType.Name == "Boolean");
#if DEBUG
            WriteLog($"  PATCHING: {targetMethod.FullName} => {hookRef.FullName}");
#endif

            // 挿入ターゲットは先頭
            var pos = targetMethod.Body.Instructions.First();

            // Prefix 引数順でプッシュ
            var il = targetMethod.Body.GetILProcessor();
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_3));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg, 4));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg, 5));

            // Prefix 関数を呼び出し
            il.InsertBefore(pos, il.Create(OpCodes.Call, hookRef));
        }
       
        /// <summary>
        /// Maid.SetSubPropのパッチ(postfix)
        /// </summary>
        private static void PatchMaidSetSubProp(ModuleDefinition module, TypeDefinition hookType)
        {
            // フックメソッドの取得
            var hookMethod = hookType.Methods.FirstOrDefault(m => m.Name == "Maid_SetSubProp_Postfix");
            var hookRef = module.ImportReference(hookMethod);
            
            // ターゲットメソッドの取得
            var cacheType = module.GetType("Maid");
            var targetMethod = cacheType.Methods.First(m => m.Name == "SetSubProp");
#if DEBUG
            WriteLog($"  PATCHING: {targetMethod.FullName} => {hookRef.FullName}");
#endif

            // 挿入ターゲットは、最後の ret 命令の直前
            var pos = targetMethod.Body.Instructions.Last();
            if (pos.OpCode != OpCodes.Ret) return;

            // Postfix 引数順でプッシュ
            var il = targetMethod.Body.GetILProcessor();
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_3));

            // Postfix 関数を呼び出し
            il.InsertBefore(pos, il.Create(OpCodes.Call, hookRef));
        }
       
        /// <summary>
        /// Maid.SubPropAlphaのパッチ(postfix)
        /// </summary>
        private static void PatchMaidSubPropAlpha(ModuleDefinition module, TypeDefinition hookType)
        {
            // フックメソッドの取得
            var hookMethod = hookType.Methods.FirstOrDefault(m => m.Name == "Maid_SubPropAlpha_Postfix");
            var hookRef = module.ImportReference(hookMethod);
            
            // ターゲットメソッドの取得
            var cacheType = module.GetType("Maid");
            var targetMethod = cacheType.Methods.First(m => m.Name == "SubPropAlpha");
#if DEBUG
            WriteLog($"  PATCHING: {targetMethod.FullName} => {hookRef.FullName}");
#endif

            // 挿入ターゲットは、最後の ret 命令の直前
            var pos = targetMethod.Body.Instructions.Last();
            if (pos.OpCode != OpCodes.Ret) return;

            // Postfix 引数順でプッシュ
            var il = targetMethod.Body.GetILProcessor();
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_2));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_3));

            // Postfix 関数を呼び出し
            il.InsertBefore(pos, il.Create(OpCodes.Call, hookRef));
        }
       
        /// <summary>
        /// TBody.MulTexProcのパッチ(postfix)
        /// </summary>
        private static void PatchTBodyMulTexProc(ModuleDefinition module, TypeDefinition hookType)
        {
            // フックメソッドの取得
            var hookMethod = hookType.Methods.FirstOrDefault(m => m.Name == "TBody_MulTexProc_Postfix");
            var hookRef = module.ImportReference(hookMethod);
            
            // ターゲットメソッドの取得
            var cacheType = module.GetType("TBody");
            var targetMethod = cacheType.Methods.First(m =>
                m.Name == "MulTexProc" && m.Parameters.Count == 1);
#if DEBUG
            WriteLog($"  PATCHING: {targetMethod.FullName} => {hookRef.FullName}");
#endif

            // 挿入ターゲットは、最後の ret 命令の直前
            var pos = targetMethod.Body.Instructions.Last();
            if (pos.OpCode != OpCodes.Ret) return;

            // Postfix 引数順でプッシュ
            var il = targetMethod.Body.GetILProcessor();
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_1));

            // Postfix 関数を呼び出し
            il.InsertBefore(pos, il.Create(OpCodes.Call, hookRef));
        }
        
        /// <summary>
        /// InfinityColorTextureCache.UpdateColorのパッチ(postfix)
        /// </summary>
        private static void PatchInfinityColorTextureCache(ModuleDefinition module, TypeDefinition hookType)
        {
            // フックメソッドの取得
            var hookMethod = hookType.Methods.FirstOrDefault(
                m => m.Name == "InfinityColorTextureCache_UpdateColor_Postfix");
            var hookRef = module.ImportReference(hookMethod);

            // ターゲットメソッドの取得
            var cacheType = module.GetType("InfinityColorTextureCache");
            var targetMethod = cacheType.Methods.First(m => m.Name == "UpdateColor");
#if DEBUG
            WriteLog($"  PATCHING: {targetMethod.FullName} => {hookRef.FullName}");
#endif

            // 挿入ターゲットは、最終的な ret 命令の直前
            var pos = targetMethod.Body.Instructions.Last();
            if (pos.OpCode != OpCodes.Ret) return;
            
            // ローカル変数 (bool, result) を定義
            var resultVar = new VariableDefinition(module.TypeSystem.Boolean);
            targetMethod.Body.Variables.Add(resultVar);
   
            // 戻り値をローカル変数に格納
            var il = targetMethod.Body.GetILProcessor();
            il.InsertBefore(pos, il.Create(OpCodes.Stloc, resultVar));

            // Postfix 引数順でプッシュ
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(pos, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(pos, il.Create(OpCodes.Ldloc, resultVar));

            // Postfix 関数を呼び出し
            il.InsertBefore(pos, il.Create(OpCodes.Call, hookRef));

            // ローカル変数の値 (元の戻り値) をスタックに戻す
            il.InsertBefore(pos, il.Create(OpCodes.Ldloc, resultVar));
        }
        
#if DEBUG
        private static void WriteLog(string message)
        {
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "patcher_debug.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " [Patcher] " + message + Environment.NewLine);
            }
            catch {}
        }
#endif
    }
}