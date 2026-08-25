# 手机号短信登录 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 管理端和 Android 停止使用邮箱、密码和 TOTP，统一改成“手机号 → 验证码 2.0 → 阿里云短信验证码 → 登录”，并允许登录后填写可空联系邮箱。

**Architecture:** ASP.NET Core 保留 controller-based API。PostgreSQL 保存账号；Redis 保存短时预登录、短信挑战、原子限流和发送队列；阿里云官方 .NET SDK负责验证码服务端验签、短信发送和短信核验。管理端动态加载验证码 2.0 V3 脚本；Android 用受限 WebView 打开 API 托管的 H5 验证页。自动化测试只注入假的阿里云端口，不调用真实短信。

**Tech Stack:** .NET 10、ASP.NET Core Identity、EF Core 10、PostgreSQL 17、StackExchange.Redis 3、AlibabaCloud Captcha 20230305、AlibabaCloud Dypnsapi 20170525、Vue 3、Pinia、Vitest、Flutter 3.47、Dart 3.13、webview_flutter 4.14.1、xUnit、Testcontainers。

**Spec:** [docs/superpowers/specs/2026-08-25-phone-sms-login-design.md](../specs/2026-08-25-phone-sms-login-design.md)

## Global Constraints

- 只实现中国大陆手机号；输入接受 `13800138000` 或 `+8613800138000`，数据库统一保存 `+8613800138000`。
- 产品只开放两个手机号账号：`abc@qq.com` 对应普通用户，`123@qq.com` 对应管理端。真实手机号只进入被 Git 忽略的 `.env` 或 Secret Manager。
- `counselor@demo.local` 和 `doctor@demo.local` 只保留给既有自动化测试签发短时 JWT，不分配手机号，不提供可调用的绕过登录接口。
- 正式运行不允许固定短信码、不允许跳过人机验签、不允许按配置切换到假供应商。测试替身只能通过 `WebApplicationFactory.ConfigureTestServices` 注入。
- API 响应和日志不能包含账号是否存在、完整手机号、邮箱、短信码、`CaptchaVerifyParam`、AccessKey、`ekey`、挑战令牌或 JWT。
- `CaptchaVerifyParam` 原样交给阿里云 SDK；服务端同时传入预登录记录绑定的 `SceneId`。
- 客户端初始化同时传 `SceneId` 与 `EncryptedSceneId`。客户端不能控制服务端验签所用场景。
- UI 改动开始前重新读取 `avoid-ai-design`。页面只保留一张登录表单，不加步骤条、说明卡片、装饰图和返回手机号入口。
- 管理端渲染检查使用 `build-web-apps:frontend-testing-debugging`，优先用 Browser 插件；Android 最终验收只认 API 36 模拟器上的真实页面与真实短信流程。
- 每项先运行指定的 RED 测试，确认失败原因正确，再写最小代码。每次提交前运行 `git diff --check` 和秘密扫描。
- 计划内提交只提交到本地分支，不推送。

---

### Task 1: 固定手机号、JWT 和错误契约

**Files:**

- Create: `src/MentalHealth.Application/Security/PhoneNumberNormalizer.cs`
- Modify: `src/MentalHealth.Application/Security/IJwtTokenService.cs`
- Modify: `src/MentalHealth.Infrastructure/Identity/JwtTokenService.cs`
- Modify: `src/MentalHealth.Infrastructure/Identity/JwtOptions.cs`
- Modify: `src/MentalHealth.Contracts/Common/ApiProblemCodes.cs`
- Modify: `src/MentalHealth.Api/Authorization/Policies.cs`
- Create: `tests/MentalHealth.UnitTests/Security/PhoneNumberNormalizerTests.cs`
- Modify: C# call sites returned by `rg -l "JwtTokenScope|new JwtTokenSubject" tests src`

**Interfaces:**

```csharp
public static class PhoneNumberNormalizer
{
    public static bool TryNormalizeMainlandChina(string? value, out string normalized);
}

public sealed record JwtTokenSubject(
    Guid UserId,
    string PhoneNumber,
    IReadOnlyCollection<string> Roles,
    Guid? SubjectId,
    Guid? PractitionerId);

public interface IJwtTokenService
{
    IssuedJwtToken Issue(JwtTokenSubject subject);
}
```

- [ ] **Step 1: 写 RED 测试**

```csharp
public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("13800138000", "+8613800138000")]
    [InlineData("+8613800138000", "+8613800138000")]
    public void Mainland_number_is_normalized(string input, string expected)
    {
        Assert.True(PhoneNumberNormalizer.TryNormalizeMainlandChina(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1380013800")]
    [InlineData("+85291234567")]
    [InlineData("138 0013 8000")]
    public void Unsupported_number_is_rejected(string input)
    {
        Assert.False(PhoneNumberNormalizer.TryNormalizeMainlandChina(input, out _));
    }
}
```

- [ ] **Step 2: 运行 RED**

```powershell
dotnet test tests/MentalHealth.UnitTests/MentalHealth.UnitTests.csproj --filter FullyQualifiedName~PhoneNumberNormalizerTests
```

Expected: 编译失败，因为规范化器尚不存在。

- [ ] **Step 3: 实现最小契约**

