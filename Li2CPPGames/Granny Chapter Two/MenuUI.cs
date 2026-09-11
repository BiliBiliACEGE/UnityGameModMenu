using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace GrannyModMenu;

public sealed class MenuBehaviour : MonoBehaviour
{
    public MenuBehaviour(IntPtr pointer) : base(pointer) { }

    private const int WinW = 640;
    private const int WinH = 508;
    private const int ItemsPerPage = 10;

    private static readonly string[] Tabs = { "玩家", "物品", "敌人", "世界", "其它", "关于" };

    // 主题色
    private static readonly Color32 WinFill = new(23, 23, 31, 255);
    private static readonly Color32 BorderCol = new(58, 58, 76, 255);
    private static readonly Color32 Accent = new(91, 141, 239, 255);
    private static readonly Color32 BtnFill = new(40, 40, 54, 255);
    private static readonly Color32 BtnHover = new(53, 53, 71, 255);
    private static readonly Color32 TextCol = new(222, 226, 240, 255);
    private static readonly Color32 DimCol = new(146, 152, 170, 255);
    private static readonly Color32 OnTextCol = new(18, 20, 30, 255);
    private static readonly Color32 TrackCol = new(15, 15, 23, 255);
    private static readonly Color32 HudBg = new(15, 15, 23, 230);

    private static bool _open;
    private static bool _placed;
    private static Rect _win = new(0f, 0f, WinW, WinH);
    private static int _tab;
    private static int _itemPage;
    private static bool _dragging;
    private static bool _dragWin;
    private static Vector2 _dragOff;
    private static bool _stylesBuilt;
    private static Font _font;

    private static GUIStyle _bgStyle, _title, _fpsLabel, _btnText, _btnTextOn, _rowLabel, _section, _status, _hint, _about, _hudStyle;
    private const float ThumbW = 12f;

    private static bool _godMode, _freeze, _blind, _mute, _showFps = true, _showDist, _infAmmo;
    private static CursorLockMode _prevLock = CursorLockMode.Locked;
    private static bool _prevVisible;
    private static Game.EnemyInfo[] _enemyCache;
    private static float _walk, _run, _jump, _grav, _sens, _fov = 70f;
    private static float _grannySpd = 3f, _grandpaSpd = 3f;
    private static float _day = 1f, _timeScale = 1f;
    private static string _statusText = "就绪。";
    private static float _statusUntil;
    private static int _frames, _fps;
    private static float _fpsTimer;
    private static float _lastErrLog;

    private void Update()
    {
        try
        {
            if (Input.GetKeyDown(KeyCode.F1)) Toggle();
            Game.TickEffects();
            _enemyCache = _showDist ? Game.GetEnemyInfos() : null;
        }
        catch (Exception ex)
        {
            _enemyCache = null;
            LogErrorLimited("update", ex);
        }
        CountFps();
    }

    private void LateUpdate()
    {
        if (!_open) return;
        try
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        catch { }
    }

    private static void CountFps()
    {
        _frames++;
        _fpsTimer += Time.unscaledDeltaTime;
        if (_fpsTimer < 0.5f) return;
        _fps = Mathf.RoundToInt(_frames / _fpsTimer);
        _frames = 0;
        _fpsTimer = 0f;
    }

    private void OnGUI()
    {
        try
        {
            BuildStyles();
            if (_open) DrawOverlay();
            DrawHud();
        }
        catch (Exception ex)
        {
            LogErrorLimited("gui", ex);
        }
    }

