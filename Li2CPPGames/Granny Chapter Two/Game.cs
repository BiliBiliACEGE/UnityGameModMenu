using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.FirstPerson;

namespace GrannyModMenu;

internal static class Game
{
    internal sealed class ItemDef
    {
        internal ItemDef(string label, string have)
        {
            Label = label;
            Have = have;
        }

        internal string Label { get; }
        internal string Have { get; }
    }

    internal static readonly ItemDef[] Items =
    {
        new("撬棍",     "havecrowbar"),
        new("剪线钳",   "haveAvbitare"),
        new("门锁",     "havedoorlock"),
        new("保险箱钥匙", "havesafekey"),
        new("挂锁钥匙", "havepadlockkey"),
        new("武器室钥匙", "haveweaponKey"),
        new("安保钥匙", "havesecurityKey"),
        new("特殊钥匙", "havespecialKey"),
        new("直升机钥匙", "havehelicopterKey"),
        new("小船钥匙", "haveboatKey"),
        new("齿轮把手", "havehandle"),
        new("火花塞",   "havesparkPlug"),
        new("汽油桶",   "havegasCan"),
        new("备用车轮", "haveburWheel"),
        new("船舵",     "haveboatRatt"),
        new("猎枪",     "haveshotGun"),
        new("电击枪",   "havetaserGun"),
        new("扳手",     "havewrench"),
        new("花瓶",     "havevas"),
        new("肉",       "havemeat"),
        new("胶带",     "haveductTape"),
        new("保险丝",   "haveglassFuse"),
        new("画作碎片一", "havetavelbit1"),
        new("画作碎片二", "havetavelbit2"),
        new("画作碎片三", "havetavelbit3"),
        new("手雷",     "havehandgranat"),
    };

    internal static bool GodMode;
    internal static bool FreezeEnemies;
    internal static bool EnemiesBlind;
    internal static bool InfiniteAmmo;

    private static InventoryController _inv;
    private static playerCaught _caught;
    private static startNewDay _day;
    private static FirstPersonController_Egen _fps;
    private static EnemyAIGranny _granny;
    private static EnemyAIGrandpa _grandpa;
    private static EnemyAIBaby _baby;
    private static checkShotgun _shotgunHud;
    private static PickUp _pickUp;
    private static shootGun _shooter;
    private static float _nextFindAt;

    private static float _dWalk = 4f, _dRun = 8f, _dJump = 5f, _dGrav = 2f, _dSens = 2f, _dFov = 70f;
    private static float _dGrannySpd = 3f, _dGrandpaSpd = 3f;
    private static float _dGrannyAtk = 2f, _dGrandpaAtk = 2f;
    private static bool _hasDefaults, _hasEnemyDefaults;

    internal static InventoryController Inv => Get(ref _inv);
    internal static playerCaught Caught => Get(ref _caught);
    internal static startNewDay DayCtrl => Get(ref _day);
    internal static FirstPersonController_Egen Fps => Get(ref _fps);
    internal static EnemyAIGranny GrannyAI => Get(ref _granny);
    internal static EnemyAIGrandpa GrandpaAI => Get(ref _grandpa);
    internal static EnemyAIBaby BabyAI => Get(ref _baby);
    internal static checkShotgun ShotgunHud => Get(ref _shotgunHud);
    internal static PickUp PickUpCtl => Get(ref _pickUp);
    internal static shootGun Shooter => Get(ref _shooter);

    private static T Get<T>(ref T cache) where T : UnityEngine.Object
    {
        try { if (cache != null) return cache; } catch { cache = null; }
        if (Time.unscaledTime < _nextFindAt) return null;
        _nextFindAt = Time.unscaledTime + 2f;
        try
        {
            cache = UnityEngine.Object.FindObjectOfType<T>();
        }
        catch
        {
            try
            {
                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(typeof(T));
                cache = (T)UnityEngine.Object.FindObjectOfType(il2cppType);
            }
            catch { cache = null; }
        }
        return cache;
    }

