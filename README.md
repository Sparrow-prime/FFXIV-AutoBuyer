# AutoBuyer（FFXIV-AutoBuyer）

面向**最终幻想14 国服**（XIVLauncherCN / Dalamud API 15）的市场布告板增强插件：
跨服价格排行、在售物品列表，以及**按目标持有数量从最低价开始一键购买**。

> 不依赖 DailyRoutines（DR）主插件，可独立安装运行。

## 功能

| 功能 | 说明 |
| ---- | ---- |
| 跨服价格卡片 | 中国大区内「三个最低价 / 三个最高价」世界价格卡片；可勾选「仅显示当前大区数据」只看当前数据中心 |
| 在售物品列表 | 读取游戏内市场布告板实时挂牌（单价 / 数量 / 总价 / 雇员名），支持 HQ 过滤，并插入「成交均价」「NPC 收购价」基准行 |
| 一键购买 | 顶部输入目标持有数量后点击「购买」，从当前服务器在售列表按最低价逐条购买，直到达到目标数量或背包已满 |
| 隐式刷新 | 购买后不隐藏当前列表，等新数据就绪后自动替换 |
| 物品选择器 | 左栏「搜索 / 收藏」，支持按名称与物品 ID 检索 |
| 后台静默 | 插件窗口未打开时不做任何自动请求：不查游戏市场、不请求 Universalis、不监测跨界传送日志（开窗时一次性校正并补齐） |
| IPC | 对外提供 `FFXIVAutoBuyer.MarketBoard.SearchItem` 与 `FFXIVAutoBuyer.MarketBoard.ToggleOverlay` |

## 命令

| 命令 | 说明 |
| ---- | ---- |
| `/market` | 打开 / 关闭市场布告板 |
| `/market <物品ID>` | 以指定物品打开 |
| `/market <物品名称>` | 模糊匹配名称并以首个结果打开 |

## 安装

1. 需要 **XIVLauncherCN**（国服 Dalamud，API Level 15）。
2. 在游戏内使用 `/xlsettings` → **实验性（Experimental）** → 将本插件的 `AutoBuyer.dll` 路径加入 Dev Plugin Locations；
   或通过第三方插件库（仓库地址见插件清单 `RepoUrl`）安装。
3. 使用 `/xlplugins` 启用 **AutoBuyer**。

## 使用要点

- **购买仅对「当前服务器」的在售列表可用**（切换到其他世界时列表为 Universalis 数据，无法直接下单）。
- 每次购买的是**整条挂单**（含该挂单的全部数量），因此最终持有量可能略高于目标数量。
- 购买过程中按钮显示「购买中…」，再次点击即手动停止。
- 跨服价格卡片上**右键**可请求通过 Lifestream 传送到该世界（未安装 / 未启用 Lifestream 时仅提示，不发送聊天指令）。
- 因背包已满（或已达 9999 持有上限）而停止购买时会弹出提示；如觉得吵，可在设置里关闭「背包已满导致停止购买时弹提醒」。

## 数据来源

跨服价格、历史成交与统计来自 [Universalis](https://universalis.app) 公开 API（API v2），
国服大区固定为「中国」。数据受 Universalis 上传延迟影响，仅供参考。

## 配置

配置保存于 `%APPDATA%\XIVLauncherCN\pluginConfigs\AutoBuyer\MarketBoardModule.json`：

| 字段 | 说明 |
| ---- | ---- |
| `OnlyCurrentDC` | 是否只显示当前数据中心（大区）的数据 |
| `PurchaseQuantity` | 上次使用的目标持有数量 |
| `EnableDiagnostics` | 是否输出诊断日志（默认关闭；跨服检测日志不受此开关影响） |
| `NotifyInventoryFull` | 背包已满 / 已达 9999 上限停止购买时是否弹提醒（默认开启） |
| `FavoriteItems` | 收藏物品 |
| `AllWorlds` | 世界 / 数据中心目录缓存 |

## 来源与许可

- 市场布告板功能移植自 **DailyRoutines** 的 `BetterMarketBoard` 模块，原作者 **Fragile**
  （<https://github.com/Dalamud-DailyRoutines/DailyRoutines>，AGPL-3.0），
  并按本项目需求裁剪与改造（移除分页 / 统计块 / 整单购买 / 价格监控，新增按目标数量购买等）。
- 内嵌依赖（源码内置于 `lib/`）：
  - **OmenTools** — MIT，<https://github.com/AtmoOmen/OmenTools>
  - **DailyRoutines.Common** — AGPL-3.0，<https://github.com/Dalamud-DailyRoutines/DailyRoutines.Common>
- 本项目整体以 **AGPL-3.0** 发布，详见 `LICENSE.md`。

## 构建

需要 .NET 10 SDK，且本机已安装 XIVLauncherCN（编译期从 `%APPDATA%\XIVLauncherCN\addon\Hooks\dev\` 读取 Dalamud 引用）。

```bash
dotnet build FFXIV-AutoBuyer/FFXIV-AutoBuyer.csproj -c Release
```

产物输出到 `E:\Code\Output\FFXIV-AutoBuyer\Release\`，包含 `AutoBuyer.dll`、`AutoBuyer.json`（插件清单）
以及打包好的 `AutoBuyer\latest.zip`。

> ⚠️ 请以**上面这条 csproj 命令**为准（产物落在 `Release\`）。
> 若改为构建解决方案 `FFXIV-AutoBuyer.slnx`（解决方案平台为 `x64`），产物会落到 `x64\Release\`，
> 两处同时存在时容易误装旧文件——插件安装请统一指向 `Release\AutoBuyer.dll`。
