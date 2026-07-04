using System;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 玩家电量（续航）管理器（单例）。
    /// 电量是统一消耗资源：打捞 / 听声 / 碰暗礁 都扣电；电量归 0 触发失败 → 复活续玩
    /// （重置电量、继续游戏；船复位回出生点由 <see cref="GameManager"/> 负责）。
    ///
    /// 用法：场景里建空 GameObject 挂本脚本。
    /// 失败复活：电量归 0 时只负责重置电量并广播 <see cref="GameEvents.PlayerDied"/>；
    /// 船的传送回出生点由 GameManager 监听该事件负责。
    /// 其他系统联动：
    ///   - UI：订阅 <see cref="OnBatteryChanged"/> 刷新电量条；
    ///   - 复活编排：见上，由 GameManager 统一负责。
    /// </summary>
    public class BatteryManager : SingletonBehaviour<BatteryManager>
    {
        [Header("电量")]
        [Tooltip("电量上限")]
        [SerializeField] private int maxBattery = 100;

        [Tooltip("起始 / 复活后重置到的电量")]
        [SerializeField] private int startBattery = 100;

        [Header("各类消耗")]
        [Tooltip("每次打捞消耗")]
        [SerializeField] private int salvageCost = 5;

        [Tooltip("每次听声消耗")]
        [SerializeField] private int listenCost = 1;

        [Tooltip("每次碰撞暗礁消耗")]
        [SerializeField] private int reefDamage = 20;

        /// <summary>当前电量（0 ~ <see cref="MaxBattery"/>）。</summary>
        public int CurrentBattery { get; private set; }

        /// <summary>电量上限。</summary>
        public int MaxBattery => maxBattery;

        /// <summary>电量比例 0~1（current/max）。</summary>
        public float Normalized => maxBattery > 0 ? CurrentBattery / (float)maxBattery : 0f;

        /// <summary>电量变化事件：(当前电量, 上限)。UI 订阅以刷新电量条。</summary>
        public event Action<int, int> OnBatteryChanged;

        /// <summary>电量首次归 0 时触发一次（在重置之前）。可用于播放失败音效/闪屏等。</summary>
        public event Action OnBatteryEmpty;

        protected override void Awake()
        {
            base.Awake();   // 完成单例注册（必须调用）

            // 在 Awake 就把初值设好，确保任何在 OnEnable/Start 订阅的 UI 都能读到正确值
            CurrentBattery = Mathf.Clamp(startBattery, 0, maxBattery);
        }

        private void Start()
        {
            // Start 时所有 Awake 已跑完：补发一次，确保晚订阅的 UI 也能拿到初始电量
            FireChanged();
        }

        /// <summary>扣除任意正整数电量；扣到 0 自动触发失败复活。返回是否真的扣到。</summary>
        public bool Consume(int amount)
        {
            if (amount <= 0) return false;
            if (CurrentBattery <= 0) return false; // 已归 0（复活流程会重置，期间忽略新消耗）

            CurrentBattery = Mathf.Max(0, CurrentBattery - amount);
            FireChanged();

            if (CurrentBattery <= 0) HandleEmpty();
            return true;
        }

        /// <summary>打捞消耗（= <see cref="salvageCost"/>）。由 UI 按钮 / 按键调用。</summary>
        public void ConsumeSalvage() => Consume(salvageCost);

        /// <summary>听声消耗（= <see cref="listenCost"/>）。由 SonarSystem 调用。</summary>
        public void ConsumeListen() => Consume(listenCost);

        /// <summary>碰撞暗礁消耗（= <see cref="reefDamage"/>）。由 BoatController 碰撞调用。</summary>
        public void ConsumeReefHit() => Consume(reefDamage);

        /// <summary>把电量重置为起始值并广播。</summary>
        public void ResetBattery()
        {
            CurrentBattery = Mathf.Clamp(startBattery, 0, maxBattery);
            FireChanged();
        }

        // ===== 内部 =====

        private void HandleEmpty()
        {
            // 此时 CurrentBattery 仍为 0；订阅者可据此播放失败反馈
            OnBatteryEmpty?.Invoke();
            // 广播失败：GameManager 传送船回出生点、BoatController 清速、DepthChargeLauncher 补弹
            EventBus.Raise(GameEvents.PlayerDied);
            ResetBattery();   // 本管理器只负责重置电量；传送职责已交给 GameManager
        }

        private void FireChanged()
        {
            OnBatteryChanged?.Invoke(CurrentBattery, maxBattery);
        }
    }
}
