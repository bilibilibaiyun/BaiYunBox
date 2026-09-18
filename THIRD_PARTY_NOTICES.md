# 第三方组件声明 (THIRD_PARTY_NOTICES)

BaiYun Box 随安装包分发以下第三方开源组件。各组件版权归其原作者所有。

## 1. mpv / libmpv

- 项目：https://mpv.io / https://github.com/mpv-player/mpv
- 用途：本地视频/音频/流媒体解码与播放内核（libmpv-2.dll）。
- 许可：LGPLv2.1 或更高版本（libmpv 客户端库）。
- 二进制来源：zhongfly/mpv-winbuild 的 LGPL 构建（`mpv-dev-lgpl-x86_64`），
  https://github.com/zhongfly/mpv-winbuild
- 说明：BaiYun Box 通过动态链接（P/Invoke）加载 libmpv-2.dll，libmpv 库以
  LGPLv2.1+ 分发，允许用户替换库文件。LGPL 协议全文见
  https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html

## 2. FFmpeg

- 项目：https://ffmpeg.org
- 用途：libmpv 内置的多媒体解码组件。
- 许可：LGPL（本分发使用 LGPL 构建，不含 GPL-only 组件）。
- 说明：随上述 LGPL 版 libmpv 静态链接提供。

## 免责声明

BaiYun Box 不提供任何影视/直播/播客内容，所有播放源由用户自行配置。
请仅播放您有权访问的内容，并遵守相关法律法规与各内容平台的条款。
