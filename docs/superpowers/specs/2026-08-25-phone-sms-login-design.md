# 手机号短信登录设计

日期：2026-08-25
状态：已确认

## 改动范围

管理端和 Android 都改用手机号短信登录。用户不再输入密码，系统也不再要求绑定验证器。手机号必须由管理员预先写入账号，V1 不提供注册和修改手机号功能。

登录顺序固定为：输入手机号，完成人机验证，发送短信验证码，核验验证码，签发登录令牌。

邮箱改为可选联系信息。它不能用于登录或找回账号，也不做邮件验证。登录后可以填写、修改或清空邮箱。

V1 不做国际手机号、密码找回、邮箱验证码、账号注册、手机号换绑和其他短信供应商。

## 页面

页面只有一张表单。初始状态显示 11 位手机号输入框和“获取验证码”按钮，不显示 `+86`、密码或验证器相关内容。

用户点击“获取验证码”后，客户端启动阿里云验证码 2.0。无风险时使用无痕验证，需要二次确认时显示“一点即过”。验证通过后，API 才能调用短信认证服务。

短信发送后，手机号输入框锁定，同一张表单增加 6 位数字输入框和“登录”按钮。60 秒内不能再次发送。页面不增加分步导航、返回入口、动画或说明面板。

可见提示保持简短：

- 发送后显示“如果该手机号已登记，你会收到验证码”。
- 人机验证失败显示“未完成安全验证，请重试”。
- 验证码错误、过期或账号不存在都显示“验证码无效或已过期”。
- 触发发送限制时显示还需等待的秒数。
- 阿里云接口不可用时停止登录，正式环境不得改用固定验证码或跳过验证。

## 阿里云验证码 2.0

客户端使用 V3 架构。固定配置如下：

| 配置 | 管理端 | Android |
| --- | --- | --- |
| `prefix` | `xfkdn8` | `xfkdn8` |
| `SceneId` | `1lae8yfm` | `e20maaxh` |
| 接入方式 | Web/H5 | Webview+H5 |
| 验证码形态 | 无痕验证 | 无痕验证 |
| 二次挑战 | 一点即过 | 一点即过 |

API 使用 `ekey` 为场景 ID 生成短时有效的 `EncryptedSceneId`。明文格式为 `sceneId&timestamp&expireTime`，有效期为 300 秒。加密使用 AES-256-CBC、PKCS7 填充和每次新生成的 16 字节随机 IV。返回值是 `Base64(IV + 密文)`。`ekey` 只存在于 API 的私密配置中。

管理端按阿里云要求动态加载验证码脚本。Android 在 WebView 中打开项目自己的 H5 验证页面，通过受限的 JavaScript 通道把 `CaptchaVerifyParam` 交回 Flutter。WebView 只允许验证码页面和阿里云验证码资源，不允许任意跳转；完成或关闭后立即销毁。

服务端必须调用 `VerifyIntelligentCaptcha`。只有 `VerifyResult=true` 才能继续发送短信。前端成功回调不能代替服务端验签。

## 短信认证

短信使用阿里云号码认证服务，不使用传统短信服务，也不创建融合认证方案。服务端调用 `SendSmsVerifyCode` 和 `CheckSmsVerifyCode`。签名和模板使用号码认证控制台提供的配套资源。

发送参数固定为：

- `CountryCode=86`
- `CodeLength=6`
- `CodeType=1`，只生成数字
- `ValidTime=300`
- `Interval=60`
- `DuplicatePolicy=1`，新验证码覆盖旧验证码
- `ReturnVerifyCode=false`
- `TemplateParam` 使用 `##code##`，由阿里云生成验证码
- `OutId` 使用本次登录挑战编号

服务端不生成、不接收也不记录验证码明文。核验以 `CheckSmsVerifyCode` 返回的 `Model.VerifyResult=PASS` 为准。

## 账号数据

`AppUser.PhoneNumber` 保存统一后的 `+86` 手机号。数据库为非空手机号建立唯一索引，一个手机号只能对应一个账号。`UserName` 同步使用同一个手机号，业务授权仍按用户 ID、角色、`SubjectId` 和 `PractitionerId` 判断。

`Email` 允许为空，`EmailConfirmed` 固定为 `false`。JWT 不再要求邮箱，改为包含用户 ID、手机号、角色以及现有的业务主体编号。

现有两个测试账号继续保留原邮箱作为联系邮箱：

| 账号 | 原邮箱 | 角色 |
| --- | --- | --- |
| 管理端 | `123@qq.com` | `OperationsAdmin` |
| 客户端 | `abc@qq.com` | `User` |

两个手机号从本机私密配置读取，不能写入源码、迁移、日志、截图或 Git 历史。两个号码必须不同，并已在阿里云短信认证快速测试中绑定。

EF Core 迁移只增加索引，不读取本机配置。API 启动时运行一次可重复执行的账号升级程序：先确认两个测试手机号都已提供、格式正确且彼此不同，再在一个数据库事务中写入手机号并清除密码哈希、验证器密钥、恢复码和双重验证状态。任一检查失败时不修改账号，并让启动失败。原 `/auth/login` 和 `/auth/mfa/setup` 停止提供服务。第一次短信验证成功时设置 `PhoneNumberConfirmed=true`。

## API

保留 controller-based API 和 ProblemDetails 错误格式。

### `POST /api/v1/auth/captcha/bootstrap`

