using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam2026
{
    /// <summary>
    /// 字幕堆叠 UI：新字幕从底部出现，未消失的旧字幕被挤到上面；消失时淡出并移除。
    ///
    /// 订阅 <see cref="SubtitleManager.OnShown"/>/<see cref="SubtitleManager.OnHidden"/>：
    ///   - OnShown：实例化一行作为容器最后一个子物体（→ 排在最底），填文本、淡入；
    ///   - OnHidden：按 InstanceId 找到对应行，淡出后销毁。
    ///
    /// 「挤到上面」靠布局实现：建议 stackContainer 挂 Vertical Layout Group，
    /// Child Alignment = Lower Left/Lower Center（整块贴底），新行 append 在最末 → 最底，
    /// 旧行（更靠前的兄弟节点）自然位于其上方；旧行到点被销毁时整块自动上移。
    ///
    /// 用法：字幕面板（屏幕底部）上挂本脚本；
    ///   - stackContainer：堆叠容器（建议 Vertical Layout Group，Child Force Expand 宽度可勾以撑满）；
    ///   - rowPrefab：单行预制体，根节点带一个 <see cref="Text"/>（根节点有/无 CanvasGroup 均可，缺省会自动补一个用于淡入淡出）。
    /// </summary>
    public class SubtitleUIController : MonoBehaviour
    {
        [Tooltip("字幕堆叠容器：建议挂 Vertical Layout Group，Child Alignment = Lower Left/Center（贴底向上堆叠）")]
        [SerializeField] private Transform stackContainer;

        [Tooltip("单行预制体：根节点需带 UnityEngine.UI.Text（根节点带 CanvasGroup 可直接淡入淡出，缺省自动补）")]
        [SerializeField] private GameObject rowPrefab;

        [Tooltip("淡入时长（秒）")]
        [SerializeField] private float fadeInDuration = 0.2f;

        [Tooltip("淡出时长（秒）")]
        [SerializeField] private float fadeOutDuration = 0.4f;

        private readonly Dictionary<int, GameObject> _rows = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Coroutine> _fades = new Dictionary<int, Coroutine>();

        private void OnEnable()
        {
            SafeSubscribe();
        }

        private void OnDisable()
        {
            if (SubtitleManager.Instance != null)
            {
                SubtitleManager.Instance.OnShown -= HandleShown;
                SubtitleManager.Instance.OnHidden -= HandleHidden;
            }
        }

        private void Start()
        {
            // Start 时所有 Awake 已跑完：补订阅一次，防 OnEnable 早于 SubtitleManager.Awake 时漏订
            SafeSubscribe();
        }

        /// <summary>幂等订阅 OnShown/OnHidden（重复调用不会多次订阅）。</summary>
        private void SafeSubscribe()
        {
            if (SubtitleManager.Instance == null) return;
            SubtitleManager.Instance.OnShown -= HandleShown;
            SubtitleManager.Instance.OnShown += HandleShown;
            SubtitleManager.Instance.OnHidden -= HandleHidden;
            SubtitleManager.Instance.OnHidden += HandleHidden;
        }

        private void HandleShown(ActiveSubtitle sub)
        {
            if (sub == null) return;
            if (stackContainer == null || rowPrefab == null)
            {
                Debug.LogWarning("[SubtitleUIController] stackContainer / rowPrefab 未配置，无法显示字幕。", this);
                return;
            }

            // 作为最后一个子物体 → 排在最底；旧行被挤到上面
            GameObject row = Instantiate(rowPrefab, stackContainer);

            Text text = row.GetComponentInChildren<Text>();
            if (text != null) text.text = sub.Text;

            CanvasGroup cg = GetOrAddCanvasGroup(row);
            _rows[sub.InstanceId] = row;
            StartFade(sub.InstanceId, cg, 0f, 1f, fadeInDuration, null);
        }

        private void HandleHidden(ActiveSubtitle sub)
        {
            if (sub == null) return;
            if (!_rows.TryGetValue(sub.InstanceId, out GameObject row) || row == null)
            {
                _rows.Remove(sub.InstanceId);
                return;
            }

            CanvasGroup cg = GetOrAddCanvasGroup(row);
            // 淡出完成后销毁该行并清账
            StartFade(sub.InstanceId, cg, cg.alpha, 0f, fadeOutDuration, () =>
            {
                _rows.Remove(sub.InstanceId);
                if (row != null) Destroy(row);
            });
        }

        /// <summary>启动一次淡入/淡出；同一行有进行中的淡变先停掉，避免互相覆盖。</summary>
        private void StartFade(int instanceId, CanvasGroup cg, float from, float to, float duration, System.Action onComplete)
        {
            if (_fades.TryGetValue(instanceId, out Coroutine existing) && existing != null)
            {
                StopCoroutine(existing);
            }
            Coroutine c = StartCoroutine(FadeRoutine(instanceId, cg, from, to, duration, onComplete));
            _fades[instanceId] = c;
        }

        private IEnumerator FadeRoutine(int instanceId, CanvasGroup cg, float from, float to, float duration, System.Action onComplete)
        {
            if (cg == null)
            {
                _fades.Remove(instanceId);
                onComplete?.Invoke();
                yield break;
            }
            if (duration <= 0f)
            {
                cg.alpha = to;
                _fades.Remove(instanceId);
                onComplete?.Invoke();
                yield break;
            }

            cg.alpha = from;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            cg.alpha = to;
            _fades.Remove(instanceId);   // 收尾清账，避免字典随字幕累积无限增长
            onComplete?.Invoke();
        }

        /// <summary>取行根节点的 CanvasGroup；没有则补一个（设为不拦截射线，避免挡住下层点击）。</summary>
        private static CanvasGroup GetOrAddCanvasGroup(GameObject row)
        {
            if (row == null) return null;
            CanvasGroup cg = row.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = row.AddComponent<CanvasGroup>();
            }
            cg.interactable = false;
            cg.blocksRaycasts = false;
            return cg;
        }
    }
}
