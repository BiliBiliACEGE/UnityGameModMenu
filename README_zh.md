[中文](README_zh.md) | [English](README.md)
# UnityGameModMenu

> 基于 [BepInEx](https://github.com/BepInEx/BepInEx) 的 Unity 游戏修改菜单合集，涵盖 Mono 与 IL2CPP 两种后端的 Unity 游戏。

## 简介

本仓库收录了多款 Unity 游戏的修改菜单 / 训练器（Trainer），所有插件均基于 **BepInEx** + **HarmonyLib** 开发，通过 OnGUI 绘制可视化菜单，配合反射与 Harmony Patch 实现游戏内数据的实时修改。

项目按游戏脚本后端分为两大类：

| 目录 | 后端类型 | 说明 |
|------|---------|------|
| `MonoGames/` | Mono | 适用于使用 Mono 运行时的 Unity 游戏 |
| `Li2CPPGames/` | IL2CPP | 适用于使用 IL2CPP 编译的 Unity 游戏 |

> **BepInEx 版本选择**：BepInEx 版本取决于游戏的 Unity 引擎版本而非后端类型。Unity 2019~2022 使用 **BepInEx 5**，Unity 6 使用 **BepInEx 6**。详见下方[前置要求](#前置要求)章节。

## 支持的游戏

### Mono 游戏

<details>
<summary><b>Bendy and the Dark Revival</b> — 作弊菜单 (BATDR Cheat Menu)</summary>

- **插件 ID**：`com.ace20.batdr.cheatmenu`
- **菜单开关**：`F1`
- **功能标签页**：玩家 / 敌人 / 物品 / 武器·能力 / 游戏

| 分类 | 功能 |
|------|------|
| 玩家 | 无敌模式、无限生命、无限耐力、无冷却、回满生命、立即复活、超级速度（可调倍率）、超级跳跃（可调倍率）、隐身（禁用墨水恶魔侦测）、照亮全图、位置保存/传送 |
| 敌人 | 冻结所有敌人、敌人无视玩家、一击击杀、设置敌人免疫状态 |
| 物品 | 修改生命/食物/子弹/卡片/工具/零件/电池等统计数据 |
| 武器·能力 | 解锁/切换武器、设置武器威力、解锁/切换能力状态 |
| 游戏 | 禁用墨水恶魔、控制墨水恶魔计时器等 |

</details>

<details>
<summary><b>SchoolBoy Runaway</b> — 修改菜单 (Mod Menu)</summary>

- **菜单开关**：`F1`（可在配置中修改）
- **核心模块**：`ModMenuPlugin.cs`（主菜单逻辑）、`GameHelpers.cs`（游戏对象缓存与反射辅助）、`PossessionController.cs`（附身/控制功能）
- **功能**：速度倍率调整（行走/奔跑/蹲伏/攀爬/游泳）、跳跃力调整、生命值管理、难度修改、附身控制等

</details>

<details>
<summary><b>Sniper 3D</b> — 训练器 (Sniper3D Trainer)</summary>

- **插件 ID**：`com.example.sniper3d.trainer`
- **菜单开关**：`F1`
- **支持语言**：中文 / English（可在菜单内切换）
- **功能标签页**：货币 / 商店 / 武器 / 解锁 / 设置

| 分类 | 功能 |
|------|------|
| 货币 | 修改软货币（金币）、硬货币（钻石）、PVP 货币、能量值；支持无限模式 |
| 商店 | 免费购买（商店/礼包）、绕过内购校验 |
| 武器 | 一键解锁/锁定全部武器、一键等级全满/回退、逐武器解锁与升级、选择升级部件设置等级 |
| 解锁 | 解除武器限制、修改器全模式生效、修改器等级/数量最大化、伤害倍率调整 |
| 设置 | 菜单缩放、字体颜色（RGB）、语言切换 |

</details>

### IL2CPP 游戏

<details>
<summary><b>Granny</b> — 修改菜单 (Granny Mod Menu)</summary>

- **插件 ID**：`com.granny.modmenu`
- **目标环境**：BepInEx 6 IL2CPP (Unity 6 + IL2CPP + D3D12)
- **菜单开关**：`F1`（可在配置中修改）
- **核心模块**：`Plugin.cs`（插件入口，IL2CPP 组件注入）、`ModMenuBehavior.cs`（菜单 UI 与功能逻辑）、`ModState.cs`（全局模组状态）
- **功能标签页**：玩家 / 敌人 / 地图 / 游戏 / 武器 / 物品 / 车辆 / 关于

| 分类 | 功能 |
|------|------|
| 玩家 | 无敌模式、穿墙、飞行模式（可调速度）、速度倍率、跳跃增强、强制蹲下、手电筒、空中控制、玩家缩放、永不被抓 |
| 敌人 - Granny | 冻结、致盲、致聋、禁止攻击、速度倍率、禁止抓捕、Granny 缩放 |
| 敌人 - 蜘蛛 | 冻结、禁止追猎、禁止撕咬、禁止抓捕、蜘蛛缩放 |
| 敌人 - 妈妈蜘蛛 | 冻结、致盲、禁止抓捕、强制逃脱、击杀、缩放 |
| 敌人 - 妈妈爬行者 | 击杀、缩放 |
| 敌人 - 小圣诞老人 | 击晕、缩放 |
| 敌人 - 乌鸦 | 冻结、禁止攻击、禁止偷窃、缩放 |
| 敌人 - 老鼠 | 冻结、缩放 |
| 地图 | 全图变亮、关闭雾、启用所有光源 |
| 游戏 | 强制第2天/第3天、强制逃脱、噩梦模式 |
| 武器 | 枪械连发、无限射程、旧猎枪上膛 |
| 物品 | 旧猎枪一键上膛 |
| 车辆 | 汽车一键就绪 |

</details>

## 前置要求

### BepInEx

所有插件均依赖 **BepInEx** 运行环境。**不同 Unity 版本需要使用不同版本的 BepInEx**，请先确认游戏的 Unity 引擎版本，再选择对应的 BepInEx：

| Unity 引擎版本 | BepInEx 版本 | 说明 |
|---------------|-------------|------|
| Unity 2019 ~ 2022 | **BepInEx 5** | 稳定版，兼容大部分旧版 Unity 游戏 |
| Unity 6 | **BepInEx 6** | Bleeding Edge 预览版，支持 Unity 6 新运行时 |

> **如何确认游戏的 Unity 版本**：
> - **方法一**：将鼠标悬停在游戏的 `.exe` 文件上，弹出的提示中「文件版本」即为 Unity 版本号。
> - **方法二**：右键 `.exe` → 属性 → 详细信息 → 查看「产品版本」。

> **BepInEx 下载链接**（请根据游戏使用的 Unity 版本和脚本后端选择）
>
> - **BepInEx 5**（Unity 2019 ~ 2022）：
>
>   <!-- TODO: 请在此处填写 BepInEx 5 下载链接 -->
>   https://github.com/BepInEx/BepInEx/releases?page=2#release-v5.4.15
>
> - **BepInEx 6 - Mono**（Unity 6，Mono 后端）：
>
>   <!-- TODO: 请在此处填写 BepInEx 6 (Mono / Bleeding Edge) 下载链接 -->
>   https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.Mono-win-x64-6.0.0-be.785%2B6abdba4.zip
>
> - **BepInEx 6 - IL2CPP**（Unity 6，IL2CPP 后端）：
>
>   <!-- TODO: 请在此处填写 BepInEx 6 (IL2CPP / Bleeding Edge) 下载链接 -->
>   https://builds.bepinex.dev/projects/bepinex_be/785/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.785%2B6abdba4.zip

### 开发环境（从源码编译）

- [.NET SDK](https://dotnet.microsoft.com/)（建议 8.0+）
- IDE：Visual Studio / Rider / VS Code
- **Mono 游戏**：对应游戏的 `Assembly-CSharp.dll` 及 `UnityEngine.dll` 等托管 DLL（位于 `游戏名_Data/Managed/`）
- **IL2CPP 游戏**：需要先 Dump 出游戏 DLL（见下方说明）

## 安装

1. 下载并安装 BepInEx 到游戏目录（参考上方的下载链接）。
2. 从本仓库的 [Releases](../../releases) 页面下载对应游戏的插件 DLL。
3. 将 DLL 文件放入游戏的 `BepInEx/plugins/` 目录。
4. 启动游戏，按 `F1` 打开修改菜单。

### 目录结构示例

```
游戏根目录/
├── BepInEx/
│   ├── core/            # BepInEx 核心文件
│   ├── plugins/         # ← 将插件 DLL 放在这里
│   └── config/          # 插件配置文件（自动生成）
├── 游戏名_Data/
└── 游戏名.exe
```

## 使用说明

| 游戏 | 菜单快捷键 | 备注 |
|------|-----------|------|
| Bendy and the Dark Revival | `F1` | 可在 BepInEx 配置中修改快捷键 |
| SchoolBoy Runaway | `F1` | 可在 BepInEx 配置中修改快捷键 |
| Sniper 3D | `F1` | 支持菜单内切换中英文 |
| Granny | `F1` | 可在 BepInEx 配置中修改快捷键 |

打开菜单后，鼠标会自动解锁，可点击勾选/取消各项功能。关闭菜单后鼠标恢复锁定。

## 从源码编译

> **重要**：各项目的 `.csproj` 通过相对路径（`HintPath`）引用游戏的托管 DLL 和 BepInEx 核心库，因此**源码目录必须放在游戏根目录下**才能正确解析引用路径。

### Mono 游戏

1. 克隆仓库：

```bash
git clone https://github.com/BiliBiliACEGE/UnityGameModMenu.git
```

2. 将对应游戏的源码文件夹复制到**游戏根目录**下（与 `游戏名.exe` 和 `游戏名_Data/` 同级）：

```
游戏根目录/
├── BepInEx/                  # 已安装的 BepInEx
│   └── core/
├── 游戏名_Data/
│   └── Managed/              # 游戏托管 DLL（Assembly-CSharp.dll 等）
├── 游戏名.exe
└── 源码文件夹/               # ← 从仓库复制到这里
    ├── *.csproj
    └── *.cs
```

3. 在源码目录中执行编译：

```bash
# 以 Sniper3D 为例
cd "游戏根目录/Sniper3D"
dotnet build -c Release
```

4. 编译后的 DLL 位于 `bin/Release/` 目录，将其复制到 `BepInEx/plugins/` 即可。

> **提示**：不同项目的 `.csproj` 引用路径略有差异（部分使用 `$(MSBuildProjectDirectory)\...`，部分使用 `..\...`），但统一要求是源码文件夹与游戏的 `游戏名_Data/` 和 `BepInEx/` 处于同一层级（即游戏根目录下）。如果引用路径找不到，请检查 `.csproj` 中的 `HintPath` 并确保游戏 DLL 和 BepInEx 核心库位于对应位置。

### IL2CPP 游戏

由于 IL2CPP 游戏的 C# 代码已被编译为原生 C++ 代码，**游戏目录中不包含可直接引用的托管 DLL**。因此，从源码编译 IL2CPP 游戏的插件前，必须先使用工具 Dump 出游戏的 DLL 文件。

#### 步骤 0：Dump 游戏 DLL

使用 [Il2CppDumper GUI](https://github.com/AndnixSH/Il2CppDumper-GUI) 从 IL2CPP 游戏中 Dump 出 `Assembly-CSharp.dll` 等 DLL 和 `global-metadata.dat` 信息。

> **工具下载**：[Il2CppDumper GUI Releases](https://github.com/AndnixSH/Il2CppDumper-GUI/releases)
>
> **使用方法**：
> 1. 下载并运行 Il2CppDumper GUI。
> 2. 将游戏的二进制文件（如 `GameAssembly.dll`）和 `游戏名_Data/il2cpp_data/Metadata/global-metadata.dat` 拖入程序。
> 3. 点击 Start 开始 Dump。
> 4. Dump 完成后，输出的 DLL 文件位于 `Dump/` 目录中。

> **注意**：Dump 出的 DLL 为 **DummyDll**（仅包含类型和方法签名，不包含实际实现），用于编译时引用，不可用于运行。运行时由 BepInEx 6 的 Il2CppInterop 负责生成完整的 interop 程序集。

#### 步骤 1：安装 BepInEx 6 IL2CPP 并生成 Interop DLL

1. 将 BepInEx 6 (IL2CPP) 安装到游戏目录并运行一次游戏，BepInEx 会在 `BepInEx/interop/` 目录下自动生成完整的 interop 程序集。

```
游戏根目录/
├── BepInEx/
│   ├── core/                # BepInEx 核心库（BepInEx.Core.dll, BepInEx.Unity.IL2CPP.dll 等）
│   ├── interop/             # ← 运行游戏后自动生成的 interop 程序集
│   │   ├── Assembly-CSharp.dll
│   │   ├── UnityEngine.dll
│   │   ├── UnityEngine.CoreModule.dll
│   │   └── ...
│   └── plugins/
├── GameAssembly.dll
├── 游戏名_Data/
└── 游戏名.exe
```

#### 步骤 2：放置源码并编译

1. 将 IL2CPP 游戏的源码文件夹复制到游戏根目录下（与 `BepInEx/` 同级）。
2. 执行编译：

```bash
# 以 Granny 为例
cd "游戏根目录/Granny"
dotnet build -c Release
```

3. IL2CPP 项目的 `.csproj` 已配置编译后自动复制到 `BepInEx/plugins/`，无需手动操作。

> **提示**：IL2CPP 项目的 `.csproj` 引用路径为 `..\BepInEx\core\` 和 `..\BepInEx\interop\`，因此源码文件夹必须位于游戏根目录下、与 `BepInEx/` 同级。如果 `interop/` 目录不存在，请先运行一次游戏让 BepInEx 生成 interop 程序集。

## 项目结构

```
UnityGameModMenu/
├── MonoGames/                          # Mono 后端游戏
│   ├── Bendy and the Dark Revival/
│   │   ├── BATDRCheatMenu.csproj
│   │   └── BATDRCheatMenuPlugin.cs
│   ├── SchoolBoyRunAway/
│   │   ├── ModMenu.csproj
│   │   ├── ModMenuPlugin.cs
│   │   ├── GameHelpers.cs
│   │   └── PossessionController.cs
│   └── Sniper3D/
│       ├── Sniper3D_Trainer.csproj
│       └── Sniper3D_Trainer.cs
├── Li2CPPGames/                        # IL2CPP 后端游戏
│   └── Granny/
│       ├── GrannyModMenu.csproj
│       ├── Plugin.cs                   # 插件入口（IL2CPP 组件注入）
│       ├── ModMenuBehavior.cs          # 菜单 UI 与功能逻辑
│       └── ModState.cs                 # 全局模组状态
├── LICENSE
└── README.md
```

## 免责声明

- 本项目仅供**学习与研究**用途，旨在探索 Unity 游戏的运行机制与 BepInEx 插件开发。
- 请**勿**在多人/在线模式中使用，避免影响其他玩家的游戏体验。
- 使用本插件导致的任何账号封禁、数据损坏等后果，由使用者自行承担。
- 本项目不隶属于任何游戏开发商，所有游戏名称及商标归其各自所有者所有。

## 许可证

本项目基于 [MIT License](./LICENSE) 开源，可自由使用、修改和分发。
