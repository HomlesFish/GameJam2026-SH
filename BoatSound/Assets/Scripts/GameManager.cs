using System;
using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 游戏全局管理器（单例）。
    /// 负责管理整体游戏状态与生命周期，并在状态变化时通知其他系统（UI、音乐、关卡等）。
    ///
    /// 48小时Jam用法：在场景中新建一个空 GameObject 挂上本脚本即可，
    /// DontDestroyOnLoad 会自动生效，跨场景保留。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ===== 单例访问（全局唯一入口）=====
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// 游戏状态枚举。策划同学如需新增状态（如过场动画、结算），在此添加即可。
        /// </summary>
        public enum GameState
        {
            MainMenu, // 主菜单
            Playing,  // 游戏进行中
            Paused,   // 暂停
            GameOver  // 游戏结束 / 通关
        }

        /// <summary>当前游戏状态。请通过 <see cref="SetState"/> 修改，以便触发事件。</summary>
        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        /// <summary>
        /// 状态变化事件。其他系统订阅即可联动。
        /// 参数：(新状态, 旧状态)
        /// <example>
        /// GameManager.Instance.OnStateChanged += (newState, oldState) => { ... };
        /// </example>
        /// </summary>
        public event Action<GameState, GameState> OnStateChanged;

        private void Awake()
        {
            // 单例初始化：保证全局只有一个 GameManager
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>切换游戏状态并广播事件。相同状态不会重复触发。</summary>
        public void SetState(GameState newState)
        {
            if (newState == CurrentState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(newState, oldState);
        }

        // ===== 给策划/UI按钮预留的快捷方法（按钮OnClick直接绑定即可） =====
        public void StartGame()   => SetState(GameState.Playing);
        public void PauseGame()   => SetState(GameState.Paused);
        public void ResumeGame()  => SetState(GameState.Playing);
        public void EndGame()     => SetState(GameState.GameOver);
        public void QuitToMenu()  => SetState(GameState.MainMenu);
    }
}
