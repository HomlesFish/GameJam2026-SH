using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 船（玩家）控制器 —— 俯视视角下的惯性船控。
    ///
    /// 规则：
    ///   1. 加速：按住 W，速度持续增加，直到达到 <see cref="maxSpeed"/>。
    ///   2. 转向：按住 A/D，经过 <see cref="turnDelay"/> 秒反应延迟后，开始以
    ///            <see cref="turnRate"/> 的角速度持续转向；松开立即停止。
    ///            点按（按住不足延迟时间）不产生转向。
    ///   3. 抛锚：按下 <see cref="anchorKey"/>，速度在 <see cref="anchorDuration"/> 秒内线性降到 0；
    ///            抛锚期间 W/A/D 均无效。
    ///
    /// 实现：Transform + 手写速度积分（不用 Rigidbody），便于精确控制延迟与线性减速。
    /// 船贴图请以 +Y 方向作为“船头”。
    /// </summary>
    public class BoatController : MonoBehaviour
    {
        [Header("推进")]
        [Tooltip("加速度（单位/秒²）。按住 W 时每秒增加的速度。")]
        [SerializeField] private float acceleration = 3f;

        [Tooltip("最大速度（单位/秒）。加速到此值后不再增加。")]
        [SerializeField] private float maxSpeed = 6f;

        [Header("转向")]
        [Tooltip("转向角速度（度/秒）。延迟结束后每秒转向的角度。")]
        [SerializeField] private float turnRate = 90f;

        [Tooltip("转向反应延迟（秒）。按下 A/D 后需持续按住这么久才开始转向。")]
        [SerializeField] private float turnDelay = 1f;

        [Header("抛锚")]
        [Tooltip("抛锚减速时长（秒）。速度在此时间内线性降到 0。")]
        [SerializeField] private float anchorDuration = 2f;

        [Tooltip("抛锚按键。")]
        [SerializeField] private KeyCode anchorKey = KeyCode.Space;

        [Header("运行时状态（调试只读）")]
        [SerializeField] private float currentSpeed;
        [SerializeField] private float heading;          // 船头朝向（度），0 = 指向 +Y
        [SerializeField] private bool anchoring;

        // 转向计时：每个方向各自累计按住时长，达到 turnDelay 后 engaged
        private float _leftTimer;
        private float _rightTimer;
        private bool _leftEngaged;
        private bool _rightEngaged;

        // 抛锚
        private float _anchorElapsed;
        private float _anchorStartSpeed;

        private void Awake()
        {
            // 以对象当前朝向作为初始 heading，避免与编辑器里设置的旋转冲突
            heading = transform.eulerAngles.z;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (anchoring)
            {
                UpdateAnchor(dt);
            }
            else if (Input.GetKeyDown(anchorKey) && currentSpeed > 0.0001f)
            {
                // 抛锚优先：一旦触发即进入减速，期间忽略推进/转向
                BeginAnchor();
            }
            else
            {
                HandleThrust(dt);
                HandleTurning(dt);
            }

            ApplyMotion(dt);
        }

        /// <summary>加速：按住 W（或上方向键）时累加速度，封顶 maxSpeed。松开不自动减速。</summary>
        private void HandleThrust(float dt)
        {
            bool thrust = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            if (thrust)
            {
                currentSpeed += acceleration * dt;
                if (currentSpeed > maxSpeed) currentSpeed = maxSpeed;
            }
        }

        /// <summary>转向：A/D 各自计时，持续按住超过 turnDelay 后开始持续转向。</summary>
        private void HandleTurning(float dt)
        {
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            _leftEngaged = TickTurn(left, ref _leftTimer, dt);
            _rightEngaged = TickTurn(right, ref _rightTimer, dt);

            // D（右）增大 heading（顺时针，朝 +X），A（左）减小
            int dir = (_rightEngaged ? 1 : 0) - (_leftEngaged ? 1 : 0);
            if (dir != 0)
            {
                heading += turnRate * dir * dt;
            }
        }

        /// <summary>按住计时：持续按住累计时间，达到 turnDelay 返回 true；松开则清零。</summary>
        private bool TickTurn(bool held, ref float timer, float dt)
        {
            if (held)
            {
                timer += dt;
                return timer >= turnDelay;
            }
            timer = 0f;
            return false;
        }

        private void BeginAnchor()
        {
            anchoring = true;
            _anchorElapsed = 0f;
            _anchorStartSpeed = currentSpeed;
            // 重置转向计时，避免减速结束后带着残余的“待生效”转向
            _leftTimer = _rightTimer = 0f;
            _leftEngaged = _rightEngaged = false;
        }

        /// <summary>抛锚减速：在 anchorDuration 秒内把速度线性降到 0。</summary>
        private void UpdateAnchor(float dt)
        {
            _anchorElapsed += dt;
            float t = Mathf.Clamp01(_anchorElapsed / anchorDuration);
            currentSpeed = _anchorStartSpeed * (1f - t);

            if (t >= 1f)
            {
                currentSpeed = 0f;
                anchoring = false;
            }
        }

        /// <summary>把当前速度与朝向应用到 Transform。</summary>
        private void ApplyMotion(float dt)
        {
            // heading=0 → forward=(0,1) 指向 +Y；递增 heading 朝 +X（右）转
            float rad = heading * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

            transform.position += (Vector3)(forward * (currentSpeed * dt));
            // heading 的增量约定为顺时针（与上方 forward=(sinθ,cosθ) 一致）；
            // Unity 的 +Z 旋转是逆时针，故这里取负，使精灵与行进方向同向。
            transform.eulerAngles = new Vector3(0f, 0f, -heading);
        }

        // ===== 预留扩展点（后续阶段填充）=====

        /// <summary>交互（如发射声呐 ping）。留给后续声呐系统调用。</summary>
        public virtual void Interact() { /* TODO: 声呐等 */ }

        /// <summary>受击。返回是否成功受到伤害（可用于无敌帧判断）。</summary>
        public virtual bool TakeDamage(int amount) { /* TODO: 按需求实现 */ return false; }
    }
}
