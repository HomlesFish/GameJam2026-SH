using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 一条字幕的配置：id + 文本 + 语音。配在 <see cref="SubtitleDatabase"/> 里，按 id 查找。
    /// </summary>
    [Serializable]
    public class SubtitleEntry
    {
        [Tooltip("字幕 ID（全库唯一）。其他系统调用 SubtitleManager.Instance.Show(id) 时传它。")]
        [SerializeField] private string id;

        [Tooltip("字幕文本（显示用）")]
        [SerializeField] [TextArea(2, 4)] private string text;

        [Tooltip("配音（可选）。播放完 hideDelay 秒后该条字幕消失；留空则按文本长度估算显示时长。")]
        [SerializeField] private AudioClip voice;

        /// <summary>字幕 ID（全库唯一，调用方用它索引）。</summary>
        public string Id => id;

        /// <summary>字幕文本。</summary>
        public string Text => text;

        /// <summary>配音音频（可空）。</summary>
        public AudioClip Voice => voice;
    }

    /// <summary>
    /// 字幕数据库（ScriptableObject）：集中配置所有字幕条目（id/文本/语音），供 <see cref="SubtitleManager"/> 按 id 查找。
    ///
    /// 48小时Jam用法：
    ///   1) Project 窗口右键 → Create → GameJam2026 → Subtitle Database，新建一个 .asset；
    ///   2) 在 Inspector 往 entries 列表里加条目，每条填 id、文本、（可选）拖入配音；
    ///   3) 把这个 .asset 拖到 SubtitleManager 的 database 字段。
    ///   id 必须全库唯一（重复时取第一个并告警）；其他系统只需 <c>SubtitleManager.Instance.Show("你的id")</c>。
    /// </summary>
    [CreateAssetMenu(fileName = "SubtitleDatabase", menuName = "GameJam2026/Subtitle Database", order = 30)]
    public class SubtitleDatabase : ScriptableObject
    {
        [Tooltip("全库字幕条目，每条需有唯一 id")]
        [SerializeField] private List<SubtitleEntry> entries = new List<SubtitleEntry>();

        private Dictionary<string, SubtitleEntry> _lookup  =   new Dictionary<string, SubtitleEntry>();
        private bool _indexed = false;

        /// <summary>条目总数。</summary>
        public int Count => entries != null ? entries.Count : 0;

        /// <summary>按 id 查找字幕条目；找不到返回 null。重复 id 以第一个为准（建库时已告警）。</summary>
        public SubtitleEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureIndexed();
            _lookup.TryGetValue(id, out SubtitleEntry entry);
            return entry;
        }

        /// <summary>懒构建 id → 条目 索引；重复 id 取第一个并向控制台告警。</summary>
        private void EnsureIndexed()
        {
            string firstId = (entries != null && entries.Count > 0 && entries[0] != null) ? entries[0].Id : "<空>";
            Debug.Log($"[SubtitleDB] 进入: _indexed={_indexed}, entries.Count={(entries != null ? entries.Count : -1)}, _lookup.Count={_lookup.Count}, entries[0].Id='{firstId}', instance={GetInstanceID()}, frame={Time.frameCount}");
            // 只在「确实已建好且有内容」时跳过；_lookup 为空就重建，杜绝被 stale 的 _indexed 卡成空索引
            if (_indexed && _lookup.Count > 0) return;
            _lookup.Clear();
            if (entries != null)
            {
                foreach (SubtitleEntry e in entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.Id)) continue;
                    if (_lookup.ContainsKey(e.Id))
                    {
                        Debug.LogWarning($"[SubtitleDatabase] 重复的字幕 id '{e.Id}'，已忽略后续条目。", this);
                        continue;
                    }
                    _lookup[e.Id] = e;
                }
            }
            _indexed = true;
            Debug.Log($"[SubtitleDB] 结束: _lookup.Count={_lookup.Count}, _indexed={_indexed}");
        }
    }
}
