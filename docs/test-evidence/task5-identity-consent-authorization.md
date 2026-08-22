# 身份、授权与同意记录验证

日期：2026-08-22

## 本次实现

- 使用 ASP.NET Core Identity 保存本机虚构账户和角色。
- 普通用户使用密码登录；医生和运营管理员首次登录必须设置动态验证码。
- MFA 设置令牌只有 `mfa_setup` 作用域，不能调用普通业务接口。
- 普通用户、咨询师、医生和运营管理员按资源归属判断权限。运营管理员不能读取聊天正文或原始音视频。
- `Service`、`Recording`、`AiAnalysis` 分别记录同意文本版本、签署人和时间，也可按记录 ID 撤回。
- `ModelTraining` 在 v1 返回 `CONSENT_TYPE_DISABLED`，不写入同意记录。
- MFA 启用、授权和撤回只记录操作人、动作、资源类型、资源 ID 和时间，不记录密码、MFA 共享密钥、聊天正文或媒体内容。

## 失败用例

实现前运行登录、权限和同意接口测试，`POST /api/v1/auth/login` 与 `POST /api/v1/consents` 均返回 404，确认测试会因路由缺失而失败。

## 自动验证

- 身份、权限和同意专项测试：18 个通过。
- 全仓单元测试：58 个通过。
- 全仓 Provider 契约测试：64 个通过。
- 全仓集成测试：23 个通过。
- 构建：0 个警告，0 个错误。
- 锁定依赖还原成功；已安装依赖未发现已知漏洞。
- `dotnet format --verify-no-changes` 通过。

覆盖的关键场景：

- 密码错误不返回 MFA 设置令牌。
- 医生和运营管理员未完成 MFA 时返回 `MFA_REQUIRED`。
- 医生可完成 TOTP 设置并登录；设置动作写入审计。
- MFA 设置令牌调用普通业务接口时返回 403 和 `FORBIDDEN_RESOURCE`。
- 普通用户仅可操作自己的同意记录；咨询师不能代替用户授权。
- 咨询师只能访问分配给自己的会话；医生只能访问自己负责复核或待医生复核的会话。
- 运营管理员可进入运营管理权限，但不能取得会话内容权限。
- 授权和撤回写入同一数据库事务；撤回保留历史和两条审计动作。
- 空文本版本和模型训练授权均被拒绝。

## 数据库验证

本机 PostgreSQL 已依次应用：

- `20260822100423_AddIdentity`
- `20260822101237_AddConsentAndAudit`

再次执行迁移时没有重复变更；EF Core 报告模型与迁移一致。`scripts/Test-LocalIdentity.ps1` 使用被忽略的本机 `.env` 启动 API，健康检查、普通用户登录和医生 MFA 门禁均通过，脚本没有输出密码、JWT 密钥或 MFA 设置令牌。

## Android 回归

`scripts/Test-W1Capabilities.ps1` 在 Android 模拟器上再次通过 4 个场景：SignalR、本机 WebRTC、相机被系统拒绝后的错误处理、离线中文 TTS。脚本只向 API 子进程传入测试配置，结束后恢复进程环境。

当前仍有两项非阻塞工具警告：`flutter_tts` 和 `flutter_webrtc` 仍使用旧式 Kotlin Gradle Plugin 接入；JDK 提示 Gradle 原生库调用未来需要显式开放。当前 APK 构建和 4 个 Android 场景均通过，后续升级插件时处理。
