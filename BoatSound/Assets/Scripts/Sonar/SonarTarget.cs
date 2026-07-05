using System.Collections;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 可被声呐探测的物体。挂在场景里的目标上。
    /// - <see cref="band"/>：所属频段（决定显示用图片/颜色，以及听声音频）。
    /// - <see cref="isObjective"/>：主线任务目标，始终显示为光点（不随刷新淡出）。
    /// - 按 ping 时由 SonarSystem 驱动 <see cref="PlayPingSound"/>，经 SoundManager 以 2D **一次性**发声：
    ///   不走 3D 距离衰减（永远可听见），并按目标相对船的方位做左右声道 pan。
    ///
    /// 普通目标平时隐藏（alpha=0）；被 SonarSystem 扫描命中且在范围内时由 OnPing() 闪现一次。
    ///
    /// 打捞：<see cref="salvageable"/> 默认为 true——所有被探测物体都可被船近距离打捞；
    /// 暗礁等不可打捞物取消勾选。打捞后用 <see cref="itemName"/> 作为货舱里显示的名字，物品从场景移除。
    ///
    /// 深水炸弹：<see cref="destructible"/> 标记能否被深水炸弹摧毁（暗礁/鱼群/海豚勾选）。
    /// <see cref="isObjective"/>（主线任务目标）恒免疫，不受深水炸弹影响。
    ///
    /// HUD 目标物品：主线目标（isObjective）的 <see cref="uiIcon"/> 与 <see cref="uiSoundClip"/> 由 SonarUIController 读取，
    /// 显示在 HUD 左上角（图标 + 喇叭音效）；**每关不同**，在该关的主线目标上配置即可，UI 会自动取用。
    /// </summary>
    public class SonarTarget : MonoBehaviour
    {
        [Tooltip("所属频段")]
        [SerializeField] private SonarBand band = SonarBand.Mid;

        [Tooltip("主线任务目标：始终显示为光点，不随刷新淡出")]
        [SerializeField] private bool isObjective;

        [Tooltip("显示颜色（可与贴图配合区分频段）")]
        [SerializeField] private Color tint = Color.white;

        [Tooltip("每次扫描的 显现→淡出 时长（秒），需小于刷新间隔")]
        [SerializeField] private float pulseDuration = 1.5f;

        [Tooltip("主线目标的呼吸频率（次/秒）")]
        [SerializeField] private float objectivePulseSpeed = 1.2f;

        [Header("听声音频")]
        [Tooltip("该物体自己的声音（可选）。留空则用 SonarSystem 里当前频段的默认音")]
        [SerializeField] private AudioClip overrideClip;

        [Header("HUD 目标物品（仅 isObjective 时由 SonarUIController 读取）")]
        [Tooltip("HUD 左上角显示的目标物品图标。每关不同——在该关的主线目标（isObjective）上配置")]
        [SerializeField] private Sprite uiIcon;

        [Tooltip("HUD 喇叭按钮点击播放的音效（船锚撞击目标物品）。留空则回退用上面的 overrideClip")]
        [SerializeField] private AudioClip uiSoundClip;

        [Header("打捞")]
        [Tooltip("打捞后显示在货舱 UI 里的物品名字")]
        [SerializeField] private string itemName = "未知物品";

        [Tooltip("该物品的积分（打捞后累加到货舱总分）")]
        [SerializeField] private int score = 10;

        [Tooltip("是否可被打捞。默认勾选（所有被探测物体都可打捞）；暗礁等不可打捞物取消勾选")]
        [SerializeField] private bool salvageable = true;

        [Tooltip("是否可被深水炸弹摧毁。暗礁/鱼群/海豚勾选；任务目标由 isObjective 自动免疫。默认不勾选——也可不改此项，直接给物体打 Tag(Reef/Fish/Dolphin) 标记为可摧毁")]
        [SerializeField] private bool destructible;

        public SonarBand Band => band;
        public bool IsObjective => isObjective;
        public AudioClip OverrideClip => overrideClip;

        /// <summary>HUD 左上角显示的目标物品图标（每关在该关主线目标上配置）。</summary>
        public Sprite UiIcon => uiIcon;

        /// <summary>HUD 喇叭按钮音效；留空回退 <see cref="OverrideClip"/>。</summary>
        public AudioClip GetUiSoundClip() => uiSoundClip != null ? uiSoundClip : overrideClip;

        /// <summary>打捞后显示在货舱里的物品名字。</summary>
        public string ItemName => itemName;

        /// <summary>该物品的积分。</summary>
        public int Score => score;

        /// <summary>是否可被打捞（暗礁等设为 false）。</summary>
        public bool Salvageable => salvageable;

        /// <summary>是否可被深水炸弹摧毁（暗礁/鱼群/海豚设为 true；任务目标恒免疫）。</summary>
        public bool Destructible => destructible;

        private SpriteRenderer _renderer;
        private Coroutine _pulseRoutine;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer == null)
            {
                Debug.LogWarning("[SonarTarget] 缺少 SpriteRenderer，无法显示：" + name, this);
            }
            else
            {
                _renderer.color = new Color(tint.r, tint.g, tint.b, isObjective ? 1f : 0f);
            }
        }

        private void Update()
        {
            // 主线目标：始终亮，轻微呼吸，代表"光点"
            if (isObjective && _renderer != null)
            {
                float a = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * objectivePulseSpeed * Mathf.PI * 2f));
                SetAlpha(a);
            }
        }

        /// <summary>被声呐扫描命中且在范围内时调用：闪现一次（仅普通目标）。</summary>
        public void OnPing()
        {
            if (isObjective) return;            // 主线目标由 Update 常亮
            if (_renderer == null) return;
            if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
            _pulseRoutine = StartCoroutine(PulseRoutine());
        }

        /// <summary>按 ping 时调用：经 SoundManager 以 2D 一次性播放该物体的听声音
        /// （不走 3D 距离衰减，永远可听见；按目标相对船的方位做左右声道 pan）。</summary>
        public void PlayPingSound(AudioClip clip)
        {
            if (clip == null) return;
            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySonarPing(clip, transform.position);
        }

        private IEnumerator PulseRoutine()
        {
            float t = 0f;
            while (t < pulseDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / pulseDuration); // 0→1
                float a = Mathf.Sin(p * Mathf.PI);          // 0→1→0
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(0f);
            _pulseRoutine = null;
        }

        private void SetAlpha(float a)
        {
            if (_renderer == null) return;
            Color c = _renderer.color;
            c.a = a;
            _renderer.color = c;
        }
    }
}
