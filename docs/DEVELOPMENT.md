# NodeForge 节点锻造 — 交付说明

一个把原 nodeswap 模组重做并扩展的新模组，接入 BaseLib + RitsuLib。

## 相对原 mod 的变化
1. **修复远古事件对话卡死 bug**：原 mod 在 `GenerateInitialOptionsWrapper` 的 Prefix 里调用已删除的 `Rng.get_Seed()` 与 `Rng(uint,int)` 构造，导致新版抛 `MissingMethodException`、事件初始化中断、界面卡死。新实现改用仍存在的 `RunState.Rng.Seed`(ulong) + `Rng(ulong)`，并且不再改写事件内部的 `Rng`；改为在 `ActModel.PullAncient` 里按楼层/坐标选择不同远古，无副作用。
2. **接入 RitsuLib 设置页**：`RitsuLib 设置 → NodeForge` 下可配置两个浮层面板的唤出按键（默认 F6 / F7）。可在主菜单与跑局暂停菜单里访问。
3. **子菜单 1（F6）**：沿用原 nodeswap 的“把勾选的来源节点批量替换成目标节点”，重绘为深青+琥珀主题。
4. **子菜单 2（F7）**：在地图上把鼠标悬停的节点改成选定类型：
   - Boss 节点、当前所在节点 **不可修改**；
   - 可选类型：问号/普通战斗/精英/火堆/商店/宝箱/远古/事件（不含 Boss）；
   - 选“事件”时可选具体事件（也可留空=随机）；
   - 悬停事件条目 → 右侧预览该事件全貌（标题/描述/选项文字），数据来自离线扫描 `ModelDb.AllEvents`；
   - 事件全貌缓存写 `nodeforge_events.cache.json`，启动时增量对比（新增补入、消失屏蔽）。

## 事件筛选与颜色标注（子菜单2 事件列表）

- **按地图分组**：默认视图只列“当前地图（当前 Act）可出现的事件”。事件实际归属通过 `ActModel.AllEvents`（各 Act 注册的事件）+ `ModelDb.AllSharedEvents`（全地图共享）反查得到；跨多个地图的事件会在各对应地图中都出现。
- **筛选可选**：面板内“显示全部事件”按钮可切换为**全量视图**（不筛选、全部事件），切换回来即当前地图视图。玩家仍可跨地图选择并强制事件——筛选只是默认视图。
- **三色标注**（仅视觉，不影响可选）：
  - **绿** = 正常可触发（`EventModel.IsAllowed(currentRunState)==true`）
  - **黄** = 当前条件不满足（`IsAllowed==false`，如物品/进度/角色条件）
  - **红** = 模组添加的事件（无法可靠内省 mod 事件的角色条件，按需求兜底：所有 mod 事件标红，悬停 tooltip 显示来源模组名）
  - 红优先于黄；判定不出的按绿处理。
- **排序**：绿段在上，黄、红段置底；各段内部按标题/ID 排序。（排序规则可配置为后续增强）
- **颜色可配置**：RitsuLib 设置 → NodeForge → “事件列表颜色”，三个 `AddColor` 条目分别设 绿/黄/红（十六进制，默认 `6FD3A8`/`E8C24B`/`E8795E`）。

## 第二批增强

- **四色分类**（可配置，均在 RitsuLib 设置：“事件列表颜色”）：绿=`vanilla 且 IsAllowed`；黄=`vanilla 且 !IsAllowed`；橙=`mod 事件且 IsAllowed`；红=`mod 事件且 !IsAllowed`（角色/条件受限）。排序严格按 绿→黄→橙→红 分段，各段内按标题/ID 排序。
- **远古强制事件**：新增 `ActModel.PullAncient` Postfix，节点被强制为某个 Ancient 事件 id 时进入该远古（与按层变化并存，显式强制优先）。
- **红圈标记**：被 Menu 2 改过的节点叠加一枚独立红色圆环（比原版黑圈大一号，复用 `map_circle_4` 纹理染色）。地图重建时自动重绘。
- **呼出前置条件**：仅当鼠标悬停在**非 Boss、非起点先古之民、非当前所在**节点时才能唤出 Menu 2；否则弹出红色提示（“请将鼠标悬浮在节点上 / 暂时不支持修改起点和终点”）。
- **撤销**：Menu 2 内“撤销上一次”，恢复节点的原始类型与强制事件并移除红圈。
- **单图批量**：Menu 2 内可选“把本图所有 <类型> 改为上方目标”，只影响当前地图。
- **组合键**：唤出键支持 `Ctrl/Shift/Alt + Key`（如 `Ctrl+F6`），默认 F6/F7。
- **持久化**：单点/单图批量直接写入 `MapPoint.PointType` 并触发 `SaveManager.SaveRun`，SL 后节点保持（PointType 随存档保存）；子菜单1 的全局批量仍在 `ModifyGeneratedMap` 生成时应用，且**不覆盖**本会话内 Menu 2 编辑过的节点。
- **画面**：事件条目用“左侧色条 + 文字同色”保留颜色编码（选中也不丢色）、新增搜索框、预览区加背景块与选项列表、面板按视口自适应、两个浮层入口按钮缩小并整齐堆叠。

