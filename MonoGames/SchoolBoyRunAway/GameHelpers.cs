using System;
using System.Reflection;
using UnityEngine;
using BepInEx;

namespace SchoolBoyRunawayModMenu
{
    /// <summary>
    /// 游戏对象引用缓存与反射辅助工具
    /// </summary>
    public static class GameHelpers
    {
        // 缓存的游戏对象引用
        private static LinkedSquad.PlayerControls.GameManager _gameManager;
        private static LinkedSquad.PlayerControls.FP_Control _fpControl;
        private static LinkedSquad.PlayerControls.FP_Body _fpBody;
        private static LinkedSquad.PlayerControls.FP_Health _fpHealth;
        private static AI.Player.PlayerState _playerState;
        private static DifficultyLevel _difficultyLevel;

        // 原始速度值缓存(用于倍率重置)
        private static float _origWalkSpeed = -1f;
        private static float _origRunSpeed = -1f;
        private static float _origJumpForce = -1f;
        private static float _origCrouchSpeed = -1f;
        private static float _origClimbingSpeed = -1f;
        private static float _origWaterSpeed = -1f;

        public static LinkedSquad.PlayerControls.GameManager GameManager
        {
            get
            {
                if (_gameManager == null)
                    _gameManager = FindObjectOfType<LinkedSquad.PlayerControls.GameManager>();
                return _gameManager;
            }
        }

        public static AI.Player.PlayerState PlayerState
        {
            get
            {
                if (_playerState == null)
                    _playerState = FindObjectOfType<AI.Player.PlayerState>();
                return _playerState;
            }
        }

        public static DifficultyLevel Difficulty
        {
            get
            {
                if (_difficultyLevel == null)
                    _difficultyLevel = FindObjectOfType<DifficultyLevel>();
                return _difficultyLevel;
            }
        }

        public static LinkedSquad.PlayerControls.FP_Control FP_Control
        {
            get
            {
                if (_fpControl == null)
                {
                    // 优先通过 GameManager 获取(controller字段,私有)
                    if (GameManager != null)
                        _fpControl = GetField<LinkedSquad.PlayerControls.FP_Control>(GameManager, "controller");
                    if (_fpControl == null && PlayerState != null)
                        _fpControl = PlayerState.fP_Control;
                    if (_fpControl == null)
                        _fpControl = FindObjectOfType<LinkedSquad.PlayerControls.FP_Control>();
                }
                return _fpControl;
            }
        }

        public static LinkedSquad.PlayerControls.FP_Body FP_Body
        {
            get
            {
                if (_fpBody == null)
                {
                    if (FP_Control != null)
                        _fpBody = FP_Control._scriptFP_Body;
                    if (_fpBody == null)
                        _fpBody = FindObjectOfType<LinkedSquad.PlayerControls.FP_Body>();
                }
                return _fpBody;
            }
        }

        public static LinkedSquad.PlayerControls.FP_Health FP_Health
        {
            get
            {
                if (_fpHealth == null)
                    _fpHealth = FindObjectOfType<LinkedSquad.PlayerControls.FP_Health>();
                return _fpHealth;
            }
        }

        /// <summary>
        /// 清除缓存(场景切换后调用)
        /// </summary>
        public static void ClearCache()
        {
            _gameManager = null;
            _fpControl = null;
            _fpBody = null;
            _fpHealth = null;
            _playerState = null;
            _difficultyLevel = null;
            _origWalkSpeed = -1f;
            _origRunSpeed = -1f;
            _origJumpForce = -1f;
            _origCrouchSpeed = -1f;
            _origClimbingSpeed = -1f;
            _origWaterSpeed = -1f;
        }

        // ========== 反射辅助 ==========

