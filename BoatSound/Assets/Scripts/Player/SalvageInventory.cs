using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 玩家货舱（单例）：按打捞顺序存放支线物品（名字 + 积分），上限 <see cref="capacity"/>（默认 3）。
    /// 满后 <see cref="TryAdd"/> 返回 false（打捞被阻止）。
    ///
    /// 用法：场景里建空 GameObject 挂本脚本。
    /// 联动：UI 订阅 <see cref="OnChanged"/> 刷新列表；满时尝试入舱触发 <see cref="OnPickupBlocked"/>（可做"满了"提示）。
    /// </summary>
    public class SalvageInventory : SingletonBehaviour<SalvageInventory>
    {
        [Tooltip("货舱容量（最多几件支线物品）")]
        [SerializeField] private int capacity = 3;

        private readonly List<SalvageRecord> _items = new List<SalvageRecord>();
        private int _totalScore;
        private bool _hasObjective;   // 是否已打捞到主线目标物品（入港结算前置校验用）

        /// <summary>容量上限。</summary>
        public int Capacity => capacity;

        /// <summary>当前件数。</summary>
        public int Count => _items.Count;

        /// <summary>是否已满。</summary>
        public bool IsFull => _items.Count >= capacity;

        /// <summary>按打捞顺序的物品记录列表（只读）。</summary>
        public IReadOnlyList<SalvageRecord> Items => _items;

        /// <summary>已打捞物品的累计积分。</summary>
        public int TotalScore => _totalScore;

        /// <summary>是否已获得主线目标物品（关卡目标）。打捞到 isObjective 物品后置 true，<see cref="Clear"/> 时重置。入港结算用此前置校验。</summary>
        public bool HasObjective => _hasObjective;

        /// <summary>货舱变化事件（增/删时触发）。UI 订阅以刷新列表。</summary>
        public event Action OnChanged;

        /// <summary>货舱已满时仍尝试入舱触发（供"满了/无法打捞"反馈）。</summary>
        public event Action OnPickupBlocked;

        /// <summary>尝试入舱一件普通支线物品（名字 + 积分），不视为主线目标。等价于 <see cref="TryAdd(string,int,bool)"/> 传 isObjective=false。</summary>
        public bool TryAdd(string itemName, int score) => TryAdd(itemName, score, false);

        /// <summary>尝试入舱一件物品（名字 + 积分）。未满则追加并返回 true；已满则触发 <see cref="OnPickupBlocked"/> 并返回 false。
        /// isObjective=true 时同时把 <see cref="HasObjective"/> 置 true（标记已获得主线目标物品，供入港结算前置校验）。</summary>
        public bool TryAdd(string itemName, int score, bool isObjective)
        {
            if (string.IsNullOrEmpty(itemName)) return false;
            if (_items.Count >= capacity)
            {
                OnPickupBlocked?.Invoke();
                return false;
            }
            _items.Add(new SalvageRecord(itemName, score));
            _totalScore += score;
            if (isObjective) _hasObjective = true;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>清空货舱（入港交付 / 重开）。同时重置主线目标标记。</summary>
        public void Clear()
        {
            bool hadItems = _items.Count > 0;
            _items.Clear();
            _totalScore = 0;
            _hasObjective = false;
            if (hadItems) OnChanged?.Invoke();
        }
    }

    /// <summary>一件打捞物品的记录：名字 + 积分。</summary>
    public struct SalvageRecord
    {
        /// <summary>物品名字（UI 显示）。</summary>
        public string Name { get; }

        /// <summary>该物品的积分。</summary>
        public int Score { get; }

        public SalvageRecord(string name, int score)
        {
            Name = name;
            Score = score;
        }
    }
}