## 第三批修复

- **批量替换可回滚/可重复**：Menu 1 新增“应用到当前地图”（立即改当前图，不再只等新地图）与“撤销本次批量”（还原每个节点原类型）；“仅保存配置”保留给新地图。Menu 2 的“单图批量”也可整批撤销。
- **面板位置修复**：两个浮层面板改用 `CenterContainer` 居中，彻底避免锚点+偏移导致跑到屏幕外看不见。
- **配置界面中文**：`UiText.T` 现在对 `zhs/zht/zh` 及“语言未初始化”一律走中文（中文优先），修正中文环境仍显示英文的问题。
- **补全橙色**：RitsuLib 设置“事件列表颜色”补齐第四色（橙），四色均可配。
- **红色提示 5 秒自动消失**：Menu 2 的红色提示条显示 5 秒后自动隐藏（每次新提示重置计时）。

## 第四批修复

- **右上角悬浮按钮全部移除**：两个菜单仅通过热键/设置页唤出，不再在右上角放按钮。
- **两菜单互斥**：打开任一菜单会自动关闭另一个。
- **悬浮锁定**：打开 Menu 2 时锁定当前悬浮节点，面板打开期间不再随鼠标移动变化。
- **节点类型调整**：Menu 2 的“事件”改名为“定向事件”；新增“定向远古”。“定向事件”只列普通事件、“定向远古”只列远古（远古节点走 `PullAncient` 强制）。
- **Menu 1 文案与撤销**：副标题改为“点应用到当前地图立即生效”；撤销按钮改名“撤销一次批量”；“保存配置”提示改为可配合“应用到当前地图”。
- **事件列表空修复**：当前地图视图为空时（如 Act 归属信息缺失）自动回退显示该类别全部事件并提示，不再显示“没有可显示的事件”。

## 第五批修复

- **默认不替换 / 会话内可改 / 不持久化**：Menu 1 的来源节点开关默认全部关闭（不做任何替换），且这些开关**不写入配置**（重启恢复全不替换）；进入游戏后勾选只在本会话生效。
- **开局替换可撤销**：地图生成时自动执行的替换会记入共享撤销栈，Menu 1 的“撤销一次批量”可一并撤销。
- **红圈镜像 + 对齐 + 颜色可配**：被改节点的圆环改为与节点中心对齐、水平镜像原版黑圈（`Scale.X=-1`），并新增 RitsuLib 设置“标记圆环颜色”。
- **已走过节点不可改**：Menu 2 打开/应用时排除 `Traveled` 状态节点。
- **事件扫描修复（关键）**：改为**懒扫描**——菜单打开时若事件列表为空会立即重扫（此前只在初始化时扫，模型库未就绪导致 0 事件）。刷新产出 0 事件时不再清空旧缓存。
- **事件可按地图选择**：事件区新增下拉框，可选“全部地图”或任意 Act，而非只有当前/全部。
- **定向远古/定向事件同步修复**：两者共用同一扫描，扫描修复后一起可用。

## 第六批修复

- **特殊颜色事件兼容**：预览区若该事件自定义了 `ButtonColor`，会提示“此事件有特殊按钮颜色”；列表仍按四色分类不冲突。
- **选中事件高亮框**：选中事件行显示整圈彩色边框 + 同色半透明底色，未选中为左侧色条。
- **红圈绘制更可靠**：改为按节点实际尺寸 1.25 倍居中叠加、镜像；颜色配置生效；不再用负 ZIndex（导致不可见）。
- **撤销后进入仍被替换修复**：移除了 `EnterMapPointInternal` 与 `NMapScreen.SetMap` 上“再次按规则替换”的逻辑——这两处会在你撤销或重开地图后把点类型又改回替换值（即“撤销了走过去还是应用了”的根因）。现在只在**地图生成时**应用一次。
- **条件不满足的事件不再误强制**：应用/批量前校验选中事件的 `IsAllowed(当前局)`；不满足则**拒绝并红色提示**“条件不满足，进入会是随机问号内容，无法强制”，避免假生效。

