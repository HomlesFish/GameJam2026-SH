using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 暂停菜单控制器：ESC 唤起 / 关闭暂停面板，并处理面板上「继续 / 重玩 / 退出」按钮。
    ///
    /// 机制：
    ///   - <see cref="Update"/> 轮询 <see cref="pauseKey"/>（默认 ESC）：仅在 <see cref="GameManager.GameState.Playing"/>
    ///     时唤起、仅在 <see cref="GameManager.GameState.Paused"/> 时关闭，主菜单/结算/游戏结束等状态忽略，避免误触。
    ///   - 订阅 <see cref="GameManager.OnStateChanged"/>：进入 Paused 显示面板、离开 Paused 隐藏面板。
    ///     以状态为唯一真相源，ESC、按钮、外部代码调用 <see cref="GameManager.PauseGame"/> 三者不会失同步。
    ///   - 实际冻结由 <see cref="GameManager.SetState"/> 统一把 <c>Time.timeScale</c> 置 0 实现；恢复时还原 1。
    ///     <see cref="Update"/> 与按钮点击不受 timeScale 影响，故暂停面板自身始终可交互。
    ///
    /// 场景接法（必须）：
    ///   1) 本脚本挂在**常驻激活**的 GameObject 上（如 Canvas 根，或专门的空物体 PauseMenuManager）；
    ///      切勿挂在会被 SetActive(false) 的 PausePanel 上——否则面板隐藏后 Update 不再执行，ESC 无法重新唤起。
    ///   2) 把暂停面板根节点（PausePanel）拖入 <see cref="pausePanel"/>。
    ///   3) 面板上三个按钮的 OnClick 分别绑定本脚本的
    ///      <see cref="OnResumeButton"/> / <see cref="OnReplayButton"/> / <see cref="OnExitButton"/>。
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Tooltip("暂停面板根节点（整体显示/隐藏）。挂本脚本的物体需常驻激活，请拖入它下面的 PausePanel 子物体")]
        [SerializeField] private GameObject pausePanel = null;

        [Tooltip("唤起/关闭暂停菜单的按键（默认 ESC）")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        private void OnEnable()
        {
            SafeSubscribe();
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            // 补订阅（防 OnEnable 早于 GameManager.Awake 漏订），并按当前状态初始化面板显隐
            SafeSubscribe();
            SyncPanel(GameManager.Instance != null
                ? GameManager.Instance.CurrentState
                : GameManager.GameState.MainMenu);
        }

        /// <summary>幂等订阅 OnStateChanged（重复调用不会多次订阅）。</summary>
        private void SafeSubscribe()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnStateChanged += HandleStateChanged;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(pauseKey)) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.CurrentState == GameManager.GameState.Playing)
                gm.PauseGame();          // → SetState 置 timeScale=0 并触发面板显示
            else if (gm.CurrentState == GameManager.GameState.Paused)
                gm.ResumeGame();         // → SetState 还原 timeScale=1 并触发面板隐藏
        }

        /// <summary>状态变化时同步面板显隐（唯一显隐入口，保证 ESC / 按钮 / 代码暂停三者一致）。</summary>
        private void HandleStateChanged(GameManager.GameState newState, GameManager.GameState oldState)
            => SyncPanel(newState);

        private void SyncPanel(GameManager.GameState state)
        {
            if (pausePanel != null)
                pausePanel.SetActive(state == GameManager.GameState.Paused);
        }

        // ===== 按钮 OnClick 绑定 =====

        /// <summary>「继续」按钮：恢复游戏（还原 timeScale、隐藏面板）。</summary>
        public void OnResumeButton() => GameManager.Instance?.ResumeGame();

        /// <summary>「重玩」按钮：重新开始当前关（<see cref="GameManager.ReplayCurrentLevel"/>）。</summary>
        public void OnReplayButton() => GameManager.Instance?.ReplayCurrentLevel();

        /// <summary>「退出」按钮：返回主菜单（<see cref="GameManager.QuitToMenu"/>）。
        /// 若需改为退出整个程序，把这里换成 <c>Application.Quit()</c> 即可。</summary>
        public void OnExitButton() => GameManager.Instance?.QuitToMenu();
    }
}