`TryNormalizeMainlandChina` 只接受 `^1[3-9]\d{9}$` 与 `^\+861[3-9]\d{9}$`。JWT 保留 `sub`、`ClaimTypes.NameIdentifier`、`scope=api`、角色、`subject_id`、`practitioner_id`，删除 email claim，增加 `phone_number`。删除 `JwtTokenScope.MfaSetup`、`Jwt:MfaSetupTokenMinutes` 和 `Policies.MfaSetup`。

- [ ] **Step 4: 增加 JWT 断言并运行 GREEN**

```powershell
dotnet test tests/MentalHealth.UnitTests/MentalHealth.UnitTests.csproj --filter FullyQualifiedName~PhoneNumberNormalizerTests
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter "FullyQualifiedName~AuthorizationMatrixTests|FullyQualifiedName~PhoneJwtTests"
```

Expected: 有 `phone_number`、没有 email、没有 `mfa_setup`，全部通过。

- [ ] **Step 5: 提交**

```powershell
git add src/MentalHealth.Application/Security src/MentalHealth.Infrastructure/Identity/JwtTokenService.cs src/MentalHealth.Infrastructure/Identity/JwtOptions.cs src/MentalHealth.Contracts/Common/ApiProblemCodes.cs src/MentalHealth.Api/Authorization/Policies.cs tests
git diff --cached --check
git commit -m "refactor: prepare phone based identity tokens"
```

---

### Task 2: 升级两个可登录账号并建立唯一手机号索引

**Files:**

- Modify: `src/MentalHealth.Infrastructure/Identity/AppUser.cs`
- Modify: `src/MentalHealth.Infrastructure/Identity/IdentitySeeder.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/PhoneLoginAccountOptions.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/PhoneLoginAccountUpgrader.cs`
- Modify: `src/MentalHealth.Infrastructure/Persistence/MentalHealthDbContext.cs`
- Modify: `src/MentalHealth.Infrastructure/DependencyInjection.cs`
- Modify: `src/MentalHealth.Api/Program.cs`
- Create: EF-generated `AddPhoneLoginIndex` migration under `src/MentalHealth.Infrastructure/Persistence/Migrations/`
- Modify: `src/MentalHealth.Infrastructure/Persistence/Migrations/MentalHealthDbContextModelSnapshot.cs`
- Modify: `tests/MentalHealth.IntegrationTests/Auth/AuthApiFixture.cs`
- Create: `tests/MentalHealth.IntegrationTests/Auth/PhoneLoginAccountUpgradeTests.cs`
- Modify: test email references returned by `rg -l "user@demo.local|admin@demo.local" tests --glob "*.cs"`

**Configuration:**

```text
PhoneLogin:Accounts:ClientPhone
PhoneLogin:Accounts:AdminPhone
```

测试 fixture 固定使用 `13800138001` 和 `13900139002`；正式值来自私密配置。

- [ ] **Step 1: 写账号升级 RED 测试**

```csharp
[Fact]
public async Task Startup_upgrades_public_accounts_without_password_or_totp()
{
    await using var scope = fixture.Services.CreateAsyncScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var client = await users.FindByEmailAsync("abc@qq.com");
    var admin = await users.FindByEmailAsync("123@qq.com");

    Assert.Equal("+8613800138001", client!.PhoneNumber);
    Assert.Equal(client.PhoneNumber, client.UserName);
    Assert.Null(client.PasswordHash);
    Assert.False(client.TwoFactorEnabled);
    Assert.False(client.EmailConfirmed);
    Assert.Equal("+8613900139002", admin!.PhoneNumber);
    Assert.Null(admin.PasswordHash);
}
```

另加三个启动测试：两个号码相同、号码格式错误、缺少一个号码。三种情况都使启动失败，并确认没有只升级一个账号。

- [ ] **Step 2: 运行 RED**

```powershell
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter FullyQualifiedName~PhoneLoginAccountUpgradeTests
```

Expected: 找不到新邮箱，或账号仍有密码和 TOTP 状态。

- [ ] **Step 3: 改造 seeder 和事务升级器**

`IdentitySeeder` 不再读取初始密码，改用 `UserManager.CreateAsync(user)`。普通用户和管理端主邮箱改为 `abc@qq.com`、`123@qq.com`；旧库只存在 `user@demo.local` / `admin@demo.local` 时，查到同一行后改名，不创建重复账号。咨询师和医生保留为无密码、无手机号的测试身份。

升级器在事务前规范化两个号码并检查不同。事务内锁定两个用户，写入手机号和相同的 UserName，设置 `EmailConfirmed=false`、`PhoneNumberConfirmed=false`、`TwoFactorEnabled=false`、`RequiresMfa=false`，清空 `PasswordHash`、`AuthenticatorKey`，删除验证器和恢复码 token；任一步失败都回滚。

- [ ] **Step 4: 建迁移**

```csharp
modelBuilder.Entity<AppUser>()
    .HasIndex(user => user.PhoneNumber)
    .IsUnique()
    .HasDatabaseName("PhoneNumberIndex")
    .HasFilter("\"PhoneNumber\" IS NOT NULL");
```

```powershell
dotnet ef migrations add AddPhoneLoginIndex --project src/MentalHealth.Infrastructure --startup-project src/MentalHealth.Api
dotnet ef migrations script --idempotent --project src/MentalHealth.Infrastructure --startup-project src/MentalHealth.Api --output "$env:TEMP\mental-health-phone-login.sql"
```

检查 SQL 只增加过滤唯一索引和快照变化，不读取配置、不写真实手机号。

