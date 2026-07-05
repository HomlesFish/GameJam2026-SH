using UnityEngine;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// 入港结算触发器（碰撞盒驱动）。只负责「触发 + 表现」：检测入港、弹确认面板、传送船入港、显示结算结果。
    /// 真正的结算流程（累计分、清舱、换关 / 全通关）由 <see cref="GameManager"/> 编排。
    ///
    /// 规则：
    ///   0. 触发器：本物体需带 isTrigger 的 Collider2D（不挂则 Awake 自动补一个 autoTriggerSize 的 BoxCollider2D 并设为触发器）。
    ///      船进入触发区时由 OnTriggerEnter2D 驱动。本物体位置由 GameManager 在每关 BeginLevel 时摆到该关结算点（LevelRoot.settlePoint）。
    ///   1. 进港即校验：船进入触发区时检查 <see cref="SalvageInventory.HasObjective"/>。
    ///      - 未获得主要物品：不弹确认 UI，直接用字幕系统提示（id=<see cref="noObjectiveSubtitleId"/>）。
    ///      - 已获得主要物品：弹出「是否进入港口」确认面板（是/否）。
    ///   2. 是：传送船入港 → 调 <see cref="GameManager.SettleCurrentLevel"/>（累计分 + 进 LevelSettled）→ 显示结算结果 → 等「继续」。
    ///   3. 继续：<see cref="GameManager.ContinueAfterSettle"/> → 有下一关则加载下一关，否则全通关 GameOver。
    ///   4. 否 / 驶离：关闭确认面板，玩家继续航行；离开触发区后再次进入才会重新触发。
    ///
    /// 用法：场景里建空 GameObject 挂本脚本（常驻，换关时由 GameManager 摆位）。
    ///   - <see cref="harborAnchor"/>：船入港传送点（不填则用本物体自身位置）；
    ///   - <see cref="boat"/>：玩家船（不填则从 <see cref="GameManager.Boat"/> 取）；
    ///   - <see cref="noObjectiveSubtitleId"/>：未获得主要物品时播放的字幕 id；
    ///   - 确认/结算 UI：confirmPanel + yesButton/noButton，resultPanel + resultScoreText + continueButton。
    ///     按钮点击逻辑由脚本自动绑定，无需手连 OnClick。
    /// </summary>
    public class HarborEntryController : MonoBehaviour
    {
        [Header("触发器")]
        [Tooltip("入港触发区。若本物体未挂任何 Collider2D，Awake 会自动补一个此尺寸的 BoxCollider2D（单位）。")]
        [SerializeField] private Vector2 autoTriggerSize = new Vector2(20f, 20f);

        [Tooltip("船入港/结算时传送到的港口点。不填则用本物体自身位置（换关时由 GameManager 摆到结算点）。")]
        [SerializeField] private Transform harborAnchor;

        [Tooltip("玩家船（结算时用于传送）。不填则从 GameManager.Boat 取。")]
        [SerializeField] private BoatController boat;

        [Header("字幕")]
        [Tooltip("未获得主要物品时用字幕系统提示的字幕 id。需在 SubtitleDatabase 里建一条同名条目。")]
        [SerializeField] private string noObjectiveSubtitleId = "HarborNoObjective";

        [Header("UI（在 Inspector 接入你自己的预制体）")]
        [Tooltip("确认面板根节点：「是否进入港口?」+ 是/否按钮。仅在已获得主要物品时弹出。")]
        [SerializeField] private GameObject confirmPanel;

        [Tooltip("确认面板的「是」按钮。")]
        [SerializeField] private Button yesButton;

        [Tooltip("确认面板的「否」按钮。")]
        [SerializeField] private Button noButton;

        [Tooltip("结算结果面板根节点：显示最终积分 +「继续」按钮。")]
        [SerializeField] private GameObject resultPanel;

        [Tooltip("结算结果面板的积分文本（由脚本写入跨关累计积分）。")]
        [SerializeField] private Text resultScoreText;

        [Tooltip("结算结果面板的「继续」按钮：点击进入下一关 / 全通关。")]
        [SerializeField] private Button continueButton;

        [Header("行为")]
        [Tooltip("确认「是」后是否把船传送到港口位置。")]
        [SerializeField] private bool moveBoatToHarborOnEntry = true;

        [Tooltip("结算结果积分文本的前缀。")]
        [SerializeField] private string resultScorePrefix = "最终积分：";

        private bool _awaitingResponse;       // 确认面板已弹出、等待玩家点 是/否
        private bool _triggeredThisEntry;     // 本次进入触发区已响应过（离开前不重复触发）
        private Transform _boatTransform;

        private void Awake()
        {
            // 确保存在一个触发器碰撞盒：没挂就自动补 BoxCollider2D；已挂（任意 Collider2D）只补设 isTrigger，不动其形状
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = autoTriggerSize;
                col = box;
            }
            col.isTrigger = true;

            if (harborAnchor == null) harborAnchor = transform;
        }

        private void Start()
        {
            ResolveBoat();
            WireButtons();
            SetActive(confirmPanel, false);
            SetActive(resultPanel, false);
        }

        private void ResolveBoat()
        {
            // 优先用 Inspector 拖入的；否则从 GameManager 取（统一真相源）
            if (boat == null && GameManager.Instance != null) boat = GameManager.Instance.Boat;
            _boatTransform = boat != null ? boat.transform : null;
            if (_boatTransform == null)
                Debug.LogWarning("[HarborEntryController] 未找到船，结算时无法传送入港。", this);
        }

        // ===== 触发器驱动 =====

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsBoat(other)) return;
            // 已全通关 / 本关已结算：忽略，避免结算后重复触发
            if (GameManager.Instance != null)
            {
                var s = GameManager.Instance.CurrentState;
                if (s == GameManager.GameState.GameOver || s == GameManager.GameState.LevelSettled) return;
            }
            // 本次进入已响应过（确认面板还开着 / 字幕已播），离开前不重复
            if (_awaitingResponse || _triggeredThisEntry) return;

            _triggeredThisEntry = true;

            bool hasObjective = SalvageInventory.Instance != null && SalvageInventory.Instance.HasObjective;
            if (hasObjective)
            {
                ShowConfirm();
            }
            else
            {
                // 未获得主要物品：不弹确认 UI，直接走字幕系统提示
                if (SubtitleManager.Instance != null)
                    SubtitleManager.Instance.Show(noObjectiveSubtitleId);
                else
                    Debug.LogWarning("[HarborEntryController] SubtitleManager 未找到，无法播放未获得主要物品字幕提示。", this);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!IsBoat(other)) return;
            // 驶离触发区：重新武装，允许下次进入再触发；若确认面板还开着则一并关闭
            _triggeredThisEntry = false;
            if (_awaitingResponse) CloseConfirm();
        }

        /// <summary>判定相撞的是不是玩家船（船及其子物体上挂了 BoatController 即算）。</summary>
        private static bool IsBoat(Collider2D other)
        {
            return other != null && other.GetComponentInParent<BoatController>() != null;
        }

        // ===== 流程入口 =====

        private void ShowConfirm()
        {
            _awaitingResponse = true;
            // 弹出确认面板期间强制停船：船往往正以高速驶入触发区，若继续按惯性前进 / 被玩家操控，
            // 既会漂出触发区导致 OnTriggerExit2D 反复开关面板，玩家也难以边操船边做决定。
            if (boat != null) boat.SetFrozen(true);
            SetActive(confirmPanel, true);
        }

        private void CloseConfirm()
        {
            _awaitingResponse = false;
            // 关闭面板恢复操控：点「否」继续航行；点「是」随后会传送 + ResetMotion + 进 LevelSettled
            if (boat != null) boat.SetFrozen(false);
            SetActive(confirmPanel, false);
        }

        /// <summary>确认面板「是」按钮回调：传送船入港 → 交 GameManager 结算 → 显示结果。</summary>
        public void OnConfirmYes()
        {
            if (!_awaitingResponse) return;
            CloseConfirm();

            // 表现：船进入港口（传送到港口点）
            if (moveBoatToHarborOnEntry && _boatTransform != null)
            {
                _boatTransform.position = harborAnchor.position;
                // 清零运动状态：否则入港前残留的 currentSpeed 会在下一帧把船推出港口，
                // 看起来像「没传送到 endport / 刚到又跑掉了」。
                if (boat != null) boat.ResetMotion();
            }

            // 流程：累计本关分 + 进入 LevelSettled（由 GameManager 编排；本脚本不再算分/清舱/EndGame）
            if (GameManager.Instance != null) GameManager.Instance.SettleCurrentLevel();

            ShowResult();
        }

        /// <summary>确认面板「否」按钮回调：关闭面板，继续航行。</summary>
        public void OnConfirmNo()
        {
            if (!_awaitingResponse) return;
            CloseConfirm();
        }

        // ===== 结算结果 =====

        /// <summary>显示结算结果面板：写入跨关累计总分（<see cref="GameManager.TotalScore"/>，已含本关）。</summary>
        private void ShowResult()
        {
            int score = GameManager.Instance != null ? GameManager.Instance.TotalScore : 0;
            if (resultScoreText != null) resultScoreText.text = resultScorePrefix + score;
            SetActive(resultPanel, true);
        }

        /// <summary>结算面板「继续」按钮回调：进入下一关 / 全通关（由 GameManager 决定）。</summary>
        public void OnContinue()
        {
            SetActive(resultPanel, false);
            if (GameManager.Instance != null) GameManager.Instance.ContinueAfterSettle();
        }

        // ===== 工具 =====

        private void WireButtons()
        {
            if (yesButton != null) { yesButton.onClick.RemoveAllListeners(); yesButton.onClick.AddListener(OnConfirmYes); }
            if (noButton != null) { noButton.onClick.RemoveAllListeners(); noButton.onClick.AddListener(OnConfirmNo); }
            if (continueButton != null) { continueButton.onClick.RemoveAllListeners(); continueButton.onClick.AddListener(OnContinue); }
        }

        private static void SetActive(GameObject go, bool value)
        {
            if (go != null) go.SetActive(value);
        }
    }
}
