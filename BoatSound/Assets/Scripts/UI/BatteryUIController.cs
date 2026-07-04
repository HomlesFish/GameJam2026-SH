using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 右上角 4 格电量显示。
    /// 订阅 <see cref="BatteryManager.OnBatteryChanged"/>，按百分比点亮格子：
    /// 0~25% → 1 格、26~50% → 2 格、51~75% → 3 格、76~100% → 4 格。
    ///
    /// 用法：电量条面板上挂本脚本；把 4 个格子按「先点亮 → 后点亮」顺序拖入 cells
    /// （即满电时 4 格全亮；电量下降时从最后一格开始熄灭）。
    /// </summary>
    public class BatteryUIController : MonoBehaviour
    {
        [Tooltip("4 个电量格子，按「先点亮 → 后点亮」顺序拖入（满电时 4 格全亮）")]
        [SerializeField] private GameObject[] cells = new GameObject[4];

        private void OnEnable()
        {
            if (BatteryManager.Instance != null)
                BatteryManager.Instance.OnBatteryChanged += HandleBatteryChanged;
        }

        private void OnDisable()
        {
            if (BatteryManager.Instance != null)
                BatteryManager.Instance.OnBatteryChanged -= HandleBatteryChanged;
        }

        private void Start()
        {
            // 解决跨对象初始化顺序：BatteryManager 在 Awake 已设好初值，这里立即刷新一次
            if (BatteryManager.Instance != null)
                HandleBatteryChanged(BatteryManager.Instance.CurrentBattery, BatteryManager.Instance.MaxBattery);
        }

        private void HandleBatteryChanged(int current, int max)
        {
            int filled = GetFilledCellCount(current, max);
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null) cells[i].SetActive(i < filled);
            }
        }

        /// <summary>按需求映射百分比到格数：≤25→1、≤50→2、≤75→3、其余→4。</summary>
        private static int GetFilledCellCount(int current, int max)
        {
            if (max <= 0) return 0;
            float pct = current / (float)max * 100f;
            if (pct <= 25f) return 1;
            if (pct <= 50f) return 2;
            if (pct <= 75f) return 3;
            return 4;
        }
    }
}