- [ ] **Step 5: 接入启动顺序**

固定为：`MigrateAsync` → `IdentitySeeder.SeedAsync` → `PhoneLoginAccountUpgrader.UpgradeAsync` → `DemoCatalogSeeder.SeedAsync`。

- [ ] **Step 6: 运行 GREEN**

```powershell
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter "FullyQualifiedName~PhoneLoginAccountUpgradeTests|FullyQualifiedName~MigrationAndRedisTests"
```

Expected: 重复启动不产生重复用户或迁移错误。

- [ ] **Step 7: 提交**

```powershell
git add src/MentalHealth.Infrastructure/Identity src/MentalHealth.Infrastructure/Persistence src/MentalHealth.Infrastructure/DependencyInjection.cs src/MentalHealth.Api/Program.cs tests/MentalHealth.IntegrationTests
git diff --cached --check
git commit -m "feat: upgrade demo accounts for phone login"
```

---

### Task 3: 接入阿里云验证码和短信认证端口

**Files:**

- Modify: `src/MentalHealth.Infrastructure/MentalHealth.Infrastructure.csproj`
- Modify: `src/MentalHealth.Infrastructure/packages.lock.json`
- Create: `src/MentalHealth.Application/Security/ICaptchaVerifier.cs`
- Create: `src/MentalHealth.Application/Security/ISmsVerificationProvider.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/AliyunPhoneLoginOptions.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/EncryptedSceneIdFactory.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/AliyunCaptchaVerifier.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/AliyunSmsVerificationProvider.cs`
- Modify: `src/MentalHealth.Infrastructure/DependencyInjection.cs`
- Create: `tests/MentalHealth.UnitTests/Security/EncryptedSceneIdFactoryTests.cs`
- Create: `tests/MentalHealth.UnitTests/Security/AliyunPhoneLoginOptionsTests.cs`

**Pinned packages:**

```xml
<PackageReference Include="AlibabaCloud.SDK.Captcha20230305" Version="1.1.4" />
<PackageReference Include="AlibabaCloud.SDK.Dypnsapi20170525" Version="2.0.0" />
```

**Ports:**

