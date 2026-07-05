using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 音频管理器（单例）。所有音频剪辑在 Inspector 里公开，直接拖入 / 替换，无需改代码。
    ///
    /// 加载优先级：Inspector 拖入的剪辑优先；某格留空则回退按文件名从
    /// Resources/Audio/ 加载（文件名见每格 Tooltip）；两边都没有只打 Warning 不报错。
    /// 所以可以先什么都不拖直接跑（沿用 Resources 约定），之后想换哪条拖哪条。
    ///
    /// 【自动接管】环境层随 GameManager 状态淡入淡出、船体呻吟随机播放、
    /// 引擎随船速变调（读 BoatController.Speed01）、低电量层与报警哔随 BatteryManager 事件启停、
    /// 暂停全局静音。以上零接线。
    ///
    /// 【被动接口】按钮 / 抛锚 / 捕捞 / 撞礁 / 爆炸 / denied / 确认叮 / 调频 / 听声 ducking / 字幕静电,
    /// 由各系统一行调用（BoatController 等已接好的调用不受本次改动影响，方法签名未变）。
    /// </summary>
    public class SoundManager : SingletonBehaviour<SoundManager>
    {
        protected override bool PersistOnLoad => true;

        // ==================================================================
        // Inspector 音频槽（留空 = 回退 Resources/Audio/ 同名文件）
        // ==================================================================

        [Header("环境循环层")]
        [Tooltip("舱内底噪循环。回退文件名: amb_cabin_lp")]
        [SerializeField] private AudioClip ambCabin;

        [Tooltip("仪表电流循环。回退: amb_electronics_lp")]
        [SerializeField] private AudioClip ambElectronics;

        [Tooltip("声呐持续扫描循环。回退: lp_radar_sweep")]
        [SerializeField] private AudioClip radarSweepLoop;

        [Tooltip("引擎怠速循环（随船速自动变调）。回退: lp_engine_idle")]
        [SerializeField] private AudioClip engineIdle;

        [Tooltip("低电量附加循环层。回退: amb_lowpower_lp")]
        [SerializeField] private AudioClip ambLowPower;

        [Tooltip("船体随机呻吟（多个变体随机抽取）。回退: amb_hull_creak_01~04")]
        [SerializeField] private AudioClip[] hullCreaks;

        [Tooltip("电台底噪（字幕静电爆点从中随机截取）。回退: lp_radio_static")]
        [SerializeField] private AudioClip radioStatic;

        [Header("按钮")]
        [Tooltip("通用轻按（UI / 暂停 / 开始）。回退: btn_press_light")]
        [SerializeField] private AudioClip btnPressLight;

        [Tooltip("通用重按（抛锚 / 捕捞 / 投弹三键共用，自动变调区分）。回退: btn_press_heavy")]
        [SerializeField] private AudioClip btnPressHeavy;

        [Tooltip("调频旋钮咔哒（随频段自动微变调）。回退: btn_freq_tune")]
        [SerializeField] private AudioClip btnFreqTune;

        [Tooltip("操作被拒 buzz。回退: btn_denied")]
        [SerializeField] private AudioClip btnDenied;

        [Tooltip("确认叮（入舱单响 / 主线双响）。回退: ui_ding")]
        [SerializeField] private AudioClip uiDing;

        [Header("玩法")]
        [Tooltip("抛锚：入水 + 链条 + 触底。回退: anchor_drop")]
        [SerializeField] private AudioClip anchorDrop;

        [Tooltip("捕捞成功：绞盘重载出水（落空音由此素材自动变调生成）。回退: salvage_success")]
        [SerializeField] private AudioClip salvageSuccess;

        [Tooltip("撞暗礁巨响。回退: evt_reef_impact")]
        [SerializeField] private AudioClip reefImpact;

        [Tooltip("深水炸弹爆炸。回退: evt_depthcharge_explode")]
        [SerializeField] private AudioClip depthChargeExplode;

        [Tooltip("低电量报警哔（单声，间隔由代码控制）。回退: lp_lowpower_warn")]
        [SerializeField] private AudioClip lowPowerWarn;

        // ==================================================================
        // 参数
        // ==================================================================

        [Header("引擎")]
        [Tooltip("船速满格时引擎循环的音高（+4半音≈1.26）")]
        [SerializeField] private float enginePitchMax = 1.26f;

        [Header("听声 Ducking")]
        [Tooltip("听声期间环境循环音量的压低倍率（0~1）")]
        [SerializeField] private float listenDuckFactor = 0.35f;

        [Tooltip("Ducking 渐变时长（秒）")]
        [SerializeField] private float duckFadeTime = 0.2f;

        [Header("低电量（自动接管）")]
        [Tooltip("触发低电报警的电量百分比阈值")]
        [SerializeField] private float lowPowerThreshold = 25f;

        [Tooltip("阈值处的哔声间隔（秒）")]
        [SerializeField] private float warnIntervalMax = 2.0f;

        [Tooltip("电量趋近 0 时的哔声间隔（秒）")]
        [SerializeField] private float warnIntervalMin = 0.8f;

        [Header("船体呻吟（自动接管）")]
        [Tooltip("随机呻吟的最短间隔（秒）")]
        [SerializeField] private float creakIntervalMin = 15f;

        [Tooltip("随机呻吟的最长间隔（秒）")]
        [SerializeField] private float creakIntervalMax = 40f;

        [Header("雷达扫描音（自动接管）")]
        [Tooltip("雷达嘀声的播放间隔（秒）。视觉扫描环是 2 秒一圈，声音间隔可独立调")]
        [SerializeField] private float radarInterval = 3f;

        [Tooltip("雷达嘀声音量")]
        [SerializeField] private float radarVolume = 0.25f;

        // ===== 内部 =====

        private class LoopEntry
        {
            public AudioSource Source;
            public float BaseVolume;
            public bool IsAmbient;
        }

        private readonly Dictionary<string, AudioClip> _resourceCache = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, LoopEntry> _loops = new Dictionary<string, LoopEntry>();
        private float _duck = 1f;
        private Coroutine _duckRoutine;
        private Coroutine _creakRoutine;
        private Coroutine _radarRoutine;
        private Coroutine _lowPowerRoutine;
        private AudioSource _engine;
        private BoatController _boat;
        private float _powerPercent = 100f;
        private bool _ambienceOn;

        // ==================================================================
        // 自动接管：订阅 / 轮询
        // ==================================================================

        private void OnEnable() => SafeSubscribe();

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            if (BatteryManager.Instance != null)
                BatteryManager.Instance.OnBatteryChanged -= HandleBatteryChanged;
        }

        private void Start()
        {
            SafeSubscribe();
            if (GameManager.Instance != null)
                ApplyState(GameManager.Instance.CurrentState);
        }

        private void SafeSubscribe()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                GameManager.Instance.OnStateChanged += HandleStateChanged;
            }
            if (BatteryManager.Instance != null)
            {
                BatteryManager.Instance.OnBatteryChanged -= HandleBatteryChanged;
                BatteryManager.Instance.OnBatteryChanged += HandleBatteryChanged;
                _powerPercent = BatteryManager.Instance.Normalized * 100f;
            }
        }

        private void HandleStateChanged(GameManager.GameState newState, GameManager.GameState oldState)
            => ApplyState(newState);

        private void ApplyState(GameManager.GameState state)
        {
            AudioListener.pause = state == GameManager.GameState.Paused;

            switch (state)
            {
                case GameManager.GameState.Playing:
                    StartAmbience();
                    break;
                case GameManager.GameState.MainMenu:
                case GameManager.GameState.GameOver:
                    StopAmbience();
                    break;
                    // Paused / LevelSettled：环境保持
            }
        }

        private void StartAmbience()
        {
            if (_ambienceOn) return;
            _ambienceOn = true;
            StartLoop("cabin", Clip(ambCabin, "amb_cabin_lp"), 0.8f, 1.5f);
            StartCoroutine(Delayed(0.8f, () => { if (_ambienceOn) StartLoop("engine", Clip(engineIdle, "lp_engine_idle"), 0.7f, 1.5f); }));
            StartCoroutine(Delayed(1.6f, () => { if (_ambienceOn) StartLoop("electronics", Clip(ambElectronics, "amb_electronics_lp"), 0.35f, 1.5f); }));
            if (_radarRoutine == null) _radarRoutine = StartCoroutine(RadarTicks());
            if (_creakRoutine == null) _creakRoutine = StartCoroutine(RandomCreaks());
        }

        private void StopAmbience()
        {
            if (!_ambienceOn) return;
            _ambienceOn = false;
            if (_creakRoutine != null) { StopCoroutine(_creakRoutine); _creakRoutine = null; }
            if (_radarRoutine != null) { StopCoroutine(_radarRoutine); _radarRoutine = null; }
            StopLowPowerLayer();
            StopAllLoops(1f);
        }

        /// <summary>雷达嘀声：每 radarInterval 秒播一次（受听声 ducking 影响的话可自行加 _duck 系数）。</summary>
        private IEnumerator RadarTicks()
        {
            yield return new WaitForSeconds(2.4f);   // 与环境层淡入错开
            while (true)
            {
                if (_ambienceOn)
                    PlayClip(Clip(radarSweepLoop, "lp_radar_sweep"), radarVolume * _duck);
                yield return new WaitForSeconds(Mathf.Max(0.2f, radarInterval));
            }
        }

        private void Update()
        {
            if (!_ambienceOn) return;
            if (_engine == null && _loops.TryGetValue("engine", out LoopEntry e))
                _engine = e.Source;
            if (_engine == null) return;

            if (_boat == null && GameManager.Instance != null)
                _boat = GameManager.Instance.Boat;
            if (_boat == null) return;

            _engine.pitch = Mathf.Lerp(1f, enginePitchMax, Mathf.Clamp01(_boat.Speed01));
        }

        private void HandleBatteryChanged(int current, int max)
        {
            _powerPercent = max > 0 ? current * 100f / max : 0f;
            bool shouldWarn = _powerPercent > 0f && _powerPercent <= lowPowerThreshold && _ambienceOn;

            if (shouldWarn && _lowPowerRoutine == null)
            {
                StartLoop("lowpower", Clip(ambLowPower, "amb_lowpower_lp"), 0.5f, 2f);
                _lowPowerRoutine = StartCoroutine(LowPowerBeeps());
            }
            else if (!shouldWarn)
            {
                StopLowPowerLayer();
            }
        }

        private void StopLowPowerLayer()
        {
            if (_lowPowerRoutine != null) { StopCoroutine(_lowPowerRoutine); _lowPowerRoutine = null; }
            StopLoop("lowpower", 1f);
        }

        private IEnumerator LowPowerBeeps()
        {
            while (true)
            {
                PlayClip(Clip(lowPowerWarn, "lp_lowpower_warn"), 0.6f);
                float t = Mathf.InverseLerp(lowPowerThreshold, 0f, _powerPercent);
                yield return new WaitForSeconds(Mathf.Lerp(warnIntervalMax, warnIntervalMin, t));
            }
        }

        private IEnumerator RandomCreaks()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(creakIntervalMin, creakIntervalMax));
                if (!_ambienceOn) continue;
                AudioClip clip = PickCreak();
                if (clip != null) PlayClip(clip, 0.4f);
            }
        }

        /// <summary>取一条船体呻吟：Inspector 数组有内容则随机抽取，否则回退 Resources 的 01~04。</summary>
        private AudioClip PickCreak()
        {
            if (hullCreaks != null && hullCreaks.Length > 0)
            {
                AudioClip c = hullCreaks[Random.Range(0, hullCreaks.Length)];
                if (c != null) return c;
            }
            int i = Random.Range(1, 5);
            return Clip(null, $"amb_hull_creak_0{i}");
        }

        // ==================================================================
        // 被动接口：游戏性音效（方法签名与旧版一致，调用方无需改动）
        // ==================================================================

        /// <summary>半音转 pitch（+n 半音 = Pow(2, n/12)）。</summary>
        public static float Semitone(float n) => Mathf.Pow(2f, n / 12f);

        // ---- 三大重型按钮共用重按素材变调 ----
        public void PlayAnchorButton() => PlayClip(Clip(btnPressHeavy, "btn_press_heavy"), 1f, Semitone(-1)); // BoatController.BeginAnchor
        public void PlaySalvageButton() => PlayClip(Clip(btnPressHeavy, "btn_press_heavy"), 1f, Semitone(0));  // BoatController.Salvage
        public void PlayDepthChargeButton() => PlayClip(Clip(btnPressHeavy, "btn_press_heavy"), 1f, Semitone(1));  // DepthChargeLauncher.Drop

        /// <summary>通用轻按。UI 按钮 / 暂停 / 开始用。</summary>
        public void PlayLightButton() => PlayClip(Clip(btnPressLight, "btn_press_light"));

        /// <summary>调频咔哒。SonarSystem.HandleBandSwitch 调用，传 (int)currentBand（0/1/2）。</summary>
        public void PlayFreqTune(int band) => PlayClip(Clip(btnFreqTune, "btn_freq_tune"), 1f, Semitone(band - 1));

        /// <summary>操作被拒 buzz。弹药耗尽 / 货舱满 / 电量不足。</summary>
        public void PlayDenied() => PlayClip(Clip(btnDenied, "btn_denied"));

        /// <summary>雷达单次 blip（可选，扫描循环已常驻）。复用轻按素材极轻高调。</summary>
        public void PlayRadarBlip() => PlayClip(Clip(btnPressLight, "btn_press_light"), 0.15f, Semitone(7));

        /// <summary>声呐 ping：2D 播放某目标的听声音（不走 3D 距离衰减，永远可听见），
        /// 并按目标相对船的方位做左右声道 pan（屏幕左右方向感）。SonarSystem.Ping 经 SonarTarget 调用。</summary>
        public void PlaySonarPing(AudioClip clip, Vector3 worldPos)
        {
            if (clip == null) return;
            float pan = 0f;
            // 船始终在屏幕正中，目标屏幕方位 = 目标世界位 - 船世界位；pan 取其方向余弦（纯侧方 ±1、正前/正后 0）。
            if (GameManager.Instance != null && GameManager.Instance.Boat != null)
            {
                Vector2 boat = GameManager.Instance.Boat.transform.position;
                Vector2 d = (Vector2)worldPos - boat;
                float dist = d.magnitude;
                if (dist > 0.0001f) pan = Mathf.Clamp(d.x / dist, -1f, 1f);
            }
            PlayClip(clip, 1f, 1f, pan);
        }

        /// <summary>抛锚音。BoatController.BeginAnchor 调用。</summary>
        public void PlayAnchorDrop() => PlayClip(Clip(anchorDrop, "anchor_drop"));

        /// <summary>捕捞结果。成功=原素材；落空=同素材升 2 半音截前段，不需要单独文件。</summary>
        public void PlaySalvageResult(bool success)
        {
            AudioClip clip = Clip(salvageSuccess, "salvage_success");
            if (clip == null) return;
            if (success) { PlayClip(clip); return; }

            AudioSource src = SpawnSource("sfx_salvage_empty");
            src.clip = clip;
            src.pitch = Semitone(2);
            src.volume = 0.7f;
            src.Play();
            StartCoroutine(FadeOutAndKill(src, 0.3f, delay: clip.length * 0.45f));
        }

        /// <summary>撞暗礁巨响。BoatController.OnTriggerEnter2D 调用。</summary>
        public void PlayReefImpact() => PlayClip(Clip(reefImpact, "evt_reef_impact"));

        /// <summary>深弹爆炸。DepthChargeLauncher.Detonate 调用。</summary>
        public void PlayExplosion() => PlayClip(Clip(depthChargeExplode, "evt_depthcharge_explode"));

        /// <summary>确认叮。入舱单响；主线目标传 true 双响（原调 + 升 4 半音）。</summary>
        public void PlayConfirm(bool isMainQuest = false)
        {
            AudioClip clip = Clip(uiDing, "ui_ding");
            PlayClip(clip);
            if (isMainQuest)
                StartCoroutine(Delayed(0.25f, () => PlayClip(clip, 1f, Semitone(4))));
        }

        /// <summary>电台静电爆点：从电台底噪随机截 0.4 秒。SubtitleManager.Show 调用；剧情故障传 true 更响。</summary>
        public void PlayRadioBurst(bool isGlitch = false)
        {
            AudioClip clip = Clip(radioStatic, "lp_radio_static");
            if (clip == null) return;
            AudioSource src = SpawnSource("sfx_radio_burst");
            src.clip = clip;
            src.volume = isGlitch ? 1f : 0.5f;
            src.time = Random.Range(0f, Mathf.Max(0.1f, clip.length - 0.5f));
            src.Play();
            StartCoroutine(FadeOutAndKill(src, 0.1f, delay: 0.35f));
        }

        /// <summary>听声 ducking。SonarSystem.Ping 开头 true，约 5 秒后 false。只压环境循环层。</summary>
        public void SetListenDuck(bool on)
        {
            if (_duckRoutine != null) StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(DuckRoutine(on ? listenDuckFactor : 1f));
        }

        // ==================================================================
        // 基础播放
        // ==================================================================

        /// <summary>播放任意剪辑（一次性）。给临时需求 / 外部剪辑用。</summary>
        public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            if (clip == null) return;
            AudioSource src = SpawnSource("sfx_" + clip.name);
            src.clip = clip;
            src.volume = volume;
            src.pitch = Mathf.Max(0.1f, pitch);
            src.panStereo = Mathf.Clamp(pan, -1f, 1f);
            src.Play();
            Destroy(src.gameObject, clip.length / src.pitch + 0.1f);
        }

        /// <summary>启动循环层（key 唯一）。isAmbient=true 的层参与听声 ducking。</summary>
        public void StartLoop(string key, AudioClip clip, float volume = 1f, float fadeIn = 0.5f, bool isAmbient = true)
        {
            if (clip == null || _loops.ContainsKey(key)) return;

            AudioSource src = SpawnSource("loop_" + key);
            src.clip = clip;
            src.loop = true;
            src.volume = 0f;
            src.Play();

            var entry = new LoopEntry { Source = src, BaseVolume = volume, IsAmbient = isAmbient };
            _loops[key] = entry;
            StartCoroutine(FadeLoopTo(entry, volume, fadeIn));
        }

        public void StopLoop(string key, float fadeOut = 0.5f)
        {
            if (!_loops.TryGetValue(key, out LoopEntry entry)) return;
            _loops.Remove(key);
            StartCoroutine(FadeOutAndKill(entry.Source, fadeOut));
        }

        public void StopAllLoops(float fadeOut = 0.5f)
        {
            foreach (var entry in _loops.Values)
                StartCoroutine(FadeOutAndKill(entry.Source, fadeOut));
            _loops.Clear();
            _engine = null;
        }

        // ==================================================================
        // 内部工具
        // ==================================================================

        /// <summary>取剪辑：Inspector 拖入的优先；留空回退 Resources/Audio/<fallbackName>；都没有打 Warning。</summary>
        private AudioClip Clip(AudioClip inspectorClip, string fallbackName)
        {
            if (inspectorClip != null) return inspectorClip;

            if (_resourceCache.TryGetValue(fallbackName, out AudioClip cached)) return cached;
            AudioClip clip = Resources.Load<AudioClip>("Audio/" + fallbackName);
            if (clip == null)
                Debug.LogWarning($"[Sound] 音频缺失: Inspector 未拖入且 Resources/Audio/{fallbackName} 不存在（已跳过）");
            _resourceCache[fallbackName] = clip;
            return clip;
        }

        private AudioSource SpawnSource(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // 全 2D：所有 SFX（含声呐 ping）都不走 3D 距离衰减，保证可听见
            return src;
        }

        private IEnumerator DuckRoutine(float target)
        {
            float start = _duck, t = 0f;
            while (t < duckFadeTime)
            {
                t += Time.unscaledDeltaTime;
                _duck = Mathf.Lerp(start, target, t / duckFadeTime);
                ApplyDuck();
                yield return null;
            }
            _duck = target;
            ApplyDuck();
            _duckRoutine = null;
        }

        private void ApplyDuck()
        {
            foreach (var entry in _loops.Values)
            {
                if (entry.IsAmbient && entry.Source != null)
                    entry.Source.volume = entry.BaseVolume * _duck;
            }
        }

        private IEnumerator FadeLoopTo(LoopEntry entry, float target, float duration)
        {
            entry.BaseVolume = target;
            float start = entry.Source != null ? entry.Source.volume : 0f;
            float t = 0f;
            while (t < duration && entry.Source != null)
            {
                t += Time.unscaledDeltaTime;
                float v = Mathf.Lerp(start, target, t / duration);
                entry.Source.volume = entry.IsAmbient ? v * _duck : v;
                yield return null;
            }
        }

        private IEnumerator FadeOutAndKill(AudioSource src, float duration, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (src == null) yield break;
            float start = src.volume, t = 0f;
            while (t < duration && src != null)
            {
                t += Time.unscaledDeltaTime;
                src.volume = Mathf.Lerp(start, 0f, t / duration);
                yield return null;
            }
            if (src != null) Destroy(src.gameObject);
        }

        private IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action?.Invoke();
        }
    }
}