    private static void Safe(Action action)
    {
        try { action(); } catch { }
    }

    private static T Safe<T>(Func<T> func, T def = default)
    {
        try { return func(); } catch { return def; }
    }

    internal static void SnapshotDefaults()
    {
        Safe(() =>
        {
            if (_hasDefaults) return;
            var fp = Fps;
            if (fp == null) return;
            _dWalk = fp.m_WalkSpeed;
            _dRun = fp.m_RunSpeed;
            _dJump = fp.m_JumpSpeed;
            _dGrav = fp.m_GravityMultiplier;
            _dSens = fp.mouseSens;
            var cam = fp.m_Camera != null ? fp.m_Camera.GetComponent<Camera>() : null;
            _dFov = cam != null ? cam.fieldOfView : 70f;
            _hasDefaults = true;
        });
        Safe(() =>
        {
            if (_hasEnemyDefaults) return;
            var g = GrannyAI;
            var gp = GrandpaAI;
            if (g == null && gp == null) return;
            if (g != null) _dGrannySpd = g.walkSpeed;
            if (gp != null) _dGrandpaSpd = gp.walkSpeed;
            _hasEnemyDefaults = true;
        });
    }

    internal static float GetWalkSpeed() => Safe(() => Fps != null ? Fps.m_WalkSpeed : 4f, 4f);
    internal static float GetRunSpeed() => Safe(() => Fps != null ? Fps.m_RunSpeed : 8f, 8f);
    internal static float GetJumpSpeed() => Safe(() => Fps != null ? Fps.m_JumpSpeed : 5f, 5f);
    internal static float GetGravity() => Safe(() => Fps != null ? Fps.m_GravityMultiplier : 2f, 2f);
    internal static float GetMouseSens() => Safe(() => Fps != null ? Fps.mouseSens : 2f, 2f);
    internal static float GetFov() => Safe(() =>
    {
        var fp = Fps;
        if (fp == null || fp.m_Camera == null) return 70f;
        var cam = fp.m_Camera.GetComponent<Camera>();
        return cam != null ? cam.fieldOfView : 70f;
    }, 70f);
    internal static float GetGrannySpeed() => Safe(() => GrannyAI != null ? GrannyAI.walkSpeed : 3f, 3f);
    internal static float GetGrandpaSpeed() => Safe(() => GrandpaAI != null ? GrandpaAI.walkSpeed : 3f, 3f);
    internal static float GetDay() => Safe(() => DayCtrl != null ? DayCtrl.daysCounter : 1f, 1f);

    internal static bool SetWalkSpeed(float v) { var fp = Fps; if (fp == null) return false; Safe(() => fp.m_WalkSpeed = v); return true; }
    internal static bool SetRunSpeed(float v) { var fp = Fps; if (fp == null) return false; Safe(() => fp.m_RunSpeed = v); return true; }
    internal static bool SetJumpSpeed(float v) { var fp = Fps; if (fp == null) return false; Safe(() => fp.m_JumpSpeed = v); return true; }
    internal static bool SetGravity(float v) { var fp = Fps; if (fp == null) return false; Safe(() => fp.m_GravityMultiplier = v); return true; }
    internal static bool SetMouseSens(float v) { var fp = Fps; if (fp == null) return false; Safe(() => fp.mouseSens = v); return true; }
    internal static bool SetFov(float v)
    {
        var fp = Fps;
        if (fp == null || fp.m_Camera == null) return false;
        Safe(() =>
        {
            var cam = fp.m_Camera.GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = v;
        });
        return true;
    }

    internal static bool SetGrannySpeed(float v)
    {
        var g = GrannyAI;
        if (g == null) return false;
        Safe(() =>
        {
            g.walkSpeed = v;
            g.grannysFollowSpeed = v;
            if (g.navComponent != null) g.navComponent.speed = v;
        });
        return true;
    }

