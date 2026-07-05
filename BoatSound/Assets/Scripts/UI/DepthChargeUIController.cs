using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 深水炸弹弹药显示。
    /// 订阅 <see cref="DepthChargeLauncher.OnChargesChanged"/>，按剩余弹药数点亮图标：
    /// 有几枚就亮几格（按数组顺序从前到后点亮；投放一枚则熄灭最后一格），用完全灭。
    ///
    /// 用法：弹药面板上挂本脚本；把 N 个图标按「先点亮 → 后点亮」顺序拖入 cells
    /// （即满弹时全部亮，数量建议与 <see cref="DepthChargeLauncher.MaxCharges"/> 一致，默认 3）。
    /// </summary>
    public class DepthChargeUIController : MonoBehaviour
    {
        [Tooltip("弹药图标格，按「先点亮 → 后点亮」顺序拖入（满弹时全亮，用一枚灭最后一格）")]
        [SerializeField] private GameObject[] cells = new GameObject[3];

        private void OnEnable() => SafeSubscribe();

        private void OnDisable()
        {
            if (DepthChargeLauncher.Instance != null)
                DepthChargeLauncher.Instance.OnChargesChanged -= HandleChargesChanged;
        }

        private void Start()
        {
            // Start 时所有 Awake 已跑完：补订阅一次（防 OnEnable 早于 DepthChargeLauncher.Awake 漏订）
            SafeSubscribe();
        }

        /// <summary>幂等订阅 OnChargesChanged（重复调用不会多次订阅）。
        /// 订阅只接收"之后"的变化，故每次成功订阅时顺带刷新当前弹药显示。</summary>
        private void SafeSubscribe()
        {
            if (DepthChargeLauncher.Instance == null) return;
            DepthChargeLauncher.Instance.OnChargesChanged -= HandleChargesChanged;
            DepthChargeLauncher.Instance.OnChargesChanged += HandleChargesChanged;
            HandleChargesChanged(DepthChargeLauncher.Instance.Charges, DepthChargeLauncher.Instance.MaxCharges);
        }

        private void HandleChargesChanged(int current, int max)
        {
            // current=剩余枚数；前 current 格亮，其余灭
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null) cells[i].SetActive(i < current);
            }
        }
    }
}
