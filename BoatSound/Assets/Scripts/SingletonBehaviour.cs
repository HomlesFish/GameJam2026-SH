using UnityEngine;

namespace GameJam2026
{
    /// <summary>
    /// 单例 <see cref="MonoBehaviour"/> 基类（CRTP）。子类写 <c>: SingletonBehaviour&lt;子类&gt;</c>
    /// 即自动获得全局唯一 <see cref="Instance"/>，无需再各自抄写 <c>Awake</c> 里的注册样板。
    ///
    /// 用法：
    /// <code>
    /// public class MyManager : SingletonBehaviour&lt;MyManager&gt;
    /// {
    ///     // 需要跨场景保留（DontDestroyOnLoad）时：
    ///     protected override bool PersistOnLoad => true;
    ///
    ///     // 需要 Awake 自定义初始化时（必须调用 base.Awake() 完成注册）：
    ///     protected override void Awake()
    ///     {
    ///         base.Awake();
    ///         // ...自己的初始化
    ///     }
    /// }
    /// </code>
    /// 访问方式与原来一致：<c>MyManager.Instance</c>。
    /// </summary>
    /// <typeparam name="T">单例子类自身类型。</typeparam>
    public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        /// <summary>全局唯一实例。场景里出现重复实例时只保留首个，其余自我销毁。</summary>
        public static T Instance { get; private set; }

        /// <summary>是否跨场景保留（<see cref="Object.DontDestroyOnLoad"/>）。需常驻的管理器 override 成 true。</summary>
        protected virtual bool PersistOnLoad => false;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = (T)this;
            if (PersistOnLoad) DontDestroyOnLoad(gameObject);
        }
    }
}
