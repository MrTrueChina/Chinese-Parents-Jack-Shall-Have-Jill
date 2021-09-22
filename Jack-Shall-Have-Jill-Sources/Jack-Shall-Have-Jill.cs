using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
using System.Reflection;
using GameAnalyticsSDK;

namespace MtC.Mod.ChineseParents.JackShallHaveJill
{
    /// <summary>
    /// 这个 Mod 的设置
    /// </summary>
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        /// <summary>
        /// 最大好感度，达到这个好感度则 100% 求婚成功
        /// </summary>
        [Draw("最大好感度 - Max Loving")]
        public int maxLoving = 150;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
        }
    }

    public static class Main
    {
        /// <summary>
        /// Mod 对象
        /// </summary>
        public static UnityModManager.ModEntry ModEntry { get; set; }

        /// <summary>
        /// 这个 Mod 是否启动
        /// </summary>
        public static bool enabled;

        /// <summary>
        /// 这个 Mod 的设置
        /// </summary>
        public static Settings settings;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            // 读取设置
            settings = Settings.Load<Settings>(modEntry);

            // 保存 Mod 对象
            ModEntry = modEntry;
            ModEntry.OnToggle = OnToggle;
            ModEntry.OnGUI = OnGUI;
            ModEntry.OnSaveGUI = OnSaveGUI;

            // 加载 Harmony
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll();

            modEntry.Logger.Log("有情人终成眷属 Mod 加载完成");

            // 返回加载成功
            return true;
        }

        /// <summary>
        /// Mod Manager 对 Mod 进行控制的时候会调用这个方法
        /// </summary>
        /// <param name="modEntry"></param>
        /// <param name="value">这个 Mod 是否激活</param>
        /// <returns></returns>
        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            // 将 Mod Manager 切换的状态保存下来
            enabled = value;

            // 返回 true 表示这个 Mod 切换到 Mod Manager 切换的状态，返回 false 表示 Mod 依然保持原来的状态
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Draw(modEntry);
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            // 保存设置
            settings.Save(modEntry);
        }
    }

    ////////--------////////--------//////// 修改好感度上限 ////////--------////////--------////////

    /// <summary>
    /// 女生面板获取最大好感值的方法
    /// </summary>
    [HarmonyPatch(typeof(XmlData), "GetInt", new Type[] { typeof(string) })]
    public static class XmlData_GetInt
    {
        private static void Postfix(ref int __result, string name)
        {
            // 如果 Mod 未启动则不作处理
            if (!Main.enabled)
            {
                return;
            }

            // 如果查询的不是 max_loving，那就不是这个 Mod 需要修改的，不作处理
            if (!("max_loving".Equals(name)))
            {
                return;
            }

            Main.ModEntry.Logger.Log("获取最大好感度方法调用完毕");

            // 修改返回值为 Mod 的最大好感度
            __result = Main.settings.maxLoving;
        }
    }

    /// <summary>
    /// 改变男生好感度的方法
    /// </summary>
    [HarmonyPatch(typeof(BoysManager), "ChangeLoving")]
    public static class BoysManager_ChangeLoving
    {
        /// <summary>
        /// 前缀向后缀传递的参数类
        /// </summary>
        public class PrefixToPostfixParams
        {
            /// <summary>
            /// 改变男生好感度方法调用前的这个男生的好感度
            /// </summary>
            public int beforeLoving;

            public PrefixToPostfixParams(int beforeLoving)
            {
                this.beforeLoving = beforeLoving;
            }
        }

        private static void Prefix(out PrefixToPostfixParams __state, BoysManager __instance, int boyId, int loving)
        {
            // 将方法调用前的这个男同学的好感度发给后缀
            __state = new PrefixToPostfixParams(__instance.BoysDictionary[boyId].loving);
        }

        private static void Postfix(PrefixToPostfixParams __state, BoysManager __instance, int boyId, int loving)
        {
            // 如果 Mod 未启动则不作处理
            if (!Main.enabled)
            {
                return;
            }

            Main.ModEntry.Logger.Log("修改男同学最大好感度方法调用完毕");

            // 将好感度改为 方法调用前的好感度 + 增加的好感度，以 Mod 的好感度上限为上限
            __instance.BoysDictionary[boyId].loving = Mathf.Min(Main.settings.maxLoving, __state.beforeLoving + loving);
        }
    }

    ////////--------////////--------//////// 修改求婚成功率 ////////--------////////--------////////

    /// <summary>
    /// 获取所有相亲选项的方法
    /// </summary>
    [HarmonyPatch(typeof(Panel_blinddate), "create_blinddates")]
    public static class Panel_blinddate_create_blinddates
    {
        private static void Postfix(Panel_blinddate __instance)
        {
            // 如果 Mod 未启动则不处理
            if (!Main.enabled)
            {
                return;
            }

            Main.ModEntry.Logger.Log("在相亲列表中添加同学方法调用完毕");

            // 根据对反编译代码的阅读可以确认以下几点：
            // 1.Blinddates 是相亲选项的容器
            // 2.ChooseClassmate 负责添加第一个相亲对象，这个对象是同学，可能添加失败
            // 3.ChooseClassmate 只有在 Blinddates 中没有元素的情况下才会尝试添加相亲对象
            // 4.create_blinddates 无论如何会尝试添加满三个相亲对象
            // 所以正确的思路是在 create_blinddates 打后缀，并在后缀中对是同学的选项进行成功率调整

            // 遍历所有相亲选项
            __instance.Blinddates.ForEach(blinddate =>
            {
                float loving = 0;

                // 相亲选项的 id 在女同学 id 列表里，说明这个相亲选项是女同学
                if (girlmanager.InstanceGirlmanager != null && girlmanager.InstanceGirlmanager.GirlsDictionary != null && girlmanager.InstanceGirlmanager.GirlsDictionary.ContainsKey(blinddate.id))
                {
                    // 按照女同学的方式读取这个同学的好感值
                    loving = girlmanager.InstanceGirlmanager.GirlsDictionary[blinddate.id];

                    Main.ModEntry.Logger.Log("相亲对象是女同学，id = " + blinddate.id + "，名称 = " + blinddate.name + "，好感度 = " + loving);
                }

                // 相亲选项的 id 在男同学 id 列表里，说明这个相亲选项是男同学
                if (BoysManager.Instance != null && BoysManager.Instance.BoysDictionary != null && BoysManager.Instance.BoysDictionary.ContainsKey(blinddate.id))
                {
                    // 按照男同学的方式读取这个同学的好感值
                    loving = BoysManager.Instance.BoysDictionary[blinddate.id].loving;

                    Main.ModEntry.Logger.Log("相亲对象是男同学，id = " + blinddate.id + "，名称 = " + blinddate.name + "，好感度 = " + loving);
                }

                // 对好感度超过 100 的选项的成功率进行补正
                if (loving > 100)
                {
                    Main.ModEntry.Logger.Log("发现好感度大于 100 的相亲选项，id = " + blinddate.id + "，名称 = " + blinddate.name + "，好感度 = " + loving);

                    // 计算最大补正，就是 100% - 当前选项的成功率
                    int maxCorrection = 100 - blinddate.base_winrate;

                    // 根据好感度计算出补正，
                    int correction = (int)Mathf.Lerp(0, maxCorrection, ((loving - 100f) / (Main.settings.maxLoving - 100f)));

                    // 添加补正
                    blinddate.base_winrate += correction;
                }
            });
        }
    }
}
