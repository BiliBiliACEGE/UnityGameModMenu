using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace BATDRCheatMenu
{
    [BepInPlugin("com.ace20.batdr.cheatmenu", "BATDR Cheat Menu", "1.0.0")]
    public class BATDRCheatMenuPlugin : BaseUnityPlugin
    {
        // UI
        private bool _showMenu = false;
        private Rect _windowRect = new Rect(40, 40, 520, 650);
        private Vector2 _scrollPos = Vector2.zero;
        private int _selectedTab = 0;
        private readonly string[] _tabs = { "玩家", "敌人", "物品", "武器/能力", "游戏" };

        // 快捷键配置
        private ConfigEntry<KeyCode> _toggleMenuKey;

        // 单例，供 Harmony Patch 快速访问
        public static BATDRCheatMenuPlugin Instance { get; private set; }

        // 作弊状态
        private bool _godMode = false;
        private bool _infiniteHealth = false;
        private bool _infiniteStamina = false;
        private bool _stealth = false;
        private bool _superSpeed = false;
        private bool _superJump = false;
        private bool _noCooldown = false;
        private bool _freezeEnemies = false;
        private bool _enemiesIgnorePlayer = false;
        private bool _fullLight = false;

        // 速度倍数
        private float _speedMult = 2.5f;
        private float _jumpMult = 2.0f;

        // 反射缓存
        private FieldInfo _playerHealthField;
        private FieldInfo _playerEnemiesField;
        private FieldInfo _playerMovementField;
        private FieldInfo _playerRunTimerField;
        private FieldInfo _playerRunCooldownField;
        private FieldInfo _playerIsRunCooldownField;
        private FieldInfo _playerMovementCanRunField;
        private FieldInfo _playerMovementIsRunLockedField;
        private FieldInfo _enemyHitPointsField;
        private FieldInfo _enemyImmuneField;
        private FieldInfo _enemyAgentField;
        private FieldInfo _enemyControllerField;
        private FieldInfo _statisticsHealthField;
        private FieldInfo _statisticsLifeField;
        private FieldInfo _statisticsFoodField;
        private FieldInfo _statisticsSlugsField;
        private FieldInfo _statisticsCardsField;
        private FieldInfo _statisticsToolkitsField;
        private FieldInfo _statisticsPartsField;
        private FieldInfo _statisticsBatteriesField;
        private FieldInfo _statisticsBatteryCasingsField;
        private FieldInfo _statisticsSpentField;
        private FieldInfo _statisticsCooldownField;
        private FieldInfo _inkDemonTimerField;
        private FieldInfo _inkDemonApproachingField;
        private FieldInfo _inkDemonIsActiveField;
        private FieldInfo _weaponPowerField;
        private FieldInfo _weaponStatusField;
        private FieldInfo _abilityStatusField;
        private MethodInfo _playerSetHealthMethod;
        private MethodInfo _playerHealMethod;
        private MethodInfo _playerDamageMethod;
        private MethodInfo _playerSetCanStealthMethod;
        private MethodInfo _playerSetCombatStatusMethod;
        private MethodInfo _enemySetHitPointsMethod;
        private MethodInfo _enemySetImmuneMethod;
        private MethodInfo _enemyOnDeathMethod;
        private MethodInfo _enemyTakedownMethod;
        private MethodInfo _weaponSetWeaponMethod;
        private MethodInfo _weaponSetStatusMethod;
        private MethodInfo _weaponSetPowerMethod;
        private MethodInfo _abilitySetStatusMethod;
        private MethodInfo _upgradeCheckGetHealthMethod;
        private MethodInfo _statisticsSetHealthMethod;

        private Harmony _harmony;
        private Light _fullLightSource;

        private void Awake()
        {
            Instance = this;

            _toggleMenuKey = Config.Bind(
                "General", "ToggleMenuKey",
                KeyCode.F1,
                "打开/关闭作弊菜单的快捷键（默认 F1，不与游戏冲突）");

            CacheReflection();
            _harmony = new Harmony(Info.Metadata.GUID);
            _harmony.PatchAll();

            Logger.LogInfo("BATDR Cheat Menu loaded. Press F1 to toggle menu.");
        }

        private void CacheReflection()
        {
            var playerType = typeof(Player);
            _playerHealthField = playerType.GetField("m_Health", BindingFlags.NonPublic | BindingFlags.Instance);
            _playerEnemiesField = playerType.GetField("m_Enemies", BindingFlags.NonPublic | BindingFlags.Instance);
            _playerMovementField = playerType.GetField("m_PlayerMovement", BindingFlags.NonPublic | BindingFlags.Instance);
            _playerRunTimerField = playerType.GetField("m_RunTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            _playerRunCooldownField = playerType.GetField("m_RunCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
            _playerIsRunCooldownField = playerType.GetField("m_IsRunCooldown", BindingFlags.NonPublic | BindingFlags.Instance);

            var movementType = typeof(PlayerMovement);
            _playerMovementCanRunField = movementType.GetField("<CanRun>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            _playerMovementIsRunLockedField = movementType.GetField("<IsRunLocked>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

            _playerSetHealthMethod = playerType.GetMethod("SetHealth", BindingFlags.Public | BindingFlags.Instance);
            _playerHealMethod = playerType.GetMethod("Heal", BindingFlags.Public | BindingFlags.Instance);
            _playerDamageMethod = playerType.GetMethod("Damage", BindingFlags.Public | BindingFlags.Instance);
            _playerSetCanStealthMethod = playerType.GetMethod("SetCanStealth", BindingFlags.Public | BindingFlags.Instance);
            _playerSetCombatStatusMethod = playerType.GetProperty("CombatStatus")?.GetSetMethod(true);

            var enemyType = typeof(Enemy);
            _enemyHitPointsField = enemyType.GetField("m_HitPoints", BindingFlags.NonPublic | BindingFlags.Instance);
            _enemyImmuneField = enemyType.GetField("m_IsImmune", BindingFlags.NonPublic | BindingFlags.Instance);
            _enemyAgentField = enemyType.GetField("m_Agent", BindingFlags.NonPublic | BindingFlags.Instance);
            _enemyControllerField = enemyType.GetField("m_Controller", BindingFlags.NonPublic | BindingFlags.Instance);
            _enemySetHitPointsMethod = enemyType.GetMethod("SetHitPoints", BindingFlags.Public | BindingFlags.Instance);
            _enemySetImmuneMethod = enemyType.GetMethod("SetImmune", BindingFlags.Public | BindingFlags.Instance);
            _enemyOnDeathMethod = enemyType.GetMethod("OnDeath", BindingFlags.Public | BindingFlags.Instance);
            _enemyTakedownMethod = enemyType.GetMethod("Takedown", BindingFlags.Public | BindingFlags.Instance);

            var statsType = typeof(PlayerStatistics);
            _statisticsHealthField = statsType.GetField("m_Health", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsLifeField = statsType.GetField("m_Life", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsFoodField = statsType.GetField("m_Food", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsSlugsField = statsType.GetField("m_Slugs", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsCardsField = statsType.GetField("m_Cards", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsToolkitsField = statsType.GetField("m_Toolkits", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsPartsField = statsType.GetField("m_Parts", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsBatteriesField = statsType.GetField("m_Batteries", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsBatteryCasingsField = statsType.GetField("m_BatteryCasings", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsSpentField = statsType.GetField("m_Spent", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsCooldownField = statsType.GetField("m_Cooldown", BindingFlags.NonPublic | BindingFlags.Instance);
            _statisticsSetHealthMethod = statsType.GetMethod("SetHealth", BindingFlags.Public | BindingFlags.Instance);

            var inkDemonType = typeof(InkDemonManager);
            _inkDemonTimerField = inkDemonType.GetField("m_Timer", BindingFlags.NonPublic | BindingFlags.Instance);
            _inkDemonApproachingField = inkDemonType.GetField("m_IsApproaching", BindingFlags.NonPublic | BindingFlags.Instance);
            _inkDemonIsActiveField = inkDemonType.GetField("<IsActive>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

            var weaponType = typeof(WeaponDataObject);
            _weaponPowerField = weaponType.GetField("m_Power", BindingFlags.NonPublic | BindingFlags.Instance);
            _weaponStatusField = weaponType.GetField("m_WeaponStatus", BindingFlags.NonPublic | BindingFlags.Instance);
            _weaponSetWeaponMethod = weaponType.GetMethod("SetWeapon", BindingFlags.Public | BindingFlags.Instance);
            _weaponSetStatusMethod = weaponType.GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance);
            _weaponSetPowerMethod = weaponType.GetMethod("SetPower", BindingFlags.Public | BindingFlags.Instance);

            var abilityType = typeof(AbilityDataDirectory);
            _abilityStatusField = abilityType.GetField("m_AbilityStatus", BindingFlags.NonPublic | BindingFlags.Instance);
            _abilitySetStatusMethod = abilityType.GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance);

            _upgradeCheckGetHealthMethod = typeof(UpgradeCheck).GetMethod("GetHealth", BindingFlags.Public | BindingFlags.Static);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleMenuKey.Value))
            {
                _showMenu = !_showMenu;
                if (_showMenu)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            // 每帧强制刷新鼠标状态，防止被游戏其他逻辑覆盖
            if (_showMenu)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (!GameReady) return;

            if (_infiniteHealth)
            {
                var max = GetMaxHealth();
                if (GameManager.Instance.Player.Health < max)
                    _playerSetHealthMethod?.Invoke(GameManager.Instance.Player, new object[] { (int)max, true });
            }

            if (_infiniteStamina)
            {
                var player = GameManager.Instance.Player;
                if (player != null)
                {
                    // 保持冲刺计时器满、冷却归零
                    _playerRunTimerField?.SetValue(player, 9999f);
                    _playerRunCooldownField?.SetValue(player, 0f);
                    _playerIsRunCooldownField?.SetValue(player, false);

                    var movement = player.PlayerMovement;
                    if (movement != null)
                    {
                        _playerMovementCanRunField?.SetValue(movement, true);
                        _playerMovementIsRunLockedField?.SetValue(movement, false);
                    }
                }
            }

            if (_noCooldown)
            {
                var stats = CurrentStatistics;
                if (stats != null)
                    _statisticsCooldownField?.SetValue(stats, 0f);
            }

            if (_freezeEnemies)
            {
                FreezeAllEnemies(true);
            }

            if (_stealth || _enemiesIgnorePlayer)
            {
                DisableInkDemon();
                var enemies = GetEnemies();
                foreach (var e in enemies)
                    _enemySetImmuneMethod?.Invoke(e, new object[] { true });
            }

            if (_fullLight)
            {
                EnsureFullLight();
            }
            else if (_fullLightSource != null)
            {
                Destroy(_fullLightSource.gameObject);
                _fullLightSource = null;
            }
        }

        private void OnGUI()
        {
            if (!_showMenu) return;

            _windowRect = GUILayout.Window(0, _windowRect, DrawWindow, "BATDR 作弊菜单 v1.0 | F1 关闭", GUILayout.Width(520), GUILayout.Height(650));
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0: DrawPlayerTab(); break;
                case 1: DrawEnemyTab(); break;
                case 2: DrawItemsTab(); break;
                case 3: DrawWeaponAbilityTab(); break;
                case 4: DrawGameTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // ===== 玩家 =====
        private void DrawPlayerTab()
        {
            if (!GameReady)
            {
                GUILayout.Label("未进入游戏或 GameManager 未就绪。");
                return;
            }

            var player = GameManager.Instance.Player;

            GUILayout.Label($"当前生命: {player.Health:0}/{GetMaxHealth():0}");
            GUILayout.Label($"当前区域: {player.CurrentZone}");

            _godMode = GUILayout.Toggle(_godMode, "无敌模式 (God Mode)");
            _infiniteHealth = GUILayout.Toggle(_infiniteHealth, "无限生命 (Infinite Health)");
            _infiniteStamina = GUILayout.Toggle(_infiniteStamina, "无限耐力 (Infinite Stamina)");
            _noCooldown = GUILayout.Toggle(_noCooldown, "无冷却 (No Cooldown)");

            GUILayout.Space(5);
            if (GUILayout.Button("回满生命"))
                _playerSetHealthMethod?.Invoke(player, new object[] { (int)GetMaxHealth(), false });
            if (GUILayout.Button("立即复活 (Respawn)"))
                player.Respawn();

            GUILayout.Space(10);
            GUILayout.Label("=== 移动 ===");
            _superSpeed = GUILayout.Toggle(_superSpeed, "超级速度");
            GUILayout.Label($"速度倍数: {_speedMult:0.0}");
            _speedMult = GUILayout.HorizontalSlider(_speedMult, 1.5f, 10f);

            _superJump = GUILayout.Toggle(_superJump, "超级跳跃");
            GUILayout.Label($"跳跃倍数: {_jumpMult:0.0}");
            _jumpMult = GUILayout.HorizontalSlider(_jumpMult, 1.5f, 10f);

            GUILayout.Space(10);
            GUILayout.Label("=== 隐身 / 环境 ===");
            _stealth = GUILayout.Toggle(_stealth, "隐身 (禁用墨水恶魔侦测)");
            _playerSetCanStealthMethod?.Invoke(player, new object[] { _stealth });
            if (_stealth)
                _playerSetCombatStatusMethod?.Invoke(player, new object[] { CombatStatus.Stealth });

            _fullLight = GUILayout.Toggle(_fullLight, "照亮全图 (玩家周围强光)");

            GUILayout.Space(10);
            GUILayout.Label("=== 位置 ===");
            if (GUILayout.Button("保存当前位置"))
                _savedPosition = player.transform.position;
            if (GUILayout.Button("传送到保存位置") && _savedPosition.HasValue)
                player.transform.position = _savedPosition.Value;

            GUILayout.Space(5);
            if (GUILayout.Button("向上传送 5 米"))
                player.transform.position += Vector3.up * 5f;
            if (GUILayout.Button("向前传送 5 米"))
                player.transform.position += player.transform.forward * 5f;
        }

        private Vector3? _savedPosition;

        // ===== 敌人 =====
        private void DrawEnemyTab()
        {
            if (!GameReady)
            {
                GUILayout.Label("未进入游戏。");
                return;
            }

            var enemies = GetEnemies();
            GUILayout.Label($"场景中的敌人数量: {enemies.Count}");

            _freezeEnemies = GUILayout.Toggle(_freezeEnemies, "冻结所有敌人");
            _enemiesIgnorePlayer = GUILayout.Toggle(_enemiesIgnorePlayer, "敌人无视玩家 (设置免疫)");

            GUILayout.Space(5);
            if (GUILayout.Button("击杀所有敌人"))
            {
                foreach (var e in enemies)
                    _enemyOnDeathMethod?.Invoke(e, null);
            }
            if (GUILayout.Button("处决动画击杀 (Takedown)"))
            {
                foreach (var e in enemies)
                    _enemyTakedownMethod?.Invoke(e, null);
            }
            if (GUILayout.Button("清空敌人生命"))
            {
                foreach (var e in enemies)
                    _enemySetHitPointsMethod?.Invoke(e, new object[] { 0 });
            }
            if (GUILayout.Button("所有敌人 1HP"))
            {
                foreach (var e in enemies)
                    _enemySetHitPointsMethod?.Invoke(e, new object[] { 1 });
            }
            if (GUILayout.Button("所有敌人满血"))
            {
                foreach (var e in enemies)
                    _enemySetHitPointsMethod?.Invoke(e, new object[] { e.MaxHitPoints });
            }
            if (GUILayout.Button("切换敌人无敌 (全部)"))
            {
                foreach (var e in enemies)
                    _enemySetImmuneMethod?.Invoke(e, new object[] { !_enemiesIgnorePlayer });
                _enemiesIgnorePlayer = !_enemiesIgnorePlayer;
            }
        }

        // ===== 物品 =====
        private void DrawItemsTab()
            {
            if (!GameReady)
            {
                GUILayout.Label("未进入游戏。");
                return;
            }

            var stats = CurrentStatistics;
            if (stats == null)
            {
                GUILayout.Label("无法获取玩家统计。");
                return;
            }

            GUILayout.Label($"食物: {stats.Food} | 弹丸: {stats.Slugs} | 卡牌: {stats.Cards}");
            GUILayout.Label($"工具包: {stats.Toolkits} | 零件: {stats.Parts} | 电池: {stats.Batteries}");
            GUILayout.Label($"电池壳: {stats.BatteryCasings}");

            if (GUILayout.Button("获得 99 所有资源"))
            {
                SetStat(_statisticsFoodField, stats, 99);
                SetStat(_statisticsSlugsField, stats, 99);
                SetStat(_statisticsCardsField, stats, 99);
                SetStat(_statisticsToolkitsField, stats, 99);
                SetStat(_statisticsPartsField, stats, 99);
                SetStat(_statisticsBatteriesField, stats, 99);
                SetStat(_statisticsBatteryCasingsField, stats, 99);
                SetStat(_statisticsSpentField, stats, 9999);
            }

            GUILayout.Space(10);
            GUILayout.Label("=== 单独修改 ===");
            DrawItemSpinner("食物", _statisticsFoodField, stats);
            DrawItemSpinner("弹丸 (Slugs)", _statisticsSlugsField, stats);
            DrawItemSpinner("卡牌 (Cards)", _statisticsCardsField, stats);
            DrawItemSpinner("工具包", _statisticsToolkitsField, stats);
            DrawItemSpinner("零件", _statisticsPartsField, stats);
            DrawItemSpinner("电池", _statisticsBatteriesField, stats);
            DrawItemSpinner("电池壳", _statisticsBatteryCasingsField, stats);
        }

        private void DrawItemSpinner(string label, FieldInfo field, PlayerStatistics stats)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            int val = (int)(field?.GetValue(stats) ?? 0);
            GUILayout.Label(val.ToString(), GUILayout.Width(50));
            if (GUILayout.Button("+10", GUILayout.Width(45))) SetStat(field, stats, val + 10);
            if (GUILayout.Button("+99", GUILayout.Width(45))) SetStat(field, stats, val + 99);
            if (GUILayout.Button("MAX", GUILayout.Width(45))) SetStat(field, stats, 999);
            GUILayout.EndHorizontal();
        }

        // ===== 武器/能力 =====
        private void DrawWeaponAbilityTab()
        {
            if (!GameReady)
            {
                GUILayout.Label("未进入游戏。");
                return;
            }

            var weaponData = CurrentWeaponData;
            if (weaponData != null)
            {
                GUILayout.Label($"当前武器: {weaponData.ID} | 状态: {weaponData.WeaponStatus} | 能量: {weaponData.Power}");
            }

            GUILayout.Label("=== 武器等级 ===");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("无武器")) SetWeapon(WeaponType.NONE);
            if (GUILayout.Button("Lv.1")) SetWeapon(WeaponType.LEVEL_1);
            if (GUILayout.Button("Lv.2")) SetWeapon(WeaponType.LEVEL_2);
            if (GUILayout.Button("Lv.3")) SetWeapon(WeaponType.LEVEL_3);
            if (GUILayout.Button("Lv.4")) SetWeapon(WeaponType.LEVEL_4);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("激活武器")) _weaponSetStatusMethod?.Invoke(weaponData, new object[] { WeaponStatus.Active });
            if (GUILayout.Button("禁用武器")) _weaponSetStatusMethod?.Invoke(weaponData, new object[] { WeaponStatus.Inactive });
            if (GUILayout.Button("武器能量 MAX")) _weaponSetPowerMethod?.Invoke(weaponData, new object[] { 999 });
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("=== 能力 ===");
            var abilityDir = CurrentAbilityDirectory;
            if (abilityDir != null)
            {
                GUILayout.Label($"能力状态: {abilityDir.AbilityStatus}");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("激活能力")) _abilitySetStatusMethod?.Invoke(abilityDir, new object[] { AbilityStatus.Active });
                if (GUILayout.Button("禁用能力")) _abilitySetStatusMethod?.Invoke(abilityDir, new object[] { AbilityStatus.Inactive });
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("=== 升级 ===");
            if (GUILayout.Button("解锁全部升级"))
            {
                UnlockAllUpgrades();
            }
        }

        // ===== 游戏 =====
        private void DrawGameTab()
        {
            if (!GameReady)
            {
                GUILayout.Label("未进入游戏。");
                return;
            }

            GUILayout.Label($"游戏状态: {GameManager.Instance.GameState}");
            GUILayout.Label($"已暂停: {GameManager.Instance.IsPaused}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("暂停游戏")) GameManager.Instance.PauseGame();
            if (GUILayout.Button("继续游戏")) GameManager.Instance.UnpauseGame();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("=== 调试/进度 ===");
            if (GUILayout.Button("触发目标完成事件"))
            {
                var evt = typeof(GameManager).GetField("OnObjectiveComplete", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(GameManager.Instance) as EventHandler;
                evt?.Invoke(GameManager.Instance, EventArgs.Empty);
            }

            GUILayout.Space(5);
            GUILayout.Label("章节标题 (显示在屏幕中央):");
            _chapterTitle = GUILayout.TextField(_chapterTitle);
            _chapterName = GUILayout.TextField(_chapterName);
            if (GUILayout.Button("显示章节标题"))
                GameManager.Instance.ShowChapterTitle(_chapterName, _chapterTitle);

            GUILayout.Space(10);
            GUILayout.Label("=== 时间缩放 ===");
            GUILayout.Label($"Time.timeScale: {Time.timeScale:0.00}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.5x")) Time.timeScale = 0.5f;
            if (GUILayout.Button("1.0x")) Time.timeScale = 1f;
            if (GUILayout.Button("2.0x")) Time.timeScale = 2f;
            if (GUILayout.Button("5.0x")) Time.timeScale = 5f;
            GUILayout.EndHorizontal();
        }

        private string _chapterName = "CHAPTER 1";
        private string _chapterTitle = "Title";

        // ===== 辅助方法 =====
        private bool GameReady => GameManager.Instance != null && GameManager.Instance.Player != null;

        private PlayerStatistics CurrentStatistics => GameManager.Instance?.GameData?.CurrentSave?.PlayerData?.Statistics;
        private WeaponDataObject CurrentWeaponData => GameManager.Instance?.GameData?.CurrentSave?.PlayerData?.WeaponData;
        private AbilityDataDirectory CurrentAbilityDirectory => GameManager.Instance?.GameData?.CurrentSave?.PlayerData?.AbilityDirectory;

        private float GetMaxHealth() => (float)(_upgradeCheckGetHealthMethod?.Invoke(null, null) ?? 100f);

        private List<Enemy> GetEnemies()
        {
            var result = new List<Enemy>();
            if (!GameReady) return result;

            var raw = _playerEnemiesField?.GetValue(GameManager.Instance.Player) as List<Character>;
            if (raw == null) return result;

            foreach (var c in raw)
            {
                if (c is Enemy e && e != null)
                    result.Add(e);
            }
            return result;
        }

        private void SetStat(FieldInfo field, PlayerStatistics stats, int value)
        {
            field?.SetValue(stats, Mathf.Clamp(value, 0, 9999));
        }

        private void SetWeapon(WeaponType type)
        {
            var wd = CurrentWeaponData;
            if (wd == null) return;
            _weaponSetWeaponMethod?.Invoke(wd, new object[] { type });
            _weaponSetStatusMethod?.Invoke(wd, new object[] { WeaponStatus.Active });
            _weaponSetPowerMethod?.Invoke(wd, new object[] { 999 });
        }

        private void UnlockAllUpgrades()
        {
            var upgradeDir = GameManager.Instance?.GameData?.CurrentSave?.DataDirectories?.UpgradeDirectory;
            if (upgradeDir == null) return;

            var values = Enum.GetValues(typeof(UpgradeID));
            foreach (UpgradeID id in values)
            {
                try
                {
                    var newObj = new UpgradeDataObject();
                    upgradeDir.Add(id, newObj);
                }
                catch { }
            }
        }

        private void FreezeAllEnemies(bool freeze)
        {
            foreach (var e in GetEnemies())
            {
                var agent = _enemyAgentField?.GetValue(e);
                if (agent != null)
                {
                    var enabledProp = agent.GetType().GetProperty("enabled");
                    enabledProp?.SetValue(agent, !freeze);
                }

                var controller = _enemyControllerField?.GetValue(e) as CharacterController;
                if (controller != null)
                    controller.enabled = !freeze;
            }
        }

        private void DisableInkDemon()
        {
            var inkDemon = FindObjectOfType<InkDemonManager>();
            if (inkDemon == null) return;

            _inkDemonIsActiveField?.SetValue(inkDemon, false);
            _inkDemonTimerField?.SetValue(inkDemon, 0f);
            _inkDemonApproachingField?.SetValue(inkDemon, false);
        }

        private void EnsureFullLight()
        {
            var player = GameManager.Instance?.Player;
            if (player == null) return;

            if (_fullLightSource == null)
            {
                var go = new GameObject("BATDR_FullLight");
                _fullLightSource = go.AddComponent<Light>();
                _fullLightSource.type = LightType.Point;
                _fullLightSource.range = 150f;
                _fullLightSource.intensity = 5f;
                _fullLightSource.color = Color.white;
            }

            _fullLightSource.transform.position = player.transform.position + Vector3.up * 3f;
        }

        // ===== Harmony Patches =====
        [HarmonyPatch(typeof(Player), nameof(Player.Damage))]
        private static class PlayerDamagePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref int damage)
            {
                if (Instance != null && Instance._godMode)
                {
                    damage = 0;
                    return false; // skip original
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.LookX))]
        private static class LookXPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(float TSpeed, ref float __result)
            {
                if (Instance != null && Instance._showMenu)
                {
                    __result = 0f;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.LookY))]
        private static class LookYPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref float __result)
            {
                if (Instance != null && Instance._showMenu)
                {
                    __result = 0f;
                    return false;
                }
                return true;
            }
        }

        // 防止游戏在菜单打开时重新锁定/隐藏鼠标
        [HarmonyPatch(typeof(Cursor), nameof(Cursor.lockState), MethodType.Setter)]
        private static class CursorLockStatePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(CursorLockMode value)
            {
                if (Instance != null && Instance._showMenu && value != CursorLockMode.None)
                    return false; // 跳过游戏的锁定
                return true;
            }
        }

        [HarmonyPatch(typeof(Cursor), nameof(Cursor.visible), MethodType.Setter)]
        private static class CursorVisiblePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(bool value)
            {
                if (Instance != null && Instance._showMenu && !value)
                    return false; // 跳过游戏的隐藏光标
                return true;
            }
        }

        // 防止点击菜单时触发游戏攻击等动作
        [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.Attack))]
        private static class AttackPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                if (Instance != null && Instance._showMenu)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerInput), nameof(PlayerInput.AttackHold))]
        private static class AttackHoldPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                if (Instance != null && Instance._showMenu)
                {
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        // 隐身 / 敌人无视：禁止墨水恶魔重新激活
        [HarmonyPatch(typeof(InkDemonManager), "IsActive", MethodType.Setter)]
        private static class InkDemonActivePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(bool value)
            {
                if (Instance != null && (Instance._stealth || Instance._enemiesIgnorePlayer) && value)
                    return false; // 跳过激活
                return true;
            }
        }

        // ===== 无限耐力相关补丁 =====
        [HarmonyPatch(typeof(PlayerMovement), "IsRunLocked", MethodType.Setter)]
        private static class IsRunLockedSetterPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(bool value)
            {
                if (Instance != null && Instance._infiniteStamina && value)
                    return false; // 禁止锁定奔跑
                return true;
            }
        }

        [HarmonyPatch(typeof(PlayerMovement), "CanRun", MethodType.Setter)]
        private static class CanRunSetterPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(bool value)
            {
                if (Instance != null && Instance._infiniteStamina && !value)
                    return false; // 禁止禁用奔跑
                return true;
            }
        }

        [HarmonyPatch(typeof(UIHUD), "SetSprintBar")]
        private static class SetSprintBarPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref float value, float max)
            {
                if (Instance != null && Instance._infiniteStamina)
                    value = max; // UI 耐力条始终满
            }
        }

        [HarmonyPatch(typeof(UIHUD), "SetRefill")]
        private static class SetRefillPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref bool isFilling)
            {
                if (Instance != null && Instance._infiniteStamina)
                    isFilling = false; // 不需要 refill 动画
            }
        }
    }
}
