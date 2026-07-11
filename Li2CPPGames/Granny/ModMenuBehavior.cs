using System;
using UnityEngine;
using UnityEngine.AI;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace GrannyModMenu
{
    /// <summary>
    /// 模组菜单行为组件
    /// 渲染: Unity IMGUI (OnGUI) - Unity6 + D3D12 兼容, 跨图形 API 通用
    /// 应用: 每帧 Update() 将 ModState 应用到游戏对象
    /// </summary>
    public class ModMenuBehavior : MonoBehaviour
    {
        // IL2CPP 必需的 IntPtr 构造函数 - 通过 base(ptr) 建立 GCHandle 映射
        public ModMenuBehavior(IntPtr ptr) : base(ptr) { }

        // ===== 缓存游戏对象引用 =====
        private FPSControllerNEW _player;
        private Transform _playerTransform; // 备选: 直接缓存玩家 Transform
        private EnemyAIGranny _granny;
        private playerCaught _playerCaught;
        private nightmareOnOff _nightmareMode;
        private GunShoot _gunShoot;
        private InventoryController _inventory;
        private PickUp _pickUp; // 拾取检测器, 持有地上物品引用 (hanglockKey 等)
        private flashlightController _flashlight;
        private checkTheCar _checkCar;
        private CheckExitDoor _checkExitDoor; // 持有 padlockCodePL1-5 真地上物品
        private elevatorController _elevator;
        private spiderControll[] _spiders;
        private CrowControl[] _crows;
        private ratController[] _rats;
        private MomSpiderHead[] _momSpiders;
        private momCrawl[] _momCrawls;
        private LittleSantaController[] _santas;
        private pickupFlashlight _pickupFlashlight;
        private Light[] _allLights;

        // 选中物体
        private GameObject _selectedObject;
        private string _selectedInfo = "(未选中)";
        private float _selectedOrigScale = 1f;

        // 已传送的物品 (第一次点击传送真物品, 之后点击复制)
        private System.Collections.Generic.HashSet<ItemId> _teleportedItems = new System.Collections.Generic.HashSet<ItemId>();

        private float _refreshTimer = 0f;
        private const float RefreshInterval = 1.0f;

        // 原始值缓存
        private float _origForwardSpeed, _origBackwardSpeed, _origSidestepSpeed, _origJumpSpeed;
        private float _origGrannyWalkSpeed, _origGrannyFollowSpeed;
        private float _origGunFireRate, _origGunRange;
        private float _origInAirMultiplier;
        private bool _origValuesCached = false;

        // 滚动位置 (手动管理, 不用 GUI.BeginScrollView)
        private float _scrollY = 0f;
        private float _contentTotalH = 0f;

        // ===== 配色方案 (深色主题 + 青色强调) =====
        private static readonly Color BgColor = new Color(0.06f, 0.07f, 0.10f, 0.98f);
        private static readonly Color SectionBgColor = new Color(0.12f, 0.13f, 0.18f, 0.95f);
        private static readonly Color HeaderColor = new Color(0.14f, 0.18f, 0.28f, 1.00f);
        private static readonly Color TabColor = new Color(0.10f, 0.11f, 0.16f, 0.95f);
        private static readonly Color TabSelectedColor = new Color(0.20f, 0.50f, 0.90f, 1.00f);
        private static readonly Color AccentColor = new Color(0.31f, 0.76f, 0.97f, 1f);
        private static readonly Color AccentDimColor = new Color(0.20f, 0.45f, 0.65f, 1f);
        private static readonly Color OnColor = new Color(0.30f, 0.85f, 0.40f, 1f);
        private static readonly Color OffColor = new Color(0.85f, 0.30f, 0.30f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        private static readonly Color TextDimColor = new Color(0.60f, 0.62f, 0.68f, 1f);
        private static readonly Color BorderColor = new Color(0.25f, 0.30f, 0.40f, 0.80f);
        private static readonly Color HoverColor = new Color(0.20f, 0.30f, 0.50f, 0.50f);
        private static readonly Color BtnBgColor = new Color(0.16f, 0.18f, 0.24f, 1f);

        // 菜单布局常量
        private const float BaseWidth = 580f;
        private const float HeaderHeight = 44f;
        private const float TabHeight = 34f;
        private const float RowHeight = 28f;
        private const float RowGap = 4f;
        private const float Padding = 14f;

        // 当前 hover 检测 (屏幕坐标)
        private Vector2 _mousePos;
        // 内容区坐标 (用于 hover 转换)
        private Rect _contentRect;
        // 可见性裁剪区域 (用于跳过不可见行)
        private float _clipY;
        private float _clipH;
        private float _scale = 1f;

        private void Awake()
        {
            GrannyModMenuPlugin.PluginLog.LogInfo("ModMenuBehavior Awake");
            RefreshReferences();
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(GrannyModMenuPlugin.MenuToggleKey.Value))
                    ModState.MenuVisible = !ModState.MenuVisible;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Input: {ex.Message}"); }

            // 玩家对象未找到时, 每帧尝试查找 (场景加载后立即捕获)
            if (_player == null && _playerTransform == null)
            {
                _refreshTimer += Time.deltaTime;
                if (_refreshTimer >= 0.5f)
                {
                    _refreshTimer = 0f;
                    RefreshReferences();
                }
            }
            else
            {
                // 已找到玩家, 改为低频刷新其他对象
                _refreshTimer += Time.deltaTime;
                if (_refreshTimer >= RefreshInterval)
                {
                    _refreshTimer = 0f;
                    RefreshReferences();
                }
            }

            ApplyModState();
            ApplyFlyMode();
            ApplyPlayerScale();
            ApplySelectedObjectScale();
            ApplyAllEnemyScales();
            DiagnosePickUpRay();
            DiagnoseSceneItems();
            DiagnoseSpiderKeyStateChange();
        }

        // 诊断: 检测 havespiderKey 状态变化 (拾取/扔出), 记录关键字段状态
        private void DiagnoseSpiderKeyStateChange()
        {
            if (_inventory == null) return;
            try
            {
                bool curHave = _inventory.havespiderKey;
                if (!_spiderKeyStateInited)
                {
                    _lastHaveSpiderKey = curHave;
                    _spiderKeyStateInited = true;
                    return;
                }
                if (curHave != _lastHaveSpiderKey)
                {
                    string action = curHave ? "拾取" : "扔出";
                    GrannyModMenuPlugin.PluginLog.LogInfo($"=== SpiderKey 状态变化: {action} (havespiderKey: {_lastHaveSpiderKey} -> {curHave}) ===");
                    LogSpiderKeyFields();
                    _lastHaveSpiderKey = curHave;
                }
                // 持续诊断: 已拾取状态下, 每秒记录 SpiderMomKeyHand 的状态, 了解游戏对它做了什么
                if (curHave)
                {
                    _spiderKeyHeldTimer += Time.deltaTime;
                    if (_spiderKeyHeldTimer >= 1.0f)
                    {
                        _spiderKeyHeldTimer = 0f;
                        LogSpiderKeyFields();
                    }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"DiagnoseSpiderKeyStateChange: {ex.Message}"); }
        }

        private float _spiderKeyHeldTimer = 0f;

        private void LogSpiderKeyFields()
        {
            try
            {
                // PickUp.spiderKey
                if (_pickUp != null)
                {
                    GameObject pkSpiderKey = _pickUp.spiderKey;
                    if (pkSpiderKey != null)
                    {
                        bool pkActive = pkSpiderKey.activeSelf;
                        bool pkActiveH = pkSpiderKey.activeInHierarchy;
                        Vector3 pkPos = pkSpiderKey.transform.position;
                        Vector3 pkScale = pkSpiderKey.transform.localScale;
                        string pkParent = pkSpiderKey.transform.parent != null ? pkSpiderKey.transform.parent.name : "null";
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  PickUp.spiderKey -> '{pkSpiderKey.name}' active={pkActive}, activeH={pkActiveH}, parent='{pkParent}', pos={pkPos}, scale={pkScale}");
                    }
                    else GrannyModMenuPlugin.PluginLog.LogInfo("  PickUp.spiderKey -> null");
                }
                // Inventory.newspiderKey
                Transform invNewSpiderKey = _inventory.newspiderKey;
                if (invNewSpiderKey != null)
                {
                    GameObject invGo = invNewSpiderKey.gameObject;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  Inventory.newspiderKey -> '{invGo.name}' active={invGo.activeSelf}, activeH={invGo.activeInHierarchy}, parent='{(invNewSpiderKey.parent != null ? invNewSpiderKey.parent.name : "null")}', pos={invNewSpiderKey.position}, scale={invNewSpiderKey.localScale}");
                }
                else GrannyModMenuPlugin.PluginLog.LogInfo("  Inventory.newspiderKey -> null");
                // Inventory.spiderKey
                GameObject invSpiderKey = _inventory.spiderKey;
                if (invSpiderKey != null)
                {
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  Inventory.spiderKey -> '{invSpiderKey.name}' active={invSpiderKey.activeSelf}, activeH={invSpiderKey.activeInHierarchy}, pos={invSpiderKey.transform.position}");
                }
                else GrannyModMenuPlugin.PluginLog.LogInfo("  Inventory.spiderKey -> null");
                // 从 _spawnedItems 找到 SpiderMomKeyHand, 记录其状态
                foreach (var si in _spawnedItems)
                {
                    if (si == null || si.obj == null) continue;
                    if (si.itemId == ItemId.spiderKey)
                    {
                        GameObject handObj = si.obj;
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  SpawnedItem(spiderKey) -> '{handObj.name}' active={handObj.activeSelf}, activeH={handObj.activeInHierarchy}, parent='{(handObj.transform.parent != null ? handObj.transform.parent.name : "null")}', pos={handObj.transform.position}, scale={handObj.transform.localScale}");
                        break;
                    }
                }
                // 对比: 记录其他正常物品的 PickUp 字段状态 (exitKey)
                if (_pickUp != null)
                {
                    GameObject pkExitKey = _pickUp.exitKey;
                    if (pkExitKey != null)
                    {
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  [对比] PickUp.exitKey -> '{pkExitKey.name}' active={pkExitKey.activeSelf}, activeH={pkExitKey.activeInHierarchy}, parent='{(pkExitKey.transform.parent != null ? pkExitKey.transform.parent.name : "null")}', pos={pkExitKey.transform.position}");
                    }
                    else GrannyModMenuPlugin.PluginLog.LogInfo("  [对比] PickUp.exitKey -> null");
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"LogSpiderKeyFields: {ex.Message}"); }
        }

        // 诊断: 搜索场景中所有含 "hanglock"/"key"/"nyckel" 的对象 (只执行一次)
        // 目的: 找到真正的地上物品 (非手上物品)
        private bool _sceneSearched = false;
        private bool _lastHaveSpiderKey = false;
        private bool _spiderKeyStateInited = false;
        private void DiagnoseSceneItems()
        {
            if (_sceneSearched) return;
            if (_spawnedItems.Count == 0) return;
            _sceneSearched = true;
            try
            {
                var type = Il2CppType.Of<Transform>();
                if (type == null) { GrannyModMenuPlugin.PluginLog.LogWarning("DiagnoseSceneItems: Transform type 为 null"); return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null) { GrannyModMenuPlugin.PluginLog.LogWarning("DiagnoseSceneItems: FindObjectsOfType 返回 null"); return; }
                GrannyModMenuPlugin.PluginLog.LogInfo($"=== DiagnoseSceneItems: 搜索 {objs.Length} 个 Transform ===");
                int found = 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    try
                    {
                        if (objs[i] == null) continue;
                        var t = objs[i].TryCast<Transform>();
                        if (t == null) continue;
                        string name = t.name;
                        if (name == null) continue;
                        string lower = name.ToLower();
                        // 搜索 hanglock, nyckel(瑞典语key), key (排除 keyboard/monkey等)
                        bool match = lower.Contains("hanglock") || lower.Contains("nyckel") ||
                                     (lower.Contains("key") && !lower.Contains("keyboard") && !lower.Contains("monkey") && !lower.Contains("donkey"));
                        if (!match) continue;
                        found++;
                        GameObject go = t.gameObject;
                        Vector3 pos = t.position;
                        bool activeInH = go.activeInHierarchy;
                        bool activeSelf = go.activeSelf;
                        int layer = go.layer;
                        string layerName = LayerMask.LayerToName(layer);
                        string parentName = t.parent != null ? t.parent.name : "null";
                        // 获取 mesh bounds (如果有)
                        string meshInfo = "";
                        try
                        {
                            var mf = go.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                            {
                                meshInfo = $" meshBounds={mf.sharedMesh.bounds.size}";
                            }
                        } catch { }
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  Found: '{name}' pos={pos} layer={layer}({layerName}) tag='{go.tag}' activeH={activeInH} activeSelf={activeSelf} parent='{parentName}'{meshInfo}");
                    }
                    catch { }
                }
                GrannyModMenuPlugin.PluginLog.LogInfo($"=== DiagnoseSceneItems 完成: 找到 {found} 个匹配对象 ===");

                // 新增: 搜索 HandHoldObjects 下的所有子对象
                // PickUp.exitKey 指向 HouseKeyHand (parent='HandHoldObjects'), 说明正常物品的 PickUp.***Key 字段指向手上物品
                // 手上物品在 HandHoldObjects 下, 拾取/扔出都通过这个引用
                DiagnoseHandHoldObjects();
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"DiagnoseSceneItems: {ex.Message}"); }
        }

        // 诊断: 搜索 HandHoldObjects 下的所有子对象, 找到 SpiderKey 手上物品
        private void DiagnoseHandHoldObjects()
        {
            try
            {
                GameObject handHold = GameObject.Find("HandHoldObjects");
                if (handHold == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning("DiagnoseHandHoldObjects: 未找到 'HandHoldObjects' GameObject");
                    return;
                }
                GrannyModMenuPlugin.PluginLog.LogInfo($"=== DiagnoseHandHoldObjects: '{handHold.name}' 子对象列表 ===");
                Transform handHoldT = handHold.transform;
                int childCount = handHoldT.childCount;
                GrannyModMenuPlugin.PluginLog.LogInfo($"  HandHoldObjects 有 {childCount} 个子对象");
                for (int i = 0; i < childCount; i++)
                {
                    try
                    {
                        Transform child = handHoldT.GetChild(i);
                        if (child == null) continue;
                        GameObject childGo = child.gameObject;
                        Vector3 pos = child.position;
                        Vector3 scale = child.localScale;
                        bool activeSelf = childGo.activeSelf;
                        bool activeH = childGo.activeInHierarchy;
                        string parentName = child.parent != null ? child.parent.name : "null";
                        // 检查是否有 MeshRenderer
                        bool hasRenderer = childGo.GetComponent<MeshRenderer>() != null;
                        bool hasMeshFilter = childGo.GetComponent<MeshFilter>() != null;
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  [{i}] '{child.name}' active={activeSelf}, activeH={activeH}, parent='{parentName}', pos={pos}, scale={scale}, hasRenderer={hasRenderer}, hasMeshFilter={hasMeshFilter}");
                    }
                    catch { }
                }
                GrannyModMenuPlugin.PluginLog.LogInfo($"=== DiagnoseHandHoldObjects 完成 ===");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"DiagnoseHandHoldObjects: {ex.Message}"); }
        }

        // 诊断: 模拟 PickUp 射线检测, 输出射线信息和命中情况
        private float _diagTimer = 0f;
        private void DiagnosePickUpRay()
        {
            _diagTimer += Time.deltaTime;
            if (_diagTimer < 1.0f) return;
            _diagTimer = 0f;
            if (_pickUp == null || _spawnedItems.Count == 0) return;
            try
            {
                // 检查 PickUp 的关键字段
                bool readyPickUp = _pickUp.readyPickUp;
                bool pickUpField = _pickUp.pickUp;
                bool playerTaken = _pickUp.playerTaken;
                GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: PickUp 字段: readyPickUp={readyPickUp}, pickUp={pickUpField}, playerTaken={playerTaken}");

                GameObject seeRay = _pickUp.SeeRay;
                if (seeRay == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: PickUp.SeeRay 为 null");
                    return;
                }
                Vector3 origin = seeRay.transform.position;
                Vector3 dir = seeRay.transform.forward;
                int mask = _pickUp.layerMask.value;
                // 检查生成的物品
                var item = _spawnedItems[_spawnedItems.Count - 1];
                if (item.obj != null)
                {
                    int itemLayer = item.obj.layer;
                    string itemTag = item.obj.tag;
                    bool layerOk = (mask & (1 << itemLayer)) != 0;
                    Vector3 itemPos = item.obj.transform.position;
                    float dist = Vector3.Distance(origin, itemPos);
                    // 模拟射线检测 (用 SphereCast, rayThick 可能是射线半径)
                    float radius = _pickUp.rayThick;
                    if (radius < 0.01f) radius = 0.1f;
                    bool rayHit = Physics.Raycast(origin, dir, out RaycastHit hit, 10f, mask);
                    bool sphereHit = Physics.SphereCast(origin, radius, dir, out RaycastHit sphereHitInfo, 10f, mask);
                    string hitName = rayHit ? hit.collider.gameObject.name : "无";
                    float hitDist = rayHit ? hit.distance : -1f;
                    string sphereHitName = sphereHit ? sphereHitInfo.collider.gameObject.name : "无";
                    float sphereHitDist = sphereHit ? sphereHitInfo.distance : -1f;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: 物品 '{item.obj.name}' tag='{itemTag}', layer={itemLayer}, layerOk={layerOk}, dist={dist:F2}m");
                    GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: Raycast 命中='{hitName}' (dist={hitDist:F2}m), SphereCast(radius={radius}) 命中='{sphereHitName}' (dist={sphereHitDist:F2}m)");

                    // 关键诊断: 检查 SphereCast 命中物体是否是 PickUp 各字段的引用
                    // (因为 PickUp 可能用 SphereCast 而非 Raycast)
                    GameObject sphereHitGo = sphereHit ? sphereHitInfo.collider.gameObject : null;
                    if (sphereHitGo != null)
                    {
                        bool isHanglockKey = IsSameReference(sphereHitGo, _pickUp.hanglockKey);
                        bool isWeaponKey = IsSameReference(sphereHitGo, _pickUp.weaponKey);
                        bool isExitKey = IsSameReference(sphereHitGo, _pickUp.exitKey);
                        bool isSafeKey = IsSameReference(sphereHitGo, _pickUp.safeKey);
                        bool isPadlockCode = IsSameReference(sphereHitGo, _pickUp.padlockCode);
                        bool isSpiderKey = IsSameReference(sphereHitGo, _pickUp.spiderKey);
                        bool isPlayhouseKey = IsSameReference(sphereHitGo, _pickUp.playhouseKey);
                        int hitLayer = sphereHitGo.layer;
                        Vector3 hitScale = sphereHitGo.transform.localScale;
                        Vector3 hitLossyScale = sphereHitGo.transform.lossyScale;
                        Vector3 hitPos = sphereHitGo.transform.position;
                        bool hasRb = sphereHitGo.GetComponent<Rigidbody>() != null;
                        Collider hitCol = sphereHitGo.GetComponent<Collider>();
                        string colType = hitCol != null ? hitCol.GetType().Name : "无";
                        Vector3 colWorldSize = hitCol != null ? hitCol.bounds.size : Vector3.zero;
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: SphereCast命中物体: layer={hitLayer}({LayerMask.LayerToName(hitLayer)}), localScale={hitScale}, lossyScale={hitLossyScale}, pos={hitPos}, hasRb={hasRb}, col={colType} worldSize={colWorldSize}");
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: SphereCast命中字段匹配: isHanglockKey={isHanglockKey}, isWeaponKey={isWeaponKey}, isExitKey={isExitKey}, isSafeKey={isSafeKey}, isPadlockCode={isPadlockCode}, isSpiderKey={isSpiderKey}, isPlayhouseKey={isPlayhouseKey}");
                    }
                    // 也检查 Raycast 命中物体
                    if (rayHit)
                    {
                        GameObject hitGo = hit.collider.gameObject;
                        bool isHanglockKey = IsSameReference(hitGo, _pickUp.hanglockKey);
                        bool isWeaponKey = IsSameReference(hitGo, _pickUp.weaponKey);
                        bool isExitKey = IsSameReference(hitGo, _pickUp.exitKey);
                        bool isSafeKey = IsSameReference(hitGo, _pickUp.safeKey);
                        bool isPadlockCode = IsSameReference(hitGo, _pickUp.padlockCode);
                        bool isSpiderKey = IsSameReference(hitGo, _pickUp.spiderKey);
                        bool isPlayhouseKey = IsSameReference(hitGo, _pickUp.playhouseKey);
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Diag: Raycast命中字段匹配: isHanglockKey={isHanglockKey}, isWeaponKey={isWeaponKey}, isExitKey={isExitKey}, isSafeKey={isSafeKey}, isPadlockCode={isPadlockCode}, isSpiderKey={isSpiderKey}, isPlayhouseKey={isPlayhouseKey}");
                    }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"Diag: {ex.Message}"); }
        }

        // 比较 IL2CPP 两个 GameObject 引用是否指向同一个对象
        private bool IsSameReference(GameObject a, GameObject b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            try
            {
                // 用 GetInstanceID 比较 (IL2CPP 对象有唯一实例 ID)
                return a.GetInstanceID() == b.GetInstanceID();
            }
            catch { return false; }
        }

        private string LayerMaskToString(int mask)
        {
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    if (names.Length > 0) names.Append(", ");
                    names.Append($"{i}:{LayerMask.LayerToName(i)}");
                }
            }
            return names.ToString();
        }

        // 安全的 GetComponent: 使用 Il2CppType.Of<T>() 避免泛型约束错误
        private T GetComponentSafe<T>(GameObject go) where T : Il2CppObjectBase
        {
            if (go == null) return null;
            try
            {
                var type = Il2CppType.Of<T>();
                if (type == null) return null;
                var component = go.GetComponent(type);
                if (component == null) return null;
                return component.TryCast<T>();
            }
            catch { return null; }
        }

        // 安全的 FindObjectOfType: 使用 FindObjectsOfType + 取第一个, 避免 FindObjectOfType(type) 在 IL2CPP 中的问题
        private T FindObjectOfTypeSafe<T>() where T : Il2CppObjectBase
        {
            try
            {
                var type = Il2CppType.Of<T>();
                if (type == null) return null;
                // 用 FindObjectsOfType 代替 FindObjectOfType (更可靠)
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) return null;
                for (int i = 0; i < objs.Length; i++)
                {
                    if (objs[i] == null) continue;
                    var result = objs[i].TryCast<T>();
                    if (result != null) return result;
                }
                return null;
            }
            catch { return null; }
        }

        // 非泛型 FindAll: 通过 Il2CppType.Of + ArrayList 转换, 避免泛型方法返回 T[] 被 IL2CPP 拒绝
        // 注意: 使用 ref 参数避免方法返回 T[] (IL2CPP 不支持泛型方法返回 T[])
        private void FindAllSpiders()
        {
            try
            {
                var type = Il2CppType.Of<spiderControll>();
                if (type == null) { _spiders = null; return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) { _spiders = new spiderControll[0]; return; }
                _spiders = new spiderControll[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    try { _spiders[i] = objs[i]?.TryCast<spiderControll>(); }
                    catch { _spiders[i] = null; }
                }
            }
            catch { _spiders = null; }
        }

        private void FindAllCrows()
        {
            try
            {
                var type = Il2CppType.Of<CrowControl>();
                if (type == null) { _crows = null; return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) { _crows = new CrowControl[0]; return; }
                _crows = new CrowControl[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    try { _crows[i] = objs[i]?.TryCast<CrowControl>(); }
                    catch { _crows[i] = null; }
                }
            }
            catch { _crows = null; }
        }

        private void FindAllRats()
        {
            try
            {
                var type = Il2CppType.Of<ratController>();
                if (type == null) { _rats = null; return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) { _rats = new ratController[0]; return; }
                _rats = new ratController[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    try { _rats[i] = objs[i]?.TryCast<ratController>(); }
                    catch { _rats[i] = null; }
                }
            }
            catch { _rats = null; }
        }

        private void FindAllMomSpiders()
        {
            try
            {
                var type = Il2CppType.Of<MomSpiderHead>();
                if (type == null) { _momSpiders = null; return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) { _momSpiders = new MomSpiderHead[0]; return; }
                _momSpiders = new MomSpiderHead[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    try { _momSpiders[i] = objs[i]?.TryCast<MomSpiderHead>(); }
                    catch { _momSpiders[i] = null; }
                }
            }
            catch { _momSpiders = null; }
        }

        private void FindAllMomCrawls()
        {
            try
            {
                var type = Il2CppType.Of<momCrawl>();
                if (type == null) { _momCrawls = null; return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) { _momCrawls = new momCrawl[0]; return; }
                _momCrawls = new momCrawl[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    try { _momCrawls[i] = objs[i]?.TryCast<momCrawl>(); }
                    catch { _momCrawls[i] = null; }
                }
            }
            catch { _momCrawls = null; }
        }

        private void FindAllSantas()
        {
            try
            {
                var type = Il2CppType.Of<LittleSantaController>();
                if (type == null) { _santas = null; return; }
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null || objs.Length == 0) { _santas = new LittleSantaController[0]; return; }
                _santas = new LittleSantaController[objs.Length];
                for (int i = 0; i < objs.Length; i++)
                {
                    try { _santas[i] = objs[i]?.TryCast<LittleSantaController>(); }
                    catch { _santas[i] = null; }
                }
            }
            catch { _santas = null; }
        }

        private int _refreshCount = 0;
        private void RefreshReferences()
        {
            try
            {
                _refreshCount++;
                bool wasPlayerNull = (_player == null && _playerTransform == null);
                if (wasPlayerNull)
                {
                    // 方案 1: 安全的 FindObjectOfType (使用 Il2CppType.Of 避免泛型约束错误)
                    _player = FindObjectOfTypeSafe<FPSControllerNEW>();

                    // 方案 2: 通过 CharacterController 查找 (玩家必有此组件)
                    if (_player == null && _playerTransform == null)
                    {
                        try
                        {
                            var ccs = UnityEngine.Object.FindObjectsOfType<CharacterController>();
                            if (ccs != null && ccs.Length > 0)
                            {
                                if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 找到 {ccs.Length} 个 CharacterController");
                                foreach (var cc in ccs)
                                {
                                    if (cc == null) continue;
                                    var go = cc.gameObject;
                                    if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogInfo($"  - CC 对象: '{go.name}' (tag='{go.tag}')");
                                    if (go.name == "Player" || go.name.Contains("Player") || go.CompareTag("Player"))
                                    {
                                        _player = GetComponentSafe<FPSControllerNEW>(go);
                                        if (_player != null)
                                        {
                                            GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 通过 CharacterController 找到 FPSControllerNEW: {go.name}");
                                            break;
                                        }
                                        // 玩家对象上没有 FPSControllerNEW, 直接缓存 Transform
                                        _playerTransform = go.transform;
                                        GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 缓存玩家 Transform (无 FPSControllerNEW): {go.name}");
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogWarning($"FindObjectsOfType<CharacterController> 异常: {ex.Message}"); }
                    }

                    // 方案 3: 通过 Camera.main 查找 (相机跟随玩家)
                    if (_player == null && _playerTransform == null)
                    {
                        try
                        {
                            Camera cam = Camera.main;
                            if (cam != null)
                            {
                                if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: Camera.main = '{cam.gameObject.name}'");
                                Transform t = cam.transform.parent;
                                int depth = 0;
                                while (t != null && depth < 5)
                                {
                                    if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogInfo($"  相机父级[{depth}]: '{t.name}'");
                                    var fps = GetComponentSafe<FPSControllerNEW>(t.gameObject);
                                    if (fps != null) { _player = fps; GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 通过相机找到 FPSControllerNEW: '{t.name}'"); break; }
                                    var cc = t.GetComponent<CharacterController>();
                                    if (cc != null) { _playerTransform = t; GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 通过相机缓存玩家 Transform: '{t.name}'"); break; }
                                    t = t.parent;
                                    depth++;
                                }
                            }
                            else if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogWarning("Refresh: Camera.main 为 null");
                        }
                        catch (Exception ex) { if (_refreshCount <= 3) GrannyModMenuPlugin.PluginLog.LogWarning($"Camera 查找异常: {ex.Message}"); }
                    }
                }
                // 找到玩家时记录一次日志
                if (wasPlayerNull && (_player != null || _playerTransform != null))
                    GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 找到玩家! FPSControllerNEW={_player != null}, Transform={(_playerTransform != null ? _playerTransform.position.ToString() : "null")}");
                else if (wasPlayerNull && _player == null && _playerTransform == null && _refreshCount <= 5)
                    GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh #{_refreshCount}: 玩家仍未找到");

                if (_granny == null) _granny = FindObjectOfTypeSafe<EnemyAIGranny>();
                if (_playerCaught == null) _playerCaught = FindObjectOfTypeSafe<playerCaught>();
                if (_nightmareMode == null) _nightmareMode = FindObjectOfTypeSafe<nightmareOnOff>();
                if (_gunShoot == null) _gunShoot = FindObjectOfTypeSafe<GunShoot>();
                if (_inventory == null)
                {
                    _inventory = FindObjectOfTypeSafe<InventoryController>();
                    // 备选: 在玩家对象子对象中查找 InventoryController
                    if (_inventory == null)
                    {
                        try
                        {
                            Transform searchRoot = _player != null ? _player.transform : (_playerTransform ?? null);
                            if (searchRoot == null && Camera.main != null) searchRoot = Camera.main.transform.root;
                            if (searchRoot != null)
                            {
                                var type = Il2CppType.Of<InventoryController>();
                                if (type != null)
                                {
                                    var found = searchRoot.GetComponentInChildren(type, true);
                                    if (found != null) _inventory = found.TryCast<InventoryController>();
                                }
                                if (_inventory == null && _refreshCount <= 5)
                                    GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 在 '{searchRoot.name}' 中未找到 InventoryController");
                            }
                        }
                        catch { }
                    }
                    if (_inventory != null)
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 找到 InventoryController");
                }
                if (_pickUp == null)
                {
                    _pickUp = FindObjectOfTypeSafe<PickUp>();
                    if (_pickUp != null)
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 找到 PickUp (拾取检测器)");
                }
                if (_flashlight == null)
                {
                    _flashlight = FindObjectOfTypeSafe<flashlightController>();
                    // 备选: 在玩家对象或相机上查找 flashlightController
                    if (_flashlight == null)
                    {
                        try
                        {
                            Transform searchRoot = _player != null ? _player.transform : (_playerTransform ?? null);
                            if (searchRoot == null && Camera.main != null) searchRoot = Camera.main.transform.root;
                            if (searchRoot != null)
                            {
                                var type = Il2CppType.Of<flashlightController>();
                                if (type != null)
                                {
                                    var found = searchRoot.GetComponentInChildren(type, true);
                                    if (found != null) _flashlight = found.TryCast<flashlightController>();
                                }
                                if (_flashlight == null && _refreshCount <= 5)
                                    GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 在 '{searchRoot.name}' 中未找到 flashlightController");
                            }
                        }
                        catch { }
                    }
                    if (_flashlight != null)
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 找到 flashlightController");
                }
                if (_checkCar == null) _checkCar = FindObjectOfTypeSafe<checkTheCar>();
                if (_checkExitDoor == null)
                {
                    _checkExitDoor = FindObjectOfTypeSafe<CheckExitDoor>();
                    if (_checkExitDoor != null)
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh: 找到 CheckExitDoor (持有 padlockCodePL1-5)");
                }
                if (_elevator == null) _elevator = FindObjectOfTypeSafe<elevatorController>();

                // FindObjectsOfType: 用非泛型方法 (每个类型单独写方法, 避免 T[] 返回类型)
                FindAllSpiders();
                FindAllCrows();
                FindAllRats();
                FindAllMomSpiders();
                FindAllMomCrawls();
                FindAllSantas();
                if (_pickupFlashlight == null) _pickupFlashlight = FindObjectOfTypeSafe<pickupFlashlight>();

                // 调试日志: 每 10 次刷新输出一次状态
                if (_refreshCount % 10 == 0)
                {
                    GrannyModMenuPlugin.PluginLog.LogInfo($"Refresh #{_refreshCount}: granny={_granny != null}, inv={_inventory != null}, flash={_flashlight != null}, spiders={_spiders?.Length ?? -1}, crows={_crows?.Length ?? -1}");
                }

                CacheOriginalValues();
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogError($"Refresh: {ex.Message}"); }
        }

        private void CacheOriginalValues()
        {
            if (_origValuesCached) return;
            try
            {
                if (_player != null)
                {
                    _origForwardSpeed = _player.forwardSpeed;
                    _origBackwardSpeed = _player.backwardSpeed;
                    _origSidestepSpeed = _player.sidestepSpeed;
                    _origJumpSpeed = _player.jumpSpeed;
                    _origInAirMultiplier = _player.inAirMultiplier;
                }
                if (_granny != null)
                {
                    _origGrannyWalkSpeed = _granny.walkSpeed;
                    _origGrannyFollowSpeed = _granny.grannysFollowSpeed;
                }
                if (_gunShoot != null)
                {
                    _origGunFireRate = _gunShoot.fireRate;
                    _origGunRange = _gunShoot.weaponRange;
                }
                if (_player != null || _granny != null || _gunShoot != null)
                    _origValuesCached = true;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Cache: {ex.Message}"); }
        }

        private void ApplyModState()
        {
            try
            {
                ApplyPlayerMods();
                ApplyGrannyMods();
                ApplySpiderMods();
                ApplyMomSpiderMods();
                ApplyMomCrawlMods();
                ApplySantaMods();
                ApplyCrowMods();
                ApplyRatMods();
                ApplyCaughtMods();
                ApplyGameMods();
                ApplyWeaponMods();
                ApplyFlashlightMods();
                ApplyCarMods();
                ApplyMapMods();
                ApplyNoClip();
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Apply: {ex.Message}"); }
        }

        // ===== Apply 方法 (略, 与之前相同) =====
        private void ApplyPlayerMods()
        {
            if (_player == null) return;
            try
            {
                if (ModState.PlayerSpeedEnabled)
                {
                    _player.forwardSpeed = _origForwardSpeed * ModState.PlayerSpeedMultiplier;
                    _player.backwardSpeed = _origBackwardSpeed * ModState.PlayerSpeedMultiplier;
                    _player.sidestepSpeed = _origSidestepSpeed * ModState.PlayerSpeedMultiplier;
                }
                else
                {
                    _player.forwardSpeed = _origForwardSpeed;
                    _player.backwardSpeed = _origBackwardSpeed;
                    _player.sidestepSpeed = _origSidestepSpeed;
                }
                _player.jumpSpeed = ModState.PlayerJumpBoost ? _origJumpSpeed * ModState.PlayerJumpMultiplier : _origJumpSpeed;
                if (ModState.PlayerAlwaysGrounded) _player.PlayerIsGrounded = true;
                if (ModState.PlayerNoFallDamage) { _player.fallTimerStarted = false; _player.timeInAir = 0f; }
                if (ModState.PlayerCrouch) _player.playerCrouch = true;
                _player.inAirMultiplier = ModState.PlayerInAirControl ? _origInAirMultiplier * 5f : _origInAirMultiplier;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Player: {ex.Message}"); }
        }

        private void ApplyCaughtMods()
        {
            if (ModState.GodMode || ModState.NeverGetCaught)
            {
                if (_playerCaught != null)
                {
                    try
                    {
                        _playerCaught.grannyTakePlayer = false;
                        _playerCaught.spiderBitePlayer = false;
                        _playerCaught.explodingPlayer = false;
                        _playerCaught.playerFallDead = false;
                        _playerCaught.momSpiderEat = false;
                    }
                    catch { }
                }
            }
            // GodMode 不修改 _granny 字段, 避免干扰 Granny AI (GrannyNoCatch 选项单独处理)
        }

        private void ApplyNoClip()
        {
            if (_player == null) return;
            try
            {
                var cc = _player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    bool want = !ModState.NoClip;
                    if (cc.enabled != want) cc.enabled = want;
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"NoClip: {ex.Message}"); }
        }

        private void ApplyFlyMode()
        {
            if (!ModState.FlyMode || _player == null) return;
            try
            {
                var t = _player.transform;
                Vector3 move = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) move += t.forward;
                if (Input.GetKey(KeyCode.S)) move -= t.forward;
                if (Input.GetKey(KeyCode.A)) move -= t.right;
                if (Input.GetKey(KeyCode.D)) move += t.right;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) move -= Vector3.up;
                if (move.sqrMagnitude > 0)
                    t.position += move.normalized * ModState.FlySpeed * Time.deltaTime;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Fly: {ex.Message}"); }
        }

        // ===== 玩家缩放 =====
        private void ApplyPlayerScale()
        {
            if (!ModState.PlayerScaleEnabled) return;
            Transform pt = GetPlayerTransform();
            if (pt == null) return;
            try
            {
                // IL2CPP 可能返回 Vector3.zero, 重置为 one
                Vector3 cur = pt.localScale;
                if (cur == Vector3.zero) cur = Vector3.one;
                Vector3 target = Vector3.one * ModState.PlayerScale;
                if (cur != target)
                    pt.localScale = target;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"PlayerScale: {ex.Message}"); }
        }

        // ===== 选中物体缩放 =====
        private void ApplySelectedObjectScale()
        {
            if (_selectedObject == null) return;
            try
            {
                Vector3 target = Vector3.one * ModState.SelectedObjectScale;
                Vector3 cur = _selectedObject.transform.localScale;
                if (cur == Vector3.zero) cur = Vector3.one;
                if (cur != target)
                    _selectedObject.transform.localScale = target;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"ObjScale: {ex.Message}"); }
        }

        // ===== 选中准星对准的物体 =====
        internal void TriggerSelectCrosshairObject()
        {
            Camera cam = null;
            try { cam = Camera.main; } catch { }
            if (cam == null) { _selectedInfo = "(无摄像机)"; return; }
            try
            {
                Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                // 使用 SphereCast (半径 0.5) 提高命中率
                if (UnityEngine.Physics.SphereCast(ray, 0.5f, out RaycastHit hit, 100f))
                {
                    _selectedObject = hit.collider?.gameObject;
                    if (_selectedObject != null)
                    {
                        _selectedOrigScale = _selectedObject.transform.localScale.x;
                        if (_selectedOrigScale == 0f) _selectedOrigScale = 1f;
                        ModState.SelectedObjectScale = _selectedOrigScale;
                        string parentName = _selectedObject.transform.parent != null ? _selectedObject.transform.parent.name : "null";
                        _selectedInfo = $"{_selectedObject.name} (父={parentName}, scale={_selectedOrigScale:F2})";
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Select: 选中 '{_selectedObject.name}' scale={_selectedOrigScale}");
                    }
                    else _selectedInfo = "(命中但物体为空)";
                }
                else _selectedInfo = "(未命中)";
            }
            catch (Exception ex) { _selectedInfo = $"(错误: {ex.Message})"; }
        }

        internal void TriggerClearSelection()
        {
            _selectedObject = null;
            _selectedInfo = "(未选中)";
            ModState.SelectedObjectScale = 1.0f;
        }

        // ===== 克隆选中的敌人 =====
        internal void TriggerCloneSelectedEnemy()
        {
            if (_selectedObject == null) { GrannyModMenuPlugin.PluginLog.LogWarning("Clone: 未选中物体"); return; }
            try
            {
                GameObject clone = UnityEngine.Object.Instantiate(_selectedObject, _selectedObject.transform.position + new Vector3(1, 0, 0), _selectedObject.transform.rotation);
                clone.name = _selectedObject.name + "_Clone";
                clone.transform.SetParent(null, true);
                GrannyModMenuPlugin.PluginLog.LogInfo($"Clone: 已克隆 '{_selectedObject.name}' -> '{clone.name}'");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"Clone: {ex.Message}"); }
        }

        private Transform GetPlayerTransform()
        {
            if (_player != null) return _player.transform;
            return _playerTransform;
        }

        // ===== 敌人缩放 =====
        private void ApplyEnemyScale<T>(T[] enemies, float scale) where T : Il2CppObjectBase
        {
            if (enemies == null) return;
            try
            {
                foreach (var e in enemies)
                {
                    if (e == null) continue;
                    try
                    {
                        var mb = e.TryCast<MonoBehaviour>();
                        if (mb != null)
                        {
                            Vector3 cur = mb.transform.localScale;
                            if (cur == Vector3.zero) cur = Vector3.one;
                            Vector3 target = Vector3.one * scale;
                            if (cur != target)
                                mb.transform.localScale = target;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void ApplyEnemyScaleSingle<T>(T enemy, float scale) where T : Il2CppObjectBase
        {
            if (enemy == null) return;
            try
            {
                var mb = enemy.TryCast<MonoBehaviour>();
                if (mb != null)
                {
                    Vector3 cur = mb.transform.localScale;
                    if (cur == Vector3.zero) cur = Vector3.one;
                    Vector3 target = Vector3.one * scale;
                    if (cur != target)
                        mb.transform.localScale = target;
                }
            }
            catch { }
        }

        // 应用所有敌人缩放 (仅在对应 Enabled=true 时应用, 避免干扰游戏原始 scale)
        private void ApplyAllEnemyScales()
        {
            if (ModState.GrannyScaleEnabled) ApplyEnemyScaleSingle(_granny, ModState.GrannyScale);
            if (ModState.SpiderScaleEnabled) ApplyEnemyScale(_spiders, ModState.SpiderScale);
            if (ModState.MomSpiderScaleEnabled) ApplyEnemyScale(_momSpiders, ModState.MomSpiderScale);
            if (ModState.MomCrawlScaleEnabled) ApplyEnemyScale(_momCrawls, ModState.MomCrawlScale);
            if (ModState.CrowScaleEnabled) ApplyEnemyScale(_crows, ModState.CrowScale);
            if (ModState.RatScaleEnabled) ApplyEnemyScale(_rats, ModState.RatScale);
            if (ModState.SantaScaleEnabled) ApplyEnemyScale(_santas, ModState.SantaScale);
        }

        // ===== 克隆指定敌人 =====
        internal void TriggerCloneEnemy<T>(T[] enemies, string typeName) where T : Il2CppObjectBase
        {
            if (enemies == null || enemies.Length == 0) { GrannyModMenuPlugin.PluginLog.LogWarning($"Clone: 无 {typeName}"); return; }
            try
            {
                var mb = enemies[0].TryCast<MonoBehaviour>();
                if (mb == null) return;
                GameObject clone = UnityEngine.Object.Instantiate(mb.gameObject, mb.transform.position + new Vector3(1, 0, 0), mb.transform.rotation);
                clone.name = mb.gameObject.name + "_Clone";
                clone.transform.SetParent(null, true);
                GrannyModMenuPlugin.PluginLog.LogInfo($"Clone: 已克隆 {typeName} '{mb.gameObject.name}' -> '{clone.name}'");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"Clone {typeName}: {ex.Message}"); }
        }

        internal void TriggerCloneGranny()
        {
            if (_granny == null) { GrannyModMenuPlugin.PluginLog.LogWarning("Clone: 无 Granny"); return; }
            try
            {
                GameObject clone = UnityEngine.Object.Instantiate(_granny.gameObject, _granny.transform.position + new Vector3(1, 0, 0), _granny.transform.rotation);
                clone.name = "Granny_Clone";
                clone.transform.SetParent(null, true);
                GrannyModMenuPlugin.PluginLog.LogInfo($"Clone: 已克隆 Granny -> '{clone.name}'");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"Clone Granny: {ex.Message}"); }
        }

        private void ApplyGrannyMods()
        {
            if (_granny == null) return;
            // 没有启用任何 Granny 选项时, 不修改任何字段 (避免干扰游戏原生 AI 逻辑)
            if (!ModState.GrannyFreezeEnabled && !ModState.GrannyBlind && !ModState.GrannyDeaf
                && !ModState.GrannyNoAttack && !ModState.GrannySpeedEnabled && !ModState.GrannyNoCatch)
                return;
            try
            {
                if (ModState.GrannyFreezeEnabled)
                {
                    _granny.freeze = true;
                    if (_granny.navComponent != null) _granny.navComponent.enabled = false;
                }
                if (ModState.GrannyBlind) { _granny.seePlayer = false; _granny.seePlayerTimer = false; }
                if (ModState.GrannyDeaf) { _granny.grannyHearPlayer = false; _granny.grannyHearObject = false; }
                if (ModState.GrannyNoAttack)
                {
                    _granny.dontHitPlayer = true; _granny.huntPlayer = false;
                    _granny.attackingPlayer = false; _granny.GrannyGonnaSmack = false;
                }
                if (ModState.GrannySpeedEnabled && _origValuesCached)
                {
                    _granny.walkSpeed = _origGrannyWalkSpeed * ModState.GrannySpeedMultiplier;
                    _granny.grannysFollowSpeed = _origGrannyFollowSpeed * ModState.GrannySpeedMultiplier;
                }
                if (ModState.GrannyNoCatch) _granny.playerGetCaught = false;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Granny: {ex.Message}"); }
        }

        private void ApplySpiderMods()
        {
            if (_spiders == null) return;
            try
            {
                foreach (var spider in _spiders)
                {
                    if (spider == null) continue;
                    try
                    {
                        if (ModState.SpiderFreeze) { spider.spiderDead = true; spider.huntPlayer = false; }
                        if (ModState.SpiderNoHunt) spider.huntPlayer = false;
                        if (ModState.SpiderNoBite) spider.SpiderBitePlayer = false;
                        if (ModState.SpiderNoCatch) spider.playerCaught = false;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Spider: {ex.Message}"); }
        }

        private void ApplyCrowMods()
        {
            if (_crows == null) return;
            try
            {
                foreach (var crow in _crows)
                {
                    if (crow == null) continue;
                    try
                    {
                        if (ModState.CrowFreeze) { crow.crowGetShoot = true; crow.isAttacking = false; }
                        if (ModState.CrowNoAttack) crow.isAttacking = false;
                        if (ModState.CrowNoSteal) crow.playerSteal = false;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Crow: {ex.Message}"); }
        }

        private void ApplyRatMods()
        {
            if (_rats == null) return;
            try
            {
                foreach (var rat in _rats)
                {
                    if (rat == null) continue;
                    try { if (ModState.RatFreeze) { rat.ratS = true; rat.ratR = false; } }
                    catch { }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Rat: {ex.Message}"); }
        }

        // ===== 下水道蜘蛛 granny (MomSpiderHead) =====
        private void ApplyMomSpiderMods()
        {
            if (_momSpiders == null) return;
            try
            {
                foreach (var mom in _momSpiders)
                {
                    if (mom == null) continue;
                    try
                    {
                        if (ModState.MomSpiderFreeze) { mom.Hunting = false; mom.getShot = true; }
                        if (ModState.MomSpiderBlind) mom.seePlayer = false;
                        if (ModState.MomSpiderNoCatch) mom.playerCaught = false;
                        if (ModState.MomSpiderEscape) mom.playerEscape = true;
                        // 上帝模式: 防止 SpiderMom 触发击杀动画 (卡在原地不动)
                        if (ModState.GodMode)
                        {
                            mom.playerCaught = false;
                            mom.playerEscape = true;
                        }
                        // 杀死 SpiderMom (解除无法被击杀的限制)
                        if (ModState.MomSpiderDead)
                        {
                            mom.Hunting = false;
                            mom.getShot = true;
                            mom.seePlayer = false;
                            mom.playerCaught = false;
                            // 禁用 NavMeshAgent 让它停止移动
                            try
                            {
                                var agent = mom.agent;
                                if (agent != null) agent.enabled = false;
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"MomSpider: {ex.Message}"); }
        }

        // ===== 妈妈爬行者 (momCrawl) =====
        private void ApplyMomCrawlMods()
        {
            if (_momCrawls == null) return;
            try
            {
                foreach (var crawl in _momCrawls)
                {
                    if (crawl == null) continue;
                    try { if (ModState.MomCrawlDead) crawl.momDead = true; }
                    catch { }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"MomCrawl: {ex.Message}"); }
        }

        // ===== 小圣诞老人 (LittleSantaController) =====
        private void ApplySantaMods()
        {
            if (_santas == null) return;
            try
            {
                foreach (var santa in _santas)
                {
                    if (santa == null) continue;
                    try { if (ModState.SantaStunned) santa.currentState = (LittleSantaController.AIState)3; /* Stunned */ }
                    catch { }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Santa: {ex.Message}"); }
        }

        // ===== 地图 / 光照 (全图变亮) =====
        private Light _fullBrightDirLight; // 大范围方向光 (覆盖整个场景)
        private Light _fullBrightPointLight; // 跟随玩家的点光源
        private bool _fullBrightLogOnce = false;
        private void ApplyMapMods()
        {
            try
            {
                // 关闭雾
                if (ModState.NoFog)
                {
                    RenderSettings.fog = false;
                }

                // 全图变亮
                if (ModState.FullBright)
                {
                    // 方案 A: 提升环境光 (Built-in RP 有效)
                    RenderSettings.ambientLight = Color.white;
                    RenderSettings.ambientIntensity = 4.0f;

                    // 方案 B: 创建大范围方向光 (覆盖整个场景, 模拟太阳光)
                    if (_fullBrightDirLight == null)
                    {
                        GameObject lightObj = new GameObject("FullBrightDirLight");
                        _fullBrightDirLight = lightObj.AddComponent<Light>();
                        _fullBrightDirLight.type = LightType.Directional;
                        _fullBrightDirLight.intensity = 2.0f;
                        _fullBrightDirLight.color = Color.white;
                        _fullBrightDirLight.shadows = LightShadows.None; // 无阴影, 性能好
                        // 方向光从上方照射
                        lightObj.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
                        if (!_fullBrightLogOnce) GrannyModMenuPlugin.PluginLog.LogInfo("Map: 创建 FullBrightDirLight 方向光 (intensity=2.0)");
                    }
                    _fullBrightDirLight.enabled = true;

                    // 方案 C: 通过 _flashlight 操作手电筒 Light
                    if (_flashlight != null && _flashlight.flashlightPlayer != null)
                    {
                        var light = _flashlight.flashlightPlayer.GetComponent<Light>();
                        if (light != null)
                        {
                            light.enabled = true;
                            light.intensity = 8f;
                            light.range = 100f;
                        }
                    }

                    // 方案 D: 启用 pickupFlashlight 的 lightOnPlayerDarker 光源
                    if (_pickupFlashlight != null)
                    {
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker1);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker2);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker3);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker4);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker5);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker6);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker7);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker8);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker9);
                        EnableLightDarker(_pickupFlashlight.lightOnPlayerDarker10);
                    }

                    // 方案 E: 在玩家/相机对象上查找所有 Light 组件并启用
                    Transform lightRoot = _player != null ? _player.transform : (_playerTransform ?? null);
                    if (lightRoot == null && Camera.main != null) lightRoot = Camera.main.transform.root;
                    if (lightRoot != null)
                    {
                        var lights = lightRoot.GetComponentsInChildren<Light>(true);
                        if (lights != null)
                        {
                            foreach (var light in lights)
                            {
                                if (light == null) continue;
                                light.enabled = true;
                                if (light.intensity < 5f) light.intensity = 5f;
                            }
                        }
                    }

                    // 方案 F: 创建跟随玩家的高强度点光源 (兜底)
                    if (_fullBrightPointLight == null)
                    {
                        GameObject lightObj = new GameObject("FullBrightPointLight");
                        _fullBrightPointLight = lightObj.AddComponent<Light>();
                        _fullBrightPointLight.type = LightType.Point;
                        _fullBrightPointLight.intensity = 5f;
                        _fullBrightPointLight.range = 50f;
                        _fullBrightPointLight.color = Color.white;
                        _fullBrightPointLight.shadows = LightShadows.None;
                        if (!_fullBrightLogOnce) GrannyModMenuPlugin.PluginLog.LogInfo("Map: 创建 FullBrightPointLight 点光源 (intensity=5, range=50)");
                    }
                    _fullBrightPointLight.enabled = true;
                    Transform follow = _player != null ? _player.transform : (_playerTransform ?? null);
                    if (follow != null)
                        _fullBrightPointLight.transform.position = follow.position + Vector3.up * 2f;

                    if (!_fullBrightLogOnce)
                    {
                        GrannyModMenuPlugin.PluginLog.LogInfo($"Map: 全图变亮已启用 (ambient=4.0, dirLight=2.0, pointLight=5.0)");
                        _fullBrightLogOnce = true;
                    }
                }
                else
                {
                    // 关闭时禁用光源
                    if (_fullBrightDirLight != null) _fullBrightDirLight.enabled = false;
                    if (_fullBrightPointLight != null) _fullBrightPointLight.enabled = false;
                }

                // 启用所有场景光源
                if (ModState.AllLightsOn)
                {
                    _allLights = UnityEngine.Object.FindObjectsOfType<Light>();
                    if (_allLights != null)
                    {
                        foreach (var light in _allLights)
                        {
                            if (light != null) light.enabled = true;
                        }
                    }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogError($"Map: {ex.Message}\n{ex.StackTrace}"); }
        }

        private void EnableLightDarker(GameObject obj)
        {
            if (obj == null) return;
            obj.SetActive(true);
            var light = obj.GetComponent<Light>();
            if (light != null) light.enabled = true;
        }

        private void ApplyGameMods()
        {
            try
            {
                if (_player != null)
                {
                    if (ModState.ForceDay2) _player.day2 = true;
                    if (ModState.ForceDay3) _player.day3 = true;
                }
                if (_granny != null && ModState.ForceEscaped)
                {
                    _granny.PlayerEscaped = true; _granny.playerGetCaught = false;
                }
                if (_nightmareMode != null && ModState.NightmareMode)
                    _nightmareMode.NightmareOnOff = true;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Game: {ex.Message}"); }
        }

        private void ApplyWeaponMods()
        {
            if (_gunShoot == null) return;
            try
            {
                _gunShoot.fireRate = ModState.GunRapidFire ? 0.05f : _origGunFireRate;
                _gunShoot.weaponRange = ModState.GunInfiniteRange ? 1000f : _origGunRange;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogInfo($"Weapon: {ex.Message}"); }
        }

        private void ApplyFlashlightMods()
        {
            if (_flashlight != null && ModState.PlayerFlashlight)
            {
                try { _flashlight.playerHaveFlashlight = true; } catch { }
            }
        }

        private void ApplyCarMods()
        {
            if (_checkCar == null || !ModState.CarReadyAll) return;
            try
            {
                _checkCar.batteryOK = true; _checkCar.topplockOK = true;
                _checkCar.sparkplugOK = true; _checkCar.fuelOK = true;
                _checkCar.playerHaveCarKey = true;
            }
            catch { }
        }

        // ===== Trigger 方法 =====
        internal void TriggerGrannyKnockout() { try { _granny?.grannyHitByGun(); } catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"grannyHitByGun: {ex.Message}"); } }
        internal void TriggerGrannyPepper() { try { _granny?.grannyHitByPepper(); } catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"grannyHitByPepper: {ex.Message}"); } }
        internal void TriggerGrannyHitByCar() { try { _granny?.grannyHitByCar(); } catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"grannyHitByCar: {ex.Message}"); } }
        internal void TriggerSpiderKillAll()
        {
            if (_spiders == null) return;
            foreach (var s in _spiders) { if (s != null) try { s.spiderIsDead(); } catch { } }
        }
        internal void TriggerStartCar()
        {
            if (_checkCar == null) return;
            try
            {
                _checkCar.batteryOK = true; _checkCar.topplockOK = true; _checkCar.sparkplugOK = true;
                _checkCar.fuelOK = true; _checkCar.playerHaveCarKey = true; _checkCar.startCar();
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"startCar: {ex.Message}"); }
        }
        internal void TriggerElevatorCall() { try { _elevator?.CallElevatorDown(); } catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"CallElevatorDown: {ex.Message}"); } }
        internal void TriggerElevatorGo() { try { _elevator?.ElevatorGo(); } catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"ElevatorGo: {ex.Message}"); } }
        internal void TriggerElevatorDoors() { try { _elevator?.DoorsCloseOpen(); } catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"DoorsCloseOpen: {ex.Message}"); } }

        // ===== 物品生成 + 拾取系统 =====
        // 生成的物品列表 (用于距离检测拾取)
        private System.Collections.Generic.List<SpawnedItem> _spawnedItems = new System.Collections.Generic.List<SpawnedItem>();

        // 物品 ID 枚举 (对应 InventoryController 的 have* 字段)
        private enum ItemId
        {
            None, exitKey, safeKey, hanglockKey, padlockCode, weaponKey, playhouseKey, carKey,
            specialkey, rustyPadlockKey, spiderKey, oldShotgun, gunDel1, gunDel2, gunDel3,
            topplock, ammo, armborst, arrow, pepperspray, freezeTrap,
            avbitare, hammare, screwdriver, planka, battery, wrench, chainCutter, wheelCrank, woodenStick,
            carbattery, gascan, sparkplug,
            tb1, tb2, tb3, tb4, vas, vas2, melon, teddy, kugg1, kugg2,
            message, brunnsvev, meat, book, remote, birdSeed, deadRat, christmasBall, christmasKulaBomb
        }

        private class SpawnedItem
        {
            public GameObject obj;
            public ItemId itemId;
            public string displayName;
        }

        // 设置 InventoryController 的 have* 字段, 同时把对应的 new* (玩家手上的物品) SetActive
        // 这样玩家立即获得物品并显示在手上, 无需依赖拾取事件触发
        private void SetItemHave(ItemId id, bool value)
        {
            if (_inventory == null) return;
            try
            {
                switch (id)
                {
                    case ItemId.exitKey: _inventory.haveexitKey = value; SetNewActive(_inventory.newexitKey, value); break;
                    case ItemId.safeKey: _inventory.havesafeKey = value; SetNewActive(_inventory.newsafeKey, value); break;
                    case ItemId.hanglockKey: _inventory.havehanglockKey = value; SetNewActive(_inventory.newhanglockKey, value); break;
                    case ItemId.padlockCode: _inventory.havepadlockCode = value; SetNewActive(_inventory.newpadlockCode, value); break;
                    case ItemId.weaponKey: _inventory.haveweaponKey = value; SetNewActive(_inventory.newweaponKey, value); break;
                    case ItemId.playhouseKey: _inventory.haveplayhouseKey = value; SetNewActive(_inventory.newplayhouseKey, value); break;
                    case ItemId.carKey: _inventory.havecarKey = value; SetNewActive(_inventory.newcarKey, value); break;
                    case ItemId.specialkey: _inventory.havespecialkey = value; SetNewActive(_inventory.newspecialkey, value); break;
                    case ItemId.rustyPadlockKey: _inventory.haverustyPadlockKey = value; SetNewActive(_inventory.newrustyPadlockKey, value); break;
                    case ItemId.spiderKey: _inventory.havespiderKey = value; SetNewActive(_inventory.newspiderKey, value); break;
                    case ItemId.oldShotgun: _inventory.haveoldShotgun = value; SetNewActive(_inventory.newoldShotgun, value); break;
                    case ItemId.gunDel1: _inventory.havegunDel1 = value; SetNewActive(_inventory.newgunDel1, value); break;
                    case ItemId.gunDel2: _inventory.havegunDel2 = value; SetNewActive(_inventory.newgunDel2, value); break;
                    case ItemId.gunDel3: _inventory.havegunDel3 = value; SetNewActive(_inventory.newgunDel3, value); break;
                    case ItemId.topplock: _inventory.havetopplock = value; SetNewActive(_inventory.newtopplock, value); break;
                    case ItemId.ammo: break; // ammo 没有 have* / new* 字段, 只生成物品
                    case ItemId.armborst: _inventory.havearmborst = value; SetNewActive(_inventory.newarmborst, value); break;
                    case ItemId.arrow: _inventory.haveArrow = value; SetNewActive(_inventory.newArrow, value); break;
                    case ItemId.pepperspray: _inventory.havepepperspray = value; SetNewActive(_inventory.newpepperspray, value); break;
                    case ItemId.freezeTrap: _inventory.havefreezeTrap = value; SetNewActive(_inventory.newfreezeTrap, value); break;
                    case ItemId.avbitare: _inventory.haveAvbitare = value; SetNewActive(_inventory.newAvbitare, value); break;
                    case ItemId.hammare: _inventory.haveHammare = value; SetNewActive(_inventory.newHammare, value); break;
                    case ItemId.screwdriver: _inventory.havescrewdriver = value; SetNewActive(_inventory.newscrewdriver, value); break;
                    case ItemId.planka: _inventory.haveplanka = value; SetNewActive(_inventory.newplanka, value); break;
                    case ItemId.battery: _inventory.havebattery = value; SetNewActive(_inventory.newbattery, value); break;
                    case ItemId.wrench: _inventory.havewrench = value; SetNewActive(_inventory.newwrench, value); break;
                    case ItemId.chainCutter: _inventory.havechainCutter = value; SetNewActive(_inventory.newchainCutter, value); break;
                    case ItemId.wheelCrank: _inventory.havewheelCrank = value; SetNewActive(_inventory.newwheelCrank, value); break;
                    case ItemId.woodenStick: _inventory.havewoodenStick = value; SetNewActive(_inventory.newwoodenStick, value); break;
                    case ItemId.carbattery: _inventory.havecarbattery = value; SetNewActive(_inventory.newcarbattery, value); break;
                    case ItemId.gascan: _inventory.havegascan = value; SetNewActive(_inventory.newgascan, value); break;
                    case ItemId.sparkplug: _inventory.havesparkplug = value; SetNewActive(_inventory.newsparkplug, value); break;
                    case ItemId.tb1: _inventory.havetb1 = value; SetNewActive(_inventory.newtb1, value); break;
                    case ItemId.tb2: _inventory.havetb2 = value; SetNewActive(_inventory.newtb2, value); break;
                    case ItemId.tb3: _inventory.havetb3 = value; SetNewActive(_inventory.newtb3, value); break;
                    case ItemId.tb4: _inventory.havetb4 = value; SetNewActive(_inventory.newtb4, value); break;
                    case ItemId.vas: _inventory.havevas = value; SetNewActive(_inventory.newvas, value); break;
                    case ItemId.vas2: _inventory.havevas2 = value; SetNewActive(_inventory.newvas2, value); break;
                    case ItemId.melon: _inventory.havemelon = value; SetNewActive(_inventory.newmelon, value); break;
                    case ItemId.teddy: _inventory.haveteddy = value; SetNewActive(_inventory.newteddy, value); break;
                    case ItemId.kugg1: _inventory.havekugg1 = value; SetNewActive(_inventory.newkugg1, value); break;
                    case ItemId.kugg2: _inventory.havekugg2 = value; SetNewActive(_inventory.newkugg2, value); break;
                    case ItemId.message: _inventory.havemessage = value; SetNewActive(_inventory.newmessage, value); break;
                    case ItemId.brunnsvev: _inventory.havebrunnsvev = value; SetNewActive(_inventory.newbrunnsvev, value); break;
                    case ItemId.meat: _inventory.havemeat = value; SetNewActive(_inventory.newmeat, value); break;
                    case ItemId.book: _inventory.havebook = value; SetNewActive(_inventory.newbook, value); break;
                    case ItemId.remote: _inventory.haveremote = value; SetNewActive(_inventory.newremote, value); break;
                    case ItemId.birdSeed: _inventory.havebirdSeed = value; SetNewActive(_inventory.newbirdSeed, value); break;
                    case ItemId.deadRat: _inventory.havedeadRat = value; SetNewActive(_inventory.newdeadRat, value); break;
                    case ItemId.christmasBall: _inventory.havechristmasBall = value; SetNewActive(_inventory.newchristmasBall, value); break;
                    case ItemId.christmasKulaBomb: _inventory.havechristmasKulaBomb = value; SetNewActive(_inventory.newchristmasKulaBomb, value); break;
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"SetItemHave({id}): {ex.Message}"); }
        }

        // 安全地把 new* Transform 的 GameObject SetActive
        private void SetNewActive(Transform newTransform, bool active)
        {
            if (newTransform == null)
            {
                GrannyModMenuPlugin.PluginLog.LogWarning($"  SetNewActive: newTransform 为 null, 跳过 SetActive");
                return;
            }
            try
            {
                GameObject go = newTransform.gameObject;
                if (go == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning($"  SetNewActive: gameObject 为 null");
                    return;
                }
                GrannyModMenuPlugin.PluginLog.LogInfo($"  SetNewActive: {go.name} (active={go.activeSelf}) -> {active}");
                go.SetActive(active);
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"  SetNewActive: {ex.Message}"); }
        }

        // 检测玩家与生成物品的距离, 按E键拾取 (距离<3米且按E)
        private void CheckItemPickup()
        {
            if (_spawnedItems.Count == 0 || _inventory == null) return;
            Transform playerT = _player != null ? _player.transform : _playerTransform;
            if (playerT == null) return;

            bool ePressed = false;
            try { ePressed = Input.GetKeyDown(KeyCode.E); } catch { }

            for (int i = _spawnedItems.Count - 1; i >= 0; i--)
            {
                var item = _spawnedItems[i];
                if (item.obj == null) { _spawnedItems.RemoveAt(i); continue; }
                try
                {
                    float dist = Vector3.Distance(playerT.position, item.obj.transform.position);
                    // 距离<3米且按E键才拾取
                    if (dist < 3.0f && ePressed)
                    {
                        // 拾取: 设置 have* 字段为 true 并销毁对象
                        SetItemHave(item.itemId, true);
                        GrannyModMenuPlugin.PluginLog.LogInfo($"拾取: {item.displayName} (距离={dist:F2}m, 按E), 已设置 have{item.itemId}=true");
                        UnityEngine.Object.Destroy(item.obj);
                        _spawnedItems.RemoveAt(i);
                    }
                }
                catch { }
            }
        }

        // 生成物品: 第一次点击传送真物品到玩家前方, 之后点击复制
        // 真物品带正确 layer 和组件, 添加 Collider 后可被 PickUp 射线检测命中
        private void SpawnItem(GameObject prefab, ItemId itemId, string displayName)
        {
            try
            {
                if (_pickUp == null) RefreshReferences();
                if (_pickUp == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning($"SpawnItem 失败: PickUp 未找到 ({displayName})");
                    return;
                }

                // 从 PickUp 实例获取地上的真物品
                GameObject groundItem = null;
                // 特殊: padlockCode 的真地上物品在 CheckExitDoor 中 (不在 PickUp 中), 优先从那里获取
                if (itemId == ItemId.padlockCode && _checkExitDoor != null)
                {
                    groundItem = GetPadlockCodeFromCheckExitDoor();
                    if (groundItem != null)
                        GrannyModMenuPlugin.PluginLog.LogInfo($"SpawnItem: 从 CheckExitDoor 获取 padlockCode 真地上物品 '{groundItem.name}'");
                }
                // 其他物品从 PickUp 字段获取
                if (groundItem == null)
                    groundItem = GetPickUpItem(itemId);
                if (groundItem == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning($"SpawnItem 失败: PickUp 上无 {itemId} 字段或为 null ({displayName})");
                    return;
                }

                if (_player == null && _playerTransform == null) RefreshReferences();

                // 获取玩家位置和朝向
                Vector3 playerPos;
                Vector3 forward;
                Camera cam = Camera.main;
                if (cam != null)
                {
                    playerPos = cam.transform.position;
                    forward = cam.transform.forward;
                }
                else if (_playerTransform != null)
                {
                    playerPos = _playerTransform.position;
                    forward = _playerTransform.forward;
                }
                else
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning("SpawnItem 失败: 找不到 Camera 和 Player");
                    return;
                }

                Vector3 spawnPos = playerPos + forward * 2f + Vector3.up * 0.5f;

                // 判断是否找到真正的地上物品 (而非回退到 PickUp 字段引用的手上物品)
                bool isRealGroundItem = groundItem != null && !groundItem.name.EndsWith("Hand", System.StringComparison.OrdinalIgnoreCase);

                GameObject spawnedObj;
                if (isRealGroundItem)
                {
                    // 真正的地上物品: 直接传送 (它已有正确的 scale, layer, Collider, 脚本)
                    groundItem.transform.SetParent(null, true);
                    groundItem.transform.position = spawnPos;
                    groundItem.SetActive(true);
                    EnsureRigidbody(groundItem);
                    spawnedObj = groundItem;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"SpawnItem: 已传送真地上物品 [{groundItem.name}] @ {spawnPos}");
                }
                else
                {
                    // 回退: 是手上物品, 创建全新的 GameObject 并复制 mesh
                    // 关键: HanglockKeyHand 的 transform.localScale 在 IL2CPP 中读取始终返回 0
                    // 游戏也读到 0, 认为物品是隐藏的, 所以无法拾取
                    // 方案: 创建全新 GameObject, 新对象的 localScale 能正常工作
                    // 对于 spiderKey, 优先用 Instantiate (保留所有组件, 包括脚本)
                    // spiderKey 没有 PL 版本, CreateItemFromMesh 创建的新对象缺少脚本组件, 游戏可能无法识别
                    bool useInstantiate = (itemId == ItemId.spiderKey);
                    if (useInstantiate)
                    {
                        // spiderKey: 用 Instantiate 创建 clone 放到地上, 不传送原始手上物品
                        // 原始 SpiderMomKeyHand 在 HandHoldObjects 下, 是 PickUp.spiderKey 指向的手上物品
                        // 传送它会破坏 PickUp.spiderKey 的引用, 导致扔出失败
                        // 用 clone 放到地上, 让游戏通过 tag='spiderkey' 判断 isSpiderKey
                        spawnedObj = UnityEngine.Object.Instantiate(groundItem, spawnPos, Quaternion.identity);
                        spawnedObj.transform.SetParent(null, true);
                        spawnedObj.SetActive(true);
                        ScaleHandItem(spawnedObj);
                        EnsureCollider(spawnedObj);
                        EnsureRigidbody(spawnedObj);
                        SetLayerRecursive(spawnedObj, 0);
                        // spiderKey 的 tag='spiderkey' (已确认在 TagManager 中存在)
                        try
                        {
                            string beforeTag = spawnedObj.tag;
                            spawnedObj.tag = "spiderkey";
                            string afterTag = spawnedObj.tag;
                            GrannyModMenuPlugin.PluginLog.LogInfo($"  SpawnItem(spiderKey): tag 设置 before='{beforeTag}', after='{afterTag}'");
                        }
                        catch (Exception tagEx)
                        {
                            GrannyModMenuPlugin.PluginLog.LogWarning($"  SpawnItem(spiderKey): 设置 tag='spiderkey' 失败: {tagEx.Message}");
                        }
                        GrannyModMenuPlugin.PluginLog.LogInfo($"SpawnItem: 已创建 spiderKey clone [{spawnedObj.name}] @ {spawnPos}, tag={spawnedObj.tag}, layer=0(Default)");
                    }
                    else
                    {
                        spawnedObj = CreateItemFromMesh(groundItem, spawnPos);
                        if (spawnedObj == null)
                        {
                            // 创建失败, 回退到 Instantiate 方案
                            GrannyModMenuPlugin.PluginLog.LogWarning("SpawnItem: CreateItemFromMesh 失败, 回退到 Instantiate");
                            spawnedObj = UnityEngine.Object.Instantiate(groundItem, spawnPos, Quaternion.identity);
                            spawnedObj.transform.SetParent(null, true);
                            spawnedObj.SetActive(true);
                            ScaleHandItem(spawnedObj);
                            EnsureCollider(spawnedObj);
                            EnsureRigidbody(spawnedObj);
                            SetLayerRecursive(spawnedObj, 0);
                        }
                    }
                    // 关键: 修改 PickUp 字段引用指向新对象
                    // 当 tag 不在 TagManager 中时 (如 spidermomkey), 游戏可能用对象引用比较识别物品
                    // 把 PickUp.spiderKey 等字段指向我们的新对象, 让游戏认为它就是可拾取的物品
                    SetPickUpField(itemId, spawnedObj);
                    GrannyModMenuPlugin.PluginLog.LogInfo($"SpawnItem: 已创建新物品 [{groundItem.name}] @ {spawnPos}, tag={spawnedObj.tag}, layer=0(Default)");
                }
                // 添加到拾取检测列表 (CheckItemPickup 会检测玩家距离, 靠近时自动拾取)
                _spawnedItems.Add(new SpawnedItem { obj = spawnedObj, itemId = itemId, displayName = displayName });
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogError($"Spawn: {ex.Message}\n{ex.StackTrace}"); }
        }

        // 放大手上物品: 手上物品 mesh 很大 (第一人称视角, mesh 本身 100+ 米), 原始 localScale 很小
        // 正确方案: 基于 mesh bounds 计算让最终世界大小约 0.3 米, scale = targetSize / meshMaxDim
        private void ScaleHandItem(GameObject obj)
        {
            if (obj == null) return;
            try
            {
                // 找子对象的 MeshFilter, 用 sharedMesh.bounds (模型本地大小, 不受 scale 影响)
                var mfs = obj.GetComponentsInChildren<MeshFilter>(true);
                Vector3 meshSize = Vector3.zero;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (mfs != null && mfs.Length > 0)
                {
                    foreach (var mf in mfs)
                    {
                        if (mf == null) continue;
                        try
                        {
                            var mesh = mf.sharedMesh;
                            if (mesh == null) mesh = mf.mesh;
                            if (mesh == null) continue;
                            Vector3 s = mesh.bounds.size;
                            sb.Append($"[{mf.name}: {s}] ");
                            // 取最大的非零 mesh bounds (手上物品 mesh 很大, 我们要用它计算 scale)
                            if (s.x > 0.001f && s.y > 0.001f && s.z > 0.001f)
                            {
                                if (meshSize == Vector3.zero || s.magnitude > meshSize.magnitude)
                                {
                                    meshSize = s;
                                }
                            }
                        }
                        catch { }
                    }
                }
                GrannyModMenuPlugin.PluginLog.LogInfo($"  ScaleHandItem: {obj.name} meshBounds: {sb}");
                // 计算让世界大小约为 0.3 米的 scale
                float targetSize = 0.3f;
                float maxDim = Mathf.Max(meshSize.x, meshSize.y, meshSize.z);
                if (maxDim > 0.001f)
                {
                    float scaleFactor = targetSize / maxDim;
                    obj.transform.localScale = Vector3.one * scaleFactor;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  ScaleHandItem: {obj.name} meshSize={meshSize} maxDim={maxDim} -> scale={scaleFactor} (targetSize={targetSize})");
                }
                else
                {
                    // 无法计算, 用固定小 scale
                    obj.transform.localScale = Vector3.one * 0.003f;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  ScaleHandItem: {obj.name} 无法计算 mesh bounds, 用 scale=0.003");
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"ScaleHandItem({obj.name}): {ex.Message}"); }
        }

        // 确保物品有 Collider (PickUp 射线检测需要 Collider 才能命中)
        // 关键: BoxCollider 的世界大小 = size * scale, 必须合理 (约 0.5 米)
        // 物品 scale 很小 (如 0.00274), 所以 size 要很大 (如 182)
        private void EnsureCollider(GameObject obj)
        {
            if (obj == null) return;
            try
            {
                // 已有 Collider, 直接用
                Collider col = obj.GetComponent<Collider>();
                if (col != null)
                {
                    col.isTrigger = false;
                    return;
                }
                // 子对象上找
                Collider[] childCols = obj.GetComponentsInChildren<Collider>(true);
                if (childCols != null && childCols.Length > 0)
                {
                    foreach (var c in childCols)
                    {
                        try { if (c != null) c.isTrigger = false; } catch { }
                    }
                    return;
                }
                // 没有 Collider, 添加 BoxCollider
                // 目标: 世界大小约 0.5 米 (足够大, 让 PickUp 射线容易命中)
                // BoxCollider.size 是本地坐标, 世界大小 = size * lossyScale
                // size = 0.5 / lossyScale
                Vector3 lossyScale = obj.transform.lossyScale;
                Vector3 targetWorldSize = new Vector3(0.5f, 0.5f, 0.5f);
                Vector3 boxSize;
                if (Mathf.Abs(lossyScale.x) > 0.0001f && Mathf.Abs(lossyScale.y) > 0.0001f && Mathf.Abs(lossyScale.z) > 0.0001f)
                {
                    boxSize = new Vector3(
                        targetWorldSize.x / lossyScale.x,
                        targetWorldSize.y / lossyScale.y,
                        targetWorldSize.z / lossyScale.z
                    );
                }
                else
                {
                    boxSize = new Vector3(0.5f, 0.5f, 0.5f);
                }
                var bc = obj.AddComponent<BoxCollider>();
                bc.size = boxSize;
                bc.isTrigger = false;
                // 输出实际世界大小用于诊断
                Vector3 worldSize = new Vector3(boxSize.x * lossyScale.x, boxSize.y * lossyScale.y, boxSize.z * lossyScale.z);
                GrannyModMenuPlugin.PluginLog.LogInfo($"  EnsureCollider: 为 {obj.name} 添加 BoxCollider (size={boxSize}, worldSize={worldSize})");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"EnsureCollider({obj.name}): {ex.Message}"); }
        }

        // 确保物品有 Rigidbody (让物品掉到地上, 避免悬浮)
        private void EnsureRigidbody(GameObject obj)
        {
            if (obj == null) return;
            try
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = obj.AddComponent<Rigidbody>();
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"EnsureRigidbody({obj.name}): {ex.Message}"); }
        }

        // 根据 ItemId 查找场景中真正的地上物品 (而非 _pickUp.* 引用的手上物品)
        // 关键发现: _pickUp.hanglockKey 等字段引用的是 "HanglockKeyHand" (手上物品, layer=13, mesh 109m)
        // 真正的地上物品命名为 "HanglockKeyPL1/PL2/PL4" 或 "Wrench_PL2" (layer=0, parent=RandomPlace*)
        // 这些地上物品的 localScale 是正常值 (非0), 可被游戏 PickUp 识别为可拾取
        // 此方法通过名称搜索场景中以 PL/Pl 结尾的地上物品
        private GameObject GetPickUpItem(ItemId id)
        {
            // 获取该 ItemId 对应的物品名称关键字 (用于场景搜索)
            string nameKey = GetItemNameKey(id);
            if (string.IsNullOrEmpty(nameKey)) return null;
            try
            {
                // 搜索场景中所有 Transform, 查找名称匹配的地上物品
                var type = Il2CppType.Of<Transform>();
                if (type == null) return null;
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null) return null;
                // 第一轮: 精确匹配 (去掉 PL 后缀后等于 nameKey)
                for (int i = 0; i < objs.Length; i++)
                {
                    try
                    {
                        if (objs[i] == null) continue;
                        var t = objs[i].TryCast<Transform>();
                        if (t == null) continue;
                        string name = t.name;
                        if (name == null) continue;
                        // 排除手上物品 (名称含 "Hand")
                        if (name.Contains("Hand", System.StringComparison.OrdinalIgnoreCase)) continue;
                        // 提取去后缀的名称
                        string baseName = StripGroundSuffix(name);
                        if (string.IsNullOrEmpty(baseName)) continue;
                        // 精确匹配 (忽略大小写)
                        if (baseName.Equals(nameKey, System.StringComparison.OrdinalIgnoreCase))
                        {
                            GrannyModMenuPlugin.PluginLog.LogInfo($"  GetPickUpItem: 找到地上物品 '{name}' (精确匹配 key={nameKey})");
                            return t.gameObject;
                        }
                    }
                    catch { }
                }
                // 第二轮: 包含匹配 (回退, 如 nameKey="Shotgun" 匹配 "ShotgunHandP1" 但这已被排除)
                for (int i = 0; i < objs.Length; i++)
                {
                    try
                    {
                        if (objs[i] == null) continue;
                        var t = objs[i].TryCast<Transform>();
                        if (t == null) continue;
                        string name = t.name;
                        if (name == null) continue;
                        if (name.Contains("Hand", System.StringComparison.OrdinalIgnoreCase)) continue;
                        string baseName = StripGroundSuffix(name);
                        if (string.IsNullOrEmpty(baseName)) continue;
                        // 包含匹配 (如 nameKey="CarBattery" 匹配 "CarBattery")
                        // 关键: 加入长度限制, 避免 "playHouseKey" 包含 "HouseKey" 的错误匹配
                        // 要求 baseName 长度不超过 nameKey + 3 (允许少量后缀, 如数字)
                        if (baseName.Contains(nameKey, System.StringComparison.OrdinalIgnoreCase)
                            && baseName.Length <= nameKey.Length + 3)
                        {
                            GrannyModMenuPlugin.PluginLog.LogInfo($"  GetPickUpItem: 找到地上物品 '{name}' (包含匹配 key={nameKey})");
                            return t.gameObject;
                        }
                    }
                    catch { }
                }
                GrannyModMenuPlugin.PluginLog.LogWarning($"  GetPickUpItem: 未找到地上物品 (key={nameKey}), 回退到 PickUp 字段引用");
                // 回退: 用 PickUp 字段引用 (可能不是真正的地上物品)
                return GetPickUpItemFallback(id);
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"GetPickUpItem({id}): {ex.Message}"); }
            return null;
        }

        // 获取 ItemId 对应的物品名称关键字 (用于场景搜索)
        private string GetItemNameKey(ItemId id)
        {
            // 返回物品在场景中的实际名称关键字 (用于搜索真地上物品)
            // 注意: 这不是代码字段名, 而是物品 GameObject 的实际名称
            switch (id)
            {
                case ItemId.exitKey: return "HouseKey"; // HouseKeyPL*, 不是 exitKey
                case ItemId.safeKey: return "SafeKey";
                case ItemId.hanglockKey: return "HanglockKey";
                case ItemId.padlockCode: return "padlockCode"; // 真地上物品命名: padlockCodePL1-5 (在 CheckExitDoor 类中)
                case ItemId.weaponKey: return "Vapennyckel"; // 瑞典语: 武器钥匙, tag='weaponkey'
                case ItemId.playhouseKey: return "playHouseKey";
                case ItemId.carKey: return "Carkey";
                case ItemId.specialkey: return "SpecialKey";
                case ItemId.rustyPadlockKey: return "RustyPadlockKey";
                case ItemId.spiderKey: return "SpiderMomKey"; // 无 PL 版本, 会回退到 CreateItemFromMesh
                case ItemId.oldShotgun: return "OldShotgun";
                case ItemId.gunDel1: return "Shotgun"; // ShotgunHandP1, 可能无 PL 版本
                case ItemId.gunDel2: return "Shotgun";
                case ItemId.gunDel3: return "Shotgun";
                case ItemId.topplock: return "Topplock";
                case ItemId.ammo: return "Ammo";
                case ItemId.armborst: return "Armborst"; // 弩
                case ItemId.pepperspray: return "PepperSpray";
                case ItemId.freezeTrap: return "FreezeTrap";
                case ItemId.avbitare: return "Avbitare";
                case ItemId.hammare: return "Hammer"; // 代码字段是 hammare, 物品名是 Hammer
                case ItemId.screwdriver: return "Screwdriver";
                case ItemId.planka: return "Planka";
                case ItemId.battery: return "CarBattery";
                case ItemId.wrench: return "Wrench";
                case ItemId.chainCutter: return "ChainCutter";
                case ItemId.wheelCrank: return "WheelCrank";
                case ItemId.woodenStick: return "WoodenStick";
                case ItemId.carbattery: return "CarBattery";
                case ItemId.gascan: return "Gascan";
                case ItemId.sparkplug: return "SparkPlug";
                case ItemId.vas: return "Vas";
                case ItemId.vas2: return "Vas2";
                case ItemId.melon: return "Melon";
                case ItemId.teddy: return "Teddy";
                case ItemId.kugg1: return "Kugg1";
                case ItemId.kugg2: return "Kugg2";
                case ItemId.message: return "Message";
                case ItemId.brunnsvev: return "Brunnsvev";
                case ItemId.meat: return "Meat";
                case ItemId.book: return "Book";
                case ItemId.remote: return "Remote";
                case ItemId.birdSeed: return "BirdSeed";
                case ItemId.deadRat: return "DeadRat";
                case ItemId.christmasBall: return "ChristmasKula";
                case ItemId.christmasKulaBomb: return "ChristmasKulaBomb";
                default: return null;
            }
        }

        // 回退方案: 用 PickUp 字段引用 (可能不是真正的地上物品)
        private GameObject GetPickUpItemFallback(ItemId id)
        {
            if (_pickUp == null) return null;
            try
            {
                switch (id)
                {
                    case ItemId.exitKey: return _pickUp.exitKey;
                    case ItemId.safeKey: return _pickUp.safeKey;
                    case ItemId.hanglockKey: return _pickUp.hanglockKey;
                    case ItemId.padlockCode: return _pickUp.padlockCode;
                    case ItemId.weaponKey: return _pickUp.weaponKey;
                    case ItemId.playhouseKey: return _pickUp.playhouseKey;
                    case ItemId.carKey: return _pickUp.carKey;
                    case ItemId.specialkey: return _pickUp.specialkey;
                    case ItemId.rustyPadlockKey: return _pickUp.rustyPadlockKey;
                    case ItemId.spiderKey: return _pickUp.spiderKey;
                    case ItemId.oldShotgun: return _pickUp.oldShotgun;
                    case ItemId.gunDel1: return _pickUp.gunDel1;
                    case ItemId.gunDel2: return _pickUp.gunDel2;
                    case ItemId.gunDel3: return _pickUp.gunDel3;
                    case ItemId.topplock: return _pickUp.topplock;
                    case ItemId.ammo: return _pickUp.ammo;
                    case ItemId.armborst: return _pickUp.armborst;
                    case ItemId.pepperspray: return _pickUp.pepperspray;
                    case ItemId.freezeTrap: return _pickUp.freezeTrap;
                    case ItemId.avbitare: return _pickUp.avbitare;
                    case ItemId.hammare: return _pickUp.hammare;
                    case ItemId.screwdriver: return _pickUp.screwdriver;
                    case ItemId.planka: return _pickUp.planka;
                    case ItemId.battery: return _pickUp.battery;
                    case ItemId.wrench: return _pickUp.wrench;
                    case ItemId.chainCutter: return _pickUp.chainCutter;
                    case ItemId.wheelCrank: return _pickUp.wheelCrank;
                    case ItemId.woodenStick: return _pickUp.woodenStick;
                    case ItemId.carbattery: return _pickUp.carbattery;
                    case ItemId.gascan: return _pickUp.gascan;
                    case ItemId.sparkplug: return _pickUp.sparkplug;
                    case ItemId.vas: return _pickUp.vas;
                    case ItemId.vas2: return _pickUp.vas2;
                    case ItemId.melon: return _pickUp.melon;
                    case ItemId.teddy: return _pickUp.teddy;
                    case ItemId.kugg1: return _pickUp.kugg1;
                    case ItemId.kugg2: return _pickUp.kugg2;
                    case ItemId.message: return _pickUp.message;
                    case ItemId.brunnsvev: return _pickUp.brunnsvev;
                    case ItemId.meat: return _pickUp.meat;
                    case ItemId.book: return _pickUp.book;
                    case ItemId.remote: return _pickUp.remote;
                    case ItemId.birdSeed: return _pickUp.birdSeed;
                    case ItemId.deadRat: return _pickUp.deadRat;
                    case ItemId.christmasBall: return _pickUp.christmasKula;
                    case ItemId.christmasKulaBomb: return _pickUp.christmasKulaBomb;
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"GetPickUpItemFallback({id}): {ex.Message}"); }
            return null;
        }

        // 修改 PickUp 字段引用, 指向新创建的对象
        // 关键: 当 tag 不在 TagManager 中时 (如 spidermomkey), 游戏可能用对象引用比较识别物品
        // 把 PickUp.spiderKey 等字段指向我们的新对象, 让游戏认为它就是可拾取的物品
        private void SetPickUpField(ItemId id, GameObject newObj)
        {
            if (_pickUp == null || newObj == null) return;
            try
            {
                // 重要: 不设置 PickUp.***Key 字段!
                // PickUp.***Key 应该指向手上物品 (HandHoldObjects 下), 不是地上物品
                // 游戏通过 tag 判断 is***Key, 不需要 PickUp.***Key 指向地上物品
                // 如果覆盖 PickUp.***Key, 拾取后会被清空, 扔出时找不到手上物品 -> 扔出失败
                GrannyModMenuPlugin.PluginLog.LogInfo($"  SetPickUpField: 跳过设置 PickUp.{id} (保持原始引用, 避免干扰扔出)");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"SetPickUpField({id}): {ex.Message}"); }
        }

        // 诊断辅助: 打印 InventoryController 中某 new*** 字段指向的 Transform 的 parent/scale 等状态
        private void LogInventoryItemParent(string label, Transform t)
        {
            try
            {
                if (t == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  [对比] Inventory.{label} = null");
                    return;
                }
                GameObject go = t.gameObject;
                string parentName = t.parent != null ? t.parent.name : "null";
                string grandParentName = t.parent != null && t.parent.parent != null ? t.parent.parent.name : "null";
                Vector3 pos = t.position;
                Vector3 scale = t.localScale;
                bool active = go.activeSelf;
                bool activeH = go.activeInHierarchy;
                GrannyModMenuPlugin.PluginLog.LogInfo($"  [对比] Inventory.{label} -> '{go.name}' active={active}, activeH={activeH}, parent='{parentName}', grandParent='{grandParentName}', pos={pos}, scale={scale}");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"  [对比] Inventory.{label} 异常: {ex.Message}"); }
        }

        // 根据 renderer bounds 归一化大小, 使物品最大边长约为 targetSize 米
        // 注意: IL2CPP 中 obj.transform.localScale 读取可能返回 0, 必须先重置再基于 bounds 计算
        private void NormalizeScale(GameObject obj, float targetSize)
        {
            try
            {
                Transform t = obj.transform;
                // 先重置 scale 为 1, 这样 bounds.size 反映 mesh 原始大小 (scale=1 时)
                t.localScale = Vector3.one;

                var mr = obj.GetComponent<MeshRenderer>();
                if (mr == null)
                {
                    // 尝试在子对象中查找
                    mr = obj.GetComponentInChildren<MeshRenderer>(true);
                }
                if (mr != null)
                {
                    Vector3 boundsSize = mr.bounds.size;
                    float maxDim = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);
                    if (maxDim > 0.01f)
                    {
                        // 目标 scale = targetSize / mesh原始大小
                        float scaleFactor = targetSize / maxDim;
                        // 直接设置绝对 scale (不依赖读取旧值, 避免 IL2CPP 读取返回 0 的问题)
                        t.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
                        GrannyModMenuPlugin.PluginLog.LogInfo($"NormalizeScale: bounds={boundsSize}, maxDim={maxDim}, scaleFactor={scaleFactor}");
                    }
                    else
                    {
                        // mesh 太小, 用固定 scale
                        t.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                        GrannyModMenuPlugin.PluginLog.LogInfo($"NormalizeScale: maxDim={maxDim} 太小, 使用固定 scale=0.1");
                    }
                }
                else
                {
                    // 没有 renderer, 用固定小值
                    t.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    GrannyModMenuPlugin.PluginLog.LogInfo("NormalizeScale: 无 MeshRenderer, 使用固定 scale=0.1");
                }
            }
            catch (Exception ex)
            {
                GrannyModMenuPlugin.PluginLog.LogWarning($"NormalizeScale 失败: {ex.Message}");
                try { obj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); } catch { }
            }
        }

        private void DisableMonoBehaviours(GameObject go)
        {
            if (go == null) return;
            try
            {
                var scripts = go.GetComponents<MonoBehaviour>();
                if (scripts != null)
                {
                    int count = 0;
                    foreach (var s in scripts)
                    {
                        if (s == null) continue;
                        try { s.enabled = false; count++; }
                        catch { }
                    }
                    if (count > 0) GrannyModMenuPlugin.PluginLog.LogInfo($"  禁用 {count} 个脚本 on {go.name}");
                }
                // 递归处理子对象
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i);
                    if (child != null) DisableMonoBehaviours(child.gameObject);
                }
            }
            catch { }
        }

        private void SetActiveRecursive(GameObject go, bool active)
        {
            if (go == null) return;
            go.SetActive(active);
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = go.transform.GetChild(i);
                if (child != null) SetActiveRecursive(child.gameObject, active);
            }
        }

        // 递归设置 layer (物品及其所有子对象)
        // 关键: 手上物品在 layer=13 (ObjectHand), 真正的地上物品在 layer=0 (Default)
        // 游戏只识别 layer=0 的物品为可拾取
        private void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            try
            {
                go.layer = layer;
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i);
                    if (child != null) SetLayerRecursive(child.gameObject, layer);
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"SetLayerRecursive({go.name}): {ex.Message}"); }
        }

        // 移除可能干扰物品的 MonoBehaviour 脚本 (会重置 scale 或控制可见性)
        // 保留: Renderer, Collider, Rigidbody, MeshFilter 等组件
        // 移除: 所有 MonoBehaviour (手上物品脚本会每帧重置 scale=0)
        private void RemoveDisablingScripts(GameObject go)
        {
            if (go == null) return;
            try
            {
                var components = go.GetComponents<Component>();
                if (components == null) return;
                int removedCount = 0;
                foreach (var comp in components)
                {
                    try
                    {
                        if (comp == null) continue;
                        // 保留: Transform, Renderer, Collider, Rigidbody, MeshFilter, MeshRenderer
                        if (comp is Transform) continue;
                        if (comp is Renderer) continue;
                        if (comp is Collider) continue;
                        if (comp is Rigidbody) continue;
                        if (comp is MeshFilter) continue;
                        // 移除: MonoBehaviour 和其他脚本
                        if (comp is MonoBehaviour || comp is Behaviour)
                        {
                            string typeName = comp.GetType().Name;
                            UnityEngine.Object.Destroy(comp);
                            removedCount++;
                            GrannyModMenuPlugin.PluginLog.LogInfo($"  RemoveDisablingScripts: 移除 {typeName} 从 {go.name}");
                        }
                    }
                    catch { }
                }
                // 递归处理子对象
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    var child = go.transform.GetChild(i);
                    if (child != null) RemoveDisablingScripts(child.gameObject);
                }
                if (removedCount > 0)
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  RemoveDisablingScripts: 从 {go.name} 移除了 {removedCount} 个脚本");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"RemoveDisablingScripts({go.name}): {ex.Message}"); }
        }

        // 从 CheckExitDoor 获取 padlockCodePL1-5 中第一个激活的真地上物品
        private GameObject GetPadlockCodeFromCheckExitDoor()
        {
            if (_checkExitDoor == null) return null;
            try
            {
                // padlockCodePL1-5 中找一个激活的
                GameObject[] candidates = { _checkExitDoor.padlockCodePL1, _checkExitDoor.padlockCodePL2, _checkExitDoor.padlockCodePL3, _checkExitDoor.padlockCodePL4, _checkExitDoor.padlockCodePL5 };
                foreach (var c in candidates)
                {
                    if (c == null) continue;
                    if (!c.activeInHierarchy) continue;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  GetPadlockCodeFromCheckExitDoor: 找到 '{c.name}' (parent={c.transform.parent?.name})");
                    return c;
                }
                // 如果都没有激活, 取第一个非 null 的
                foreach (var c in candidates)
                {
                    if (c != null)
                    {
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  GetPadlockCodeFromCheckExitDoor: 找到未激活的 '{c.name}', 将激活它");
                        return c;
                    }
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"GetPadlockCodeFromCheckExitDoor: {ex.Message}"); }
            return null;
        }

        // 提取物品的基础名称 (去掉 PL*/_PL* 后缀)
        // 例如: "HanglockKeyPL4" -> "HanglockKey", "Wrench_PL2" -> "Wrench", "HanglockKeyHand" -> null
        private string StripGroundSuffix(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // 模式1: 以 "PL" + 数字 结尾 (如 HanglockKeyPL4)
            if (name.Length >= 3)
            {
                string tail = name.Substring(name.Length - 3);
                if (tail.StartsWith("PL", System.StringComparison.OrdinalIgnoreCase) && char.IsDigit(tail[2]))
                {
                    return name.Substring(0, name.Length - 3);
                }
            }
            // 模式2: 以 "_PL" + 数字 结尾 (如 Wrench_PL2)
            if (name.Length >= 4)
            {
                int idx = name.LastIndexOf('_');
                if (idx > 0 && idx < name.Length - 3)
                {
                    string suffix = name.Substring(idx + 1);
                    if (suffix.Length >= 3 && suffix.StartsWith("PL", System.StringComparison.OrdinalIgnoreCase) && char.IsDigit(suffix[2]))
                    {
                        return name.Substring(0, idx);
                    }
                }
            }
            return null;
        }

        // 搜索场景中同类型的真地上物品 (PL 版本) 并返回它的 tag
        // 用于 CreateItemFromMesh 时获取正确的 tag (手上物品 tag 通常是 Untagged)
        // 例如: 源物品 "VapennyckelHand" → 搜索 "VapennyckelPL*" → 返回 tag='weaponkey'
        private string FindGroundItemTag(string sourceName)
        {
            if (string.IsNullOrEmpty(sourceName)) return null;
            try
            {
                // 提取物品名称关键字 (去掉 Hand 后缀)
                string nameKey = sourceName;
                if (nameKey.EndsWith("Hand", System.StringComparison.OrdinalIgnoreCase))
                    nameKey = nameKey.Substring(0, nameKey.Length - 4);
                // 特殊: ShotgunHandP1/P2/P3 -> 搜索 "Shotgun"
                if (nameKey.StartsWith("ShotgunHandP", System.StringComparison.OrdinalIgnoreCase))
                    nameKey = "Shotgun";
                // 特殊: CodePlattaHand -> 真地上物品命名是 padlockCodePL1-5
                if (nameKey.Equals("CodePlatta", System.StringComparison.OrdinalIgnoreCase))
                    nameKey = "padlockCode";
                if (string.IsNullOrEmpty(nameKey)) return null;

                var type = Il2CppType.Of<Transform>();
                if (type == null) return null;
                var objs = UnityEngine.Object.FindObjectsOfType(type);
                if (objs == null) return null;
                for (int i = 0; i < objs.Length; i++)
                {
                    try
                    {
                        if (objs[i] == null) continue;
                        var t = objs[i].TryCast<Transform>();
                        if (t == null) continue;
                        string name = t.name;
                        if (name == null) continue;
                        // 排除手上物品
                        if (name.Contains("Hand", System.StringComparison.OrdinalIgnoreCase)) continue;
                        // 提取基础名称
                        string baseName = StripGroundSuffix(name);
                        if (string.IsNullOrEmpty(baseName)) continue;
                        // 精确匹配
                        if (!baseName.Equals(nameKey, System.StringComparison.OrdinalIgnoreCase)) continue;
                        // 获取 tag
                        string tag = t.gameObject.tag;
                        if (!string.IsNullOrEmpty(tag) && tag != "Untagged")
                        {
                            GrannyModMenuPlugin.PluginLog.LogInfo($"  FindGroundItemTag: 源='{sourceName}', 找到真地上物品 '{name}' tag='{tag}'");
                            return tag;
                        }
                    }
                    catch { }
                }
                GrannyModMenuPlugin.PluginLog.LogInfo($"  FindGroundItemTag: 源='{sourceName}', 未找到同类型的真地上物品");
                return null;
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"FindGroundItemTag({sourceName}): {ex.Message}"); return null; }
        }

        // 从物品名称推断 tag (游戏 PickUp 检查 tag 判断是否可拾取)
        // tag 必须在 Unity TagManager 中预定义, 否则设置会失败
        // 已知的 tag (从场景真地上物品获取):
        private string InferItemTag(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                string n = name.ToLowerInvariant();
                // 已知的 tag 映射 (物品名称 -> TagManager 中的 tag)
                if (n.StartsWith("hanglockkey")) return "hanglockkey";
                if (n.StartsWith("housekey")) return "exitkey"; // HouseKey -> tag='exitkey' (用户称 masterKey)
                if (n.StartsWith("safekey")) return "safekey";
                if (n.StartsWith("vapennyckel")) return "weaponkey";
                if (n.StartsWith("playhousekey")) return "playhousekey";
                if (n.StartsWith("carkey")) return "carkey";
                if (n.StartsWith("specialkey")) return "specialkey";
                if (n.StartsWith("rustypadlockkey")) return "rustypadlockkey";
                if (n.StartsWith("spidermomkey")) return "spiderkey"; // SpiderMomKeyHand -> tag='spiderkey' (spidermomkey 不在 TagManager, 用 spiderkey)
                if (n.StartsWith("codeplatta")) return "padlockcode"; // CodePlattaHand -> tag='padlockcode' (可能存在)
                if (n.StartsWith("oldshotgun")) return "oldshotgun";
                if (n.StartsWith("shotgunhand")) return "shotgun";
                if (n.StartsWith("topplock")) return "topplock";
                if (n.StartsWith("ammo")) return "ammo";
                if (n.StartsWith("armborst")) return "armborst";
                if (n.StartsWith("pepperspray")) return "pepperspray";
                if (n.StartsWith("freezetrap")) return "freezetrap";
                if (n.StartsWith("avbitare")) return "avbitare";
                if (n.StartsWith("hammer")) return "hammer";
                if (n.StartsWith("screwdriver")) return "screwdriver";
                if (n.StartsWith("planka")) return "planka";
                if (n.StartsWith("carbattery")) return "carbattery";
                if (n.StartsWith("wrench")) return "wrench";
                if (n.StartsWith("chaincutter")) return "chaincutter";
                if (n.StartsWith("wheelcrank")) return "wheelcrank";
                if (n.StartsWith("woodenstick")) return "woodenstick";
                if (n.StartsWith("sparkplug")) return "sparkplug";
                GrannyModMenuPlugin.PluginLog.LogInfo($"  InferItemTag: 未知物品 '{name}', 无法推断 tag");
                return null;
            }
            catch { return null; }
        }

        // 检查 tag 是否在 TagManager 中存在 (运行时)
        // tag 必须在 TagManager 中预定义, 否则 GameObject.tag = "xxx" 会失败
        // 运行时无法动态添加 tag (SerializedObject 是 UnityEditor 的), 只能检查
        private System.Collections.Generic.HashSet<string> _checkedTags = new System.Collections.Generic.HashSet<string>();
        private System.Collections.Generic.HashSet<string> _failedTags = new System.Collections.Generic.HashSet<string>();
        private bool IsTagAvailable(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == "Untagged") return true;
            if (_checkedTags.Contains(tag)) return true;
            if (_failedTags.Contains(tag)) return false;
            try
            {
                // 用一个临时 GameObject 测试 tag 是否可用
                GameObject tester = new GameObject("tag_tester");
                try
                {
                    tester.tag = tag;
                    // 关键: IL2CPP 中 GameObject.tag = "xxx" 可能不抛异常但不设置 tag
                    // 必须在设置后验证 tag 是否真的改变了
                    string actualTag = tester.tag;
                    bool ok = actualTag == tag;
                    UnityEngine.Object.Destroy(tester);
                    if (ok)
                    {
                        _checkedTags.Add(tag);
                        return true;
                    }
                    else
                    {
                        _failedTags.Add(tag);
                        GrannyModMenuPlugin.PluginLog.LogWarning($"  IsTagAvailable: tag '{tag}' 设置后验证失败 (实际 tag='{actualTag}'), 不在 TagManager 中");
                        return false;
                    }
                }
                catch
                {
                    UnityEngine.Object.Destroy(tester);
                    _failedTags.Add(tag);
                    GrannyModMenuPlugin.PluginLog.LogWarning($"  IsTagAvailable: tag '{tag}' 不在 TagManager 中, 无法设置");
                    return false;
                }
            }
            catch { return false; }
        }

        // 创建全新的 GameObject 并从原物品复制 mesh
        // 关键: 手上物品 HanglockKeyHand 的 transform.localScale 在 IL2CPP 中读取始终返回 0
        // 创建全新 GameObject, 新对象的 localScale 能正常工作
        private GameObject CreateItemFromMesh(GameObject source, Vector3 spawnPos)
        {
            if (source == null) return null;
            try
            {
                // 创建全新的 GameObject
                GameObject newObj = new GameObject(source.name + "_Spawned");
                newObj.transform.SetParent(null, false);
                newObj.transform.position = spawnPos;
                newObj.layer = 0; // Default layer (可拾取物品的层)

                // 从源物品及其子对象收集所有 MeshFilter
                var sourceMeshes = source.GetComponentsInChildren<MeshFilter>(true);
                if (sourceMeshes == null || sourceMeshes.Length == 0)
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning($"CreateItemFromMesh: 源物品 {source.name} 无 MeshFilter");
                    UnityEngine.Object.Destroy(newObj);
                    return null;
                }

                // 找到最大的 mesh (手上物品 mesh 很大, 109米)
                Mesh mainMesh = null;
                Vector3 meshSize = Vector3.zero;
                float maxDim = 0f;
                foreach (var mf in sourceMeshes)
                {
                    if (mf == null) continue;
                    try
                    {
                        var mesh = mf.sharedMesh;
                        if (mesh == null) mesh = mf.mesh;
                        if (mesh == null) continue;
                        Vector3 s = mesh.bounds.size;
                        float dim = Mathf.Max(s.x, s.y, s.z);
                        if (dim > maxDim)
                        {
                            maxDim = dim;
                            mainMesh = mesh;
                            meshSize = s;
                        }
                    }
                    catch { }
                }

                if (mainMesh == null)
                {
                    GrannyModMenuPlugin.PluginLog.LogWarning($"CreateItemFromMesh: 未找到有效 mesh");
                    UnityEngine.Object.Destroy(newObj);
                    return null;
                }

                // 添加 MeshFilter 和 MeshRenderer
                var newMf = newObj.AddComponent<MeshFilter>();
                newMf.sharedMesh = mainMesh;
                var newMr = newObj.AddComponent<MeshRenderer>();
                // 从源物品复制材质
                try
                {
                    var sourceMr = source.GetComponentInChildren<MeshRenderer>(true);
                    if (sourceMr != null)
                    {
                        var mats = sourceMr.sharedMaterials;
                        if (mats != null && mats.Length > 0) newMr.sharedMaterials = mats;
                    }
                }
                catch { }

                // 关键: 从源物品复制 tag (游戏 PickUp 检查 tag 判断是否可拾取)
                // 真正的地上物品有 tag (如 'hanglockkey'), 手上物品可能也有相同的 tag
                try
                {
                    string sourceTag = source.tag;
                    GrannyModMenuPlugin.PluginLog.LogInfo($"  CreateItemFromMesh: 源物品 {source.name} tag='{sourceTag}'");
                    if (!string.IsNullOrEmpty(sourceTag) && sourceTag != "Untagged" && IsTagAvailable(sourceTag))
                    {
                        newObj.tag = sourceTag;
                        GrannyModMenuPlugin.PluginLog.LogInfo($"  CreateItemFromMesh: 已复制 tag='{sourceTag}' 到 {newObj.name}");
                    }
                    else
                    {
                        // 源物品 tag 是 Untagged, 搜索场景中同类型的真地上物品获取正确 tag
                        string groundTag = FindGroundItemTag(source.name);
                        if (!string.IsNullOrEmpty(groundTag) && IsTagAvailable(groundTag))
                        {
                            try
                            {
                                newObj.tag = groundTag;
                                GrannyModMenuPlugin.PluginLog.LogInfo($"  CreateItemFromMesh: 从真地上物品获取 tag='{groundTag}' 到 {newObj.name}");
                            }
                            catch (Exception tagEx)
                            {
                                GrannyModMenuPlugin.PluginLog.LogWarning($"  CreateItemFromMesh: 设置 tag='{groundTag}' 失败: {tagEx.Message}");
                            }
                        }
                        else
                        {
                            // 找不到真地上物品, 用名称推断 tag (可能不准确)
                            string inferredTag = InferItemTag(source.name);
                            if (!string.IsNullOrEmpty(inferredTag) && IsTagAvailable(inferredTag))
                            {
                                try
                                {
                                    newObj.tag = inferredTag;
                                    GrannyModMenuPlugin.PluginLog.LogInfo($"  CreateItemFromMesh: 推断 tag='{inferredTag}' 到 {newObj.name} (可能不准确)");
                                }
                                catch (Exception tagEx)
                                {
                                    GrannyModMenuPlugin.PluginLog.LogWarning($"  CreateItemFromMesh: 设置 tag='{inferredTag}' 失败: {tagEx.Message}");
                                }
                            }
                            else
                            {
                                GrannyModMenuPlugin.PluginLog.LogWarning($"  CreateItemFromMesh: 无法为 {newObj.name} 设置 tag, 该物品可能无法被游戏识别为可拾取");
                            }
                        }
                    }
                }
                catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogWarning($"  CreateItemFromMesh: 复制 tag 失败: {ex.Message}"); }

                // 计算让世界大小约 1.0 米的 scale (增大, 让物品更容易看到和对准)
                float targetSize = 1.0f;
                float scaleFactor = targetSize / maxDim;
                // 关键: 全新 GameObject 的 localScale 能正常工作
                newObj.transform.localScale = Vector3.one * scaleFactor;

                // 添加 SphereCollider (世界半径约 2.0 米, 让 Raycast 和 SphereCast 都容易命中)
                // 关键: PickUp 用 SphereCast(radius=rayThick) 检测, 但按E拾取时可能用 Raycast
                // 增大半径确保 Raycast 也能命中 (spiderKey 无 PL 版本, 需要更大 Collider)
                var sc = newObj.AddComponent<SphereCollider>();
                sc.radius = 2.0f / scaleFactor; // 本地坐标, 世界半径 = radius * scale = 2.0
                sc.isTrigger = false;
                // 验证设置是否生效
                GrannyModMenuPlugin.PluginLog.LogInfo($"  CreateItemFromMesh: SphereCollider.radius={sc.radius} (世界半径={sc.radius * scaleFactor})");

                // 添加 Rigidbody (isKinematic=true, 让物品保持在生成位置不移动)
                // 关键: 之前 useGravity=true 导致物品因重力滑动到远离玩家的位置
                // Raycast 无法命中不在摄像机正前方的物品, 导致无法拾取
                var rb = newObj.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                GrannyModMenuPlugin.PluginLog.LogInfo($"  CreateItemFromMesh: 创建 {newObj.name}, meshSize={meshSize}, maxDim={maxDim}, scale={scaleFactor}, layer=0");
                return newObj;
            }
            catch (Exception ex)
            {
                GrannyModMenuPlugin.PluginLog.LogWarning($"CreateItemFromMesh({source.name}): {ex.Message}");
                return null;
            }
        }

        private void GiveAllItems()
        {
            if (_inventory == null) return;
            try
            {
                _inventory.havesafeKey = true; _inventory.haveexitKey = true; _inventory.havehanglockKey = true;
                _inventory.havepadlockCode = true; _inventory.haveweaponKey = true; _inventory.haveplayhouseKey = true;
                _inventory.havecarKey = true; _inventory.havespecialkey = true; _inventory.haverustyPadlockKey = true;
                _inventory.havespiderKey = true; _inventory.havearmborst = true; _inventory.haveArrow = true;
                _inventory.haveoldShotgun = true; _inventory.havegunDel1 = true; _inventory.havegunDel2 = true;
                _inventory.havegunDel3 = true; _inventory.havetopplock = true; _inventory.havepepperspray = true;
                _inventory.havefreezeTrap = true; _inventory.haveAvbitare = true; _inventory.haveHammare = true;
                _inventory.havescrewdriver = true; _inventory.haveplanka = true; _inventory.havebattery = true;
                _inventory.havewrench = true; _inventory.havechainCutter = true; _inventory.havewheelCrank = true;
                _inventory.havewoodenStick = true; _inventory.havecarbattery = true; _inventory.havegascan = true;
                _inventory.havesparkplug = true; _inventory.havetb1 = true; _inventory.havetb2 = true;
                _inventory.havetb3 = true; _inventory.havetb4 = true; _inventory.havevas = true;
                _inventory.havevas2 = true; _inventory.havemelon = true; _inventory.haveteddy = true;
                _inventory.havekugg1 = true; _inventory.havekugg2 = true; _inventory.havemessage = true;
                _inventory.havebrunnsvev = true; _inventory.havemeat = true; _inventory.havebook = true;
                _inventory.haveremote = true; _inventory.havebirdSeed = true; _inventory.havedeadRat = true;
                _inventory.havechristmasBall = true; _inventory.havechristmasKulaBomb = true;
                GrannyModMenuPlugin.PluginLog.LogInfo("已给予所有物品");
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogError($"GiveAll: {ex.Message}"); }
        }

        // ==========================================
        // === OnGUI: 直接绘制, 不用 BeginScrollView ===
        // ==========================================
        private void OnGUI()
        {
            if (!ModState.MenuVisible) return;
            try
            {
                _mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                DrawMenu();
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogError($"OnGUI: {ex.Message}"); }
        }

        private void DrawMenu()
        {
            _scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (_scale < 0.5f) _scale = 0.5f;

            float menuW = Mathf.Min(Screen.width - 40, BaseWidth * _scale);
            // 限制菜单最大高度, 不超过屏幕高度的 80%
            float maxMenuH = Screen.height * 0.8f;
            float menuH = Mathf.Min(maxMenuH, 720f * _scale);
            if (menuH > maxMenuH) menuH = maxMenuH;
            float menuX = (Screen.width - menuW) * 0.5f;
            float menuY = (Screen.height - menuH) * 0.5f;

            // 阴影 + 背景 + 边框
            DrawRect(new Rect(menuX - 3, menuY - 3, menuW + 6, menuH + 6), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect(menuX, menuY, menuW, menuH), BgColor);
            DrawRectBorder(new Rect(menuX, menuY, menuW, menuH), BorderColor);

            // 标题栏
            DrawRect(new Rect(menuX, menuY, menuW, HeaderHeight * _scale), HeaderColor);
            DrawRect(new Rect(menuX, menuY + HeaderHeight * _scale - 2, menuW, 2), AccentColor);
            GUI.Label(new Rect(menuX + Padding * _scale, menuY, menuW - Padding * 2 * _scale, HeaderHeight * _scale),
                $"  GRANNY MOD MENU  v{GrannyModMenuPlugin.PluginVersion}", MakeStyle(16, true, 0, AccentColor));

            // 标签栏
            DrawTabs(menuX, menuY + HeaderHeight * _scale, menuW, TabHeight * _scale);

            // 内容区 (直接绘制, 不用 BeginGroup - IL2CPP 中 BeginGroup 可能不裁剪)
            float tabBottom = menuY + (HeaderHeight + TabHeight) * _scale;
            float statusH = RowHeight * _scale + Padding;
            float contentX = menuX + Padding * _scale;
            float contentY = tabBottom + Padding * _scale;
            float contentW = menuW - Padding * 2 * _scale;
            float contentH = menuH - (HeaderHeight + TabHeight) * _scale - (Padding * 2 + statusH);

            _contentRect = new Rect(contentX, contentY, contentW, contentH);
            _clipY = contentY;
            _clipH = contentH;

            // 处理鼠标滚轮滚动
            HandleScroll(contentH);

            // 绘制内容区背景
            DrawRect(_contentRect, new Color(0.04f, 0.05f, 0.08f, 0.5f));

            // 直接绘制内容 (不用 BeginGroup, 用 _clipY/_clipH 手动检查可见性)
            try
            {
                float innerX = contentX;
                float innerY = contentY - _scrollY; // 滚动偏移
                float innerW = contentW - 16f; // 留滚动条空间

                // 绘制内容 (手动跳过不可见行)
                _contentTotalH = DrawTabContent(innerX, innerY, innerW, _scale);

                // 绘制滚动条 (在内容区右侧)
                if (_contentTotalH > contentH)
                {
                    float barX = contentX + contentW - 10f;
                    float barH = contentH * (contentH / _contentTotalH);
                    float barY = contentY + (_scrollY / (_contentTotalH - contentH)) * (contentH - barH);
                    DrawRect(new Rect(barX, contentY, 6, contentH), new Color(0.1f, 0.1f, 0.1f, 0.5f));
                    DrawRect(new Rect(barX, barY, 6, barH), AccentDimColor);
                }
            }
            catch (Exception ex) { GrannyModMenuPlugin.PluginLog.LogError($"Content: {ex.Message}"); }

            // 状态栏
            float statusY = menuY + menuH - statusH;
            DrawRect(new Rect(menuX, statusY, menuW, statusH), SectionBgColor);
            DrawRect(new Rect(menuX, statusY, menuW, 1), BorderColor);
            int sc = _spiders != null ? _spiders.Length : 0;
            int cc2 = _crows != null ? _crows.Length : 0;
            int rc = _rats != null ? _rats.Length : 0;
            int mc = _momSpiders != null ? _momSpiders.Length : 0;
            string status = $"  P:{((_player != null || _playerTransform != null) ? "已找到" : "未找到")}  G:{StatusText(_granny)}  S:{sc}  M:{mc}  C:{cc2}  R:{rc}  |  滚轮滚动  |  F1 切换";
            GUI.Label(new Rect(menuX + Padding * _scale, statusY + 6, menuW - Padding * 2 * _scale, statusH),
                status, MakeStyle(11, false, 0, TextDimColor));
        }

        private void HandleScroll(float contentH)
        {
            try
            {
                // 鼠标在内容区内时, 用滚轮滚动
                if (_contentRect.Contains(_mousePos))
                {
                    float delta = Input.GetAxis("Mouse ScrollWheel");
                    if (delta != 0)
                    {
                        _scrollY -= delta * 80f;
                        float maxScroll = _contentTotalH - contentH;
                        if (maxScroll < 0) maxScroll = 0;
                        _scrollY = Mathf.Clamp(_scrollY, 0, maxScroll);
                    }
                }
            }
            catch { }
        }

        // 返回内容总高度
        private float DrawTabContent(float x, float y, float width, float scale)
        {
            switch (ModState.SelectedTab)
            {
                case 0: return DrawPlayerTab(x, y, width, scale);
                case 1: return DrawEnemiesTab(x, y, width, scale);
                case 2: return DrawMapTab(x, y, width, scale);
                case 3: return DrawGameTab(x, y, width, scale);
                case 4: return DrawWeaponTab(x, y, width, scale);
                case 5: return DrawItemsTab(x, y, width, scale);
                case 6: return DrawVehicleTab(x, y, width, scale);
                case 7: return DrawAboutTab(x, y, width, scale);
            }
            return 0;
        }

        private void DrawTabs(float x, float y, float width, float height)
        {
            string[] tabs = { "玩家", "敌人", "地图", "游戏", "武器", "物品", "车辆", "关于" };
            float tabW = width / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tabRect = new Rect(x + i * tabW, y, tabW, height);
                bool selected = ModState.SelectedTab == i;
                bool hover = tabRect.Contains(_mousePos);

                DrawRect(tabRect, selected ? TabSelectedColor : (hover ? HoverColor : TabColor));
                if (selected)
                    DrawRect(new Rect(tabRect.x, tabRect.yMax - 3, tabRect.width, 3), AccentColor);

                GUI.Label(tabRect, tabs[i], MakeStyle(13, true, 1, selected ? Color.white : TextColor));

                if (GUI.Button(tabRect, "", GUIStyle.none))
                    ModState.SelectedTab = i;
            }
        }

        // ===== 玩家标签页 =====
        private float DrawPlayerTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, "基础功能 (上帝模式)", scale);
            curY += gap;
            ModState.GodMode = DrawToggle(x, curY, width, rowH, "上帝模式 (God Mode - 无敌)", ModState.GodMode, scale); curY += rowH + gap;
            ModState.NoClip = DrawToggle(x, curY, width, rowH, "穿墙 (NoClip - 禁用碰撞)", ModState.NoClip, scale); curY += rowH + gap;
            ModState.FlyMode = DrawToggle(x, curY, width, rowH, "飞行模式 (WASD + 空格/Ctrl)", ModState.FlyMode, scale); curY += rowH + gap;
            if (ModState.FlyMode) { ModState.FlySpeed = DrawSlider(x, curY, width, rowH * 1.5f, "飞行速度", ModState.FlySpeed, 1f, 30f, scale); curY += rowH * 1.5f + gap; }
            curY += gap;

            curY = DrawSection(x, curY, width, "移动", scale);
            curY += gap;
            ModState.PlayerSpeedEnabled = DrawToggle(x, curY, width, rowH, "速度倍率", ModState.PlayerSpeedEnabled, scale); curY += rowH + gap;
            if (ModState.PlayerSpeedEnabled) { ModState.PlayerSpeedMultiplier = DrawSlider(x, curY, width, rowH * 1.5f, "速度倍率", ModState.PlayerSpeedMultiplier, 0.5f, 10f, scale); curY += rowH * 1.5f + gap; }
            ModState.PlayerJumpBoost = DrawToggle(x, curY, width, rowH, "跳跃增强", ModState.PlayerJumpBoost, scale); curY += rowH + gap;
            if (ModState.PlayerJumpBoost) { ModState.PlayerJumpMultiplier = DrawSlider(x, curY, width, rowH * 1.5f, "跳跃倍率", ModState.PlayerJumpMultiplier, 1f, 10f, scale); curY += rowH * 1.5f + gap; }
            ModState.PlayerAlwaysGrounded = DrawToggle(x, curY, width, rowH, "始终着地 (无限跳)", ModState.PlayerAlwaysGrounded, scale); curY += rowH + gap;
            ModState.PlayerNoFallDamage = DrawToggle(x, curY, width, rowH, "无坠落伤害", ModState.PlayerNoFallDamage, scale); curY += rowH + gap;
            ModState.PlayerInAirControl = DrawToggle(x, curY, width, rowH, "空中控制增强 (5x)", ModState.PlayerInAirControl, scale); curY += rowH + gap;
            curY += gap;

            curY = DrawSection(x, curY, width, "其他", scale);
            curY += gap;
            ModState.PlayerCrouch = DrawToggle(x, curY, width, rowH, "强制蹲下 (潜行)", ModState.PlayerCrouch, scale); curY += rowH + gap;
            ModState.PlayerFlashlight = DrawToggle(x, curY, width, rowH, "拥有手电筒", ModState.PlayerFlashlight, scale); curY += rowH + gap;
            ModState.PlayerScaleEnabled = DrawToggle(x, curY, width, rowH, "缩放玩家", ModState.PlayerScaleEnabled, scale); curY += rowH + gap;
            if (ModState.PlayerScaleEnabled) { ModState.PlayerScale = DrawSlider(x, curY, width, rowH * 1.5f, "玩家大小", ModState.PlayerScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap; }

            return curY - y;
        }

        // ===== 敌人标签页 =====
        private float DrawEnemiesTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, "场景物体选中与克隆", scale);
            curY += gap;
            DrawInfo(x, curY, width, rowH, $"选中: {_selectedInfo}", scale); curY += rowH + gap;
            if (DrawButton(x, curY, width / 2 - gap / 2, rowH, "选中准星物体", scale)) TriggerSelectCrosshairObject();
            if (DrawButton(x + width / 2 + gap / 2, curY, width / 2 - gap / 2, rowH, "清除选中", scale)) TriggerClearSelection();
            curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "克隆选中物体", scale)) TriggerCloneSelectedEnemy();
            curY += rowH + gap;
            ModState.SelectedObjectScale = DrawSlider(x, curY, width, rowH * 1.5f, "选中物体大小", ModState.SelectedObjectScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            curY += gap;

            curY = DrawSection(x, curY, width, $"奶奶 (EnemyAIGranny) [{StatusText(_granny)}]", scale);
            curY += gap;
            ModState.GrannyFreezeEnabled = DrawToggle(x, curY, width, rowH, "冻结奶奶", ModState.GrannyFreezeEnabled, scale); curY += rowH + gap;
            ModState.GrannyBlind = DrawToggle(x, curY, width, rowH, "奶奶失明", ModState.GrannyBlind, scale); curY += rowH + gap;
            ModState.GrannyDeaf = DrawToggle(x, curY, width, rowH, "奶奶失聪", ModState.GrannyDeaf, scale); curY += rowH + gap;
            ModState.GrannyNoAttack = DrawToggle(x, curY, width, rowH, "奶奶不攻击", ModState.GrannyNoAttack, scale); curY += rowH + gap;
            ModState.GrannyNoCatch = DrawToggle(x, curY, width, rowH, "无法抓住玩家", ModState.GrannyNoCatch, scale); curY += rowH + gap;
            ModState.GrannySpeedEnabled = DrawToggle(x, curY, width, rowH, "速度倍率", ModState.GrannySpeedEnabled, scale); curY += rowH + gap;
            if (ModState.GrannySpeedEnabled) { ModState.GrannySpeedMultiplier = DrawSlider(x, curY, width, rowH * 1.5f, "速度", ModState.GrannySpeedMultiplier, 0.1f, 2f, scale); curY += rowH * 1.5f + gap; }
            curY += gap;
            curY = DrawSection(x, curY, width, "瞬时操作", scale);
            curY += gap;
            if (DrawButton(x, curY, width, rowH, "一击击倒 (grannyHitByGun)", scale)) TriggerGrannyKnockout(); curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "辣椒效果 (grannyHitByPepper)", scale)) TriggerGrannyPepper(); curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "撞车效果 (grannyHitByCar)", scale)) TriggerGrannyHitByCar(); curY += rowH + gap;
            ModState.GrannyScaleEnabled = DrawToggle(x, curY, width, rowH, "启用 Granny 缩放", ModState.GrannyScaleEnabled, scale); curY += rowH + gap;
            if (ModState.GrannyScaleEnabled)
            {
                ModState.GrannyScale = DrawSlider(x, curY, width, rowH * 1.5f, "Granny 大小", ModState.GrannyScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆 Granny", scale)) TriggerCloneGranny(); curY += rowH + gap;
            curY += gap;

            int sc = _spiders != null ? _spiders.Length : 0;
            curY = DrawSection(x, curY, width, $"蜘蛛 (spiderControll) [{sc} 只]", scale);
            curY += gap;
            ModState.SpiderFreeze = DrawToggle(x, curY, width, rowH, "冻结蜘蛛 (spiderDead)", ModState.SpiderFreeze, scale); curY += rowH + gap;
            ModState.SpiderNoHunt = DrawToggle(x, curY, width, rowH, "蜘蛛不追玩家", ModState.SpiderNoHunt, scale); curY += rowH + gap;
            ModState.SpiderNoBite = DrawToggle(x, curY, width, rowH, "蜘蛛不咬玩家", ModState.SpiderNoBite, scale); curY += rowH + gap;
            ModState.SpiderNoCatch = DrawToggle(x, curY, width, rowH, "蜘蛛无法抓住", ModState.SpiderNoCatch, scale); curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "立即杀死所有蜘蛛 (spiderIsDead)", scale)) TriggerSpiderKillAll(); curY += rowH + gap;
            ModState.SpiderScaleEnabled = DrawToggle(x, curY, width, rowH, "启用蜘蛛缩放", ModState.SpiderScaleEnabled, scale); curY += rowH + gap;
            if (ModState.SpiderScaleEnabled)
            {
                ModState.SpiderScale = DrawSlider(x, curY, width, rowH * 1.5f, "蜘蛛大小", ModState.SpiderScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆第一只蜘蛛", scale)) TriggerCloneEnemy(_spiders, "Spider"); curY += rowH + gap;
            curY += gap;

            int mc = _momSpiders != null ? _momSpiders.Length : 0;
            curY = DrawSection(x, curY, width, $"下水道蜘蛛 granny (MomSpiderHead) [{mc} 只]", scale);
            curY += gap;
            ModState.MomSpiderFreeze = DrawToggle(x, curY, width, rowH, "冻结妈妈蜘蛛 (Hunting=false)", ModState.MomSpiderFreeze, scale); curY += rowH + gap;
            ModState.MomSpiderBlind = DrawToggle(x, curY, width, rowH, "妈妈蜘蛛失明", ModState.MomSpiderBlind, scale); curY += rowH + gap;
            ModState.MomSpiderNoCatch = DrawToggle(x, curY, width, rowH, "妈妈蜘蛛无法抓住", ModState.MomSpiderNoCatch, scale); curY += rowH + gap;
            ModState.MomSpiderEscape = DrawToggle(x, curY, width, rowH, "玩家逃脱 (playerEscape)", ModState.MomSpiderEscape, scale); curY += rowH + gap;
            ModState.MomSpiderDead = DrawToggle(x, curY, width, rowH, "杀死妈妈蜘蛛 (解除无法击杀限制)", ModState.MomSpiderDead, scale); curY += rowH + gap;
            ModState.MomSpiderScaleEnabled = DrawToggle(x, curY, width, rowH, "启用妈妈蜘蛛缩放", ModState.MomSpiderScaleEnabled, scale); curY += rowH + gap;
            if (ModState.MomSpiderScaleEnabled)
            {
                ModState.MomSpiderScale = DrawSlider(x, curY, width, rowH * 1.5f, "妈妈蜘蛛大小", ModState.MomSpiderScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆第一只妈妈蜘蛛", scale)) TriggerCloneEnemy(_momSpiders, "MomSpider"); curY += rowH + gap;
            curY += gap;

            int mcc = _momCrawls != null ? _momCrawls.Length : 0;
            curY = DrawSection(x, curY, width, $"妈妈爬行者 (momCrawl) [{mcc} 只]", scale);
            curY += gap;
            ModState.MomCrawlDead = DrawToggle(x, curY, width, rowH, "杀死妈妈爬行者 (momDead)", ModState.MomCrawlDead, scale); curY += rowH + gap;
            ModState.MomCrawlScaleEnabled = DrawToggle(x, curY, width, rowH, "启用妈妈爬行者缩放", ModState.MomCrawlScaleEnabled, scale); curY += rowH + gap;
            if (ModState.MomCrawlScaleEnabled)
            {
                ModState.MomCrawlScale = DrawSlider(x, curY, width, rowH * 1.5f, "妈妈爬行者大小", ModState.MomCrawlScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆第一只妈妈爬行者", scale)) TriggerCloneEnemy(_momCrawls, "MomCrawl"); curY += rowH + gap;
            curY += gap;

            int cc2 = _crows != null ? _crows.Length : 0;
            curY = DrawSection(x, curY, width, $"乌鸦 (CrowControl) [{cc2} 只]", scale);
            curY += gap;
            ModState.CrowFreeze = DrawToggle(x, curY, width, rowH, "吓跑乌鸦 (crowGetShoot)", ModState.CrowFreeze, scale); curY += rowH + gap;
            ModState.CrowNoAttack = DrawToggle(x, curY, width, rowH, "乌鸦不攻击", ModState.CrowNoAttack, scale); curY += rowH + gap;
            ModState.CrowNoSteal = DrawToggle(x, curY, width, rowH, "乌鸦不偷窃", ModState.CrowNoSteal, scale); curY += rowH + gap;
            ModState.CrowScaleEnabled = DrawToggle(x, curY, width, rowH, "启用乌鸦缩放", ModState.CrowScaleEnabled, scale); curY += rowH + gap;
            if (ModState.CrowScaleEnabled)
            {
                ModState.CrowScale = DrawSlider(x, curY, width, rowH * 1.5f, "乌鸦大小", ModState.CrowScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆第一只乌鸦", scale)) TriggerCloneEnemy(_crows, "Crow"); curY += rowH + gap;
            curY += gap;

            int rc = _rats != null ? _rats.Length : 0;
            curY = DrawSection(x, curY, width, $"老鼠 (ratController) [{rc} 只]", scale);
            curY += gap;
            ModState.RatFreeze = DrawToggle(x, curY, width, rowH, "冻结老鼠", ModState.RatFreeze, scale); curY += rowH + gap;
            ModState.RatScaleEnabled = DrawToggle(x, curY, width, rowH, "启用老鼠缩放", ModState.RatScaleEnabled, scale); curY += rowH + gap;
            if (ModState.RatScaleEnabled)
            {
                ModState.RatScale = DrawSlider(x, curY, width, rowH * 1.5f, "老鼠大小", ModState.RatScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆第一只老鼠", scale)) TriggerCloneEnemy(_rats, "Rat"); curY += rowH + gap;
            curY += gap;

            int scount = _santas != null ? _santas.Length : 0;
            curY = DrawSection(x, curY, width, $"小圣诞老人 (LittleSantaController) [{scount} 只]", scale);
            curY += gap;
            ModState.SantaStunned = DrawToggle(x, curY, width, rowH, "眩晕圣诞老人 (currentState=Stunned)", ModState.SantaStunned, scale); curY += rowH + gap;
            ModState.SantaScaleEnabled = DrawToggle(x, curY, width, rowH, "启用小圣诞老人缩放", ModState.SantaScaleEnabled, scale); curY += rowH + gap;
            if (ModState.SantaScaleEnabled)
            {
                ModState.SantaScale = DrawSlider(x, curY, width, rowH * 1.5f, "小圣诞老人大小", ModState.SantaScale, 0.1f, 10f, scale); curY += rowH * 1.5f + gap;
            }
            if (DrawButton(x, curY, width, rowH, "克隆第一只小圣诞老人", scale)) TriggerCloneEnemy(_santas, "Santa"); curY += rowH + gap;

            return curY - y;
        }

        // ===== 地图标签页 (新增) =====
        private float DrawMapTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, "光照 (全图变亮)", scale);
            curY += gap;
            ModState.FullBright = DrawToggle(x, curY, width, rowH, "全图变亮 (手电筒 Light.intensity = 5)", ModState.FullBright, scale); curY += rowH + gap;
            ModState.AllLightsOn = DrawToggle(x, curY, width, rowH, "启用所有场景光源", ModState.AllLightsOn, scale); curY += rowH + gap;
            ModState.NoFog = DrawToggle(x, curY, width, rowH, "关闭雾 (RenderSettings.fog = false)", ModState.NoFog, scale); curY += rowH + gap;
            curY += gap;
            DrawInfo(x, curY, width, rowH, $"手电筒控制器: {StatusText(_flashlight)}", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, $"拾取手电筒: {StatusText(_pickupFlashlight)}", scale); curY += rowH + gap;
            curY += gap;

            return curY - y;
        }

        // ===== 游戏标签页 =====
        private float DrawGameTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, "游戏流程", scale);
            curY += gap;
            ModState.NeverGetCaught = DrawToggle(x, curY, width, rowH, "永不被抓 (终极保护)", ModState.NeverGetCaught, scale); curY += rowH + gap;
            ModState.ForceDay2 = DrawToggle(x, curY, width, rowH, "强制第 2 天", ModState.ForceDay2, scale); curY += rowH + gap;
            ModState.ForceDay3 = DrawToggle(x, curY, width, rowH, "强制第 3 天", ModState.ForceDay3, scale); curY += rowH + gap;
            ModState.NightmareMode = DrawToggle(x, curY, width, rowH, "噩梦模式", ModState.NightmareMode, scale); curY += rowH + gap;
            curY += gap;
            if (DrawButton(x, curY, width, rowH, "立即通关 (PlayerEscaped)", scale)) ModState.ForceEscaped = true; curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "取消通关", scale)) ModState.ForceEscaped = false; curY += rowH + gap;

            return curY - y;
        }

        // ===== 武器标签页 =====
        private float DrawWeaponTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, $"猎枪 (GunShoot) [{StatusText(_gunShoot)}]", scale);
            curY += gap;
            ModState.GunRapidFire = DrawToggle(x, curY, width, rowH, "连发模式 (fireRate = 0.05)", ModState.GunRapidFire, scale); curY += rowH + gap;
            ModState.GunInfiniteRange = DrawToggle(x, curY, width, rowH, "无限射程 (weaponRange = 1000)", ModState.GunInfiniteRange, scale); curY += rowH + gap;
            ModState.OldShotgunLoaded = DrawToggle(x, curY, width, rowH, "旧猎枪上膛 (oldShotgunLoaded)", ModState.OldShotgunLoaded, scale); curY += rowH + gap;
            if (_gunShoot != null)
            {
                curY += gap;
                DrawInfo(x, curY, width, rowH, $"当前 fireRate: {_gunShoot.fireRate:F3}", scale); curY += rowH + gap;
                DrawInfo(x, curY, width, rowH, $"当前 weaponRange: {_gunShoot.weaponRange:F1}", scale); curY += rowH + gap;
            }

            return curY - y;
        }

        // ===== 物品标签页 =====
        private float DrawItemsTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, $"物品栏 (InventoryController) [{StatusText(_inventory)}]", scale);
            curY += gap;
            if (DrawButton(x, curY, width, rowH, "一键获得所有物品 (have* = true)", scale)) GiveAllItems(); curY += rowH + gap;
            curY += gap;

            curY = DrawSection(x, curY, width, "物品生成 (准星前方 2 米, 自由下坠)", scale);
            curY += gap;

            if (_inventory == null)
            {
                DrawInfo(x, curY, width, rowH, "InventoryController 未找到 (需要进入游戏场景)", scale); curY += rowH + gap;
                return curY - y;
            }

            curY = DrawSubSection(x, curY, width, "钥匙类", scale);
            curY += DrawItemBtn(x, curY, width, rowH, "出口钥匙 (exitKey)", _inventory.exitKey, ItemId.exitKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "保险柜钥匙 (safeKey)", _inventory.safeKey, ItemId.safeKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "挂锁钥匙 (hanglockKey)", _inventory.hanglockKey, ItemId.hanglockKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "密码盘 (padlockCode)", _inventory.padlockCode, ItemId.padlockCode, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "武器钥匙 (weaponKey)", _inventory.weaponKey, ItemId.weaponKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "玩具房钥匙 (playhouseKey)", _inventory.playhouseKey, ItemId.playhouseKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "车钥匙 (carKey)", _inventory.carKey, ItemId.carKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "特殊钥匙 (specialkey)", _inventory.specialkey, ItemId.specialkey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "生锈挂锁钥匙 (rustyPadlockKey)", _inventory.rustyPadlockKey, ItemId.rustyPadlockKey, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "蜘蛛钥匙 (spiderKey)", _inventory.spiderKey, ItemId.spiderKey, scale) + gap;
            curY += gap;

            curY = DrawSubSection(x, curY, width, "武器类", scale);
            curY += DrawItemBtn(x, curY, width, rowH, "旧猎枪 (oldShotgun)", _inventory.oldShotgun, ItemId.oldShotgun, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "猎枪零件1 (gunDel1)", _inventory.gunDel1, ItemId.gunDel1, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "猎枪零件2 (gunDel2)", _inventory.gunDel2, ItemId.gunDel2, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "猎枪零件3 (gunDel3)", _inventory.gunDel3, ItemId.gunDel3, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "枪栓 (topplock)", _inventory.topplock, ItemId.topplock, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "弹药 (ammo)", _inventory.ammo, ItemId.ammo, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "弩 (armborst)", _inventory.armborst, ItemId.armborst, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "箭 (newArrow)", _inventory.newArrow != null ? _inventory.newArrow.gameObject : null, ItemId.arrow, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "辣椒喷雾 (pepperspray)", _inventory.pepperspray, ItemId.pepperspray, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "冰冻陷阱 (freezeTrap)", _inventory.freezeTrap, ItemId.freezeTrap, scale) + gap;
            curY += gap;

            curY = DrawSubSection(x, curY, width, "工具类", scale);
            curY += DrawItemBtn(x, curY, width, rowH, "锯子 (avbitare)", _inventory.avbitare, ItemId.avbitare, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "锤子 (hammare)", _inventory.hammare, ItemId.hammare, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "螺丝刀 (screwdriver)", _inventory.screwdriver, ItemId.screwdriver, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "木板 (planka)", _inventory.planka, ItemId.planka, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "电池 (battery)", _inventory.battery, ItemId.battery, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "扳手 (wrench)", _inventory.wrench, ItemId.wrench, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "链条切割器 (chainCutter)", _inventory.chainCutter, ItemId.chainCutter, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "摇把 (wheelCrank)", _inventory.wheelCrank, ItemId.wheelCrank, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "木棍 (woodenStick)", _inventory.woodenStick, ItemId.woodenStick, scale) + gap;
            curY += gap;

            curY = DrawSubSection(x, curY, width, "车辆零件", scale);
            curY += DrawItemBtn(x, curY, width, rowH, "车电池 (carbattery)", _inventory.carbattery, ItemId.carbattery, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "油桶 (gascan)", _inventory.gascan, ItemId.gascan, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "火花塞 (sparkplug)", _inventory.sparkplug, ItemId.sparkplug, scale) + gap;
            curY += gap;

            curY = DrawSubSection(x, curY, width, "任务物品", scale);
            curY += DrawItemBtn(x, curY, width, rowH, "画板1 (tb1)", _inventory.tb1, ItemId.tb1, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "画板2 (tb2)", _inventory.tb2, ItemId.tb2, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "画板3 (tb3)", _inventory.tb3, ItemId.tb3, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "画板4 (tb4)", _inventory.tb4, ItemId.tb4, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "花瓶 (vas)", _inventory.vas, ItemId.vas, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "花瓶2 (vas2)", _inventory.vas2, ItemId.vas2, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "西瓜 (melon)", _inventory.melon, ItemId.melon, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "泰迪熊 (teddy)", _inventory.teddy, ItemId.teddy, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "齿轮1 (kugg1)", _inventory.kugg1, ItemId.kugg1, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "齿轮2 (kugg2)", _inventory.kugg2, ItemId.kugg2, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "消息 (message)", _inventory.message, ItemId.message, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "井绳 (brunnsvev)", _inventory.brunnsvev, ItemId.brunnsvev, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "肉 (meat)", _inventory.meat, ItemId.meat, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "书 (book)", _inventory.book, ItemId.book, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "遥控器 (remote)", _inventory.remote, ItemId.remote, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "鸟食 (birdSeed)", _inventory.birdSeed, ItemId.birdSeed, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "死老鼠 (deadRat)", _inventory.deadRat, ItemId.deadRat, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "圣诞球 (christmasBall)", _inventory.christmasBall, ItemId.christmasBall, scale) + gap;
            curY += DrawItemBtn(x, curY, width, rowH, "圣诞炸弹 (christmasKulaBomb)", _inventory.christmasKulaBomb, ItemId.christmasKulaBomb, scale) + gap;

            return curY - y;
        }

        // ===== 车辆标签页 =====
        private float DrawVehicleTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, $"车辆 (checkTheCar) [{StatusText(_checkCar)}]", scale);
            curY += gap;
            ModState.CarReadyAll = DrawToggle(x, curY, width, rowH, "一键备齐车辆零件", ModState.CarReadyAll, scale); curY += rowH + gap;
            curY += gap;
            if (_checkCar != null)
            {
                DrawInfo(x, curY, width, rowH, $"电池: {(_checkCar.batteryOK ? "OK" : "X")} | 火花塞: {(_checkCar.sparkplugOK ? "OK" : "X")}", scale); curY += rowH + gap;
                DrawInfo(x, curY, width, rowH, $"油: {(_checkCar.fuelOK ? "OK" : "X")} | 顶盖: {(_checkCar.topplockOK ? "OK" : "X")}", scale); curY += rowH + gap;
                DrawInfo(x, curY, width, rowH, $"车钥匙: {(_checkCar.playerHaveCarKey ? "有" : "无")} | 引擎: {(_checkCar.engineOn ? "开" : "关")}", scale); curY += rowH + gap;
                curY += gap;
            }
            if (DrawButton(x, curY, width, rowH, "立即启动车辆 (startCar)", scale)) TriggerStartCar(); curY += rowH + gap;
            curY += gap;

            curY = DrawSection(x, curY, width, $"电梯 (elevatorController) [{StatusText(_elevator)}]", scale);
            curY += gap;
            if (_elevator != null)
            {
                DrawInfo(x, curY, width, rowH, $"电梯已下降: {_elevator.ElevatorIsDown} | 门关: {_elevator.DoorsClosed}", scale); curY += rowH + gap;
                curY += gap;
            }
            if (DrawButton(x, curY, width, rowH, "呼叫电梯 (CallElevatorDown)", scale)) TriggerElevatorCall(); curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "电梯运行 (ElevatorGo)", scale)) TriggerElevatorGo(); curY += rowH + gap;
            if (DrawButton(x, curY, width, rowH, "电梯开关门 (DoorsCloseOpen)", scale)) TriggerElevatorDoors(); curY += rowH + gap;

            return curY - y;
        }

        // ===== 关于标签页 =====
        private float DrawAboutTab(float x, float y, float width, float scale)
        {
            float curY = y;
            float rowH = RowHeight * scale;
            float gap = RowGap * scale;

            curY = DrawSection(x, curY, width, "关于", scale);
            curY += gap;
            DrawInfo(x, curY, width, rowH, $"Granny Mod Menu v{GrannyModMenuPlugin.PluginVersion}", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, "引擎: Unity6 + IL2CPP + D3D12", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, "渲染: Unity IMGUI (OnGUI)", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, "框架: BepInEx IL2CPP v6 + Il2CppInterop", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, $"菜单切换键: {GrannyModMenuPlugin.MenuToggleKey.Value}", scale); curY += rowH + gap;
            curY += gap;
            DrawInfo(x, curY, width, rowH, "功能通过每帧应用状态实现", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, "物品生成在准星前方, 自由下坠", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, "支持敌人: Granny/Spider/MomSpider/Crow/Rat/Santa", scale); curY += rowH + gap;
            DrawInfo(x, curY, width, rowH, "地图: 全图变亮/关雾/启用所有光源", scale); curY += rowH + gap;

            return curY - y;
        }

        // ==========================================
        // === 辅助绘制方法 (无 Action 参数, 用返回值) ===
        // ==========================================

        /// <summary>创建 GUIStyle (align: 0=Left, 1=Center, 2=Right; bold: 是否粗体)</summary>
        private GUIStyle MakeStyle(int fontSize, bool bold, int align, Color color)
        {
            var style = new GUIStyle();
            style.fontSize = (int)(fontSize * _scale);
            if (bold) style.fontStyle = FontStyle.Bold;
            style.alignment = align == 0 ? TextAnchor.MiddleLeft : (align == 1 ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight);
            style.normal.textColor = color;
            return style;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawRectBorder(Rect rect, Color color)
        {
            float t = 1f;
            DrawRect(new Rect(rect.x, rect.y, rect.width, t), color);
            DrawRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);
            DrawRect(new Rect(rect.x, rect.y, t, rect.height), color);
            DrawRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);
        }

        private float DrawSection(float x, float y, float width, string title, float scale)
        {
            float h = 30f * scale;
            // 可见性检查 (跳过不可见行)
            if (IsVisible(y, h))
            {
                Rect rect = new Rect(x, y, width, h);
                DrawRect(rect, SectionBgColor);
                DrawRect(new Rect(x, y, 3 * scale, h), AccentColor);
                DrawRect(new Rect(x, y + h, width, 1), BorderColor);
                GUI.Label(new Rect(x + 12 * scale, y, width - 16 * scale, h), title, MakeStyle(13, true, 0, AccentColor));
            }
            return y + h;
        }

        private float DrawSubSection(float x, float y, float width, string title, float scale)
        {
            float h = 22f * scale;
            if (IsVisible(y, h))
            {
                DrawRect(new Rect(x, y + h - 1, width, 1), AccentDimColor);
                GUI.Label(new Rect(x + 4 * scale, y + 2, width - 8 * scale, h), title, MakeStyle(12, true, 0, AccentDimColor));
            }
            return y + h;
        }

        /// <summary>检查行是否在可见区域内</summary>
        private bool IsVisible(float y, float h)
        {
            return y + h >= _clipY && y <= _clipY + _clipH;
        }

        /// <summary>开关行 - 返回新值 (点击则取反, 不点击则原值)</summary>
        private bool DrawToggle(float x, float y, float width, float height, string label, bool value, float scale)
        {
            // 不可见时直接返回原值 (不绘制, 不响应点击)
            if (!IsVisible(y, height)) return value;

            Rect rowRect = new Rect(x, y, width, height);
            bool hover = rowRect.Contains(_mousePos);
            if (hover) DrawRect(rowRect, HoverColor);

            float stateW = 55 * scale;
            GUI.Label(new Rect(x + 10 * scale, y, stateW, height), value ? "[ON]" : "[OFF]", MakeStyle(12, true, 0, value ? OnColor : OffColor));
            GUI.Label(new Rect(x + 10 * scale + stateW, y, width - stateW - 20 * scale, height), label, MakeStyle(12, false, 0, TextColor));

            if (GUI.Button(rowRect, "", GUIStyle.none))
                return !value;
            return value;
        }

        /// <summary>滑块行 - 返回新值</summary>
        private float DrawSlider(float x, float y, float width, float height, string label, float value, float min, float max, float scale)
        {
            if (!IsVisible(y, height)) return value;

            GUI.Label(new Rect(x + 10 * scale, y, width - 20 * scale, height * 0.4f), $"{label}: {value:F1}", MakeStyle(11, false, 0, TextDimColor));
            float newVal = GUI.HorizontalSlider(new Rect(x + 10 * scale, y + height * 0.5f, width - 20 * scale, height * 0.5f), value, min, max);
            return newVal;
        }

        /// <summary>按钮 - 返回是否点击</summary>
        private bool DrawButton(float x, float y, float width, float height, string label, float scale)
        {
            if (!IsVisible(y, height)) return false;

            Rect btnRect = new Rect(x, y, width, height);
            bool hover = btnRect.Contains(_mousePos);

            DrawRect(btnRect, hover ? HoverColor : BtnBgColor);
            DrawRectBorder(btnRect, BorderColor);
            GUI.Label(btnRect, label, MakeStyle(12, true, 1, hover ? AccentColor : TextColor));
            return GUI.Button(btnRect, "", GUIStyle.none);
        }

        private float DrawInfo(float x, float y, float width, float height, string text, float scale)
        {
            if (!IsVisible(y, height)) return height;

            GUI.Label(new Rect(x + 10 * scale, y, width - 20 * scale, height), text, MakeStyle(11, false, 0, TextDimColor));
            return height;
        }

        private float DrawItemBtn(float x, float y, float width, float height, string label, GameObject prefab, ItemId itemId, float scale)
        {
            bool available = prefab != null;
            // 不可见时直接返回 (不绘制, 不响应点击)
            if (!IsVisible(y, height)) return height;

            Rect btnRect = new Rect(x, y, width, height);
            bool hover = btnRect.Contains(_mousePos);

            Color bg = available ? (hover ? HoverColor : new Color(0.14f, 0.16f, 0.22f, 1f)) : new Color(0.10f, 0.10f, 0.12f, 0.5f);
            DrawRect(btnRect, bg);
            DrawRectBorder(btnRect, BorderColor);

            string display = available ? $"  {label}" : $"  {label}  (无)";
            GUI.Label(new Rect(x + 8 * scale, y, width - 16 * scale, height), display, MakeStyle(11, false, 0, available ? (hover ? AccentColor : TextColor) : TextDimColor));

            if (GUI.Button(btnRect, "", GUIStyle.none) && available)
                SpawnItem(prefab, itemId, label);

            return height;
        }

        private string StatusText(UnityEngine.Object obj) => obj != null ? "已找到" : "未找到";
    }
}
