using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// 声呐 / 雷达系统（主画面即雷达）。
    /// - <b>声呐扫描环</b>（默认，<see cref="useRingScan"/>）：每隔 <see cref="ringInterval"/>（默认 2s）从船中心扩散一个圆环（由小变大），
    ///   波前半径到达某物品的距离时，该物品闪现一次（<see cref="SonarTarget.OnPing"/>）。主线目标常亮。
    /// - <b>旋转扫描线</b>（默认，<see cref="showSweepLine"/>）：一根带渐隐拖尾的光束绕船（世界绝对方位）旋转，**纯视觉**，不揭示目标。
    /// - <b>定时全闪模式</b>（关闭 <see cref="useRingScan"/>）：每 <see cref="refreshInterval"/> 秒令范围内
    ///   <see cref="SonarTarget"/> 同时闪现一次（旧行为，向后兼容）。
    /// - 三个频段的物体同时显示（靠各自贴图/颜色区分）；切换频段（**Q/E** 循环 或 1/2/3 直接）选择当前频段。
    /// - 点 <b>ping 按钮</b>（UI Button，OnClick 调用 <see cref="Ping"/>）：播放<b>当前频段、范围内</b>物体各自的声音（3D，音量随到船距离衰减、带方向），一次性。
    ///
    /// 扫描线 / 扫描环均为程序化生成的 LineRenderer，无需美术资源、无需额外场景接线；挂在空 GameObject 上、把 Boat 拖进 boat 即可。
    /// 注意：3D 距离音需把 AudioListener 放在船上（从相机移过来），听者才在船的位置。
    /// </summary>
    public class SonarSystem : SingletonBehaviour<SonarSystem>
    {
        [Header("雷达")]
        [Tooltip("雷达中心（通常是船）")]
        [SerializeField] private Transform boat;

        [Tooltip("探测半径（单位）")]
        [SerializeField] private float detectionRange = 8f;

        [Tooltip("定时全闪模式的扫描间隔（秒）；扫描线模式忽略此项")]
        [SerializeField] private float refreshInterval = 2f;

        [Header("旋转扫描线（纯视觉）")]
        [Tooltip("显示绕船旋转的扫描线（仅视觉，不揭示目标）；揭示由声呐扫描环负责")]
        [SerializeField] private bool showSweepLine = true;

        [Tooltip("扫描线转速（度/秒）。120°/s ≈ 每 3 秒一圈")]
        [SerializeField] private float sweepSpeed = 120f;

        [Tooltip("是否绘制 中心→边缘 的径向主线（关掉只剩边缘拖尾弧）")]
        [SerializeField] private bool drawLeadingLine = true;

        [Tooltip("拖尾弧长（度），仅视觉；越大尾巴越长")]
        [SerializeField] private float beamTrailDegrees = 70f;

        [Tooltip("拖尾分段数，越大越平滑")]
        [SerializeField] private int trailSegments = 18;

        [Tooltip("扫描线宽度（单位）")]
        [SerializeField] private float beamWidth = 0.12f;

        [Tooltip("扫描线颜色（默认雷达绿）")]
        [SerializeField] private Color beamColor = new Color(0.25f, 1f, 0.45f, 1f);

        [Tooltip("线材质（扫描线/扫描环共用，可选）。留空用程序化材质；如 URP 下显示异常可在此指定一个线/精灵材质")]
        [SerializeField] private Material sweepMaterial;

        [Header("声呐扫描环（每 N 秒扩散并揭示目标）")]
        [Tooltip("每隔 ringInterval 秒从船中心扩散一个声呐环，波前到达目标时该目标闪现")]
        [SerializeField] private bool useRingScan = true;

        [Tooltip("声呐环间隔（秒）")]
        [SerializeField] private float ringInterval = 2f;

        [Tooltip("声呐环从起点扩散到探测半径所需时间（秒），建议 ≤ ringInterval")]
        [SerializeField] private float ringDuration = 1.6f;

        [Tooltip("声呐环起点半径（单位），避免一开始太小看不见")]
        [SerializeField] private float ringStartRadius = 0.3f;

        [Tooltip("声呐环圆周分段，越大越圆")]
        [SerializeField] private int ringPoints = 56;

        [Tooltip("声呐环线宽（单位）")]
        [SerializeField] private float ringWidth = 0.1f;

        [Tooltip("声呐环颜色（默认与扫描线同色）")]
        [SerializeField] private Color ringColor = new Color(0.25f, 1f, 0.45f, 1f);

        [Header("频段 / 听声")]
        [Tooltip("三个频段的默认音频（低/中/高），由用户提供；物体未设 overrideClip 时用它")]
        [SerializeField] private AudioClip[] bandClips = new AudioClip[3];

        [Header("UI（可选）")]
        [Tooltip("显示当前频段的文本（TMP），可不接")]
        [SerializeField] private Text bandLabel;

        public SonarBand CurrentBand => currentBand;

        private SonarBand currentBand = SonarBand.Mid;
        private float _timer;

        // ===== 扫描线运行时状态 =====
        private LineRenderer _sweepLine;
        private Vector3[] _sweepPoints;     // 复用，避免每帧分配
        private int _sweepPointCount;
        private float _sweepTotal;          // 累积角度（度，不取模），用于扫描线旋转
        private readonly List<SonarTarget> _targets = new List<SonarTarget>();
        private float _targetCacheTimer;
        private const float TargetCacheInterval = 1f; // 每隔 1s 刷新一次目标缓存，兼顾新Spawn/销毁
        private const float SweepZ = -1f;             // 略近于相机，确保画在目标精灵之上

        // ===== 声呐扫描环运行时状态 =====
        private readonly List<PingRing> _rings = new List<PingRing>();
        private float _ringTimer;
        private Material _defaultLineMat;             // 程序化线材质（缓存复用，扫描线/扫描环共用）

        private bool _active;   // 是否激活扫描（仅 Playing；菜单/暂停/结算/通关时停转）

        private void OnEnable() => SafeSubscribe();

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        /// <summary>幂等订阅 GameManager.OnStateChanged（重复调用不会多次订阅）。防 OnEnable 早于 GameManager.Awake 漏订。</summary>
        private void SafeSubscribe()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            // 启动时若已在 Playing（如直接以 Playing 开局），立即激活
            if (GameManager.Instance.CurrentState == GameManager.GameState.Playing) Activate();
        }

        /// <summary>游戏状态变化：进入 Playing 激活扫描（发首个声呐环/全闪）；其它状态停转。</summary>
        private void HandleStateChanged(GameManager.GameState newState, GameManager.GameState oldState)
        {
            if (newState == GameManager.GameState.Playing) Activate();
            else _active = false;
        }

        /// <summary>激活扫描：刷新目标缓存并按模式发出首个扫描（开局即有反馈，不必空等一个间隔）。</summary>
        private void Activate()
        {
            _active = true;
            if (boat == null) return;
            RefreshTargetCache();
            if (useRingScan)
            {
                SpawnRing();
                _ringTimer = 0f;
            }
            else
            {
                Scan();
            }
        }

        private void Start()
        {
            // boat 未指定则从 GameManager 取（统一真相源），消除每处重复接线
            if (boat == null && GameManager.Instance != null && GameManager.Instance.Boat != null)
                boat = GameManager.Instance.Boat.transform;

            UpdateBandLabel();
            if (boat == null)
                Debug.LogWarning("[SonarSystem] 未指定 boat（雷达中心），扫描将无效。", this);

            if (showSweepLine) CreateSweepLine();

            // 补订阅（防 OnEnable 早于 GameManager.Awake 漏订）
            SafeSubscribe();

            // 扫描（扫描线旋转 / 声呐环扩散）只在 Playing 时工作，由 HandleStateChanged→Activate 启动，
            // 避免菜单阶段就开始扫描。扫描线在此创建，但 Update 未激活时不旋转。
        }

        private void Update()
        {
            if (!_active) return;   // 非游戏进行中（菜单/暂停/结算/通关）：雷达停转
            HandleBandSwitch();

            // 旋转扫描线（纯视觉）
            if (showSweepLine)
            {
                UpdateSweep();
            }

            // 声呐扫描环（每 N 秒扩散并揭示）—— 唯一揭示机制
            if (useRingScan)
            {
                _targetCacheTimer += Time.deltaTime;
                if (_targetCacheTimer >= TargetCacheInterval)
                {
                    _targetCacheTimer = 0f;
                    RefreshTargetCache();
                }

                _ringTimer += Time.deltaTime;
                if (_ringTimer >= ringInterval)
                {
                    _ringTimer = 0f;
                    SpawnRing();
                }
                UpdateRings();
            }
            else
            {
                // 未启用声呐环：旧的定时全闪
                _timer += Time.deltaTime;
                if (_timer >= refreshInterval)
                {
                    _timer = 0f;
                    Scan();
                }
            }
        }

        private void OnDestroy()
        {
            foreach (PingRing r in _rings)
            {
                if (r != null && r.Line != null) Destroy(r.Line.gameObject);
            }
            _rings.Clear();
            if (_defaultLineMat != null) Destroy(_defaultLineMat);
        }

        private void HandleBandSwitch()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetBand(SonarBand.Low);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SetBand(SonarBand.Mid);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SetBand(SonarBand.High);
            else if (Input.GetKeyDown(KeyCode.E)) CycleBand(1);  // E：下一频段
            else if (Input.GetKeyDown(KeyCode.Q)) CycleBand(-1); // Q：上一频段
        }

        /// <summary>循环切换频段：delta=+1 下一频段、-1 上一频段（自动回绕）。</summary>
        private void CycleBand(int delta)
        {
            int count = (int)SonarBand.High + 1;          // 频段总数（Low=0 .. High）
            int n = ((int)currentBand + delta) % count;
            if (n < 0) n += count;                         // 负数回绕
            SetBand((SonarBand)n);
        }

        private void SetBand(SonarBand band)
        {
            if (band == currentBand) return;
            currentBand = band;
            UpdateBandLabel();
            // 注意：切频段不会自动发声，需玩家点 ping 按钮才会播放当前频段的声音
        }

        /// <summary>定时全闪模式：让范围内的普通 SonarTarget 同时闪现一次。主线目标由其自身常亮。</summary>
        private void Scan()
        {
            if (boat == null) return;

            Vector2 center = boat.position;
            float sqrRange = detectionRange * detectionRange;

            foreach (SonarTarget t in FindTargets())
            {
                if (t == null || t.IsObjective) continue; // 主线目标自带常亮
                float sqrDist = ((Vector2)t.transform.position - center).sqrMagnitude;
                if (sqrDist <= sqrRange)
                {
                    t.OnPing();
                }
            }
        }

        // ===== 雷达扫描线 =====

        /// <summary>程序化创建扫描线 GameObject + LineRenderer（无需美术资源、无需场景接线）。</summary>
        private void CreateSweepLine()
        {
            var go = new GameObject("SonarSweepLine");
            go.transform.SetParent(transform, false);

            _sweepLine = go.AddComponent<LineRenderer>();
            _sweepLine.useWorldSpace = true;   // 位置每帧按 boat 世界坐标重算
            _sweepLine.loop = false;
            _sweepLine.widthMultiplier = Mathf.Max(0.001f, beamWidth);
            _sweepLine.numCornerVertices = 4;
            _sweepLine.numCapVertices = 4;
            _sweepLine.alignment = LineAlignment.View; // 始终面向相机，俯视下保证可见
            _sweepLine.textureMode = LineTextureMode.Stretch;
            _sweepLine.sortingOrder = 5;                // 画在普通目标精灵之上
            _sweepLine.material = sweepMaterial != null ? sweepMaterial : DefaultLineMaterial;

            int segs = Mathf.Max(1, trailSegments);
            _sweepPointCount = (drawLeadingLine ? 2 : 1) + segs;
            _sweepPoints = new Vector3[_sweepPointCount];

            // 渐变：径向主线起端略暗 → 前缘最亮 → 沿拖尾衰减到 0（彗尾感）
            var grad = new Gradient();
            float rimFrac = drawLeadingLine ? 1f / (1f + segs) : 0f; // 前缘（最亮点）在折线中的位置
            float centerAlpha = drawLeadingLine ? 0.35f : 1f;        // 无径向主线时折线从前缘开始
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(beamColor, 0f), new GradientColorKey(beamColor, 1f) },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(centerAlpha, 0f),
                    new GradientAlphaKey(1f, rimFrac),
                    new GradientAlphaKey(0f, 1f),
                });
            _sweepLine.colorGradient = grad;
        }

        /// <summary>
        /// 程序化构建一个支持顶点色（扫描线渐变）+ 透明混合的线材质。
        /// URP 下运行时新建的 LineRenderer 若不显式赋材质，会回退成紫色（缺着色器）；
        /// 这里用 Unity 自带、任意管线可用的 Internal-Colored（读顶点色）修复，让 beamColor 与拖尾 alpha 生效。
        /// </summary>
        private static Material CreateDefaultLineMaterial()
        {
            Shader sh = Shader.Find("Hidden/Internal-Colored")
                      ?? Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
            var mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            // 以下属性 Internal-Colored 支持（其它着色器若无则忽略），保证拖尾 alpha 正确混合
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            return mat;
        }

        /// <summary>程序化线材质（缓存复用）。扫描线 / 扫描环在未指定 sweepMaterial 时共用此材质。</summary>
        private Material DefaultLineMaterial
        {
            get
            {
                if (_defaultLineMat == null) _defaultLineMat = CreateDefaultLineMaterial();
                return _defaultLineMat;
            }
        }

        /// <summary>刷新目标缓存（捕获 Spawn/销毁）。声呐环模式每 ~1s 调用一次。</summary>
        private void RefreshTargetCache()
        {
            _targets.Clear();
            foreach (SonarTarget t in FindTargets())
            {
                if (t != null) _targets.Add(t);
            }
        }

        /// <summary>推进扫描角度并重绘扫描线（纯视觉，不揭示）。</summary>
        private void UpdateSweep()
        {
            if (_sweepLine == null || boat == null) return;

            _sweepTotal += sweepSpeed * Time.deltaTime; // 持续累加，不取模
            UpdateSweepVisual();
        }

        /// <summary>按当前角度重建扫描线顶点（径向主线 + 边缘拖尾弧），跟随船。</summary>
        private void UpdateSweepVisual()
        {
            Vector2 center = boat.position;
            float leadRad = _sweepTotal * Mathf.Deg2Rad;
            Vector2 leadDir = new Vector2(Mathf.Cos(leadRad), Mathf.Sin(leadRad));
            int segs = Mathf.Max(1, trailSegments);
            float trailDeg = Mathf.Max(0f, beamTrailDegrees);

            int idx = 0;
            // 中心 → 边缘的径向主线（前缘）
            if (drawLeadingLine)
                _sweepPoints[idx++] = (Vector3)center + Vector3.forward * SweepZ;

            // 前缘边缘点
            _sweepPoints[idx++] = (Vector3)(center + leadDir * detectionRange) + Vector3.forward * SweepZ;

            // 拖尾弧：从前缘向后（角度递减）到 lead - trailSpan
            for (int i = 1; i <= segs; i++)
            {
                float t = (float)i / segs;
                float r = (_sweepTotal - t * trailDeg) * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * detectionRange;
                _sweepPoints[idx++] = (Vector3)p + Vector3.forward * SweepZ;
            }

            // idx 应恰好等于 _sweepPointCount
            _sweepLine.positionCount = _sweepPointCount;
            _sweepLine.SetPositions(_sweepPoints);
        }

        // ===== 声呐扫描环 =====

        /// <summary>新建一个声呐环（LineRenderer 闭合圆），加入更新列表。</summary>
        private void SpawnRing()
        {
            var go = new GameObject("SonarPingRing");
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;                                   // 闭合圆
            lr.widthMultiplier = Mathf.Max(0.001f, ringWidth);
            lr.numCornerVertices = 8;
            lr.numCapVertices = 4;
            lr.alignment = LineAlignment.View;
            lr.sortingOrder = 3;
            lr.material = sweepMaterial != null ? sweepMaterial : DefaultLineMaterial; // 复用扫描线材质（未设则程序化）
            lr.positionCount = Mathf.Max(8, ringPoints);

            _rings.Add(new PingRing { Line = lr });
        }

        /// <summary>推进所有声呐环：扩大半径、整体淡出、波前到达的目标触发闪现；到时销毁。</summary>
        private void UpdateRings()
        {
            if (boat == null) return;
            Vector2 center = boat.position;
            float dt = Time.deltaTime;
            float startR = Mathf.Max(0.01f, ringStartRadius);
            float dur = Mathf.Max(0.05f, ringDuration);
            int pts = Mathf.Max(8, ringPoints);

            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                PingRing ring = _rings[i];
                if (ring == null || ring.Line == null)
                {
                    _rings.RemoveAt(i);
                    continue;
                }

                ring.Elapsed += dt;
                float p = Mathf.Clamp01(ring.Elapsed / dur);        // 0→1
                float radius = Mathf.Lerp(startR, detectionRange, p);
                float alpha = 1f - p;                                // 扩散同时整体淡出

                // 重画圆周（跟随船）
                for (int j = 0; j < pts; j++)
                {
                    float ang = (j / (float)pts) * Mathf.PI * 2f;
                    Vector2 pt = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                    ring.Line.SetPosition(j, (Vector3)pt + Vector3.forward * SweepZ);
                }
                Color c = ringColor; c.a = alpha;
                ring.Line.startColor = ring.Line.endColor = c;

                // 波前到达的目标触发闪现（每个环每目标只触发一次）
                float sqrR = radius * radius;
                for (int k = 0; k < _targets.Count; k++)
                {
                    SonarTarget t = _targets[k];
                    if (t == null || t.IsObjective) continue;
                    if (ring.Pinged.Contains(t)) continue;
                    Vector2 d = (Vector2)t.transform.position - center;
                    if (d.sqrMagnitude <= sqrR)
                    {
                        ring.Pinged.Add(t);
                        t.OnPing();
                    }
                }

                if (ring.Elapsed >= dur)
                {
                    Destroy(ring.Line.gameObject);
                    _rings.RemoveAt(i);
                }
            }
        }

        /// <summary>点 ping 按钮（UI Button 的 OnClick 调用此方法）：让当前频段、范围内的物体各自播放一次声音（3D，音量随距离、带方向）。</summary>
        public void Ping()
        {
            if (boat == null) return;

            // 听声（ping）：每次按下扣 1 点电量（无 BatteryManager 时正常 ping，向后兼容）
            if (BatteryManager.Instance != null) BatteryManager.Instance.ConsumeListen();

            Vector2 center = boat.position;
            float sqrRange = detectionRange * detectionRange;
            AudioClip bandClip = GetBandClip(currentBand);

            foreach (SonarTarget t in FindTargets())
            {
                if (t == null || t.Band != currentBand) continue; // 只播当前频段
                float sqrDist = ((Vector2)t.transform.position - center).sqrMagnitude;
                if (sqrDist <= sqrRange)
                {
                    AudioClip clip = t.OverrideClip != null ? t.OverrideClip : bandClip;
                    t.PlayPingSound(clip);
                }
            }
        }

        private AudioClip GetBandClip(SonarBand band)
        {
            int i = (int)band;
            return (bandClips != null && i >= 0 && i < bandClips.Length) ? bandClips[i] : null;
        }

        private static SonarTarget[] FindTargets()
            => Object.FindObjectsByType<SonarTarget>(FindObjectsSortMode.None);

        private void UpdateBandLabel()
        {
            if (bandLabel != null)
                bandLabel.text = SonarBandInfo.GetDisplay(currentBand);
        }

        /// <summary>一个声呐扫描环的运行时数据。</summary>
        private class PingRing
        {
            public LineRenderer Line;
            public float Elapsed;
            public readonly HashSet<SonarTarget> Pinged = new HashSet<SonarTarget>();
        }
    }
}
