using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// UI 管理器基础结构（单例）。
    /// 统一管理各类 UI 面板的显示/隐藏，避免散落各处的 SetActive 调用。
    ///
    /// 48小时Jam用法：
    ///   1) 新建一个空 GameObject 挂上本脚本；
    ///   2) 把各个 UI 面板作为它的子物体；
    ///   3) 在 Inspector 的 panels 列表中注册：填写名称 + 拖入对应根节点；
    ///   4) 代码或按钮调用 UIManager.Instance.ShowOnly("MainMenu") 即可。
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI 面板注册表（在 Inspector 中拖入）")]
        [Tooltip("所有 UI 面板；名称用于代码查找，root 为面板根节点 GameObject")]
        [SerializeField] private UIPanel[] panels;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // 如 UI 需随场景切换，注释掉下一行即可
            DontDestroyOnLoad(gameObject);
            HideAll();
        }

        /// <summary>只显示指定名称的面板，其余全部隐藏。</summary>
        public void ShowOnly(string panelName)
        {
            foreach (var p in panels)
            {
                if (p == null || p.root == null) continue;
                p.root.SetActive(p.panelName == panelName);
            }
        }

        /// <summary>显示单个面板（不影响其他面板）。</summary>
        public void Show(string panelName) => SetActive(panelName, true);

        /// <summary>隐藏单个面板。</summary>
        public void Hide(string panelName) => SetActive(panelName, false);

        /// <summary>隐藏所有已注册面板。</summary>
        public void HideAll()
        {
            if (panels == null) return;
            foreach (var p in panels)
            {
                if (p != null && p.root != null) p.root.SetActive(false);
            }
        }

        private void SetActive(string panelName, bool active)
        {
            if (panels == null) return;
            foreach (var p in panels)
            {
                if (p != null && p.panelName == panelName && p.root != null)
                {
                    p.root.SetActive(active);
                    return;
                }
            }
            Debug.LogWarning($"[UIManager] 找不到名为 '{panelName}' 的面板，请检查 Inspector 注册。");
        }
    }

    /// <summary>UI 面板数据结构：名称 + 根节点 GameObject。</summary>
    [System.Serializable]
    public class UIPanel
    {
        [Tooltip("面板名称，用于代码查找（如 MainMenu、HUD、GameOver）")]
        public string panelName;

        [Tooltip("面板根节点 GameObject")]
        public GameObject root;
    }
}
