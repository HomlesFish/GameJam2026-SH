using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 单关配置（<see cref="ScriptableObject"/>）。一关 = 一个关卡内容预制体（<see cref="levelPrefab"/>）。
    ///
    /// 策划新建关卡：
    ///   1) 在 Project 窗口 Create ▸ GameJam2026 ▸ Level Data，得到一个 .asset；
    ///   2) 把对应的关卡 Prefab 拖进 levelPrefab（prefab 根节点需挂 <see cref="LevelRoot"/> 并配好出生/结算点）；
    ///   3) 把这个 .asset 拖进 <see cref="GameManager"/> 的 levels 数组——数组顺序即关卡顺序，通关后自动进下一关。
    /// </summary>
    [CreateAssetMenu(menuName = "GameJam2026/Level Data", fileName = "LevelData")]
    public class LevelData : ScriptableObject
    {
        [Tooltip("关名（UI/调试用，如「第 1 关 · 浅滩」）")]
        [SerializeField] private string displayName = "New Level";

        [Tooltip("关卡内容预制体：根节点需挂 LevelRoot（配置 spawnPoint/settlePoint），里面摆好本关的 SonarTarget/暗礁等")]
        [SerializeField] private GameObject levelPrefab;

        /// <summary>关名（UI/调试用）。</summary>
        public string DisplayName => displayName;

        /// <summary>关卡内容预制体（根节点挂 <see cref="LevelRoot"/>）。</summary>
        public GameObject LevelPrefab => levelPrefab;
    }
}
