using UnityEngine;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// 右上角电量显示（Filled Image 版本）。
    /// 订阅 <see cref="BatteryManager.OnBatteryChanged"/>，按百分比设置 Image 的 fillAmount（0~1）。
    ///
    /// 用法：电量条面板上挂本脚本；把电量填充 Image（Image Type = Filled）拖入 fillImage。
    /// 电量随 fillAmount 连续变化，满电时填满，没电时为空。
    /// </summary>
    public class BatteryUIController : MonoBehaviour
    {
        [Tooltip("电量填充 Image，需将 Image Type 设为 Filled")]
        [SerializeField] private Image fillImage = null;

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
            if (fillImage == null) return;
            fillImage.fillAmount = max > 0 ? current / (float)max : 0f;
        }
    }
}
