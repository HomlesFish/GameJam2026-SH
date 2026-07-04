using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 俯视跟随相机：把目标（船）保持在屏幕中央。
    /// 用于雷达仪表盘式视角——相机始终紧随船移动。
    /// 挂在 Main Camera 上，把 Target 设为 Player（BoatController 所在对象）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        [Tooltip("跟随目标（通常是 Player / 船）")]
        [SerializeField] private Transform target;

        [Tooltip("勾选则相机完全锁定在目标正上方（船始终在屏幕正中，雷达视角推荐）")]
        [SerializeField] private bool lockToTarget = true;

        [Tooltip("未锁定时的跟随平滑度，越大跟得越紧")]
        [SerializeField] private float smooth = 10f;

        private void Start()
        {
            // target 未指定则从 GameManager 取（统一真相源）
            if (target == null && GameManager.Instance != null && GameManager.Instance.Boat != null)
                target = GameManager.Instance.Boat.transform;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float z = transform.position.z;
            Vector3 desired = new Vector3(target.position.x, target.position.y, z);

            if (lockToTarget)
            {
                transform.position = desired;
            }
            else
            {
                // 帧率无关的指数平滑：smooth 越大越接近瞬贴
                transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            }
        }
    }
}
