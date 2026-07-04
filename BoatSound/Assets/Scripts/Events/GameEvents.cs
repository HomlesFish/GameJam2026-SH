namespace GameJam2026
{
    /// <summary>
    /// 游戏事件 ID 常量集中处。
    /// 通过 <see cref="EventBus"/> 发布/订阅时统一从此取字符串，避免散落各处的字面量拼写不一致。
    /// 命名约定：PascalCase。按需新增。
    /// </summary>
    public static class GameEvents
    {
        /// <summary>玩家失败（如电量耗尽）。发布者：<see cref="BatteryManager"/>；订阅者：需响应失败/复活的系统。</summary>
        public const string PlayerDied = "PlayerDied";

        /// <summary>关卡结算完成（玩家携带主线目标入港结算）。发布者：<see cref="HarborEntryController"/>；订阅者：需响应通关/结算的系统。</summary>
        public const string LevelSettled = "LevelSettled";

        /// <summary>关卡开始（换关后：新关内容已实例化、船已就位）。发布者：<see cref="GameManager"/>；订阅者：需按新关重新初始化的系统（如 SonarUIController 重新解析本关主线目标）。</summary>
        public const string LevelStarted = "LevelStarted";
    }
}
