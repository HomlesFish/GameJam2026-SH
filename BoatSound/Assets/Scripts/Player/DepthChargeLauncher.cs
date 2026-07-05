using System;
using System.Collections;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 深水炸弹发射器（单例，挂在玩家船上）。
    ///
    /// 规则：
    ///   1. 弹药：<see cref="maxCharges"/>（默认 3 枚）为上限，<see cref="Drop"/> 每次消耗一枚，用完无法再投。
    ///   2. 投放：按 <see cref="dropKey"/>（默认 F）或调用 <see cref="Drop"/>（可绑 UI 按钮 OnClick）。
    ///   3. 爆炸：以**船当前位置**为中心、<see cref="explosionRadius"/>（默认 5）半径内的圆形区域，
    ///            区域内的「可摧毁」雷达目标（<see cref="SonarTarget"/>）被摧毁并从场景移除。
    ///   4. 可摧毁：<see cref="SonarTarget.Destructible"/> 为 true，**或**物体 Tag 命中
    ///            <see cref="destructibleTags"/>（默认 Reef/Fish/Dolphin）。
    ///   5. 免疫：<see cref="SonarTarget.IsObjective"/>（主线任务目标）恒不受伤。
    ///
    /// 联动：失败复活（<see cref="GameEvents.PlayerDied"/>）时把弹药补满；
    ///       UI 订阅 <see cref="OnChargesChanged"/> 刷新剩余弹药数。
    /// 用法：选中船 GameObject，Add Component 挂本脚本即可（全局唯一）。
    /// </summary>
    public class DepthChargeLauncher : SingletonBehaviour<DepthChargeLauncher>, IGameEventListener
    {
        [Header("弹药")]
        [Tooltip("弹药上限（最多几枚深水炸弹）")]
        [SerializeField] private int maxCharges = 3;

        [Header("爆炸")]
        [Tooltip("爆炸半径（单位），以船当前位置为圆心")]
        [SerializeField] private float explosionRadius = 5f;

        [Tooltip("视为可摧毁的 Tag：物体 Tag 命中其中任一即视为可摧毁（也可改用 SonarTarget.destructible）")]
        [SerializeField] private string[] destructibleTags = { "Reef", "Fish", "Dolphin" };

        [Header("输入 / 反馈")]
        [Tooltip("爆炸音效（可选，留空则无声）")]
        [SerializeField] private AudioClip explosionClip;

        [Tooltip("爆炸环颜色（程序化扩散环，无需美术资源）")]
        [SerializeField] private Color explosionColor = new Color(1f, 0.55f, 0.2f, 1f);

        [Tooltip("爆炸环扩散时长（秒）")]
        [SerializeField] private float explosionDuration = 0.4f;

        [Tooltip("爆炸环线宽（单位）")]
        [SerializeField] private float explosionWidth = 0.14f;

        /// <summary>当前剩余弹药数。</summary>
        public int Charges { get; private set; }

        /// <summary>弹药上限。</summary>
        public int MaxCharges => maxCharges;

        /// <summary>弹药变化事件：(当前剩余, 上限)。UI 订阅以刷新弹药显示。</summary>
        public event Action<int, int> OnChargesChanged;

        private Material _lineMat; // 程序化线材质（爆炸环复用，缓存避免每帧分配）

        // 字幕 id（需在 SubtitleDatabase 里建同名条目）
        private const string SubtitleLaunched = "DepthChargeLaunched";
        private const string SubtitleBombedFish = "BombedFish";
        private const string SubtitleBombedReef = "BombedReef";

        protected override void Awake()
        {
            base.Awake();   // 完成单例注册（必须调用）
            Charges = Mathf.Max(0, maxCharges);
        }

        private void Start()
        {
            // Start 时所有 Awake 已跑完：补发一次，确保晚订阅的 UI 也能拿到初始弹药数
            FireChanged();
        }

        // ===== 失败复活：监听电量耗尽，复活时补满弹药 =====

        private void OnEnable()
        {
            EventBus.Subscribe(GameEvents.PlayerDied, this);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(GameEvents.PlayerDied, this);
        }

        /// <inheritdoc />
        public void OnEventRaised(string eventId)
        {
            if (eventId == GameEvents.PlayerDied)
            {
                // 复活续玩：把弹药补满（与电量重置一致，给玩家重新来过的机会）
                ResetCharges();
            }
        }

        /// <summary>把弹药重置为上限并广播（复活 / 换关 / 开局重置用）。</summary>
        public void ResetCharges()
        {
            Charges = Mathf.Max(0, maxCharges);
            FireChanged();
        }

        // ===== 投放 / 引爆 =====

        /// <summary>
        /// 投放一枚深水炸弹：弹药 &gt; 0 时消耗一枚并以船位为中心引爆；弹药为 0 时无操作（用完即不可用）。
        /// 可由键盘（<see cref="dropKey"/>）或 UI 按钮 OnClick 调用。
        /// </summary>
        public void Drop()
        {
            if (Charges <= 0) return;

            Charges--;
            FireChanged();
            // 发射成功字幕提示
            if (SubtitleManager.Instance != null) SubtitleManager.Instance.Show(SubtitleLaunched);
            Detonate();
        }

        /// <summary>以船当前位置为中心引爆：半径内可摧毁、且非任务目标的 SonarTarget 全部移除。</summary>
        private void Detonate()
        {
            Vector2 center = transform.position;
            float sqrRadius = explosionRadius * explosionRadius;

            // FindObjectsByType 返回快照数组，遍历中销毁安全
            bool hitFish = false;
            bool hitReef = false;

            SonarTarget[] targets = FindObjectsByType<SonarTarget>(FindObjectsSortMode.None);
            foreach (SonarTarget t in targets)
            {
                if (t == null) continue;
                if (t.IsObjective) continue;                          // 任务目标免疫
                if (!t.Destructible && !HasDestructibleTag(t)) continue; // 非可摧毁跳过

                float sqrDist = ((Vector2)t.transform.position - center).sqrMagnitude;
                if (sqrDist <= sqrRadius)
                {
                    // 记录炸到的类型（用 Tag 区分），用于字幕提示
                    if (t.tag == "Fish") hitFish = true;
                    else if (t.tag == "Reef") hitReef = true;
                    Destroy(t.gameObject);
                }
            }

            // 炸到鱼 / 暗礁的字幕提示（每种每次爆炸最多各一次）
            if (SubtitleManager.Instance != null)
            {
                if (hitFish) SubtitleManager.Instance.Show(SubtitleBombedFish);
                if (hitReef) SubtitleManager.Instance.Show(SubtitleBombedReef);
            }

            PlayExplosionSound();
            StartCoroutine(ExplosionRingRoutine(center));
        }

        /// <summary>物体 Tag 是否命中可摧毁标签列表。用字符串比较，避免 CompareTag 对未注册 Tag 抛异常。</summary>
        private bool HasDestructibleTag(Component c)
        {
            if (destructibleTags == null) return false;
            string tag = c.tag;
            foreach (string dt in destructibleTags)
            {
                if (!string.IsNullOrEmpty(dt) && tag == dt) return true;
            }
            return false;
        }

        private void PlayExplosionSound()
        {
            if (explosionClip == null) return;
            // 一次性 3D 播放，无需在船上挂常驻 AudioSource
            AudioSource.PlayClipAtPoint(explosionClip, transform.position);
        }

        /// <summary>程序化爆炸环：从 0 扩散到 explosionRadius 并整体淡出（LineRenderer，无需美术资源）。</summary>
        private IEnumerator ExplosionRingRoutine(Vector2 center)
        {
            float dur = Mathf.Max(0.05f, explosionDuration);
            float radius = Mathf.Max(0.1f, explosionRadius);

            var go = new GameObject("DepthChargeExplosion");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;                                  // 闭合圆
            lr.widthMultiplier = Mathf.Max(0.001f, explosionWidth);
            lr.numCornerVertices = 8;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;               // 俯视下保证可见
            lr.sortingOrder = 6;                             // 画在普通目标精灵之上
            lr.material = LineMaterial;

            int pts = 48;
            lr.positionCount = pts;
            Vector3[] positions = new Vector3[pts];

            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                float r = Mathf.Lerp(0.15f, radius, p);
                float a = 1f - p;                            // 扩散同时淡出

                for (int j = 0; j < pts; j++)
                {
                    float ang = (j / (float)pts) * Mathf.PI * 2f;
                    positions[j] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                }
                lr.SetPositions(positions);

                Color c = explosionColor; c.a = a;
                lr.startColor = lr.endColor = c;
                yield return null;
            }

            Destroy(go);
        }

        /// <summary>程序化线材质（缓存复用）：顶点色 + 透明混合，任意管线可用（修复 URP 下运行时新建线变紫）。</summary>
        private Material LineMaterial
        {
            get
            {
                if (_lineMat == null)
                {
                    Shader sh = Shader.Find("Hidden/Internal-Colored")
                              ?? Shader.Find("Universal Render Pipeline/Unlit")
                              ?? Shader.Find("Unlit/Color");
                    _lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                    _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _lineMat.SetInt("_ZWrite", 0);
                }
                return _lineMat;
            }
        }

        private void OnDestroy()
        {
            if (_lineMat != null) Destroy(_lineMat);
        }

        private void FireChanged()
        {
            OnChargesChanged?.Invoke(Charges, maxCharges);
        }
    }
}