        public static T GetField<T>(object obj, string fieldName) where T : class
        {
            if (obj == null) return null;
            try
            {
                var type = obj.GetType();
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(obj) as T;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 获取值类型字段(float/int/bool 等),失败返回默认值
        /// </summary>
        public static T GetFieldValue<T>(object obj, string fieldName, T defaultValue = default(T))
        {
            if (obj == null) return defaultValue;
            try
            {
                var type = obj.GetType();
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(T))
                    return (T)field.GetValue(obj);
            }
            catch { }
            return defaultValue;
        }

        /// <summary>获取 float 字段(可空语义:未找到返回 -1)</summary>
        public static float GetFloat(object obj, string fieldName, float notFound = -1f)
        {
            return GetFieldValue<float>(obj, fieldName, notFound);
        }

        public static void SetField(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            try
            {
                var type = obj.GetType();
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    field.SetValue(obj, value);
            }
            catch { }
        }

        public static T GetStaticField<T>(Type type, string fieldName) where T : class
        {
            if (type == null) return null;
            try
            {
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                    return field.GetValue(null) as T;
            }
            catch { }
            return null;
        }

        public static void SetStaticField(Type type, string fieldName, object value)
        {
            if (type == null) return;
            try
            {
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                    field.SetValue(null, value);
            }
            catch { }
        }

        public static bool GetStaticBool(Type type, string fieldName)
        {
            if (type == null) return false;
            try
            {
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null && field.FieldType == typeof(bool))
                    return (bool)field.GetValue(null);
            }
            catch { }
            return false;
        }

        private static T FindObjectOfType<T>() where T : UnityEngine.Object
        {
            try
            {
                // 优先使用 FindFirstObjectOfType(较新API),回退到 FindObjectsOfType
                var found = UnityEngine.Object.FindObjectOfType<T>();
                return found;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 通过类型名查找 MonoBehaviour 实例
        /// </summary>
        public static UnityEngine.Object FindByType(string typeFullName)
        {
            try
            {
                Type type = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(typeFullName);
                    if (type != null) break;
                }
                if (type == null) return null;
                var method = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", Type.EmptyTypes);
                if (method == null)
                {
                    var generic = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", Type.EmptyTypes)
                        ?.MakeGenericMethod(type);
                    var arr = generic?.Invoke(null, null) as Array;
                    return arr?.Length > 0 ? arr.GetValue(0) as UnityEngine.Object : null;
                }
                var genericMethod = method.MakeGenericMethod(type);
                return genericMethod.Invoke(null, null) as UnityEngine.Object;
            }
            catch { return null; }
        }

        // ========== 速度倍率 ==========

        /// <summary>
        /// 缓存原始速度值
        /// </summary>
        public static void CacheOriginalSpeeds()
        {
            var body = FP_Body;
            if (body == null) return;
            if (_origWalkSpeed < 0f) _origWalkSpeed = body.walkSpeed;
            if (_origRunSpeed < 0f) _origRunSpeed = body.runSpeed;
            if (_origJumpForce < 0f) _origJumpForce = body.jumpForce;
            if (_origCrouchSpeed < 0f) _origCrouchSpeed = body.crouchSpeed;
            if (_origClimbingSpeed < 0f) _origClimbingSpeed = body.climbingSpeed;
            if (_origWaterSpeed < 0f) _origWaterSpeed = body.waterSpeed;
        }

        /// <summary>
        /// 应用速度倍率
        /// </summary>
        public static void ApplySpeedMultiplier(float multiplier)
        {
            var body = FP_Body;
            if (body == null || _origWalkSpeed < 0f) return;
            body.walkSpeed = _origWalkSpeed * multiplier;
            body.runSpeed = _origRunSpeed * multiplier;
            body.crouchSpeed = _origCrouchSpeed * multiplier;
            body.climbingSpeed = _origClimbingSpeed * multiplier;
            body.waterSpeed = _origWaterSpeed * multiplier;
        }

        /// <summary>
        /// 重置速度为原始值
        /// </summary>
        public static void ResetSpeeds()
        {
            if (_origWalkSpeed < 0f) return;
            var body = FP_Body;
            if (body == null) return;
            body.walkSpeed = _origWalkSpeed;
            body.runSpeed = _origRunSpeed;
            body.jumpForce = _origJumpForce;
            body.crouchSpeed = _origCrouchSpeed;
            body.climbingSpeed = _origClimbingSpeed;
            body.waterSpeed = _origWaterSpeed;
        }

        public static bool HasCachedSpeeds => _origWalkSpeed >= 0f;

        public static float OrigWalkSpeed => _origWalkSpeed;
        public static float OrigRunSpeed => _origRunSpeed;
        public static float OrigJumpForce => _origJumpForce;
    }
}
