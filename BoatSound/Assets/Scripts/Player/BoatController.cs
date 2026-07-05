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
    ///   3. 抛锚：点击 UI 抛锚按钮（<see cref="BeginAnchor"/>），速度在 <see cref="anchorDuration"/> 秒内线性降到 0；
    ///            抛锚期间 W/A/D 均无效。
    ///
    /// 实现：Transform + 手写速度积分（不用 Rigidbody），便于精确控制延迟与线性减速。
    /// 船贴图请以 +Y 方向作为“船头”。
    /// </summary>
    public class BoatController : MonoBehaviour, IGameEventListener
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

        [Tooltip("碰撞暗礁后的免疫时长（秒），防止重叠时持续掉电")]
        [SerializeField] private float reefHitCooldown = 1f;

        [Tooltip("打捞有效半径（单位）。按打捞时捡起此半径内最近的支线物品。")]
        [SerializeField] private float salvageRange = 1.5f;

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

        // 暗礁碰撞免疫冷却
        private float _reefCooldown;

        // 外部冻结（如入港确认面板弹出期间）：true 时瞬间停船并忽略全部输入
        private bool _frozen;

        // 字幕 id（需在 SubtitleDatabase 里建同名条目）
        private const string SubtitleHitReef = "HitReef";
        private const string SubtitleAnchored = "Anchored";

        private void Awake()
        {
            // 以对象当前朝向作为初始 heading，避免与编辑器里设置的旋转冲突
            heading = transform.eulerAngles.z;
        }

        private void Update()
        {
            // 仅在关卡进行中响应输入与移动：暂停 / 单关结算 / 全通关 / 主菜单期间一律冻结。
            // 否则结算确认把船传送到 endport 后，残留的 currentSpeed 会在下一帧把船推出港口
            // （LevelSettled 时 timeScale 仍为 1，Update 照跑），看起来像「没到 endport / 又跑掉了」。
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            // 外部冻结（如入港确认面板弹出期间）：瞬间停船并忽略推进 / 转向 / 抛锚输入
            if (_frozen) return;

            float dt = Time.deltaTime;

            if (anchoring)
            {
                UpdateAnchor(dt);
            }
            else
            {
                HandleThrust(dt);
                HandleTurning(dt);
            }

            ApplyMotion(dt);
            if (_reefCooldown > 0f) _reefCooldown -= dt;
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

        /// <summary>
        /// 抛锚操作：UI 抛锚按钮的 OnClick 可直接绑定本方法（与 <see cref="Salvage"/> 同样的绑定方式）。
        /// 仅在关卡进行中、船正在移动、且当前未抛锚时生效；触发后在 <see cref="anchorDuration"/> 秒内
        /// 把速度线性降到 0，期间推进 / 转向 / 再次点击均被忽略。
        /// </summary>
        public void BeginAnchor()
        {
            // 与原 Update 中的按键门禁保持一致：非进行中 / 已在抛锚 / 船已停 一律不响应。
            // UI 按钮不经过 Update 的状态门禁，必须在此自检，避免暂停 / 结算期间误触。
            if (anchoring) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
            if (currentSpeed <= 0.0001f) return;

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
                // 抛锚触底字幕提示（每次抛锚完成只触发一次）
                if (SubtitleManager.Instance != null) SubtitleManager.Instance.Show(SubtitleAnchored);
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

        // ===== 打捞 / 碰撞 / 失败复活 =====

        /// <summary>
        /// 打捞操作：**每次点击都扣 5 点电量**。若 <see cref="salvageRange"/> 内有可打捞的雷达目标
        /// （SonarTarget，排除暗礁等 salvageable=false）且货舱未满，则同时捡取入舱。
        /// 货舱满 / 附近无可打捞物时仍扣电、但不捡。UI 打捞按钮的 OnClick 可直接绑定本方法。
        /// </summary>
        public void Salvage()
        {
            SalvageInventory inventory = SalvageInventory.Instance;
            bool salvagedObjective = false;   // 本次是否打捞到主线目标（用于触发该关剧情字幕）

            // 先尝试捡取（若有货舱、有目标、有空间）；这样即便随后扣电导致复活，也已先拿到物品
            if (inventory != null)
            {
                SonarTarget target = FindNearestSalvageableTarget();
                if (target != null && inventory.TryAdd(target.ItemName, target.Score, target.IsObjective))
                {
                    salvagedObjective = target.IsObjective;
                    Destroy(target.gameObject);
                }
            }

            // 只要点击打捞就要扣电量（无论是否捡到东西）
            if (BatteryManager.Instance != null) BatteryManager.Instance.ConsumeSalvage();

            // 打捞到主线目标：按当前关卡顺序播放对应剧情字幕
            if (salvagedObjective) PlayLevelStorySequence();
        }

        /// <summary>取当前关卡的剧情字幕 id 并交给 SubtitleManager 顺序播放；未配置 / 无 SubtitleManager 则空操作。</summary>
        private void PlayLevelStorySequence()
        {
            if (SubtitleManager.Instance == null || GameManager.Instance == null) return;
            string[] ids = GameManager.Instance.GetLevelStorySubtitleIds(GameManager.Instance.CurrentLevelIndex);
            if (ids != null && ids.Length > 0)
                SubtitleManager.Instance.ShowSequence(ids);
        }

        /// <summary>找船周围 <see cref="salvageRange"/> 内最近的、可打捞的雷达目标（排除暗礁等 salvageable=false）；没有则返回 null。</summary>
        private SonarTarget FindNearestSalvageableTarget()
        {
            float sqrRange = salvageRange * salvageRange;
            Vector2 origin = transform.position;

            SonarTarget nearest = null;
            float nearestSqr = sqrRange;
            foreach (SonarTarget t in Object.FindObjectsByType<SonarTarget>(FindObjectsSortMode.None))
            {
                if (t == null || !t.Salvageable) continue;       // 排除暗礁等不可打捞物
                float sqrDist = ((Vector2)t.transform.position - origin).sqrMagnitude;
                if (sqrDist <= nearestSqr)
                {
                    nearestSqr = sqrDist;
                    nearest = t;
                }
            }
            return nearest;
        }

        /// <summary>碰撞暗礁：受 reefHitCooldown 免疫保护，避免重叠期间持续掉电。需船挂 Rigidbody2D(Kinematic)+Collider2D、暗礁挂 Collider2D(isTrigger) 并打 Tag "Reef"。</summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Reef")) return;
            if (_reefCooldown > 0f) return;

            _reefCooldown = reefHitCooldown;
            if (BatteryManager.Instance != null) BatteryManager.Instance.ConsumeReefHit();

            // 触礁字幕提示
            if (SubtitleManager.Instance != null) SubtitleManager.Instance.Show(SubtitleHitReef);
        }

        // ===== 失败复活：监听电量耗尽，复活时把船停住 =====

        private void OnEnable()
        {
            EventBus.Subscribe(GameEvents.PlayerDied, this);
            EventBus.Subscribe(GameEvents.LevelStarted, this);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(GameEvents.PlayerDied, this);
            EventBus.Unsubscribe(GameEvents.LevelStarted, this);
        }

        /// <inheritdoc />
        public void OnEventRaised(string eventId)
        {
            if (eventId == GameEvents.PlayerDied)
            {
                // 复活：船已被 GameManager 传送回出生点，这里清零运动状态
                ResetMotion();
            }
            else if (eventId == GameEvents.LevelStarted)
            {
                // 换关：船被传送到新关出生点，清零运动状态避免带速漂移
                ResetMotion();
            }
        }

        /// <summary>清零全部运动状态（速度、抛锚、转向计时、暗礁冷却）并解除外部冻结。复活 / 换关时调用。</summary>
        public void ResetMotion()
        {
            ClearMotionState();
            _frozen = false;
        }

        /// <summary>清零运动数值（不动 <see cref="_frozen"/>）。</summary>
        private void ClearMotionState()
        {
            currentSpeed = 0f;
            anchoring = false;
            _leftTimer = _rightTimer = 0f;
            _leftEngaged = _rightEngaged = false;
            _reefCooldown = 0f;
        }

        /// <summary>
        /// 外部冻结开关：<paramref name="value"/>=true 时瞬间把船停住并忽略全部输入（推进 / 转向 / 抛锚），
        /// 用于入港确认面板弹出期间强制停船；=false 恢复操控。换关 / 复活（<see cref="ResetMotion"/>）会自动解冻。
        /// </summary>
        public void SetFrozen(bool value)
        {
            _frozen = value;
            // 冻结瞬间清零速度，否则船会带着残余 currentSpeed 继续按惯性漂出触发区
            if (value) ClearMotionState();
        }

        // ===== 预留扩展点（后续阶段填充）=====

        /// <summary>交互（如发射声呐 ping）。留给后续声呐系统调用。</summary>
        public virtual void Interact() { /* TODO: 声呐等 */ }

        /// <summary>受击。返回是否成功受到伤害（可用于无敌帧判断）。</summary>
        public virtual bool TakeDamage(int amount) { /* TODO: 按需求实现 */ return false; }
    }
}