## 第七批修复

- **兼容事件文本内的彩色富文本**：预览的事件描述与选项文本不再做 BBCode 转义，直接按游戏原有 `[color]`/`[b]` 等标签渲染（此前被转义成字面量，彩色文本不显示）。
- **排序改为 绿 → 橙 → 黄 → 红**（原为绿黄橙红）。

## 适配游戏 v0.113.0（2026-11-13）

- 游戏本体更新到 **v0.113.0**；重新反编译 `sts2.dll` 核对 NodeForge 用到的全部 API（ActModel/EventModel/ModelDb/MapPoint/ActMap/NMapPoint/NMapScreen/Hook/RunManager/SaveManager/NGame/NMainMenu/Rng/RunRngSet 等），**签名均未变化**，源码无需改动，重编译即通过（0 警告 0 错误）。
- 依赖升级：RitsuLib 由 0.6.10 → **0.6.11**（compat 目标 0.113.0，min_game_version 0.113.0）。BaseLib 按你要求**保持 3.4.8**（min 0.107.1，在 0.113 下仍可用）。
- 编译引用换成 `sts2base/deps/refs0113`（新 sts2/GodotSharp/Harmony + RitsuLib 0.6.11 nupkg lib + BaseLib 3.4.8）。
- 部署目录 `testgame0.111.0/mods/`（目录名沿用旧称，内容已更新为 0.113 版本）。

## 修复：批量替换后地图卡死（SL 卡在远古）

- **根因**：批量替换把**起点远古节点**也改成了其它类型；读档时游戏期望起点处是远古事件，找不到 → 卡死。开局自动替换（`ConvertMap`）同样会把起点改掉。
- **修复**：所有替换路径（开局自动、Menu1 应用到当前地图、Menu2 单图批量）统一排除受保护节点：**Boss、第二 Boss、起点远古节点、当前所在节点、已走过节点**，以及 Menu2 已手工编辑、已撤销的节点。
- **一致化**：批量替换现在与单点编辑规则一致——**不能改走过的节点和当前节点**。

## 第八批修复

- **撤销也跳过受保护节点**：单点撤销与批量撤销都不会修改 Boss / 起点远古 / 当前节点 / 已走过节点。
- **红圈改用原版圆环**：不再用可能被镜像成“倒着的 nope”的图集纹理，改为复用原版 `NMapCircleVfx`（真实圆环）染色 + 放大 1.6 倍，居中叠加。
- **事件扫描/缓存更新**：缓存加**版本戳 = schema + mod 版本 + 游戏版本**，任一变则强制重扫；修复新版本新事件不出现、旧缓存文本过时的问题。定向远古列表随之恢复。

## 第九批：Mod 支持与联机权限

- **事件增量扫描**：启动时按“版本戳相同则跳过已有事件、只 Capture 新增 id”的方式增量扫描；已有事件只刷新轻量元数据（所属地图/共享/来源 mod），不重复抓文本。版本戳 = `schema | mod版本 | 游戏版本`，任一更新才全量重扫。
- **联机权限**：只有**主机（或单人）**可修改地图节点；联机**客机**打开菜单会被拒绝并红色提示“只有主机可以修改地图节点”。判定用 `RunManager.NetService.Type`（Singleplayer/Host 允许，Client/Replay 拒绝）；打开菜单与应用/批量三处都有校验。

## 第十批修复

- **特殊富文本标签渲染**：游戏自定义标签（jitter/sine/purple/red…）会被转换为标准 BBCode（颜色类映射到 `StsColors` 对应色，特效类剥离保留文字），预览不再显示 `[jitter]` 之类的字面量。
- **远古事件扫描修复（根因）**：远古事件在 `ModelDb.AllAncients` 中、不在 `AllEvents`；此前只扫 `AllEvents` 导致远古永远为空。现同时扫描 `AllAncients`，与普通事件共用同一**增量**扫描与 mod 来源识别、按地图归属（`act.AllAncients`）。
- **每次打开重检条件**：打开/切换列表时用当前局实时重算 `IsAllowed`；并把**已触发过（`RunState.VisitedEventIds`）**判为不可再次触发。
- **已触发过 = 涂黑 + 置底**：新增 `Visited` 颜色类别（默认 `555555`），已触发事件排到最底层；颜色可在 RitsuLib 设置“已触发过（置底）”配置。
- **红圈不再与黑混色**：改用 `Line2D` 自绘圆环（纯色精确、与节点居中、略大），避免黑色纹理 × 染色变黑的问题；颜色仍可配。
- **拼音搜索**：搜索框支持拼音首字母（GB2312 表，分享→fx）；设置项“拼音搜索自动跟随语言（中文自动开）”/“关闭自动时使用”可配置。
- **非本图事件提示**：选中事件不属于当前 Act 时，预览提示“强制后可能变成随机问号内容”。

