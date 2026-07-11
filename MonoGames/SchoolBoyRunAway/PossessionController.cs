using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace SchoolBoyRunawayModMenu
{
    /// <summary>
    /// 变身控制器
    /// 关键设计:
    /// 1. 只禁用 FP_Control(enableController=false),不禁用其他玩家脚本,避免破坏玩家隐蔽状态
    /// 2. 相机定位在 LateUpdate 中执行,覆盖 FP_Head 的相机控制
    /// 3. 克隆体在 SetActive(false) 状态下禁用所有 MonoBehaviour,防止 Awake/Update 干扰
    /// 4. 使用正确的 Animator 参数名 "MovementSpeed"
    /// 5. 保留 CharacterController 碰撞,防止穿墙
    /// </summary>
    public class PossessionController : MonoBehaviour
    {
        public static PossessionController Instance { get; private set; }

        private GameObject _cloneObject;
        private Animator _cloneAnimator;
        private Type _cloneAiType;
        private string _cloneAiName;

        // 玩家控制器原始状态
        private bool _savedEnableController;
        private Camera _playerCamera;

        // 头部渲染器(第一人称时隐藏)
        private List<Renderer> _headRenderers = new List<Renderer>();

        // 移动参数
        private float _moveSpeed = 3f;
        private float _cameraHeight = 1.7f;
        private float _cameraThirdPersonDistance = 3f;
        private float _yaw;
        private float _pitch;

        // 视角模式
        public bool IsFirstPerson { get; private set; } = true;

        // 碰撞
        private CharacterController _cloneController;

        private bool _moveForward, _moveBackward, _moveLeft, _moveRight, _runMode;

        public bool IsPossessing => _cloneObject != null;
        public string CurrentAiName => _cloneAiName ?? "无";
        public GameObject CurrentClone => _cloneObject;

        private void Awake()
        {
            Instance = this;
        }

        public bool Possess(MonoBehaviour aiTarget)
        {
            if (aiTarget == null || aiTarget.gameObject == null) return false;
            try
            {
                if (IsPossessing) ExitPossession();

                var type = aiTarget.GetType();

                // 临时清除单例 _instance,防止 Awake 中 Destroy(gameObject)
                var instanceField = type.GetField("_instance",
                    BindingFlags.NonPublic | BindingFlags.Static);
                object originalInstance = null;
                if (instanceField != null)
                {
                    originalInstance = instanceField.GetValue(null);
                    instanceField.SetValue(null, null);
                }

                _cloneObject = Instantiate(aiTarget.gameObject);
                _cloneObject.name = aiTarget.gameObject.name + "_PlayerClone";
                _cloneAiName = type.Name;
                _cloneAiType = type;

                if (instanceField != null)
                    instanceField.SetValue(null, originalInstance);

                _cloneObject.SetActive(false);

                // 禁用克隆体上所有 MonoBehaviour(保留 Animator/Renderer)
                foreach (var b in _cloneObject.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (b == null) continue;
                    try { b.enabled = false; } catch { }
                }

                // 将克隆体及其所有子物体的 layer 改为默认层(0),tag 改为 Untagged
                // 防止 AI_Vision 的射线检测命中克隆体(双保险,即使 AI_Vision 已禁用)
                _cloneObject.tag = "Untagged";
                foreach (var tr in _cloneObject.GetComponentsInChildren<Transform>(true))
                {
                    if (tr == null) continue;
                    tr.gameObject.tag = "Untagged";
                    tr.gameObject.layer = 0;
                }

                // 禁用 NavMeshAgent
                var agent = _cloneObject.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                // 碰撞处理
                var rb = _cloneObject.GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

                // 移除原 Collider,添加 CharacterController
                var existingColliders = _cloneObject.GetComponents<Collider>();
                foreach (var col in existingColliders)
                {
                    if (col is CharacterController) continue;
                    DestroyImmediate(col);
                }

                _cloneController = _cloneObject.GetComponent<CharacterController>();
                if (_cloneController == null)
                    _cloneController = _cloneObject.AddComponent<CharacterController>();
                _cloneController.height = 1.8f;
                _cloneController.radius = 0.3f;
                _cloneController.center = new Vector3(0, 0.9f, 0);
                _cloneController.slopeLimit = 60f;
                _cloneController.stepOffset = 0.3f;

                _cloneAnimator = _cloneObject.GetComponentInChildren<Animator>(true);
                // 确保动画器始终更新(即使骨骼不在视野内)
                if (_cloneAnimator != null)
                {
                    _cloneAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    _cloneAnimator.updateMode = AnimatorUpdateMode.Normal;
                    // 游戏需要 IsMovement=true 才会播放移动动画(DadReset 中设置)
                    try { _cloneAnimator.SetBool("IsMovement", true); } catch { }
                }

                // 识别头部渲染器(基于骨骼层级 + 命名)
                _headRenderers.Clear();
                Transform headBone = null;
                var allTransforms = _cloneObject.GetComponentsInChildren<Transform>(true);
                foreach (var tr in allTransforms)
                {
                    if (tr == null) continue;
                    string n = tr.name.ToLower();
                    if (n == "head" || n == "headtop" || n == "head_end" ||
                        n == "neck" || n == "head_top")
                    {
                        headBone = tr;
                        break;
                    }
                }

                var allRenderers = _cloneObject.GetComponentsInChildren<Renderer>(true);
                foreach (var r in allRenderers)
                {
                    if (r == null) continue;
                    bool isHead = false;
                    string nameLower = r.gameObject.name.ToLower();

                    if (nameLower.Contains("head") || nameLower.Contains("face") ||
                        nameLower.Contains("eye") || nameLower.Contains("hair") ||
                        nameLower.Contains("mouth") || nameLower.Contains("nose") ||
                        nameLower.Contains("teeth") || nameLower.Contains("jaw") ||
                        nameLower.Contains("skull") || nameLower.Contains("brain") ||
                        nameLower.Contains("brow") || nameLower.Contains("lid") ||
                        nameLower.Contains("pupil") || nameLower.Contains("iris") ||
                        nameLower.Contains("tongue") || nameLower.Contains("beard"))
                    {
                        isHead = true;
                    }

                    if (!isHead && headBone != null)
                    {
                        var p = r.transform.parent;
                        while (p != null)
                        {
                            if (p == headBone) { isHead = true; break; }
                            p = p.parent;
                        }
                    }

                    if (isHead) _headRenderers.Add(r);
                }

                // 生成位置:基于玩家脚下,射线检测地面
                var ctrl = GameHelpers.FP_Control;
                Vector3 spawnPos;
                if (ctrl != null)
                {
                    spawnPos = ctrl.transform.position;
                    _yaw = ctrl.transform.eulerAngles.y;
                }
                else
                {
                    spawnPos = Vector3.zero;
                    _yaw = 0f;
                }

                if (Physics.Raycast(spawnPos + Vector3.up * 0.5f, Vector3.down, out var hit, 5f))
                    spawnPos = hit.point;

                _cloneObject.transform.position = spawnPos;
                _cloneObject.transform.rotation = Quaternion.Euler(0, _yaw, 0);
                _pitch = 0f;

                DontDestroyOnLoad(_cloneObject);
                _cloneObject.SetActive(true);

                ApplyViewMode();

                // 只禁用 FP_Control 的 enableController,不禁用其他玩家脚本
                // 这样玩家的隐蔽/光照状态不会被打断,AI不会因此检测到玩家
                DisablePlayerControlOnly();

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModMenu] 变身失败: {e}");
                if (_cloneObject != null) Destroy(_cloneObject);
                _cloneObject = null;
                return false;
            }
        }

        public void ExitPossession()
        {
            RestorePlayerControl();

            if (_cloneObject != null) Destroy(_cloneObject);
            _cloneObject = null;
            _cloneAnimator = null;
            _cloneAiType = null;
            _cloneAiName = null;
            _cloneController = null;
            _headRenderers.Clear();
        }

        public void ToggleViewMode()
        {
            IsFirstPerson = !IsFirstPerson;
            ApplyViewMode();
        }

        private void ApplyViewMode()
        {
            foreach (var r in _headRenderers)
            {
                if (r != null) r.enabled = !IsFirstPerson;
            }
        }

        /// <summary>
        /// 只禁用 FP_Control.enableController,保留其他玩家脚本运行。
        /// 这样玩家的光照/隐蔽状态不被破坏,AI 不会因脚本禁用而触发检测。
        /// 相机由 LateUpdate 覆盖控制。
        /// </summary>
        private void DisablePlayerControlOnly()
        {
            var ctrl = GameHelpers.FP_Control;
            if (ctrl != null)
            {
                _savedEnableController = ctrl.enableController;
                ctrl.EnableController(false);
            }

            var ps = GameHelpers.PlayerState;
            if (ps != null && ps.fpCamera != null)
                _playerCamera = ps.fpCamera;
            else
                _playerCamera = Camera.main;
        }

        private void RestorePlayerControl()
        {
            var ctrl = GameHelpers.FP_Control;
            if (ctrl != null && _savedEnableController)
            {
                ctrl.EnableController(true);
            }
            _savedEnableController = false;
            _playerCamera = null;
        }

        private void Update()
        {
            if (!IsPossessing || _cloneObject == null) return;

            if (Input.GetKeyDown(KeyCode.V))
                ToggleViewMode();

            HandleInput();
            HandleMovement();
        }

        /// <summary>
        /// 在 LateUpdate 中定位相机,确保覆盖 FP_Head 等脚本的相机控制。
        /// </summary>
        private void LateUpdate()
        {
            if (!IsPossessing || _cloneObject == null) return;
            HandleCamera();
        }

        private void HandleInput()
        {
            _moveForward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            _moveBackward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            _moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            _moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            _runMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 变身期间鼠标始终控制视角
            _yaw += Input.GetAxis("Mouse X") * 3f;
            _pitch -= Input.GetAxis("Mouse Y") * 2f;
            _pitch = Mathf.Clamp(_pitch, -60f, 60f);
        }

        private void HandleMovement()
        {
            if (_cloneObject == null || _cloneController == null) return;

            var forward = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;

            var moveDir = Vector3.zero;
            if (_moveForward) moveDir += forward;
            if (_moveBackward) moveDir -= forward;
            if (_moveRight) moveDir += right;
            if (_moveLeft) moveDir -= right;

            if (moveDir != Vector3.zero)
            {
                moveDir.Normalize();
                var speed = _runMode ? _moveSpeed * 2f : _moveSpeed;

                Vector3 motion = moveDir * speed * Time.deltaTime;
                motion.y = -9.8f * Time.deltaTime;
                _cloneController.Move(motion);

                var targetRot = Quaternion.LookRotation(moveDir);
                _cloneObject.transform.rotation = Quaternion.Slerp(
                    _cloneObject.transform.rotation, targetRot, 8f * Time.deltaTime);

                // 游戏的 MovementSpeed 是归一化值(0-1):agentDad.velocity.magnitude / 3f
                // 走路 ~1m/s → 0.33, 跑步 ~2m/s → 0.67, 全速 ~3m/s → 1.0
                if (_cloneAnimator != null)
                {
                    float normalizedSpeed = Mathf.Clamp01(speed / 3f);
                    _cloneAnimator.SetFloat("MovementSpeed", normalizedSpeed);
                }
            }
            else
            {
                Vector3 gravityMotion = Vector3.up * (-9.8f * Time.deltaTime);
                _cloneController.Move(gravityMotion);

                if (_cloneAnimator != null)
                {
                    _cloneAnimator.SetFloat("MovementSpeed", 0f);
                }
            }
        }

        private void HandleCamera()
        {
            if (_playerCamera == null || _cloneObject == null) return;

            var clonePos = _cloneObject.transform.position;
            var camRotation = Quaternion.Euler(_pitch, _yaw, 0);

            if (IsFirstPerson)
            {
                _playerCamera.transform.position = clonePos + new Vector3(0, _cameraHeight, 0);
                _playerCamera.transform.rotation = camRotation;
            }
            else
            {
                var camDir = camRotation * Vector3.back;
                var targetPos = clonePos + new Vector3(0, _cameraHeight, 0) + camDir * _cameraThirdPersonDistance;

                var headPos = clonePos + new Vector3(0, _cameraHeight, 0);
                if (Physics.Raycast(headPos, camDir, out var hit, _cameraThirdPersonDistance))
                    targetPos = hit.point + camDir * -0.2f;

                _playerCamera.transform.position = targetPos;
                _playerCamera.transform.rotation = camRotation;
            }
        }

        public void TriggerAttack()
        {
            if (_cloneAnimator != null)
            {
                try
                {
                    // 使用游戏原生的 Animator 触发器名称
                    _cloneAnimator.SetTrigger("AngryC");
                    _cloneAnimator.SetTrigger("Swears");
                    _cloneAnimator.SetTrigger("GestureA");
                } catch { }
            }
        }

        public void TriggerInteract()
        {
            if (_cloneAnimator != null)
            {
                try
                {
                    _cloneAnimator.SetTrigger("Check");
                    _cloneAnimator.SetTrigger("LookAround");
                } catch { }
            }
        }

        public void SetMoveSpeed(float speed) { _moveSpeed = Mathf.Max(0.1f, speed); }
    }
}
