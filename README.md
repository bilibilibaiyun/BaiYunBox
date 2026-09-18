# BaiYun Box（白云盒子）

一个**轻量、离线优先**的 Windows 桌面 TVBox 播放器 —— 点播 + 直播 + 本地视频 + 播客（含本地转录），一个安装包装好即用。

BaiYun Box 兼容 TVBox 单线路源、FongMi 多线路源（CMS JSON）与 M3U / TXT 直播源，内置 libmpv 播放内核（一个内核同时搞定点播/直播流、本地视频与播客音频），并内置 whisper.cpp 实现**本地离线**播客转录。无需部署服务、无需配置 Go / Node 环境。

## ✨ 特性

- **点播 + 直播**：一套界面同时支持视频点播与 IPTV 直播，分类 / 分组 / 搜索齐全。
- **TVBox 兼容**：支持 TVBox 单线路源（顶层 `sites`）与 FongMi 多线路源（`storeHouse` → 子源），CMS JSON API 分类/列表/搜索/详情。
- **多线路切换**：点播详情页按线路切换剧集列表。
- **本地媒体库**：添加本地目录递归扫描视频，同名海报自动识别，断点续播。
- **播客播放**：RSS / Apple Podcasts 搜索与订阅、剧集下载、在线/本地播放。
- **本地导入转录**：导入本地音频文件（mp3/m4a/wav/flac 等），本地 whisper.cpp 转录音频为文字，转录结果可复制/另存。
- **断点续播**：自动记录观看进度，首页一键接着看。
- **播放控制**：播放/暂停、进度拖动、音量、倍速（0.5×–2.0×）、键盘快捷键（空格暂停、←/→ 快退快进、Esc 退出）。
- **完全离线**：识别与播放不离开本机（除播客/点播源本身需要联网访问外，无任何遥测、无账号）。

## 📡 支持的源

| 类型 | 结构 | 说明 |
|---|---|---|
| TVBox 单线路源 | 顶层 `sites` 数组 | `type=1` CMS JSON |
| FongMi 多线路源 | `storeHouse` → 子源 | 点播页可切换线路 |
| 直播源 | M3U / M3U8 / TXT | 支持 URL 或本地文件路径 |

> 暂不支持 `type=3` JS 爬虫（js0 / drpy）与 `csp_` JAR 站点，后续按需补充。

## 🖥️ 安装

前往 [Releases](../../releases) 下载 `BaiYunBox_Setup.exe`（Inno Setup 安装程序，x64）。

> ⚠️ 安装包暂未代码签名，首次运行 SmartScreen 会提示「发布者未知」，点「更多信息」→「仍要运行」即可。

## 🚀 使用

1. **点播**：设置页粘贴 TVBox / FongMi JSON 源地址 → 点播页选择站点，浏览分类或搜索，进入详情选集播放。
2. **直播**：设置页粘贴 M3U / TXT 直播源地址 → 直播页选择源加载，点频道播放。
3. **媒体库**：设置页添加本地目录 → 媒体库页扫描，点击视频断点续播。
4. **播客**：播客页粘贴 RSS / Apple 地址导入订阅，或搜索节目订阅；剧集可在线播放/下载；「导入本地音频」可转录任意本地音频文件。
5. **首页**：查看观看历史，点击断点续播。

## 🔧 开发者构建

```powershell
# 1. 下载引擎（libmpv + whisper.cpp + ffmpeg），需 Python 3.9+
python .tools\fetch_engines.py

# 2. 构建
dotnet build BaiYunBox.sln -c Release

# 3. 发布（自包含 win-x64）
dotnet publish src\BaiYunBox\BaiYunBox.csproj -c Release -r win-x64 --self-contained true
```

## 🧱 技术栈

- **UI**：.NET 8 WPF（`net8.0-windows`，自包含 `win-x64`，无需预装运行时）。
- **播放内核**：libmpv（`libmpv-2.dll`，LGPL，P/Invoke 动态加载 + HwndHost 渲染）。
- **转录引擎**：whisper.cpp（`whisper-cli.exe`，MIT，本地离线）。
- **存储**：JSON 配置文件（零第三方依赖）。
- **网络/JSON/XML**：框架内置 HttpClient / System.Text.Json / XDocument。
- **零 NuGet 依赖**：除上述随包内置的二进制引擎外，无任何第三方 NuGet 包。

## 📄 License

MIT License，详见 [LICENSE](LICENSE)。

随包分发的第三方组件（libmpv / FFmpeg / whisper.cpp）许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## ⚠️ 免责声明

本项目仅用于学习与研究，不提供任何影视/直播/播客内容。所有播放源由用户自行配置，请仅播放您有权访问的内容，并遵守相关法律法规与各内容平台的条款。