## 第十一批修复

- **红圈重做**：改用节点内 `Line2D` 自绘圆环（线宽按半径比例、`ShowBehindParent` 绘于节点之下、居中、颜色精确不混黑），随节点移动/销毁，不再浮于最上层。颜色仍可配。
- **先古事件提示改为“全部遗物”**：先古不走 `GameInfoOptions`（其本地化是 `ancients` 表、格式不同，之前抛 `LocException`）；改为读取 `AncientEventModel.AllPossibleOptions` 的遗物，展示“该远古的全部遗物（名称+描述）”。
- **先古应用防卡死**：强制远古前，若无法解析该远古（遗物集为空）则拒绝并提示，避免卡死；普通事件仍校验 `IsAllowed`/已触发。
- **拼音搜索修复**：用 pypinyin 生成**全量汉字→完整拼音**表（`PinyinData.cs`），支持完整拼音子串搜索（落难瓦皮 → `luonanwapi`，搜 `luo`/`wa` 均可）；中文自动开、可配置。
- 已触发事件 = 涂黑置底（`VisitedEventIds` 判定）。

## 第十二批修复

- **删除“停用全部效果”按钮**（无效且多余）。
- **红圈改为下落粒子特效**：`CpuParticles2D` 一次性发射、重力下落、淡出后自消失；性能开销小。新增设置“启用节点特效”开关 + 粒子颜色；批量替换的节点同样加特效。
- **修复 ESC 失效**：`Menu1Overlay.CloseIfOpen` 之前误用 `_panel.Visible`（面板在 `CenterContainer` 内，该值恒真），导致 ESC 被永久吞掉；改用 `_center.Visible`。
- **拼音搜索改为模式单选**：`全拼 / 首字母 / 两者`（`AddEnumChoice`），移除多余的双开关。
- **已触发判定修正**：`VisitedEventIds` 按 `ModelId.Entry` 字符串比较（原来 `Contains(ModelId)` 可能因对象比较失效，导致第 2、3 个判断错）。
- **先古卡死修复（换方案）**：强制定向远古时，同步把该 Act 的 `RoomSet.Ancient` 设为所选远古再返回（此前只改返回值、`_rooms.Ancient` 不一致导致卡死）；`ResolveAncient` 从 `ModelDb.AllAncients` 解析。
- **跨局残留修复**：生成新地图时清空 `MapMarks / EditHistory / BulkHistory / ForcedEventRegistry`，特效不再跨对局。
- **远古遗物栏**：预览区下方用游戏原生 `NRelic` 图标控件（`IconSize.Large`）直接放置该远古的全部遗物，与游戏内遗物栏一致、可悬停查看。

## 第十三批修复

- **卡死根因（日志确认）**：`NNormalMapPoint.IconName(Ancient)` 在 0.113 无 Ancient 分支 → 抛 `ArgumentOutOfRangeException('Ancient')` → 地图卡死。因此**任何节点都不能被设为 `Ancient` 类型**；批量目标已移除远古。
- **定向远古改为“远古事件”实现**：目标节点设为问号(Unknown)，进入时由 `PullNextEvent` 返回指定远古（`ResolveEvent` 也查 `AllAncients`）。节点显示为问号，进去仍是指定远古——不卡死。
- **粒子特效颜色修复 + 持续生成**：纹理改为白色、颜色由 `Color` 单次着色（不再双重相乘导致偏色）；`OneShot=false` 持续在节点周边环形生成、轻微漂移聚集，直到离开地图。
- **批量与单点统一逻辑**：新增 `MapNodeService.BulkApply`，Menu 1 规则批量与 Menu 2 单源批量都走同一路径（相同保护节点、target/强制事件、粒子特效、统一撤销快照）。

## 第十三批（RitsuLib 0.6.12）

- 前置升级 **RitsuLib 0.6.11 → 0.6.12**（compat 0.113.0）。
- **远古按地图分组**：远古与事件一样按 `ActIndices`/shared 过滤，列表只显示当前地图可出现的远古。
- **远古提示**：远古预览不再显示事件式（条件不满足等）提示，只提示“不符合当前地图的远古会变成随机事件”。
- **粒子特效**：`LocalCoords=true`（跟随节点，不跟屏幕）、去掉过高 ZIndex（不再盖住 ESC 菜单）、白色纹理 + `Color`+`ColorRamp` 单次着色（修红色变蓝）。
- **事件颜色判定**：绿色仅在 `IsAllowed && !visited && 属于当前Act` 时；非本图或已触发不再误标绿；已触发判定仍按 `ModelId.Entry`。
- **定向远古随机**：未指定具体远古的节点进入时**每次随机**挑一个远古（`Rnd.Chaotic`），不再固定同一层。
- **缓存**：schema 升至 5，强制重扫含遗物数据。

