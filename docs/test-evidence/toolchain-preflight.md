# 工具链预检

检查日期：2026-08-22

验收平台：Android

| 项目 | 结果 |
| --- | --- |
| .NET SDK | 10.0.302，由 `global.json` 固定 |
| Flutter | 3.47.0 stable，Dart 3.13 |
| Android Studio | 2026.1.3.8 便携版，内含 JBR 25.0.2 |
| Android SDK | platform-tools 37.0.1、platform 36 rev2、build-tools 36.0.0、emulator 37.1.11 |
| 模拟器 | `MentalHealth_API_36`，Pixel 7，x86_64，API 36；已完成启动检查 |
| Docker | Docker Desktop 服务端 29.4.3，`desktop-linux` 上下文可用 |
| FFmpeg | 8.1.1 |
| Flutter doctor | Android 工具链、许可证和模拟器通过；未安装的 Windows C++ 桌面组件不影响本项目 |
| 数据采集 | Dart/Flutter 统一遥测已关闭并回读确认 |
| 持久环境 | 未写入用户或系统 `Path`，未写入持久 `JAVA_HOME`、`ANDROID_HOME` 或 `ANDROID_SDK_ROOT` |

下载包在解压前完成 SHA-256 校验：

- Flutter：`9f96d393cdfad05bea0b4b42c603ffda027af11adadc8e4cf3ac87e49110c1ca`
- Android Studio：`758e927767972c44f2bb14e0af035e6b90ec07a9e2819fc2eb83f51a2492501b`
- Android command-line tools：`90ae805d20434428bffcb699c290860f19bb5f66a67e6b330067e3de801fb04a`

仓库脚本 `scripts/Use-Toolchain.ps1` 只修改当前 PowerShell 进程。关闭窗口后不再生效。
