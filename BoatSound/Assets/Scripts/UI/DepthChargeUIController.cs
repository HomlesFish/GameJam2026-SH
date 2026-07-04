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

        private void OnEnable()
        {
            if (DepthChargeLauncher.Instance != null)
                DepthChargeLauncher.Instance.OnChargesChanged += HandleChargesChanged;
        }

        private void OnDisable()
        {
            if (DepthChargeLauncher.Instance != null)
                DepthChargeLauncher.Instance.OnChargesChanged -= HandleChargesChanged;
        }

        private void Start()
        {
            // 解决跨对象初始化顺序：DepthChargeLauncher 在 Awake 已设好初值，这里立即刷新一次
            if (DepthChargeLauncher.Instance != null)
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