```csharp
public interface ICaptchaVerifier
{
    Task<bool> VerifyAsync(
        string sceneId,
        string captchaVerifyParam,
        CancellationToken cancellationToken);
}

public interface ISmsVerificationProvider
{
    Task SendAsync(string nationalPhoneNumber, string outId, CancellationToken cancellationToken);
    Task<bool> CheckAsync(
        string nationalPhoneNumber,
        string outId,
        string code,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: 写 RED 测试**

用 32 字节 Base64 测试 ekey 解开工厂输出，断言前 16 字节是 IV，明文为 `sceneId&unixSeconds&300`，相同输入连续两次输出不同。选项测试断言 prefix、两个 scene、ekey、AK、签名、模板任一为空都验证失败。

- [ ] **Step 2: 运行 RED**

```powershell
dotnet test tests/MentalHealth.UnitTests/MentalHealth.UnitTests.csproj --filter "FullyQualifiedName~EncryptedSceneIdFactoryTests|FullyQualifiedName~AliyunPhoneLoginOptionsTests"
```

Expected: 编译失败，因为工厂和选项尚不存在。

- [ ] **Step 3: 安装 SDK 并实现加密**

```powershell
dotnet add src/MentalHealth.Infrastructure/MentalHealth.Infrastructure.csproj package AlibabaCloud.SDK.Captcha20230305 --version 1.1.4
dotnet add src/MentalHealth.Infrastructure/MentalHealth.Infrastructure.csproj package AlibabaCloud.SDK.Dypnsapi20170525 --version 2.0.0
dotnet restore MentalHealth.slnx
dotnet restore MentalHealth.slnx --locked-mode
```

加密使用 `Aes.Create()`、256 位 key、CBC、PKCS7 和每次新生成的 16 字节 IV。ekey 先 Base64 解码并要求正好 32 字节。

- [ ] **Step 4: 实现适配器**

验证码 endpoint 固定 `captcha.cn-shanghai.aliyuncs.com`，调用 `VerifyIntelligentCaptchaAsync`，请求同时带 `SceneId` 和未经修改的 `CaptchaVerifyParam`；只接受 `Body.Result.VerifyResult == true`。

短信调用 `SendSmsVerifyCodeAsync` / `CheckSmsVerifyCodeAsync`。发送固定 `CountryCode=86`、6 位纯数字、有效期 300 秒、间隔 60 秒、覆盖旧码、不返回明文码、`TemplateParam={"code":"##code##","min":"5"}`、挑战 ID 作为 `OutId`。核验要求 `Body.Code == "OK"` 且 `Body.Model.VerifyResult == "PASS"`。

SDK 异常统一包装为 `PhoneLoginProviderException`，日志只允许请求 ID、结果码、客户端类型和耗时。

- [ ] **Step 5: 注册和验证配置**

选项使用 `ValidateOnStart()`。正式运行始终注册真实适配器；测试随后用 `ConfigureTestServices` 显式替换，不能通过配置字符串启用假实现。

- [ ] **Step 6: 运行 GREEN**

```powershell
dotnet test tests/MentalHealth.UnitTests/MentalHealth.UnitTests.csproj --filter "FullyQualifiedName~EncryptedSceneIdFactoryTests|FullyQualifiedName~AliyunPhoneLoginOptionsTests"
dotnet build MentalHealth.slnx --no-restore
```

- [ ] **Step 7: 提交**

```powershell
git add src/MentalHealth.Application/Security src/MentalHealth.Infrastructure tests/MentalHealth.UnitTests/Security
git diff --cached --check
git commit -m "feat: add Aliyun phone verification adapters"
```

---

### Task 4: 用 Redis 保存预登录、挑战、限流和异步队列

**Files:**

- Create: `src/MentalHealth.Application/Security/PhoneLoginModels.cs`
- Create: `src/MentalHealth.Application/Security/ILoginChallengeStore.cs`
- Create: `src/MentalHealth.Infrastructure/Identity/RedisLoginChallengeStore.cs`
- Create: `src/MentalHealth.Api/Services/SmsDispatchWorker.cs`
- Modify: `src/MentalHealth.Infrastructure/DependencyInjection.cs`
- Modify: `src/MentalHealth.Api/Program.cs`
- Create: `tests/MentalHealth.IntegrationTests/Auth/RedisLoginChallengeStoreTests.cs`
- Create: `tests/MentalHealth.IntegrationTests/Auth/FakeSmsVerificationProvider.cs`

**Redis keys:**

```text
auth:pre:{sha256-token}
auth:challenge:{sha256-token}
auth:verify-lock:{challenge-id}
auth:rate:phone:60s:{sha256-phone}
auth:rate:phone:hour:{sha256-phone}
auth:rate:phone:day:{sha256-phone}
auth:rate:ip:minute:{sha256-ip}
auth:rate:ip:day:{sha256-ip}
auth:rate:bootstrap:minute:{sha256-ip}
auth:sms:dispatch
```

客户端令牌用 32 字节随机数生成 Base64Url；Redis key 只用令牌 SHA-256。Redis 值允许保存规范手机号和可空用户 ID，但日志不能打印。

- [ ] **Step 1: 写 Redis RED 测试**

覆盖：300 秒过期；预登录一次性读取；手机号第 2/60 秒、第 6/小时、第 11/天被拒绝；IP 第 11/分钟、第 101/天被拒绝；bootstrap IP 第 31/分钟被拒绝；返回精确剩余秒数；挑战第 6 次核验被拒绝；并发核验只有一个取得租约；成功消费后不能重复消费；队列任务只有挑战 ID。

- [ ] **Step 2: 运行 RED**

```powershell
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter FullyQualifiedName~RedisLoginChallengeStoreTests
```

- [ ] **Step 3: 实现原子操作**

限流使用一段 Lua 一次检查并递增所有 key，首次设置 TTL；不能在 C# 中“先 GET 后 INCR”。核验 Lua 原子检查挑战、有效期、失败次数和 30 秒租约，再增加次数。阿里云失败释放租约；成功时 Lua 原子删除挑战与租约并返回可空用户 ID。

- [ ] **Step 4: 实现 Redis Stream worker**

API 只向 `auth:sms:dispatch` 写挑战 ID。worker 用固定 consumer group 读取并重新加载挑战；`UserId` 为空直接 ack，非空调用短信发送。暂时错误最多重试 3 次，最后记录脱敏错误并 ack。HTTP 请求不等待 worker。

- [ ] **Step 5: 运行 GREEN**

```powershell
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter FullyQualifiedName~RedisLoginChallengeStoreTests
```

- [ ] **Step 6: 提交**

```powershell
git add src/MentalHealth.Application/Security src/MentalHealth.Infrastructure/Identity/RedisLoginChallengeStore.cs src/MentalHealth.Api/Services/SmsDispatchWorker.cs src/MentalHealth.Infrastructure/DependencyInjection.cs src/MentalHealth.Api/Program.cs tests/MentalHealth.IntegrationTests/Auth
git diff --cached --check
git commit -m "feat: add Redis phone login challenges"
```

---

### Task 5: 替换认证 API 并增加联系邮箱接口

**Files:**

- Rewrite: `src/MentalHealth.Api/Controllers/AuthController.cs`
- Create: `src/MentalHealth.Api/Controllers/AccountController.cs`
- Modify: `src/MentalHealth.Contracts/Common/ApiProblemCodes.cs`
- Modify: `tests/MentalHealth.IntegrationTests/Auth/AuthApiFixture.cs`
- Delete: `tests/MentalHealth.IntegrationTests/Auth/LoginAndMfaTests.cs`
- Create: `tests/MentalHealth.IntegrationTests/Auth/PhoneSmsLoginTests.cs`
- Create: `tests/MentalHealth.IntegrationTests/Auth/ContactEmailTests.cs`
- Create: `tests/MentalHealth.IntegrationTests/Auth/FakeCaptchaVerifier.cs`
- Modify: `tests/MentalHealth.IntegrationTests/Security/SensitiveLoggingTests.cs`

**Routes:**

```text
POST /api/v1/auth/captcha/bootstrap
POST /api/v1/auth/sms/challenges
POST /api/v1/auth/sms/verify
GET  /api/v1/account/contact-email
PUT  /api/v1/account/contact-email
```

**Problem codes:**

```text
INVALID_PHONE_NUMBER        400
LOGIN_CHALLENGE_INVALID     400
CAPTCHA_FAILED              422
SMS_RATE_LIMITED            429
INVALID_SMS_CODE            401
AUTH_PROVIDER_UNAVAILABLE   503
CONTACT_EMAIL_INVALID       422
```

- [ ] **Step 1: fixture 注入测试替身**

fixture 的可登录号码固定为 `13800138001` 和 `13900139002`。`FakeCaptchaVerifier` 接受一个合成参数；`FakeSmsVerificationProvider` 为挑战保存测试码 `246810`。API 响应和日志不能出现测试码。

- [ ] **Step 2: 写 API RED 测试**

```csharp
[Fact]
public async Task Registered_client_completes_captcha_sms_and_receives_phone_jwt()
{
    var bootstrap = await fixture.BootstrapAsync("13800138001", "android");
    var challenge = await fixture.CreateChallengeAsync(
        bootstrap.PreChallengeToken,
        FakeCaptchaVerifier.ValidParam);
    await fixture.Sms.WaitUntilSentAsync(challenge.ChallengeId);

    using var response = await fixture.Client.PostAsJsonAsync(
        "/api/v1/auth/sms/verify",
        new { challengeToken = challenge.ChallengeToken, code = "246810" });

    response.EnsureSuccessStatusCode();
    var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
    Assert.Contains(jwt.Claims, claim =>
        claim.Type == "phone_number" && claim.Value == "+8613800138001");
    Assert.DoesNotContain(jwt.Claims, claim => claim.Type == "email");
}
```

同时覆盖：人机失败不排队；登记与未登记号码的 bootstrap/challenge 状态码、字段和长度相同；未登记号码不发短信；错误码、过期码、第 5 次失败、并发重复核验统一失败；成功设置 `PhoneNumberConfirmed=true`；阿里云或 Redis 不可用时返回 503 并停止登录。

- [ ] **Step 3: 运行 RED**

```powershell
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter FullyQualifiedName~PhoneSmsLoginTests
```

Expected: 新路由 404。

- [ ] **Step 4: 实现 bootstrap**

规范化手机号、执行 IP 限流、始终查询数据库、写入相同结构的预登录。`client` 只接受 `admin` / `android` 并选择固定场景。返回一次性 token、`xfkdn8`、短时 `EncryptedSceneId` 和 UTC 过期时间，不返回账号状态。

- [ ] **Step 5: 实现 challenge 和 verify**

challenge 一次性取预登录，做手机号/IP 原子限流，再调用验证码验签；通过后写挑战和队列，统一返回 `202 Accepted`、challenge ID/token、过期和重发时间。

verify 要求 6 位数字并取得租约。登记账号调用阿里云核验；未登记账号直接走统一失败延迟。所有失败补足 800ms 再随机增加 0–200ms。测试注入 `ILoginFailureDelay` 记录目标延迟但不实际等待。成功时原子消费挑战、确认手机号并签发 JWT。

- [ ] **Step 6: 实现联系邮箱**

两个接口都从 `sub` 取得当前用户。GET 返回 `{ "email": null }` 或当前联系邮箱。PUT 的 `email=null` 清空邮箱；非空 Trim 后用 `MailAddress` 校验且要求解析地址与输入一致。始终 `EmailConfirmed=false`，成功返回 204。测试证明只能读取和修改当前账号。

- [ ] **Step 7: 删除旧路由并测日志**

断言 `/auth/login`、`/auth/mfa/setup` 都是 404。用独特手机号、验证码、captcha 参数、挑战令牌请求新接口，断言日志完全不含这些值。

- [ ] **Step 8: 运行 GREEN**

```powershell
dotnet test tests/MentalHealth.IntegrationTests/MentalHealth.IntegrationTests.csproj --filter "FullyQualifiedName~PhoneSmsLoginTests|FullyQualifiedName~ContactEmailTests|FullyQualifiedName~SensitiveLoggingTests"
```

- [ ] **Step 9: 提交**

```powershell
git add src/MentalHealth.Api/Controllers src/MentalHealth.Contracts/Common/ApiProblemCodes.cs tests/MentalHealth.IntegrationTests/Auth tests/MentalHealth.IntegrationTests/Security/SensitiveLoggingTests.cs
git diff --cached --check
git commit -m "feat: replace password login with SMS verification"
```

---

### Task 6: 重做管理端单表单登录

**Required skills:** 先读取 `avoid-ai-design`；页面完成后读取 `build-web-apps:frontend-testing-debugging` 并用 Browser 插件检查真实渲染。

**Files:**

- Create: `apps/admin_web/src/features/auth/aliyunCaptcha.ts`
- Create: `apps/admin_web/src/features/auth/PhoneLoginForm.vue`
- Create: `apps/admin_web/src/features/account/contactEmailService.ts`
- Create: `apps/admin_web/src/features/account/ContactEmailView.vue`
- Rewrite: `apps/admin_web/src/stores/auth.ts`
- Modify: `apps/admin_web/src/App.vue`
- Modify: `apps/admin_web/src/style.css`
- Create: `apps/admin_web/src/tests/phone-login.spec.ts`
- Create: `apps/admin_web/src/tests/contact-email.spec.ts`
- Modify: `apps/admin_web/src/tests/catalog-flow.spec.ts`
- Modify: `content/zh-CN/ui-copy.v1.json`
- Regenerate: `apps/admin_web/src/generated/uiCopy.generated.ts`
- Regenerate: `apps/mobile_flutter/lib/generated/ui_copy.g.dart`

- [ ] **Step 1: 写管理端 RED 测试**

断言：初始只有 `login-phone` 和 `login-send-code`；没有密码、邮箱、TOTP 和固定 `+86`；发送顺序是 bootstrap → captcha runner → challenge；成功后手机号 disabled，同一表单出现 `login-sms-code` 和 `login-submit`；60 秒后才可重发；没有“换个手机号”；错误码显示已确认短句。

- [ ] **Step 2: 运行 RED**

```powershell
npm --prefix apps/admin_web test -- --run src/tests/phone-login.spec.ts
```

- [ ] **Step 3: 实现验证码 runner**

只加载一次 `https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js`，加载前设置：

```ts
window.AliyunCaptchaConfig = { region: 'cn', prefix: bootstrap.prefix }
```

初始化固定使用 `SceneId='1lae8yfm'`、bootstrap 的 `EncryptedSceneId`、popup、`language='cn'`、`delayBeforeSuccess=false`、`slideStyle={ width: 360, height: 40 }`。业务按钮完成手机号校验和 bootstrap 后调用 `startTracelessVerification()`；每次重发重新 bootstrap，不能复用验签参数。

- [ ] **Step 4: 实现最小表单**

手机号：`type=tel`、`inputmode=numeric`、`autocomplete=tel-national`、`maxlength=11`。短信码：`inputmode=numeric`、`autocomplete=one-time-code`、`maxlength=6`。发送成功显示“如果该手机号已登记，你会收到验证码”；倒计时只放按钮文字。

- [ ] **Step 5: 更新文案并生成**

删除旧密码/TOTP 文案，增加手机号、获取验证码、短信码、安全验证失败、统一无效码、发送提示、限流和供应商不可用文案。

```powershell
pwsh .\scripts\Generate-UiCopy.ps1
```

- [ ] **Step 6: 加最小账号设置入口**

在已登录管理端的现有左侧导航增加“账号设置”，页面只有当前联系邮箱输入框、“保存”和“清空”三个元素。进入时 GET 当前值；保存或清空后 PUT。页面明确写“联系邮箱不能用于登录”，不增加邮箱验证码、找回密码或手机号修改。

- [ ] **Step 7: GREEN 与构建**

```powershell
npm --prefix apps/admin_web test -- --run src/tests/phone-login.spec.ts src/tests/contact-email.spec.ts src/tests/catalog-flow.spec.ts
npm --prefix apps/admin_web run build
```

- [ ] **Step 8: 浏览器检查**

用 Browser 插件检查 1440×900 和 390×844：键盘顺序、按钮禁用、错误焦点、验证码弹层、倒计时和无横向滚动。截图只用合成测试号。

- [ ] **Step 9: 提交**

```powershell
git add apps/admin_web content/zh-CN/ui-copy.v1.json apps/mobile_flutter/lib/generated/ui_copy.g.dart
git diff --cached --check
git commit -m "feat: add admin phone verification login"
```

---

### Task 7: 实现 Android WebView 验证码和短信登录

**Required skill:** 重新读取 `avoid-ai-design`，保持与管理端相同的单表单结构。

**Files:**

- Modify: `apps/mobile_flutter/pubspec.yaml`
- Modify: `apps/mobile_flutter/pubspec.lock`
- Create: `src/MentalHealth.Api/wwwroot/captcha/mobile.html`
- Modify: `src/MentalHealth.Api/Program.cs`
- Create: `apps/mobile_flutter/lib/features/auth/aliyun_captcha_webview_page.dart`
- Create: `apps/mobile_flutter/lib/features/auth/captcha_runner.dart`
- Create: `apps/mobile_flutter/lib/core/account/contact_email_gateway.dart`
- Create: `apps/mobile_flutter/lib/features/account/contact_email_page.dart`
- Rewrite: `apps/mobile_flutter/lib/core/auth/auth_store.dart`
- Rewrite: `apps/mobile_flutter/lib/features/auth/login_page.dart`
- Modify: `apps/mobile_flutter/lib/features/catalog/catalog_page.dart`
- Modify: `apps/mobile_flutter/lib/features/home/patient_home_page.dart`
- Modify: `apps/mobile_flutter/lib/main.dart`
- Modify: `apps/mobile_flutter/android/app/src/main/AndroidManifest.xml`
- Rewrite: `apps/mobile_flutter/test/core/auth/auth_store_test.dart`
- Create: `apps/mobile_flutter/test/features/auth/login_page_test.dart`
- Create: `apps/mobile_flutter/test/features/auth/aliyun_captcha_webview_page_test.dart`
- Create: `apps/mobile_flutter/test/features/account/contact_email_page_test.dart`

- [ ] **Step 1: 写 Flutter RED 测试**

假的 `CaptchaRunner` 返回 `captcha-test-param`。测试两个表单阶段、手机号锁定、60 秒倒计时、错误映射和无返回入口。另测 WebView 顶层导航白名单：当前 API origin、`o.alicdn.com` 和验证码运行时实际使用的阿里云 HTTPS 域名允许，其他 URL 拒绝。

- [ ] **Step 2: 运行 RED**

```powershell
. .\scripts\Use-Toolchain.ps1
Push-Location apps/mobile_flutter
try {
    flutter test test/core/auth/auth_store_test.dart test/features/auth/login_page_test.dart test/features/auth/aliyun_captcha_webview_page_test.dart
} finally {
    Pop-Location
}
```

- [ ] **Step 3: 添加依赖和静态页**

```powershell
. .\scripts\Use-Toolchain.ps1
Push-Location apps/mobile_flutter
try {
    flutter pub add webview_flutter:^4.14.1
} finally {
    Pop-Location
}
```

API 加 `app.UseStaticFiles()`。H5 从 query 读取 prefix、固定 `SceneId=e20maaxh`、`EncryptedSceneId`，动态加载官方脚本。页面设置 `default-src 'none'` 的 CSP；脚本只允许 `https://o.alicdn.com`，验证码的连接、图片、样式、字体和 frame 只允许 HTTPS 的 `*.alicdn.com` 与 `*.aliyuncs.com`。成功只执行：

