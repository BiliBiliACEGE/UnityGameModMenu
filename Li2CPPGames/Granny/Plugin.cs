using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace GrannyModMenu
{
    /// <summary>
    /// Granny 模组菜单插件主入口
    /// 目标: BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785
    /// 引擎: Unity6 + IL2CPP + D3D12 (使用 Unity IMGUI 跨图形 API 渲染)
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Granny.exe")]
    public class GrannyModMenuPlugin : BasePlugin
    {
        public const string PluginGuid = "com.granny.modmenu";
        public const string PluginName = "Granny Mod Menu";
        public const string PluginVersion = "1.0.0";

        // 重命名为 PluginLog 以避免遮蔽 BasePlugin.Log 实例属性
        // BasePlugin.Log 是实例属性 (ManualLogSource), 由 BepInEx 内部注入
        // 这里缓存到静态字段,供 ModMenuBehavior 等其他类访问
        internal static ManualLogSource PluginLog;
        internal static ConfigEntry<UnityEngine.KeyCode> MenuToggleKey;
        internal static ConfigEntry<bool> MenuOpenOnStart;

        public override void Load()
        {
            PluginLog = Log;

            // 注册配置项
            MenuToggleKey = Config.Bind(
                "General",
                "MenuToggleKey",
                UnityEngine.KeyCode.F1,
                "打开/关闭菜单的快捷键");

            MenuOpenOnStart = Config.Bind(
                "General",
                "MenuOpenOnStart",
                false,
                "游戏启动时自动打开菜单");

            // 注册自定义 MonoBehaviour 类型到 IL2CPP 运行时
            // 这样 Unity 才能识别我们的组件,从而调用 Update/OnGUI
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<ModMenuBehavior>();
            }
            catch (Exception ex)
            {
                Log.LogError($"注册 ModMenuBehavior 失败: {ex.Message}");
                return;
            }

            // 创建承载 GameObject,挂载我们的 MonoBehaviour
            try
            {
                var go = new GameObject("__GrannyModMenu__");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<ModMenuBehavior>();
                Log.LogInfo($"Granny Mod Menu v{PluginVersion} 加载成功");
                Log.LogInfo($"菜单切换键: {MenuToggleKey.Value} (可在 BepInEx/config 中修改)");
            }
            catch (Exception ex)
            {
                Log.LogError($"创建 ModMenuBehavior GameObject 失败: {ex}");
            }
        }
    }
}
