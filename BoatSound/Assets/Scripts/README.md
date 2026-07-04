# Scripts 架构说明（给程序 & 策划）

> 本目录是 48 小时 Game Jam 的 C# 脚本骨架。原则：**简洁优先、快速迭代、预留扩展点**。
> 统一命名空间：`GameJam2026`（如需修改，全局替换即可）。

## 目录结构

```
Scripts/
├── GameManager.cs          # 游戏全局管理器（单例 + 状态机 + 事件）
├── Player/
│   └── PlayerController.cs # 玩家控制器（输入 + 移动 + 预留交互/受击）
├── UI/
│   └── UIManager.cs        # UI 管理器（单例，统一显示/隐藏各类面板）
└── Events/
    ├── IGameEventListener.cs # 事件监听器接口（事件系统基础接口）
    └── EventBus.cs           # 事件总线（静态，发布/订阅解耦）
```

## 各模块速览

### 1. GameManager（全局管理器）
- 场景里建一个空 GameObject 挂上即可，自动 `DontDestroyOnLoad`。
- 用 `GameState` 枚举表示状态：`MainMenu / Playing / Paused / GameOver`。
- 订阅 `OnStateChanged` 联动其他系统：
  ```csharp
  GameManager.Instance.OnStateChanged += (next, prev) => { /* 例如切音乐、刷新UI */ };
  ```
- 策划对接：按钮 `OnClick` 直接绑定 `StartGame() / PauseGame() / EndGame()`。

### 2. PlayerController（玩家控制器）
- 挂到玩家对象上，WASD/方向键即可移动（`moveSpeed` 在 Inspector 调）。
- 想改输入方式或移动实现：重写 `GetMoveInput()` / `Move()`。
- 预留点：`Interact()`（交互）、`TakeDamage()`（受击）——按需求填充。

### 3. UIManager（UI 管理器）
- 挂本脚本，把面板做成子物体，在 `panels` 数组里注册「名称 + 根节点」。
- 调用：
  ```csharp
  UIManager.Instance.ShowOnly("GameOver"); // 只显示某个面板
  UIManager.Instance.Show("HUD");          // 显示
  UIManager.Instance.Hide("HUD");          // 隐藏
  ```

### 4. Events（事件系统）
两种用法按场景二选一，不要混用同一事件：

**(a) EventBus + IGameEventListener（跨系统、解耦）**
```csharp
// 发布
EventBus.Raise("PlayerDied");

// 订阅（实现 IGameEventListener 的组件）
private void OnEnable()  => EventBus.Subscribe("PlayerDied", this);
private void OnDisable() => EventBus.Unsubscribe("PlayerDied", this);
public void OnEventRaised(string id) { if (id == "PlayerDied") DoSomething(); }
```

**(b) C# event（同模块内、强类型）**
GameManager 的 `OnStateChanged` 即此风格，适合 1 对 N 但类型明确的场景。

> ⚠️ 用 EventBus 务必在 `OnDisable/OnDestroy` 取消订阅，否则对象销毁后触发事件会空引用。

## 约定
- 事件 ID 用 **PascalCase** 字符串常量，建议集中放在一个 `GameEvents.cs` 里管理（按需新增）。
- 新建脚本继承/组合现有基类，优先复用，避免重复造轮子。
- 单例（`Instance`）只在确需全局访问时使用，别滥用。