```javascript
window.MentalHealthCaptcha.postMessage(captchaVerifyParam)
```

失败、关闭、初始化错误只发送固定状态字符串，不发送阿里云原始对象。

- [ ] **Step 4: 实现受限 WebView**

Flutter 用 `Uri` 组装静态页地址。WebView 启用 JavaScript、禁用缓存、注册唯一 channel；顶层导航只允许当前 API origin 下的精确 `/captcha/mobile.html`，其余全部拒绝，阿里云子资源由 H5 的 CSP 控制。成功或关闭后立即移除页面并释放引用。不得新增 `usesCleartextTraffic=true`，阿里云资源必须 HTTPS。

- [ ] **Step 5: 实现 AuthStore 和页面**

gateway 提供 `bootstrapPhone`、`createSmsChallenge`、`verifySmsCode`。store 不保存密码、邮箱或短信码，只保存当前手机号、challenge token、倒计时和错误。页面 key 固定 `login-phone`、`login-send-code`、`login-sms-code`、`login-submit`；不显示 `+86` 和“换个手机号”。

- [ ] **Step 6: 加最小账号设置入口**

在客户端首页现有 AppBar 的退出按钮前增加“账号”图标，打开 `ContactEmailPage`。页面进入时 GET 当前联系邮箱，只提供邮箱输入、“保存”和“清空”，并写明“联系邮箱不能用于登录”。保存、清空和非法邮箱各有 widget test；不在底部导航增加第 5 项。

