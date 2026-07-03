using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 玩家控制器基础框架。
    /// 处理玩家输入与基础移动，并预留交互 / 受击等扩展点。
    ///
    /// 48小时Jam用法：直接挂在玩家 GameObject 上，WASD/方向键即可移动。
    /// 默认使用 Transform 平移，不强制物理；如需 Rigidbody / CharacterController，
    /// 只需重写 <see cref="Move"/>（见方法内注释）。
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("移动参数（可在 Inspector 直接调整）")]
        [Tooltip("移动速度（单位/秒）")]
        public float moveSpeed = 5f;

        // 缓存本帧输入，供移动使用
        private Vector3 _moveInput;

        private void Update()
        {
            // 在 Update 读取输入，避免漏帧
            _moveInput = GetMoveInput();
            Move(_moveInput);
        }

        /// <summary>读取移动输入。默认为 WASD / 方向键；改摇杆/触屏只需重写此方法。</summary>
        protected virtual Vector3 GetMoveInput()
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 根据输入移动玩家。默认用 Transform 平移。
        /// 扩展提示：
        ///   - 物理(Rigidbody)：改为 rb.MovePosition(...)，并在 FixedUpdate 中调用；
        ///   - 2D 项目：把 Vector3 换成 Vector2、用 transform.Translate 即可；
        ///   - 角色控制器：使用 CharacterController.Move(...)。
        /// </summary>
        protected virtual void Move(Vector3 input)
        {
            if (input.sqrMagnitude > 0.0001f)
            {
                transform.Translate(input.normalized * (moveSpeed * Time.deltaTime), Space.World);
            }
        }

        // ===== 预留扩展点（Jam 过程中按策划需求填充）=====

        /// <summary>玩家交互（拾取、对话、开门等）。可由子类或按键逻辑触发。</summary>
        public virtual void Interact() { /* TODO: 按需求实现 */ }

        /// <summary>玩家受击。返回是否成功受到伤害（可用于无敌帧判断）。</summary>
        public virtual bool TakeDamage(int amount) { /* TODO: 按需求实现 */ return false; }
    }
}
