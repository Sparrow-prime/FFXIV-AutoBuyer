# FFXIV-AutoBuyer 更新日志

> 本文件是**历史档案**：逐轮记录「现象 → 根因 → 处置 → 实机结论」，含已否决与已回滚的方案。
> 现行设计见 `设计文档.md`，测试方法与验收清单见 `测试文档.md`，需求与决策见 `立项文档.md`。
>
> 归档日期：2026-09-28（由三份文档中的历史章节汇总，内容未删减，仅去重与统一体例）。
> 版本号遵循 `x.y.z.a`：正式发布对外显示 `x.y.z`。

## 版本索引

| 对外版本 | 日期 | 本文件的对应章节 | 提交 / Release |
| ---- | ---- | ---- | ---- |
| 1.0.0 | 2026-09-12 | §1.0.0 | `98d9cc3` ／ tag `1.0.0` |
| 1.0.1 | 2026-09-14 | §1.0.1 | `ea00e51` ／ tag `1.0.1` |
| 1.1.0 | 2026-09-18 | §1.1.0 | `ffdcf99` ／ tag `1.1.0` |
| 1.1.1 | 2026-09-19 | §1.1.1 | `392e0d5` ／ tag `1.1.1` |
| 1.1.2 | 2026-09-24 | §1.1.2 | `6d50976` ／ tag `1.1.2` |
| 1.1.3 | 2026-10-01 | §1.1.3 | `73ae5bd` ／ tag `1.1.3` |

**版本归属的口径与限度（如实说明）**：第五轮之前的逐轮记录，原文（`测试文档.md` §四~§七、`设计文档.md` §十五~§十七）只注明日期、未注明所属版本，无法凭原文可靠判定每轮落在哪个 tag 内；本节按**日期先后**排入相邻版本，凡原文未明示者一律不臆断，均列在对应版本下并标注轮次与日期。第五轮及以后各轮的版本归属以原文为准。

---

## 1.0.0（2026-09-12）

首个正式版本：把 DailyRoutines 的 `BetterMarketBoard` 模块移植为可独立安装的国服 Dalamud 插件，并按需求裁剪界面、新增「目标数量购买」。

