# `lib/` 冻结副本说明（FROZEN）

> 本目录下的两个库是**源码内嵌的冻结副本**（不是 NuGet 包、不会自动跟随上游）。
> 冻结日期：2026-10-01 ｜ 对应轮次：第五十四 / 五十五轮 ｜ 本插件为**个人自用**，不对外分发。

## 一、来源与许可

| 目录 | 上游 | 许可 | 本插件用到的部分 |
| ---- | ---- | ---- | ---- |
| `lib/OmenTools` | `AtmoOmen/OmenTools` | MIT | 服务容器、字体、窗口、词条、日志消息、NPC 收购价数据源、若干扩展方法 |
| `lib/DailyRoutines.Common` | `Dalamud-DailyRoutines/DailyRoutines.Common` | AGPL-3.0 | `ModuleBase`、`Overlay`、`CardComponent`、`RemoteSnapshot`、宿主抽象 |

## 二、冻结策略（重要，改代码前先读）

1. **永不同步上游。** 上游作者的其他插件 / 库怎么改都与本插件无关：本插件只在自己需要时改动这些文件。
2. 只有两种情况才动本目录：① Dalamud / 游戏版本更新导致编译或运行失败 → 做**最小修补**；② 本插件确实需要某个能力。
3. 每次改动都必须登记到 `设计文档.md` §14.3（`D1`、`D2`…），写清**改了什么、为什么**。
4. **服务白名单是硬闸门。** `OmenTools/DService.cs` 的 `ENABLED_SERVICE_TYPES` 决定哪些服务会被实例化并 `Init`：
   - 不在表内的服务**一律不生效**（不挂 Hook、不起后台 tick、不发网络请求），并会在加载日志里逐个列名（`[OmenTools][服务白名单]`）；
   - 因此往本目录里新增服务文件**不会**让它悄悄生效；要用必须显式加进白名单，并在 `CHANGELOG.md` 里说明理由与它 `Init` 时做什么。
5. **加载自检**：插件启动时输出两行 `[AutoBuyer][自检]`（启用服务清单 / 本方公共面），
   用于在游戏出现异常行为时判断「是不是本插件干的」。

## 三、当前生效的服务（白名单，12 个）

`FrameworkManager`、`FontManager`、`WindowManager`、`LocalizationManager`、`CommandManager`、
`LogMessageManager`、`ItemSourceManager`、`GameState`、`LocalPlayerState`、`SecureSaveHelper`、
`HTTPClientHelper`、`NotifyHelper`。

`TooltipManager` 已于第五十五轮**从库中删除**（连同它的枚举与修改器共 8 个文件）——它是上一轮删除 20 个服务后
库内**唯一**没有任何引用的服务。因此现在「白名单外服务数 = 0」：加载日志里一旦出现
`[OmenTools][服务白名单] 已跳过…` 一行，就说明库里**真的新增了**服务文件（该行本身就是告警）。

**库内已不存在**（第五十四 ~ 五十六轮删除）：右键菜单（`ContextMenuManager`）、命令层（`ExecuteCommandManager`）、
封包收发（`GamePacketManager`）、输入 ID（`InputIDManager`）、区域指示（`ZoneIndicatorRenderer`）、
成就 / 玩家状态 / 目标 / 控制器 / 数据共享 / IPC / 链接载荷 / 实例 / 游戏资源 / 标准时间 / 道具提示 等 21 个服务，
以及 `LodestoneSearcher`、`FuzzyMatcher`、`AngleSharp`/`GuerrillaNtp` 依赖、全部未使用的 DTO / 词条解析器 /
Interop 帮助类 / ImGui 控件（`Combos`、`MapRenderer`、`DatePicker`）与 13 个零调用扩展。

## 四、已做的本地改动索引（细节见 `设计文档.md` §14.3）

| 编号 | 内容 |
| ---- | ---- |
| D1 | `FontManager` 不再后台枚举本机字体 |
| D2 | `OmenTools.csproj` 的 `NoWarn` 追加 `CS0618` |
| D3 | 字体构建门控 `IsUIFontBuilding` 仅首次构建生效 |
| D4 | `LRUCache` 改用 `DateTime.UtcNow`（解除对标准时间的硬依赖） |
| D5 | `DatePicker` / `ImageHelper` 解除标准时间依赖 |
| D6 | 删除 `StandardTimeManager` + 移除 `GuerrillaNtp` 包（原本加载即请求腾讯 checktime + NTP） |
| D7 | 删除 20 个未使用服务及其连带死代码（141 文件 / 约 17,500 行） |
| D8 | `DailyRoutines.Common` 的 `ModuleInfo` 删除「作者支持链接」自动请求 |
| D9 | `DailyRoutines.Common` 移除 `TimeAgo.Core` 包 |
| D10 | 插件本体死代码清理（历史成交管线、卡片 tooltip 管线等） |
| D11 | `DService` 服务白名单 + 加载自检（第五十五轮） |
| D13 | 删除 `TooltipManager` 及其枚举/修改器（8 文件，第五十五轮） |
| D14 | 批量删除库内「零引用」文件（约 112 文件 / 约 10,000 行，第五十六轮） |
| D15 | 移除 `AngleSharp` 包（唯一使用者 `LodestoneSearcher` 已删，发布包 −1 MB，第五十六轮） |
| D16 | `NotifyHelper` 删除 `#region TTS`（只转发 EdgeTTSIPC，本插件不用，第五十六轮） |
| D17 | 插件侧删除 16 条未使用的 `global using I* = …` 别名（第五十六轮） |

> **依赖闭包现状**：运行时只需 `AutoBuyer` + `OmenTools` + `DailyRoutines.Common` + `TinyPinyin` 四个程序集；
> 部署目录合计约 **900 KB**（1.1.2 时约 2.26 MB）。

## 五、维护提示

- 本副本的代码**越少，将来跟着 Dalamud / 游戏更新修的成本越低** → 确认没人用的代码请直接删（登记进 §14.3）。
- 依赖闭包、产物清单与词条计数记录在 `测试文档.md`；发布包体积变化记录在 `CHANGELOG.md`。
