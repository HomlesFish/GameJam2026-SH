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

        private void OnEnable() => SafeSubscribe();

        private void OnDisable()
        {
            if (BatteryManager.Instance != null)
                BatteryManager.Instance.OnBatteryChanged -= HandleBatteryChanged;
        }

        private void Start()
        {
            // Start 时所有 Awake 已跑完：补订阅一次（防 OnEnable 早于 BatteryManager.Awake 漏订）
            SafeSubscribe();
        }

        /// <summary>幂等订阅 OnBatteryChanged（重复调用不会多次订阅）。
        /// 订阅只接收”之后”的变化，故每次成功订阅时顺带刷新当前值，保证显示与电量一致。</summary>
        private void SafeSubscribe()
        {
            if (BatteryManager.Instance == null) return;
            BatteryManager.Instance.OnBatteryChanged -= HandleBatteryChanged;
            BatteryManager.Instance.OnBatteryChanged += HandleBatteryChanged;
            HandleBatteryChanged(BatteryManager.Instance.CurrentBattery, BatteryManager.Instance.MaxBattery);
        }

        private void HandleBatteryChanged(int current, int max)
        {
            if (fillImage == null) return;
            fillImage.fillAmount = max > 0 ? current / (float)max : 0f;
        }
    }
}
