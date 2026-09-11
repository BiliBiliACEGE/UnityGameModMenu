using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace GrannyModMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BasePlugin
    {
        public const string PluginGuid = "com.granny.modmenu";
        public const string PluginName = "Granny Mod Menu";
        public const string PluginVersion = "1.0.0";

        public static BepInEx.Logging.ManualLogSource StaticLog;

        public override void Load()
        {
            StaticLog = Log;
            try
            {
                AddComponent<MenuBehaviour>();
                Log.LogInfo($"{PluginName} v{PluginVersion} loaded. Press F1 in game to open the menu.");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to initialize {PluginName}: {e}");
            }
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableAttribute : Attribute
    {
        public NullableAttribute(byte code) { }
        public NullableAttribute(byte[] codes) { }
    }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class NullableContextAttribute : Attribute
    {
        public NullableContextAttribute(byte code) { }
    }
}