    internal static bool SetGrandpaSpeed(float v)
    {
        var g = GrandpaAI;
        if (g == null) return false;
        Safe(() =>
        {
            g.walkSpeed = v;
            g.grannysFollowSpeed = v;
            g.walkAnimSpeed = v;
            g.grannysAnimFollowSpeed = v;
            if (g.navComponent != null) g.navComponent.speed = v;
        });
        return true;
    }

    internal static void SetFreezeEnemies(bool on)
    {
        FreezeEnemies = on;
        if (!on) Safe(() =>
        {
            var g = GrannyAI;
            if (g != null && g.navComponent != null) g.navComponent.enabled = true;
            var gp = GrandpaAI;
            if (gp != null && gp.navComponent != null) gp.navComponent.enabled = true;
            var b = BabyAI;
            if (b != null && b.navComponent != null) b.navComponent.enabled = true;
        });
    }

    internal static bool SetDay(float v)
    {
        var d = DayCtrl;
        if (d == null) return false;
        Safe(() => d.daysCounter = v);
        return true;
    }

    internal static void SetTimeScale(float v) => Time.timeScale = v;
    internal static void SetMute(bool on) => AudioListener.volume = on ? 0f : 1f;

    internal static bool ReleaseBeartrap()
    {
        var fp = Fps;
        if (fp == null) return false;
        Safe(() => fp.playerInBeartrap = false);
        return true;
    }

    internal static bool RestoreDefaults()
    {
        if (!_hasDefaults && !_hasEnemyDefaults) return false;
        Safe(() =>
        {
            var fp = Fps;
            if (fp == null) return;
            fp.m_WalkSpeed = _dWalk;
            fp.m_RunSpeed = _dRun;
            fp.m_JumpSpeed = _dJump;
            fp.m_GravityMultiplier = _dGrav;
            fp.mouseSens = _dSens;
            var cam = fp.m_Camera != null ? fp.m_Camera.GetComponent<Camera>() : null;
            if (cam != null) cam.fieldOfView = _dFov;
        });
        Safe(() =>
        {
            var g = GrannyAI;
            if (g != null) { g.walkSpeed = _dGrannySpd; g.grannysFollowSpeed = _dGrannySpd; if (g.navComponent != null) g.navComponent.speed = _dGrannySpd; if (GodMode) g.attackDistance = -1f; else g.attackDistance = _dGrannyAtk; }
            var gp = GrandpaAI;
            if (gp != null)
            {
                gp.walkSpeed = _dGrandpaSpd;
                gp.grannysFollowSpeed = _dGrandpaSpd;
                gp.walkAnimSpeed = _dGrandpaSpd;
                gp.grannysAnimFollowSpeed = _dGrandpaSpd;
                if (gp.navComponent != null) gp.navComponent.speed = _dGrandpaSpd;
                gp.attackDistance = GodMode ? -1f : _dGrandpaAtk;
            }
        });
        Time.timeScale = 1f;
        AudioListener.volume = 1f;
        return true;
    }

    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static bool SetItemField(InventoryController inv, string name, object value)
    {
        try
        {
            var t = typeof(InventoryController);
            var p = t.GetProperty(name, MemberFlags);
            if (p != null) { p.SetValue(inv, value); return true; }
            var f = t.GetField(name, MemberFlags);
            if (f != null) { f.SetValue(inv, value); return true; }
        }
        catch { }
        return false;
    }

    internal static string GiveItem(ItemDef item)
    {
        var inv = Inv;
        if (inv == null) return "未找到背包 — 请先开始游戏。";
        if (!SetItemField(inv, item.Have, true)) return $"字段 '{item.Have}' 不存在。";
        Safe(() => inv.CheckInventory());
        return $"{item.Label} 已加入背包。";
    }

    internal static string GiveAllItems()
    {
        var inv = Inv;
        if (inv == null) return "未找到背包 — 请先开始游戏。";
        var count = 0;
        foreach (var item in Items)
            if (SetItemField(inv, item.Have, true)) count++;
        Safe(() => inv.CheckInventory());
        return $"已给予 {count}/{Items.Length} 件物品。";
    }