- **构建与产物**：`dotnet build FFXIV-AutoBuyer.slnx -c Release` → 0 错误 / 0 警告；产物落 `E:\Code\Output\FFXIV-AutoBuyer\Release\`。
- **自动化验证**：插件清单字段（`Name`/`InternalName` 均为 `AutoBuyer`、`DalamudApiLevel 15`）、产物合规（未携带 Dalamud 与 DR 专有程序集）、词条 46/46 无缺失无冗余（该数字随版本增长，1.1.2 为 59）、Universalis 契约 40 项断言全 PASS、静态合规扫描无 DR 私有命名空间残留。
- **需求落点自检**：`TabBar` / `DrawMarketHistoryTab` / `DrawMarketPriceTrend` / `DrawMetricDashboard` / `BatchPurchase` / `SelectAll` 出现次数均为 0；`ItemSelectorTab.History` 0 处；`Lang.Get("Tax")` 0 处。
- **Release 附件**：`latest.zip` 1,065,343 B（SHA256 `6f6aae04cf6f042abcdb5e8cb3961630c76288d3655d368a6dd8e1401542c3f7`），内含 `AutoBuyer.dll` 157,696 B（SHA256 `aa0565b1b718ceedf8a76ef3eaf0d41d105978d177e33dde8fd9bb5b993ba75b`）。
- **推送方式**：SSH（`git@github.com:Sparrow-prime/FFXIV-AutoBuyer.git`）+ `api.github.com`，Token 取自 `E:\Code\Personal file`，未落盘、未回显。
- **未入库内容**：`立项文档.md` / `设计文档.md` / `测试文档.md` 按用户要求仅保留本地；未提交 `pluginmaster.json`。
- **许可**：根 `LICENSE.md`（AGPL-3.0）+ `lib/OmenTools/LICENSE`（MIT）+ `lib/DailyRoutines.Common/LICENSE`（AGPL-3.0）。

### 编码阶段实现说明与偏差（原文：设计文档 §十四）

实际落地的工程结构：

```
FFXIV-AutoBuyer/
├─ FFXIV-AutoBuyer.slnx                 # 3 个项目：插件 + lib/OmenTools + lib/DailyRoutines.Common
├─ README.md / LICENSE.md / .gitignore / .editorconfig
├─ 立项文档.md / 设计文档.md / 测试文档.md
├─ FFXIV-AutoBuyer/
│  ├─ FFXIV-AutoBuyer.csproj            # Dalamud.CN.NET.Sdk/15.0.0
│  ├─ Plugin.cs                         # DService.Init → 本地化 → PluginHost → 模块
│  ├─ Host/PluginHost.cs                # IManagerHost 实现
│  ├─ Manager/LanguageManager.cs        # Lang 门面（OmenTools 无 Lang 类）
│  ├─ Localization/LocalizationSetup.cs + zh-CN.json
│  ├─ GlobalUsings/GlobalUsing.OmenTools.cs + GlobalUsing.Project.cs
│  ├─ Universalis/                      # 7 文件：模型 / HTTP / 缓存观察者 / 四个数据源
│  └─ MarketBoard/                      # 10 文件：模块主体 + UI + 数据 + 提示 + 购买
└─ lib/OmenTools（MIT）、lib/DailyRoutines.Common（AGPL-3.0，已裁剪）
```

与设计的偏差（均已确认落地）：

| 项 | 设计 | 实际 | 原因 |
| ---- | ---- | ---- | ---- |
| 程序集名 / 内部名 | 程序集 `FFXIV-AutoBuyer`、内部名 `AutoBuyer` | **两者均为 `AutoBuyer`** | DalamudPackager 的 `Manifest.InternalName` 恒取 `AssemblyName`（源码注释：InternalName "should match the assembly name"），无可配置属性；用户既有插件同样表现 |
| `Lang` 门面 | 复用 OmenTools 的 `Lang` | 自建 `FFXIVAutoBuyer.Manager.LanguageManager` + `global using Lang = …` | OmenTools **没有** `Lang` 类（那是 DR 自己的静态门面） |
| Universalis `GetOrRequest` | 返回 `Task` | 返回 `RemoteSnapshot<T>` | 与 DR 调用形态一致（Tooltip 直接接收快照做同步判断） |
| 跨服跳转按钮 | 未明确 | **移除** | 原实现依赖 DR 的 `/pdr worldtravel`（FastWorldTravel 前置），按决策 D4 一并移除 |
| NPC 商店跳转 | 未明确 | **移除**（保留 NPC 收购价基准行） | 原实现依赖 DR 模块 IPC `AutoShowItemNPCShopInfo`，按决策 D4 移除 |
| CI 工作流 | 改造模板 `pr-build.yml` | **删除** | 模板工作流针对国际服 Dalamud 分发地址；用户既有项目亦未使用 CI |
| 本地化文件复制 | `None` + `CopyToOutputDirectory` | 改为 `Content` | CN SDK 设置 `EnableDefaultNoneItems=false`，`None` 项不会被隐式包含 |

### 第二轮实机反馈修复（2026-09-12）

| # | 现象 | 根因 | 修复落点 |
| ---- | ---- | ---- | ---- |
| 1 | 安装器缺少「打开主界面」按钮 | 未订阅 Dalamud `UiBuilder.OpenMainUi` / `OpenConfigUi` | `Plugin.cs` 订阅/解绑；`MarketBoardModule.ToggleOverlayPublic` / `ToggleConfigPublic` 对外暴露（`ModuleBase.ToggleOverlay` 为 `protected`） |
| 2 | 销售列表行高过高、四列宽度不均、物品图标过大 | ① `marketDataTableImageSize` 取值来自 `ImGui.GetItemRectSize()`（上一个控件的实测尺寸，随相邻控件变化）→ 行高与表头图标被放大；② 数据列权重 `15 / 固定宽 / 15 / 15` 不相等 | 图标固定 `ImGui.GetFrameHeight()`；行高固定 `GetTextLineHeight() * 1.6f`；本地表与在线表数据列统一 `WidthStretch, 1f`（四列平分） |
| 3 | 跨服后、启动插件后长时间持续刷新 | `RetryLocalSearchIfStale` 由 `OnWorldWatch`（1 秒间隔）驱动，原实现在 60 秒窗口内每秒补拉一次（最多 ~60 次） | 改为指数退避 + 次数上限（最多 5 次：1s→2s→4s→8s…，总窗口 30s）；叠加同物品 1.2s 限流（`LOCAL_SEARCH_MIN_INTERVAL_MS`）；数据到达清零计数 |
| 4 | 购买按钮提示「…热键 5000…」无法理解 | `BetterMarketBoard-Purchase-Help` 原属列表行右键提示，且调用时误传 `PurchaseQuantity` | 按钮与列表行提示拆分，新增 `-Purchase-Button-Help` / `-Purchase-InProgress-Help`；列表行提示传 `ConflictKey.Keyboard`；`gen_loc.py` 增加 `OVERRIDE` 覆盖原机翻文案 |

附带加固：隐式刷新 3 秒兜底截止（`implicitRefreshDeadline`）；购买环 350ms 冷却 + 2 秒隐式刷新限流；窗口默认尺寸 `ScaledVector2(1000, 620)` + `SizeCondition = FirstUseEver`。

> 说明：问题 2、3 均为**移植自原模块的既有实现细节**（上游通过 DR 框架的窗口与调度上下文表现不同），本项目按国服独立插件的使用场景做了确定性改造。

### 第三轮实机反馈修复（2026-09-12）

| # | 需求 / 现象 | 实现 |
| ---- | ---- | ---- |
| 1 | 持有数量 + 目标数量输入框 + 购买按钮移到物品名同行并右对齐 | 行结构：`[图标][物品名] [☆][HQ][刷新] ——右对齐——> [持有数量][输入框][购买]`；`DrawPurchaseControls` 用 `MeasurePurchaseControlsWidth()` 测算（UIFont80 下：持有文字 + `90f*GlobalUIScale` 输入框 + 按钮文字 + `FramePadding.X*2` + 两处 `ItemSpacing.X`），把游标推到 `GetWindowContentRegionMax().X - 宽度`；窗口过窄时保留当前 X 以避免与左侧元素重叠 |
| 2 | 物品图标尺寸 = 「物品名 + 收藏/刷新按钮」行高 | `marketDataTableImageSize = new(MathF.Max(GetActualFontSize(1.6f), GetFrameHeight()))`，在绘制图标前计算（消除原先「取上一个控件实测尺寸」的一帧延迟与不稳定性）；列表行高独立固定为 `GetTextLineHeight() * 1.6` |
| 3 | 去掉成交均价 | `BuildMarketBenchmarks(npcGilPrice)` 仅保留 NPC 收购价；`DrawLocalMarketDataTable` / `DrawOnlineMarketDataTable` 去掉 `avgPrice` 形参与调用链；世界价格卡片悬浮详情去掉「成交均价」行；`BetterMarketBoard-AveragePrice` 词条随之从 `zh-CN.json` 移除（47 条） |
| 4 | 跨服后仍持续刷新 → 单次获取 / 失败重获取 / 成功即停 | `RetryLocalSearchIfStale` 重写：成功判定 `SearchItemId == SelectedItemID && IsFullyReceived(SelectedItemID)` → 立即 `localListingsStale = false` 且不再请求；失败最多重获取 `LOCAL_SEARCH_RETRY_MAX_ATTEMPTS = 2` 次（首次延迟 `1_500ms`、间隔 `2_000ms`、总窗口 `10_000ms`）；不在市场/列表卡住时不消耗重试次数 |

> 第三轮的第 4 项替代第二轮基于指数退避的实现：由「有限次补拉」升级为「以数据完整到达为终止条件的一次性获取」。

### 第四轮实机反馈修复（2026-09-12）

**右侧面板头部布局（最终形态）**

```
┌ 右栏 ────────────────────────────────────────────────────────────────┐
│  ┌───────┐  物品名          [持有数量 N] [目标数量▢] [购买]            │ ← 购买区与物品名同行、贴右
│  │ 图标  │  [☆] [HQ] [刷新]        ☐ 仅显示当前大区数据 ▼             │ ← 勾选框落在按钮行右侧
│  └───────┘    ↑ 图标高度 = 物品名行 + 行间距 + 按钮行（跨两行）          │
├──────────────────────────────────────────────────────────────────────┤
│  三低 / 三高 价格卡片                                                │
├──────────────────────────────────────────────────────────────────────┤
│  在售物品列表（四列平分）                                            │
└──────────────────────────────────────────────────────────────────────┘
```

- **购买控件组与物品名同行、贴右边缘**：勾选框所在的「上移一行」落点改为 `☆/HQ/刷新` 那一行，两行右侧因此互不争位。
- **图标跨两行**：`nameBand = max(GetActualFontSize(1.6f) + FramePadding.Y*2, GetFrameHeight())`，`icon = nameBand + ItemSpacing.Y + GetFrameHeight()`；图标用 `GetWindowDrawList().AddImage()` 绘制（不参与布局），另以 `InvisibleButton` 覆盖同尺寸矩形承接悬浮与后续扩展；物品名与按钮行用 `SetCursorPos` 手工落位。列表行高仍独立为 `GetTextLineHeight() * 1.6`。

| # | 现象 | 根因 | 修复 |
| ---- | ---- | ---- | ---- |
| 1 | 持有数量行与「仅显示当前大区数据」勾选框重叠 | 勾选框由价格区组件上移一行绘制，落点行正是购买控件组所在行 | 购买控件组保持在物品名同行；勾选框所在行改为 `☆/HQ/刷新` 那一行 |
| 2 | 物品图标仍偏小 | 图标高度只取「物品名行高」，未含下方按钮行 | 改为 `物品名行 + 行间距 + 按钮行`，改用 `DrawList.AddImage` + `InvisibleButton` 跨两行 |
| 3 | 跨服后持续刷新；连续切换物品提示「重新选择物品」 | ① 世界号抖动：`GameState.CurrentWorld` 在过场中读到 0 或反复跳变；② 搜索请求未串行化：不同物品绕过「同物品 1.2s」限流 | ① 世界切换去抖：忽略 0，需连续 2 次（2 秒）读到同一新世界，两次重同步间隔 ≥ 5 秒；② `RequestLocalSearchData` 改全局限流 600ms + 在途互斥（未完整返回最多等 3 秒）；③ 补拉收敛为最多 1 次（首次 3 秒、间隔 5 秒、窗口 12 秒） |
| 4 | 「出现列表 → 请稍后再次确认（错误）」循环往复 | 该错误＝服务器拒绝市场数据请求（`ProcessRequestResultHook` 中 `errorCode > 0` 且无结果 → `IsMarketListingsStuck = true`），而 `IsAbleToSearchMarket()` 不检查该状态，形成「请求 → 被拒 → 再请求」自激循环；`SyncItemWithGame` 每帧执行进一步放大 | ① 订阅 `GameState.MarketListingsStuck`，被拒后 20 秒静默冷却（`NotifyMarketRequestRejected`）；② `RequestLocalSearchData` 增加守卫（繁忙/冷却期不请求）；③ `SelectItem` 繁忙期不再请求或清列表；④ `SyncItemWithGame` 去抖（繁忙不跟随、最小间隔 3 秒、同一物品 30 秒内只跟随一次）；⑤ 保留手动刷新强制通道 |

---

## 1.0.1（2026-09-14）

**提交** `ea00e51` — *fix: 1.0.1 跨服数据隔离、补拉策略与物品跟随修复*（8 文件：版本号 + 7 个源文件，+276 / −70）
**版本号** 清单 `1.0.0.0` → `1.0.1.0`（修复位：z 动、a 归零）
**Release** <https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/tag/1.0.1>
**附件** `latest.zip` 1,066,350 B（SHA256 `25a58acf135a15b4dc491b1058f94ea47e414d601448602e2c222f37ac5bbc63`），内含 `AutoBuyer.dll` 159,744 B（SHA256 `b0afc81fd443faf34e4e25a5f11dac53f60fbd11811aab675e23840d9dcf0ab7`）

本次修复：① 跨服显示旧服数据；② 跨服后取不到数据（补拉窗口仅在可搜索时计时）；③ 切换物品跳回原物品；④ 隐式刷新不隐藏列表；⑤ 背包已满立即停止；⑥ 切换物品立即刷新价格卡片；⑦ 物品卡尺寸与控件位置调整。

### 第五轮：严查「连续刷新」（2026-09-12）

对**所有**会写游戏市场状态或触发请求的代码路径逐一核对：

| 路径 | 触发方式 | 现有守卫 | 结论 |
| ---- | ---- | ---- | ---- |
| `RequestLocalSearchData` | 唯一真正向游戏下发搜索的方法 | 服务器拒绝冷却 20s / 繁忙态拒绝 / 全局 600ms / 在途互斥 3s / 熔断 | 受控 |
| `SelectItem` | 点击卡片、命令、右键菜单、IPC、初始化、目录就绪、世界重同步、同步游戏侧物品 | 每次调用均带 `reason` 并打日志；繁忙态不请求 | 受控（可观测） |
| `ResyncAfterWorldChange` | 世界切换（1 秒轮询 + 去抖） | 去抖 | 受控 |
| `RetryLocalSearchIfStale` | 每秒轮询，仅在 `localListingsStale` 时 | 最多 1 次重获取、窗口 12s | 受控 |
| `SyncItemWithGame` | 每帧（OverlayUI） | 繁忙不跟随 / 最小间隔 3s / 同一物品 30s 只跟随一次 | 受控 |
| `FollowLocalSearch` | 当前无调用者（死代码） | — | 无影响 |
| 左栏搜索输入 | 文本变化时 | 每次变化一次 | 受控 |
| `EnsurePriceData` / `RequestTooltipAggregatedScope` | 定义未被每帧调用 / 悬浮提示（按缓存键去重） | — | 受控 |
| Universalis 观察者回调（28 世界） | 数据到达时 | 仅更新缓存 | 无游戏请求 |
| `ClearAllData` / `ClearListData` / `SearchItemId=` | 仅由上述受控路径触发 | — | 受控 |

审计中发现的两个「观感上的连续刷新」来源：

| # | 来源 | 说明 | 修复 |
| ---- | ---- | ---- | ---- |
| 1 | **卡表重建节流过密** | `Throttler.Shared.Throttle` 默认仅 500ms；28 个世界最低价陆续到达，每次置 `worldPriceTableDirty` → 卡片区约 2 次/秒重排 | ① 节流放宽到 3 秒；② 仅当该世界最低价真的变化时才置脏 |
| 2 | **缺少兜底熔断** | 即便某条路径被漏掉也应有最终防线 | 新增自动请求熔断：30 秒内自动请求超过 6 次即暂停 60 秒（手动刷新可越过） |

内置诊断（限流 5 条/秒，超出汇总条数）：

```
[AutoBuyer][诊断] 【发起市场搜索请求】item=5 reason=世界重同步 force=False
[AutoBuyer][诊断] 请求被跳过（全局节流）item=5 reason=SelectItem:同步游戏侧物品
[AutoBuyer][诊断] 世界重同步 world=1167 item=5
[AutoBuyer][诊断] 标记本地列表待补拉 reason=世界重同步
[AutoBuyer][诊断] 补拉重试 第 1 次 item=5
[AutoBuyer][诊断] 本地列表为空返回（游戏数据未接收完）item=5 条目=0/0
[AutoBuyer][诊断] 本地列表重建 item=5 HQ=False 条目=37 哈希=…
[AutoBuyer][诊断] 价格表重建 region=中国 HQ=False 仅当前大区=False
[AutoBuyer][诊断] 世界最低价变化 world=1167 36 -> 35
[AutoBuyer][诊断] 跟随游戏侧物品 game=5（我方=12）
[AutoBuyer][诊断] 服务器拒绝市场数据请求，进入 20 秒静默冷却
[AutoBuyer][诊断] 自动请求过于频繁（6 次 / 30 秒），已暂停自动请求 60 秒；可点刷新按钮手动恢复
```

**判读方式**：`/xllog` 过滤 `AutoBuyer`，跨服后观察 30 秒——大量 `【发起市场搜索请求】` 说明我们仍在刷游戏（看 `reason` 即知来源）；大量 `价格表重建` / `世界最低价变化` 说明卡片区在重排；大量 `本地列表为空返回` 说明游戏侧列表在抖动；大量 `跟随游戏侧物品` 说明游戏侧 `SearchItemId` 在抖动。

**追加根因：直接改写游戏搜索目标（第七轮复测后定位）**

| 位置 | 代码 | 后果 |
| ---- | ---- | ---- |
| `SelectItem`（每次选物品都执行） | `info->SearchItemId = itemID;` | 该字段是**游戏侧**市场搜索目标；游戏侦测到变化后会自行发起搜索，绕过本插件的节流/熔断 |
| `Init`（插件启动） | `InfoProxy->SearchItemId = 0;` | 同理：程序化清零也会让游戏重新搜索 |
| `FollowLocalSearch`（移植后无调用者的死代码） | `info->SearchItemId = itemID;` | 潜在隐患，已删除 |

因此现象是「商品列表持续刷新」+ 大量「获取过于频繁」，而插件自身的请求日志看起来并不频繁。**修复**：① 只在真正发起请求的那一刻写入搜索目标（`RequestLocalSearchData` 内 `EndRequest → SearchItemId = itemID → RequestData` 为全项目唯一写入点），`SelectItem` 与 `Init` 只读；② 节流参数收紧（全局 600ms → 2s、同物品冷却 10s、在途等待 6s、熔断 60s 内 >4 次停 120s、补拉 8s/8s/25s）；③ 玩家主动点刷新走 `force` 通道不受限。

### 第八轮：日志判读与修复（2026-09-12）

用户提供 02:53:40–02:54:53 的完整日志，据此定位并修复三项。

| # | 问题 | 根因 | 修复 |
| ---- | ---- | ---- | ---- |
| 1 | 关键词条缺失（`LocalizationManager` 报错） | `DevModuleTitle` / `DevModuleDescription` / `DailyModuleBase-Exported` 等来自**内嵌 DailyRoutines.Common**；`BetterMarketBoard-ShowAllWorldPrices` / `-CollapseAllWorldPrices` 源码为**多行** `Lang.Get`，旧正则漏采 | 生成器增加扫描 `lib/DailyRoutines.Common` 并补中文文案；正则改为 `Lang\.Get\(\s*"…"`。词条 47 → 54，校验「模块 + 内嵌 Common」全部键 0 缺失 |
| 2 | 商品列表持续刷新 | 静默冷却生效、请求日志消失后，仍每 ~300ms 出现 `本地列表重建`，且哈希在几个固定值之间循环（`-1547364697 / -306562502 / -1049797867`）→ 游戏侧 `Listings` 数组被反复重置/重排，而指纹按数组原顺序计算 | `CalculateLocalListingsHash` 改为**排序后哈希**（单价 → 数量 → 挂牌 ID） |
| 3 | 「获取过于频繁」的真凶 | `[DailyRoutines] [GameState] 市场交易板数据请求被服务器拒绝` 在 AutoBuyer 静默后仍约 2 秒一次（02:54:11 → 02:54:53 共 20+ 次，全部来自 `[DailyRoutines]`）→ 用户同时启用了 DR 的市场相关模块，`InfoProxy` 全局共享 | 建议在本插件已提供同等功能的前提下，在 DR 中关闭其「更好的市场布告板」模块 |

其他修复：`Universalis 请求失败: 429` —— `ClearAllData()` 每次切换物品都清空三份缓存导致每次重拉 28 个世界；改为保留以 `(物品, 世界)` 为键的缓存，仅超 `CACHE_ENTRY_LIMIT = 2000` 时整体清理。`[HITCH] Long "UiBuilder(AutoBuyer)" 112ms` —— 28 个世界回调逐个调用 `TooltipManager.TriggerItemDetailUpdate()`；新增 `RequestTooltipDetailUpdate()` 以 `Throttler` 限流 500ms。

### 第九轮：复核与收敛（2026-09-12）

DR 关闭后问题消失，据此复核前几轮：**需求/界面/真 bug 类修复全部保留；为「DR 症状」加的节流属于过度限制，按用户决策放宽数值并保留骨架。**

保留骨架（有真实价值）：只读游戏状态（除 `RequestLocalSearchData` 外不写 `InfoProxyItemSearch`）；服务器拒绝冷却 20 秒（日志有实证）；单次获取 + 成功即停（`IsFullyReceived` 为终止条件）；诊断日志改为默认关闭 + 配置开关。

放宽的数值：

| 参数 | 追症状时 | 现在 | 说明 |
| ---- | ---- | ---- | ---- |
| 全局最小请求间隔 | 2s | **800ms** | 连续点物品不再明显迟滞 |
| 同一物品冷却 | 10s | **移除** | 该冷却会挡住「购买后的隐式刷新」（回归），已删除 |
| 在途等待上限 | 6s | **3s** | 兼顾「不重复下发」与响应速度 |
| 熔断 | 任意 4 次 / 60s → 停 120s | 同一物品 10 次 / 60s → 停 120s（另有全局 30 次兜底） | 浏览不同物品不再触发；`购买后隐式刷新` 不计入熔断 |
| 补拉首次延迟 / 间隔 / 窗口 | 8s / 8s / 25s | **3s / 5s / 15s**（仍最多 1 次） | 空列表等待时间恢复 |
| 世界号去抖 | 连续 2 次 + ≥5s | 保持 | 代价仅跨服后最多等 2 秒 |

修复的回归：`BeginImplicitRefresh` 的请求若被节流跳过，现在会 `MarkLocalListingsStale` 交给补拉机制，保证购买后列表终会刷新。诊断日志改为开关：新增配置项 `EnableDiagnostics`（默认关闭），`DiagLog` 先判断 `MarketDataProvider.DiagnosticsEnabled`，关闭时零开销。词条 56 条，构建 0 错误 0 警告。

### 第十轮：启动时 Universalis 429 的成因与修复

| # | 成因 | 修复 |
| ---- | ---- | ---- |
| 1 | **目录失败后每 500ms 重试**：拉取失败时把 task 置空，而 `Init` 的 TaskHelper 步进每 500ms 调用一次 → 429 循环 | 增加失败冷却 `CATALOG_RETRY_COOLDOWN_MS = 30s` |
| 2 | **缓存失败后立刻重试**：请求失败时不设置过期时间 → 下一次 `GetOrRequest` 立即重试 | `Entry.FailureBackoffUntilUtc` + `FailureCount`：失败后指数退避 20s → 40s → 80s（上限 180s），成功清零 |
| 3 | **28 个世界一次性并发**：选择物品时同时发起 28 个聚合请求 | `MAX_AGGREGATED_WORLDS_PER_CALL = 6` 分批；新增 `OnWorldWatch`（每秒）驱动的 `EnsurePriceData` 逐步补齐（约 5 秒拉完 28 个世界） |

已知项：首次打开窗口时偶发一次 ~111ms `[HITCH]`（Dalamud 阈值 100ms 报警），来源为一次性初始化（道具来源 / NPC 收购价查询 `ItemSourceInfo.Query` 与首次卡表构建），不持续复现；当前不阻塞使用。

### 第十一轮：通信最小化（2026-09-12）

目标：尽量减少与游戏服务器、Universalis 的通信，避免被判定为脚本或滥用。

**游戏服务器（市场搜索请求）——只在玩家操作时发生**

| 触发 | 请求数 | 说明 |
| ---- | ---- | ---- |
| 选择物品（点击卡片 / 命令 / 右键菜单 / IPC / 搜索） | 1 | 玩家明确动作 |
| 点击「刷新」 | 1 | 走强制通道 |
| 打开布告板窗口 | 最多 1 | 仅在「关 → 开」的瞬间补一次 |
| 购买流程中的隐式刷新 | 每次购买 ≤1 | 玩家已发起购买 |
| 跨服后 | 不再是立即请求：仅标记待补拉；窗口开着才会在 3 秒后补 1 次 | 原实现跨服即发请求 |
| 插件启动 | **0** | 改为 `AdoptItemWithoutSearch`（只采用游戏当前物品用于显示，不搜索） |
| 窗口关闭时 | **0** | `OnWorldWatch` 在窗口未打开时直接返回 |

**Universalis 请求**

| 收敛项 | 效果 |
| ---- | ---- |
| 默认「仅显示当前大区数据」勾选 | 只拉当前数据中心 7 个世界，而非 28 个（约 1/4 请求量） |
| 单次调用最多 6 个世界 + 每秒补齐 | 无突发并发 |
| 本服可直读时不请求本世界挂牌数据 | 常见场景省 1 次请求 |
| 移除历史成交数据请求 | `成交均价` 已删除、`GetHistoryPercentilePrice` 无调用者 → 每物品省 1 次请求 + 1 个订阅 |
| 失败指数退避 20s→40s→80s（上限 180s） | 失败不再连环重试 |
| 目录失败冷却 30 秒 | 同上 |
| 窗口关闭时 | 0 请求（`EnsurePriceData` 仅在窗口打开时执行） |
| 缓存复用 | (物品, 世界) 键控缓存跨物品复用（TTL：聚合 5 分钟 / 挂牌 60 秒 / 元目录进程内一次） |

### 第十二轮：价格来源优先级与「缺世界数据」修复（2026-09-12）

**用户反馈**：① 短时缓存服务器最低价并优先于 Universalis 显示，避免跨服后价格不准；② 陆行鸟区缺少**沃仙曦染**与**晨曦王座**的价格数据。

价格表每个世界最低价的取值顺序改为：① 服务器（游戏内）实时数据，读取成功即写入短时缓存 `gameMinPriceCache[(物品, 世界, HQ)]`（TTL 120 秒）；② 服务器短时缓存，跨服/刷新间隙游戏数据短暂不可用时优先使用它，不再立刻回落到 Universalis；③ Universalis 聚合数据，仅当前两者都没有时才使用。

缺世界数据的真因：线上核对（`GET /api/v2/data-centers` + `/api/v2/worlds`）确认陆行鸟共 8 个世界（含 `1174=沃仙曦染`、`1175=晨曦王座`），Universalis 侧数据完整；真因在分批请求的实现缺陷——`if (processedWorlds >= 6) continue;` 每次调用都从头开始，只处理最前面的 6 个，而「仅显示当前大区数据」默认开启 → 排在最后的两个世界永远不被请求。**修复**：改为扁平遍历 + 「已请求过且未过期则跳过（不消耗配额）」+ 配额用尽 `break`，使下一次 tick 从上次中断处继续。另新增**世界目录自愈**（`EnsureWorldCatalog` / `IsSameWorldSet` / `BuildAllWorlds` / `MarkPriceTableDirty`），并跳过在 `/data-centers` 中但 `/worlds` 中无名称的世界。`UniversalisApi` 的 User-Agent 改为 `FFXIV-AutoBuyer/1.0 (+仓库地址)`。

### 第十三～十四轮：远端 TTL 与批量参数（2026-09-12）

| 参数 | 原值 | 现值 | 依据 |
| ---- | ---- | ---- | ---- |
| 服务器最低价缓存 `GAME_MIN_PRICE_TTL_MS` | 120 秒 | **30 分钟** | 用户要求：短时缓存服务器最低价，优先于 Universalis 显示 |
| 缓存生效范围 | 仅当前世界 | **所有世界** | 离开过的世界在缓存期内也沿用服务器价格 |
| Universalis 单次批量 `MAX_AGGREGATED_WORLDS_PER_CALL` | 6 | **8** | 单一大区世界数硬上限为 8，一次调用即可覆盖整个大区 |
| 聚合最低价 TTL | 5 分钟 | **15 分钟** | Universalis API 更新明显滞后于其网页（新上传数据网页立即可见、API 需很久才更新），缩短重拉间隔无收益 |
| 挂牌列表 TTL | 60 秒 | **5 分钟** | 同上；该表仅在非本服视图下使用 |
| 历史成交 | 15 分钟 | 不再请求（无消费者） | 第八轮已移除调用 |

> 取舍说明：30 分钟缓存意味着「离开某世界后，该世界价格最多沿用半小时前的服务器读数」，期间若有人改价不会立刻反映；这是「服务器数据优先于众包数据」所要求的取舍。缓存在内存中，重载插件即清空。

### 第十五轮：查看其他服务器时表格变两列（2026-09-12）

**现象**：切换到其他服务器视图时，在售列表的四项（单价 / 数量 / 总价 / 雇员）被折成 2×2。**根因**：`DrawOnlineMarketDataTable` 的列数计算沿用了原模块的「减法」写法，但本移植版基准列数已从 6 改为 4 个数据列：

```csharp
var columnsCount = 4;
if (!isAnyHQ)          columnsCount--;   // 4 → 3
if (!isAnyOnMannequin) columnsCount--;   // 3 → 2   ← 实际建了 2 列
```

表体每行调用 4 次 `TableNextColumn()`，ImGui 在列数不足时第 3 次调用会换行，于是呈现为 2 列 × 2 行（本地表用 `++` 写法所以正常）。**修复**：改为与本地表一致的加法写法，并加注释说明此坑。

### 第十六轮：悬停浮动与三联卡片配色（2026-09-12）

① 去掉鼠标指向服务器价格时的浮动效果：`WorldPriceCardComponent` 覆盖 `HoverFloatOffset => 0f`（基类默认 `-2.5f * GlobalUIScale`），悬停不再上移，仅保留边框/高亮反馈（`EnableHoverAnimation` 仍为 true）。

② 三联卡片配色交换：

| 卡片 | 原配色 | 现配色 |
| ---- | ---- | ---- |
| 最高价（rank1） | OrangeRed（橙红） | **洋红**：底 `DeepPink`(0.18/0.28)、框 `Pink`(0.70/0.95)、价格 `LightPink` |
| 本服（当前世界） | `DeepPink` / `Pink` / `LightPink` | **青蓝**：底 `DeepSkyBlue`(0.20/0.30)、框 `DeepSkyBlue`(0.90)、价格 `DeepSkyBlue` |
| 选中（本服默认即选中） | `DeepSkyBlue` | 不变（与本服同色系） |
| 徽标 | 本服 `Pink`、最高 `OrangeRed` | 本服 `DeepSkyBlue`、最高 `Pink`（与卡片同步） |

优先级：`选中/本服` 判定先于 `最高`，因此若本服本身即最高价，卡片显示青蓝（本服）配色。

### 第十七轮：启动几秒后字体突变（2026-09-12）

**现象**：插件启动数秒后界面字体发生一次变化；用户怀疑引入了 DR 的自定义字体。**排查结论：并没有引入任何外部字体。** 内嵌 OmenTools 的 `FontManager.CreateFontHandleDefinition(size)` 首先看 `FontManagerConfig.FontFileName`：文件不存在 → 走 `AddDalamudDefaultFont(size, ...)` + 游戏符号 + FontAwesome（即 Dalamud 原生默认字体）；文件存在 → 才用该字体文件。本插件从未设置过 `FontFileName`（默认 `string.Empty`）→ 恒定走原生分支。真正的「变化」来自**字体图集的异步构建**：`FontManager.Init()` 在后台执行 `RebuildUIFontsAsync()`，构建完成前 `GetFont(size)` 返回回退字体 `AxisFont180`（游戏 Axis 18px），构建完成后才切换到按档位计算出的正式字号。

**修复**：① 字体构建完成前不绘制界面内容（`IsUIFontBuilding`，`OverlayUI` / `ConfigUI`），另有 20 秒安全兜底；② 内嵌 OmenTools 不再后台枚举本机安装字体（`RegenerateInstalledFonts` 会递归扫描 `C:\Windows\Fonts` 并用 GDI+ 逐个加载，仅供本插件不用的字体选择界面），需要时仍可手动调用 `RegenerateInstalledFontsAsync()`；③ 内嵌 OmenTools 的 `NoWarn` 追加 `CS0618`，屏蔽上游未使用代码的过时 API 警告。

### 第十八轮：「统一使用游戏原生字号」模式（已撤销）

用户反馈喜欢此前**回退字体**（游戏原生 `Axis 18px`）的观感。由于两者是不同取舍（回退字体固定 18px 不随 UI 缩放；分档字号会随 `ImGuiHelpers.GlobalScale` 缩放），当时做成配置开关而非直接替换：配置项 `UseUniformNativeFont`（默认关闭），界面位置「统一使用游戏原生字号」，开启时一律使用 `AxisFont180`。新增静态开关 `MarketBoardModule.UseUniformNativeFontMode` 与字体助手 `UIFont(scale)` / `UIFontSize(scale)`，**全部 24 处**字体取用点统一改走助手。

> 注意：开启统一字号后界面的字号层级会消失，图片/表格/卡片可能偏挤或偏小（尤其在高 UI 缩放下 18px 不随缩放变化）。

### 第十九轮：字号方案纠正（撤销上一条）

第十八轮的「统一使用游戏原生字号」**实测字号过大，不是所需效果，已撤销**。用户澄清真实诉求：不是要统一字号，而是小卡上的小字不该那么小。

| # | 改动 | 说明 |
| ---- | ---- | ---- |
| 1 | **撤销**配置项 `UseUniformNativeFont` 与设置界面开关 | 统一 18px 会让占界面多数的 12px 小字整体放大 50%，观感过大 |
| 2 | **抬升小字档位**：`UIFont(0.6f)` → `UIFont(0.72f)` | 20 × 0.72 = 14.4px（原 12px） |
| 3 | 新增**「界面字号」滑块**（16~28，默认 20） | 绑定 OmenTools `FontManagerConfig.FontSize`；拖动结束后保存并重建字体 |
| 4 | 字体构建门控**限时**（仅加载后 20 秒内） | 调字号触发的重建不会再把界面隐藏起来 |

### 第二十轮：移除物品右键菜单入口（2026-09-12）

按用户要求移除原 F10 需求「物品右键菜单 → 在市场搜索」：删除 `MarketBoardModule.SearchInMarketMenu`（`ContextMenuEntry` 实现）及字段；删除 `Init` 的 `ContextMenuManager.Reg(...)` 与 `Uninit` 的 `Unreg(...)`；词条 `BetterMarketBoard-SearchInMarket` 不再采集（58 → 57）；残留检查 `grep -rn "ContextMenu\|SearchInMarket" MarketBoard/*.cs` 无输出；产物 DLL 中不再包含 `SearchInMarketMenu`。打开布告板的入口保留：`/market` 命令、安装器「打开主界面」按钮、IPC `FFXIVAutoBuyer.MarketBoard.ToggleOverlay`。

### 第二十二轮：交互精简与指令调整（2026-09-12）

| # | 指令 | 处理 | 复测点 |
| ---- | ---- | ---- | ---- |
| 1 | 购买按钮降低亮度、看不清 | 暗橙底 + 浅暖色文字，三个按钮状态显式配色，按钮尺寸随字号 | 按钮文字清晰可读、悬停不刺眼 |
| 2 | 小卡字体再大点 | 0.72 → **0.8** | 小卡（徽标 / 世界名 / 价格）文字大小 |
| 3 | 取消悬浮显示游戏物品信息面板 | 删除 4 处原生提示调用 | 悬浮左栏卡片 / 右侧图标不应再出现游戏物品面板 |
| 4 | 金币改英文千分位 | 新增 `MarketBoardFormatting.ToGilString()`（`N0` + 不变文化），替换 13 处 `ToChineseString()` | 价格显示形如 `1,234,567` |
| 5 | 传送改调用 Lifestream | 新增 `RequestWorldTravel(worldID, worldName)` 经 IPC `Lifestream.ChangeWorldById`；先查 `Lifestream.IsBusy`；未安装/未启用时提示 | 装 Lifestream：右键世界卡片可传送；未装：仅提示、不发指令 |
| 6 | 移除若干交互 | 复制物品名（含通知）/ 点击复制价格 / 按住修饰键右键直接购买（含 `Config.ConflictKey`）/ 左栏右键切换 HQ-NQ / 世界卡片悬浮详情 | 上述交互均不再生效；列表行右键「购买」菜单仍在 |

残留检查（应全为 0）：`ShowItemTooltip` / `HideTooltip` / `SetClipboardText` / `ClickToCopy` / `ConflictKey` / `ToChineseString` / `worldtravel`。构建 0 错误 0 警告；词条 59。

### 第二十三轮：6 项实机反馈（2026-09-13）

| # | 反馈 | 处理 |
| ---- | ---- | ---- |
| 1 | 左侧物品卡偏小，收藏与品级上下出界 | 卡片高度 `文字行高 × 2 + 10`（原 `×1.6 + 6`）；右侧操作区 18 → 20 |
| 2 | 「仅显示当前大区数据」+「展开全部世界价格」按钮位置 | 从价格区顶部移到标题区第二行、紧随「刷新市场数据」按钮；删除原先基于 `GetWindowContentRegionMax()` 的靠右绝对定位与游标还原 |
| 3 | 目标数量输入框偏长、整行偏高 | 输入框宽度 90 → **45**（`PurchaseTargetInputWidth`，绘制与测宽同源）；购买控件行按名号行高垂直居中 |
| 4 | 自动购买时列表被隐藏，未实现隐式刷新 | `GetLocalListingsDataSet`：数据未接收完整时沿用上一次完整数据（原先直接返回空） |
| 5 | 背包超容提示后未第一时间停止购买 | 下单前预检查背包空位 → 已满立即停止且不发出请求；等待上限 5s → 2.5s；超时提示区分「背包已满 / 等待超时」 |
| 6 | 切物品时价格卡片短暂显示上一物品数据 | 新增 `priceTableItemID`：物品变化即清空 `worldPriceRanks` / `cachedDCWorldPrices` / 最高最低标记并强制本轮重建（不受 3 秒节流限制） |

### 第二十四轮：跨服后显示上一服务器数据（2026-09-13）

**根因（两处叠加）**：① 世界变化采用去抖确认（连续 2 个 tick + 距上次重同步 ≥5 秒），这 2~5 秒内游戏侧 `InfoProxyItemSearch` 仍残留上一服务器的挂牌，而判定只检查 `IsFullyReceived()`，于是把旧服务器数据当作本服实时数据渲染；② 该残留数据还会被写入服务器最低价缓存（30 分钟），键为 (物品, 世界, HQ) 而写入时用的是「当前世界」，于是旧服务器价格被缓存成新服务器的价格。

**修复**：

| # | 措施 | 说明 |
| ---- | ---- | ---- |
| 1 | 新增 `MarketDataProvider.InvalidateWorldData(reason)` | 首次发现世界变化即当帧执行（不去抖）：清空价格表、世界价格缓存、最高/最低标记、本地列表；`ClearListData()`；`SelectedWorldID` 立即切到当前世界；标记待补拉（窗口未打开不发任何请求） |
| 2 | `OnWorldWatch` 首次运行只记基线 | 避免插件启动时把「0 → 当前世界」误判为跨服 |
| 3 | 新增 `IsGameMarketDataUsable(itemID)` | 取代原先 4 处直接判定；跨服后必须观察到「列表被清空 → 重新到齐」的完整循环才认可游戏侧数据属于新世界 |
| 4 | 隐式刷新与该判定兼容 | 同世界内（`worldDataRefreshed` 为真）仍沿用上一次数据不隐藏列表；跨服过渡期一律不显示 |
| 5 | `ResyncAfterWorldChange` 去重 | 世界数据已在第 1 步作废时不重复清空，避免把刚取到的新世界数据再清一次 |

### 第二十五轮：跨服后「获取太早 / 未成功获取数据」（2026-09-13）

**根因**：补拉策略与真实场景不匹配——首次补拉延迟 3 秒（跨服落地后玩家还在过场）；最大自动尝试次数仅 1 次（唯一一次若太早便再无第二次）；补拉总窗口 15 秒为墙上时钟，而「可发起本地搜索」要求 `IsAbleToSearchLocalMarket()`（已登录 + 不在副本）——跨服过场期间为假 → 窗口早已过期 → `localListingsStale` 被清除 → 之后不再自动搜索。

> 口径更正（第五十九轮）：该轮原文写的是「玩家走到布告板通常远超 15 秒」，属**错误前提** —— 本插件**替代布告板界面**，本地搜索不需要玩家站在布告板前，跨服后唯一的等待来源是跨界传送的过场（`IsPlayerTransitioning`，通常数秒）。结论（窗口只在可搜索时计时、避免提前过期）不变，此处仅修正依据。

**修复**：① 补拉窗口只在「可以搜索」时消耗（`!IsAbleToSearchMarket()` 或 `GameState.IsMarketListingsStuck` 时自动续期并直接返回，窗口与次数均不消耗）；② 尝试次数 1 → 4，间隔 5s → 10s，总窗口 15s → 60s（可搜索时间），首次延迟 3s → 5s；③ 仍受通信最小化约束（只有布告板窗口打开时才走到补拉逻辑，每次只请求 1 个物品）；④ 保留手动兜底（刷新按钮 = `forceRefresh` 强制搜索并重新武装补拉窗口）。

### 第二十六轮：切换物品有时跳回原来的物品（2026-09-14）

**根因**：`SyncItemWithGame()` 判定过松——玩家点选 A→B 时若搜索请求被节流/在途等待跳过，游戏侧 `SearchItemId` 仍是 A；原逻辑只等「距上次选择 3 秒」就跟随游戏侧物品，于是 3 秒后把界面切回 A。

**修复**：① 选择保护期 3 秒 → **6 秒**（`SYNC_ITEM_GUARD_MS`）；② 新增 `provider.IsLocalListingsStale`：我方所选物品的搜索尚未完成时不跟随；③ 新增连续确认：必须连续 **1.5 秒**读到同一个「不同物品」（`SYNC_ITEM_CONFIRM_MS`）；④ 保留既有约束（同一物品 30 秒内只跟随一次、两次跟随至少间隔 3 秒）。

### 第二十七轮：跨服后未能正常显示销售列表（2026-09-15）

**根因**：第二十四轮引入的「清空 → 重新到齐」确认存在不可恢复的卡死路径——`IsGameMarketDataUsable()` 只有在「先看到 `IsFullyReceived() == false`，之后又看到 `== true`」时才解锁；若 `ClearListData()` 未让游戏侧出现「未就绪」采样，或采样时机撞上 `GetLocalListingsDataSet` 的提前 return，就永远不解锁 ⇒ 本服列表永远不显示（直到重载插件）。

**修复**：① 解锁路径 2：我方在新世界成功发起过搜索请求（`searchIssuedSinceWorldChange`，在 `RequestLocalSearchData` 内打标、世界变化时清除）→ 直接认可；② 解锁路径 3（兜底）：世界变化后超过 **15 秒**（`WORLD_DATA_CONFIRM_TIMEOUT_MS`）仍未确认 → 按可用处理，绝不永久空白；③ 状态机前移：`GetLocalListingsDataSet` 入口处先调用 `IsGameMarketDataUsable()`；④ 过渡期兜底显示：新增 `provider.IsGameWorldDataReady`，UI 在过渡期改用 Universalis 当前世界数据渲染列表（附「刷新中」提示）。

### 第二十八轮：连续购买残留挂单 / 跨服延迟 / 体验回退（2026-09-15）

| # | 反馈 | 根因 | 处理 |
| ---- | ---- | ---- | ---- |
| 1 | 连续购买时只有第一条被移除 | 购买后的隐式刷新受 `IMPLICIT_REFRESH_MIN_INTERVAL_MS`（2 秒）限流；界面又按指纹复用旧数据集 | 新增 `locallyPurchasedListingIDs`：购买成功后立即从显示列表移除该挂单（`MarkListingPurchased`，加入指纹版本号使缓存失效），游戏数据真正刷新后自动清理，不额外发请求 |
| 2 | 跨服后刷新延迟大 | 跨服后需等「首次补拉延迟 5 秒」，即使过场早已结束也在空等 | 新增 `waitingBoardAfterWorldChange`：过场一结束（可发起本地搜索）就立即发起搜索；首次延迟 5s → 2.5s、间隔 10s → 6s |
| 3 | 整体体验不如 1.0.0 | 各项限流/隔离保护叠加后的综合感受 | 跟随保护期 6s → 4s、连续确认 1.5s → 1.2s |

> 说明：1.0.0 的「快」部分来自更频繁的请求（当时尚未加入通信最小化）；当前限制了请求频率以降低账号风险，因此数据更新会更「克制」。

### 第二十九轮：采用「更保守」请求策略（2026-09-15）

用户选择**更保守**：购买后不自动刷新数据，仅本地移除已购行；数据只在开窗 / 手动刷新时获取。

| # | 措施 | 说明 |
| ---- | ---- | ---- |
| 1 | **购买后不再自动刷新列表** | 删除 `TryImplicitRefresh()` 及其常量/字段；购买成功只做 `MarkListingPurchased`（本地移除已购行，零额外请求） |
| 2 | **购买循环跳过已购挂单** | `FindCheapestListing` 过滤 `IsListingPurchased`；下单被游戏拒绝时也标记跳过并结束 |
| 3 | 跨服补拉收紧 | 尝试次数 4 → **2**、间隔 6s → **8s**（跨服后布告板可用时仍立即发起第 1 次） |

仍然保留的自动请求（仅这些）：打开布告板窗口 1 次本地搜索；跨服后布告板可用立即 1 次、最多再补 1 次（合计 ≤2 次，间隔 8 秒）；手动点「刷新市场数据」强制 1 次；Universalis 分档 TTL 不变（聚合 15 分钟 / 挂牌 5 分钟 / 目录每会话一次）。

### 第三十轮：突然买不了物品，提示「背包已满或无法继续购买」（2026-09-16）

**根因**：① 直接复用 OmenTools 的 `IsFull()` —— `InventoryManager.Instance() == null` 时返回 true，容器 `!IsLoaded` 时被 `continue` 跳过 → 计数为 0 也算「满」，跨服/过场/刚登录时必然误报；② 判定未考虑堆叠 —— 背包无空位但可与已有同类物品堆叠时其实买得成；③ 购买等待上限 2.5 秒偏短。

**修复**：① 新增自研 `IsUnableToReceiveItem(itemID)`：`manager == null` → 不阻断；容器未加载 → 跳过且要求至少读到一个容器；有空位 → 可收；有同类物品 → 可能堆叠、不阻断；只有「读到容器、无空位、无同类」才判定收不下；② 两处调用均改用该判定；③ 购买等待上限 2.5s → **4s**；④ 下单被游戏拒绝时不再把该挂单计入「已购」（它可能仍在售，隐藏会误导），只结束本次购买。

### 第三十一轮：购买水晶仍提示「背包已满」（2026-09-16）

**根因**：水晶 / 碎晶 / 晶簇存放在**水晶专用背包**（`InventoryType.Crystals`），而预检查只遍历 4 个主背包 —— 主背包满、且主背包里没有水晶 → 满足「读到容器、无空位、无同类」→ 误判「收不下」。**修复**：`PlayerInventoryTypes` 增加 `InventoryType.Crystals`，水晶背包的空位/同类水晶都能被识别；判定命中「收不下」时输出诊断（`判定收不下 item=… 已读容器=[…]`）。

> 附注：若水晶背包本身已满（水晶每格上限 9999），本条提示是正确行为 —— 需要清理水晶格再买。

---

## 1.1.0（2026-09-18）

**提交** `ffdcf99` — *feat: 1.1.0 跨服判定改走游戏日志，统一「当前世界」来源*（10 文件：版本号 + 8 个源文件 + 本地化，+574 / −257）
**版本号** 清单 `1.0.1.0` → `1.1.0.0`（设计位：y 动、z/a 归零）
**Release** <https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/tag/1.1.0>
**附件** `latest.zip` 1,068,704 B（SHA256 `b96a6a718b6d7660bc014f8c54947428c3609a30944c6dded14729a908860341`），内含 `AutoBuyer.dll` 163,840 B（SHA256 `47652c898fd2aa3b92d79b156f140807c5ef532e282ce71d9b682d9035faa09b`）

本次变更：① 跨服判定改走游戏日志（去掉 3 秒确认延迟与 5 秒最小间隔）；② 新增 `CurrentWorldID` 统一 26 处「当前世界」判定；③ 世界名解析与兜底（Lumina 世界表 → 前缀匹配 → Universalis 目录 → 角色结构体 → 大厅数据）；④ 仅窗口打开时监测 + 开窗校正；⑤ 一并包含 1.0.1 之后未发布的修复（去掉 Universalis 兜底、移除服装模特列、过渡期购买提示、购买期间禁止自动搜索等）。

### 第三十二轮：移除服装模特列 / 雇员名列错位（2026-09-17）

| # | 反馈 | 根因 | 处理 |
| ---- | ---- | ---- | ---- |
| 1 | 去掉服装模特**列** | 列表存在「模特（Mannequin）」标记列（本服表与 Universalis 表各一处） | 两表均删除该列（列数、表头、单元格一并移除）；数据层的 `IsMannequin` / `IsAnyOnMannequin` 保留（不参与显示，仅用于哈希与统计一致性） |
| 2 | 自动购买时只有雇员名那一列在上移，其他列未清除已买单 | 雇员名只能从游戏字符串数组按下标读取（`StringArray[208 + 6 × 行号]`），而代码用的是**渲染行号**；购买后本地移除已购行 → 渲染行号整体前移，雇员名便读到下一行的名字 | 新增 `LocalListingsDataSet.SourceRowIndexes`（挂单 ID → 游戏侧顺序中的原始行号）；雇员名改用该原始行号取值，渲染行号仅作兜底 |

附带调整：已购挂单的本地隐藏改由 `BuildLocalListingsDataSet` 统一处理（先记录原始行号，再隐藏已购行，统计口径也随之只统计可见行）。

### 第三十三 / 三十四轮：雇员名列仍错位 → 行号记忆（2026-09-18）

**更精确的根因**：游戏侧字符串数组（雇员名）与市场代理数据并不同步刷新——购买后 `info->Listings` 立刻少掉该挂单，但 `ItemSearch` 字符串数组里那一行的名字仍在（要等游戏布告板界面自身刷新才重排）；若此时把「已购记录」清掉，行号记忆会被判定为「没有隐藏行」而按当前顺序重新归位 → 后续每一行都读到上一行的名字。

**修复**：① 新增 `retainerRowIndexes`（挂单 ID → 雇员名所用行号）**行号记忆**：只有在「没有本地隐藏行」时才按当前顺序归位，一旦开始隐藏已购行就沿用隐藏之前的行号；② 不再因「游戏数据里已没有该挂单」而删除已购记录，改由统一的 `ResetPurchasedListings()` 在重新请求数据时清空（手动刷新 / 开窗补拉 `MarkLocalListingsStale` / 切换物品或世界 `ClearAllData`）；③ 记忆上限 512 条，超出整体重建；取不到记忆时回退到渲染行号。

**关键教训**：不要在「游戏数据少了一行」时清掉已购记录 —— 那会让记忆立刻复位，错位复现。

### 第三十五轮：跨服过渡期显示旧服挂单 / 刷新中无法购买（2026-09-18）

| # | 反馈 | 根因 | 处理 |
| ---- | ---- | ---- | ---- |
| 1 | 跨服后一段时间，列表变回原服务器挂单 | 第二十七轮为「避免列表永久空白」加入的超时解锁（`WORLD_DATA_CONFIRM_TIMEOUT_MS = 15s`）：只要超时就把当前游戏侧数据当成新世界数据 —— 若游戏侧仍是上一服务器残留，就会显示旧服挂单 | **删除超时解锁**，只保留两条可靠解锁（① 观察到「清空 → 重新到齐」；② 我方在新世界成功发起过搜索）；改为超时后重新补拉（`WORLD_DATA_REARM_INTERVAL_MS = 30s`，仅在布告板窗口打开时） |
| 2 | 跨服后显示「刷新中」时点购买，提示无法购买 | 过渡期列表可能正在显示 Universalis 兜底数据，而购买依赖游戏侧实时数据；按钮此时仍可点击，点了才失败 | ① `IsPurchaseAvailable` 纳入「世界数据是否就绪」；② `StartPurchase` 增加专用提示词条 `BetterMarketBoard-Purchase-WaitWorldData`；③ 购买途中若发生跨服，购买随之停止 |

词条 59（新增 WaitWorldData）。

### 第三十六轮：跨服后购买按钮长时间不亮（死锁）（2026-09-18）

**根因（死锁）**：世界变化 → `worldDataRefreshed = false`（过渡期）；界面绘制时 `IsGameWorldDataReady == false` → 走 Universalis 兜底表 → 不再绘制本地表 → `GetLocalListingsDataSet()` 不被调用；而解锁判定 `IsGameMarketDataUsable()` 只在 `GetLocalListingsDataSet()` 内部被调用 ⇒ 状态机永远不推进 ⇒ 按钮永远不亮。

**修复**：① 新增 `MarketDataProvider.TickWorldDataState(itemID)`，**主动推进状态机**，与「本地表是否被绘制」解耦（模块 1 秒 tick + Universalis 兜底分支每帧都会调用）；② 过渡期按钮的悬浮提示改为明确原因；③ 跨服后时间线：发现世界变化（当帧）→ 布告板可用即立即搜索 1 次 → 数据到达即解锁、按钮变亮（通常 1~3 秒内）。

### 第三十七轮：去掉购买按钮锁 + 跨服逻辑重做（2026-09-18）

用户要求把购买按钮的锁去掉，并重新解决「跨服后显示刷新中 / 无法购买」。

| # | 措施 |
| ---- | ---- |
| 1 | **删除整套「世界数据状态机」**：`worldDataRefreshed` / `worldDataNotReadySeen` / 超时解锁 / 30 秒重补拉 / `searchIssuedSinceWorldChange` / `TickWorldDataState` / `IsGameWorldDataReady` 全部移除（`grep` 校验 0 处残留） |
| 2 | **信任规则简化**：跨服时 `InvalidateWorldData()` 已调用 `ClearListData()` 作废旧世界数据，因此「数据完整（`IsFullyReceived`）」即代表它是切换之后重新取回的新世界数据 |
| 3 | **购买按钮不再置灰**：`canPurchase = !isPurchasing`，随时可点；`IsPurchaseAvailable` 不再包含「数据就绪」条件 |
| 4 | **购买改为「点一次就等」**：数据未就绪时不判失败，最多等待 **8 秒**（`PURCHASE_DATA_WAIT_MS`），期间主动请求一次刷新并提示「正在获取当前服务器的市场数据」，数据一到立即开买 |
| 5 | **列表显示**：本地（游戏内）数据有内容就用它，否则用 Universalis 兜底（附「刷新中」提示） |

**经验教训**：能用「游戏自身 API 的语义」（`ClearListData` + `IsFullyReceived`）表达的判定，不要自建跨帧状态机 —— 前者不会因为 UI 分支变化而漏推进。

### 第三十八轮：去掉 Universalis 兜底 + 跨服检测被门控卡住（2026-09-18）

| # | 反馈 | 根因 | 处理 |
| ---- | ---- | ---- | ---- |
| 1 | 不要显示 Universalis 兜底（不能买、不准、干扰观察刷新） | 第三十七轮为「避免空白」在本服也启用了 Universalis 兜底表 | 删除本服兜底：本服**只显示游戏内实时数据**，列表为空时显示「刷新中」提示；Universalis 仅保留给「查看其他世界」 |
| 2 | 跨服后列表不刷新，仍显示原服务器数据 | `OnWorldWatch` 开头有 `if (!IsAbleToSearchMarket()) return;` —— 跨服时玩家通常还没打开游戏布告板，因此世界变化根本检测不到 | 世界变化检测**无条件执行**（只做本地作废，不发任何请求）；请求相关逻辑仍受「布告板可用 + 窗口打开」约束 |
| 3 | 此次更新后购买时列表会刷新 | 购买期间游戏侧数据会短暂「未就绪」，此时本服兜底表接管列表 → 数据来源切换 | 随第 1 项一并消除：购买期间沿用上一次完整数据，列表不换源、不清空 |

### 第三十九轮：跨服确认延迟 + 取消「需要布告板」前提（2026-09-18）

| # | 指示 | 处理 |
| ---- | ---- | ---- |
| 1 | 跨服刷新太早，还没离开就尝试刷新 → 加 3 秒延迟 | 世界读数变化后先只记录时刻，**稳定 3 秒**（`WORLD_CHANGE_CONFIRM_DELAY_MS = 3000`）才认定跨服：此时才 `InvalidateWorldData()` 并重同步。此前「读到新世界 id 当帧就作废 + 立刻重同步」会在玩家真正离开前于原服务器触发一次无谓的重新搜索 |
| 2 | 本插件用于**取代游戏布告板**，放弃所有「走到布告板打开后」的理解 | **移除 `IsAbleToSearchMarket()`**（它含游戏内市场布告板界面状态判定）；所有调用点（数据层 3 处、购买 1 处、模块 2 处）统一改用 `IsAbleToSearchLocalMarket()`（仅要求「已登录 + 不在副本内」） |

`grep` 校验 `IsAbleToSearchMarket` / `pendingWorldTicks` / `WORLD_RESYNC_CONFIRM_TICKS` 均 0 残留。

**定位**：本插件是游戏布告板的替代品 —— 任何「先走到布告板、再打开界面」的假设都不成立。

### 第四十轮：购买后又刷新（2026-09-18）

**根因**：第三十七轮为「数据未就绪时自动等待」写的判定过于宽泛——`if (info->SearchItemId != purchaseItemID || !info->IsFullyReceived())` 视为「数据未就绪」→ 等待 + `RequestRefreshOnce`；而购买成功后游戏侧会短暂进入「未完整」状态（它自己在更新列表），这是正常过程，却被判定为未就绪 → 触发补拉 → 发出一次市场搜索 → 列表看起来又刷新了一遍。

**修复**：① 判定收紧为「游戏侧根本没有这个物品的数据」才等待/补拉（`info->SearchItemId != purchaseItemID`）；`IsFullyReceived` 为假不再视作未就绪；② 新增 `MarketDataProvider.AutoSearchSuppressed`：购买开始置位、结束复位，补拉入口在置位期间直接返回（窗口顺延）；③ 已购挂单仍由本地记录跳过，列表只消失「买掉的那一行」。

**教训**：把「游戏侧正在更新」误判成「没有数据」会引发多余请求；购买这种「正在进行的状态」应有明确的抑制开关，而不是依赖各处判定都写对。

### 第四十一轮：3 秒后仍在获取上一服务器数据（2026-09-18）

**根因**：`GameState.CurrentWorld` 取自 `AgentLobby.LobbyData.CurrentWorldId` —— 这个值在**过场刚开始**时就已经变成目标世界了，而此刻客户端与服务器仍是原服务器；因此任何在过场期间发出的 `RequestData()` 拿回来的都是原服务器的数据，固定 3 秒也无法覆盖过场时长（跨服落地可能十几秒）。

**修复**（以「过场是否结束」为准，而不是固定时间）：① 新增 `IsPlayerTransitioning`＝`Condition.IsBetweenAreas`（OmenTools 扩展，对应 `ConditionFlag.BetweenAreas/51`）或 `ClientState.TerritoryType == 0`；② 世界变化确认：过场中不断重置计时，过场结束后再稳定 3 秒才作废 + 重同步；③ 自动搜索（补拉）在过场期间一律不发起，窗口自动顺延；④ 顺序保证：过场结束 → 稳定 3 秒 → 作废旧数据 → 重新搜索。

**教训**：判断「状态切换完成」要用**客户端可操作状态**（过场结束），而不是时间或某个可能提前更新的字段。

### 第四十二轮：跨服判定改走游戏日志（2026-09-18）

**背景**：第三十九～四十一轮都在「用 `GameState.CurrentWorld` 判断世界变化」这条路上做延后与去抖，但实机仍会一直显示/获取上一服务器的数据 —— 说明该数据源本身不可靠，而不是等待时机不对。

```
LobbyData.CurrentWorldId
  ├─ 过场开始就变（此时客户端仍连原服务器）
  └─ 也可能根本不随跨界传送变化
        ⇒ ① 检测不触发 → 旧缓存永不作废 → 一直用上一服务器的数据
        ⇒ ② 锚点 SelectedWorldID 被写成错值 → 之后请求/缓存全挂在错世界
        ⇒ ③ 全插件 26 处「当前世界」判定都读该字段 → 判定一起错
```

**设计：以游戏日志为唯一权威信号，并统一「当前世界」的来源。**

```
LogMessageManager.RegPost（OmenTools）
  └─ 命中「使用跨界传送移动到了xxx。」（固定文案，除服务器名外无变体）
       ├─ 该行只在【落地后】出现 → 命中即当帧处理，无去抖、无延迟
       ├─ 世界名 → Lumina World 表精确匹配（回退最长前缀匹配 → Universalis 世界目录）
       │     └─ 识别失败：退到 LocalPlayer.CurrentWorld（角色结构体）→ GameState.CurrentWorld
       └─ HandleWorldChange(worldID)
            ├─ CurrentWorldID = worldID          插件认定的当前世界（唯一真相）
            ├─ InvalidateWorldData(reason)       作废旧世界显示数据（纯本地）
            └─ ResyncAfterWorldChange()          重锚 + 标记补拉（仍不立即请求）
```

| # | 设计点 | 说明 |
| ---- | ---- | ---- |
| 1 | 权威信号 | 游戏日志行「使用跨界传送移动到了xxx。」；文案固定，只按固定前缀定位并去掉句号；渲染文本尾部可能有 SeString 残留字符，故只做 Trim 而非定长截取 |
| 2 | 触发即落地 | 日志行只在落地后出现 → 删除 `WORLD_CHANGE_CONFIRM_DELAY_MS`(3s)、`WORLD_RESYNC_MIN_INTERVAL_MS`(5s)、`pendingWorld*`、`lastWorldID`；`OnWorldWatch` 不再承担世界检测 |
| 3 | 单一真相 | 新增 `MarketBoardModule.CurrentWorldID`：加载时取一次 `GameState.CurrentWorld`，之后由日志行权威更新；26 处「当前世界」判定统一改用它（数据层 11、世界卡片与 Tooltip 11、UI 帧 1、购买 1、模块 2） |
| 4 | 监测门控 | 仅在插件窗口打开时监测；窗口关闭期间的跨服由开窗时的一次校正兜底（`SyncWorldOnWindowOpen`，以 `LocalPlayer.CurrentWorld` 为准） |
| 5 | 检索策略不变 | 跨服后仍不主动发请求：标记待补拉，窗口打开且布告板可用时由既有「可用即立刻搜索」机制补 1 次 |

待实机验证（当时）：窗口打开时跨界传送 → 落地瞬间即作废并重同步、无 3 秒等待；连续跨服 A→B→A 各触发一次；窗口关闭时跨服再开窗则校正世界；同世界内移动/进副本不触发；窗口未打开时跨服由开窗校正兜底。

**教训**：当同一症状反复出现（第二十四 / 三十九 / 四十 / 四十一轮），应当怀疑**数据源本身**，而不是继续加延后与去抖 —— 换用「事件式、只在事实发生后出现」的信号（游戏日志）后，时序问题自然消失。

实机验证：用户反馈「测试通过」（跨界传送后不再显示上一服务器数据，无 3 秒等待）。

---

## 1.1.1（2026-09-19）

**提交** `392e0d5` — *fix: 1.1.1 已购行列同步、雇员名行号探针、失败判定提速与水晶类满包判定*（5 文件：版本号 + 3 个源文件 + 本地化，+234 / −76）
**版本号** 清单 `1.1.0.0` → `1.1.1.0`（修复位：z 动、a 归零）
**Release** <https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/tag/1.1.1>
**附件** `latest.zip` 1,069,831 B（SHA256 `73af5d1a37f3dc8a02965ce6e644973d8e2e8ebb5aed661a66e946e06288aac2`），内含 `AutoBuyer.dll` 165,888 B（SHA256 `75e1591fa869480df6cb1390c61fa51a22ec1e507f405c5dfa9f56dfdb1307af`）

本次修复：① 购买后「只有雇员列上移、三列不动」→ 已购行改在渲染期过滤 + 雇员名行号探针；② 购买失败判定 4s → 2s；③ 水晶类（9999 上限）「持有 + 本单 > 9999」直接判失败并给出独立提示。

### 第四十三轮：购买后「只有雇员列上移」与失败判定提速（2026-09-19）

**现象**：① 手动/自动购买后，在售列表只有雇员名列整体上移一行、另外三列完全没动；② 购买失败（挂单已被买走 / 收不下）的提示来得太慢。

**根因**：

```
购买成功 → MarkListingPurchased(id)：本地隐藏该挂单
  ├─ 代理数据随即「未接收完整」→ GetLocalListingsDataSet 沿用上一次快照
  │     ⇒ 快照仍含刚买掉的那一行 → 三个数据列完全不动
  └─ 游戏字符串数组（雇员名）已整体前移一行
        ⇒ 仍按「隐藏前的原行号」取名字 → 行号偏大一行 → 只有雇员列上移
```

问题本质：隐藏发生在渲染层之外（数据集层），而快照可能早于隐藏；同时「用原行号还是当前行号」不能一概而论 —— 取决于字符串数组有没有重排。

**修复**：

| # | 措施 | 位置 |
| ---- | ---- | ---- |
| 1 | **渲染期**过滤已购行（`IsListingPurchased`），数据集是旧快照也照样隐藏 → 四列同进同出；`IsAnyHQ` 按过滤后的行重算 | `UI.MarketData.DrawLocalMarketDataTable` |
| 2 | 新增「雇员名探针」：`MarkListingPurchased` 记录 (原行号, 该行号处的雇员名)；渲染时每帧判定一次「所有探针在原行号处是否仍是同名」 | `Data` + `UI.MarketData` |
| 3 | 探针成立（字符串数组尚未重排）→ 用隐藏前的原行号；已失效（已重排）→ 用当前显示行号 | 同上 |
| 4 | 两次购买之间字符串数组若自行重排，购买时先 `RebuildRetainerRowMemory()` 重建行号记忆再记探针 | `Data.MarkListingPurchased` |
| 5 | 字符串数组访问收敛为 `GetRetainerNameAtRow(rowIndex)`（含行号负值 / 空值防护） | `UI.MarketData` |
| 6 | **失败判定提速**：`PURCHASE_WAIT_MS` 4000 → **2000**（下单后等待「持有量增加 / 挂单消失」的上限；任务轮询 500ms，2 秒 = 4 次判定） | `Purchase` |
| 7 | **水晶类专属满包判定**：物品可堆叠上限 ≥ 9999（碎晶 / 水晶 / 晶簇）且「持有量 + 本单数量 > 9999」→ 取到最低价挂单后直接判失败，不发请求；提示独立为「该物品已达持有上限（9999），无法继续购买」 | `Purchase` + `Localization/zh-CN.json` |

**教训**：本地「已购隐藏」制造了两个不同步的视图（我们的行序 vs 游戏的字符串数组行序）。凡是「用行号取数据」的展示都必须先回答「两套行序此刻是否一致」，并用**可验证的探针**回答，而不是靠注释里的假设。

### 第四十四轮：窗口未打开时彻底静默（2026-09-19）

**需求**（用户原话）：「我希望这个插件在后台的时候啥也别干，也不要工具提示啥的」。经确认，「后台」＝**插件窗口未打开**，与游戏窗口是否在前台无关；购买任务无需特殊处理。

**口径（单一判据）**

```
IsPluginWindowOpen = Overlay is { IsOpen: true }
  ├─ tick（1s 世界巡检）：未打开 → 直接返回（原本已如此，保留）
  ├─ 跨界传送日志：未打开 → 不监测（原本已如此，保留）
  ├─ 道具工具提示：未打开 → 不追加市场数据、不发起请求        ← 本次新增
  ├─ 初始化目录任务：未打开 → 不发请求，按 500ms 轮询等待开窗  ← 本次新增
  └─ provider 级请求入口：EnsurePriceData / RequestAllWorldsData /
     RequestTooltipAggregatedScope 再各加一道「窗口未打开」闸门 ← 本次新增（纵深防御）
```

**为什么在 provider 层再加闸门**：请求入口不止 tick 一条路（初始化、工具提示、UI 交互、物品采用等），在唯一的低层出口统一拦截，比逐个上层调用点打补丁更不容易漏，也让「窗口未打开 = 零通信」成为可验证的断言。

**未做的取舍**：不加「游戏是否在前台」判定（用户明确与游戏状态无关）；不动购买任务（用户明确该情形不会出现）；窗口关闭期间发生的变化由开窗时一次性补齐（世界校正 `SyncWorldOnWindowOpen` + 一次本地刷新 `RequestRefreshOnce`）；配置项说明补「（仅在插件窗口打开时显示）」。

**教训**：把「要不要工作」的判据收敛成**一个**可命名的条件（`IsPluginWindowOpen`），并在**唯一出口**拦截，比在每个调用点写 `if` 更容易长期保持正确。

### 第四十五轮：彻底移除道具工具提示（2026-09-19）

**起因**：用户报「还是在刷工具提示」，并提供日志（每悬停一次道具就有 `[AutoBuyer] [TooltipManager] 物品工具提示内容刷新 / 物品工具提示刷新` 一对行）。

**根因（第四十四轮的静默挡不住它的原因）**：

```
OmenTools 的 TooltipManager.Init()      ← 由 DService 自动实例化所有服务时调用，无条件
  └─ IAddonLifecycle.RegisterListener(PreRequestedUpdate, "ItemDetail"/"ActionDetail")
       ⇒ 游戏每重绘一次道具提示框：
            TooltipManager.cs:169  打「物品工具提示内容刷新」（物品变了）
            TooltipManager.cs:188  打「物品工具提示刷新」（每次重绘）
            ↑ 这两行在「找注册者」之前就打了，与我们的配置 / 窗口状态 / 是否追加数据完全无关
```

第四十四轮加的「窗口未打开就返回」只挡住了我们自己的追加逻辑，挡不住这个钩子的日志。**要零日志，只能让那个服务根本不初始化。**

**方案（OmenTools 官方开关）**：

```csharp
// Plugin.cs
DService.Init(pluginInterface, static () => new DServiceInitOptions().Disable<TooltipManager>());
// 依据：DService.DiscoverEnabledServiceTypes() 里 .Where(t => !InitOptions.IsDisabled(t))
```

**删除清单**：

| # | 位置 | 内容 |
| ---- | ---- | ---- |
| 1 | `Plugin.cs` | 初始化时 `Disable<TooltipManager>()`（根因处理） |
| 2 | `MarketBoardModule.cs` | `RegItem(OnItemTooltipUpdate)` / `Unreg(...)` |
| 3 | `MarketBoard.Tooltip.cs` | **整文件删除**（追加文本、抓聚合行情、拼 3 行文案） |
| 4 | `MarketBoard.Data.cs` | `RequestTooltipDetailUpdate()` + 2 处调用点；`RequestTooltipAggregatedScope()`；`tooltipAggregatedSubscriptionCache`（字段 / Clear / Dispose 循环） |
| 5 | `MarketBoard.Models.cs` / `MarketBoard.UI.cs` | 配置项 `AppendMarketStatsTooltip` 与设置页勾选 |
| 6 | `Localization/zh-CN.json` | 删除 `Config-AppendMarketStatsTooltip(-Help)` 与 `Tooltip-CurrentRegion` |
| 7 | `MarketBoard.Utils.cs` | `GetAggregatedMarketScope()` 从被删文件迁入（`Data` 与 `UI.WorldPrices` 仍在用） |

**差点踩的坑（重要教训）**：有些「看名字像工具提示专属」的东西其实被别处在用 —— `GetAggregatedMarketScope` 还被 `Data` 的均价计算与 `UI.WorldPrices` 的世界价格卡片使用；`BetterMarketBoard-Tooltip-CurrentWorld` / `-DailySales` 还被 `UI.WorldPrices`（本服列名、日销量行）使用。删除前逐个 `grep` 引用点，是这轮唯一真正费时的事；也因此**先编译再谈其他**（第一次构建就报 `GetAggregatedMarketScope` 不存在，随即改为把静态方法放到模块 partial 类里）。

**好处（顺带）**：日志干净（不再有任何 `[AutoBuyer] [TooltipManager]` 行）；提示框零追加、零重绘触发；悬停道具不再产生「本服 + 大区」两个 scope 的 Universalis 请求与订阅。

**如何恢复**：`git show 392e0d5^:FFXIV-AutoBuyer/MarketBoard/MarketBoard.Tooltip.cs`（或直接 revert 本轮提交），再把 `DService.Init(pluginInterface, ...)` 改回单参、恢复 `RegItem` 注册即可。

**遗留（不在本次范围）**：DailyRoutines 的 `BetterMarketBoard` 模块是同一功能的另一份实现，它自带一份 OmenTools，因此仍会打 `[DailyRoutines] [TooltipManager] ...` 行并继续往提示框追加数据；要彻底干净需另行禁用该模块。

---

## 1.1.2（2026-09-24）

**提交** `6d50976` — *fix: 1.1.2 跨服检测日志化、行情/列表「拿不到数据」的四处根因修复与购买提示开关*（11 文件：版本号 + 9 个源文件/词条 + README，+665 / −388；含删除 `MarketBoard.Tooltip.cs`）
**版本号** 清单 `1.1.1.0` → `1.1.2.0`（修复位：z 动、a 归零）
**Release** <https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/tag/1.1.2>（tag `1.1.2` → `6d50976`）
**附件** `latest.zip` 1,069,986 B（SHA256 `6ffd0273f41b909240f7c83c24680f9f6754638c2e1ec29c72c5ffdc3b753d0d`），内含 `AutoBuyer.dll` 165,888 B（SHA256 `89aa44071bb532dc932b000f04ee06ea0099b149179938019779f3ede95f093f`）

本次修复：① 跨服检测日志化（常开 `[AutoBuyer][跨服]`：新路/老路各自的位置与来源）+ 窗口判据统一；② 跨服后本服列表不更新（空数据被 `IsFullyReceived` 判为「已接收」→ 补拉被短路）；③ 跨世界价格卡片取不到数据（上游 `failedItems` 与「请求发出但响应无人接收」被当成「15 分钟内已取过」→ 新鲜度改为以真的持有数据为前提）+ 卡片链路常开可观测性日志；④ 布告板搜索被服务器拒绝后再也不重试（补拉状态单一管理者、空转不计次、冷却到期自动重试、最小间隔 800ms → 2s）；⑤ 连续切换物品后选中回弹（跟随游戏侧物品改确认制）；⑥ 新增「背包已满导致停止购买时弹提醒」开关；⑦ 移除道具工具提示、窗口未打开彻底静默。

### 第四十六轮：跨服检测日志化 / 统一窗口判据 /「已满」提示开关（2026-09-24）

**需求**（`工作.txt`，2026-09-23）：①「好像未能成功读取到跨服日志」②「把新老两个版本的跨服检测输出到日志我看看具体位置和来源」③「把已满的停止购买提醒做个开关」。

**① 为什么看不到跨服日志（根因）**：世界切换检测只有两条路 —— 新路：游戏日志「使用跨界传送移动到了xxx。」→ `OnWorldVisitLogMessage` → `HandleWorldChange`；老路：窗口打开时以角色结构体世界校正 `SyncWorldOnWindowOpen` → `HandleWorldChange`。两条路的输出此前都走 `MarketDataProvider.DiagLog`，而它受 `EnableDiagnostics` 门控（默认关闭）且带 200ms 限流 ⇒ 玩家在 `/xllog` 中看不到任何跨服记录。

**② 设计**：新增始终输出的跨服专用日志出口，与诊断日志彻底分开：

```
MarketDataProvider.WorldLog(msg) → DLog.Warning($"[AutoBuyer][跨服] {msg}")
```

跨服事件本身稀疏（一次跨服 1~2 行、开窗校正 1 行、启动 1 行），因此常开不会刷屏；日志前缀 `[AutoBuyer][跨服]` 便于 `/xllog` 过滤。四类输出点：

| 时机 | 输出内容（位置 + 来源） |
| ---- | ---- |
| 插件加载（`BuildWorldVisitLogMessageIDs`） | LogMessage 表命中的模板行号清单 + 当前世界锚点；未命中则说明「退化为文本匹配」 |
| 读到跨界传送日志（新路） | 日志 id、解析出的世界名、解析来源（Lumina 精确 / Lumina 前缀匹配 / Universalis 目录 / 未识别→角色结构体→大厅数据）、当前锚点、窗口是否打开（未打开记「跳过」） |
| 开窗校正（老路） | 玩家实际所在世界 + 取值来源（角色结构体 / 大厅数据）、当前锚点、是否按跨服处理 |
| 世界切换落地（`HandleWorldChange`） | 「上一世界 → 新世界」+ 触发来源（跨界传送日志 id / 开窗校正） |

**③ 开关**：新增配置项 `NotifyInventoryFull`（默认 `true`）。`FinishPurchase` 增加 `bool inventoryFull` 参数，三处「收不下 / 达 9999 上限」的结束路径标记为 `true`；关闭开关时不再弹红字提示，改为静默停止 + 一行诊断日志（购买行为完全不变）。

**顺带合并（多轮改动收口）**：`OnWorldWatch` 内原内联判据 `Overlay is { IsOpen: true }` 改用模块内唯一判据 `IsPluginWindowOpen`（第四十四轮引入）；`ResolveWorldIDByName` / `ResolvePlayerWorldID` 增加 `out string source` 回报取值来源。

### 第四十七轮：跨服后「在售列表不更新」的根因与修复（2026-09-24）

**实测日志（2026-09-24 00:23~00:27）**：

```
[AutoBuyer][跨服] 启动：LogMessage 表命中 1 条跨界传送模板（行号 9414）…；当前世界锚点 = 0
[老路·开窗校正] 玩家实际所在世界=1167（来源=角色结构体），当前世界锚点=0 → 不一致，按跨服处理
[世界切换] 0 → 1167（红玉海），触发来源：开窗校正
[新路·日志判定] 读到跨界传送日志 id=9414，世界名="晨曦王座"，解析=1175（来源=Lumina 世界表（精确匹配））…
[世界切换] 1167 → 1175（晨曦王座），触发来源：跨界传送日志（id=9414）
```

**根因（一句话）**：`IsFullyReceived()` 把「`EntryCount == 0`（完全没有数据）」判为「已完整接收」，而跨服 `InvalidateWorldData()` → `ClearListData()` 制造的正是这个状态 —— 补拉被自己的成功判据短路，新世界的搜索永远不下发；同时 UI 侧也把空数据当成「可用」而以空列表收场。

```csharp
// OmenTools/Extensions/InfoProxyItemSearchExtension.cs
return ptr->EntryCount switch
{
    > 10 => ptr->ListingCount >= 10,
    0    => true,          // ← 空数据 = 「已完整接收」，跨服清空后即命中此分支
    _    => ptr->ListingCount != 0
};
```

| # | 位置 | 错误结论 | 后果 |
| ---- | ---- | ---- | ---- |
| 1 | `RetryLocalSearchIfStale()`「成功即结束补拉」 | 认为「数据已完整到达」 | 补拉立刻结束，新世界的搜索永远不会下发 |
| 2 | `GetLocalListingsDataSet()` 的 `IsGameMarketDataUsable()` | 认为「游戏数据可用」 | 以空列表收场，并把 `localListingsStale` 清掉（补拉随之取消） |

**设计修正**：引入跨服专用状态 `pendingWorldResyncSearch`（跨服后尚未为新世界发出搜索），并让两个判据都尊重它：

| 判据 | 修正前 | 修正后 |
| ---- | ---- | ---- |
| `RetryLocalSearchIfStale()` 的「成功即结束补拉」 | `IsFullyReceived(SelectedItemID)` 为真即结束 | 加前置 `!pendingWorldResyncSearch`：跨服后必须先真正下发一次搜索 |
| `GetLocalListingsDataSet()` 的 `gameDataUsable` | `IsGameMarketDataUsable(...)` | `IsGameMarketDataUsable(...) && !pendingWorldResyncSearch`：跨服进行中一律视为不可用（返回空列表并**保持**补拉，而不是取消它） |

**安全性**：标记在「搜索下发成功 / 补拉窗口结束 / 次数用尽 / 服务器拒绝 / 不再查看本服」全部清除，因此不会造成永久空白；每个跨服最多额外 1~2 次市场搜索请求（与第三十六/三十七轮「布告板可用即搜索 1 次」的既定策略一致）。

**可观测性**：跨服链路上新增始终输出（不受 `EnableDiagnostics` 影响）的 `[AutoBuyer][跨服] [列表] …` 三态日志 ——「已发起新世界搜索」/「新世界数据已就绪：重建 N 条」/「补拉失败（窗口结束·次数用尽·被拒）」。

**为什么上一轮没发现**：该缺陷自 1.1.0（第四十二/四十五轮前后的世界切换重构）即存在，但只有在「跨服时游戏侧列表被清空」这一组合下才暴露，且此前跨服日志不可见（第四十六轮才修好），因此长期无法定位。

### 第四十八轮：跨服渲染闸门（**已回滚**）与由此得到的结论（2026-09-24）

**尝试**：第四十七轮修好「补拉不下发」后，跨服瞬间仍会渲染一下上一服务器的挂单。当时判断是「跨界传送日志在落地帧才出现，所以过场中世界锚点仍是旧世界」，于是给渲染入口加了两条闸门：

```
gameDataUsable = IsGameMarketDataUsable(itemID)
              && !pendingWorldResyncSearch          // 跨服后尚未下发重搜索（第 47 轮，保留）
              && !IsPlayerTransitioning             // 过场中（本轮，已回滚）
              && IsWorldResyncGameDataTrusted(info) // 状态未推进则视为旧数据（本轮，已回滚）
```

**实机结果**：否决。用户反馈——

> 「这生成了一种新的闪过，跨服后落地时还未判定跨服，所以显示了一瞬间老数据，然后刷新列表，回滚一下吧，之前的比较容易接受」

**为什么会更糟**：闸门把「旧数据的连续显示」切成了 `旧数据 → 刷新中… →（落地但尚未判定的一瞬）旧数据 → 刷新中… → 新数据`，即把一次内容替换变成了多次闪烁。

**处置**：本轮改动全部回滚（含 `WORLD_RESYNC_TRUST_TIMEOUT_MS`、`IsWorldResyncGameDataTrusted`、`IsWorldResyncSearchAwaitingFreshData`、`worldResyncHideLogged` 与相应渲染分支）；回滚后产物与第四十七轮构建逐字节相同（SHA256 `a7dcaac21f6759d953f8e46044c9448b2da993cf9386a48241c22a6459f54544`，`AutoBuyer.dll` 161,280 B）。`pendingWorldResyncSearch` 及其自愈/日志（第四十七轮）保留。

**结论（后续设计原则）**：① 渲染期不要为了「数据属于谁」而反复遮断同一份数据 —— 遮断本身就会被看见；② 要消除「闪过旧服数据」，正确方向是让世界判定更早发生（不再依赖落地帧的日志），而不是在渲染期拦截；③ 「跨服时旧数据短暂显示」在当前实现下属于已接受行为，不作为缺陷处理。

### 第四十九轮：连续切换物品「选中回弹」+ 部分物品取不到同大区数据（2026-09-24）

#### A. 跟随游戏侧物品：从「时序假设」改为「确认制」

**问题**：连续快速切换物品后，选中物品会回弹到更早的那个物品。

**机理**：跟随逻辑（`UI.SyncItemWithGame`）原本只靠时序条件（选择保护期 4s、补拉中、连续观察 1.2s、最小间隔 3s）来躲开「游戏侧还没跟上我方请求」的窗口。但快速切换时，中间的搜索请求会被全局节流（800ms）与「上一次在途」（3s）跳过，游戏侧可能长时间停在较早的物品上 —— 时序条件一旦满足，跟随就把玩家的最新选择改回旧物品。

**设计变更**：引入确认制 ——

```
acknowledgedGameItem = (SelectedItemID, ItemEpoch)   // 仅当 infoProxy->SearchItemId == SelectedItemID 时写入
跟随前置条件：acknowledgedGameItem == (SelectedItemID, ItemEpoch)
```

含义：只有游戏侧确实显示过我方「当前这一次」所选物品，才允许把游戏侧的其它物品当成「玩家自己换的」。`ItemEpoch` 的引入使「A→B→A」这类回到同一物品的情形不会被旧的确认误判为已确认。时序条件全部保留（作为第二道保险）。

#### B. 跨世界行情：「已请求」不等于「已取到」

**问题**：部分物品后续取不到同大区（跨世界卡片）数据，且手动刷新也补不回来。

**机理**：`RequestAllWorldsData()` 用 `aggregatedRequestTicks[(物品, 世界)]` 既当进度游标（每轮最多 8 个世界，必须从上次中断处继续，否则排尾的世界永远拿不到数据）又当新鲜度判据（15 分钟 TTL）。它在发起请求时即写入，忽略结果 ⇒ 请求失败（429 / 5xx / 超时 / 404）也会让该世界被锁 15 分钟。`Reload()`（手动刷新）只清本地列表与派生缓存，不动该游标，因此用户没有恢复手段。

**修复**：失败时把游标改写为「已过期 15 分钟 − 30 秒」，即 `now - AGGREGATED_DATA_TTL_MS + 30s`：在 15 分钟 TTL 判定下，30 秒后该世界自动变为可请求（自愈）；当轮仍算「已处理」，不挤占每轮 8 个世界的配额；404 / 400（上游明确无此数据）保持正常 TTL；新增常开日志通道 `[AutoBuyer][数据]`。

**一般化教训**：「已请求过」不能当作「已有数据」；把两者混在同一个标记里，任何失败都会变成一段静默的空窗期。游标与新鲜度应当分开表达。

### 第五十轮：补拉状态的「单一管理者」原则（2026-09-24）

**问题**：连点两个物品后，第二个物品的数据再也取不回来（实机日志：`[GameState] 市场交易板数据请求被服务器拒绝`，与 Universalis 无关）。

**实测日志（2026-09-24 00:46~00:56）——决定性证据**：

```
[AutoBuyer] [GameState] 市场交易板数据请求被服务器拒绝，错误码：1879048195。
[AutoBuyer] [GameState] 市场交易板数据请求被服务器拒绝，错误码：1879048194。
（同期没有任何 [AutoBuyer][数据] 行）
```

**根因（三处叠加）**：

| # | 位置 | 问题 |
| ---- | ---- | ---- |
| 1 | `NotifyMarketRequestRejected()` | 被拒后把 `localListingsStale = false` —— 直接取消补拉；此后没有任何机制会再为该物品发起搜索 |
| 2 | `GetLocalListingsDataSet()` 的「状态推进即结束补拉」分支 | 被拒后游戏侧状态同样会变 ⇒ 界面也会把补拉清掉，与 #1 一起把最后的重试机会掐死 |
| 3 | `RetryLocalSearchIfStale()` 的尝试计数 | 冷却期内每次 tick 都会「尝试一次」并被冷却拦下，空转也消耗重试次数 ⇒ 冷却结束时预算已用尽 |
| 附加 | `LOCAL_SEARCH_MIN_INTERVAL_MS = 800` | 实测两次请求相隔 826ms 即被服务器拒绝 ⇒ 触发概率高（连点两个物品就会踩到） |

**修复**：① 被拒后保留待补拉状态（只进入静默冷却，并把重试次数 / 下次重试 / 窗口重排到冷却之后）；② 界面侧不再改写 `localListingsStale`（补拉状态只由补拉机制自己管理）；③ 「补拉完成」统一收敛到真正采纳数据的那一刻（指纹相同或重建成功时清除 `stale` 与重试计数）；④ 只有确实下发成功的请求才消耗重试次数（被冷却/节流/熔断/在途跳过均不计次）；⑤ 冷却期内不采信游戏侧数据（`IsMarketRejectionCoolingDown` 参与判定）；⑥ 全局最小间隔 `800ms → 2_000ms`（被拦下的请求不会丢，`SelectItem` 会标记待补拉，由 tick 在 2.5s 后重新下发）；⑦ 被拒时新增常开日志说明「静默 20 秒后自动重试，急用可点刷新立即重试」。

**行为预期**：被服务器拒绝后，插件在 20 秒冷却结束时自动重试（用户无需操作）；界面上方显示游戏原生的「请稍后再次确认」，列表保持「刷新中…」而不是永久空白。

**一般化教训**：一个状态位若被多处改写，任何一处对「失败」的语义理解偏差都会变成永久卡死。状态机应当有**唯一所有者**；其他模块只能读取它、并通过明确的接口请求状态迁移。

### 第五十一轮：`failedItems` 不是「没有数据」，而是「上游这次没算出来」（2026-09-24）

**反馈更正**（第五十轮实机）：取不到数据的不是下面的在售列表，而是上方的低价/高价世界卡片。

**根因**：Universalis 聚合接口 `GET /api/v2/aggregated/{world}/{itemIds}` 的响应模型是

```csharp
public sealed class UniversalisAggregatedMarketDataResponse
{
    public List<UniversalisAggregatedMarketResult> Results     { get; init; } = [];
    public List<uint>                              FailedItems { get; init; } = [];   // ← 上游「本次没算出来」的物品
}
```

上游对某些（物品 × 世界）组合会返回「`results` 为空 + 该物品出现在 `failedItems`」—— 这是本次失败，不是「该世界没有这个物品」。而旧代码在这一步是静默返回：

```csharp
if (data.Results.All(x => x.ItemID != itemID))
    return;                       // ← 什么都不存、什么都不记，直接放弃
```

但由于更早一步已经写入 `aggregatedRequestTicks[(物品, 世界)] = 现在`，该世界的价格于是被锁死 15 分钟：不重发请求、卡片缺数据、且一条日志都没有（与实机日志中「有 `[GameState]` 拒绝、却没有任何 `[AutoBuyer][数据]` 行」完全吻合）。这同时解释了「为什么只有部分物品 / 部分后续取不到」：取决于哪些（物品 × 世界）组合在上游失手。

**修复**：① 「`results` 为空 / 物品出现在 `failedItems`」按失败处理，沿用第四十九轮的「推迟过期」机制，30 秒后自动重试；② 新增常开日志说明是 `failedItems` 还是空结果；③ 新增卡片链路三条常开可观测性日志（每个物品最多各一条）：

```
[世界行情] item=… 本轮发起 N 个世界的请求（M 个数据中心；K 个世界在 15 分钟有效期内被跳过）
[价格卡片] item=… 重建：有效世界 x/y（聚合缓存 a 个世界，服务器价缓存 b 个世界）
[价格卡片] item=… 数据已就绪：有效世界 x（聚合缓存 a 个世界）
```

**一般化教训**：接入第三方 API 时，必须把「接口级失败（`failedItems` / 部分成功 / 空结果）」与「业务级无数据（该世界确实没有该物品）」区分开 —— 两者的正确后续动作完全相反（重试 vs 不重试），把它们混为一谈会造成一段无从诊断的静默空窗期。

### 第五十二轮：缓存「已请求」标记必须以「真的持有数据」为前提（2026-09-24）

**问题**：连点多个物品后，前面几个物品的卡片永远取不到数据（等多久都不行）。

**实测日志（2026-09-24 01:12，靠第五十一轮新增的卡片可观测性日志才看清）**：

```
01:12:03.180 [世界行情] item=14 本轮发起 8 个世界的请求（1 个数据中心；0 个世界在 15 分钟有效期内被跳过）
01:12:03.237 [价格卡片] item=14 重建：有效世界 0/28（聚合缓存 0 个世界，服务器价缓存 0 个世界）
…（item=15/16/17/18 同样：请求已发出，但有效世界 0、聚合缓存 0）
01:12:04.821 [ERR] Universalis 请求失败: UniversalisMarketDataResponse
01:12:05.808 [价格卡片] item=19 数据已就绪：有效世界 8（聚合缓存 8 个世界）      ← 最后点的那个正常
01:12:14.414 [世界行情] item=17 本轮发起 0 个世界的请求（8 个世界在 15 分钟有效期内被跳过）
01:12:15.008 [价格卡片] item=17 数据已就绪：有效世界 1（聚合缓存 0 个世界）      ← 再回来时被锁死，只剩本服
```

**根因（链式）**：

| # | 环节 | 说明 |
| ---- | ---- | ---- |
| 1 | `SelectItem` → `ClearAllData()` | 会 `DisposeAllSubscriptions()` 并清空订阅表 —— 连同其它物品正在进行的订阅一起摘除 |
| 2 | 连点 14→15→…→19 | 每个物品的请求刚发出（`GetOrRequest`），订阅就被下一次点击 Dispose 掉 |
| 3 | 响应晚到 | 回调已被摘除（且物品纪元已变）→ 无人把数据写进 `onlineAggregatedCache` |
| 4 | `aggregatedRequestTicks[(物品, 世界)]` | 在请求时就写入，且 `ClearAllData` 不清它 → 该世界被视为「15 分钟内已取过」 |
| 5 | 回到这些物品 | 8 个世界全部被跳过 → 不重取、不重订阅 → 卡片只剩本服一个世界，等多久都不会好 |

即：第五十一轮修掉了「上游 `failedItems` 被当成已取过」，但「请求发出、响应无人接收」同样被当成已取过 —— 必须让「已取过」以真的持有数据为前提。

**修复**：① 新增 `IsAggregatedWorldFresh(key, itemID, worldName)`，把「本轮无需处理」的判据改为：已持有数据（`onlineAggregatedCache`）→ 15 分钟 TTL 内不重取；未持有数据但上游缓存里已有含该物品的响应（＝只是错过了回调）→ 不等待，立即重新订阅采纳（`Observe` 会立刻回调当前快照，零网络开销）；其余（真失败 / `failedItems` / 仍在途）→ 最多等 30 秒重试。② 选中世界的挂牌数据同理：未持有 `onlineDataCache` 时，即便订阅表里已有条目也重新订阅（先 Dispose 旧订阅再注册）。③ 保留第五十一轮的 `failedItems` 处理与三段常开可观测性日志。

**一般化教训**：缓存的「新鲜度」永远应当由数据本身判定，而不是由「什么时候请求过」推断 —— 请求与数据之间隔着网络、回调注册、纪元校验等多道可能失败或失效的环节。凡是用「已请求时间戳」当新鲜度的缓存，都必须再问一句：**我现在真的拿着这份数据吗？**

实机验证：第四十六 ~ 五十二轮逐轮实机复测（含跨服检测日志、列表自动更新、卡片数据补齐、连点六个物品）；用户确认后指示发布。

---

## 1.1.3（2026-10-01）

**本次变更总览**（第五十三 ~ 五十八轮，六轮）：

1. **跨服错价两处根治**：跨服后「把上一服务器的价格当成当前价格」——第五十三轮修掉「旧世界读数被当成新世界数据」与「卡片重建被 3 秒节流压住」；第五十八轮进一步用**挂单 ID 记忆**拦住「客户端把上一服务器挂牌重新显示回来」那一段（实机三态日志已证实）。
2. **按用户指令删除未授权功能与全部死代码**（第五十四 / 五十六轮）：右键购买菜单、世界卡片悬浮详情残留、历史成交整套管线、`accessTime` / 收藏 `Note` 等，以及内嵌库中 20 个无人引用的服务与大批零引用文件。
3. **隔离与自保**（第五十五轮）：服务白名单（只有显式列出的 12 个服务会被实例化）、加载自检两行 `[AutoBuyer][自检]`、`lib/FROZEN.md` 冻结策略与改动索引。
4. **持续削薄与降噪**（第五十六 ~ 五十七轮）：移除 `AngleSharp`（1 MB）/ `TimeAgo.Core` / `GuerrillaNtp` 依赖、清理 `NotifyHelper` 的 TTS 区与 16 条未使用全局别名、清掉「初始化任务 10 秒后被放弃」的噪音。

**规模变化**：仓库 C# `39,026 行 → 28,294 行`（自 1.1.2 起 **+394 / −30,779**，整文件删除 **262 个**）；发布包 **2.26 MB → 902 KB**；运行时依赖闭包 **7 项 → 4 项**（AutoBuyer / OmenTools / DailyRoutines.Common / TinyPinyin）。

**提交** `73ae5bd` — *fix: 1.1.3 跨服旧世界读数拦截、移除未授权功能与死代码清理、服务白名单隔离*（288 文件：版本号 + 插件源码/词条/README + 内嵌库；**+479 / −30,785**）
**版本号** 清单 `1.1.2.0` → **`1.1.3.0`**（修复位：z 动、a 归零；对外显示 `1.1.3`）
**Release** <https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/tag/1.1.3>（tag `1.1.3`）
**附件** `latest.zip` **400,520 B**（SHA256 `8fbb8a8584d48ce910ec451cbf5dc64abca3370283d7cb0dc196a50f509a16d2`），内含 `AutoBuyer.dll` **149,504 B**（SHA256 `c53630c8105278a698e8ff8435fcbf3f0c123f25e542c4e9c2e0ccab86a03fc7`）
　　· 下载：<https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/download/1.1.3/latest.zip>　· GitHub 侧资产 digest 已核对一致（`sha256:8fbb8a85…`），正文 1519 字符与本地逐字一致
**推送方式说明**：本机 SSH（22 端口）被网络环境拦截，本次改用 HTTPS + Token（Token 经环境变量交给 git credential helper，未出现在命令行、未写入 `.git/config`、未打印）
**测试** 见 `测试文档.md`（2026-10-01 重跑：构建 0/0、依赖闭包 4 项、词条 51/51 无缺失无冗余、Universalis 契约 32 项断言全 PASS、服务白名单 12=12、跨服修复实机实证）

### 第五十三轮：跨服后顶部卡片把上一服务器的价格当成当前价格（2026-09-30）

**需求**（`工作.txt`，2026-09-30）：

| # | 用户原话 | 落点 |
| ---- | ---- | ---- |
| 1 | 「跨服后会有几秒，把上一服务器的价格当成当前价格，导致顶部卡片变动，几秒后恢复」 | 本轮根部修复（见下） |
| 2 | 「考虑，检测到跨服时先删除缓存列表数据再获取」 | `InvalidateWorldData()` 增加「先删除缓存列表数据」；采纳该方向 |
| 3 | 「把那个申请跨服的提醒去掉，以后没让你加的功能别乱加」 | 移除 `NotificationSuccess`（词条 `BetterMarketBoard-Travel-Requested` 一并删除）；失败 / 繁忙仍提示 |

**根因（两条并存，缺一不可）**：

| # | 环节 | 说明 |
| ---- | ---- | ---- |
| 1 | **旧世界的读数被判成「新世界的数据」** | `IsGameMarketDataUsable()` 只靠 `IsFullyReceived()`，而它对 `EntryCount == 0`（空数据）返回 **true**（第四十七轮已记录）。跨服瞬间游戏侧可能仍持有上一服务器的挂牌，或 `InvalidateWorldData → ClearListData()` 刚把列表清空 —— 两者都被判为「可用」。于是 `GetWorldPriceRanks()` 第 1 段（当前世界取游戏实时数据）读到旧世界挂牌，把最低价写进 `gameMinPriceCache[(物品, **新**世界, HQ)]`，之后半小时内都优先于 Universalis 显示。与第二十四轮同一机理，当时的护栏没覆盖这条路径。 |
| 2 | **作废不彻底 + 世界判定滞后 + 重建被节流** | ① `InvalidateWorldData()` 未清 `gameMinPriceCache` 与派生缓存；② 世界锚点的权威信号是「落地后才出现」的跨界传送日志，日志迟到期间插件仍以为在旧世界 ⇒ 顶部卡片的「本服」卡继续显示上一服务器的价格；③ `GetWorldPriceRanks()` 的重建受 3 秒 `Throttler` 限制 ⇒ 旧排名 / 空白再多停留数秒。三者叠加即用户看到的「几秒」。 |

**修复**：

| # | 措施 | 位置 |
| ---- | ---- | ---- |
| 1 | 跨服时**先删除缓存列表数据**：`InvalidateWorldData()` 额外执行 `ClearDerivedCaches()` + `gameMinPriceCache.Clear()` | `MarketBoard.Data.cs` |
| 2 | `IsGameMarketDataUsable()` 增加前置 `!pendingWorldResyncSearch`：跨服后尚未为新世界真正下发搜索之前，游戏侧读数一律不认（既不采纳、也不会被写进缓存） | `MarketBoard.Data.cs` |
| 3 | 新增 `forcePriceTableRebuild`：跨服 / `ClearAllData()`（切换物品、手动刷新）后价格表**立刻**重建一次，不受 3 秒节流限制 | `MarketBoard.Data.cs` |
| 4 | 世界锚点每秒兜底巡检：`SyncWorldWithPlayerWorld(reason, logWhenUnchanged)`（纯本地读取，不发起任何请求）。开窗那一次仍**始终**记录结论；每秒巡检只在**发现不一致**时写日志，避免每秒刷屏 | `MarketBoardModule.cs` |
| 5 | `HandleWorldChange()` 对「同一世界」的重复触发直接忽略（每秒巡检常先于日志完成判定，否则日志到达时会再作废、再补拉一次） | `MarketBoardModule.cs` |
| 6 | 移除「已请求跨服传送」弹窗（改为一行诊断日志），并删除词条 `BetterMarketBoard-Travel-Requested` | `MarketBoardModule.cs` + `Localization/zh-CN.json` |

**为什么不重复第四十八轮的弯路**：A7（第四十八轮）是在**渲染期**遮断数据（`!IsPlayerTransitioning` + 状态机信任判定），导致「旧 → 刷新中 → 旧 → 刷新中 → 新」的多次闪烁而被实机否决。本轮一律不动渲染条件，只做两件事：**让世界判定更早发生**（每秒巡检兜底）与**把旧数据作废得更彻底**（清缓存 + 不采信未重取的游戏读数）。检测是单调的（锚点一旦切到新世界不会再切回），因此过程是「旧价格 →（≤1 tick）作废 → 新价格」的一次替换。

**待实机验证（测试阶段）**：跨服后顶部卡片是否还会出现上一服务器的价格；`/xllog` 过滤 `[AutoBuyer][跨服]` 应能看到 `[老路·每秒巡检] … → 不一致，按跨服处理`（日志先到则为 `[新路·日志判定]` + `[世界切换]`）。

**一般化教训（与第四十七/五十二轮同源）**：「空」与「已就绪」必须分开表达 —— 只要有一个判据把「没有数据」当成「数据完整」，跨服这种「先清空、后重取」的流程就一定会被它短路。任何跨服后仍被读取的缓存，都必须在跨服那一刻显式作废，而不是靠 TTL 自然过期。

### 第五十四轮：删除未授权功能、清理死代码（2026-10-01）

**需求**（`工作.txt` 同轮追加，用户原话）：

| # | 原话 | 落点 |
| ---- | ---- | ---- |
| 1 | 「A1删掉」 | 删除 `StandardTimeManager`（插件加载即请求腾讯 checktime + NTP） |
| 2 | 「A2A3检查没用上的都删掉」 | 删除 20 个未被引用的 OmenTools 服务及其连带死代码 |
| 3 | 「C1删除」 | 删除列表行右键购买菜单 |
| 4 | 「E清理掉」 | 删除历史成交管线等全部「无调用者」代码与冗余词条 |
| 5 | 「此次目的为：1，去除不需要的功能。2，精简代码，清理遗留无效内容」 | 本轮总纲 |

#### A1：插件加载即外发网络请求（两处）

| 位置 | 事实 | 处置 |
| ---- | ---- | ---- |
| `OmenTools` `StandardTimeManager` | `Init()` 里 fire-and-forget：明文 HTTP `http://vv.video.qq.com/checktime` → NTP `ntp.ntsc.ac.cn`（失败退 `pool.ntp.org`）。它同时是 `LRUCache` 的**硬依赖**（`ItemSourceManager.hotCache` → 插件 NPC 收购价链路必经），因此**不能直接 `Disable`** | §14.3 D4 `LRUCache` 6 处改 `DateTime.UtcNow`；D5 `DatePicker` / `ImageHelper` 解除引用；D6 删除服务文件 + 移除 `GuerrillaNtp` 包 |
| `DailyRoutines.Common` `ModuleInfo` | **静态构造**里 `Task.Run` 请求 `gh.atmoomen.top/.../AuthorSupportLinks.json`；插件 `MarketBoardModule.Info` 必然触发该类型，而唯一消费者 `ModuleSearcher.cs` 本就被本工程裁剪 | §14.3 D8 整块删除（静态构造 + `AuthorSupportLinks` + `SupportUrls` + `LinkInfo`） |

#### A2/A3：删除未被引用的 OmenTools 服务（141 文件 / 约 17,500 行）

`DService.DiscoverEnabledServiceTypes()` 会自动实例化并 `Init` **所有**未被 `Disable` 的 `OmenServiceBase` 子类。经逐文件引用核查（插件 + `DailyRoutines.Common` + 库内其它代码），下列服务在可达路径上**没有任何调用点**，却各自挂 Hook / 起后台任务 / 做反射扫描：

| 服务 | 连带删除 |
| ---- | ---- |
| `StandardTimeManager` | （见 A1） |
| `ContextMenuManager` | `Managers/ContextMenu/**`（9 文件）——加载即 Hook 游戏 `OpenAddon` / `FireCallback` 重建右键菜单，而插件自第二十/二十二轮起不注册任何菜单项 |
| `ZoneIndicatorRenderer` | `Helpers/ZoneIndicator/**`（8 文件）——100ms tick + `WindowManager.PostDraw` |
| `ExecuteCommandManager` | `Interop/Game/ExecuteCommand/**`（74 文件）+ `ExecuteCommandFlag` / `ExecuteCommandComplexFlag` 枚举 |
| `GamePacketManager` | `Info/Game/Packets/**`（20 文件） |
| `AchievementManager`、`AetheryteRecordManager`（+`Info/Game/AetheryteRecord/**`）、`AtkEventManager`、`CharacterStatusManager`、`ControllerManager`、`DataShareManager`、`IPCManager`、`InputIDManager`、`LinkPayloadManager`、`UseActionManager`、`InstancesManager`、`GameResourceManager`、`TargetManager`、`ChatManager`、`ImageHelper` | `ContentsFinderHelper`、`MarkingControllerExtension`、`ImGuiMarkdownRenderer`、`JobSelectCombo`；`GameObjectExtension.TargetInteract()`、`LocalPlayerState.SwitchGearset(...)`（其唯一引用者） |

**为什么删文件而不是 `Disable<T>()`**：`Disable` 只挡初始化、代码仍在；这些服务既然无人引用，留着只是死重。库内现只剩 13 个服务类（其中 `TooltipManager` 已由插件显式 `Disable`），全部都有可达引用。
**IPC 澄清**：插件的 `[IPCProvider]` 由 `DailyRoutines.Common` 的 `IPCAttributeRegistry.RegObjectIPCs` 注册，**不经过** `IPCManager`，因此删除它不影响插件 IPC。

#### C1：删除列表行右键购买菜单

列表行「总价」单元格原为 `ImGui.Selectable` + `ContextPopupItem`（单价 / 数量 / 总价 + 「购买」）。现改为纯文本，不再承载任何交互；`SendBuyRequest` 仍被按量购买流程使用，未受影响。

#### E：清理「无调用者」代码

| 类别 | 删除内容 |
| ---- | ---- |
| 卡片 | `DrawWorldPriceTooltip`（第二十二轮起已无调用者）及其贯穿 `DrawPriceOverviewTable` / `DrawOverviewPriceCard` / `WorldPriceCardComponent` 的 `currentWorldPrice` 参数管线、`WorldPriceRanks.Current` |
| 历史成交 | `HistoryEntry` / `HistoryDataSet` / `ToHistoryEntry` / `GetHistoryDataSet` / `BuildHistoryDataSet`、`historyDataCache` / `onlineHistoryCache` / `historySubscriptionCache` / `onlineHistoryVersion` 及其清理与订阅代码、`RemoteUniversalisHistory.cs` 整文件、`UniversalisModels.cs` 的历史成交区与 `UniversalisMarketHistoryRequestParams`、`UniversalisMarketItemData.RecentHistory` |
| 聚合统计 | `GetItemAggregatedStats`、`GetSelectedWorldMinPrice`、`GetRegionMinPrice`、`GetAggregatedResponse` |
| 物品卡 | `accessTime` 参数与其角标分支；收藏项 `FavoriteItems.Note` 字段、`RenderItemCard(note: …)` 参数及其 ✎ 角标 |
| 词条 | `RightClick`、`BetterMarketBoard-TravelToWorld`、`BetterMarketBoard-Tooltip-CurrentWorld`、`BetterMarketBoard-Tooltip-DailySales`、`BetterMarketBoard-DailySales-Format`、`BetterMarketBoard-RecentPurchase`、`BetterMarketBoard-Travel-NoLifestream`、`BetterMarketBoard-Travel-Requested`（第五十三轮）→ 词条 59 → **51**，且**无冗余** |
| 文案 | 插件描述由移植残留的「添加 `/pdr {0}` 指令…并设定价格监控」改为实际能力描述（`/xlplugins` 中可见） |
| 依赖 | `TimeAgo.Core`（Common）、`GuerrillaNtp`（OmenTools）→ 运行时依赖闭包 7 项 → **5 项** |

#### 验证与边界

- **静态验证（已完成）**：`dotnet build FFXIV-AutoBuyer.slnx -c Release` → 0 错误 / 0 警告；对被删符号做全仓库 grep 残留自检 → 0 命中；词条 51 条且与代码引用一一对应；`AutoBuyer.deps.json` 已不含 `TimeAgo.Core` / `GuerrillaNtp`。
- **待实机验证（测试阶段）**：插件加载/运行一切正常（尤其 `/market`、购买、跨服、收藏、NPC 收购价基准行）—— 等价于验证「删掉的都确实没在用」。
- **构建产物注意**：`E:\Code\Output` 下的 `TimeAgo.Core.dll` / `GuerrillaNtp.dll` 是旧构建残留（`deps.json` 已不引用），测试阶段做一次清理后重建即可。
- **同步上游的代价**：本轮动的是内嵌库源码（删文件 + 改 `LRUCache` / 两个 `csproj`），日后同步上游 OmenTools / DailyRoutines.Common 需重新套用，已登记在 `设计文档.md` §14.3（D4~D9）。
- **尚未核查的同类候选**（留给下一轮取舍，本轮未动）：`TeleportCostCalculator`、`LodestoneSearcher`、`ImGuiOm` 中未被使用的部件（`Combos` / `MapRenderer` / `DatePicker` 等）、`Interop/Game/Helpers` 的其余部分。

### 第五十五轮：隔离与自保（服务白名单 + 加载自检 + 冻结说明）（2026-10-01）

**需求来源**（用户原话）：

> 「我希望的是能保证独立安稳运行，omen作者经常搞一些莫名其妙的力大砖飞外挂级插件，我希望能分隔开放置被意外影响」
> 「这个插件只是给我自己用不考虑许可之类的问题」→「行，可以」（批准「冻结 + 白名单 + 削薄 + 自保」方案与顺序）

**先把风险拆清楚**（这一步决定了「不需要重写」）：

| 路径 | 是否存在 | 说明 |
| ---- | ---- | ---- |
| ① 上游更新自动进来 | **不存在** | 两个库是源码内嵌的冻结副本，没有任何自动更新机制；不同步就永远不变 |
| ② 作者的其他插件在**同一个游戏进程**里抢资源 | 存在 | 它们各自带各自的 OmenTools 副本，Hook 同一批游戏函数 —— **重写本插件也消除不了**，只能靠「不共享、不抢、可观测」 |
| ③ 本副本里有「会自动干活」的代码 | 存在但已很小 | 第五十四轮删掉 20 个服务后只剩 13 个，本轮收敛为 12 个 |

结论：**不重写**（重写 5,000–8,000 行只换来「文件更干净」，对「不被影响」没有额外收益），改走「冻结 + 白名单 + 削薄 + 自保」，每步都能停在可发布状态。

**本轮实施**：

| # | 措施 | 位置 |
| ---- | ---- | ---- |
| 1 | **服务白名单**：`ENABLED_SERVICE_TYPES` 显式列出允许实例化的 12 个服务；`DiscoverEnabledServiceTypes()` 改为「只实例化 白名单 ∩ 未被 `Disable`」；白名单外的服务**不实例化**（⇒ 不挂 Hook、不起 tick、不发请求）并在加载时列名告警 | `lib/OmenTools/DService.cs` |
| 2 | 白名单只读出口 `InitializedServiceTypes` / `SkippedServiceTypes`（供自检输出） | 同上 |
| 3 | **加载自检两行**：`[AutoBuyer][自检] 已启用服务 N 个（白名单）：…` ＋ `[AutoBuyer][自检] 本方公共面：命令=/market；IPC=…；出网=universalis.app；无 Addon/右键菜单/Tooltip/封包/输入 Hook` | `Plugin.cs` |
| 4 | 「外部来源写市场板」改为**常开可观测**：`SyncItemWithGame` 的「跟随游戏侧物品」由诊断日志改为 `[AutoBuyer][数据] [市场板] …`（只有「我方从未请求过、游戏侧却变了」才会出现，且已有 4 秒保护 + 1.2 秒确认 + 冷却，不会刷屏） | `MarketBoard.UI.cs` |
| 5 | 冻结策略文档：来源与许可、**永不同步上游**、改动登记约定（§14.3）、白名单约定、当前生效服务清单与改动索引 | 新增 `lib/FROZEN.md` |
| 6 | 删除**库内最后一个无人引用的服务** `TooltipManager`（+枚举/修改器共 8 文件）；`Plugin.cs` 随之回到 `DService.Init(pluginInterface)` | `lib/OmenTools/.../Managers/Tooltip/**`、`Plugin.cs`。删掉后「白名单外服务数 = 0」，于是 `[OmenTools][服务白名单] 已跳过…` 那行只在**真的新增了服务文件**时出现，成为干净可靠的告警 |

**没有做、以及为什么**（避免被误解为遗漏）：

- **没做「检测到外部干扰就自动降级为只读」**：能观察到的只有「游戏侧搜索目标被别人改了」，而这与「玩家在游戏原生布告板里自己改物品」无法区分 —— 自动降级一旦误判，会直接把插件的搜索/刷新掐掉，比干扰本身更糟。因此本轮只做**可观测**（第 4 条），不改变行为；若日后确认需要激进模式再单独评估。
- **没有重写内嵌库**：理由见上表 ①②③。

**待实机验证（测试阶段）**：

1. 加载后 `/xllog` 过滤 `[AutoBuyer][自检]`：应看到 12 个服务（不含 `TooltipManager`）；**正常情况不应**出现 `[OmenTools][服务白名单] 已跳过…` 一行（出现即代表库里多出了服务文件）。
2. 功能回归照旧：`/market`、按量购买、跨服自动更新、收藏、NPC 收购价基准行。

**同轮踩坑：改动在游戏里完全看不到（自检日志缺失）**

- **现象**：用户实机没有看到 `[AutoBuyer][自检]` 任何一行，怀疑代码没生效。
- **取证**：直接对 DLL 做字节级检索（注意：`[Text.Encoding]::Unicode.GetString` 按文件起点解码会因字节错位而漏匹配，必须用 ISO-8859-1 中转后搜 UTF-16 字节序列）：
  | 文件 | 含「自检」 | 含「服务白名单」 |
  | ---- | ---- | ---- |
  | `E:\Code\Output\FFXIV-AutoBuyer\x64\Release\AutoBuyer.dll`（本轮构建） | ✅ | — |
  | `E:\Code\Output\_lib\OmenTools\x64\Release\OmenTools.dll`（本轮构建） | — | ✅ |
  | `E:\Code\Output\FFXIV-AutoBuyer\Release\AutoBuyer.dll`（**游戏实际加载**） | ❌ | ❌ |
- **根因**：`FFXIV-AutoBuyer.slnx` 把三个工程都映射到 `Platform=x64`，所以 `dotnet build …slnx -c Release` 输出到 `…\x64\Release\`；而 Dalamud 的 dev plugin 注册路径（`dalamudConfig.json` 的 `DevPluginSettings`）是 `…\FFXIV-AutoBuyer\Release\AutoBuyer.dll`，**不带 `x64`**。两边不是同一个目录 ⇒ 游戏一直跑 9/30 的旧副本。
- **处置**：改用**工程级**构建 `dotnet build FFXIV-AutoBuyer\FFXIV-AutoBuyer.csproj -c Release`，产物直接落到被加载的目录（已验证：部署后 `AutoBuyer.dll` 含「自检」、`OmenTools.dll` 含「服务白名单」、`deps.json` 已不含 `TimeAgo` / `GuerrillaNtp`）。构建命令的两种口径与坑已写入 `设计文档.md` §14.2.4。
- **教训（已入附录 B）**：**"我改好了"必须包含"游戏加载的是我刚构建的那份文件"** —— 否则一切实机验证都无效；验证前先比对被加载文件的时间戳，或对关键字符串做二进制检索。

**下一步**：② 继续削薄 —— 未使用的 `I*` 抽象层（34 文件 / 1,438 行）、`LodestoneSearcher` + `AngleSharp`（发布包 −1 MB）、`FuzzyMatcher`、`TeleportCostCalculator`、未使用的 `ImGuiOm` 部件与 Info 表；预期发布包 **2.26 MB → 约 1.2 MB**。

### 第五十六轮：继续削薄（零引用批量清理 + 依赖收敛）（2026-10-01）

**需求**：用户对第五十五轮方案的「② 继续削薄」放行（原话「继续」）。

**做法（三步判定，编译器兜底）**：

| 步 | 手段 | 说明 |
| ---- | ---- | ---- |
| 1 | **类型名全库扫描** | 对 `lib/**` 与插件侧全部参与编译的 `.cs`，提取每个文件声明的类型名，检查是否在**其它**文件里出现过；零命中即候选 |
| 2 | **扩展类补做「方法名级」复核** | 扩展方法调用不会写出类名（`.Foo()` 而非 `XExtension.Foo()`），故对 `Extensions/**` 这类文件再核一遍方法名是否被调用 |
| 3 | **编译器兜底** | 每批删完立即构建；报错就把该文件 `git checkout` 恢复 |

**本轮删掉的（约 112 文件 / 约 10,000 行）**：

| 类别 | 内容 |
| ---- | ---- |
| 数据/DTO/表 | `Info/DTOs/**`（GitHub / Lalachievements / RisingStone）、`Info/Lumina/ExtraSheets/**`、`Info/Game/OccultCrescent/**`、`EorzeaDate`、未用 Data 表（`Positions`/`FateAchievements`/`XIVChatTypes`/`Inventories`/`FateIcons`）、`Info/Models/Polygon` |
| 解析器/转换器 | 未用词条解析器（JavaProperties / JavaXmlProperties / Resx / StructuredJson / KeyValueJson / ParserHelper）、未用 Json 转换器（`StringNumber`/`DateTime`） |
| OmenTools 自带 IPC | `Dalamud/IPC/Providers/**`（vnavmesh / BossMod / Raphael / EdgeTTS）——`IPCManager` 已在第五十四轮删除 |
| Interop | `Interop/Game/{AddonEvent,AgentEvent}/**`、`RaycastHelper`、`GameViewHelper`、`RotationHelper`、`TeleportCostCalculator`、部分 `Models/Native`、`MovementInputController`、`MemoryPatchWithPointer`、`Interop/Runtime/StructMarshaller`、`Interop/Windows` 三个帮助类 |
| 工具 | `Utils/LodestoneSearcher`、`Utils/FuzzyMatcher/**`（只被已排除编译的 `ModuleSearcher.cs` 使用） |
| ImGui 控件 | `ImGuiOm/Widgets/Combos/**`（10 文件）、`MapRenderer/**`、`DatePicker` |
| 扩展方法 | 13 个「方法名级零调用」的扩展（`StringExtension`、`DalamudPluginInterfaceExtension`、`EventFrameworkExtension`、`TaskExtension`、`TimeExtension`、`UIModuleExtension`、`BitMapFontIconExtension`、`AtkUldManagerExtension`、`AgentMapExtension`、`AtkEventDataExtension`、`ActionManagerExtension`、`AtkStageExtension` …） |
| 依赖/代码 | `AngleSharp` 包（D15）、`NotifyHelper` 的 `#region TTS`（D16）、插件 16 条未使用 `global using I*` 别名（D17）、Common 侧未用抽象（`IRemoteQueryHandler`/`IRemoteCommandHandler`/`RemoteInteractionBase`/`WorldRegionResolver`/`PlayerInfo`/`ConflictKey`/`ManagerInitOrderAttribute`/`ManagerConfigExtension`） |

**两次误判（都是「静态零引用」的盲区，值得记住）**：

| # | 文件 | 被谁用 | 教训 |
| ---- | ---- | ---- | ---- |
| 1 | `Interop/Game/Models/NodeState.cs` | `AtkComponentExtension` / `AtkResNodeExtension`（这两个文件本身靠彼此与其它文件互相引用而「看起来有人用」） | 互相引用的死代码会彼此续命，单看「谁引用了谁」会漏 |
| 2 | `Extensions/IConditionExtension.cs` | 插件 `MarketBoard.Utils.cs:197` 的 `Condition.IsBetweenAreas` | **属性式扩展**（`public bool IsX => …`）没有方法名可查，方法级复核对它无效 → 只能靠编译器 |

**成果（与削薄前对比）**：

| 指标 | 削薄前（第五十四轮统计） | 现在 |
| ---- | ---- | ---- |
| 仓库 C# | 372 文件 / 39,026 行 | **252 文件 / 28,294 行** |
| 其中内嵌库 | 349 文件 / 33,676 行 | **229 文件 / 22,923 行** |
| 部署目录体积 | ≈2.26 MB | **≈900 KB**（−60%） |
| `OmenTools.dll` | 1,377 KB | **650 KB** |
| 运行时依赖闭包 | 7 项（含 `AngleSharp` / `TimeAgo.Core` / `GuerrillaNtp`） | **4 项**（`AutoBuyer`、`OmenTools`、`DailyRoutines.Common`、`TinyPinyin`） |
| 自 1.1.2 起累计 | — | **+248 / −30,676 行，整文件删除 261 个** |

**待实机验证（测试阶段）**：功能回归（`/market`、按量购买、跨服自动更新、收藏、NPC 收购价基准行、左栏拼音/名称搜索），以及加载自检两行仍正常输出。

### 第五十七轮：清掉「初始化任务 10 秒后被放弃」的噪音（2026-10-01）

**现象**：每次加载插件约 10 秒后，日志出现

```
[VRB] [AutoBuyer] 放弃了所有任务 (原因: 任务 (无名称) 执行时间过长)
```

**根因**：`MarketBoardModule.Init()` 里 Enqueue 了一个「等布告板窗口打开后拉 Universalis 世界目录」的初始化任务，窗口没开时它一直返回 `false` 轮询等待（`TaskIntervalMS = 500`），于是必然撞上 TaskHelper 的超时保护 → 10 秒后整批放弃。功能上无害（每秒 tick 里的 `EnsureWorldCatalog()` 会补），但每次加载都白跑 10 秒、并留下一条看着像故障的日志。

**处置**：整块删除该任务（第五十七轮）。理由：它的能力与 `EnsureWorldCatalog()` **完全重叠** —— 后者同样是「只在窗口打开时跑（`OnWorldWatch` 的 `isOverlayOpen` 闸门）、目录未就绪就发起请求、就绪且与本地不同才重建 + `AnchorRegion` + `MarkPriceTableDirty` + 保存配置」，因此删掉后目录获取仍有且只有这一条路径（已在 `EnsureWorldCatalog` 注释里写明）。`TaskHelper` 本身保留，购买流程（`MarketBoard.Purchase.cs`）仍在用。

**验证（实机，无需额外操作）**：构建产物被 Dalamud 自动重载后（02:46:33），

- 最后一次「放弃了所有任务」停留在 **02:41:50**（本轮修复前的那次加载）；
- 02:46:33 之后超过 10 秒仍**没有**新的放弃日志 → 修复生效；
- 同次加载的两行 `[AutoBuyer][自检]`（12 个服务）照常输出。

### 第五十八轮：跨服后「列表加载完仍有一两秒错价」——旧世界挂牌被认成新世界数据（2026-10-01）

**现象**（用户原话）：

> 「跨服后，加载完商品列表，有一两秒，会把当前服务器卡片挂上之前服务器的价格」

**为什么第五十三轮没堵住**：第五十三轮加的护栏是 `!pendingWorldResyncSearch`，它只在「跨服判定 → 我方为新世界下发搜索」这一段为真。而实际过程是：

```
跨服落地 → 判定并作废 → 我方下发搜索（pendingWorldResyncSearch 变 false）
   → 玩家在新世界首次请求市场数据、列表重新加载
   → 客户端把**上一服务器**的挂牌重新显示出来   ← 这一刻 SearchItemId 一致、IsFullyReceived() 也为真
   → 价格表重建读到它 → 写进 cachedDCWorldPrices 与 gameMinPriceCache[(物品, 新世界)]
   → 新世界真正的响应到达后，卡片要等 3 秒节流的下一次重建才更新（≈ 用户看到的 1~2 秒）
```

**处置（三点）**：

| # | 措施 | 说明 |
| ---- | ---- | ---- |
| 1 | **挂单 ID 记忆**（`worldResyncStaleListingIDs`）：跨服时（`ClearListData` **之前**）记下游戏侧当前持有的挂单 ID；在 `IsGameMarketDataUsable()` 里增加一道判定 —— 当前挂牌的挂单 ID 若**全部**落在这份记忆里，就认定是旧世界残留、不采信；出现任一新挂单 ID（新世界的挂单不可能与旧世界重合）立即恢复采信 | 兜底：若跨服落地时游戏侧已被清空（读不到挂单），则用「最近一次采纳过的本地列表」的挂单 ID 兜底，覆盖「客户端把上次读到的列表又显示回来」的情形 |
| 2 | **采纳即重建卡片**：新世界挂牌被采纳的那一刻置 `forcePriceTableRebuild`，卡片立刻重建，不再等 3 秒节流 | 直接消掉「列表已加载完、卡片还停在一两秒前」的那段 |
| 3 | 观测性：新增两条常开 `[AutoBuyer][跨服] [列表] …`（已记忆旧世界挂牌 N 条 / 仍是上一服务器挂牌不予采信 / 已换新恢复采信），其中「仍是旧世界」一条**限流 3 秒一条**（该判定每次绘制都会被调用） | 便于实机确认到底走的是哪条路 |

**实机验证要点**：跨服后 `/xllog` 过滤 `[AutoBuyer][跨服]`，正常应看到

```
[老路·每秒巡检] … → 不一致，按跨服处理          （或 [新路·日志判定] / [世界切换]）
[列表] 跨服后已记忆旧世界挂牌 N 条（挂单 ID 集合）；在新世界挂单出现前，游戏侧读数一律不作为当前服务器的价格
[列表] 跨服后游戏侧读数已换新（出现新世界挂单）→ 恢复采信，此前的旧世界读数不再显示
```

且**「本服」卡片全程不应出现上一服务器的价格**；若仍出现，请提供上述日志片段（挂单 ID 判定会直接指出读到的是哪一批数据）。

**实机结果（2026-10-01 02:56，用户实际跨服两次，全部按设计走完三态）**：

```
02:56:19.649 [列表] 跨服后已记忆旧世界挂牌 100 条（挂单 ID 集合）；在新世界挂单出现前，游戏侧读数一律不作为当前服务器的价格
02:56:24.736 [列表] 跨服后游戏侧仍是**上一服务器**的挂牌（100 条挂单全部来自旧世界）→ 不予采信
02:56:25.192 [列表] 跨服后游戏侧读数已换新（出现新世界挂单）→ 恢复采信，此前的旧世界读数不再显示
（02:56:37 / 02:56:45 第二次跨服：同一三态，间隔 0.4 秒）
```

这组日志同时证实了**根因判断完全正确**：客户端确实会在跨服后约 5 秒把**上一服务器的 100 条挂单重新显示出来**
（`SearchItemId` / `IsFullyReceived()` 此时全部成立），而新世界的真实数据在约 0.5 秒后到达 —— 那 0.5 秒正是用户看到的
「1~2 秒错价」窗口，现在被拦在门外。

### 第五十九轮：跨服清缓存把服务器价一起清掉，顶部价格信息块只能回落到网站数据（2026-10-01）

**现象**（用户原话）：

> 「autobuyer插件，有个问题，因跨服后清理价格数据，导致顶部价格信息块无法缓存价格，只能使用落后的网站数据」

**根因（第五十三轮清缓存的副作用）**：第五十三轮为了堵「旧世界读数被写进新世界键」，在 `InvalidateWorldData()` 里执行了 `gameMinPriceCache.Clear()` —— 这是**整体**清空。于是跨服那一刻：

```
跨服 → gameMinPriceCache 全空（所有世界、包括刚离开的世界与目的地世界）
     → 卡片重建（forcePriceTableRebuild）时，本服读不到游戏数据（布告板未打开 / 尚未下发重搜索）
     → ②「服务器最低价缓存」这条路径全空
     → 只能落到 ③ TryGetOnlineAggregatedMinPrice（Universalis 聚合）
```

而 Universalis 聚合本身「明显滞后于其网页」（第十三～十四轮的实测依据，TTL 因此定为 15 分钟）—— 即用户看到的「落后的网站数据」：跨服后顶部卡片显示的是众包价，而不是上一次真实读到的**服务器价**，且要一直等到游戏侧读数回来（过场结束、补拉下发搜索并返回，通常数秒）为止。

> 口径更正（第五十九轮）：本插件**替代布告板界面**，本地搜索不依赖「站在布告板前」——`IsAbleToSearchLocalMarket()` 只要求「已登录 + 不在副本」，跨服后唯一等待来源是跨界传送过场。因此这个「网站价窗口」实际只有数秒量级；但即便只有数秒，回落到滞后更久的众包价仍是回退（第二十四轮早已确认「服务器数据优先于众包数据」）。

**为什么可以只删目的地世界、而不整体清**：第五十三轮真正防的是「旧世界读数被写进**新世界键**」，那条路现在由两道护栏堵死：

| 护栏 | 作用 |
| ---- | ---- |
| `!pendingWorldResyncSearch`（第五十三轮） | 跨服后尚未为新世界下发搜索之前，游戏侧读数一律不采信 ⇒ 不会写入缓存 |
| `IsWorldResyncStaleReading()`（第五十八轮，挂单 ID 记忆） | 客户端把上一服务器挂牌重新显示回来时同样不采信 ⇒ 不会写入缓存 |

两者都成立时 `gameMinPriceCache` 不会被写入，缓存里不会凭空出现新世界的价格；而每个键都带世界 ID，旧世界的条目**只会显示在旧世界自己的卡片上**（该世界自己的历史读数），不会冒充当前服务器价格。

**处置**：

| # | 措施 | 位置 |
| ---- | ---- | ---- |
| 1 | `InvalidateWorldData()` 不再 `gameMinPriceCache.Clear()`，改为只删「目的地世界（= 新当前世界）」的条目（新增 `RemoveGameMinPriceCacheEntriesForWorld(worldID)`）；其余世界的服务器价保留至 TTL 到期 | `MarketBoard.Data.cs` |
| 2 | 目的地世界的条目仍然删除：它们通常是从网站回落写入的**非服务器读数**，删掉才能保证新世界卡片优先显示游戏数据，而不是旧一次的网站数值 | 同上 |
| 3 | 设计文档 §14.1 / §14.4 的「跨服时清空服务器最低价缓存」口径同步修正为「只删目的地世界」 | `设计文档.md` |
| 4 | **定位前提的口径更正**（用户本轮同时指出）：本插件**替代布告板界面**，本地搜索不需要玩家站在布告板前。据此改写三处会误导后续排查的注释/文案：`waitingBoardAfterWorldChange` 的说明（等的是**过场结束**，不是玩家走到布告板）、补拉参数块（跨服后唯一等待来源是 `IsPlayerTransitioning`）、跨服日志「跨服后布告板已可用 → 立即发起搜索」→「跨服后已可发起本地搜索（过场结束）→ 立即发起搜索」；并修正第五十八轮「走到布告板」与第二十五轮「走过去通常远超 15 秒」两处错误依据 | `MarketBoard.Data.cs`、`MarketBoardModule.cs`、`CHANGELOG.md`（本轮）、`CHANGELOG.md`（第二十五轮附口径更正）、`设计文档.md`、`测试文档.md` |

**未采用（本轮刻意不做）**：

- **不给缓存价加「陈旧」角标**：界面已用「本服」徽标与配色表达世界归属，再加一层时效角标属未要求的新功能（第五十四轮「没让你加的功能别乱加」）。
- **不缩短 30 分钟 TTL**：该 TTL 是用户明确要求（第十三～十四轮「短时缓存服务器最低价并优先于 Universalis 显示」），本轮只恢复它的适用范围。

**待实机验证（测试阶段）**：跨服后顶部价格信息块应立即显示各世界**上一次真实读到的服务器价**（而不是网站价），并在补拉下发搜索、游戏侧读数回来（通常数秒，无「走到布告板」这一环）后被新读数覆盖；`MinPriceData` / `MaxPriceData`（最低价 / 最高价卡片高亮）随之立即恢复，不再有「跨服后卡片一段时间的网站价」。

**本轮构建验证的环境障碍（已解决，非代码问题）**：2026-10-01 16:2x 期间，受 DSH 文件沙箱限制的构建进程无法写入工程内 `obj/` 与 `E:\Code\Output`——报 `error MSB3491: 未能向文件"obj\Release\OmenTools.AssemblyInfoInputs.cache"写入行 … Access to the path … is denied`（以 `dotnet build` 默认并行节点运行时表现为「0 错误 0 警告但生成失败」，改用 `-m:1 -nodeReuse:false` 才暴露出这条真实错误）。两轮 ACL 修复（`E:\Code\Work\FFXIV-AutoBuyer` 与 `E:\Code\Output` 两条链，`WRITE_OWNER` 均已补回并验证）后受限写入仍被拒，且 `E:\Code\Work` 根目录与本次新建子目录可写、既有子目录不可写 —— 判定为**会话的工作区授权未覆盖既有子目录**。用户将会话切到完全权限后，同一条命令立即构建成功；**解除限制前后产物逐字节一致**（`564A1AFC…7AA36`），确认此前失败与代码无关。

**构建与产物（2026-10-01 16:27 首轮 / 16:30 定稿）**：工程级 `dotnet build FFXIV-AutoBuyer\FFXIV-AutoBuyer.csproj -c Release` 与解决方案级 `.slnx` 均 **0 错误 / 0 警告**；`-t:Rebuild` 全量重建复核结果与增量一致。**定稿产物**（含本轮的口径更正）`AutoBuyer.dll` 150,016 B（SHA256 `66855F52CB1182952F7B4C65321908B62578906CE2BFDB591D6C4F9FE1F448CB`，2026-10-01 16:30:58），落 `E:\Code\Output\FFXIV-AutoBuyer\Release\`；清单与 DLL 版本 `1.1.3.1`；DLL 元数据含新方法 `RemoveGameMinPriceCacheEntriesForWorld`（与 `InvalidateWorldData` / `IsWorldResyncStaleReading` 并存），确认改动确在游戏加载的那份产物里。（16:27 那版为纯代码改动版，SHA256 `564A1AFC…7AA36`，已被定稿版取代。）

**提交** `d86e6b2` — *fix: 1.1.3.1 跨服不再清空服务器价缓存（顶部价格信息块恢复服务器价优先）*（4 文件：`FFXIV-AutoBuyer.csproj` + `MarketBoard.Data.cs` + `MarketBoardModule.cs` + 新增 `CHANGELOG.md`；**+1,381 / −20**）
**版本号** 清单 `1.1.3.0` → **`1.1.3.1`**（**修订位**：z 不变、a 递增；对外仍显示 `1.1.3` —— 本轮是 1.1.3 的同一发布单元内的修订，与第五十三～五十八轮同属一次发布）
**tag** `1.1.3.1`（annotated，指向 `d86e6b2`）。既有 `1.1.3` tag 保留不动（该 tag 用的是旧的四位版本写法，故新 tag 只能带 `.1` 尾段）
**Release** <https://github.com/Sparrow-prime/FFXIV-AutoBuyer/releases/tag/1.1.3.1>（**标题仍为 `1.1.3`**，与既有 release 标题一致；正文 1,400 字符，已核对无乱码）
**附件** `latest.zip` **391,611 B**（SHA256 `B76C451F12DA27AFE85272A57AE56ED2A8E12C8F5295757B1E1329DE64F9F0CB`；GitHub 侧资产 `digest=sha256:b76c451f…0cb` 与本地一致），内含 `AutoBuyer.dll` 150,016 B（SHA256 `66855F52…48CB`）、`AutoBuyer.json`、`AutoBuyer.deps.json`、`Localization/zh-CN.json`、`OmenTools.dll`、`DailyRoutines.Common.dll`、`TinyPinyin.dll`
**推送方式**：本机 SSH（22 端口）仍被网络环境拦截（`Connection closed … port 22`），改走 HTTPS + Token（`E:\Code\Personal file\github_token.txt`，仅经命令参数与环境传入，未写入 `.git/config`、未落盘、未回显）
**测试**：自动化（构建 / 清单 / 产物 / 部署一致性）见 `测试文档.md`（2026-10-01 重跑）；**实机回归（★1、★1b、★2、★3）尚未执行**，见该文件 §三

---

## 附录 A：已否决与已回滚的方案

集中登记，避免读者把中间态误当现行设计。

| # | 轮次 | 方案 | 结局与原因 |
| ---- | ---- | ---- | ---- |
| A1 | 第十八轮 → 第十九轮 | 「统一使用游戏原生字号」（`UseUniformNativeFont`，全界面 18px） | **撤销**。实测字号过大：占界面多数的 12px 小字被放大 50%，并非用户所需；真实诉求是「小卡小字别那么小」 |
| A2 | 第二十四 → 四十二轮 | 以 `GameState.CurrentWorld`（`AgentLobby.LobbyData.CurrentWorldId`）为世界变化依据，配以去抖（连续 2 tick）、3 秒稳定确认、过场结束判定等层层延后 | **整体废弃**。该字段本身不可靠（过场开始就变、甚至根本不随跨界传送变化），导致长期「一直显示上一服务器数据」；第四十二轮改用游戏日志作为唯一权威信号 |
| A3 | 第二十四 → 三十七轮 | 「世界数据状态机」：`worldDataRefreshed` / `worldDataNotReadySeen` / 15 秒超时解锁 / 30 秒重新补拉 / `searchIssuedSinceWorldChange` / `TickWorldDataState` / `IsGameWorldDataReady` | **整体删除**（第三十七轮）。自建跨帧状态机出现死锁（判定只在绘制本地表时被调用），且超时解锁会把旧服数据当新数据；改用游戏自身 API 语义（`ClearListData` + `IsFullyReceived`） |
| A4 | 第二十七轮 | 世界数据确认的「15 秒超时兜底解锁」 | **删除**（第三十五轮）。超时即认可会把上一服务器的残留挂单当作新服数据显示 |
| A5 | 第三十七轮 | 本服也用 Universalis 兜底表渲染 | **删除**（第三十八轮）。兜底数据不能购买、价格不准，且在购买/跨服期间会接管列表，来源切换看起来像列表被刷新 |
| A6 | 第三十五 ~ 三十六轮 | 过渡期将购买按钮置灰 + 状态机推进 | **删除**（第三十七轮）。用户明确要求去掉按钮锁（原话「垃圾设计」），并因判定只在绘制本地表时被调用而出现死锁 |
| A7 | 第四十八轮 | 跨服渲染闸门：`!IsPlayerTransitioning` + `IsWorldResyncGameDataTrusted()`，过渡期不沿用上一次快照 | **已完整回滚**（同轮）。把「旧数据连续显示」切成多次闪烁（旧数据 → 刷新中 → 旧数据 → 刷新中 → 新数据），比原现象更刺眼；回滚后产物与第四十七轮逐字节相同 |
| A8 | 第五轮 ~ 第九轮 | 为压制「DR 造成的刷新症状」而设的严格节流（全局 2s、同物品冷却 10s、在途 6s、熔断 4 次/60s） | **放宽**。DR 关闭后症状消失，证明是在追错的嫌疑人；按用户决策保留骨架、放宽数值（详见第九轮） |
| A9 | 设计文档 §6.3.1 | 勾选框用 `ImGui.CalcTextSize` + `GetWindowContentRegionMax()` 计算宽度并靠右绝对定位，再把游标还原 | **被推翻**（第二十三轮）。改为就地绘制、紧随「刷新市场数据」按钮，删除绝对定位与游标还原 |
| A10 | 第一轮移植 | `FollowLocalSearch`（跟随游戏侧搜索并改写 `info->SearchItemId`） | **删除**。属死代码，且会绕过全部节流直接触发游戏自行搜索 |

---

## 附录 B：工程教训（一般化结论）

以下结论跨轮次成立，写代码与改代码时都适用。

| # | 教训 | 来源轮次 |
| ---- | ---- | ---- |
| B1 | 一个状态位若被多处改写，任何一处对「失败」的语义理解偏差都会变成永久卡死；状态机应有**唯一所有者**，其他模块只读 | 第五十轮 |
| B2 | 缓存的「新鲜度」应由**数据本身**判定，而不是「什么时候请求过」；请求与数据之间隔着网络、回调注册、纪元校验 | 第五十二轮 |
| B3 | 接入第三方 API 必须区分「接口级失败」（`failedItems` / 空结果）与「业务级无数据」—— 后续动作完全相反（重试 vs 不重试） | 第五十一轮 |
| B4 | 能用「游戏自身 API 的语义」表达的判定，不要自建跨帧状态机 —— 前者不会因 UI 分支变化而漏推进 | 第三十、三十七轮 |
| B5 | 任何「状态机」都必须在**固定 tick 源**上推进，不能依赖某个 UI 分支是否被绘制 | 第三十六轮 |
| B6 | 「检测与作废」必须无条件执行；「发起请求」才受通信最小化约束 | 第三十八轮 |
| B7 | 当同一症状反复出现（第 24/39/40/41 轮），应怀疑**数据源本身**，而不是继续加延后与去抖；换用「事件式、只在事实发生后出现」的信号（游戏日志）后时序问题自然消失 | 第四十二轮 |
| B8 | 判断「状态切换完成」要用**客户端可操作状态**（过场结束），而不是时间或某个可能提前更新的字段 | 第四十一轮 |
| B9 | 本地隐藏制造了两个不同步的视图（我方行序 vs 游戏字符串数组行序）；凡「用行号取数据」的展示都必须回答「两套行序此刻是否一致」，并用**可验证的探针**回答，而不是靠注释里的假设 | 第四十三轮 |
| B10 | 「改好了」必须包含「游戏加载的是我刚构建的那份文件」。本工程有两套输出目录（`.slnx` 因 `Platform=x64` 落到 `x64\Release\`，工程级构建落到 `Release\`），而 Dalamud 只加载 `Release\`；不同步就会出现「代码明明写了、游戏里什么都看不到」 | 第五十五轮 |
| B11 | 二进制取证时不要用 `Encoding.Unicode.GetString(整个文件)` 直接搜中文：字符串堆的起始偏移可能是奇数，按文件起点两两配对会整体错位而漏匹配。应先以 ISO-8859-1 中转成「1 字节 = 1 字符」，再用 UTF-16 编码后的字节序列去搜 | 第五十五轮 |
| B12 | 「零引用」不能只按类型名判定：① 扩展方法调用不会写出类名；② **属性式扩展**（`public bool IsX => …`）连方法名都没有；③ 互相引用的死代码会彼此续命。可靠做法是「类型名扫描 → 方法名复核 → **编译器兜底**（删完必编译，报错就恢复）」，任何一步都不能省 | 第五十六轮 |
| B10 | 把「要不要工作」的判据收敛成**一个**可命名的条件，并在**唯一出口**拦截，比在每个调用点写 `if` 更容易长期保持正确 | 第四十四轮 |
| B11 | 删除前逐个 `grep` 引用点：有些「看名字像某功能专属」的符号其实被别处在用（`GetAggregatedMarketScope` 等） | 第四十五轮 |
| B12 | 渲染期不要为了「数据属于谁」而反复遮断同一份数据 —— 遮断本身就会被看见 | 第四十八轮 |
| B13 | 把「游戏侧正在更新」误判成「没有数据」会引发多余请求；进行中的状态应有明确的抑制开关 | 第四十轮 |
| B14 | 除 `RequestLocalSearchData` 外，任何位置都不得写入 `InfoProxyItemSearch` 的状态字段（`SearchItemId` / `Listings` 等），只允许读取 | 第七轮，全期有效 |
| B15 | 表格列数计算必须用加法（`columnsCount = 基准 + 可选列`）；基准列数一变，原模块的减法写法必须同步改符号 | 第十五轮 |

---

## 附录 C：立项风险清单（含后续处置）

原文：立项文档 §七、设计文档 §十七末的重复风险表。

| 风险 | 影响 | 对策 | 后续结果 |
| ---- | ---- | ---- | ---- |
| `github.com` HTTPS 被阻断 | 无法推送／拉取 | 推送走 SSH（已验证）；拉取走 `ghproxy` 镜像或 `codeload` | **已关闭**：SSH 推送可长期使用 |
| 镜像不稳定、大文件截断 | 依赖源码获取失败 | 全部源码已缓存至 `E:\Code\Work\临时文件\立项调研-20260912\clone\` | **已关闭**：依赖已内嵌进 `lib/` |
| Universalis 客户端为重写 | 跨服数据与原子段偏差 | 严格按 Universalis v2 官方 API 契约实现，字段命名与现有模型对齐，逐项比对 | **已验收**：契约测试 40 项断言全通过 |
| CN Dalamud v15 API 变动 | 编译或运行失败 | 以本机 `Hooks\dev` 为编译基准；保留 DR 安装作为行为对照 | **已关闭**：构建 0 错误 0 警告 |
| 移除两个前置模块 | 少两项体验 | 已确认接受；后续如有需要可评估自实现 | **已关闭**（决策 D4） |
| AGPL 传染性 | 需开源 | 项目本就是 AGPL-3.0，无冲突 | **已关闭** |
| 内嵌 `OmenTools` 与已装 DR 的 `OmenTools.dll` 同名 | 潜在加载冲突／重复 Hook | 各插件由 Dalamud 分属独立 AssemblyLoadContext，互不干扰 | **已关闭**：实机共存无异常 |
| 内嵌 `DailyRoutines.Common` 裁剪面 | 编译缺口 | 确认仅 `KamiToolKit/**`、`Extensions/TextNodeExtension.cs`、`Info/AtkColors.cs` 依赖 KamiToolKit，可 `Compile Remove` | **已关闭**：裁剪后构建通过 |
| Universalis 的国服大区名取值待定 | 硬编码区域名可能不匹配 | 实现时以 `/api/v2/data-centers` 实际返回值确认（`中国` 或 `China`） | **已关闭**：`ChinaRegionName = "中国"` 已线上核实 |
| 国服大区名取值 | — | 初始「中国」；编码首日以 `/api/v2/data-centers` 实测校正 | **已关闭**（同上） |
| Universalis 字段偏差 | — | 严格按 v2 契约建模；模型字段与模块使用点逐项比对（`GetLastUploadTime` 等辅助方法需自实现） | **已关闭**：契约测试通过 |
| 购买流程稳定性 | — | 以「整条挂单」为单位、5 秒等待窗口 + `TaskHelper` 超时兜底；避免无限循环 | **已演进**：等待窗口最终定为 `PURCHASE_WAIT_MS = 2000`（第四十三轮提速），并新增背包/9999 上限判定 |

> 说明：设计文档中 **L519–L525 的这张表原为无标题的孤立重复块**（与 §十二 的验收要点脱节，且其「5 秒等待窗口」已被后续轮次改写），归档时一并清理。

## 附录 D：一次性调研与构建环境记录

**源码可得性**（原文：立项文档 §4.1）

| 内容 | 位置 | 可见性 | 许可 |
| ---- | ---- | ---- | ---- |
| BetterMarketBoard 模块源码 | `DailyRoutines.ModulesPublic/Interface/BetterMarketBoard/`（12 文件，6226 行） | 公开 | AGPL-3.0 |
| DR 主程序 `DailyRoutines` | 仓库内仅有 README 与预览图 | 闭源（仅分发 DLL） | 无 |
| Universalis 客户端与模型 | 位于闭源 `DailyRoutines.dll` | 闭源 | — |
| `OmenTools`（框架底座） | `AtmoOmen/OmenTools` | 公开 | MIT |
| `KamiToolKit` | `AtmoOmen/KamiToolKit` | 公开 | MIT |
| `DailyRoutines.Common` | `Dalamud-DailyRoutines/DailyRoutines.Common` | 公开 | AGPL-3.0 |
| 模板仓库 | `Sparrow-prime/FFXIV-AutoBuyer`（goatcorp SamplePlugin 原样，仅 1 次 commit） | 公开 | AGPL-3.0 |

**依赖面梳理**（原文：立项文档 §4.2）

可复用（无需改动）：`OmenTools` 覆盖了模块所需的全部框架能力 —— `LuminaGetter`／`LuminaSearcher`／`LuminaWrapper`、`GameState`、`LocalPlayerState`、`FontManager`、`TooltipManager`、`LinkPayloadManager`、`ContextMenuManager`、`FrameworkManager`、`CommandManager`、`StandardTimeManager`、`NotifyHelper`、`ItemSource`、`IPCProvider`/`IPCSubscriber` 特性、`TaskHelper`、`WindowManager`、`HttpClientHelper`、`ImGuiOm`。其本身即面向独立插件设计（`DService.Init(pluginInterface)` / `DService.Uninit()`）。`DailyRoutines.Common` 的 `ManagerHost` 只是一个可自行实现的接口槽（`IManagerHost`），因此 `ModuleBase` / `BaseOverlay` / `OverlayConfig` 能在不安装 DR 的前提下复用。

必须重写／替换：

| 符号 | 归属 | 处置 |
| ---- | ---- | ---- |
| `DailyRoutines.RemoteInteraction.Universalis.*`（`RemoteUniversalisCatalog` / `Market` / `AggregatedMarket` / `History` 及全部 `Universalis*` 模型） | 闭源 | 按 Universalis 公开 REST API v2 重写等价客户端 |
| `ModuleManager.Instance().GetModule<>()` | 闭源 | 换成自实现的模块宿主 |
| `PluginConfig.Instance().ConflictKeyBinding` | 闭源 | 换成插件自身配置项 |
| `DailyRoutines.Extensions` 的 `Config.Load/Save` | 闭源 | 换用 `ModuleBase.LoadConfig/SaveConfig` |
| IPC 命名前缀 `DailyRoutines.Modules.*` | 闭源 | 改为 `FFXIVAutoBuyer.*` |
| `Lang.Get` 文案 | 公开仓库 `DailyRoutines.Localizations` | 抽取本模块文案，内嵌简体中文（可选英文） |
| `ModulesPrerequisite` / `ModulesRecommend` | DR 机制 | 移除 |

其它事实：`Config` 为模块自带类（`BetterMarketBoard.Models.cs`），无需外部依赖；模块使用 ImPlot 绘制趋势图。

**构建环境核查**（原文：立项文档 §4.3）

- 本机 `dotnet` SDK：8.0.411 / 9.0.300 / 9.0.315 / **10.0.400**。
- `%APPDATA%\XIVLauncherCN\addon\Hooks\dev\` 存在，含 `Dalamud.dll`、`Dalamud.Bindings.ImGui/ImPlot/ImGuizmo.dll`、`FFXIVClientStructs.dll`。
- 本机已安装 DR（`pluginConfigs\DailyRoutines\Dev\`），可作为 API 行为对照与调试参照。
- NuGet 可用：`Dalamud.CN.NET.Sdk 15.0.0`、`DalamudPackager 15.0.0`、`DailyRoutines.CodeAnalysis 2.6.0`、`AngleSharp`、`TinyPinyin`、`TimeAgo.Core`。
- `OmenTools` / `KamiToolKit` / `DailyRoutines.Common` 不在 NuGet ⇒ 只能源码内嵌或引用 DLL。
- 网络：`github.com` 的 HTTPS 被阻断；`api.github.com`、`codeload`、`raw` 可用；SSH 推送可用且已认证为 `Sparrow-prime`。

**构建与产物**（原文：设计文档 §14.3）

- 命令：`dotnet build FFXIV-AutoBuyer/FFXIV-AutoBuyer.csproj -c Release`
- 结果：0 错误、0 警告（仅 OmenTools 上游 3 处过时 API 警告被 `NoWarn` 吞掉）
- 产物目录 `E:\Code\Output\FFXIV-AutoBuyer\Release\`：`AutoBuyer.dll`、`AutoBuyer.json`（`Name`/`InternalName` 均为 `AutoBuyer`，`AssemblyVersion 1.0.0.0`，`DalamudApiLevel 15`）、`Localization/zh-CN.json`、依赖 `OmenTools.dll`、`DailyRoutines.Common.dll`、`AngleSharp.dll`、`GuerrillaNtp.dll`、`TimeAgo.Core.dll`、`TinyPinyin.dll`，以及打包产物 `AutoBuyer/latest.zip`。

**对内存库的本地改动（非功能，同步上游时需重新套用）**

| # | 内容 |
| ---- | ---- |
| C1 | OmenTools 不再后台枚举本机字体（`RegenerateInstalledFonts`） |
| C2 | OmenTools `NoWarn` 追加 `CS0618` |
| C3 | 字体构建门控（`IsUIFontBuilding`）仅首次构建生效 |