请求包含 `phoneNumber` 和 `client`。服务端先统一手机号格式并在数据库内部查询账号，再为存在和不存在的账号生成相同格式的预登录记录。响应包含一次性 `preChallengeToken`、`prefix`、`encryptedSceneId` 和过期时间。服务端按 `client` 选择固定场景，不接受客户端传入任意场景 ID，也不返回账号是否存在。

必需配置在 API 启动时校验，缺项时启动失败。阿里云运行中暂时不可用时返回服务不可用，不返回 `ekey`。

### `POST /api/v1/auth/sms/challenges`

请求包含 `preChallengeToken` 和 `captchaVerifyParam`。服务端读取预登录记录，执行发送限制并完成阿里云人机验签。验签通过后，服务端为存在和不存在的账号创建相同格式的登录挑战，并把发送任务写入 Redis 队列。

两种情况都返回同样的 `202 Accepted` 响应：一次性 `challengeToken`、验证码过期时间和再次发送时间。挑战编号使用密码学安全随机数，不能按顺序猜测。API 内的后台发送服务读取任务，账号存在时调用 `SendSmsVerifyCode`，账号不存在时丢弃任务。HTTP 响应不等待短信供应商返回，避免通过响应时间查询账号。

### `POST /api/v1/auth/sms/verify`

请求包含 `challengeToken` 和 6 位 `code`。服务端最多允许 5 次核验。挑战已过期、验证码错误、账号不存在和次数用尽都返回相同的 `INVALID_SMS_CODE`。

账号存在时调用 `CheckSmsVerifyCode`。核验通过后删除挑战，标记手机号已确认并签发现有 API JWT。重复提交同一挑战不能再次取得令牌。所有失败响应至少等待 800 毫秒，再增加 0 至 200 毫秒随机延迟，降低通过核验耗时区分账号的风险。

### `GET /api/v1/account/contact-email`

要求已登录。返回当前账号的可空联系邮箱，供客户端刷新后显示现值。接口不返回手机号之外的登录凭据，也不触发邮件验证。

### `PUT /api/v1/account/contact-email`

要求已登录。请求中的 `email` 可以是合法邮箱或 `null`。接口只更新当前账号，不改变登录标识，不发送邮件，也不把邮箱标记为已确认。

## 临时状态和发送限制

预登录记录、登录挑战和短信发送队列都放在 Redis。预登录记录和登录挑战保存 5 分钟。记录只包含随机编号、规范手机号、可空用户 ID、客户端类型、发送时间、过期时间和失败次数。Redis 不可用时停止登录。

限制按规范手机号和来源 IP 同时计算：

- 同一手机号 60 秒内只能发送一次。
- 同一手机号每小时最多 5 次，每天最多 10 次。
- 同一 IP 每分钟最多 10 次，每天最多 100 次。

预登录接口对同一 IP 每分钟最多接受 30 次请求。短信发送限流在预登录记录中的账号结果分支之前执行。不存在的手机号也产生同样的预登录和登录挑战响应，避免通过响应内容、状态码或明显的时间差查询账号。

## 配置和权限

以下值只能来自开发机的 Secret Manager、被 Git 忽略的本机环境或部署平台的密钥存储：

- 验证码 `ekey`
- 阿里云 RAM AccessKey ID 和 AccessKey Secret
- 短信认证签名、模板 Code
- 管理端和客户端测试手机号

`prefix` 和两个场景 ID 可以进入普通配置。API 启动时验证必需配置，缺项直接报错。RAM 用户只保留验证码服务端验签、`SendSmsVerifyCode` 和 `CheckSmsVerifyCode` 所需权限，不使用主账号 AccessKey。

日志只记录请求关联号、客户端类型、结果码和耗时。完整手机号、邮箱、验证码、`CaptchaVerifyParam`、AccessKey、`ekey`、登录挑战令牌和 JWT 都不能写入日志。

## 测试与验收

自动化测试使用替代的阿里云客户端，不调用真实短信。测试内容包括：

- 场景映射和 `EncryptedSceneId` 加密格式。
- 手机号规范化和唯一索引。
- 人机验签失败时不发送短信。
- 存在和不存在的手机号返回相同响应。
- 60 秒、小时和每天的发送限制。
- 错误码、过期码、第五次失败和重复核验。
- 成功登录后的手机号、角色和业务主体声明。
- 联系邮箱只能修改当前账号。
- 管理端和 Android 的表单状态及错误提示。

仓库内检查包括 .NET 单元与集成测试、管理端测试与生产构建、Flutter 测试、静态分析和 Android Debug APK 构建。

真实验收使用阿里云测试场景和两个已绑定手机号。管理端完成一次短信登录和页面检查。Android 16 模拟器完成一次验证码 WebView、短信接收、验证码核验和登录。真实短信内容、手机号和验证码不写入测试报告。

## 参考资料

- [阿里云验证码 2.0 接入指引](https://help.aliyun.com/zh/captcha/captcha2-0/user-guide/quick-start)
- [V3 加密模式](https://help.aliyun.com/zh/captcha/captcha2-0/user-guide/encrypted-mode-access-boot)
- [Android V3 接入](https://help.aliyun.com/zh/captcha/captcha2-0/user-guide/android-access-v3-architecture)
- [发送短信验证码](https://help.aliyun.com/zh/pnvs/developer-reference/api-dypnsapi-2017-05-25-sendsmsverifycode)
- [核验短信验证码](https://help.aliyun.com/zh/pnvs/developer-reference/api-dypnsapi-2017-05-25-checksmsverifycode)