## 第十四批

- **Menu 1 目标新增“随机远古”，移除来源里的“远古”栏**：批量把选中来源节点替换成“当前地图允许的随机远古”（实现为问号节点 + 每次进入随机远古事件，不崩）。
- **定向远古界面**：去掉四色分类/图例，只保留“不符合当前地图会变成随机事件”的提示；远古预览下方用游戏原生 `NRelic`（`IconSize.Large`）直接放置该远古全部遗物（含图标，失败回退为遗物贴图+tooltip）。
- **统一逻辑**：`ConvertMap` / Menu1 批量 / Menu2 单点批量全部走 `MapNodeService.BulkApply` → `ApplyHoverEdit`，同一套保护节点、目标/强制事件、粒子特效、撤销，杜绝“另一边变随机问号”的疏漏。


















## 工程结构
```
NodeForge/
  NodeForge.csproj          # 引用 base\deps\refs 下的 sts2/GodotSharp/BaseLib/RitsuLib
  mod_manifest.json         # mod id: NodeForge, 依赖 BaseLib + STS2-RitsuLib
  ModEntry.cs               # [ModInitializer] 初始化：设置/数据槽/设置页/PatchAll/事件扫描
  Settings/NodeForgeSettings.cs      # 设置 POCO + RitsuLib DataStore 绑定 + 本地 cfg 镜像
  Settings/SettingsRegistration.cs   # RitsuLib RegisterModSettings 页面（两个唤出键）
  Core/NodeTypeCatalog.cs   # 节点类型表（含 hover 专用“事件”项）
  Core/MapNodeService.cs    # 批量替换 + 悬浮编辑 + boss/当前节点保护
  Core/ForcedEventRegistry.cs # 地图坐标 → 强制事件 id
  Core/EventCatalog.cs      # 事件离线扫描 + 全貌缓存 + 增量 diff
  Core/HoverState.cs        # 当前悬浮节点状态
  Core/UiText.cs            # 中英文本 + 日志
  Patches/MapPatches.cs     # ModifyGeneratedMap / NMapScreen.SetMap / EnterMapPointInternal
  Patches/HoverPatches.cs   # NMapPoint.OnFocus / OnUnfocus
  Patches/ForcedEventPatch.cs # ActModel.PullNextEvent → 强制指定事件
  Patches/AncientVariationPatch.cs # PullAncient 变体（bug 修复）
  Patches/NGameInputPatch.cs # NGame._Input 热键
  Patches/NGameReadyPatch.cs # 挂载浮层
  UI/UiTheme.cs             # 主题
  UI/Menu1Overlay.cs        # 子菜单1（批量替换）
  UI/Menu2Overlay.cs        # 子菜单2（悬浮编辑 + 事件选择/预览）
```

## 构建
```
dotnet build NodeForge/NodeForge.csproj -c Release
# 产物: NodeForge/bin/Release/NodeForge.dll
```
依赖 DLL 位于 `<deps>/`。

## 部署（每个 mod 一个文件夹）
游戏目录 `mods/` 下需要三个模组：
- `mods/BaseLib/`  ← BaseLib.dll + BaseLib.json + BaseLib.pck + BaseLibCompatibilityHelper.dll/.json
- `mods/STS2-RitsuLib/` ← RitsuLib release github 包整份
- `mods/NodeForge/` ← NodeForge.dll + mod_manifest.json

测试副本已部署：`<testgame>/mods/`。

## 游戏版本
本机为 public-beta **v0.111.0**（release_info.json）。BaseLib 3.4.8（min 0.107.1）、RitsuLib 0.6.10（min/compat 0.111.0）均匹配。

## 已知限制 / 待验证
- 悬浮改节点即时改写 `MapPoint.PointType` 并调用节点 `RefreshVisualsInstantly()` 刷新图标；已生效，但需实机确认视觉刷新与存档持久（地图 PointType 通常随存档保存）。
- 强制事件通过 `ActModel.PullNextEvent` Postfix 注入；若事件本身对 Owner/RunState 有特殊要求，个别事件可能表现不同，可回退为随机。
- 事件全貌为静态文本（标题/描述/初始选项文字），不含选择后的动态结果（按需求确认）。
