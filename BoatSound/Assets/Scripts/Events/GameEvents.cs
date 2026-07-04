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
    }
}
