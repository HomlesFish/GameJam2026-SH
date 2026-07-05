using System;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 游戏全局管理器（单例）—— 流程 + 关卡中枢。
    /// 负责整体游戏状态、关卡生命周期（加载 / 结算 / 换关）、失败复活编排，
    /// 并统一持有玩家船 <see cref="Boat"/>（各系统不再各自查找船）。
    ///
    /// 48小时Jam用法：场景里建空 GameObject 挂本脚本，Inspector 配置：
    ///   - levels：关卡序列（LevelData .asset 数组，顺序即关卡顺序）；
    ///   - levelContainer：关卡内容挂载点（空 GameObject，每关 Prefab 实例化在其下）；
    ///   - boat：玩家船（不填则 Awake 自动查找）。
    /// DontDestroyOnLoad 自动生效（跨场景保留，由基类 PersistOnLoad 控制）。
    /// </summary>
    public class GameManager : SingletonBehaviour<GameManager>, IGameEventListener
    {
        protected override bool PersistOnLoad => true;

        /// <summary>
        /// 游戏状态枚举。策划如需新增状态（过场、结算变体），在此添加。
        /// </summary>
        public enum GameState
        {
            MainMenu,      // 主菜单
            Playing,       // 关卡进行中
            Paused,        // 暂停
            LevelSettled,  // 单关结算完成，等待进入下一关
            GameOver       // 全通关（所有关卡完成）
        }

        [Header("关卡")]
        [Tooltip("关卡序列（按顺序通关）。每项是一个 LevelData(.asset)，引用关卡 Prefab")]
        [SerializeField] private LevelData[] levels;

        [Tooltip("关卡内容挂载点（场景里的空 GameObject）。每关 Prefab 实例化为其子物体，换关时清空")]
        [SerializeField] private Transform levelContainer;

        [Header("玩家")]
        [Tooltip("玩家船。不填则 Awake 自动查找场景里的 BoatController")]
        [SerializeField] private BoatController boat;

        /// <summary>玩家船（全局唯一真相源；各系统统一从此取，不再各自持有/查找）。</summary>
        public BoatController Boat { get; private set; }

        /// <summary>当前游戏状态。请通过 <see cref="SetState"/> 修改，以便触发事件。</summary>
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        /// <summary>当前关卡索引（0 起；-1 表示尚未开始任何关卡）。</summary>
        public int CurrentLevelIndex { get; private set; } = -1;

        /// <summary>跨关卡累计总分（每关结算时累加当关货舱分）。</summary>
        public int TotalScore { get; private set; }

        /// <summary>状态变化事件。其他系统订阅即可联动。参数：(新状态, 旧状态)</summary>
        public event Action<GameState, GameState> OnStateChanged;

        private GameObject _currentLevelInstance;  // 当前关 Prefab 实例
        private LevelRoot _currentLevelRoot;       // 当前关锚点（出生/结算点）

        protected override void Awake()
        {
            base.Awake();
            // 解析玩家船：Inspector 拖了用拖的，否则自动查找。
            // Awake 最早执行，保证各系统在 Start 时 Boat 已就绪。
            Boat = boat != null ? boat : FindFirst<BoatController>();
            if (Boat == null)
                Debug.LogWarning("[GameManager] 未找到 BoatController（玩家船）。请拖入 boat 字段或确保场景里有船。", this);
        }

        private void OnEnable() => EventBus.Subscribe(GameEvents.PlayerDied, this);
        private void OnDisable() => EventBus.Unsubscribe(GameEvents.PlayerDied, this);

        /// <inheritdoc />
        public void OnEventRaised(string eventId)
        {
            if (eventId == GameEvents.PlayerDied) HandlePlayerDied();
        }

        /// <summary>切换游戏状态并广播事件。相同状态不会重复触发。
        /// 同时统一管理 <see cref="Time.timeScale"/>：进入 <see cref="GameState.Paused"/> 冻结时间，
        /// 切到任意其它状态都还原为 1（保证从暂停退出到主菜单/结算/重玩时不会卡在慢速）。</summary>
        public void SetState(GameState newState)
        {
            if (newState == CurrentState) return;
            GameState oldState = CurrentState;
            CurrentState = newState;
            Time.timeScale = (newState == GameState.Paused) ? 0f : 1f;
            OnStateChanged?.Invoke(newState, oldState);
        }

        // ===== 关卡流程 =====

        /// <summary>开始游戏：累计分清零，从第 0 关开始。可绑主菜单「开始」按钮 OnClick。</summary>
        public void StartGame()
        {
            TotalScore = 0;
            BeginLevel(0);
        }

        /// <summary>加载并开始第 <paramref name="index"/> 关：
        /// 清空旧关 → 实例化新关 → HarborEntry 就位 → 船传送到出生点 → 重置玩家状态 → 广播 LevelStarted → 进入 Playing。</summary>
        public void BeginLevel(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
            {
                Debug.LogError($"[GameManager] 无法开始关卡 {index}（levels 未配置或越界，共 {levels?.Length ?? 0} 关）。", this);
                return;
            }

            CurrentLevelIndex = index;
            LevelData data = levels[index];

            ClearCurrentLevel();

            if (data.LevelPrefab == null)
            {
                Debug.LogError($"[GameManager] 关卡 {index}（{data.DisplayName}）未配置 levelPrefab，无法加载。", this);
                return;
            }
            if (levelContainer == null)
            {
                Debug.LogError("[GameManager] 未配置 levelContainer（关卡内容挂载点），无法加载关卡。", this);
                return;
            }

            _currentLevelInstance = Instantiate(data.LevelPrefab, levelContainer);
            _currentLevelRoot = _currentLevelInstance.GetComponentInChildren<LevelRoot>();
            if (_currentLevelRoot == null)
                Debug.LogWarning($"[GameManager] 关卡 {index}（{data.DisplayName}）的 Prefab 未挂 LevelRoot，出生/结算点将回退原点。", this);

            // HarborEntryController 常驻场景，每关摆到本关结算点
            PositionHarborEntry();

            // 船就位
            if (Boat != null) Boat.transform.position = CurrentSpawnPosition();

            // 重置玩家状态：电量、弹药、货舱（换关 / 开局一致）
            if (BatteryManager.Instance != null) BatteryManager.Instance.ResetBattery();
            if (DepthChargeLauncher.Instance != null) DepthChargeLauncher.Instance.ResetCharges();
            if (SalvageInventory.Instance != null) SalvageInventory.Instance.Clear();

            Debug.Log($"[GameManager] 开始关卡 {index + 1}/{levels.Length}：{data.DisplayName}");

            EventBus.Raise(GameEvents.LevelStarted);   // SonarUIController 等据此重新解析本关主线目标
            SetState(GameState.Playing);
        }

        /// <summary>单关结算（玩家携带主线目标入港确认后，由 HarborEntryController 调用）：
        /// 把本关货舱分累加到 <see cref="TotalScore"/>，进入 <see cref="GameState.LevelSettled"/> 等待「继续」。</summary>
        public void SettleCurrentLevel()
        {
            // 必须在 BeginLevel 清舱之前读当关货舱分
            int levelScore = SalvageInventory.Instance != null ? SalvageInventory.Instance.TotalScore : 0;
            TotalScore += levelScore;
            SetState(GameState.LevelSettled);
        }

        /// <summary>结算面板「继续」按钮调用：有下一关 → 加载下一关；否则 → 清场并全通关 <see cref="GameState.GameOver"/>。</summary>
        public void ContinueAfterSettle()
        {
            if (levels == null || CurrentLevelIndex < 0)
            {
                SetState(GameState.GameOver);
                return;
            }

            int next = CurrentLevelIndex + 1;
            if (next < levels.Length)
            {
                BeginLevel(next);
            }
            else
            {
                ClearCurrentLevel();
                SetState(GameState.GameOver);
            }
        }

        // ===== 失败复活编排 =====

        /// <summary>玩家失败（电量耗尽，由 PlayerDied 事件触发）：把船传送回当前关出生点。
        /// 电量/弹药/船速由各自系统监听 PlayerDied 自行重置
        /// （BatteryManager.ResetBattery、DepthChargeLauncher.ResetCharges、BoatController 清速）。</summary>
        private void HandlePlayerDied()
        {
            if (Boat != null) Boat.transform.position = CurrentSpawnPosition();
        }

        // ===== 给策划/UI 按钮预留的快捷方法 =====

        public void PauseGame()  => SetState(GameState.Paused);
        public void ResumeGame() => SetState(GameState.Playing);

        /// <summary>返回主菜单：清空当前关实例后切到 <see cref="GameState.MainMenu"/>
        ///（暂停菜单「退出」按钮可绑定；timeScale 由 <see cref="SetState"/> 还原）。</summary>
        public void QuitToMenu()
        {
            ClearCurrentLevel();
            SetState(GameState.MainMenu);
        }

        /// <summary>重玩当前关：重新加载 <see cref="CurrentLevelIndex"/> 关（重置电量/弹药/货舱，船送回出生点）。
        /// 暂停菜单「重玩」按钮可绑定；尚未进入任何关卡时退化为 <see cref="StartGame"/>。
        /// BeginLevel 内部会把状态切回 <see cref="GameState.Playing"/>，故 timeScale 自动还原、面板随之隐藏。</summary>
        public void ReplayCurrentLevel()
        {
            if (CurrentLevelIndex < 0) { StartGame(); return; }
            BeginLevel(CurrentLevelIndex);
        }

        /// <summary>强制结束（=全通关 GameOver）。保留给「结束游戏」按钮；正常通关流程走 <see cref="ContinueAfterSettle"/>。</summary>
        public void EndGame() => SetState(GameState.GameOver);

        // ===== 内部 =====

        /// <summary>当前关出生点（无 LevelRoot 则回退原点）。</summary>
        private Vector3 CurrentSpawnPosition()
            => _currentLevelRoot != null ? _currentLevelRoot.SpawnPosition : Vector3.zero;

        /// <summary>清空当前关实例（销毁 Prefab 实例，重置锚点引用）。</summary>
        private void ClearCurrentLevel()
        {
            if (_currentLevelInstance != null) Destroy(_currentLevelInstance);
            _currentLevelInstance = null;
            _currentLevelRoot = null;
        }

        /// <summary>把常驻的 HarborEntryController 摆到当前关结算点；本关无结算点则不移动。</summary>
        private void PositionHarborEntry()
        {
            if (_currentLevelRoot == null || !_currentLevelRoot.HasSettlePoint) return;
            HarborEntryController harbor = FindFirst<HarborEntryController>();
            if (harbor != null) harbor.transform.position = _currentLevelRoot.SettlePosition;
        }

        /// <summary>场景里查找第一个类型为 T 的组件（封装 FindObjectsByType 取首个，避免过时 API 警告）。</summary>
        private static T FindFirst<T>() where T : Component
        {
            T[] arr = FindObjectsByType<T>(FindObjectsSortMode.None);
            return (arr != null && arr.Length > 0) ? arr[0] : null;
        }
    }
}
