namespace GrannyModMenu
{
    /// <summary>
    /// 全局模组状态 - 所有功能开关与数值
    /// 通过菜单 UI 修改这些字段,Update 时应用到游戏对象
    /// </summary>
    internal static class ModState
    {
        // ===== Player 玩家相关 =====
        public static bool GodMode = false;                       // 上帝模式 (无敌: 所有死亡/被抓字段 false)
        public static bool NoClip = false;                        // 穿墙 (禁用玩家碰撞器)
        public static bool FlyMode = false;                       // 飞行模式 (空中自由移动)
        public static float FlySpeed = 10f;                       // 飞行速度

        public static bool PlayerSpeedEnabled = false;
        public static float PlayerSpeedMultiplier = 2.0f;
        public static bool PlayerJumpBoost = false;
        public static float PlayerJumpMultiplier = 2.0f;
        public static bool PlayerAlwaysGrounded = false;
        public static bool PlayerNoFallDamage = false;
        public static bool PlayerCrouch = false;                  // 强制蹲下 (潜行)
        public static bool PlayerFlashlight = false;              // 拥有手电筒
        public static bool PlayerInAirControl = false;            // 空中控制增强

        // 玩家缩放
        public static bool PlayerScaleEnabled = false;
        public static float PlayerScale = 1.0f;                  // 0.1 ~ 10

        // ===== 敌人: Granny =====
        public static bool GrannyFreezeEnabled = false;
        public static bool GrannyBlind = false;
        public static bool GrannyDeaf = false;
        public static bool GrannyNoAttack = false;
        public static bool GrannySpeedEnabled = false;
        public static float GrannySpeedMultiplier = 0.5f;
        public static bool GrannyNoCatch = false;
        public static float GrannyScale = 1.0f;                    // Granny 缩放
        public static bool GrannyScaleEnabled = false;            // 是否启用 Granny 缩放

        // ===== 敌人: Spider (小蜘蛛) =====
        public static bool SpiderFreeze = false;                  // spiderDead = true (永久冻结)
        public static bool SpiderNoHunt = false;                  // huntPlayer = false
        public static bool SpiderNoBite = false;                  // SpiderBitePlayer = false
        public static bool SpiderNoCatch = false;                  // playerCaught = false
        public static float SpiderScale = 1.0f;                  // 蜘蛛缩放
        public static bool SpiderScaleEnabled = false;

        // ===== 敌人: MomSpider (下水道蜘蛛 granny / MomSpiderHead) =====
        public static bool MomSpiderFreeze = false;                // Hunting = false + getShot = true (停止追猎)
        public static bool MomSpiderBlind = false;                // seePlayer = false (失明)
        public static bool MomSpiderNoCatch = false;              // playerCaught = false (无法抓住)
        public static bool MomSpiderEscape = false;                // playerEscape = true (玩家逃脱)
        public static bool MomSpiderDead = false;                  // 杀死 SpiderMom (解除无法被击杀的限制)
        public static float MomSpiderScale = 1.0f;                // 妈妈蜘蛛缩放
        public static bool MomSpiderScaleEnabled = false;

        // ===== 敌人: MomCrawl (妈妈爬行者 / momCrawl) =====
        public static bool MomCrawlDead = false;                  // momDead = true
        public static float MomCrawlScale = 1.0f;                // 妈妈爬行者缩放
        public static bool MomCrawlScaleEnabled = false;

        // ===== 敌人: LittleSanta (小圣诞老人) =====
        public static bool SantaStunned = false;                  // currentState = 3 (Stunned)
        public static float SantaScale = 1.0f;                  // 小圣诞老人缩放
        public static bool SantaScaleEnabled = false;

        // ===== 敌人: Crow (乌鸦) =====
        public static bool CrowFreeze = false;                    // crowGetShoot = true (吓跑)
        public static bool CrowNoAttack = false;                  // isAttacking = false
        public static bool CrowNoSteal = false;                  // playerSteal = false
        public static float CrowScale = 1.0f;                  // 乌鸦缩放
        public static bool CrowScaleEnabled = false;

        // ===== 敌人: Rat (老鼠) =====
        public static bool RatFreeze = false;                    // 调用 ratStopped + 禁用 NavMeshAgent
        public static float RatScale = 1.0f;                    // 老鼠缩放
        public static bool RatScaleEnabled = false;

        // ===== Player Caught 通用 =====
        public static bool NeverGetCaught = false;

        // ===== 场景物体选中与克隆 =====
        public static float SelectedObjectScale = 1.0f;          // 选中物体缩放 (0.1 ~ 10)

        // ===== Game / Day 游戏流程 =====
        public static bool ForceDay2 = false;
        public static bool ForceDay3 = false;
        public static bool ForceEscaped = false;
        public static bool NightmareMode = false;

        // ===== Weapon 武器相关 =====
        public static bool GunRapidFire = false;
        public static bool GunInfiniteRange = false;
        public static bool OldShotgunLoaded = false;

        // ===== Map / 地图 (新增) =====
        public static bool FullBright = false;                    // 全图变亮 (手电筒 Light.intensity = 5)
        public static bool NoFog = false;                        // 关闭雾 (RenderSettings.fog = false)
        public static bool AllLightsOn = false;                  // 启用所有光源 (遍历场景 Light 组件)

        // ===== Items 物品栏 =====
        public static bool OldShotgunLoadedOneShot = false;       // 旧猎枪上膛 (按钮触发)

        // ===== Vehicle 车辆与电梯 =====
        public static bool CarReadyAll = false;

        // ===== Menu UI =====
        public static bool MenuVisible = false;
        // 0=Player, 1=Enemies, 2=Map, 3=Game, 4=Weapon, 5=Items, 6=Vehicle, 7=About
        public static int SelectedTab = 0;
    }
}
