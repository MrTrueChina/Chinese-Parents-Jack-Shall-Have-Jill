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
        /// 修改后的最大好感度，到达这个好感度则 100% 求婚成功
        /// </summary>
        public static int maxLoving = 150;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            // 保存 Mod 对象
            ModEntry = modEntry;
            ModEntry.OnToggle = OnToggle;

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
            __result = Main.maxLoving;
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
            __instance.BoysDictionary[boyId].loving = Mathf.Min(Main.maxLoving, __state.beforeLoving + loving);
        }
    }

    ////////--------////////--------//////// 修改求婚成功率 ////////--------////////--------////////

    /// <summary>
    /// 在获取相亲可选人物时获取同学的方法
    /// </summary>
    [HarmonyPatch(typeof(Panel_blinddate), "ChooseClassmate")]
    public static class Panel_blinddate_ChooseClassmate
    {
        private static bool Prefix(Panel_blinddate __instance)
        {
            // 如果 Mod 未启动则直接按照游戏原本的逻辑进行调用
            if (!Main.enabled)
            {
                return true;
            }

            Main.ModEntry.Logger.Log("在相亲列表中添加同学方法即将调用");

            // 以下代码直接复制粘贴自反编译
            if (__instance.Blinddates.Count == 0)
            {
                if (record_manager.InstanceManagerRecord.IsBoy())
                {
                    int x2 = (from x in girlmanager.InstanceGirlmanager.GirlsDictionary.Keys
                              select new
                              {
                                  x = x,
                                  y = girlmanager.InstanceGirlmanager.GirlsDictionary[x]
                              } into x
                              orderby x.y descending
                              select x).First().x;
                    blinddate blinddate = blinddate.create(x2);

                    ////----////----//// Mod 替换逻辑 ////----////----////

                    //// 原有的成功率计算逻辑，概率 = 好感度，最大 80
                    //blinddate.base_winrate = Mathf.Min(80, girlmanager.InstanceGirlmanager.GirlsDictionary[x2]);

                    // 好感度
                    int loving = girlmanager.InstanceGirlmanager.GirlsDictionary[x2];

                    if(loving <= 100)
                    {
                        // 好感度小于等于 100，按照原有逻辑，概率 = 好感度，最大 80
                        blinddate.base_winrate = Mathf.Clamp(loving, 0, 80);
                    }
                    else
                    {
                        // 好感度大于 100，在 80 的基础上匀速增长到 100
                        blinddate.base_winrate = Mathf.Clamp((int)(80 + (float)loving / (Main.maxLoving - 100) * 20), 80, 100);
                    }

                    // 保留原有的计算逻辑
                    blinddate.base_winrate = Mathf.Min(80, girlmanager.InstanceGirlmanager.GirlsDictionary[x2]);


                    // 如果好感度大于 100，对成功率进行补正
                    if(girlmanager.InstanceGirlmanager.GirlsDictionary[x2] > 100)
                    {
                        blinddate.base_winrate += (girlmanager.InstanceGirlmanager.GirlsDictionary[x2] - 100) / (Main.maxLoving - 100) * 20;
                    }

                    ////----////----//// Mod 替换逻辑 ////----////----////

                    // 以下代码直接复制粘贴自反编译
                    __instance.Blinddates.Add(blinddate);
                    GameAnalytics.NewDesignEvent(begin_analytic.GirlMostPop + x2);
                }
                else
                {
                    int num = BoysManager.Instance.GetMaxLoving(1)[0];
                    blinddate blinddate2 = blinddate.create(num);

                    ////----////----//// Mod 替换逻辑 ////----////----////

                    //// 原有的成功率计算逻辑，概率 = 好感度，最大 80
                    //blinddate2.base_winrate = Mathf.Min(80, BoysManager.Instance.BoysDictionary[num].loving);

                    // 好感度
                    int loving = BoysManager.Instance.BoysDictionary[num].loving;

                    if (loving <= 100)
                    {
                        // 好感度小于等于 100，按照原有逻辑，概率 = 好感度，最大 80
                        blinddate2.base_winrate = Mathf.Clamp(loving, 0, 80);
                    }
                    else
                    {
                        // 好感度大于 100，在 80 的基础上匀速增长到 100
                        blinddate2.base_winrate = Mathf.Clamp((int)(80 + (float)loving / (Main.maxLoving - 100) * 20), 80, 100);
                    }

                    ////----////----//// Mod 替换逻辑 ////----////----////

                    // 以下代码直接复制粘贴自反编译
                    __instance.Blinddates.Add(blinddate2);
                }
            }

            // 阻断对原方法的调用
            return false;
        }
    }
}