- [ ] **Step 7: GREEN、静态分析、APK**

```powershell
. .\scripts\Use-Toolchain.ps1
Push-Location apps/mobile_flutter
try {
    flutter test test/core/auth/auth_store_test.dart test/features/auth/login_page_test.dart test/features/auth/aliyun_captcha_webview_page_test.dart test/features/account/contact_email_page_test.dart
    flutter analyze
    flutter build apk --debug
} finally {
    Pop-Location
}
```

Expected: APK 位于 `apps/mobile_flutter/build/app/outputs/flutter-apk/app-debug.apk`。

- [ ] **Step 8: 提交**

```powershell
git add apps/mobile_flutter src/MentalHealth.Api/wwwroot/captcha/mobile.html src/MentalHealth.Api/Program.cs
git diff --cached --check
git commit -m "feat: add Android SMS login with Captcha WebView"
```

---

### Task 8: 修复既有测试脚本并跑全仓回归

**Files:**

- Create: `scripts/LocalTestJwt.psm1`
- Modify: `scripts/Initialize-LocalSecrets.ps1`
- Modify: `.env.example`
- Modify: `deploy/docker-compose.yml`
- Modify: `scripts/Test-LocalIdentity.ps1`
- Modify: `scripts/Test-Task7Android.ps1`
- Modify: `scripts/Test-Task9Android.ps1`
- Modify: `scripts/Test-Task11Video.ps1`
- Modify: `scripts/Test-Task13Android.ps1`
- Modify: `apps/mobile_flutter/integration_test/task7_catalog_flow_test.dart`
- Modify: `apps/mobile_flutter/integration_test/task9_realtime_chat_test.dart`
- Modify: `apps/mobile_flutter/integration_test/task11_video_test.dart`
- Modify: `apps/mobile_flutter/integration_test/task13_ai_consultation_test.dart`
- Modify: `README.md`
- Create: `docs/test-evidence/phone-sms-login.md`

