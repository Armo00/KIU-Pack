using System;
using System.Collections.Generic;
using UnityEngine;

namespace KCLV_CustomPlugins
{
    public class TQ12EngineController : PartModule
    {
        // 跨场景保存玩家选择的模式
        [KSPField(isPersistant = true)]
        public int currentMode = 9;

        // 当前模式的文字提示（让玩家知道现在在哪种模式）
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "Current Mode")]
        public string modeDisplay = "9 Engines";

        // 引擎模块引用
        private ModuleEnginesFX centerEngine;
        private ModuleEnginesFX pair1Engine;
        private ModuleEnginesFX pair2Engine;
        private ModuleEnginesFX staticEngines;

        // 万向节分组引用列表
        private List<ModuleGimbal> pair1Gimbals = new List<ModuleGimbal>();
        private List<ModuleGimbal> pair2Gimbals = new List<ModuleGimbal>();

        // 万向节骨骼名哈希库
        private HashSet<string> pair1GimbalNames = new HashSet<string> { "gimbal4", "Gimble_Bone7", "Gimble_Bone8", "Gimble_Bone25", "Gimble_Bone26", "gimbal8", "Gimble_Bone15", "Gimble_Bone16", "Gimble_Bone33", "Gimble_Bone34" };
        private HashSet<string> pair2GimbalNames = new HashSet<string> { "gimbal2", "Gimble_Bone3", "Gimble_Bone4", "Gimble_Bone21", "Gimble_Bone22", "gimbal6", "Gimble_Bone11", "Gimble_Bone12", "Gimble_Bone29", "Gimble_Bone30" };

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            // 1. 查找并绑定对应的 4 个引擎模块
            var allEngines = part.FindModulesImplementing<ModuleEnginesFX>();
            foreach (var eng in allEngines)
            {
                if (eng.engineID == "CenterEngine") centerEngine = eng;
                else if (eng.engineID == "GimbalPair1") pair1Engine = eng;
                else if (eng.engineID == "GimbalPair2") pair2Engine = eng;
                else if (eng.engineID == "StaticOuterEngines") staticEngines = eng;
            }

            // 2. 查找并绑定万向节组
            var allGimbals = part.FindModulesImplementing<ModuleGimbal>();
            foreach (var gim in allGimbals)
            {
                string gName = gim.gimbalTransformName;
                if (pair1GimbalNames.Contains(gName)) pair1Gimbals.Add(gim);
                else if (pair2GimbalNames.Contains(gName)) pair2Gimbals.Add(gim);
            }

            // 3. 【关键 Bug 修复】隐藏 KSP 自动生成的"启动引擎/关闭引擎"按钮
            //    否则玩家误点会导致 8/6 台等非预设数量
            HideAutoEngineEvents();

            // 4. 游戏加载时，自动恢复之前存档或 VAB 里设置的模式
            ApplyMode(currentMode);
        }

        // ================= 右键菜单按钮 =================

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 9 Engines (All)")]
        public void Mode9() { ApplyMode(9); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 5 Engines (Drop Static Outer)")]
        public void Mode5() { ApplyMode(5); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 3 Engines (Center + Pair1)")]
        public void Mode3() { ApplyMode(3); }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 1 Engine (Center Only)")]
        public void Mode1() { ApplyMode(1); }

        // ================= 核心控制逻辑 =================

        private void ApplyMode(int mode)
        {
            currentMode = mode;

            // 更新 UI 显示
            switch (mode)
            {
                case 9: modeDisplay = "9 Engines (All)"; break;
                case 5: modeDisplay = "5 Engines (Drop Static)"; break;
                case 3: modeDisplay = "3 Engines (Center+Pair1)"; break;
                case 1: modeDisplay = "1 Engine (Center)"; break;
                default: modeDisplay = "Unknown Mode " + mode; break;
            }

            // 检测中心引擎是否处于燃烧状态
            bool clusterIsRunning = centerEngine != null && centerEngine.EngineIgnited;

            // 根据玩家点的按钮，精准决定各组的启用状态
            bool enableCenter = true;
            bool enablePair1  = (mode == 9 || mode == 5 || mode == 3);
            bool enablePair2  = (mode == 9 || mode == 5);
            bool enableStatic = (mode == 9);

            // 刷新引擎状态
            UpdateEngine(centerEngine, enableCenter, clusterIsRunning);
            UpdateEngine(pair1Engine,  enablePair1,  clusterIsRunning);
            UpdateEngine(pair2Engine,  enablePair2,  clusterIsRunning);
            UpdateEngine(staticEngines, enableStatic, clusterIsRunning);

            // 刷新万向节状态
            UpdateGimbals(pair1Gimbals, enablePair1);
            UpdateGimbals(pair2Gimbals, enablePair2);
        }

        private void UpdateEngine(ModuleEnginesFX eng, bool shouldEnable, bool clusterIsRunning)
        {
            if (eng == null) return;

            if (shouldEnable)
            {
                eng.isEnabled = true;
                if (clusterIsRunning && !eng.EngineIgnited)
                {
                    eng.Activate();
                }
            }
            else
            {
                if (clusterIsRunning && eng.EngineIgnited)
                {
                    eng.Shutdown();
                }
                eng.isEnabled = false;
            }
        }

        private void UpdateGimbals(List<ModuleGimbal> gimbals, bool shouldEnable)
        {
            foreach (var gim in gimbals)
            {
                if (gim != null)
                {
                    gim.gimbalLock = !shouldEnable;
                    gim.isEnabled = shouldEnable;
                }
            }
        }

        // ================= Bug 修复方法 =================

        /// <summary>
        /// 隐藏 KSP 在 ModuleEnginesFX 上自动生成的"Activate / Shutdown"按钮。
        /// 多引擎分组部件中，这些按钮会让玩家误点单组，造成 8/6 等非预设数量。
        /// </summary>
        private void HideAutoEngineEvents()
        {
            var allEngines = new[] { centerEngine, pair1Engine, pair2Engine, staticEngines };
            foreach (var eng in allEngines)
            {
                if (eng == null) continue;

                for (int i = 0; i < eng.Events.Count; i++)
                {
                    var evt = eng.Events[i];
                    if (evt == null) continue;

                    // KSP 在 ModuleEnginesFX 上自动生成的事件名为 Activate 和 Shutdown
                    if (evt.name == "Activate" || evt.name == "Shutdown")
                    {
                        evt.guiActive = false;
                        evt.guiActiveEditor = false;
                    }
                }
            }
        }
    }
}
