# W1 本机能力验证

- 验证日期：2026-08-22
- 设备：Android API 36 模拟器 `emulator-5554`
- 范围：只验证本机开发能力，不连接云 RTC、语音、存储、通知或支付服务。

## 运行方法

在仓库根目录执行：

```powershell
.\scripts\Test-W1Capabilities.ps1
```

脚本会启动一个临时 Development API，使用空闲端口运行 SignalR Echo Hub，安装调试 APK，设置相机拒绝状态并执行四项 Android 用例。结束时，它会恢复测试权限并停止自己启动的 API。环境变量只在脚本进程内生效。

## Android 验证结果

| 项目 | 输入 | 结果 |
| --- | --- | --- |
| SignalR | Android 连接本机 Development Echo Hub，发送 `w1-probe` | 返回 `w1-probe`，通过 |
| WebRTC | 两个本机 `RTCPeerConnection`，`iceServers` 为空 | offer/answer 互换完成，双方本地和远端描述正确，通过 |
| 相机拒绝 | CAMERA 权限为已拒绝且不再询问 | 2 秒内返回 `NotAllowedError`，没有授权弹窗，通过 |
| 中文 TTS | 查询 Android 本地 TTS 引擎和 `zh-CN` | 找到 `com.google.android.tts`，`zh-CN` 可用，语言列表含中文，通过 |

TTS 用例只查询已安装引擎和语言，不调用语音合成，也不发送文本。

六组 Provider 合同测试同时通过：

```text
失败: 0，通过: 41，已跳过: 0，总计: 41
```

合同覆盖取消令牌、幂等键冲突、对象键穿越、空输入和对象读写删除；测试项目另提供可调时钟。

完整执行结果：

```text
00:00 +0: development SignalR hub echoes a value
00:03 +1: two local WebRTC peers complete offer and answer
00:03 +2: camera capture returns an error when Android blocks it
00:04 +3: offline Chinese TTS capability is available
00:06 +4: All tests passed!
```

## FFmpeg 验证结果

工具版本：

```text
ffmpeg 8.1.1-full_build-www.gyan.dev
ffprobe 8.1.1-full_build-www.gyan.dev
```

FFmpeg 生成一段 1 秒合成媒体，内容为黑色视频和静音音频。ffprobe 读回结果：

| 流 | 编码 | 参数 | duration |
| --- | --- | --- | --- |
| 视频 | H.264 | 320×240，25 fps | 1.000000 秒 |
| 音频 | AAC | 16 kHz，单声道 | 1.000000 秒 |
| 文件 | MP4 | 3035 bytes | 1.000000 秒 |

测试文件 SHA-256：`E0AF2AE1EC6A2F9AC6A7E36A2EE606CAD315992FAFF7B470412F26B234DE1346`。文件位于被 Git 忽略的 `tests/output/`，不进入提交。

## RED 记录

1. SignalR 首次运行返回 HTTP 404。原因是 API 尚未映射探测 Hub。增加仅限 Development 的 `/hubs/development-probe` 后通过。
2. WebRTC 首次运行发生原生中止。Android 日志显示读取网络状态时缺少 `ACCESS_NETWORK_STATE`。补齐权限后，两个本机 PeerConnection 通过。
3. 用 AppOps 强制相机服务忽略请求时，插件报告相机设备严重错误且调用不结束。这不是用户拒绝授权。正式的权限拒绝用例改用 Android“已拒绝且不再询问”状态，并稳定返回 `NotAllowedError`。

## 当前风险

- `flutter_tts 4.2.5` 和 `flutter_webrtc 1.6.0` 仍使用旧式 Kotlin Gradle Plugin 接入。Flutter 3.47 当前可以构建，但未来升级 Flutter 前必须复测并优先升级插件。
- JBR 25 运行 Gradle 时提示原生访问兼容警告，当前调试 APK 构建通过。
- 相机服务故障与用户拒绝不是同一情况。视频功能实现时仍需提供打开失败和超时提示，不能把它当成权限拒绝。

## 结论

W1 要求的本机 SignalR、WebRTC、相机拒绝、中文 TTS 和 FFmpeg 能力均已通过，可以继续使用当前技术组合。
