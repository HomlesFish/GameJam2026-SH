using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 关卡 Prefab 根节点标记：暴露本关的出生/复活点与结算港口点。
    /// 挂在 <see cref="LevelData.LevelPrefab"/> 的根物体上，策划在预制体里摆好 SonarTarget 后，
    /// 把代表「出生点」「结算港口」的空物体拖进 <see cref="spawnPoint"/>/<see cref="settlePoint"/>。
    ///
    /// <see cref="GameManager.BeginLevel"/> 实例化预制体后通过本组件取锚点：
    /// 船传送到 <see cref="SpawnPosition"/>、<see cref="HarborEntryController"/> 摆到 <see cref="SettlePosition"/>。
    /// </summary>
    public class LevelRoot : MonoBehaviour
    {
        [Tooltip("本关出生 / 复活点（船开局与电量耗尽复活都传送到这里）")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("本关结算港口点（HarborEntryController 摆到这里，船入港结算）")]
        [SerializeField] private Transform settlePoint;

        /// <summary>出生/复活点世界坐标（未配置则回退本物体原点）。</summary>
        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

        /// <summary>结算港口世界坐标（未配置则回退本物体原点）。</summary>
        public Vector3 SettlePosition => settlePoint != null ? settlePoint.position : transform.position;

        /// <summary>是否显式配置了结算点（GameManager 据此决定是否摆 HarborEntryController）。</summary>
        public bool HasSettlePoint => settlePoint != null;
    }
}