    internal static string TeleportEnemiesToPlayer()
    {
        var fp = Fps;
        if (fp == null || fp.Player == null) return "未找到玩家 — 请先开始游戏。";
        var p = Safe(() => fp.Player.transform.position, Vector3.zero);
        var fwd = Safe(() => fp.Player.transform.forward, Vector3.forward);
        var right = Safe(() => fp.Player.transform.right, Vector3.right);
        var count = 0;
        Safe(() => { var g = GrannyAI; if (g != null && Warp(g.navComponent, p + fwd * 2f)) count++; });
        Safe(() => { var g = GrandpaAI; if (g != null && Warp(g.navComponent, p + right * 2f)) count++; });
        Safe(() => { var b = BabyAI; if (b != null && Warp(b.navComponent, p - fwd * 2f)) count++; });
        return $"已传送 {count}/3 个敌人到身边。";
    }

    internal sealed class EnemyInfo
    {
        internal string Name;
        internal Vector3 Pos;
        internal float Dist;
    }

    internal static EnemyInfo[] GetEnemyInfos()
    {
        var fp = Fps;
        if (fp == null || fp.Player == null) return null;
        var p = Safe(() => fp.Player.transform.position, Vector3.zero);
        var result = new List<EnemyInfo>(3);
        Safe(() =>
        {
            var g = GrannyAI;
            if (g != null)
            {
                var pos = g.transform.position;
                result.Add(new EnemyInfo { Name = "奶奶", Pos = pos, Dist = Vector3.Distance(pos, p) });
            }
        });
        Safe(() =>
        {
            var g = GrandpaAI;
            if (g != null)
            {
                var pos = g.transform.position;
                result.Add(new EnemyInfo { Name = "爷爷", Pos = pos, Dist = Vector3.Distance(pos, p) });
            }
        });
        Safe(() =>
        {
            var b = BabyAI;
            if (b != null)
            {
                var pos = b.transform.position;
                result.Add(new EnemyInfo { Name = "婴儿", Pos = pos, Dist = Vector3.Distance(pos, p) });
            }
        });
        return result.ToArray();
    }

    internal static Camera GetMainCamera() => Safe(() => Camera.main, null);

    internal static Vector3 WorldToViewport(Camera cam, Vector3 worldPos)
        => Safe(() => cam.WorldToViewportPoint(worldPos), new Vector3(-1f, -1f, -1f));

    private static bool Warp(NavMeshAgent agent, Vector3 position)
    {
        if (agent == null) return false;
        try { agent.enabled = true; } catch { }
        try { return agent.Warp(position); } catch { return false; }
    }

    internal static void SetGodMode(bool on)
    {
        if (on && !GodMode)
        {
            // 开启前记录原始攻击距离,供关闭/恢复默认时还原
            Safe(() => { var g = GrannyAI; if (g != null) _dGrannyAtk = g.attackDistance; });
            Safe(() => { var gp = GrandpaAI; if (gp != null) _dGrandpaAtk = gp.attackDistance; });
        }
        if (!on && GodMode)
        {
            // 关闭时恢复原始攻击距离
            Safe(() => { var g = GrannyAI; if (g != null) g.attackDistance = _dGrannyAtk; });
            Safe(() => { var gp = GrandpaAI; if (gp != null) gp.attackDistance = _dGrandpaAtk; });
        }
        GodMode = on;
    }

