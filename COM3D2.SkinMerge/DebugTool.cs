using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace COM3D2.SkinMerge
{
    public class DebugTool
    {
        public static void DumpVars(object obj, int depth=0)
        {
            var indent = new string(' ', depth * 2);
            if (obj == null)
            {
                Console.WriteLine(indent + "null");
                return;
            }

            if (depth == 0) Console.WriteLine("==== DumpVars: {0} ====", obj);

            if (obj is ICollection collection)
            {
                foreach (var item in collection)
                {
                    if (item is string str)
                        Console.WriteLine(indent + "    - {0}", str);
                    else
                    {
                        Console.WriteLine(indent + "    -");
                        DumpVars(item, depth + 2);
                    }
                }
            }
            else
            {
                foreach (var field in obj.GetType().GetFields())
                {
                    var value = field.GetValue(obj);
                    if (value is ICollection collection1)
                    {
                        Console.WriteLine(indent + "  - {0}:", field.Name);
                        foreach (var item in collection1)
                            if (item is string str)
                                Console.WriteLine(indent + "    - {0}", str);
                            else
                            {
                                Console.WriteLine(indent + "    -");
                                DumpVars(item, depth + 2);
                            }
                    }
                    else
                        Console.WriteLine(indent + "  - {0} = {1}", field.Name, value);
                }
            }
        }
        
        [HarmonyPatch(typeof(Material), nameof(Material.SetFloat), typeof(string), typeof(float))]
        [HarmonyPostfix]
        private static void Material_SetFloat_Postfix(Material __instance, string name, float value)
        {
            if (__instance.name != "Hidden/BlendForBloom")
                Console.WriteLine($"******************* Material: {__instance.name}, Shader: {__instance.shader.name} SetFloat: {name} = {value}");
        }
        [HarmonyPatch(typeof(Material), nameof(Material.SetInt), typeof(string), typeof(int))]
        [HarmonyPostfix]
        private static void Material_SetInt_Postfix(Material __instance, string name, int value)
        {
            Console.WriteLine($"******************* Material: {__instance.name}, Shader: {__instance.shader.name} SetInt: {name} = {value}");
        }
        [HarmonyPatch(typeof(Material), nameof(Material.SetColor), typeof(string), typeof(Color))]
        [HarmonyPostfix]
        private static void Material_SetColor_Postfix(Material __instance, string name, Color value)
        {
            Console.WriteLine($"******************* Material: {__instance.name}, Shader: {__instance.shader.name} SetColor: {name} = {value}");
        }
        [HarmonyPatch(typeof(Material), nameof(Material.SetColorArray), typeof(string), typeof(Color[]))]
        [HarmonyPostfix]
        private static void Material_SetColor_Postfix(Material __instance, string name, Color[] value)
        {
            Console.WriteLine($"******************* Material: {__instance.name}, Shader: {__instance.shader.name} SetColorArray: {name} = {value}");
        }
        [HarmonyPatch(typeof(Graphics), nameof(Graphics.Blit), typeof(Texture), typeof(RenderTexture), typeof(Material), typeof(int))]
        [HarmonyPostfix]
        private static void Graphics_Blit_Postfix(Texture source, RenderTexture dest, Material mat, int pass)
        {
            //if (mat.name.StartsWith("Infinity"))
            Console.WriteLine($"******************* Graphics.Blit: Source: {source.name}, Dest: {dest.name}, Material: {mat.name}, Shader: {mat.shader.name}");
        }

        [HarmonyPatch(typeof(Shader), nameof(Shader.Find), typeof(string))]
        [HarmonyPostfix]
        private static void Shader_Find_Postfix(Shader __instance, string name)
        {
            Console.WriteLine($"******************* Shader.Find: {name}");
        }
        
    }
}