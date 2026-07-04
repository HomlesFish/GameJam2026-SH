using System.Collections;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 可被声呐探测的物体。挂在场景里的目标上。
    /// - <see cref="band"/>：所属频段（决定显示用图片/颜色，以及听声音频）。
    /// - <see cref="isObjective"/>：主线任务目标，始终显示为光点（不随刷新淡出）。
    /// - 按 ping 时由 SonarSystem 驱动 <see cref="PlayPingSound"/>，以 3D 声音**一次性**发声：
    ///   音量随到船（AudioListener）的距离衰减、带左右方向。
    ///
    /// 普通目标平时隐藏（alpha=0）；被 SonarSystem 扫描命中且在范围内时由 OnPing() 闪现一次。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
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

        [Tooltip("3D 声音衰减最大距离（单位）。超出此距离基本听不见")]
        [SerializeField] private float audioMaxDistance = 8f;

        public SonarBand Band => band;
        public bool IsObjective => isObjective;
        public AudioClip OverrideClip => overrideClip;

        private SpriteRenderer _renderer;
        private AudioSource _audio;
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

            // 配置 3D 声音：听者在船上，音量随距离衰减、带左右方向；一次性播放（ping）
            _audio = GetComponent<AudioSource>();
            if (_audio != null)
            {
                _audio.playOnAwake = false;
                _audio.loop = false;               // ping 为一次性播放
                _audio.spatialBlend = 1f;          // 完全 3D
                _audio.dopplerLevel = 0f;          // 避免相对运动造成的变调
                _audio.spread = 0f;
                _audio.rolloffMode = AudioRolloffMode.Logarithmic;
                _audio.minDistance = 0.3f;
                _audio.maxDistance = audioMaxDistance;
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

        /// <summary>按 ping 时调用：用 clip 一次性播放该物体的 3D 声音（音量随到听者距离衰减、带方向）。</summary>
        public void PlayPingSound(AudioClip clip)
        {
            if (_audio == null || clip == null) return;
            _audio.loop = false;
            _audio.clip = clip;
            _audio.Play();
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