    internal static void TickEffects()
    {
        if (GodMode) Safe(() =>
        {
            // 源头阻断:攻击距离判定永不可能满足,敌人看得见、追得上,但攻击(含动画/协程)根本不会发起
            var g = GrannyAI;
            if (g != null) g.attackDistance = -1f;
            var gp = GrandpaAI;
            if (gp != null) gp.attackDistance = -1f;
            var b = BabyAI;
            if (b != null) b.playerNear = false;
            // 兜底:清除死亡序列标志,拦截坠落等非敌人路径
            var pc = Caught;
            if (pc != null)
            {
                pc.grannyTakePlayer = false;
                pc.granpaTakePlayer = false;
                pc.monsterTakePlayer = false;
                pc.PlayerGetsElectric = false;
                pc.PlayerGetTortyr = false;
                pc.spiderBitePlayer = false;
                pc.explodingPlayer = false;
                pc.BabyBitePlayer = false;
                pc.playerIsBiten = false;
                pc.playerFallDead = false;
            }
            var fp = Fps;
            if (fp != null)
            {
                fp.playerInBeartrap = false;
                fp.playerCaught = false;
            }
        });

        // 无限弹药:强制所有已知装填/射击门控 + 每 3 秒输出状态探针到日志
        if (InfiniteAmmo)
        {
            Safe(() =>
            {
                var cs = ShotgunHud;
                if (cs != null)
                {
                    cs.HowMuchAmmo = 2f;
                    cs.Loaded = true;
                }
                var pu = PickUpCtl;
                if (pu != null)
                {
                    pu.oldShotgunLoaded = true;
                    var inv = Inv;
                    if (inv != null && inv.havetaserGun) pu.havetaserGunArrow = true;
                }
                var sg = Shooter;
                if (sg != null)
                {
                    sg.readyToShoot = true;
                    sg.readyToShootAgain = true;
                }
            });
            AmmoProbe();
        }

        if (!FreezeEnemies && !EnemiesBlind) return;

        Safe(() =>
        {
            var g = GrannyAI;
            var gp = GrandpaAI;
            var b = BabyAI;

            if (FreezeEnemies)
            {
                if (g != null && g.navComponent != null) g.navComponent.enabled = false;
                if (gp != null && gp.navComponent != null) gp.navComponent.enabled = false;
                if (b != null && b.navComponent != null) b.navComponent.enabled = false;
            }

            if (EnemiesBlind)
            {
                if (g != null)
                {
                    g.seePlayer = false;
                    g.huntPlayer = false;
                    g.grannyHearPlayer = false;
                    g.attackingPlayer = false;
                }
                if (gp != null)
                {
                    gp.seePlayer = false;
                    gp.huntPlayer = false;
                    gp.attackingPlayer = false;
                    gp.granpaGuardingSeePlayer = false;
                }
                if (b != null)
                {
                    b.babyHunting = false;
                    b.playerNear = false;
                }
            }
        });
    }

    private static float _nextAmmoProbe;

    // 每 3 秒输出一次武器相关状态,用于定位射击门控
    private static void AmmoProbe()
    {
        if (Time.unscaledTime < _nextAmmoProbe) return;
        _nextAmmoProbe = Time.unscaledTime + 3f;
        Safe(() =>
        {
            var cs = ShotgunHud;
            var pu = PickUpCtl;
            var sg = Shooter;
            var inv = Inv;
            var fp = Fps;
            Plugin.StaticLog?.LogWarning(
                "[ModMenu:ammo] inv.haveshotGun=" + (inv != null ? inv.haveshotGun.ToString() : "null") +
                " inv.havetaserGun=" + (inv != null ? inv.havetaserGun.ToString() : "null") +
                " | cs=" + (cs != null ? "Loaded=" + cs.Loaded + " Ammo=" + cs.HowMuchAmmo : "null") +
                " | pu=" + (pu != null ? "oldLoaded=" + pu.oldShotgunLoaded + " taserArrow=" + pu.havetaserGunArrow + " holding=" + pu.pickUp : "null") +
                " | sg=" + (sg != null ? "ready=" + sg.readyToShoot + " readyAgain=" + sg.readyToShootAgain + " shooting=" + sg.shooting + " timer=" + sg.shootTimer.ToString("F2") : "null") +
                " | fp.playerInBeartrap=" + (fp != null ? fp.playerInBeartrap.ToString() : "null"));
        });
    }
}
