using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// 声呐面板 UI：
    /// - HUD 左上角：当前任务的目标物品图标 + 右侧喇叭按钮（点击播放"船锚撞击目标物品"音效）；
    ///   玩家获得目标物品后，目标图标隐藏、改显「√」（checkmarkSprite）表示目标完成。
    /// - 面板上方是声呐信息，下方自上而下按打捞顺序显示已打捞的支线物品**名字**。
    /// - 全通关（GameManager 进入 GameOver）时，把跨关累计总分填到 <see cref="resultScoreText"/>。
    ///
    /// 目标物品的**图标与音效每关不同**，因此不在此处硬编码：解析场景里 isObjective 的
    /// <see cref="SonarTarget"/>，自动取其 UiIcon / GetUiSoundClip()。<b>换关时</b>订阅
    /// <see cref="GameEvents.LevelStarted"/> 重新解析（主线目标随关替换）。
    ///
    /// 订阅 <see cref="SalvageInventory.OnChanged"/> 刷新物品列表与目标物品状态。
    /// 用法：面板上挂本脚本；listContainer 挂 Vertical Layout Group；rowPrefab 带一个 UnityEngine.UI.Text；
    /// resultScoreText 是结算界面的总分文本（可不接，单关结算分由 HarborEntryController 驱动）。
    /// 目标物品 UI（Inspector 接线，仅 UI 部件，与具体关卡无关）：objectiveIcon 接左上角的 Image（显示目标图标）；
    /// checkmarkSprite 接「√」Image（获得后才出现）；soundButton 接喇叭按钮。
    /// 本脚本靠 <see cref="RequireComponent"/> 自带一个 <see cref="AudioSource"/> 用于播放音效。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SonarUIController : MonoBehaviour, IGameEventListener
    {
        [Tooltip("列表容器（建议挂 Vertical Layout Group）")]
        [SerializeField] private Transform listContainer;

        [Tooltip("行预制体：根节点或其子节点需带 UnityEngine.UI.Text")]
        [SerializeField] private GameObject rowPrefab;

        [Tooltip("全通关结算界面的总分文本（可不接；单关结算分由 HarborEntryController 驱动）")]
        [SerializeField] private Text resultScoreText;

        [Header("目标物品 UI（HUD 左上角，目标自动读取）")]
        [Tooltip("目标物品图标")]
        [SerializeField] private Image objectiveIcon;

        [Tooltip("已获得目标物品后替换显示的「√」")]
        [SerializeField] private Image checkmarkSprite;

        [Tooltip("喇叭按钮：点击播放本关目标物品的音效")]
        [SerializeField] private Button soundButton;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private AudioSource _audio;
        private AudioClip _objectiveClip;     // 本关主线目标的喇叭音效（缓存，目标被打捞后会从场景移除）
        private bool _objectiveReady;         // 是否成功解析到本关主线目标（false 时隐藏左上角目标 UI）

        private void Awake()
        {
            _audio = GetComponent<AudioSource>();
            // 「√」默认隐藏，获得目标物品后才出现（显隐统一由 RefreshObjectiveState 接管）
            if (checkmarkSprite != null) checkmarkSprite.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            SafeSubscribeInventory();
            SafeSubscribeGameState();
            EventBus.Subscribe(GameEvents.LevelStarted, this);
        }

        private void OnDisable()
        {
            if (SalvageInventory.Instance != null)
            {
                SalvageInventory.Instance.OnChanged -= Rebuild;
                SalvageInventory.Instance.OnChanged -= RefreshObjectiveState;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
            EventBus.Unsubscribe(GameEvents.LevelStarted, this);
        }

        /// <inheritdoc />
        public void OnEventRaised(string eventId)
        {
            if (eventId == GameEvents.LevelStarted)
            {
                // 换关：本关主线目标 SonarTarget 已替换，重新解析图标/音效 + 重建列表 + 刷新目标状态
                ResolveObjective();
                if (SalvageInventory.Instance != null) Rebuild();
                RefreshObjectiveState();
            }
        }

        private void Start()
        {
            // Start 时所有 Awake 已跑完：补订阅各 Manager（防 OnEnable 早于对应 Awake 漏订）
            SafeSubscribeInventory();
            SafeSubscribeGameState();
            // 解析本关主线目标 → 图标/音效（每关不同，从场景里的 isObjective SonarTarget 读取）
            ResolveObjective();
            if (SalvageInventory.Instance != null) Rebuild();
            RefreshObjectiveState();   // 应用 目标图标 / 「√」 的显隐
            WireSoundButton();

            if (_objectiveReady && objectiveIcon != null && checkmarkSprite == null)
                Debug.LogWarning("[SonarUIController] 未配置 checkmarkSprite：获得目标物品后将无法显示「√」。", this);
        }

        /// <summary>幂等订阅 SalvageInventory.OnChanged（重复调用不会多次订阅）。</summary>
        private void SafeSubscribeInventory()
        {
            if (SalvageInventory.Instance == null) return;
            SalvageInventory.Instance.OnChanged -= Rebuild;
            SalvageInventory.Instance.OnChanged += Rebuild;
            SalvageInventory.Instance.OnChanged -= RefreshObjectiveState;
            SalvageInventory.Instance.OnChanged += RefreshObjectiveState;
        }

        /// <summary>幂等订阅 GameManager.OnStateChanged（重复调用不会多次订阅）。</summary>
        private void SafeSubscribeGameState()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
        }

        private void Rebuild()
        {
            // 销毁旧行
            foreach (GameObject row in _rows)
            {
                if (row != null) Destroy(row);
            }
            _rows.Clear();

            if (listContainer == null || rowPrefab == null || SalvageInventory.Instance == null) return;

            // 按打捞顺序重建：第一个打捞的排最上；只显示物品名字
            foreach (SalvageRecord rec in SalvageInventory.Instance.Items)
            {
                GameObject row = Instantiate(rowPrefab, listContainer);
                Text text = row.GetComponentInChildren<Text>();
                if (text != null) text.text = rec.Name;
                _rows.Add(row);
            }
        }

        /// <summary>扫描场景里 isObjective 的 <see cref="SonarTarget"/>，取本关目标物品的图标与音效。
        /// 找不到（本关无主线目标）则隐藏左上角目标 UI；目标被打捞后虽从场景移除，但图标/音效已在此时缓存。</summary>
        private void ResolveObjective()
        {
            SonarTarget objective = FindObjectiveTarget();
            if (objective == null)
            {
                _objectiveReady = false;
                if (objectiveIcon != null) objectiveIcon.gameObject.SetActive(false);
                if (checkmarkSprite != null) checkmarkSprite.gameObject.SetActive(false);
                if (soundButton != null) soundButton.gameObject.SetActive(false);
                return;
            }

            _objectiveReady = true;
            _objectiveClip = objective.GetUiSoundClip();

            // 目标图标贴图（每关不同）；显隐交由 RefreshObjectiveState 按 HasObjective 切换
            if (objectiveIcon != null)
            {
                objectiveIcon.sprite = objective.UiIcon;
                objectiveIcon.gameObject.SetActive(true);
                if (objective.UiIcon == null)
                    Debug.LogWarning($"[SonarUIController] 主线目标 {objective.name} 未配置 uiIcon，左上角图标无贴图。", this);
            }
            if (soundButton != null) soundButton.gameObject.SetActive(true);
            if (_objectiveClip == null)
                Debug.LogWarning($"[SonarUIController] 主线目标 {objective.name} 既无 uiSoundClip 也无 overrideClip，喇叭按钮点击无声。", this);
        }

        /// <summary>找到场景里第一个 isObjective 的 <see cref="SonarTarget"/>（本关的主线目标）。</summary>
        private static SonarTarget FindObjectiveTarget()
        {
            foreach (SonarTarget t in FindObjectsByType<SonarTarget>(FindObjectsSortMode.None))
            {
                if (t != null && t.IsObjective) return t;
            }
            return null;
        }

        /// <summary>按是否已获得主线目标物品（<see cref="SalvageInventory.HasObjective"/>）切换显隐：
        /// 未获得 → 显示目标物品原图、隐藏「√」；已获得 → 隐藏原图、显示「√」。清舱重开时自动还原。</summary>
        private void RefreshObjectiveState()
        {
            if (!_objectiveReady) return;
            bool obtained = SalvageInventory.Instance != null && SalvageInventory.Instance.HasObjective;

            if (checkmarkSprite != null) checkmarkSprite.gameObject.SetActive(obtained);
        }

        /// <summary>绑定喇叭按钮点击 → 播放目标物品音效（幂等：RemoveAllListeners 后再 Add，重复调用不会叠加监听）。</summary>
        private void WireSoundButton()
        {
            if (soundButton == null) return;
            soundButton.onClick.RemoveAllListeners();
            soundButton.onClick.AddListener(PlayObjectiveSound);
        }

        /// <summary>喇叭按钮回调：用面板自带 AudioSource 一次性播放本关目标物品的音效。</summary>
        private void PlayObjectiveSound()
        {
            if (_audio == null || _objectiveClip == null) return;
            _audio.PlayOneShot(_objectiveClip);
        }

        /// <summary>游戏状态变化：进入 GameOver（全通关）时填写跨关累计总分。</summary>
        private void HandleStateChanged(GameManager.GameState newState, GameManager.GameState oldState)
        {
            if (newState != GameManager.GameState.GameOver) return;
            if (resultScoreText == null) return;

            int total = GameManager.Instance != null ? GameManager.Instance.TotalScore : 0;
            resultScoreText.text = total.ToString();
        }
    }
}