**Private `.env` keys:**

```text
MH_ALIYUN_ACCESS_KEY_ID
MH_ALIYUN_ACCESS_KEY_SECRET
MH_ALIYUN_CAPTCHA_EKEY
MH_ALIYUN_SMS_SIGN_NAME
MH_ALIYUN_SMS_TEMPLATE_CODE
MH_CLIENT_PHONE
MH_ADMIN_PHONE
```

`.env.example` 只列 key，不写值。初始化脚本只生成数据库、JWT、证书随机密钥；阿里云和手机号留空并提示本机填写，不输出内容。

- [ ] **Step 1: 建 RED**

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalIdentity.ps1
```

Expected: 旧脚本调用已删除的 `/auth/login`，失败。

- [ ] **Step 2: 用短时测试 JWT 替代旧密码**

`LocalTestJwt.psm1` 只接受显式 user ID、角色、业务 ID 和本机 JWT key，签发 5 分钟 token，不落盘、不输出。Flutter 集成测试改收 `USER_ACCESS_TOKEN` / `COUNSELOR_ACCESS_TOKEN`，pump 前写入内存 storage 并调用 `auth.restore()`；不再调用 `ApiAuthGateway.login`。defines JSON 仍只写系统临时目录并在 finally 删除。

- [ ] **Step 3: 映射私密配置**

Docker Compose 和本机脚本把 `.env` key 映射到：

```text
PhoneLogin__Aliyun__AccessKeyId
PhoneLogin__Aliyun__AccessKeySecret
PhoneLogin__Aliyun__CaptchaEkey
PhoneLogin__Aliyun__SmsSignName
PhoneLogin__Aliyun__SmsTemplateCode
PhoneLogin__Accounts__ClientPhone
PhoneLogin__Accounts__AdminPhone
```

README 删除邮箱密码与医生 MFA 说明，改成手机号短信流程、Secret Manager 配置和真实验收命令。

- [ ] **Step 4: 生成文案并扫描旧实现**

```powershell
pwsh .\scripts\Generate-UiCopy.ps1
rg -n "abc123|MH_DEMO_INITIAL_PASSWORD|auth/login|auth/mfa/setup|login-email|login-password|login-totp|MFA_REQUIRED|INVALID_MFA_CODE" src apps tests scripts README.md content .env.example
```

Expected: 无旧登录实现残留。

- [ ] **Step 5: 全仓回归**

```powershell
dotnet restore MentalHealth.slnx --locked-mode
dotnet build MentalHealth.slnx --no-restore
dotnet test MentalHealth.slnx --no-build
npm --prefix apps/admin_web test -- --run
npm --prefix apps/admin_web run build
. .\scripts\Use-Toolchain.ps1
Push-Location apps/mobile_flutter
try {
    flutter analyze
    flutter test
    flutter build apk --debug
} finally {
    Pop-Location
}
git diff --check
```

若完整 `flutter test` 再次出现已知 `flutter_tester.exe` `0xc0000005`，保存原始错误，并单独证明三个 auth 测试和 Android APK 构建通过；不能把主机崩溃写成通过。

- [ ] **Step 6: 提交**

```powershell
git add .env.example deploy/docker-compose.yml scripts apps/mobile_flutter/integration_test README.md docs/test-evidence/phone-sms-login.md content apps/admin_web/src/generated apps/mobile_flutter/lib/generated
git diff --cached --check
git commit -m "test: update local flows for phone authentication"
```

---

### Task 9: 真实阿里云和 Android 16 验收

**Files:**

- Modify: `docs/test-evidence/phone-sms-login.md`

真实短信会计费。只有用户确认 `.env` 已填好、两个号码已绑定并允许发送后执行。文档只记录脱敏尾号、截断请求 ID、时间、HTTP 结果、模拟器名称和截图文件名，不记验证码或密钥。

- [ ] **Step 1: 启动依赖和 API**

```powershell
docker compose --env-file .env -f deploy/docker-compose.yml up -d
pwsh .\scripts\Test-LocalIdentity.ps1
```

确认缺少任一阿里云配置时 API 启动失败；补齐后 `/health` 为 200。禁止打印 `.env`。

- [ ] **Step 2: 管理端真实登录**

```powershell
npm --prefix apps/admin_web run dev
```

用 Browser 插件输入已绑定管理端手机号，完成人机验证，人工读取验证码并输入，确认进入管理首页。退出后确认同一 challenge 不能重复登录。

- [ ] **Step 3: Android 16 模拟器真实登录**

```powershell
. .\scripts\Use-Toolchain.ps1
adb devices
flutter emulators --launch MentalHealth_API_36
Push-Location apps/mobile_flutter
try {
    flutter run -d emulator-5554 --dart-define=API_BASE_URL=http://10.0.2.2:5165/api/v1/
} finally {
    Pop-Location
}
```

只允许一个 booted emulator。输入已绑定客户端手机号，确认 WebView、无痕验证或“一点即过”、短信接收、6 位码核验和客户端首页。

若阿里云返回 `F009`，在控制台把 Android 测试场景设为测试策略或关闭虚拟设备拦截后重试；不得改代码绕过。

- [ ] **Step 4: 检查枚举保护和限流**

用一个未登记的合成号码完成 bootstrap 和人机验证，确认响应结构相同且不发短信。只真实验证一次 60 秒限制；小时与每日上限由自动化测试证明，避免浪费短信额度。

- [ ] **Step 5: 证据和秘密扫描**

```powershell
git status --short
git diff --check
git grep -n -I -E "(AccessKeySecret|CaptchaEkey|MH_CLIENT_PHONE|MH_ADMIN_PHONE)=.+"
```

Expected: 无带值密钥被跟踪；证据不含完整手机号、短信码或 JWT。

- [ ] **Step 6: 提交证据**

```powershell
git add docs/test-evidence/phone-sms-login.md
git diff --cached --check
git commit -m "docs: record phone login acceptance"
```

---

## Final verification gate

宣布完成前读取并执行 `superpowers:verification-before-completion`，从干净工作树重跑 Task 8 全仓命令，并核对：

```powershell
git status --short --branch
git log --oneline --decorate -10
git grep -n -I -E "(123@qq\.com|abc@qq\.com|xfkdn8|1lae8yfm|e20maaxh)"
git grep -n -I -E "(AccessKeySecret|CaptchaEkey|MH_CLIENT_PHONE|MH_ADMIN_PHONE)=.+"
```

邮箱、prefix 和 scene ID 可以出现在源码或文档；真实手机号、ekey 和 AccessKey 值绝不能出现。最终报告本地 HEAD、各组测试真实结果、APK 绝对路径和未解决的主机或云端限制。只有用户再次明确要求时才 push。
