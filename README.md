# NodeForge (偷天换日)

一个为《Slay the Spire 2》制作的地图节点编辑模组：改写地图节点类型、批量替换、定向事件与远古，支持联机同步、撤销与粒子特效。

steam创意工坊地址
https://steamcommunity.com/sharedfiles/filedetails/?id=3838974684

Idea based on **xiakkto's NodeSwap**, used with permission.

> 本模组由 AI 制作，可能存在 bug，欢迎反馈。
> This mod was made with AI and may contain bugs.

## 功能 / Features

- 悬浮地图节点 → 改成任意类型（战斗 / 精英 / 事件 / 商店 / 休息 / 宝箱 / 未知）。
- 批量替换：把某一类节点一次性换成另一类（单图 / 全图）。
- 定向事件：强制某节点进入指定事件。
- 定向远古：把节点变成指定先古之民（或随机远古，进入时由主机定死）。
- 联机：主机修改实时同步给所有客机；客机只读。
- 撤销 / 粒子特效。
- 界面中英双语；事件检索支持全语言（随游戏语言自动重扫）。

## 依赖 / Dependencies

- [BaseLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3737335127)
- [STS2-RitsuLib](https://steamcommunity.com/sharedfiles/filedetails/?id=3747602295)

需要游戏版本 v0.113.0。

## 构建 / Build

本项目是普通的 .NET (net9.0) 类库，通过 Harmony 打补丁。

编译需要引用游戏与依赖的 DLL（`sts2.dll`、`0Harmony.dll`、`GodotSharp.dll`、`BaseLib.dll`、`STS2-RitsuLib*.dll` 等）。把这些 DLL 放在同一个目录，然后二选一指定该目录：

- 设置环境变量 `STS2_REFS_DIR` 指向该目录；或
- 在项目根目录放一个 `refsdir_local.txt`，内容为一行目录路径（此文件已在 `.gitignore` 中忽略）。

然后：

```bash
dotnet build NodeForge.csproj -c Release
```

产物 `NodeForge.dll` 与 `mod_manifest.json` 一起放进游戏 `mods/NodeForge/` 即可。

## 目录 / Layout

- `ModEntry.cs` — 模组入口（初始化、注册设置、装补丁）。
- `Core/` — 核心逻辑（事件目录、地图节点服务、联机同步、拼音、粒子标记等）。
- `Patches/` — Harmony 补丁。
- `Settings/` — RitsuLib 设置。
- `UI/` — 两个浮层面板与主题。

## 免责 / Disclaimer

仅供个人学习与娱乐。与 Mega Crit 及原作无关。使用风险自负。
