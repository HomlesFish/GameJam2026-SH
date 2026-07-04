namespace GameJam2026
{
    /// <summary>
    /// 游戏事件监听器接口（事件系统基础接口）。
    /// 任何需要响应"游戏事件"的组件实现此接口，并通过 <see cref="EventBus"/> 订阅即可。
    ///
    /// 事件以字符串 ID 标识（约定见 Events/README，例如 "PlayerDied"、"ScoreChanged"），
    /// 便于策划与程序对齐命名，实现解耦通信：发布者与订阅者互不引用。
    /// </summary>
    public interface IGameEventListener
    {
        /// <summary>订阅的事件被触发时调用。<paramref name="eventId"/> 为事件名称。</summary>
        void OnEventRaised(string eventId);
    }
}
