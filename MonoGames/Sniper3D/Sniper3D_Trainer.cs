using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace Sniper3DTrainer
{
    [BepInPlugin("com.example.sniper3d.trainer", "Sniper3D Trainer", "1.0.0")]
    public class Sniper3DTrainerPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;
        private Harmony harmony;

        // Menu state
        private bool showMenu = false;
        private Rect menuRect = new Rect(20, 20, 400, 450);
        private Vector2 scrollPosition = Vector2.zero;
        private int selectedTab = 0;
        private readonly string[] tabKeys = { "Tab_Currency", "Tab_Shop", "Tab_Weapons", "Tab_Unlocks", "Tab_Settings" };

        private const float BaseMenuWidth = 400f;
        private const float BaseMenuHeight = 450f;
        private const float BaseScrollHeight = 320f;

        // Currency inputs
        private string softCurrencyInput = "99999999";
        private string hardCurrencyInput = "999999";
        private string pvpCurrencyInput = "999999";
        private string energyInput = "999";
        private bool infiniteSoft = false;
        private bool infiniteHard = false;
        private bool infinitePvp = false;
        private bool infiniteEnergy = false;

        // Shop
        public static bool FreeShopping = false;

        // Weapons Tab
        private List<WeaponData> allWeapons = new List<WeaponData>();
        private int selectedWeaponIndex = -1;
        private bool allWeaponsUnlocked = false;
        private bool allWeaponsMaxed = false;
        private Dictionary<string, bool> originalForceAvailable = new Dictionary<string, bool>();
        private Dictionary<string, Dictionary<string, int>> originalUpgrades = new Dictionary<string, Dictionary<string, int>>();
        private Vector2 weaponListScrollPosition = Vector2.zero;
        private int selectedUpgradePartIndex = 0;
        private string upgradeLevelInput = "1";
        private bool showWeaponDropdown = false;

        // Unlocks / Mods
        public static bool UnlockAllWeapons = false;
        public static bool ModsAllModes = false;
        public static float DamageMultiplier = 1.0f;

        // Settings
        private ConfigEntry<float> cfgMenuScale;
        private ConfigEntry<float> cfgFontColorR;
        private ConfigEntry<float> cfgFontColorG;
        private ConfigEntry<float> cfgFontColorB;
        private ConfigEntry<string> cfgLanguage;

        private float menuScale = 1.0f;
        private Color fontColor = Color.white;
        private string currentLanguage = "zh";

        private float nextCurrencyUpdate = 0f;
        private const float CurrencyUpdateInterval = 0.5f;

        // Localization
        private readonly Dictionary<string, Dictionary<string, string>> i18n = new Dictionary<string, Dictionary<string, string>>
        {
            ["zh"] = new Dictionary<string, string>
            {
                ["Tab_Currency"] = "货币",
                ["Tab_Shop"] = "商店",
                ["Tab_Weapons"] = "武器",
                ["Tab_Unlocks"] = "解锁",
                ["Tab_Settings"] = "设置",
                ["SoftCurrency"] = "软货币 (金币)",
                ["HardCurrency"] = "硬货币 (钻石)",
                ["PvpCurrency"] = "PVP 货币",
                ["Energy"] = "能量值",
                ["Infinite"] = "无限",
                ["Apply"] = "应用修改",
                ["FreeShopping"] = "免费购买 (商店/礼包)",
                ["UnlockWeapons"] = "解除武器限制",
                ["ModsAllModes"] = "修改器全模式生效",
                ["MaxModLevel"] = "修改器等级最大化",
                ["MaxModAmount"] = "修改器数量最大化",
                ["DamageMultiplier"] = "伤害倍率",
                ["MenuScale"] = "菜单大小缩放",
                ["FontColor"] = "字体颜色 (R, G, B)",
                ["Language"] = "语言",
                ["Chinese"] = "中文",
                ["English"] = "English",
                ["Close"] = "关闭菜单 (F1)",
                ["SkipLevelUp"] = "跳过升级提示",
                ["Active"] = "已开启",
                ["Inactive"] = "已关闭",
                ["Title"] = "Sniper3D 修改器",
                ["UnlockAllWeaponsBtn"] = "一键解锁全部武器",
                ["RelockAllWeaponsBtn"] = "一键回锁全部武器",
                ["MaxAllUpgradesBtn"] = "一键等级全满",
                ["RevertAllUpgradesBtn"] = "一键回退全部等级",
                ["SelectWeapon"] = "选择武器",
                ["UnlockWeapon"] = "解锁选中武器",
                ["LockWeapon"] = "锁定选中武器",
                ["MaxWeaponUpgrades"] = "选中武器全满",
                ["RevertWeaponUpgrades"] = "回退选中武器等级",
                ["SelectUpgradePart"] = "选择升级部件",
                ["SetUpgradeLevel"] = "设置等级",
                ["WeaponStatus"] = "武器状态",
                ["Owned"] = "已拥有",
                ["NotOwned"] = "未拥有",
                ["UpgradesMaxed"] = "已全满",
                ["UpgradesNormal"] = "未全满",
                ["RefreshWeapons"] = "刷新武器列表",
                ["ResetAllUpgradesBtn"] = "重置所有武器升级",
                ["CurrentLevel"] = "当前等级",
                ["MaxLevel"] = "最大等级",
            },
            ["en"] = new Dictionary<string, string>
            {
                ["Tab_Currency"] = "Currency",
                ["Tab_Shop"] = "Shop",
                ["Tab_Weapons"] = "Weapons",
                ["Tab_Unlocks"] = "Unlocks",
                ["Tab_Settings"] = "Settings",
                ["SoftCurrency"] = "Soft Currency (Coins)",
                ["HardCurrency"] = "Hard Currency (Gems)",
                ["PvpCurrency"] = "PVP Currency",
                ["Energy"] = "Energy",
                ["Infinite"] = "Infinite",
                ["Apply"] = "Apply Changes",
                ["FreeShopping"] = "Free Shopping (Store/Packs)",
                ["UnlockWeapons"] = "Unlock All Weapons",
                ["ModsAllModes"] = "Mods Work Everywhere",
                ["MaxModLevel"] = "Maximize Mod Levels",
                ["MaxModAmount"] = "Maximize Mod Amounts",
                ["DamageMultiplier"] = "Damage Multiplier",
                ["MenuScale"] = "Menu Scale",
                ["FontColor"] = "Font Color (R, G, B)",
                ["Language"] = "Language",
                ["Chinese"] = "中文",
                ["English"] = "English",
                ["Close"] = "Close Menu (F1)",
                ["SkipLevelUp"] = "Skip Level Up Popup",
                ["Active"] = "Active",
                ["Inactive"] = "Inactive",
                ["Title"] = "Sniper3D Trainer",
                ["UnlockAllWeaponsBtn"] = "Unlock All Weapons",
                ["RelockAllWeaponsBtn"] = "Relock All Weapons",
                ["MaxAllUpgradesBtn"] = "Max All Upgrades",
                ["RevertAllUpgradesBtn"] = "Revert All Upgrades",
                ["SelectWeapon"] = "Select Weapon",
                ["UnlockWeapon"] = "Unlock Selected",
                ["LockWeapon"] = "Lock Selected",
                ["MaxWeaponUpgrades"] = "Max Selected Upgrades",
                ["RevertWeaponUpgrades"] = "Revert Selected Upgrades",
                ["SelectUpgradePart"] = "Select Upgrade Part",
                ["SetUpgradeLevel"] = "Set Level",
                ["WeaponStatus"] = "Weapon Status",
                ["Owned"] = "Owned",
                ["NotOwned"] = "Not Owned",
                ["UpgradesMaxed"] = "Maxed",
                ["UpgradesNormal"] = "Normal",
                ["RefreshWeapons"] = "Refresh Weapon List",
                ["ResetAllUpgradesBtn"] = "Reset All Upgrades",
                ["CurrentLevel"] = "Current Level",
                ["MaxLevel"] = "Max Level",
            }
        };

        private string T(string key)
        {
            if (i18n.TryGetValue(currentLanguage, out var dict) && dict.TryGetValue(key, out var value))
                return value;
            if (i18n["en"].TryGetValue(key, out var enValue))
                return enValue;
            return key;
        }

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Sniper3D Trainer is loading...");

            // Config bindings
            cfgMenuScale = Config.Bind("Settings", "MenuScale", 1.0f, "Menu scale factor");
            cfgFontColorR = Config.Bind("Settings", "FontColorR", 1.0f, "Font color red");
            cfgFontColorG = Config.Bind("Settings", "FontColorG", 1.0f, "Font color green");
            cfgFontColorB = Config.Bind("Settings", "FontColorB", 1.0f, "Font color blue");
            cfgLanguage = Config.Bind("Settings", "Language", "zh", "Menu language (zh/en)");

            // Load settings
            menuScale = cfgMenuScale.Value;
            fontColor = new Color(cfgFontColorR.Value, cfgFontColorG.Value, cfgFontColorB.Value);
            currentLanguage = cfgLanguage.Value;
            if (currentLanguage != "zh" && currentLanguage != "en")
                currentLanguage = "zh";

            harmony = new Harmony("com.example.sniper3d.trainer");
            harmony.PatchAll();

            Log.LogInfo("Sniper3D Trainer loaded successfully!");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                showMenu = !showMenu;
                Log.LogInfo("Menu toggled: " + showMenu);
            }

            if (Time.time >= nextCurrencyUpdate)
            {
                nextCurrencyUpdate = Time.time + CurrencyUpdateInterval;
                ApplyInfiniteCurrency();
            }
        }

        private void OnGUI()
        {
            if (!showMenu) return;

            Color oldColor = GUI.color;
            GUI.color = fontColor;

            menuRect.width = BaseMenuWidth * menuScale;
            menuRect.height = BaseMenuHeight * menuScale;

            menuRect = GUI.Window(0, menuRect, DrawMenuWindow, T("Title"));

            GUI.color = oldColor;
        }

        private void DrawMenuWindow(int windowID)
        {
            GUILayout.BeginVertical();

            // Tabs
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabKeys.Length; i++)
            {
                GUIStyle tabStyle = new GUIStyle(GUI.skin.button);
                if (selectedTab == i)
                {
                    tabStyle.normal.textColor = Color.yellow;
                }
                if (GUILayout.Button(T(tabKeys[i]), tabStyle, GUILayout.Height(30)))
                {
                    selectedTab = i;
                    scrollPosition = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(BaseScrollHeight * menuScale));

            switch (selectedTab)
            {
                case 0: DrawCurrencyTab(); break;
                case 1: DrawShopTab(); break;
                case 2: DrawWeaponsTab(); break;
                case 3: DrawUnlocksTab(); break;
                case 4: DrawSettingsTab(); break;
            }

            GUILayout.EndScrollView();

            GUILayout.Space(5);

            if (GUILayout.Button(T("Close"), GUILayout.Height(25)))
            {
                showMenu = false;
            }

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void DrawCurrencyTab()
        {
            GUILayout.Label(T("SoftCurrency") + ":");
            softCurrencyInput = GUILayout.TextField(softCurrencyInput, GUILayout.Height(22));
            infiniteSoft = GUILayout.Toggle(infiniteSoft, T("Infinite"));

            GUILayout.Space(5);

            GUILayout.Label(T("HardCurrency") + ":");
            hardCurrencyInput = GUILayout.TextField(hardCurrencyInput, GUILayout.Height(22));
            infiniteHard = GUILayout.Toggle(infiniteHard, T("Infinite"));

            GUILayout.Space(5);

            GUILayout.Label(T("PvpCurrency") + ":");
            pvpCurrencyInput = GUILayout.TextField(pvpCurrencyInput, GUILayout.Height(22));
            infinitePvp = GUILayout.Toggle(infinitePvp, T("Infinite"));

            GUILayout.Space(5);

            GUILayout.Label(T("Energy") + ":");
            energyInput = GUILayout.TextField(energyInput, GUILayout.Height(22));
            infiniteEnergy = GUILayout.Toggle(infiniteEnergy, T("Infinite"));

            GUILayout.Space(10);

            if (GUILayout.Button(T("Apply"), GUILayout.Height(30)))
            {
                ApplyCurrencyChanges();
            }
        }

        private void DrawShopTab()
        {
            GUILayout.Label(T("FreeShopping") + ":");
            FreeShopping = GUILayout.Toggle(FreeShopping, FreeShopping ? T("Active") : T("Inactive"));

            GUILayout.Space(10);

            GUILayout.Label("Store.BuyResult: " + (FreeShopping ? T("Active") : T("Inactive")));
            GUILayout.Label("Store.CanBuy: " + (FreeShopping ? "Always Succeeded" : "Normal"));
            GUILayout.Label("Iap.Buy: " + (FreeShopping ? "Bypassed" : "Normal"));
        }

        private void DrawWeaponsTab()
        {
            // Refresh weapon list if empty
            if (allWeapons.Count == 0)
            {
                RefreshWeaponList();
            }

            // Global buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(allWeaponsUnlocked ? T("RelockAllWeaponsBtn") : T("UnlockAllWeaponsBtn"), GUILayout.Height(28)))
            {
                if (allWeaponsUnlocked)
                    DoRelockAllWeapons();
                else
                    DoUnlockAllWeapons();
            }
            if (GUILayout.Button(allWeaponsMaxed ? T("RevertAllUpgradesBtn") : T("MaxAllUpgradesBtn"), GUILayout.Height(28)))
            {
                if (allWeaponsMaxed)
                    RevertAllWeaponUpgrades();
                else
                    MaxAllWeaponUpgrades();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            if (GUILayout.Button(T("ResetAllUpgradesBtn"), GUILayout.Height(22)))
            {
                ResetAllWeaponUpgrades();
            }

            if (GUILayout.Button(T("RefreshWeapons"), GUILayout.Height(22)))
            {
                RefreshWeaponList();
            }

            GUILayout.Space(5);

            // Weapon dropdown
            string selectedWeaponName = selectedWeaponIndex >= 0 && selectedWeaponIndex < allWeapons.Count
                ? allWeapons[selectedWeaponIndex].Title
                : T("SelectWeapon");

            if (GUILayout.Button(selectedWeaponName + (showWeaponDropdown ? " ▲" : " ▼"), GUILayout.Height(24)))
            {
                showWeaponDropdown = !showWeaponDropdown;
            }

            if (showWeaponDropdown && allWeapons.Count > 0)
            {
                weaponListScrollPosition = GUILayout.BeginScrollView(weaponListScrollPosition, GUILayout.Height(120 * menuScale));
                for (int i = 0; i < allWeapons.Count; i++)
                {
                    var weapon = allWeapons[i];
                    string label = weapon.Title + " (" + (weapon.Owned ? T("Owned") : T("NotOwned")) + ")";
                    GUIStyle style = new GUIStyle(GUI.skin.button);
                    if (i == selectedWeaponIndex)
                    {
                        style.normal.textColor = Color.yellow;
                    }
                    if (GUILayout.Button(label, style, GUILayout.Height(22)))
                    {
                        selectedWeaponIndex = i;
                        selectedUpgradePartIndex = 0;
                        showWeaponDropdown = false;
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(5);

            // Selected weapon details
            if (selectedWeaponIndex >= 0 && selectedWeaponIndex < allWeapons.Count)
            {
                var selectedWeapon = allWeapons[selectedWeaponIndex];

                GUILayout.Label(T("WeaponStatus") + ": " + (selectedWeapon.Owned ? T("Owned") : T("NotOwned")));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(selectedWeapon.ForceAvailable ? T("LockWeapon") : T("UnlockWeapon"), GUILayout.Height(26)))
                {
                    selectedWeapon.ForceAvailable = !selectedWeapon.ForceAvailable;
                }

                bool isWeaponMaxed = IsWeaponMaxed(selectedWeapon);
                if (GUILayout.Button(isWeaponMaxed ? T("RevertWeaponUpgrades") : T("MaxWeaponUpgrades"), GUILayout.Height(26)))
                {
                    if (isWeaponMaxed)
                        RevertWeaponUpgrades(selectedWeapon);
                    else
                        MaxWeaponUpgrades(selectedWeapon);
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(5);

                // Individual part upgrade
                var parts = GetWeaponParts(selectedWeapon);
                if (parts.Count > 0)
                {
                    GUILayout.Label(T("SelectUpgradePart") + ":");
                    string[] partNames = parts.ToArray();
                    selectedUpgradePartIndex = GUILayout.SelectionGrid(selectedUpgradePartIndex, partNames, 2, GUILayout.Height(22 * ((partNames.Length + 1) / 2)));

                    if (selectedUpgradePartIndex >= 0 && selectedUpgradePartIndex < parts.Count)
                    {
                        string selectedPart = parts[selectedUpgradePartIndex];
                        int currentLevel = GetUpgradeLevel(selectedWeapon, selectedPart);
                        int maxLevel = GetPartMaxLevel(selectedWeapon, selectedPart);
                        GUILayout.Label($"{T("CurrentLevel")}: {currentLevel} / {T("MaxLevel")}: {maxLevel}");

                        GUILayout.BeginHorizontal();
                        GUILayout.Label(T("SetUpgradeLevel") + ":", GUILayout.Width(80));
                        upgradeLevelInput = GUILayout.TextField(upgradeLevelInput, GUILayout.Height(22));
                        GUILayout.EndHorizontal();

                        if (GUILayout.Button(T("SetUpgradeLevel"), GUILayout.Height(24)))
                        {
                            if (int.TryParse(upgradeLevelInput, out int level) && selectedUpgradePartIndex >= 0 && selectedUpgradePartIndex < parts.Count)
                            {
                                // Clamp level to valid range
                                if (level < 0) level = 0;
                                if (level > maxLevel) level = maxLevel;
                                SetUpgradeLevel(selectedWeapon, parts[selectedUpgradePartIndex], level);
                            }
                        }
                    }
                }
            }
        }

        private void DrawUnlocksTab()
        {
            GUILayout.Label(T("UnlockWeapons") + ":");
            UnlockAllWeapons = GUILayout.Toggle(UnlockAllWeapons, UnlockAllWeapons ? T("Active") : T("Inactive"));

            GUILayout.Space(10);

            GUILayout.Label(T("ModsAllModes") + ":");
            ModsAllModes = GUILayout.Toggle(ModsAllModes, ModsAllModes ? T("Active") : T("Inactive"));

            GUILayout.Space(10);

            GUILayout.Label(T("DamageMultiplier") + ": " + DamageMultiplier.ToString("F1") + "x");
            DamageMultiplier = GUILayout.HorizontalSlider(DamageMultiplier, 1.0f, 100.0f);

            GUILayout.Space(10);

            if (GUILayout.Button(T("MaxModLevel"), GUILayout.Height(28)))
            {
                MaximizeModLevels();
            }

            GUILayout.Space(5);

            if (GUILayout.Button(T("MaxModAmount"), GUILayout.Height(28)))
            {
                MaximizeModAmounts();
            }
        }

        private void DrawSettingsTab()
        {
            GUILayout.Label(T("MenuScale") + ": " + menuScale.ToString("F2"));
            menuScale = GUILayout.HorizontalSlider(menuScale, 0.5f, 2.0f);
            if (GUILayout.Button("Reset Scale", GUILayout.Height(22)))
            {
                menuScale = 1.0f;
            }

            GUILayout.Space(5);

            GUILayout.Label(T("FontColor") + ":");
            GUILayout.BeginHorizontal();
            GUILayout.Label("R", GUILayout.Width(20));
            fontColor.r = GUILayout.HorizontalSlider(fontColor.r, 0f, 1f, GUILayout.Width(80));
            GUILayout.Label(fontColor.r.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("G", GUILayout.Width(20));
            fontColor.g = GUILayout.HorizontalSlider(fontColor.g, 0f, 1f, GUILayout.Width(80));
            GUILayout.Label(fontColor.g.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("B", GUILayout.Width(20));
            fontColor.b = GUILayout.HorizontalSlider(fontColor.b, 0f, 1f, GUILayout.Width(80));
            GUILayout.Label(fontColor.b.ToString("F2"), GUILayout.Width(40));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.Label(T("Language") + ":");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Chinese"), currentLanguage == "zh" ? GUI.skin.box : GUI.skin.button))
            {
                currentLanguage = "zh";
            }
            if (GUILayout.Button(T("English"), currentLanguage == "en" ? GUI.skin.box : GUI.skin.button))
            {
                currentLanguage = "en";
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
            {
                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            cfgMenuScale.Value = menuScale;
            cfgFontColorR.Value = fontColor.r;
            cfgFontColorG.Value = fontColor.g;
            cfgFontColorB.Value = fontColor.b;
            cfgLanguage.Value = currentLanguage;
            Config.Save();
            Log.LogInfo("Settings saved.");
        }

        private void ApplyCurrencyChanges()
        {
            var profile = App.User;
            if (profile == null)
            {
                Log.LogWarning("User.Profile is null, cannot modify currency.");
                return;
            }

            if (long.TryParse(softCurrencyInput, out long softValue))
            {
                profile.SoftCurrency = softValue;
                Log.LogInfo($"SoftCurrency set to: {softValue}");
            }

            if (int.TryParse(hardCurrencyInput, out int hardValue))
            {
                profile.HardCurrency = hardValue;
                Log.LogInfo($"HardCurrency set to: {hardValue}");
            }

            if (int.TryParse(pvpCurrencyInput, out int pvpValue))
            {
                profile.PVPCurrency = pvpValue;
                Log.LogInfo($"PVPCurrency set to: {pvpValue}");
            }

            if (int.TryParse(energyInput, out int energyValue))
            {
                profile.Energy = energyValue;
                Log.LogInfo($"Energy set to: {energyValue}");
            }
        }

        private void ApplyInfiniteCurrency()
        {
            var profile = App.User;
            if (profile == null) return;

            if (infiniteSoft && long.TryParse(softCurrencyInput, out long softValue))
            {
                if (profile.SoftCurrency != softValue)
                {
                    profile.SoftCurrency = softValue;
                }
            }

            if (infiniteHard && int.TryParse(hardCurrencyInput, out int hardValue))
            {
                if (profile.HardCurrency != hardValue)
                {
                    profile.HardCurrency = hardValue;
                }
            }

            if (infinitePvp && int.TryParse(pvpCurrencyInput, out int pvpValue))
            {
                if (profile.PVPCurrency != pvpValue)
                {
                    profile.PVPCurrency = pvpValue;
                }
            }

            if (infiniteEnergy && int.TryParse(energyInput, out int energyValue))
            {
                if (profile.Energy != energyValue)
                {
                    profile.Energy = energyValue;
                }
            }
        }

        private void MaximizeModLevels()
        {
            if (App.ModManager == null || App.ModManager.EquippedMods == null)
            {
                Log.LogWarning("ModManager not available.");
                return;
            }

            foreach (var mod in App.ModManager.EquippedMods)
            {
                if (mod != null && mod.ModInfo != null)
                {
                    mod.CurrentLevel = mod.ModInfo.MaxLevel;
                }
            }
            Log.LogInfo("All equipped mod levels maximized.");
        }

        private void MaximizeModAmounts()
        {
            if (App.ModManager == null || App.ModManager.EquippedMods == null)
            {
                Log.LogWarning("ModManager not available.");
                return;
            }

            foreach (var mod in App.ModManager.EquippedMods)
            {
                if (mod != null && mod.ModInfo != null)
                {
                    mod.Amount = mod.ModInfo.MaxAmount;
                }
            }
            Log.LogInfo("All equipped mod amounts maximized.");
        }

        // ===== Weapon Tab Helpers =====

        private void RefreshWeaponList()
        {
            allWeapons.Clear();
            selectedWeaponIndex = -1;
            try
            {
                Store store = null;
                // Try to get Store via reflection from App.User._store
                if (App.User != null)
                {
                    var storeField = App.User.GetType().GetField("_store", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    store = storeField?.GetValue(App.User) as Store;
                }
                // Fallback: find Store in scene
                if (store == null)
                {
                    store = UnityEngine.Object.FindObjectOfType<Store>();
                }

                if (store?.Items != null)
                {
                    foreach (var item in store.Items)
                    {
                        if (item is WeaponData wd)
                        {
                            allWeapons.Add(wd);
                        }
                    }
                }
                allWeapons.Sort((a, b) => string.Compare(a.Title, b.Title));
                Log.LogInfo($"Refreshed weapon list: {allWeapons.Count} weapons found.");
            }
            catch (Exception ex)
            {
                Log.LogWarning("Failed to refresh weapon list: " + ex.Message);
            }
        }

        private void DoUnlockAllWeapons()
        {
            originalForceAvailable.Clear();
            foreach (var weapon in allWeapons)
            {
                try
                {
                    originalForceAvailable[weapon.UniqueId] = weapon.ForceAvailable;
                    weapon.ForceAvailable = true;
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"DoUnlockAllWeapons failed for {weapon?.Title}: {ex.Message}");
                }
            }
            allWeaponsUnlocked = true;
            Log.LogInfo("All weapons unlocked.");
        }

        private void DoRelockAllWeapons()
        {
            foreach (var weapon in allWeapons)
            {
                if (originalForceAvailable.TryGetValue(weapon.UniqueId, out bool original))
                {
                    weapon.ForceAvailable = original;
                }
                else
                {
                    weapon.ForceAvailable = false;
                }
            }
            allWeaponsUnlocked = false;
            Log.LogInfo("All weapons relocked.");
        }

        private void MaxAllWeaponUpgrades()
        {
            originalUpgrades.Clear();
            foreach (var weapon in allWeapons)
            {
                try
                {
                    MaxWeaponUpgrades(weapon);
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"MaxAllWeaponUpgrades failed for {weapon?.Title}: {ex.Message}");
                }
            }
            allWeaponsMaxed = true;
            Log.LogInfo("All weapon upgrades maximized.");
        }

        private void RevertAllWeaponUpgrades()
        {
            foreach (var weapon in allWeapons)
            {
                RestoreWeaponUpgrades(weapon);
            }
            allWeaponsMaxed = false;
            Log.LogInfo("All weapon upgrades reverted.");
        }

        private void ResetAllWeaponUpgrades()
        {
            foreach (var weapon in allWeapons)
            {
                ResetWeaponUpgrades(weapon);
            }
            allWeaponsMaxed = false;
            Log.LogInfo("All weapon upgrades reset to default.");
        }

        private void ResetWeaponUpgrades(WeaponData weapon)
        {
            if (weapon == null) return;
            try
            {
                // Clear _upgradeLevels so the game recreates it from default data when needed.
                // Do NOT force-access weapon.Upgrades here to avoid triggering recreation
                // at a bad time; let the game recreate it naturally.
                var upgradeField = weapon.GetType().GetField("_upgradeLevels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (upgradeField != null)
                {
                    upgradeField.SetValue(weapon, null);
                }
                Log.LogInfo($"Weapon {weapon.Title} upgrades cleared (will recreate on next access).");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"ResetWeaponUpgrades failed: {ex.Message}");
            }
        }

        private void MaxWeaponUpgrades(WeaponData weapon)
        {
            if (weapon == null) return;
            try
            {
                SaveWeaponUpgrades(weapon);
                var parts = GetWeaponParts(weapon);
                foreach (var part in parts)
                {
                    int maxLevel = GetPartMaxLevel(weapon, part);
                    if (maxLevel > 0)
                        SetUpgradeLevel(weapon, part, maxLevel);
                }
                Log.LogInfo($"Weapon {weapon.Title} upgrades maximized safely.");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"MaxWeaponUpgrades failed: {ex.Message}");
            }
        }

        private void RevertWeaponUpgrades(WeaponData weapon)
        {
            if (weapon == null) return;
            RestoreWeaponUpgrades(weapon);
            Log.LogInfo($"Weapon {weapon.Title} upgrades reverted.");
        }

        private void SaveWeaponUpgrades(WeaponData weapon)
        {
            var upgrades = new Dictionary<string, int>();
            var parts = weapon.PartsUpgrades;
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    string partName = GetPartName(part);
                    if (!string.IsNullOrEmpty(partName))
                    {
                        int currentLevel = GetUpgradeLevel(weapon, partName);
                        upgrades[partName] = currentLevel;
                    }
                }
            }
            originalUpgrades[weapon.UniqueId] = upgrades;
        }

        private void RestoreWeaponUpgrades(WeaponData weapon)
        {
            if (originalUpgrades.TryGetValue(weapon.UniqueId, out var upgrades))
            {
                foreach (var kvp in upgrades)
                {
                    SetUpgradeLevel(weapon, kvp.Key, kvp.Value);
                }
            }
        }

        private List<string> GetWeaponParts(WeaponData weapon)
        {
            var result = new List<string>();
            if (weapon?.PartsUpgrades == null) return result;
            foreach (var part in weapon.PartsUpgrades)
            {
                string partName = GetPartName(part);
                if (!string.IsNullOrEmpty(partName))
                {
                    result.Add(partName);
                }
            }
            return result;
        }

        private string GetPartName(object upgradablePart)
        {
            if (upgradablePart == null) return null;
            try
            {
                var prop = upgradablePart.GetType().GetProperty("PartName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                return prop?.GetValue(upgradablePart, null) as string;
            }
            catch
            {
                return null;
            }
        }

        private int GetUpgradeLevel(WeaponData weapon, string partName)
        {
            if (weapon?.Upgrades == null) return 0;
            try
            {
                var indexer = weapon.Upgrades.GetType().GetProperty("Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (indexer != null)
                {
                    var value = indexer.GetValue(weapon.Upgrades, new object[] { partName });
                    return value is int i ? i : 0;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"GetUpgradeLevel failed: {ex.Message}");
            }
            return 0;
        }

        private void SetUpgradeLevel(WeaponData weapon, string partName, int level)
        {
            if (weapon?.Upgrades == null) return;
            try
            {
                var indexer = weapon.Upgrades.GetType().GetProperty("Item", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (indexer != null)
                {
                    indexer.SetValue(weapon.Upgrades, level, new object[] { partName });
                    Log.LogInfo($"Set {weapon.Title} [{partName}] to level {level}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"SetUpgradeLevel failed: {ex.Message}");
            }
        }

        private int GetPartMaxLevel(WeaponData weapon, string partName)
        {
            if (weapon?.PartsUpgrades == null) return 0;
            try
            {
                foreach (var part in weapon.PartsUpgrades)
                {
                    string name = GetPartName(part);
                    if (name == partName)
                    {
                        var upgradesProp = part.GetType().GetProperty("Upgrades", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        var upgradesList = upgradesProp?.GetValue(part, null) as System.Collections.IList;
                        return upgradesList?.Count ?? 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"GetPartMaxLevel failed: {ex.Message}");
            }
            return 0;
        }

        private bool IsWeaponMaxed(WeaponData weapon)
        {
            if (weapon?.PartsUpgrades == null) return false;
            try
            {
                foreach (var part in weapon.PartsUpgrades)
                {
                    string partName = GetPartName(part);
                    if (string.IsNullOrEmpty(partName)) continue;

                    int currentLevel = GetUpgradeLevel(weapon, partName);
                    var upgradesProp = part.GetType().GetProperty("Upgrades", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var upgradesList = upgradesProp?.GetValue(part, null) as System.Collections.IList;
                    int maxLevel = upgradesList?.Count ?? 0;

                    if (currentLevel < maxLevel)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(LevelUp), "SetInfo")]
    class LevelUp_SetInfo_Patch
    {
        static void Postfix(LevelUp __instance)
        {
            __instance.InstantDismiss();
            Sniper3DTrainerPlugin.Log?.LogInfo("LevelUp popup skipped via InstantDismiss.");
        }
    }

    [HarmonyPatch(typeof(Store), "CanBuy")]
    class Store_CanBuy_Patch
    {
        static void Postfix(Store.IBuyable storeItem, Store.Currency currency, ref Store.BuyResult __result)
        {
            if (!Sniper3DTrainerPlugin.FreeShopping) return;

            // Don't interfere with weapon buying when UnlockAllWeapons is on
            // (Arsenal should show EquipButton instead of BuyPanel for unlocked weapons)
            if (Sniper3DTrainerPlugin.UnlockAllWeapons && storeItem is WeaponData)
            {
                return;
            }

            // Don't force Succeeded for IAP if the item has no IapId (prevents null reference in BuyPanel)
            if (currency == Store.Currency.Iap && (storeItem == null || storeItem.IapId == null))
            {
                return;
            }

            __result = Store.BuyResult.Succeeded;
            Sniper3DTrainerPlugin.Log?.LogInfo("Store.CanBuy forced to Succeeded (FreeShopping).");
        }
    }

    [HarmonyPatch(typeof(Iap), "Buy")]
    class Iap_Buy_Patch
    {
        static bool Prefix(Iap.Id id, Action<Iap.BuyResult> result, int quantity, string source)
        {
            if (!Sniper3DTrainerPlugin.FreeShopping) return true;

            result(Iap.BuyResult.Succeeded);
            Sniper3DTrainerPlugin.Log?.LogInfo("Iap.Buy bypassed (FreeShopping).");
            return false;
        }
    }

    [HarmonyPatch(typeof(WeaponData), "get_Available")]
    class WeaponData_Available_Patch
    {
        static void Postfix(ref bool __result)
        {
            if (Sniper3DTrainerPlugin.UnlockAllWeapons)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WeaponData), "get_IsTierUsable")]
    class WeaponData_IsTierUsable_Patch
    {
        static void Postfix(ref bool __result)
        {
            if (Sniper3DTrainerPlugin.UnlockAllWeapons)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(WeaponData), "get_UpgradedParamsMods")]
    class WeaponData_UpgradedParamsMods_Patch
    {
        static void Postfix(WeaponData __instance, ref WeaponData.IParams __result)
        {
            if (!Sniper3DTrainerPlugin.ModsAllModes) return;

            try
            {
                // Re-apply mods if we are not in tournament mode (since original skipped them)
                if (LevelController.Instance != null && !LevelController.Instance.LevelData.IsTournament)
                {
                    var @params = __instance.UpgradedParams;
                    if (App.ModManager != null && App.ModManager.EquippedMods != null)
                    {
                        foreach (var modItem in App.ModManager.EquippedMods.Where(m => m.ModInfo is Player.Mods.UpgrStatModData))
                        {
                            modItem.UpdateStats();
                            @params = (modItem.ModInfo as Player.Mods.UpgrStatModData).ApplyMod(@params);
                        }
                        foreach (var modItem in App.ModManager.EquippedMods.Where(m => m.ModInfo is Player.Mods.TempStatModData))
                        {
                            modItem.UpdateStats();
                            @params = (modItem.ModInfo as Player.Mods.TempStatModData).ApplyMod(@params);
                        }
                    }
                    __result = @params;
                }
            }
            catch (Exception ex)
            {
                Sniper3DTrainerPlugin.Log?.LogWarning($"UpgradedParamsMods patch exception: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Player.Weapon), "WeaponDamage")]
    class PlayerWeapon_WeaponDamage_Patch
    {
        static bool Prefix(Player.Weapon __instance, bool headshot, ref double __result)
        {
            try
            {
                if (__instance.Data == null)
                {
                    Sniper3DTrainerPlugin.Log?.LogWarning("WeaponDamage: Data is null, using fallback damage.");
                    __result = 999999.0;
                    return false;
                }

                var upgradedParams = __instance.Data.UpgradedParams;
                if (upgradedParams == null)
                {
                    Sniper3DTrainerPlugin.Log?.LogWarning("WeaponDamage: UpgradedParams is null, using fallback damage.");
                    __result = 999999.0;
                    return false;
                }

                bool isTournament = false;
                bool isArena = false;
                if (LevelController.Instance != null && LevelController.Instance.LevelData != null)
                {
                    isTournament = LevelController.Instance.LevelData.IsTournament;
                    isArena = LevelController.Instance.LevelData.IsArena;
                }

                __result = upgradedParams.DamageOutput(headshot, isTournament, isArena);

                if (Sniper3DTrainerPlugin.DamageMultiplier > 1.0f)
                {
                    __result *= Sniper3DTrainerPlugin.DamageMultiplier;
                }

                return false;
            }
            catch (Exception ex)
            {
                Sniper3DTrainerPlugin.Log?.LogWarning($"WeaponDamage exception: {ex.Message}, using fallback damage.");
                __result = 999999.0;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(LongItemData), "get_Owned")]
    class LongItemData_Owned_Patch
    {
        static void Postfix(LongItemData __instance, ref bool __result)
        {
            if (Sniper3DTrainerPlugin.UnlockAllWeapons && __instance is WeaponData)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(User.Profile), "get_CurrentProgressTier")]
    class UserProfile_CurrentProgressTier_Patch
    {
        static void Postfix(ref int __result)
        {
            if (Sniper3DTrainerPlugin.UnlockAllWeapons)
            {
                __result = 50;
            }
        }
    }

    [HarmonyPatch(typeof(User.Profile), "CanSelectWeapon")]
    class UserProfile_CanSelectWeapon_Patch
    {
        static void Postfix(ref bool __result)
        {
            if (Sniper3DTrainerPlugin.UnlockAllWeapons)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Menu.BuyPanel), "UpdateUI")]
    class BuyPanel_UpdateUI_Patch
    {
        static void Postfix(Menu.BuyPanel __instance)
        {
            if (!Sniper3DTrainerPlugin.UnlockAllWeapons) return;

            var buyableField = __instance.GetType().GetField("_buyable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (buyableField == null) return;

            var buyable = buyableField.GetValue(__instance);
            if (buyable is WeaponData)
            {
                var enabledField = __instance.GetType().GetField("<EnabledOptions>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (enabledField != null)
                {
                    enabledField.SetValue(__instance, Menu.BuyPanel.Options.None);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Menu.BuyPanel), "GetIapLabelString")]
    class BuyPanel_GetIapLabelString_Patch
    {
        static void Postfix(Menu.BuyPanel __instance, ref string __result)
        {
            var buyableField = __instance.GetType().GetField("_buyable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (buyableField == null) return;

            var buyable = buyableField.GetValue(__instance) as Store.IBuyable;
            if (buyable != null && buyable.IapId == null)
            {
                __result = string.Empty;
            }
        }
    }

    // ===== Crash-protection patches for weapon runtime =====

    [HarmonyPatch(typeof(Player.Weapon), "ShootTest")]
    class PlayerWeapon_ShootTest_Patch
    {
        static bool Prefix(Player.Weapon __instance, ref Player.Weapon.HitInfo[] __result)
        {
            if (__instance == null)
            {
                __result = new Player.Weapon.HitInfo[0];
                return false;
            }
            try
            {
                // Unity fake-null check
                if (__instance.gameObject == null || !__instance.gameObject.activeInHierarchy)
                {
                    __result = new Player.Weapon.HitInfo[0];
                    return false;
                }
            }
            catch
            {
                __result = new Player.Weapon.HitInfo[0];
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player.Weapon), "FireFeedback")]
    class PlayerWeapon_FireFeedback_Patch
    {
        static bool Prefix(Player.Weapon __instance)
        {
            if (__instance == null)
                return false;
            try
            {
                if (__instance.gameObject == null || !__instance.gameObject.activeInHierarchy)
                    return false;

                var muzzleField = __instance.GetType().GetField("_muzzleFire", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (muzzleField != null)
                {
                    var muzzle = muzzleField.GetValue(__instance) as GameObject;
                    if (muzzle == null)
                        return false;
                }
            }
            catch
            {
                return true; // Let original try if reflection fails
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Player.CharacterShooter), "DoWeaponFeedback")]
    class CharacterShooter_DoWeaponFeedback_Patch
    {
        static bool Prefix(Player.CharacterShooter __instance)
        {
            if (__instance == null)
                return false;
            try
            {
                var weaponField = __instance.GetType().GetField("_weapon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var weapon = weaponField?.GetValue(__instance) as Player.Weapon;
                if (weapon == null)
                {
                    Sniper3DTrainerPlugin.Log?.LogWarning("DoWeaponFeedback skipped: _weapon is null.");
                    return false;
                }

                var weaponDataField = __instance.GetType().GetField("_weaponData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var weaponData = weaponDataField?.GetValue(__instance) as WeaponData;
                if (weaponData == null)
                {
                    Sniper3DTrainerPlugin.Log?.LogWarning("DoWeaponFeedback skipped: _weaponData is null.");
                    return false;
                }

                // If UpgradedParams throws, skip to avoid crash
                var upgradedParams = weaponData.UpgradedParams;
                if (upgradedParams == null)
                {
                    Sniper3DTrainerPlugin.Log?.LogWarning("DoWeaponFeedback skipped: UpgradedParams is null.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Sniper3DTrainerPlugin.Log?.LogWarning($"DoWeaponFeedback safety check failed: {ex.Message}. Skipping.");
                return false;
            }
            return true;
        }
    }
}
