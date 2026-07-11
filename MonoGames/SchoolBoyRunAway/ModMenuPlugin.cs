using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.AI;

namespace SchoolBoyRunawayModMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class ModMenuPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.schoolboyrunaway.modmenu";
        public const string PluginName = "SchoolBoy Runaway ModMenu";
        public const string PluginVersion = "1.0.0";

        // 配置
        private ConfigEntry<KeyboardShortcut> _toggleKey;
        private ConfigEntry<bool> _startOpened;

        // 菜单状态
        private bool _menuVisible = false;
        private int _currentTab = 0;
        private Rect _windowRect = new Rect(20f, 20f, 580f, 720f);
        private bool _isDragging = false;
        private Vector2 _dragOffset;

        // 滚动位置
        private Vector2 _scrollPos = Vector2.zero;

        // ===== 玩家作弊状态 =====
        private bool _godMode = false;
        private bool _infiniteAir = false;
        private bool _noClip = false;
        private float _speedMultiplier = 1f;
        private float _jumpMultiplier = 1f;
        private bool _speedEnabled = false;

        // ===== AI 禁用状态 =====
        private bool _disableDad = false;
        private bool _disableMom = false;
        private bool _disableFishman = false;
        private bool _disableAndrew = false;
        private bool _disableDog = false;
        private bool _blindAllAi = false;

        // ===== 时间缩放 =====
        private float _timeScale = 1f;

        // ===== AI 状态控制 =====
        private int _aiTargetIndex = 0; // 0=Dad 1=Mom 2=Fishman 3=Andrew 4=Dog
        private int _aiStateIndex = 0;
        private static readonly string[] AiStateNames = {
            "Idle (待机)", "Chase (追击)", "Catch (抓捕)", "CheckPlayer (检查玩家)",
            "CheckCloset (检查柜子)", "Sleep (睡觉)", "WatchTV (看电视)",
            "WatchOnPlayer (监视玩家)", "Cook (做饭)", "Fishing (钓鱼)",
            "Hide (躲藏)", "House (回屋)", "Runaway (逃跑)"
        };
        // 每个AI支持的状态类名(按 AiStateNames 索引对应,不存在则留空)
        private static readonly string[][] AiStateClassNames = new string[][] {
            new string[] { "DadIdleState", "DadChaseState", "DadCatchState", "DadCheckPlayerState",
                           "DadCheckClosetState", "DadSleepState", "DadWatchTVState", "DadWatchOnPlayerState",
                           "", "", "", "", "" }, // Dad
            new string[] { "MomIdleState", "MomChaseState", "MomCatchState", "MomCheckPlayerState",
                           "MomCheckClosetState", "", "", "", "MomCookState",
                           "", "", "", "" }, // Mom
            new string[] { "FishmanIdleState", "", "", "", "", "", "", "",
                           "", "FishmanFishingState", "FishmanHideState", "FishmanHouseState", "FishmanRunawayState" }, // Fishman
            new string[] { "AndrewIdleState", "", "", "", "", "", "", "",
                           "", "", "", "", "AndrewRunawayState" }, // Andrew
            new string[] { "DogIdleState", "", "", "", "", "", "", "",
                           "", "", "", "", "" }, // Dog
        };

        // ===== GUI 样式缓存 =====
        private GUIStyle _headerStyle;
        private GUIStyle _tabStyle;
        private GUIStyle _tabActiveStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _toggleStyle;
        private Texture2D _bgTex;
        private Texture2D _tabTex;
        private Texture2D _tabActiveTex;
        private Texture2D _boxTex;
        private bool _stylesReady = false;

        // AI 类型名映射
        private static readonly string[] AiTypeNames = { "AI.Dad.Dad", "AI.Mom.Mom", "AI.Fishman.Fishman", "AI.Andrew.Andrew", "AI.Dog.Dog" };

        // ===== 变身(附身)状态 =====
        private int _selectedAiIndex = 0;
        private static readonly string[] AiDisplayNames = { "Dad (爸爸)", "Mom (妈妈)", "Fishman (鱼人)", "Andrew (安德鲁)", "Dog (狗)" };
        private static readonly string[] AiFieldNames = { "dad", "mom", "fishman", "andrew", "dog" };
        private PossessionController _possession;
        private float _possessSpeed = 3f;

        // ===== 对象选择器状态 =====
        private bool _selectMode = false;
        private GameObject _selectedObj = null;
        private float _objPosX, _objPosY, _objPosZ;
        private float _objRotX, _objRotY, _objRotZ;
        private float _objScaleX = 1f, _objScaleY = 1f, _objScaleZ = 1f;
        private float _objScaleUniform = 1f;
        private bool _useUniformScale = true;
        private string _objSearchName = "";
        // 输入模式: true=滑块, false=手动输入
        private bool _objUseSlider = true;
        // 滑块步长(拖动滑块时的增量)
        private float _objPosStep = 1f;
        private float _objRotStep = 15f;
        private float _objScaleStep = 0.1f;
        // 手动输入文本缓存(避免光标跳动)
        private string _objPosXText, _objPosYText, _objPosZText;
        private string _objRotXText, _objRotYText, _objRotZText;
        private string _objScaleXText, _objScaleYText, _objScaleZText;
        private string _objScaleUniformText;

        // ===== 结局触发状态 =====
        private int _selectedEndingIndex = 0;
        private static readonly string[] EndingNames = {
            "Door - Left (门左)", "Door - Front (门前)", "Door - Right (门右)", "Door - Back (门后)",
            "Roof - Left (屋顶左)", "Roof - Front (屋顶前)", "Roof - Right (屋顶右)",
            "Gate Ending (大门结局)", "Roof Ending (屋顶结局)", "Belt Ending (皮带结局)",
            "Backrooms (后室)"
        };
        private static readonly string[] DoorEndingParams = { "Left", "Front", "Right", "Back", "Roof Left", "Roof Front", "Roof Right" };

        // ===== 传送房间状态 =====
        private int _selectedRoomIndex = 0;
        private string[] _roomNames = new string[0];
        private WayPointPlayer[] _cachedWaypoints;

        // ===== 生成物品状态 =====
        private System.Collections.Generic.List<GameObject> _allItemPrefabs = new System.Collections.Generic.List<GameObject>();
        private System.Collections.Generic.List<MonoBehaviour> _itemSpawnSources = new System.Collections.Generic.List<MonoBehaviour>();
        private Vector2 _spawnItemsScroll = Vector2.zero;

        // 菜单开启时临时禁用的玩家脚本
        private bool _menuDisabledPlayerLook = false;
        private bool _savedFpControlEnabled = false;

        // 被禁用的 AI_Vision 组件(用于上帝模式/变身期间,防止AI检测玩家)
        private List<MonoBehaviour> _disabledVisionComponents = new List<MonoBehaviour>();
        // AI_Vision 是否因变身而被禁用(与 GodMode 区分,避免互相干扰)
        private bool _visionDisabledByPossession = false;

        // 上次刷新时间(用于持续应用某些作弊)
        private float _lastRefresh = 0f;

        private void Awake()
        {
            _toggleKey = Config.Bind("通用", "开关按键", new KeyboardShortcut(KeyCode.F1), "显示/隐藏 ModMenu 的按键");
            _startOpened = Config.Bind("通用", "启动时打开", false, "游戏启动时自动打开菜单");
            // 初始化附身控制器
            var go = new GameObject("PossessionController");
            DontDestroyOnLoad(go);
            _possession = go.AddComponent<PossessionController>();
            Logger.LogInfo($"{PluginName} v{PluginVersion} 已加载 — 按 {_toggleKey.Value} 打开菜单");
        }

        private void Start()
        {
            _menuVisible = _startOpened.Value;
        }

        private void Update()
        {
            if (_toggleKey.Value.IsDown())
            {
                _menuVisible = !_menuVisible;
                UpdateCursorState();
            }

            // 对象选择模式:鼠标左键点击场景对象
            if (_selectMode && !_menuVisible && Input.GetMouseButtonDown(0))
            {
                TrySelectObjectByRay();
            }

            // 每帧应用持续型作弊
            ApplyContinuousCheats();
        }

        /// <summary>
        /// 从摄像机发出射线,选中鼠标准星对准的对象
        /// </summary>
        private void TrySelectObjectByRay()
        {
            try
            {
                var cam = Camera.main;
                if (cam == null) return;
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 1000f))
                {
                    if (hit.collider != null && hit.collider.gameObject != null)
                    {
                        SelectObject(hit.collider.gameObject);
                        Logger.LogInfo($"已选中对象: {hit.collider.gameObject.name}");
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 只在菜单可见性切换时设置一次光标,避免每帧覆盖游戏自身的光标管理。
        /// 菜单可见:解锁光标。
        /// 菜单关闭:仅在游戏进行中(玩家控制器存在且启用)时恢复锁定,
        ///          主菜单等场景不干预,交由游戏自身管理。
        /// </summary>
        private void UpdateCursorState()
        {
            if (_menuVisible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // 禁用玩家视角控制脚本,防止鼠标移动带动视角
                if (!_possession.IsPossessing && !_menuDisabledPlayerLook)
                {
                    var ctrl = GameHelpers.FP_Control;
                    if (ctrl != null && ctrl.enableController)
                    {
                        _savedFpControlEnabled = true;
                        ctrl.EnableController(false);
                    }
                    _menuDisabledPlayerLook = true;
                }
            }
            else
            {
                // 恢复玩家视角控制
                if (_menuDisabledPlayerLook)
                {
                    var ctrl = GameHelpers.FP_Control;
                    if (ctrl != null && _savedFpControlEnabled)
                    {
                        ctrl.EnableController(true);
                        _savedFpControlEnabled = false;
                    }
                    _menuDisabledPlayerLook = false;
                }

                // 变身期间:锁定鼠标到窗口中心,使 Input.GetAxis 返回相对移动值
                if (_possession.IsPossessing)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else
                {
                    // 非变身:仅在玩家控制启用时恢复锁定
                    var ctrl2 = GameHelpers.FP_Control;
                    if (ctrl2 != null && ctrl2.enableController)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
            }
        }

        private void ApplyContinuousCheats()
        {
            // 上帝模式: 免伤 + 免死 + 免被抓(禁用AI视觉 + 强制清除抓捕/追击状态)
            if (_godMode)
            {
                var hp = GameHelpers.FP_Health;
                if (hp != null)
                {
                    if (hp.FP_HealthIsOn) hp.FP_HealthIsOn = false;
                    GameHelpers.SetField(hp, "isDied", false);
                }

                // 禁用所有 AI_Vision 组件,让AI完全看不到玩家
                // 注意:变身期间已经禁用了AI_Vision,这里不重复禁用
                if (!_visionDisabledByPossession)
                    DisableAllAiVision();
                // 重置所有AI的抓捕/追击/惩罚状态
                ResetAllAiCatchState();
            }
            else
            {
                // 关闭上帝模式时恢复 AI_Vision(仅当不在变身期间)
                if (!_visionDisabledByPossession)
                    RestoreAllAiVision();
            }

            // 无限呼吸
            if (_infiniteAir)
            {
                var hp = GameHelpers.FP_Health;
                if (hp != null)
                {
                    float max = GameHelpers.GetFloat(hp, "maxAirReserve");
                    if (max >= 0f) GameHelpers.SetField(hp, "currentAirReserve", max);
                }
            }

            // 时间缩放
            if (Math.Abs(Time.timeScale - _timeScale) > 0.001f)
                Time.timeScale = _timeScale;

            // 穿墙(关闭碰撞)
            if (_noClip)
            {
                var ctrl = GameHelpers.FP_Control;
                if (ctrl != null)
                {
                    var body = GameHelpers.FP_Body;
                    if (body != null)
                    {
                        var col = body.GetComponent<Collider>();
                        if (col != null && col.enabled) col.enabled = false;
                    }
                    var playerCol = ctrl.GetComponent<Collider>();
                    if (playerCol != null && playerCol.enabled) playerCol.enabled = false;
                }
            }
        }

        /// <summary>
        /// 重置所有AI的抓捕/追击/惩罚/检测状态。
        /// 游戏的"被抓"由AI设置 isPlayerCatched=true 触发,仅关闭FP_Health无效。
        /// 需要每帧强制清除所有AI的相关标志。
        /// </summary>
        private void ResetAllAiCatchState()
        {
            foreach (var typeName in AiTypeNames)
            {
                try
                {
                    var ai = GameHelpers.FindByType(typeName);
                    if (ai == null) continue;
                    // 这些字段在 Dad/Mom/Fishman/Andrew/Dog 上通用(可能部分不存在,反射会忽略)
                    GameHelpers.SetField(ai, "isPlayerCatched", false);
                    GameHelpers.SetField(ai, "isChasingPlayer", false);
                    GameHelpers.SetField(ai, "isPlayerDetected", false);
                    GameHelpers.SetField(ai, "isPlayerHasBeenDetected", false);
                    GameHelpers.SetField(ai, "isNeedPlayerPunishe", false);
                    GameHelpers.SetField(ai, "isNeedCatchAfterScolds", false);
                    GameHelpers.SetField(ai, "isInterestedInPlayer", false);
                    GameHelpers.SetField(ai, "isSeekingPlayer", false);
                    GameHelpers.SetField(ai, "isForceDetectPlayerActive", false);
                }
                catch { }
            }
        }

        /// <summary>
        /// 禁用场景中所有 AI_Vision 组件,让AI完全看不到玩家。
        /// 用于上帝模式和变身期间,防止AI触发检测/抓取逻辑。
        /// </summary>
        private void DisableAllAiVision()
        {
            if (_disabledVisionComponents.Count > 0) return; // 已禁用,避免重复
            _disabledVisionComponents.Clear();
            var allMb = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in allMb)
            {
                if (mb == null) continue;
                if (mb.GetType().Name != "AI_Vision") continue;
                if (mb.enabled)
                {
                    // 禁用前重置检测状态,防止禁用后保持 true
                    GameHelpers.SetField(mb, "isTargetInSight", false);
                    GameHelpers.SetField(mb, "isDetectTarget", false);
                    _disabledVisionComponents.Add(mb);
                    try { mb.enabled = false; } catch { }
                }
            }
        }

        /// <summary>
        /// 恢复之前被禁用的 AI_Vision 组件。
        /// </summary>
        private void RestoreAllAiVision()
        {
            foreach (var mb in _disabledVisionComponents)
            {
                if (mb != null)
                {
                    try { mb.enabled = true; } catch { }
                }
            }
            _disabledVisionComponents.Clear();
        }

        /// <summary>
        /// 变身开始时调用:禁用所有AI_Vision,防止AI检测到玩家或克隆体。
        /// </summary>
        public void NotifyPossessionStart()
        {
            _visionDisabledByPossession = true;
            DisableAllAiVision();
        }

        /// <summary>
        /// 变身结束时调用:恢复AI_Vision(除非上帝模式仍开启)。
        /// </summary>
        public void NotifyPossessionEnd()
        {
            _visionDisabledByPossession = false;
            if (!_godMode)
                RestoreAllAiVision();
        }

        private void OnGUI()
        {
            if (!_menuVisible) return;

            InitStyles();
            _windowRect = GUI.Window(98765, _windowRect, DrawWindow, "", GUIStyle.none);
        }

        private void InitStyles()
        {
            if (_stylesReady) return;

            _bgTex = MakeTex(2, 2, new Color(0.08f, 0.09f, 0.12f, 0.96f));
            _tabTex = MakeTex(2, 2, new Color(0.15f, 0.17f, 0.22f, 0.95f));
            _tabActiveTex = MakeTex(2, 2, new Color(0.95f, 0.55f, 0.12f, 0.98f));
            _boxTex = MakeTex(2, 2, new Color(0.12f, 0.14f, 0.18f, 0.9f));

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.95f, 0.55f, 0.12f) }
            };

            _tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white, background = _tabTex },
                hover = { textColor = new Color(1f, 0.8f, 0.5f), background = _tabTex },
                active = { textColor = Color.white, background = _tabTex }
            };

            _tabActiveStyle = new GUIStyle(_tabStyle)
            {
                normal = { textColor = Color.white, background = _tabActiveTex },
                hover = { textColor = Color.white, background = _tabActiveTex }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.92f, 0.92f, 0.95f) }
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _boxTex }
            };

            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.92f, 0.92f, 0.95f) },
                onNormal = { textColor = new Color(0.4f, 1f, 0.5f) }
            };

            _stylesReady = true;
        }

        private void DrawWindow(int id)
        {
            // 背景
            GUI.DrawTexture(new Rect(0, 0, _windowRect.width, _windowRect.height), _bgTex);

            // 标题栏(可拖动)
            var headerRect = new Rect(0, 0, _windowRect.width, 36);
            GUI.DrawTexture(headerRect, _tabActiveTex);
            GUI.Label(new Rect(12, 8, 400, 24), "  SchoolBoy Runaway  ModMenu", _headerStyle);

            // 关闭按钮
            if (GUI.Button(new Rect(_windowRect.width - 36, 6, 28, 24), "X"))
            {
                _menuVisible = false;
                return;
            }

            // 刷新引用按钮
            if (GUI.Button(new Rect(_windowRect.width - 72, 6, 32, 24), "↻"))
            {
                GameHelpers.ClearCache();
            }

            // 拖动处理
            HandleDrag(headerRect);

            // 选项卡栏(两行布局,每行5个)
            var tabNames = new[] { "玩家", "AI", "变身", "对象", "内置菜单", "结局", "传送", "物品", "游戏", "关于" };
            var tabW = _windowRect.width / 5f;
            var tabH = 28;
            for (int i = 0; i < tabNames.Length; i++)
            {
                int row = i / 5;
                int col = i % 5;
                var style = i == _currentTab ? _tabActiveStyle : _tabStyle;
                if (GUI.Button(new Rect(col * tabW, 38 + row * tabH, tabW, tabH), tabNames[i], style))
                    _currentTab = i;
            }

            // 内容区域
            var contentRect = new Rect(8, 38 + 2 * tabH + 4, _windowRect.width - 16, _windowRect.height - (38 + 2 * tabH + 12));
            GUILayout.BeginArea(contentRect);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Width(contentRect.width), GUILayout.Height(contentRect.height));

            switch (_currentTab)
            {
                case 0: DrawPlayerTab(); break;
                case 1: DrawAiTab(); break;
                case 2: DrawPossessionTab(); break;
                case 3: DrawObjectSelectorTab(); break;
                case 4: DrawBuiltInMenuTab(); break;
                case 5: DrawEndingTab(); break;
                case 6: DrawTeleportTab(); break;
                case 7: DrawSpawnItemsTab(); break;
                case 8: DrawGameTab(); break;
                case 9: DrawAboutTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 36));
        }

        private void HandleDrag(Rect headerRect)
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition))
            {
                _isDragging = true;
                _dragOffset = e.mousePosition;
            }
            if (e.type == EventType.MouseUp)
                _isDragging = false;
        }

        // ==================== 玩家选项卡 ====================
        private void DrawPlayerTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("玩家作弊", _headerStyle);
            GUILayout.Space(6);

            // 检测游戏对象是否可用
            var body = GameHelpers.FP_Body;
            var hp = GameHelpers.FP_Health;
            var ctrl = GameHelpers.FP_Control;

            if (ctrl == null)
            {
                GUILayout.Label("⚠ 玩家控制器未加载,请进入游戏后刷新。", _labelStyle);
                if (GUILayout.Button("刷新引用", GUILayout.Height(28))) GameHelpers.ClearCache();
                return;
            }

            // 上帝模式
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            var newGod = GUILayout.Toggle(_godMode, " 上帝模式 (免伤/免死)", _toggleStyle);
            if (newGod != _godMode)
            {
                _godMode = newGod;
                if (!_godMode)
                {
                    // 恢复时重新开启受伤系统
                    if (hp != null) hp.FP_HealthIsOn = true;
                }
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 无限呼吸
            GUILayout.Space(6);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            _infiniteAir = GUILayout.Toggle(_infiniteAir, " 无限呼吸 (水下不缺氧)", _toggleStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 穿墙
            GUILayout.Space(6);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            _noClip = GUILayout.Toggle(_noClip, " 穿墙模式 (关闭碰撞体)", _toggleStyle);
            if (_noClip)
            {
                GUILayout.Label("  提示: 开启后可能下落,配合跳跃使用", _labelStyle);
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 速度倍率
            GUILayout.Space(6);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            _speedEnabled = GUILayout.Toggle(_speedEnabled, " 移动速度倍率", _toggleStyle);
            if (_speedEnabled)
            {
                if (!GameHelpers.HasCachedSpeeds && body != null)
                    GameHelpers.CacheOriginalSpeeds();

                GUILayout.Label($"  倍率: {_speedMultiplier:F2}x", _labelStyle);
                _speedMultiplier = GUILayout.HorizontalSlider(_speedMultiplier, 0.1f, 10f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("1x")) _speedMultiplier = 1f;
                if (GUILayout.Button("2x")) _speedMultiplier = 2f;
                if (GUILayout.Button("3x")) _speedMultiplier = 3f;
                if (GUILayout.Button("5x")) _speedMultiplier = 5f;
                GUILayout.EndHorizontal();

                if (body != null)
                {
                    GameHelpers.ApplySpeedMultiplier(_speedMultiplier);
                    GUILayout.Label($"  行走: {body.walkSpeed:F2}  跑步: {body.runSpeed:F2}", _labelStyle);
                }
            }
            else
            {
                if (GameHelpers.HasCachedSpeeds)
                    GameHelpers.ResetSpeeds();
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 跳跃倍率
            GUILayout.Space(6);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label($"跳跃力度倍率: {_jumpMultiplier:F2}x", _labelStyle);
            _jumpMultiplier = GUILayout.HorizontalSlider(_jumpMultiplier, 0.5f, 10f);
            if (body != null && GameHelpers.HasCachedSpeeds)
            {
                body.jumpForce = GameHelpers.OrigJumpForce * _jumpMultiplier;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1x")) _jumpMultiplier = 1f;
            if (GUILayout.Button("2x")) _jumpMultiplier = 2f;
            if (GUILayout.Button("3x")) _jumpMultiplier = 3f;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 信息显示
            GUILayout.Space(8);
            GUILayout.Label("玩家信息", _headerStyle);
            if (body != null)
            {
                GUILayout.Label($"  行走速度: {body.walkSpeed:F2}", _labelStyle);
                GUILayout.Label($"  跑步速度: {body.runSpeed:F2}", _labelStyle);
                GUILayout.Label($"  跳跃力度: {body.jumpForce:F2}", _labelStyle);
                GUILayout.Label($"  蹲伏速度: {body.crouchSpeed:F2}", _labelStyle);
                GUILayout.Label($"  攀爬速度: {body.climbingSpeed:F2}", _labelStyle);
                GUILayout.Label($"  水中速度: {body.waterSpeed:F2}", _labelStyle);
            }
            if (hp != null)
            {
                float air = GameHelpers.GetFloat(hp, "currentAirReserve");
                float maxAir = GameHelpers.GetFloat(hp, "maxAirReserve");
                GUILayout.Label($"  受伤系统: {(hp.FP_HealthIsOn ? "开启" : "关闭")}", _labelStyle);
                if (air >= 0f && maxAir >= 0f)
                    GUILayout.Label($"  呼吸: {air:F1} / {maxAir:F1}", _labelStyle);
            }
        }

        // ==================== AI 选项卡 ====================
        private void DrawAiTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("AI 控制", _headerStyle);
            GUILayout.Space(6);

            var gm = GameHelpers.GameManager;
            if (gm == null)
            {
                GUILayout.Label("⚠ GameManager 未加载,请进入游戏后刷新。", _labelStyle);
                if (GUILayout.Button("刷新引用", GUILayout.Height(28))) GameHelpers.ClearCache();
                return;
            }

            // 各 AI 禁用开关
            DrawAiToggle("爸爸 (Dad)", ref _disableDad, gm, "dad", "AI.Dad.Dad");
            DrawAiToggle("妈妈 (Mom)", ref _disableMom, gm, "mom", "AI.Mom.Mom");
            DrawAiToggle("鱼人 (Fishman)", ref _disableFishman, gm, "fishman", "AI.Fishman.Fishman");
            DrawAiToggle("安德鲁 (Andrew)", ref _disableAndrew, gm, "andrew", "AI.Andrew.Andrew");
            DrawAiToggle("狗 (Dog)", ref _disableDog, gm, "dog", "AI.Dog.Dog");

            GUILayout.Space(8);

            // 全部禁用/启用
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("全部冻结", GUILayout.Height(30)))
            {
                _disableDad = _disableMom = _disableFishman = _disableAndrew = _disableDog = true;
                ApplyAiDisable("dad", true);
                ApplyAiDisable("mom", true);
                ApplyAiDisable("fishman", true);
                ApplyAiDisable("andrew", true);
                ApplyAiDisable("dog", true);
            }
            if (GUILayout.Button("全部解冻", GUILayout.Height(30)))
            {
                _disableDad = _disableMom = _disableFishman = _disableAndrew = _disableDog = false;
                ApplyAiDisable("dad", false);
                ApplyAiDisable("mom", false);
                ApplyAiDisable("fishman", false);
                ApplyAiDisable("andrew", false);
                ApplyAiDisable("dog", false);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            _blindAllAi = GUILayout.Toggle(_blindAllAi, " 致盲所有 AI (关闭视野)", _toggleStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            if (_blindAllAi)
            {
                ApplyBlindAll();
            }

            GUILayout.Space(8);
            GUILayout.Label("说明: 冻结会禁用 AI 的导航代理(停止移动)。", _labelStyle);
            GUILayout.Label("致盲会关闭 AI 视野系统(无法发现玩家)。", _labelStyle);

            // ===== AI 状态控制 =====
            GUILayout.Space(10);
            GUILayout.Label("AI 状态控制", _headerStyle);
            GUILayout.Space(4);

            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label("目标 AI:", _labelStyle);
            _aiTargetIndex = GUILayout.SelectionGrid(_aiTargetIndex, AiDisplayNames, 1, GUILayout.Height(20 * AiDisplayNames.Length));
            GUILayout.Space(6);
            GUILayout.Label("目标状态:", _labelStyle);
            _aiStateIndex = GUILayout.SelectionGrid(_aiStateIndex, AiStateNames, 1, GUILayout.Height(20 * AiStateNames.Length));
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("♻ 重置该 AI", GUILayout.Height(32)))
            {
                ResetAiState(_aiTargetIndex);
            }
            if (GUILayout.Button("▶ 进入该状态", GUILayout.Height(32)))
            {
                SetAiToState(_aiTargetIndex, _aiStateIndex);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.Label("提示: 重置会清除AI的所有抓捕/追击标志并恢复默认状态。", _labelStyle);
            GUILayout.Label("      进入状态会强制AI切换到指定状态(需该AI支持)。", _labelStyle);
        }

        /// <summary>
        /// 重置指定AI:调用其 Reset 方法(如 DadReset/MomReset),并清除所有抓捕/检测字段。
        /// </summary>
        private void ResetAiState(int aiIndex)
        {
            try
            {
                var ai = ResolveAiTarget(aiIndex);
                if (ai == null)
                {
                    Logger.LogWarning($"未找到 {AiDisplayNames[aiIndex]},可能未在当前场景。");
                    return;
                }
                var type = ai.GetType();
                // 尝试调用 XxxReset 方法(Dad->DadReset, Mom->MomReset, 其他可能无)
                string resetMethodName = type.Name + "Reset";
                var resetMethod = type.GetMethod(resetMethodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (resetMethod != null)
                {
                    resetMethod.Invoke(ai, null);
                    Logger.LogInfo($"已调用 {type.Name}.{resetMethodName}()");
                }
                else
                {
                    // 没有专门的 Reset 方法,只清除字段
                    Logger.LogInfo($"{type.Name} 无 {resetMethodName} 方法,仅清除状态字段。");
                }
                // 强制清除抓捕/检测字段(所有AI通用)
                GameHelpers.SetField(ai, "isPlayerCatched", false);
                GameHelpers.SetField(ai, "isChasingPlayer", false);
                GameHelpers.SetField(ai, "isPlayerDetected", false);
                GameHelpers.SetField(ai, "isPlayerHasBeenDetected", false);
                GameHelpers.SetField(ai, "isNeedPlayerPunishe", false);
                GameHelpers.SetField(ai, "isNeedCatchAfterScolds", false);
                GameHelpers.SetField(ai, "isInterestedInPlayer", false);
                GameHelpers.SetField(ai, "isSeekingPlayer", false);
                GameHelpers.SetField(ai, "isForceDetectPlayerActive", false);
                GameHelpers.SetField(ai, "scaleDetection", 0f);
                Logger.LogInfo($"已重置 {AiDisplayNames[aiIndex]} 状态。");
            }
            catch (Exception e)
            {
                Logger.LogError($"重置AI失败: {e}");
            }
        }

        /// <summary>
        /// 强制让指定AI进入指定状态。
        /// 通过反射创建状态类实例,调用AI的 NextState(State) 方法。
        /// </summary>
        private void SetAiToState(int aiIndex, int stateIndex)
        {
            try
            {
                var stateClassName = AiStateClassNames[aiIndex][stateIndex];
                if (string.IsNullOrEmpty(stateClassName))
                {
                    Logger.LogWarning($"{AiDisplayNames[aiIndex]} 不支持状态 {AiStateNames[stateIndex]}。");
                    return;
                }
                var ai = ResolveAiTarget(aiIndex);
                if (ai == null)
                {
                    Logger.LogWarning($"未找到 {AiDisplayNames[aiIndex]},可能未在当前场景。");
                    return;
                }
                var aiType = ai.GetType();
                // 查找状态类(无命名空间)
                var stateType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(stateClassName))
                    .FirstOrDefault(t => t != null);
                if (stateType == null)
                {
                    Logger.LogWarning($"未找到状态类: {stateClassName}");
                    return;
                }
                // 创建状态实例
                var stateInstance = Activator.CreateInstance(stateType);
                if (stateInstance == null)
                {
                    Logger.LogWarning($"无法创建状态实例: {stateClassName}");
                    return;
                }
                // 调用 NextState(State) 方法
                var nextStateMethod = aiType.GetMethod("NextState",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (nextStateMethod == null)
                {
                    Logger.LogWarning($"{aiType.Name} 无 NextState 方法。");
                    return;
                }
                nextStateMethod.Invoke(ai, new object[] { stateInstance });
                Logger.LogInfo($"已让 {AiDisplayNames[aiIndex]} 进入状态 {stateClassName}。");
            }
            catch (Exception e)
            {
                Logger.LogError($"设置AI状态失败: {e}");
            }
        }

        private void DrawAiToggle(string label, ref bool state, object gameManager, string fieldName, string typeFullName)
        {
            GUILayout.Space(4);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            var newState = GUILayout.Toggle(state, $" 禁用 {label}", _toggleStyle);
            if (newState != state)
            {
                state = newState;
                ApplyAiDisable(fieldName, state);
            }
            GUILayout.Space(4);

            // 显示该 AI 状态
            var aiObj = GameHelpers.GetField<UnityEngine.Object>(gameManager, fieldName);
            if (aiObj != null)
            {
                var aiMono = aiObj as MonoBehaviour;
                if (aiMono != null && aiMono.gameObject != null)
                {
                    var pos = aiMono.transform.position;
                    GUILayout.Label($"  位置: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", _labelStyle);
                }
            }
            else
            {
                // 尝试通过类型查找
                var found = GameHelpers.FindByType(typeFullName);
                if (found == null)
                    GUILayout.Label("  (该 AI 未在当前场景)", _labelStyle);
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();
        }

        private void ApplyAiDisable(string fieldName, bool disable)
        {
            var gm = GameHelpers.GameManager;
            if (gm == null) return;

            var aiObj = GameHelpers.GetField<UnityEngine.Object>(gm, fieldName) as MonoBehaviour;
            if (aiObj == null)
            {
                // 尝试通过类型名查找
                var typeMap = new Dictionary<string, string>
                {
                    {"dad", "AI.Dad.Dad"}, {"mom", "AI.Mom.Mom"},
                    {"fishman", "AI.Fishman.Fishman"}, {"andrew", "AI.Andrew.Andrew"},
                    {"dog", "AI.Dog.Dog"}
                };
                if (typeMap.ContainsKey(fieldName))
                    aiObj = GameHelpers.FindByType(typeMap[fieldName]) as MonoBehaviour;
            }
            if (aiObj == null || aiObj.gameObject == null) return;

            // 禁用/启用 NavMeshAgent
            var agents = aiObj.GetComponentsInChildren<NavMeshAgent>();
            foreach (var agent in agents)
                agent.enabled = !disable;

            // 禁用/启用 Animator(可选,让AI静止)
            var animators = aiObj.GetComponentsInChildren<Animator>();
            foreach (var anim in animators)
                anim.enabled = !disable;

            // 禁用/启用 AI 主脚本
            if (disable)
                aiObj.enabled = false;
            else
                aiObj.enabled = true;
        }

        private void ApplyBlindAll()
        {
            var gm = GameHelpers.GameManager;
            if (gm == null) return;

            var aiFields = new[] { "dad", "mom", "fishman", "andrew", "dog" };
            var aiTypes = new[] { "AI.Dad.Dad", "AI.Mom.Mom", "AI.Fishman.Fishman", "AI.Andrew.Andrew", "AI.Dog.Dog" };

            for (int i = 0; i < aiFields.Length; i++)
            {
                var aiObj = GameHelpers.GetField<UnityEngine.Object>(gm, aiFields[i]) as MonoBehaviour;
                if (aiObj == null)
                    aiObj = GameHelpers.FindByType(aiTypes[i]) as MonoBehaviour;
                if (aiObj == null || aiObj.gameObject == null) continue;

                // 查找视野相关组件
                var visions = aiObj.GetComponentsInChildren<MonoBehaviour>();
                foreach (var v in visions)
                {
                    if (v == null) continue;
                    var name = v.GetType().Name;
                    if (name.Contains("Vision") || name.Contains("Sight") || name.Contains("View"))
                        v.enabled = false;
                }

                // 通过反射设置 vision 字段(Dad 有 visionDad 等)
                var visionFields = new[] { "visionDad", "visionMom", "visionFishman", "visionAndrew", "visionDog", "vision" };
                foreach (var vf in visionFields)
                {
                    var vis = GameHelpers.GetField<MonoBehaviour>(aiObj, vf);
                    if (vis != null) vis.enabled = false;
                }
            }
        }

        // ==================== 内置菜单选项卡 ====================
        private void DrawBuiltInMenuTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("内置菜单(开发者菜单)", _headerStyle);
            GUILayout.Space(6);

            // 说明
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(6);
            GUILayout.Label("游戏内置一个开发者调试菜单,包含 NPC 变换、", _labelStyle);
            GUILayout.Label("位置调整等功能。正常情况下需要达成特定游戏", _labelStyle);
            GUILayout.Label("流程(达成条件后 MenuWarning 弹出确认)才会开启。", _labelStyle);
            GUILayout.Space(4);
            GUILayout.Label("此处可直接解锁该菜单。", _labelStyle);
            GUILayout.Space(8);
            // 作弊结局警告
            var warnStyle = new GUIStyle(_labelStyle);
            warnStyle.normal.textColor = new Color(1f, 0.4f, 0.3f);
            warnStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label("⚠ 警告:启用内置菜单会导致结局固定为作弊结局!", warnStyle);
            GUILayout.Space(4);
            GUILayout.Label("解锁 hasAgreed 后,游戏检测到该标志会被判定为", _labelStyle);
            GUILayout.Label("作弊状态,通关时将强制进入作弊结局(无法获得", _labelStyle);
            GUILayout.Label("正常结局)。如需正常通关,请勿启用此功能。", _labelStyle);
            GUILayout.Space(6);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // MenuWarning.hasAgreed 状态
            GUILayout.Label("菜单解锁状态", _headerStyle);
            bool hasAgreed = GameHelpers.GetStaticBool(typeof(MenuWarning), "hasAgreed");
            GUILayout.Space(4);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(6);
            GUILayout.Label($"  hasAgreed = {hasAgreed}", _labelStyle);
            GUILayout.Label($"  状态: {(hasAgreed ? "已解锁 ✓" : "未解锁 ✗")}", _labelStyle);
            GUILayout.Space(6);

            if (!hasAgreed)
            {
                if (GUILayout.Button("🔓 直接解锁内置菜单", GUILayout.Height(34)))
                {
                    GameHelpers.SetStaticField(typeof(MenuWarning), "hasAgreed", true);
                }
            }
            else
            {
                GUILayout.Label("  内置菜单已解锁!", _toggleStyle);
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // ModeMenuButton 控制
            GUILayout.Label("浮动按钮控制", _headerStyle);
            GUILayout.Space(4);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(6);

            var menuBtn = FindFirstObject<ModeMenuButton>();
            if (menuBtn == null)
            {
                GUILayout.Label("  ⚠ ModeMenuButton 未在当前场景中。", _labelStyle);
                GUILayout.Label("  内置菜单的 UI 仅在特定场景/流程中存在。", _labelStyle);
                GUILayout.Label("  解锁 hasAgreed 后,到达对应流程即可直接使用。", _labelStyle);
            }
            else
            {
                GUILayout.Label($"  ModeMenuButton 已找到: {menuBtn.gameObject.name}", _labelStyle);
                var go = menuBtn.gameObject;
                GUILayout.Label($"  GameObject 激活: {go.activeSelf}", _labelStyle);

                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("显示/隐藏按钮", GUILayout.Height(28)))
                {
                    go.SetActive(!go.activeSelf);
                }
                if (GUILayout.Button("切换面板", GUILayout.Height(28)))
                {
                    // 调用私有方法 TogglePanel
                    try
                    {
                        var method = menuBtn.GetType().GetMethod("TogglePanel",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        method?.Invoke(menuBtn, null);
                    }
                    catch { }
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("强制激活菜单对象", GUILayout.Height(28)))
                {
                    go.SetActive(true);
                    // 同时激活父级面板
                    var parent = go.transform.parent;
                    while (parent != null)
                    {
                        parent.gameObject.SetActive(true);
                        parent = parent.parent;
                    }
                }
            }
            GUILayout.Space(6);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // MenuTransform 控制
            GUILayout.Label("NPC 变换菜单", _headerStyle);
            var menuTrans = FindFirstObject<MenuTransform>();
            if (menuTrans != null)
            {
                GUILayout.Label($"  MenuTransform 已找到", _labelStyle);
            }
            else
            {
                GUILayout.Label("  MenuTransform 未加载", _labelStyle);
            }
        }

        // ==================== 游戏选项卡 ====================
        private void DrawGameTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("游戏控制", _headerStyle);
            GUILayout.Space(6);

            var gm = GameHelpers.GameManager;
            if (gm == null)
            {
                GUILayout.Label("⚠ GameManager 未加载。", _labelStyle);
                if (GUILayout.Button("刷新引用", GUILayout.Height(28))) GameHelpers.ClearCache();
                return;
            }

            // 游戏状态
            GUILayout.Label("游戏状态", _headerStyle);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label($"  游戏已开始: {gm.isGameStarted}", _labelStyle);
            GUILayout.Label($"  当前平台: {gm.currentPlatform}", _labelStyle);
            GUILayout.Label($"  当前结局: {gm.currentEndingName}", _labelStyle);
            GUILayout.Label($"  灵敏度: {gm.Sensitivity:F2}", _labelStyle);
            GUILayout.Label($"  移动平台输入: {gm.isMobileIput}", _labelStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 灵敏度调整
            GUILayout.Space(8);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label($"鼠标灵敏度: {gm.Sensitivity:F2}", _labelStyle);
            var newSens = GUILayout.HorizontalSlider(gm.Sensitivity, 0.1f, 10f);
            if (Math.Abs(newSens - gm.Sensitivity) > 0.001f)
                gm.Sensitivity = newSens;
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 时间缩放
            GUILayout.Space(8);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label($"时间缩放: {_timeScale:F2}x", _labelStyle);
            _timeScale = GUILayout.HorizontalSlider(_timeScale, 0.1f, 3f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("正常 1x")) _timeScale = 1f;
            if (GUILayout.Button("慢动作 0.3x")) _timeScale = 0.3f;
            if (GUILayout.Button("加速 2x")) _timeScale = 2f;
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
            GUILayout.EndVertical();

            // 难度信息
            GUILayout.Space(8);
            GUILayout.Label("难度信息", _headerStyle);
            var diff = GameHelpers.Difficulty;
            if (diff != null)
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                var diffVal = GameHelpers.GetField<object>(diff, "difficulty");
                GUILayout.Label($"  当前难度: {diffVal}", _labelStyle);
                try
                {
                    var npcSpeedProp = diff.GetType().GetProperty("CurrentNpcMaxSpeed");
                    if (npcSpeedProp != null)
                    {
                        var speed = npcSpeedProp.GetValue(diff, null);
                        GUILayout.Label($"  NPC 最大速度: {speed}", _labelStyle);
                    }
                }
                catch { }
                GUILayout.Space(4);
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.Label("  难度系统未加载", _labelStyle);
            }

            // 玩家状态
            GUILayout.Space(8);
            GUILayout.Label("玩家状态", _headerStyle);
            var ps = GameHelpers.PlayerState;
            if (ps != null)
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                GUILayout.Label($"  在家: {ps.isPlayerAtHome}", _labelStyle);
                GUILayout.Label($"  逃出(门): {ps.isEscapedDoor}", _labelStyle);
                GUILayout.Label($"  逃出(大门): {ps.isEscapedGate}", _labelStyle);
                GUILayout.Space(4);
                GUILayout.EndVertical();
            }
        }

        // ==================== 变身(克隆模型操控)选项卡 ====================
        private void DrawPossessionTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("变身 / 操控 NPC 模型", _headerStyle);
            GUILayout.Space(6);

            if (_possession == null)
            {
                GUILayout.Label("⚠ 变身控制器未初始化。", _labelStyle);
                return;
            }

            // 状态显示
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(6);
            bool isPossessing = _possession.IsPossessing;
            GUILayout.Label($"  当前状态: {(isPossessing ? "已变身" : "未变身")}", _labelStyle);
            if (isPossessing)
            {
                var clone = _possession.CurrentClone;
                if (clone != null)
                    GUILayout.Label($"  变身模型: {clone.name} ({_possession.CurrentAiName})", _labelStyle);
                GUILayout.Label($"  移动速度: {_possessSpeed:F2}", _labelStyle);
            }
            GUILayout.Space(6);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            if (!isPossessing)
            {
                // 选择 AI 类型
                GUILayout.Label("选择目标 NPC / 敌人", _headerStyle);
                GUILayout.Space(4);
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                _selectedAiIndex = GUILayout.SelectionGrid(_selectedAiIndex, AiDisplayNames, 1, _toggleStyle);
                GUILayout.Space(4);
                GUILayout.EndVertical();

                GUILayout.Space(6);
                GUILayout.Label($"移动速度: {_possessSpeed:F2}", _labelStyle);
                _possessSpeed = GUILayout.HorizontalSlider(_possessSpeed, 0.5f, 10f);

                GUILayout.Space(8);
                if (GUILayout.Button("🔮 变身为选中模型", GUILayout.Height(36)))
                {
                    MonoBehaviour aiTarget = ResolveAiTarget(_selectedAiIndex);
                    if (aiTarget != null)
                    {
                        _possession.SetMoveSpeed(_possessSpeed);
                        // 变身前禁用所有AI_Vision,防止AI检测到玩家或克隆体
                        NotifyPossessionStart();
                        if (_possession.Possess(aiTarget))
                            Logger.LogInfo($"已变身为 {aiTarget.gameObject.name} 模型");
                        else
                        {
                            // 变身失败,恢复AI_Vision
                            NotifyPossessionEnd();
                            Logger.LogWarning($"变身失败: {aiTarget.gameObject.name}");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("未找到目标 AI,可能未在当前场景。");
                    }
                }

                GUILayout.Space(6);
                GUILayout.Label("说明: 会克隆目标 NPC 的模型,玩家直接操控", _labelStyle);
                GUILayout.Label("该克隆体(第一人称移动)。原 AI 行为不受影响。", _labelStyle);
                GUILayout.Label("攻击/交互按钮通过反射调用该 NPC 类型的", _labelStyle);
                GUILayout.Label("原生方法,保留其全部攻击/交互逻辑。", _labelStyle);
            }
            else
            {
                // 操作说明
                GUILayout.Label("操作说明", _headerStyle);
                GUILayout.Space(4);
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                GUILayout.Label("  WASD: 移动变身模型(带碰撞)", _labelStyle);
                GUILayout.Label("  Shift: 加速跑动", _labelStyle);
                GUILayout.Label("  鼠标右键拖动: 旋转视角", _labelStyle);
                GUILayout.Label("  V: 切换第一/第三人称", _labelStyle);
                GUILayout.Label("  下方按钮: 触发该 NPC 的攻击/交互", _labelStyle);
                GUILayout.Space(4);
                GUILayout.EndVertical();

                GUILayout.Space(6);
                GUILayout.Label($"移动速度: {_possessSpeed:F2}", _labelStyle);
                _possessSpeed = GUILayout.HorizontalSlider(_possessSpeed, 0.5f, 10f);
                _possession.SetMoveSpeed(_possessSpeed);

                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("⚔ 攻击", GUILayout.Height(32)))
                    _possession.TriggerAttack();
                if (GUILayout.Button("✋ 交互", GUILayout.Height(32)))
                    _possession.TriggerInteract();
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                if (GUILayout.Button($"🔄 切换视角 ({(_possession.IsFirstPerson ? "第一人称" : "第三人称")})", GUILayout.Height(30)))
                {
                    _possession.ToggleViewMode();
                }

                GUILayout.Space(6);
                if (GUILayout.Button("⏹ 退出变身(恢复玩家)", GUILayout.Height(34)))
                {
                    _possession.ExitPossession();
                    // 变身结束后恢复AI_Vision(除非上帝模式仍开启)
                    NotifyPossessionEnd();
                }
            }
        }

        private MonoBehaviour ResolveAiTarget(int index)
        {
            var gm = GameHelpers.GameManager;
            string fieldName = AiFieldNames[index];
            string typeName = AiTypeNames[index];

            MonoBehaviour target = null;
            if (gm != null)
                target = GameHelpers.GetField<MonoBehaviour>(gm, fieldName);
            if (target == null)
                target = GameHelpers.FindByType(typeName) as MonoBehaviour;
            return target;
        }

        // ==================== 对象选择器选项卡 ====================
        private void DrawObjectSelectorTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("对象选择器", _headerStyle);
            GUILayout.Space(6);

            // 选择模式开关
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            _selectMode = GUILayout.Toggle(_selectMode, " 选择模式 (关闭菜单后,鼠标准星对准对象左键点击)", _toggleStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(8);

            if (_selectedObj == null)
            {
                GUILayout.Label("未选中任何对象。", _labelStyle);
                GUILayout.Label("开启选择模式,关闭菜单(F1),然后用准星对准目标左键。", _labelStyle);
                GUILayout.Space(6);

                // 按名称搜索(备用)
                GUILayout.Label("按名称搜索(备用)", _headerStyle);
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                _objSearchName = GUILayout.TextField(_objSearchName, GUILayout.Width(240));
                if (GUILayout.Button("查找", GUILayout.Height(22)))
                {
                    if (!string.IsNullOrEmpty(_objSearchName))
                    {
                        var found = GameObject.Find(_objSearchName);
                        if (found != null)
                        {
                            SelectObject(found);
                        }
                        else
                        {
                            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                            {
                                if (go == null) continue;
                                if (!go.scene.isLoaded) continue;
                                if (go.hideFlags != HideFlags.None) continue;
                                if (go.name.IndexOf(_objSearchName, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    SelectObject(go);
                                    break;
                                }
                            }
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                GUILayout.Label($"  选中: {_selectedObj.name}", _labelStyle);
                GUILayout.Label($"  路径: {GetPath(_selectedObj.transform)}", _labelStyle);
                GUILayout.Label($"  激活: {_selectedObj.activeSelf}", _labelStyle);
                GUILayout.Space(4);
                GUILayout.EndVertical();

                GUILayout.Space(6);
                if (GUILayout.Button("取消选择", GUILayout.Height(26)))
                {
                    _selectedObj = null;
                    return;
                }

                GUILayout.Space(8);

                // 输入模式切换工具栏
                GUILayout.BeginHorizontal();
                GUILayout.Label("输入方式:", _labelStyle, GUILayout.Width(60));
                if (GUILayout.Toggle(_objUseSlider, "滑块", _toggleStyle, GUILayout.Width(50)))
                    _objUseSlider = true;
                if (GUILayout.Toggle(!_objUseSlider, "手动输入", _toggleStyle, GUILayout.Width(70)))
                    _objUseSlider = false;
                GUILayout.EndHorizontal();

                // 滑块模式下显示步长控制
                if (_objUseSlider)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("步长:", _labelStyle, GUILayout.Width(40));
                    GUILayout.Label($"位置±{_objPosStep:F1}", _labelStyle, GUILayout.Width(80));
                    _objPosStep = GUILayout.HorizontalSlider(_objPosStep, 0.1f, 10f);
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(6);

                // 位置控制
                GUILayout.Label("位置 (X / Y / Z)", _headerStyle);
                GUILayout.Space(4);
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                var pos = _selectedObj.transform.position;
                if (Mathf.Abs(_objPosX - pos.x) > 100f) _objPosX = pos.x;
                if (Mathf.Abs(_objPosY - pos.y) > 100f) _objPosY = pos.y;
                if (Mathf.Abs(_objPosZ - pos.z) > 100f) _objPosZ = pos.z;
                _objPosX = FloatControl("X", _objPosX, _objPosStep, ref _objPosXText);
                _objPosY = FloatControl("Y", _objPosY, _objPosStep, ref _objPosYText);
                _objPosZ = FloatControl("Z", _objPosZ, _objPosStep, ref _objPosZText);
                // 实时应用位置
                _selectedObj.transform.position = new Vector3(_objPosX, _objPosY, _objPosZ);
                GUILayout.Space(4);
                GUILayout.EndVertical();

                // 旋转控制
                GUILayout.Space(6);
                GUILayout.Label("旋转 (X / Y / Z)", _headerStyle);
                GUILayout.Space(4);
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                var rot = _selectedObj.transform.eulerAngles;
                if (Mathf.Abs(_objRotX - rot.x) > 180f) _objRotX = rot.x;
                if (Mathf.Abs(_objRotY - rot.y) > 180f) _objRotY = rot.y;
                if (Mathf.Abs(_objRotZ - rot.z) > 180f) _objRotZ = rot.z;
                _objRotX = FloatControl("RX", _objRotX, _objRotStep, ref _objRotXText);
                _objRotY = FloatControl("RY", _objRotY, _objRotStep, ref _objRotYText);
                _objRotZ = FloatControl("RZ", _objRotZ, _objRotStep, ref _objRotZText);
                // 实时应用旋转
                _selectedObj.transform.eulerAngles = new Vector3(_objRotX, _objRotY, _objRotZ);
                GUILayout.Space(4);
                GUILayout.EndVertical();

                // 缩放控制
                GUILayout.Space(6);
                GUILayout.Label("缩放", _headerStyle);
                GUILayout.Space(4);
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                _useUniformScale = GUILayout.Toggle(_useUniformScale, " 统一缩放", _toggleStyle);
                if (_useUniformScale)
                {
                    var scale = _selectedObj.transform.localScale;
                    if (Mathf.Abs(_objScaleUniform - scale.x) > 100f) _objScaleUniform = scale.x;
                    _objScaleUniform = FloatControl("S", _objScaleUniform, _objScaleStep, ref _objScaleUniformText);
                    // 实时应用统一缩放
                    _selectedObj.transform.localScale = Vector3.one * _objScaleUniform;
                }
                else
                {
                    var scale = _selectedObj.transform.localScale;
                    if (Mathf.Abs(_objScaleX - scale.x) > 100f) _objScaleX = scale.x;
                    if (Mathf.Abs(_objScaleY - scale.y) > 100f) _objScaleY = scale.y;
                    if (Mathf.Abs(_objScaleZ - scale.z) > 100f) _objScaleZ = scale.z;
                    _objScaleX = FloatControl("SX", _objScaleX, _objScaleStep, ref _objScaleXText);
                    _objScaleY = FloatControl("SY", _objScaleY, _objScaleStep, ref _objScaleYText);
                    _objScaleZ = FloatControl("SZ", _objScaleZ, _objScaleStep, ref _objScaleZText);
                    // 实时应用各轴缩放
                    _selectedObj.transform.localScale = new Vector3(_objScaleX, _objScaleY, _objScaleZ);
                }
                GUILayout.Space(4);
                GUILayout.EndVertical();

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("复位 (位置/旋转/缩放)", GUILayout.Height(26)))
                {
                    _selectedObj.transform.position = Vector3.zero;
                    _selectedObj.transform.rotation = Quaternion.identity;
                    _selectedObj.transform.localScale = Vector3.one;
                }
                if (GUILayout.Button("删除对象", GUILayout.Height(26)))
                {
                    UnityEngine.Object.Destroy(_selectedObj);
                    _selectedObj = null;
                }
                GUILayout.EndHorizontal();
            }
        }

        private float FloatField(string label, float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(28));
            var text = GUILayout.TextField(value.ToString("F2"), GUILayout.Height(22));
            GUILayout.EndHorizontal();
            float result;
            if (float.TryParse(text, out result)) return result;
            return value;
        }

        /// <summary>
        /// 统一的浮点控件:支持滑块模式和手动输入模式,实时返回新值。
        /// 滑块模式:以当前值为中心,±step*10 为范围,旁边显示当前值。
        /// 手动输入模式:TextField 输入,带文本缓存避免光标跳动。
        /// </summary>
        private float FloatControl(string label, float value, float step, ref string textCache)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _labelStyle, GUILayout.Width(28));
            if (_objUseSlider)
            {
                // 滑块范围:以当前值为中心,±step*10
                float min = value - step * 10f;
                float max = value + step * 10f;
                float newVal = GUILayout.HorizontalSlider(value, min, max);
                // 显示当前值
                GUILayout.Label(newVal.ToString("F2"), _labelStyle, GUILayout.Width(55));
                GUILayout.EndHorizontal();
                // 同步文本缓存
                textCache = newVal.ToString("F2");
                return newVal;
            }
            else
            {
                // 手动输入:首次或值被外部改变时同步文本
                if (string.IsNullOrEmpty(textCache))
                    textCache = value.ToString("F2");
                var newText = GUILayout.TextField(textCache, GUILayout.Height(22));
                GUILayout.EndHorizontal();
                if (newText != textCache)
                {
                    textCache = newText;
                    float f;
                    if (float.TryParse(newText, out f)) return f;
                }
                return value;
            }
        }

        private void SelectObject(GameObject obj)
        {
            _selectedObj = obj;
            if (obj != null)
            {
                var pos = obj.transform.position;
                _objPosX = pos.x; _objPosY = pos.y; _objPosZ = pos.z;
                var rot = obj.transform.eulerAngles;
                _objRotX = rot.x; _objRotY = rot.y; _objRotZ = rot.z;
                var scale = obj.transform.localScale;
                _objScaleX = scale.x; _objScaleY = scale.y; _objScaleZ = scale.z;
                _objScaleUniform = scale.x;
                // 重置文本缓存,使其在下次渲染时重新同步
                _objPosXText = _objPosYText = _objPosZText = null;
                _objRotXText = _objRotYText = _objRotZText = null;
                _objScaleXText = _objScaleYText = _objScaleZText = null;
                _objScaleUniformText = null;
            }
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "";
            var sb = new System.Text.StringBuilder(t.name);
            var parent = t.parent;
            while (parent != null)
            {
                sb.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return sb.ToString();
        }

        // ==================== 结局触发选项卡 ====================
        private void DrawEndingTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("结局触发", _headerStyle);
            GUILayout.Space(6);

            var gm = GameHelpers.GameManager;
            if (gm == null)
            {
                GUILayout.Label("⚠ GameManager 未加载,请进入游戏后刷新。", _labelStyle);
                if (GUILayout.Button("刷新引用", GUILayout.Height(28))) GameHelpers.ClearCache();
                return;
            }

            // 警告
            var warnStyle = new GUIStyle(_labelStyle);
            warnStyle.normal.textColor = new Color(1f, 0.4f, 0.3f);
            warnStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label("⚠ 直接触发结局会立即结束当前游戏流程!", warnStyle);
            GUILayout.Space(6);

            // 当前结局
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label($"  当前结局: {gm.currentEndingName ?? "(无)"}", _labelStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.Label("选择结局", _headerStyle);
            GUILayout.Space(4);
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            _selectedEndingIndex = GUILayout.SelectionGrid(_selectedEndingIndex, EndingNames, 1, _toggleStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            if (GUILayout.Button("⚡ 立即触发选中结局", GUILayout.Height(36)))
            {
                TriggerEnding(_selectedEndingIndex);
            }

            GUILayout.Space(10);
            GUILayout.Label("说明:", _headerStyle);
            GUILayout.Space(2);
            GUILayout.Label("  Door 系列: 通过门逃出(StartDoorEnding)", _labelStyle);
            GUILayout.Label("  Gate Ending: 从大门逃出(StartGateEnding)", _labelStyle);
            GUILayout.Label("  Roof Ending: 屋顶逃出(StartRoofEnding)", _labelStyle);
            GUILayout.Label("  Belt Ending: 皮带结局(StartBeltEnding)", _labelStyle);
            GUILayout.Label("  Backrooms: 进入后室场景(OpenBackRoomsScene)", _labelStyle);
        }

        private void TriggerEnding(int index)
        {
            var gm = GameHelpers.GameManager;
            if (gm == null) return;

            try
            {
                if (index >= 0 && index < DoorEndingParams.Length)
                {
                    var method = gm.GetType().GetMethod("StartDoorEnding",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(gm, new object[] { DoorEndingParams[index] });
                    Logger.LogInfo($"已触发 DoorEnding: {DoorEndingParams[index]}");
                }
                else if (index == 7)
                {
                    var method = gm.GetType().GetMethod("StartGateEnding",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(gm, null);
                    Logger.LogInfo("已触发 GateEnding");
                }
                else if (index == 8)
                {
                    var method = gm.GetType().GetMethod("StartRoofEnding",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(gm, null);
                    Logger.LogInfo("已触发 RoofEnding");
                }
                else if (index == 9)
                {
                    var method = gm.GetType().GetMethod("StartBeltEnding",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(gm, null);
                    Logger.LogInfo("已触发 BeltEnding");
                }
                else if (index == 10)
                {
                    var method = gm.GetType().GetMethod("OpenBackRoomsScene",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(gm, null);
                    Logger.LogInfo("已触发进入后室场景");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"触发结局失败: {ex.Message}");
            }
        }

        // ==================== 传送至房间选项卡 ====================
        private void DrawTeleportTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("传送至房间 / 路径点", _headerStyle);
            GUILayout.Space(6);

            var ps = GameHelpers.PlayerState;
            if (ps == null)
            {
                GUILayout.Label("⚠ PlayerState 未加载,请进入游戏后刷新。", _labelStyle);
                if (GUILayout.Button("刷新引用", GUILayout.Height(28))) GameHelpers.ClearCache();
                return;
            }

            // 刷新路径点列表
            if (_roomNames == null || _roomNames.Length == 0)
                RefreshWaypoints();

            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            GUILayout.Label($"  在家: {ps.isPlayerAtHome}", _labelStyle);
            GUILayout.Label($"  逃出(门): {ps.isEscapedDoor}", _labelStyle);
            GUILayout.Label($"  逃出(大门): {ps.isEscapedGate}", _labelStyle);
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.Label("选择路径点", _headerStyle);
            GUILayout.Space(4);

            if (_roomNames.Length == 0)
            {
                GUILayout.Label("  ⚠ 未找到路径点。请确保已进入游戏主场景。", _labelStyle);
                if (GUILayout.Button("刷新路径点", GUILayout.Height(26)))
                    RefreshWaypoints();
            }
            else
            {
                GUILayout.BeginVertical(_boxStyle);
                GUILayout.Space(4);
                _selectedRoomIndex = GUILayout.SelectionGrid(_selectedRoomIndex, _roomNames, 1, _toggleStyle);
                GUILayout.Space(4);
                GUILayout.EndVertical();

                GUILayout.Space(6);
                if (GUILayout.Button("🚀 传送至选中路径点", GUILayout.Height(34)))
                {
                    TeleportToRoom(_selectedRoomIndex);
                }
            }

            GUILayout.Space(8);
            GUILayout.Label("快捷操作", _headerStyle);
            GUILayout.Space(4);
            if (GUILayout.Button("🔄 刷新路径点列表", GUILayout.Height(28))) RefreshWaypoints();

            GUILayout.Space(10);
            GUILayout.Label("说明: 传送使用 PlayerState.wayPoints 数组。", _labelStyle);
            GUILayout.Label("部分路径点可能仅在特定场景有效。", _labelStyle);
        }

        private void RefreshWaypoints()
        {
            var ps = GameHelpers.PlayerState;
            if (ps == null) return;

            try
            {
                _cachedWaypoints = GameHelpers.GetField<WayPointPlayer[]>(ps, "wayPoints");
                if (_cachedWaypoints != null && _cachedWaypoints.Length > 0)
                {
                    _roomNames = new string[_cachedWaypoints.Length];
                    for (int i = 0; i < _cachedWaypoints.Length; i++)
                    {
                        var wp = _cachedWaypoints[i];
                        _roomNames[i] = (wp != null && !string.IsNullOrEmpty(wp.nameWaypoint))
                            ? $"{i}: {wp.nameWaypoint}"
                            : $"#{i}";
                    }
                    if (_selectedRoomIndex >= _roomNames.Length) _selectedRoomIndex = 0;
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"刷新路径点失败: {ex.Message}");
            }

            _roomNames = new string[0];
            _cachedWaypoints = null;
        }

        private void TeleportToRoom(int index)
        {
            if (_cachedWaypoints == null || index < 0 || index >= _cachedWaypoints.Length) return;
            var wp = _cachedWaypoints[index];
            if (wp == null) return;

            try
            {
                var trans = wp.wayPointTransform;
                if (trans != null)
                {
                    TeleportToTransform(trans);
                    Logger.LogInfo($"已传送至 {wp.nameWaypoint}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"传送失败: {ex.Message}");
            }
        }

        private void TeleportToTransform(Transform target)
        {
            if (target == null) return;
            TeleportToPosition(target.position);

            // 尝试调用 PlayerState.TeleportFpControllerTo
            try
            {
                var ps = GameHelpers.PlayerState;
                if (ps != null)
                {
                    var method = ps.GetType().GetMethod("TeleportFpControllerTo",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    method?.Invoke(ps, new object[] { target });
                }
            }
            catch { }
        }

        private void TeleportToPosition(Vector3 pos)
        {
            var ctrl = GameHelpers.FP_Control;
            if (ctrl != null)
            {
                ctrl.transform.position = pos;
            }

            var body = GameHelpers.FP_Body;
            if (body != null)
            {
                body.transform.position = pos;
            }
        }

        // ==================== 生成物品选项卡 ====================
        private void DrawSpawnItemsTab()
        {
            GUILayout.Space(4);
            GUILayout.Label("生成物品", _headerStyle);
            GUILayout.Space(6);

            // 刷新物品列表
            if (_allItemPrefabs == null || _allItemPrefabs.Count == 0)
                RefreshAllItems();

            if (_allItemPrefabs == null || _allItemPrefabs.Count == 0)
            {
                GUILayout.Label("⚠ 当前场景未找到可生成的物品。", _labelStyle);
                GUILayout.Label("  请进入游戏主场景后刷新。", _labelStyle);
                if (GUILayout.Button("刷新物品列表", GUILayout.Height(28))) RefreshAllItems();
                return;
            }

            GUILayout.Label($"点击物品名称即可在准星处生成 ({_allItemPrefabs.Count} 个)", _labelStyle);
            GUILayout.Space(6);

            // 物品按钮列表
            GUILayout.BeginVertical(_boxStyle);
            GUILayout.Space(4);
            for (int i = 0; i < _allItemPrefabs.Count; i++)
            {
                var prefab = _allItemPrefabs[i];
                if (prefab == null) continue;
                if (GUILayout.Button($"🎁 {prefab.name}", GUILayout.Height(26)))
                {
                    SpawnItemAtCrosshair(i);
                }
            }
            GUILayout.Space(4);
            GUILayout.EndVertical();

            GUILayout.Space(8);
            if (GUILayout.Button("🔄 刷新物品列表", GUILayout.Height(28))) RefreshAllItems();

            GUILayout.Space(10);
            GUILayout.Label("说明: 点击物品名称会在玩家准星方向生成,", _labelStyle);
            GUILayout.Label("物品带物理组件会自然下落坠地。", _labelStyle);
        }

        /// <summary>收集场景中所有 SpawnItems 的所有物品 prefab(去重)</summary>
        private void RefreshAllItems()
        {
            _allItemPrefabs = new System.Collections.Generic.List<GameObject>();
            _itemSpawnSources = new System.Collections.Generic.List<MonoBehaviour>();
            try
            {
                var list = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                var seen = new System.Collections.Generic.Dictionary<string, (MonoBehaviour, GameObject)>();
                foreach (var mb in list)
                {
                    if (mb == null) continue;
                    if (mb.GetType().Name != "SpawnItems") continue;
                    var items = GameHelpers.GetField<GameObject[]>(mb, "items");
                    if (items == null) continue;
                    foreach (var item in items)
                    {
                        if (item == null) continue;
                        if (!seen.ContainsKey(item.name))
                            seen[item.name] = (mb, item);
                    }
                }
                foreach (var kv in seen)
                {
                    _allItemPrefabs.Add(kv.Value.Item2);
                    _itemSpawnSources.Add(kv.Value.Item1);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"刷新物品列表失败: {ex.Message}");
            }
        }

        /// <summary>在玩家准星处生成物品,使用原生 SetUpItem 逻辑确保物理正确</summary>
        private void SpawnItemAtCrosshair(int index)
        {
            if (_allItemPrefabs == null || index < 0 || index >= _allItemPrefabs.Count) return;
            var prefab = _allItemPrefabs[index];
            var source = _itemSpawnSources[index];
            if (prefab == null || source == null) return;
            try
            {
                var cam = Camera.main;
                Vector3 spawnPos;
                Quaternion spawnRot;

                if (cam != null)
                {
                    var ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
                    if (Physics.Raycast(ray, out var hit, 50f))
                    {
                        spawnPos = hit.point + Vector3.up * 0.5f;
                    }
                    else
                    {
                        spawnPos = cam.transform.position + cam.transform.forward * 2f;
                    }
                    spawnRot = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
                }
                else
                {
                    spawnPos = Vector3.zero;
                    spawnRot = Quaternion.identity;
                }

                var obj = UnityEngine.Object.Instantiate(prefab, spawnPos, spawnRot);
                obj.name = prefab.name + "_Spawned";
                obj.SetActive(true);

                // 调用原生 SetUpItem(设置 layer + Rigidbody)
                var setUpMethod = source.GetType().GetMethod("SetUpItem",
                    BindingFlags.Public | BindingFlags.Instance);
                setUpMethod?.Invoke(source, new object[] { obj });

                // 额外确保 Rigidbody 启用重力
                var rb = obj.GetComponent<Rigidbody>();
                if (rb == null) rb = obj.GetComponentInChildren<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.velocity = Vector3.zero;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }

                // 确保 Collider 启用
                foreach (var col in obj.GetComponentsInChildren<Collider>())
                    col.enabled = true;
                if (obj.GetComponent<Collider>() == null && obj.GetComponentInChildren<Collider>() == null)
                    obj.AddComponent<BoxCollider>();

                Logger.LogInfo($"已在准星处生成 {prefab.name} @ {spawnPos}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"生成物品失败: {ex.Message}");
            }
        }

        // ==================== 关于选项卡 ====================
        private void DrawAboutTab()
        {
            GUILayout.Space(8);
            GUILayout.Label(PluginName, _headerStyle);
            GUILayout.Space(4);
            GUILayout.Label($"版本: {PluginVersion}", _labelStyle);
            GUILayout.Label($"GUID: {PluginGuid}", _labelStyle);
            GUILayout.Label($"BepInEx: 5.4.15", _labelStyle);

            GUILayout.Space(12);
            GUILayout.Label("功能说明", _headerStyle);
            GUILayout.Space(4);
            GUILayout.Label("• 玩家: 上帝模式/无限呼吸/穿墙/速度倍率/跳跃倍率", _labelStyle);
            GUILayout.Label("• AI: 冻结/解冻/致盲 各类敌人(Dad/Mom/Fishman等)", _labelStyle);
            GUILayout.Label("• 变身: 克隆NPC模型供玩家操控(保留攻击/交互)", _labelStyle);
            GUILayout.Label("• 对象: 选择场景任意对象控制xyz/缩放", _labelStyle);
            GUILayout.Label("• 内置菜单: 直接解锁游戏内置开发者菜单", _labelStyle);
            GUILayout.Label("• 结局: 直接触发任意结局", _labelStyle);
            GUILayout.Label("• 传送: 传送至选中房间/路径点", _labelStyle);
            GUILayout.Label("• 物品: 生成场景中的物品", _labelStyle);
            GUILayout.Label("• 游戏: 灵敏度/时间缩放/难度/状态查看", _labelStyle);

            GUILayout.Space(12);
            GUILayout.Label("快捷键", _headerStyle);
            GUILayout.Space(4);
            GUILayout.Label($"  显示/隐藏菜单: {_toggleKey.Value}", _labelStyle);
            GUILayout.Label("  拖动标题栏可移动窗口", _labelStyle);
            GUILayout.Label("  点击 ↻ 刷新游戏对象引用", _labelStyle);

            GUILayout.Space(12);
            GUILayout.Label("技术信息", _headerStyle);
            GUILayout.Space(4);
            GUILayout.Label($"  FPS: {1f / Time.unscaledDeltaTime:F0}", _labelStyle);
            GUILayout.Label($"  场景: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}", _labelStyle);
            GUILayout.Label($"  时间缩放: {Time.timeScale:F2}", _labelStyle);
        }

        // ==================== 工具方法 ====================
        private static T FindFirstObject<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectOfType<T>();
        }

        private static Texture2D MakeTex(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            var tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
