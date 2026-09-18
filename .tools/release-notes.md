## v1.0.3 更新

### 新增
- **JS 爬虫源支持**：支持 `type=3` 的 dr_py / FongMi js0 声明式爬虫（`var rule`），内置 Jint 引擎本地执行，覆盖「道长 js 源」等一大类安卓 TVBox 点播源。支持 `class_parse` 分类、`url`/`searchUrl` 模板、`json:`/`js:` 内联规则、`lazy` 懒播、muban 模板覆盖、GBK 解码。
- **源识别更友好**：加载源时自动过滤不支持的类型，给出明确提示（csp_ JAR / xpath 不支持，CMS / JS 源可用）。

### 说明
- `csp_` JAR 站点（安卓 dex + 加固）在 Windows 上无法运行，这是安卓生态与桌面生态的固有不兼容，所有桌面 TVBox 播放器（UnBox、FreeBox 等）均不支持。
- 桌面端可用源类型：CMS JSON（type=1）+ JS 爬虫（type=3，dr_py / js0 声明式）。

### 保留功能
- 点播（TVBox 单线路 / FongMi 多线路源，CMS JSON + JS 爬虫）
- 直播（M3U / M3U8 / TXT）
- 本地媒体库、播客（订阅 / 下载 / 播放）
- 断点续播、播放控制、检查更新
