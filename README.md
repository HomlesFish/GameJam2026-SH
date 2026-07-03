# GameJam2026-SH

48 小时 Game Jam 项目。

## 引擎
- Unity **2022.3.62f2c1 (LTS)**
- 渲染管线：URP（含 2D Renderer）

## 打开工程
1. 用 Unity Hub 安装上述版本；
2. 添加 `My project/` 文件夹打开工程；
3. 首次打开会自动生成 `Library/` 等缓存目录（已在 `.gitignore` 中忽略）。

## 目录结构
```
GameJam2026-SH/
├── .gitignore              # Unity 专用忽略规则
├── README.md               # 本文件
└── My project/             # Unity 工程
    ├── Assets/             # 游戏资源与脚本（纳入版本控制）
    │   ├── Scenes/         # 场景（SampleScene）
    │   ├── Scripts/        # C# 脚本骨架（见 Scripts/README.md）
    │   ├── Prefabs/        # 预制体
    │   ├── Materials/      # 材质
    │   ├── Textures/       # 贴图
    │   ├── Audio/          # 音效/音乐
    │   └── Animations/     # 动画
    ├── Packages/           # 包清单（纳入版本控制）
    └── ProjectSettings/    # 工程设置（纳入版本控制）
```

> 脚本架构详见：[`My project/Assets/Scripts/README.md`](My project/Assets/Scripts/README.md)

## Git 协作约定
- `Library/`、`Temp/`、`obj/`、`Logs/`、`*.csproj`、`*.sln` 等本机生成物**不要提交**（已被忽略）。
- 必须提交：`Assets/`、`Packages/`、`ProjectSettings/`。
- 每个新场景/预制体改动，连同对应的 `.meta` 文件一起提交。