    private void Toggle()
    {
        _open = !_open;
        if (_open)
        {
            try
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
            }
            catch { }
            Game.SnapshotDefaults();
            SyncFromGame();
            Status("菜单已打开。");
        }
        else
        {
            try
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
            catch { }
        }
    }

    private static void SyncFromGame()
    {
        _walk = Game.GetWalkSpeed();
        _run = Game.GetRunSpeed();
        _jump = Game.GetJumpSpeed();
        _grav = Game.GetGravity();
        _sens = Game.GetMouseSens();
        _fov = Game.GetFov();
        _grannySpd = Game.GetGrannySpeed();
        _grandpaSpd = Game.GetGrandpaSpeed();
        _day = Game.GetDay();
        _timeScale = Time.timeScale;
    }

    private static void Status(string msg)
    {
        _statusText = msg;
        _statusUntil = Time.realtimeSinceStartup + 8f;
    }

    private static string NotFound() => "请先开始游戏。";

    private static void Warn() => Status(NotFound());

    // 背景绘制：Texture2D.whiteTexture(引擎内置) + GUI.backgroundColor 染色
    private static void DrawBox(Rect r, Color32 color)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = color;
        GUI.Box(r, "", _bgStyle);
        GUI.backgroundColor = prev;
    }

    private static void DrawOverlay()
    {
        if (!_placed)
        {
            var px = (Screen.width - WinW) * 0.5f;
            var py = (Screen.height - WinH) * 0.28f;
            if (px < 10f) px = 10f;
            if (py < 10f) py = 10f;
            _win = new Rect(px, py, WinW, WinH);
            _placed = true;
        }

        var x = _win.x;
        var y = _win.y;
        var w = _win.width;
        var h = _win.height;

        HandleWindowDrag(x, y, w);

        DrawBox(new Rect(x, y, w, h), WinFill);
        DrawBox(new Rect(x, y, w, 1f), BorderCol);
        DrawBox(new Rect(x, y + h - 1f, w, 1f), BorderCol);
        DrawBox(new Rect(x, y, 1f, h), BorderCol);
        DrawBox(new Rect(x + w - 1f, y, 1f, h), BorderCol);
        DrawBox(new Rect(x, y + 33f, w, 2f), Accent);

        GUI.Label(new Rect(x + 16f, y, 430f, 34f), "GRANNY: CHAPTER TWO  <color=#5B8DEF>//</color>  修改菜单", _title);
        if (_showFps) GUI.Label(new Rect(x + w - 112f, y, 98f, 34f), _fps + " FPS", _fpsLabel);

        for (var i = 0; i < Tabs.Length; i++)
        {
            var tr = new Rect(x + 10f, y + 47f + i * 40f, 122f, 34f);
            if (MyButton(tr, Tabs[i], _tab == i)) _tab = i;
        }

        var area = new Rect(x + 148f, y + 51f, w - 162f, h - 51f - 34f);
        switch (_tab)
        {
            case 0: DrawPlayer(area); break;
            case 1: DrawItems(area); break;
            case 2: DrawEnemies(area); break;
            case 3: DrawWorld(area); break;
            case 4: DrawMisc(area); break;
            default: DrawAbout(area); break;
        }

        var msg = Time.realtimeSinceStartup > _statusUntil ? "F1 — 打开 / 关闭菜单" : _statusText;
        GUI.Label(new Rect(x + 12f, y + h - 26f, w - 24f, 20f), msg, _status);
    }

    private static void DrawHud()
    {
        if (!_showDist || _enemyCache == null || _enemyCache.Length == 0) return;
        var cam = Game.GetMainCamera();
        if (cam == null) return;
        foreach (var info in _enemyCache)
        {
            var vp = Game.WorldToViewport(cam, info.Pos + Vector3.up * 1.8f);
            if (vp.z <= 0f) continue;
            if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;
            var sx = vp.x * Screen.width;
            var sy = (1f - vp.y) * Screen.height;
            var rect = new Rect(sx - 56f, sy - 10f, 112f, 20f);
            DrawBox(rect, HudBg);
            GUI.Label(rect, info.Name + " " + info.Dist.ToString("F1") + "米", _hudStyle);
        }
    }

    private static void HandleWindowDrag(float x, float y, float w)
    {
        var e = Event.current;
        if (e == null) return;
        if (e.type == EventType.MouseUp)
        {
            _dragWin = false;
            _dragging = false;
            return;
        }
        var header = new Rect(x, y, w, 34f);
        if (e.type == EventType.MouseDown && header.Contains(e.mousePosition))
        {
            _dragWin = true;
            _dragOff = new Vector2(e.mousePosition.x - x, e.mousePosition.y - y);
            e.Use();
        }
        else if (_dragWin && e.type == EventType.MouseDrag)
        {
            _win.position = new Vector2(e.mousePosition.x - _dragOff.x, e.mousePosition.y - _dragOff.y);
            e.Use();
        }
    }

    private static bool MyButton(Rect r, string label, bool on)
    {
        var e = Event.current;
        if (e == null) return false;
        var hover = r.Contains(e.mousePosition);
        var bg = on ? Accent : (hover ? BtnHover : BtnFill);
        DrawBox(r, bg);
        GUI.Label(r, label, on ? _btnTextOn : _btnText);
        if (e.type == EventType.MouseDown && hover)
        {
            e.Use();
            return true;
        }
        return false;
    }

    private static void Section(Rect r, float y, string name)
    {
        GUI.Label(new Rect(r.x, y, r.width, 18f), name, _section);
    }

    private static float SliderLine(Rect r, float y, string label, float val, float min, float max, Action<float> apply, string fmt = "F1")
    {
        GUI.Label(new Rect(r.x, y, r.width, 18f), label + "   <b>" + val.ToString(fmt) + "</b>", _rowLabel);
        var track = new Rect(r.x, y + 23f, r.width, 8f);
        var nv = DoSlider(track, val, min, max);
        if (Math.Abs(nv - val) > 0.0001f) apply?.Invoke(nv);
        return y + 46f;
    }

    private static float DoSlider(Rect track, float val, float min, float max)
    {
        DrawBox(track, TrackCol);
        var t = max > min ? Mathf.Clamp01((val - min) / (max - min)) : 0f;
        var thumbRect = new Rect(track.x + t * (track.width - ThumbW), track.y - 5f, ThumbW, track.height + 10f);
        DrawBox(thumbRect, Accent);

        var e = Event.current;
        if (e == null) return val;
        var m = e.mousePosition;
        var hit = new Rect(track.x - 4f, track.y - 12f, track.width + 8f, track.height + 24f);

        if (e.type == EventType.MouseDown && hit.Contains(m))
        {
            _dragging = true;
            e.Use();
            var f0 = Mathf.Clamp01((m.x - track.x - ThumbW * 0.5f) / (track.width - ThumbW));
            return min + f0 * (max - min);
        }
        if (_dragging && e.type == EventType.MouseDrag && hit.Contains(m))
        {
            var f = Mathf.Clamp01((m.x - track.x - ThumbW * 0.5f) / (track.width - ThumbW));
            e.Use();
            return min + f * (max - min);
        }
        return val;
    }

    private static float ButtonLine(Rect r, float y, string label, bool val, Action<bool> apply)
    {
        var next = MyButton(new Rect(r.x, y, r.width, 28f), (val ? "●  " : "○  ") + label, val) ? !val : val;
        if (next != val) apply?.Invoke(next);
        return y + 34f;
    }

    private static float ActionLine(Rect r, float y, string label, Action action)
    {
        if (MyButton(new Rect(r.x, y, r.width, 28f), label, false)) action?.Invoke();
        return y + 34f;
    }

    private static void DrawPlayer(Rect r)
    {
        var y = r.y;
        Section(r, y, "移动");
        y += 24f;
        y = SliderLine(r, y, "行走速度", _walk, 0f, 20f, v => { _walk = v; if (!Game.SetWalkSpeed(v)) Warn(); });
        y = SliderLine(r, y, "奔跑速度", _run, 0f, 25f, v => { _run = v; if (!Game.SetRunSpeed(v)) Warn(); });
        y = SliderLine(r, y, "跳跃力", _jump, 0f, 15f, v => { _jump = v; if (!Game.SetJumpSpeed(v)) Warn(); });
        y = SliderLine(r, y, "重力倍率", _grav, 0f, 5f, v => { _grav = v; if (!Game.SetGravity(v)) Warn(); });
        y = SliderLine(r, y, "鼠标灵敏度", _sens, 0.1f, 10f, v => { _sens = v; if (!Game.SetMouseSens(v)) Warn(); });
        y = SliderLine(r, y, "视野 (FOV)", _fov, 50f, 120f, v => { _fov = v; if (!Game.SetFov(v)) Warn(); });
        y += 2f;
        Section(r, y, "生存");
        y += 24f;
        y = ButtonLine(r, y, "上帝模式", _godMode, v => { _godMode = v; Game.SetGodMode(v); });
        y = ActionLine(r, y, "释放捕兽夹", () => Status(Game.ReleaseBeartrap() ? "捕兽夹已释放。" : NotFound()));
        ActionLine(r, y, "恢复默认值", () =>
        {
            if (Game.RestoreDefaults()) { SyncFromGame(); Status("已恢复全部默认值。"); }
            else Status(NotFound());
        });
    }

    private static void DrawItems(Rect r)
    {
        var y = r.y;
        GUI.Label(new Rect(r.x, y, r.width, 30f), "点击\"给予\"将物品直接放入你的背包。", _hint);
        y += 34f;

        var pages = (Game.Items.Length + ItemsPerPage - 1) / ItemsPerPage;
        if (_itemPage >= pages) _itemPage = 0;
        if (MyButton(new Rect(r.x, y, 30f, 26f), "<", false)) _itemPage = (_itemPage + pages - 1) % pages;
        GUI.Label(new Rect(r.x + 38f, y + 4f, 90f, 20f), "第 " + (_itemPage + 1) + "/" + pages + " 页", _rowLabel);
        if (MyButton(new Rect(r.x + 128f, y, 30f, 26f), ">", false)) _itemPage = (_itemPage + 1) % pages;
        if (MyButton(new Rect(r.x + 168f, y, r.width - 168f, 26f), "给予全部物品", false)) Status(Game.GiveAllItems());
        y += 34f;

        var start = _itemPage * ItemsPerPage;
        var count = Math.Min(ItemsPerPage, Game.Items.Length - start);
        for (var i = 0; i < count; i++)
        {
            var item = Game.Items[start + i];
            GUI.Label(new Rect(r.x, y + 4f, r.width - 110f, 20f), item.Label, _rowLabel);
            if (MyButton(new Rect(r.xMax - 96f, y, 96f, 24f), "给予", false)) Status(Game.GiveItem(item));
            y += 28f;
        }
    }

    private static void DrawEnemies(Rect r)
    {
        var y = r.y;
        Section(r, y, "速度");
        y += 24f;
        y = SliderLine(r, y, "奶奶速度", _grannySpd, 0f, 12f, v => { _grannySpd = v; if (!Game.SetGrannySpeed(v)) Warn(); });
        y = SliderLine(r, y, "爷爷速度", _grandpaSpd, 0f, 12f, v => { _grandpaSpd = v; if (!Game.SetGrandpaSpeed(v)) Warn(); });
        y += 2f;
        Section(r, y, "行为");
        y += 24f;
        y = ButtonLine(r, y, "冻结所有敌人", _freeze, v => { _freeze = v; Game.SetFreezeEnemies(v); });
        y = ButtonLine(r, y, "敌人无视玩家", _blind, v => { _blind = v; Game.EnemiesBlind = v; });
        y = ButtonLine(r, y, "显示距离标签", _showDist, v => _showDist = v);
        ActionLine(r, y, "传送敌人到身边", () => Status(Game.TeleportEnemiesToPlayer()));
    }

    private static void DrawWorld(Rect r)
    {
        var y = r.y;
        Section(r, y, "进度");
        y += 24f;
        y = SliderLine(r, y, "天数", _day, 1f, 5f, v => { _day = v; if (!Game.SetDay(v)) Warn(); }, "F0");
        y += 2f;
        Section(r, y, "时间");
        y += 24f;
        y = SliderLine(r, y, "时间流速", _timeScale, 0f, 3f, v => { _timeScale = v; Game.SetTimeScale(v); });
        GUI.Label(new Rect(r.x, y, r.width, 18f), "0 = 冻结世界，1 = 正常速度。", _hint);
    }

    private static void DrawMisc(Rect r)
    {
        var y = r.y;
        Section(r, y, "武器");
        y += 24f;
        y = ButtonLine(r, y, "无限弹药（猎枪 / 电击枪）", _infAmmo, v => { _infAmmo = v; Game.InfiniteAmmo = v; });
        y += 2f;
        Section(r, y, "叠加层");
        y += 24f;
        y = ButtonLine(r, y, "显示 FPS 计数", _showFps, v => _showFps = v);
        y = ButtonLine(r, y, "全部静音", _mute, v => { _mute = v; Game.SetMute(v); });
        y += 2f;
        Section(r, y, "系统");
        y += 24f;
        ActionLine(r, y, "退出游戏", () =>
        {
            Status("正在退出……");
            Application.Quit();
        });
    }

    private static void DrawAbout(Rect r)
    {
        GUI.Label(r,
            "GRANNY: CHAPTER TWO — 修改菜单\n" +
            "版本 1.0.0  ·  BepInEx 6 (IL2CPP)  ·  IMGUI 渲染\n\n" +
            "快捷键\n" +
            "     F1 — 打开 / 关闭本菜单\n\n" +
            "说明\n" +
            "· 给予物品走游戏自身的背包系统（与拾取时设置的同一字段）。\n" +
            "· 上帝模式将敌人攻击距离判定归零：敌人看得见你、追得上你，但攻击永远不会发起；同时兜底拦截坠落等死亡路径。\n" +
            "· 冻结会禁用每个敌人的寻路组件；无视模式每帧清除侦测标志。\n\n" +
            "仅供单机娱乐使用。",
            _about);
    }

    private static void BuildStyles()
    {
        if (_stylesBuilt) return;
        try
        {
            BuildStylesInternal();
            _stylesBuilt = true;
            RunDiagnostics();
        }
        catch (Exception ex)
        {
            LogErrorLimited("styles", ex);
        }
    }

    // 一次性诊断：验证渲染链路各环节，输出到 BepInEx 日志
    private static void RunDiagnostics()
    {
        try
        {
            var white = Texture2D.whiteTexture;
            var probe = new GUIStyle();
            probe.normal.background = white;
            var setterOk = probe.normal.background == white;

            Plugin.StaticLog?.LogWarning("[ModMenu:diag] bgSetterOk=" + setterOk + " fontLoaded=" + (_font != null) + (_font != null ? " (" + _font.name + ")" : ""));
        }
        catch (Exception ex)
        {
            Plugin.StaticLog?.LogWarning("[ModMenu:diag] failed: " + ex.Message);
        }
    }

    private static void LogErrorLimited(string tag, Exception ex)
    {
        if (Time.realtimeSinceStartup - _lastErrLog < 2f) return;
        _lastErrLog = Time.realtimeSinceStartup;
        try { Plugin.StaticLog?.LogError("[ModMenu:" + tag + "] " + ex); } catch { }
    }

    // 从系统加载中文字体（动态字体按需光栅化字形，支持中文渲染）
    private static Font LoadFont()
    {
        try
        {
            var names = new[] { "Microsoft YaHei", "微软雅黑", "SimHei", "SimSun" };
            var font = Font.CreateDynamicFontFromOSFont(names, 14);
            if (font == null) Plugin.StaticLog?.LogWarning("[ModMenu:font] 系统字体加载返回空。");
            return font;
        }
        catch (Exception ex)
        {
            Plugin.StaticLog?.LogWarning("[ModMenu:font] " + ex.Message);
            return null;
        }
    }

    private static void BuildStylesInternal()
    {
        _font = LoadFont();

        _bgStyle = new GUIStyle();
        _bgStyle.normal.background = Texture2D.whiteTexture;

        _title = TextStyle(13, TextCol, TextAnchor.MiddleLeft, true);
        _fpsLabel = TextStyle(11, DimCol, TextAnchor.MiddleRight, false);
        _btnText = TextStyle(12, TextCol, TextAnchor.MiddleCenter, false);
        _btnTextOn = TextStyle(12, OnTextCol, TextAnchor.MiddleCenter, false);
        _rowLabel = TextStyle(12, TextCol, TextAnchor.MiddleLeft, true);
        _section = TextStyle(11, Accent, TextAnchor.MiddleLeft, true);
        _status = TextStyle(11, DimCol, TextAnchor.MiddleLeft, false);
        _hint = TextStyle(11, DimCol, TextAnchor.UpperLeft, true);
        _hint.wordWrap = true;
        _about = TextStyle(12, TextCol, TextAnchor.UpperLeft, false);
        _about.wordWrap = true;
        _hudStyle = TextStyle(11, TextCol, TextAnchor.MiddleCenter, false);
    }

    private static GUIStyle TextStyle(int size, Color32 color, TextAnchor align, bool rich)
    {
        var style = new GUIStyle();
        style.fontSize = size;
        style.normal.textColor = color;
        style.alignment = align;
        style.richText = rich;
        if (_font != null) style.font = _font;
        return style;
    }
}
