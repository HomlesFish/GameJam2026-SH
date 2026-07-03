using System.Collections.Generic;

namespace GameJam2026
{
    /// <summary>
    /// 简单事件总线（静态）。
    /// 发布者与订阅者通过事件 ID 解耦通信。
    ///
    /// 48小时Jam快速接线：
    ///   发布：EventBus.Raise("PlayerDied");
    ///   订阅：实现 <see cref="IGameEventListener"/>，
    ///         在 OnEnable 中 EventBus.Subscribe("PlayerDied", this)，
    ///         在 OnDisable/OnDestroy 中 EventBus.Unsubscribe(...)（务必取消，避免空引用）。
    /// </summary>
    public static class EventBus
    {
        // 事件 ID -> 订阅该事件的监听器集合
        private static readonly Dictionary<string, HashSet<IGameEventListener>> _subscribers
            = new Dictionary<string, HashSet<IGameEventListener>>();

        /// <summary>订阅指定事件。</summary>
        public static void Subscribe(string eventId, IGameEventListener listener)
        {
            if (string.IsNullOrEmpty(eventId) || listener == null) return;

            if (!_subscribers.TryGetValue(eventId, out var set))
            {
                set = new HashSet<IGameEventListener>();
                _subscribers[eventId] = set;
            }
            set.Add(listener);
        }

        /// <summary>取消订阅（对象销毁前务必调用）。</summary>
        public static void Unsubscribe(string eventId, IGameEventListener listener)
        {
            if (string.IsNullOrEmpty(eventId) || listener == null) return;

            if (_subscribers.TryGetValue(eventId, out var set))
                set.Remove(listener);
        }

        /// <summary>触发事件，通知所有订阅该事件的监听器。</summary>
        public static void Raise(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (!_subscribers.TryGetValue(eventId, out var set)) return;

            // 复制一份再遍历，避免回调中增删订阅者导致集合被修改异常
            foreach (var listener in new List<IGameEventListener>(set))
                listener.OnEventRaised(eventId);
        }
    }
}
