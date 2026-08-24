# 本地文本、音频和视频特征验证

日期：2026-08-24

## 本次实现

- 文本提取器输出 `self_harm_intent`、`hopelessness` 和 `social_withdrawal`，每项都带字符范围和规则版本。
- “我并没有想伤害自己”不会命中伤害自己的特征；“没有希望”和“不想见人”仍按完整短语识别，不会被简单的“没有”或“不想”误消掉。
- 音频先由 ffprobe 检查，再由 FFmpeg 解码成单声道 16 kHz 浮点样本。输出时长、声音占比、停顿占比、平均能量和能量变化。
- 视频按约每秒一帧采样。输出采样帧数、人脸可见率、头部中心移动量和缺帧率。
- 人脸检测只返回“是否可见”和归一化中心点。每帧用完立即释放，不保存人脸截图，不生成人脸向量或声纹。
- FFmpeg 和 ffprobe 参数全部使用 `ProcessStartInfo.ArgumentList`，不拼接 Shell 命令。
- 损坏文件固定返回 `MEDIA_PARSE_FAILED`，特征列表为空；不会用默认数值冒充分析结果。
- 临时媒体只写入专用临时目录。成功、失败和取消后都会删除本次生成的精确文件和空目录。

## 测试资料

- `silence.wav`：5 秒单声道静音，16 kHz。
- `tone-with-pauses.wav`：2 秒 440 Hz 合成音，后接 3 秒静音。
- `blank.mp4`：5 秒 640×480 黑色视频，5 帧/秒。
- `synthetic-face.mp4`：由仓库内的合成正脸图片生成，5 秒、640×480、5 帧/秒。
- 三段中文文本分别覆盖普通状态、回避社交和没有希望。
- 所有音视频和文本均为合成资料，不含真实患者信息。

`synthetic-face.png` 由 OpenAI 内置 `image_gen` 生成，要求“完全虚构的成年人、正脸、自然表情、纯色背景、无文字、无标志”。完整提示词、生成日期、工具名和图片 SHA-256 保存在 `synthetic-face.provenance.json`。图片只作为仓库内静态测试资料，应用运行时不会调用图片生成服务。

## OpenCV 来源

- `OpenCvSharp4` 和 Windows 运行库固定为 `4.13.0.20260627`：<https://www.nuget.org/packages/OpenCvSharp4/4.13.0.20260627>
- 正脸分类器来自 OpenCV 官方仓库 `4.13.0` 标签：<https://github.com/opencv/opencv/blob/4.13.0/data/haarcascades/haarcascade_frontalface_default.xml>
- 分类器 SHA-256 为 `0F7D4527844EB514D4A4948E822DA90FBB16A34A0BBBBC6ADC6498747A5AAFB0`。

## RED 记录

- 文本测试第一次无法编译，因为 `TextFeatureExtractor` 不存在。
- 媒体合同测试第一次无法编译，因为音频、视频、ffprobe 和 OpenCV 实现不存在。
- 官方 Windows 全包会带入只面向 .NET Framework 的 WPF 扩展，触发本项目的兼容性错误门禁。改为同版本核心包加 Windows 运行库后正常还原，没有降低警告门禁。

## 自动验证

- 文本专项单元测试：5 个通过。
- 媒体合同相关测试：9 个通过；其中真实音视频新增 6 个场景，原有 Provider 合同 3 个。
- 全仓单元测试：89 个通过。
- 全仓合同测试：93 个通过。
- 全仓集成测试：67 个通过。
- 完整构建：0 个警告，0 个错误。
- Worker 输出目录包含正脸分类器和 `OpenCvSharpExtern.dll`。
- 媒体专项测试结束后，临时分析目录内残留子目录为 0。
- NuGet 未发现已知漏洞。

当前结果是可解释的基础信号，不是诊断，也不直接给患者下结论。关注指数和证据报告从 Task 16 开始。
