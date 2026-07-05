using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 一条「正在显示中」的字幕实例（运行期对象）。
    /// <see cref="SubtitleManager.Show"/> 每次调用生成一个新实例；<see cref="InstanceId"/> 全局自增、唯一。
    /// UI 用 <see cref="InstanceId"/> 把「显示」与「消失」事件关联到具体那一行。
    /// </summary>
    public class ActiveSubtitle
    {
        /// <summary>本次显示的唯一实例 id（每次 Show 自增；与配置 id 无关）。</summary>
        public int InstanceId;

        /// <summary>配置 id（来自 <see cref="SubtitleEntry.Id"/>，可能跨实例重复）。</summary>
        public string SubtitleId;

        /// <summary>字幕文本。</summary>
        public string Text;

        /// <summary>配音音频（可空）。</summary>
        public AudioClip Voice;
    }

    /// <summary>
    /// 字幕系统（单例）：按 id 播放配音 + 弹出字幕。其余系统只需 <c>SubtitleManager.Instance.Show("id")</c>。
    ///
    /// 规则：
    ///   1. <see cref="Show(string)"/>：到 <see cref="database"/> 查 id → 播配音 → 触发 <see cref="OnShown"/>（UI 新建一行）。
    ///   2. 计时：有配音时显示时长 = 配音长度；无配音时按文本长度估算（见 secondsPerChar / min / max）。
    ///   3. 消失：显示时长结束后，再过 <see cref="hideDelay"/>（默认 5 秒）触发 <see cref="OnHidden"/>（UI 淡出移除）。
    ///      每条字幕各自独立计时；新字幕不会取消旧字幕的计时，旧字幕会先被 UI「挤到上面」、到点再消失。
    ///   4. 配音为单声道：新配音会打断上一条正在播的配音；但被打断那条的字幕文本与计时不受影响。
    ///
    /// 联动：UI 订阅 <see cref="OnShown"/>/<see cref="OnHidden"/> 即可刷新堆叠。
    /// 用法：场景里建一个空 GameObject 挂本脚本（自带 AudioSource 用于 2D 配音），把字幕数据库 .asset 拖入 database。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SubtitleManager : SingletonBehaviour<SubtitleManager>
    {
        [Header("数据")]
        [Tooltip("字幕数据库（id/文本/语音）。必填，否则 Show 调用无效。")]
        [SerializeField] private SubtitleDatabase database;

        [Header("时序")]
        [Tooltip("配音播放完（或文本显示时长结束）后，再过这么多秒字幕消失")]
        [SerializeField] private float hideDelay = 5f;

        [Tooltip("无配音时，每个字符的显示时长（秒），用于估算显示时长")]
        [SerializeField] private float secondsPerChar = 0.12f;

        [Tooltip("无配音时最小显示时长（秒）")]
        [SerializeField] private float minDisplayTime = 1.5f;

        [Tooltip("无配音时最大显示时长（秒）")]
        [SerializeField] private float maxDisplayTime = 8f;

        /// <summary>一条字幕出现（UI 据此新建一行）。参数：本次显示的活动字幕实例。</summary>
        public event System.Action<ActiveSubtitle> OnShown;

        /// <summary>一条字幕消失（UI 据此移除对应行）。参数：要消失的活动字幕实例。</summary>
        public event System.Action<ActiveSubtitle> OnHidden;

        /// <summary>字幕数据库（便于 UI 预读等，一般无需直接用）。</summary>
        public SubtitleDatabase Database => database;

        private AudioSource _voice;
        private int _instanceCounter;
        private Coroutine _sequenceCoroutine;   // 当前顺序播放的剧情序列（同时只允许一个）

        protected override void Awake()
        {
            base.Awake();   // 完成单例注册（必须调用）

            // 配音用 2D：字幕/旁白全局可听，不随听者位置衰减
            _voice = GetComponent<AudioSource>();
            if (_voice != null)
            {
                _voice.playOnAwake = false;
                _voice.loop = false;
                _voice.spatialBlend = 0f;
            }

            if (database == null)
            {
                Debug.LogWarning("[SubtitleManager] 未指定 database，Show 调用将被忽略。", this);
            }
        }

        /// <summary>
        /// 按 id 播放一条字幕：查库 → 播配音 → 触发 <see cref="OnShown"/>；
        /// 显示时长结束 + <see cref="hideDelay"/> 秒后触发 <see cref="OnHidden"/>。
        /// 返回本次的活动字幕实例（找不到 id / 未配置数据库 / 已禁用时返回 null）。
        /// </summary>
        public ActiveSubtitle Show(string id)
        {
            if (!isActiveAndEnabled) return null;
            if (database == null)
            {
                Debug.LogWarning("[SubtitleManager] database 未配置，忽略 Show 调用。", this);
                return null;
            }

            SubtitleEntry entry = database.Find(id);
            if (entry == null)
            {
                Debug.LogWarning($"[SubtitleManager] 找不到字幕 id '{id}'，请检查数据库。", this);
                return null;
            }

            var sub = new ActiveSubtitle
            {
                InstanceId = ++_instanceCounter,
                SubtitleId = entry.Id,
                Text = entry.Text,
                Voice = entry.Voice,
            };

            // 显示时长：有配音取配音长度，否则按文本估算
            float displayTime;
            if (entry.Voice != null)
            {
                if (_voice != null)
                {
                    _voice.clip = entry.Voice;   // 单声道：新配音会打断旧配音
                    _voice.Play();
                }
                displayTime = entry.Voice.length;
            }
            else
            {
                displayTime = EstimateDisplayTime(entry.Text);
            }

            OnShown?.Invoke(sub);
            StartCoroutine(HideAfter(sub, displayTime + Mathf.Max(0f, hideDelay)));
            return sub;
        }

        /// <summary>顺序播放一组字幕（剧情用）：逐条显示，每条到点消失后才出下一条。
        /// 新调用会中断正在播放的序列；voice 仍走单一声道、逐条衔接。返回是否成功启动。</summary>
        public bool ShowSequence(IList<string> ids)
        {
            if (!isActiveAndEnabled) return false;
            if (database == null)
            {
                Debug.LogWarning("[SubtitleManager] database 未配置，忽略 ShowSequence 调用。", this);
                return false;
            }
            if (ids == null || ids.Count == 0) return false;

            StopSequence();   // 中断正在播放的序列，避免两段剧情叠加
            _sequenceCoroutine = StartCoroutine(SequenceRoutine(ids));
            return true;
        }

        /// <summary>停止当前顺序播放的剧情序列（单条 <see cref="Show"/> 的字幕不受影响，各自独立计时到点消失）。</summary>
        public void StopSequence()
        {
            if (_sequenceCoroutine != null)
            {
                StopCoroutine(_sequenceCoroutine);
                _sequenceCoroutine = null;
            }
        }

        /// <summary>逐条播放：每条查库→配音→OnShown，等待 displayTime+hideDelay 后 OnHidden，再进下一条。</summary>
        private IEnumerator SequenceRoutine(IList<string> ids)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;

                SubtitleEntry entry = database.Find(id);
                if (entry == null)
                {
                    Debug.LogWarning($"[SubtitleManager] 序列找不到字幕 id '{id}'，跳过。", this);
                    continue;
                }

                var sub = new ActiveSubtitle
                {
                    InstanceId = ++_instanceCounter,
                    SubtitleId = entry.Id,
                    Text = entry.Text,
                    Voice = entry.Voice,
                };

                float displayTime;
                if (entry.Voice != null)
                {
                    if (_voice != null)
                    {
                        _voice.clip = entry.Voice;   // 单声道：新配音覆盖上一条
                        _voice.Play();
                    }
                    displayTime = entry.Voice.length;
                }
                else
                {
                    displayTime = EstimateDisplayTime(entry.Text);
                }

                OnShown?.Invoke(sub);
                yield return new WaitForSeconds(displayTime + Mathf.Max(0f, hideDelay));
                OnHidden?.Invoke(sub);
            }
            _sequenceCoroutine = null;
        }

        /// <summary>等待 delay 秒后触发 OnHidden（每条字幕各自独立计时）。</summary>
        private IEnumerator HideAfter(ActiveSubtitle sub, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            OnHidden?.Invoke(sub);
        }

        /// <summary>无配音时按字符数估算显示时长，并夹在 [min, max] 区间。</summary>
        private float EstimateDisplayTime(string text)
        {
            int len = string.IsNullOrEmpty(text) ? 0 : text.Length;
            float t = len * Mathf.Max(0.01f, secondsPerChar);
            return Mathf.Clamp(t, minDisplayTime, maxDisplayTime);
        }
    }
}
