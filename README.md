# 《明日方舟：废土前哨协议》 (Outpost Protocol)

> 在随机毁灭的废土上，用博士的头脑和干员的性命，搏一个不确定的明天。

[![Godot 4.x](https://img.shields.io/badge/Godot-4.x-%23478CBF?logo=godot-engine&logoColor=white)](https://godotengine.org/)
[![C#](https://img.shields.io/badge/C%23-.NET%208-%23512BD4?logo=csharp&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## 📖 目录

- [游戏简介](#-游戏简介)
- [核心特色](#-核心特色)
- [游戏截图](#-游戏截图)
- [技术栈](#-技术栈)
- [项目结构](#-项目结构)
- [快速开始](#-快速开始)
- [开发路线图](#-开发路线图)
- [如何贡献](#-如何贡献)
- [致谢](#-致谢)
- [许可证](#-许可证)

---

## 🎮 游戏简介

《明日方舟：废土前哨协议》是一款基于《明日方舟》世界观的**上帝视角硬核生存建造游戏**，融合了《僵尸毁灭工程》的生存压力与 Roguelike 的重开价值。

**背景设定**：泰拉大陆某座被天灾摧毁、与罗德岛本舰失联的废弃移动城市碎片上。玩家将扮演博士——罗德岛的高层领导、矿石病治疗与天灾研究方面的顶尖学者——带领身边仅剩的数名干员，在这片被源石结晶覆盖的废土上挣扎求生。

---

## ✨ 核心特色

| 特色 | 说明 |
|:---|:---|
| 🧠 **硬核生存** | 每一份资源都弥足珍贵，博士死亡即整局清零。干员不会永久死亡，但每一次受伤都让你心疼 |
| 🎯 **干员指挥** | RTS 式指挥干员移动、释放技能，AI 自动攻击。博士本体无法战斗，必须依靠干员保护 |
| ☀️🌙 **昼夜循环** | 白天探索采集、营救干员、寻找藏品；夜晚塔防防守，抵御尸潮 |
| 🔄 **肉鸽重开** | 随机地图、随机藏品、随机敌袭，每一次都是全新的挑战 |
| 📈 **干员养成** | 干员升级、被动天赋解锁、信赖度培养，永不永久死亡 |
| 🏺 **藏品系统** | 集成战略风格的藏品收集，改变每局战术走向 |
| 🏗️ **防御塔升级** | 弩炮台、减速凝胶塔、源石爆裂塔，3 次升级解锁特殊效果 |
| 📖 **二创叙事** | 纯文字气泡对话，干员之间的日常互动，不打断游戏操作 |

---

## 📸 游戏截图

> *（待补充，开发过程中随时更新）*

| 白天探索 | 夜晚防守 | 干员指挥 |
|:---:|:---:|:---:|
| ![白天探索](screenshots/daytime.png) | ![夜晚防守](screenshots/night.png) | ![干员指挥](screenshots/combat.png) |

---

## 🛠 技术栈

| 技术 | 版本 | 说明 |
|:---|:---|:---|
| **游戏引擎** | Godot 4.x (.NET) | 开源游戏引擎，C# 支持 |
| **编程语言** | C# | .NET 8 / .NET 6 LTS |
| **数据格式** | JSON | 配置表、存档 |
| **版本控制** | Git + GitHub | 代码托管、CI/CD |
| **CI/CD** | GitHub Actions | 自动构建、导出游戏 |
| **许可证** | MIT | 宽松开源许可证 |

---

## 📁 项目结构

```
OutpostProtocol/
├── .github/                 # GitHub 配置
│   ├── workflows/           # CI/CD 流水线
│   │   └── build.yml        # 自动构建 Windows/Linux
│   ├── ISSUE_TEMPLATE/      # Issue 模板
│   │   ├── bug_report.yml
│   │   └── feature_request.yml
│   └── pull_request_template.md
│
├── Scripts/                 # C# 源代码
│   ├── Core/                # 核心系统 (Grid, ECS, EventBus, Utils)
│   ├── Managers/            # 全局管理器 (AutoLoad)
│   ├── Gameplay/            # 游戏逻辑 (Entity, Character, Building, Inventory)
│   ├── UI/                  # UI 逻辑 (Controllers, Views, ViewModels)
│   └── Data/                # 数据配置结构体
│
├── Scenes/                  # Godot 场景文件 (.tscn)
│   ├── Main.tscn
│   ├── World.tscn
│   └── UI/
│
├── Assets/                  # 游戏资源
│   ├── Art/                 # 图片、模型、Spine 动画
│   ├── Audio/               # 音效、音乐
│   └── Fonts/               # 字体文件
│
├── Data/                    # JSON 配置表
│   ├── OperatorData.json    # 干员数据
│   ├── CollectionData.json  # 藏品数据
│   ├── TowerData.json       # 防御塔数据
│   └── EnemyWaveData.json   # 敌袭波次数据
│
├── Docs/                    # 项目文档
│   ├── GameDesignDocument.md   # 游戏设计文档 (GDD)
│   └── Architecture.md         # 程序架构设计书
│
├── Tools/                   # 开发工具 (Excel→JSON 转换等)
├── .gitignore
├── LICENSE
├── README.md
└── OutpostProtocol.sln      # C# 解决方案文件
```

---

## 🚀 快速开始

### 环境要求

| 依赖 | 版本 |
|:---|:---|
| Godot | 4.x (.NET 版本) |
| .NET SDK | 8.0+ |
| Git | 任意版本 |

### 克隆与运行

```bash
# 1. 克隆仓库
git clone https://github.com/你的用户名/OutpostProtocol.git
cd OutpostProtocol

# 2. 用 Godot 打开项目
#    双击 project.godot 文件，或在 Godot 中点击"导入"选择该文件

# 3. 首次打开后，构建 C# 项目
#    菜单: Project → Tools → C# → Build 或 Create C# solution

# 4. 运行游戏 (F5)
```

### 从源码导出游戏

```bash
# 1. 在 Godot 中配置导出模板
#    菜单: Project → Export → 下载导出模板

# 2. 添加导出预设 (Windows/Linux/macOS)

# 3. 点击"导出项目"
```

---

## 🗺️ 开发路线图

| 里程碑 | 目标 | 状态 |
|:---|:---|:---|
| **v0.1.0** | 核心循环原型：博士移动、干员指挥、昼夜切换、基础塔防 | 🚧 开发中 |
| **v0.2.0** | 干员与养成：升级、天赋、信赖度、损伤恢复 | 📝 规划中 |
| **v0.3.0** | 藏品与探索：藏品系统、图鉴、求救信标、无缝地图 | 📝 规划中 |
| **v1.0.0** | 正式版：完整游戏、性能优化、多平台发布 | 📝 规划中 |

---

## 🤝 如何贡献

我们欢迎所有形式的贡献！请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详情。

### 快速贡献流程

1. Fork 本仓库
2. 从 `dev` 分支切出功能分支：`git checkout -b feature/你的功能`
3. 提交代码：`git commit -m "feat: 添加 XXX 功能"`
4. 推送到你的 Fork：`git push origin feature/你的功能`
5. 创建 Pull Request 到 `dev` 分支

### 报告 Bug

请使用 [Issue 模板](.github/ISSUE_TEMPLATE/bug_report.yml) 提交 Bug 报告。

### 提议新功能

请使用 [功能请求模板](.github/ISSUE_TEMPLATE/feature_request.yml) 提交功能建议。

---

## 📄 许可证

本项目采用 **MIT License** 开源许可证。详见 [LICENSE](LICENSE) 文件。

---

## 🙏 致谢

- **《明日方舟》** — 世界观、干员、敌人设计 © [Hypergryph](https://www.hypergryph.com/) / [Studio Montagne](https://www.montagnegames.com/)
- **《僵尸毁灭工程》** — 生存压力与硬核设计灵感
- **《杀戮尖塔》** — Roguelike 局外成长灵感
- **《这是我的战争》** — 角色与资源压力设计灵感
- **《辐射：避难所》** — 博士指挥 AI 操作模式灵感
- **PRTS Wiki** — 明日方舟设定参考

---

## 📞 联系方式

| 渠道 | 链接 |
|:---|:---|
| GitHub Issues | [提交 Issue](https://github.com/你的用户名/OutpostProtocol/issues) |
| 项目看板 | [GitHub Projects](https://github.com/你的用户名/OutpostProtocol/projects) |

---

*博士，该出发了。* 🎮

---
