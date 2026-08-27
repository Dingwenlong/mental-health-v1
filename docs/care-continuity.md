# 日常记录与回访跟进

本轮面向成年用户的本地比赛演示。保留手机号短信登录、可选联系邮箱、咨询、报告、回访与原有安全提示。新增资料不进入风险评分，不作诊断或疗效判断。没有增加真实 AI、支付、短信发送、自动风险升级或云服务。

## 页面入口

- **今天**：记录或修改今天的心情、睡眠和备注；查看练习、计划和回访。
- **咨询**：查看本人咨询，直接进入已有会话或报告；服务目录仍可进入。
- **记录**：日常记录的修改、删除，7/30 天趋势，练习记录和报告列表。
- **我的**：联系邮箱、资料共享、回访、导出和清除资料。
- **管理端**：医生可查看本人回访用户、档案、跟进计划、既有重点观察与随访；咨询师从本人咨询列表进入；运营仅看汇总及原有配置、审计入口。

练习为原创简短中文说明，包括把注意力放回身边、停下来片刻和做一件小事。可以计时，也可随时停止；停止不写入完成记录。内容方向参考 [WHO《Doing What Matters in Times of Stress》](https://www.who.int/publications-detail-redirect/9789240003927)，没有复制插图，也不承诺治疗效果。

## 数据和权限

日常日期统一按 UTC+8 计算。每人每天一条，心情 1–5、睡眠 0–24 小时且最多一位小数、备注最多 500 字。过去日期可补记，不能记录未来日期。缺失日期的心情和睡眠返回 `null`，趋势不插值。练习完成记录使用客户端生成的 UUID 去重。

日常资料默认仅本人可见。授权对象必须是当前负责未结束回访的在职医生，授权说明包含历史及之后的记录。授权绑定回访及分派版本；撤回会撤销对这位医生的全部有效授权，改派后原授权失效，即使之后改派回原医生也必须重新授权。医生档案只汇总本人负责回访所关联的报告、复核与计划；日常资料须有有效授权。咨询师、运营不能读取日常资料。

计划在既有回访下建立，当前负责人可操作；一个回访最多一个草稿或进行中计划。每个计划 1–30 项任务，到期日期在今天至未来 90 天内。状态为 `Draft → Active → Completed`，草稿或进行中可取消。发布后不能改写原内容，调整需要取消并重建。创建键、数据库唯一索引和版本校验防止重复或并发覆盖；草稿不向用户展示。

既有回访的改期、改派、取消和完成接口也校验已分派回访的当前负责人，防止通过改派他人的回访取得档案或计划权限。改派成功后，原负责人不能继续修改该回访；后续操作须由新负责人发起。尚未分派的回访保留原有安排流程。

用户可将任务标为完成或跳过，提交前须确认反馈说明。全部任务处理完后计划完成，**不会自动完成回访**。回访结束不自动撤销已经发布的计划，仍可提交未处理任务的反馈；负责人可取消计划。反馈和任务状态属于回访资料；撤回日常共享后仍保留，但医生不能通过计划接口读到日常备注。记录和练习内容不会自动复制进反馈。

新增数据纳入本人导出包 `care.json` 和清除流程。未发布草稿不出现在用户导出中，清除时仍一起删除。新增审计仅写操作和资源标识，不含备注、计划标题或反馈正文。新增页面不使用公共通知广播；进入、返回和手动刷新时重新查询，管理端刷新失败或权限撤回时不会保留旧私人内容。

## 接口

所有路径前缀均为 `/api/v1`。列表通常使用 `page`（从 1 开始）和 `pageSize`（1–100），返回 `items/total/pageNumber/pageSize`。趋势、固定练习目录和可授权回访候选为数组。本人身份和角色来自认证声明。

| 接口 | 用途 |
| --- | --- |
| `GET /account/me` | 当前账号身份和角色 |
| `GET /me/check-ins` | 分页日常记录，可选 `from/to` 日期 |
| `PUT, DELETE /me/check-ins/{date}` | 保存或删除本人当天记录；修改传 `version` |
| `GET /me/trends?days=7或30` | 心情、睡眠和练习次数趋势 |
| `GET /exercises` | 三项练习说明与时长 |
| `GET, POST /me/exercise-completions` | 查询或记录练习完成，提交 `id/exerciseId` |
| `GET /me/sharing-grants/candidates` | 当前可授权的回访医生 |
| `GET, POST /me/sharing-grants` | 查询或授权，提交 `followUpId/acknowledged` |
| `DELETE /me/sharing-grants/{id}` | 撤回对该医生的日常共享 |
| `GET /consultations`, `GET /results` | 本人或本人负责的咨询，支持状态、起止时间与分页 |
| `GET /clinical/subjects`, `GET /clinical/subjects/{id}` | 医生回访用户及档案 |
| `GET, POST /care-plans` | 查询可见计划或建立草稿 |
| `GET, PUT /care-plans/{id}` | 详情或修改草稿；修改传 `version` |
| `POST /care-plans/{id}/publish或cancel` | 发布或取消 |
| `POST /care-plans/{id}/tasks/{taskId}/feedback` | 提交 `Done/Skipped`、可选反馈及确认标记 |
| `GET /workspace/summary` | 按角色返回允许的计数，不返回私人正文 |

创建计划提交 `followUpId/title/idempotencyKey/tasks`；任务包含 `kind`（`CheckIn/Exercise`）、`exerciseId`（记录任务为 null）、`dueDate`。无权限为 403，不存在为 404，并发或重复冲突为 409，业务校验为 422；请求绑定校验沿用框架 400 ProblemDetails。

咨询列表按咨询状态筛选；报告列表按分析状态（`NotRequested/Pending/Ready/Processing/NeedsManual/Completed`）筛选。日期筛选针对咨询时间，移动端将所选起止日期按 UTC+8 转为全天范围。

## 迁移和验证

迁移 `20260827111107_AddCareContinuity` 新增五张表，并给回访增加 `AssignmentVersion`。不删除或改写原账号、咨询、报告。先在隔离测试库应用；不要直接指向已有演示库或生产库。

```powershell
dotnet restore MentalHealth.slnx --ignore-failed-sources
dotnet build MentalHealth.slnx --no-restore
dotnet test MentalHealth.slnx --no-build
npm --prefix apps/admin_web test -- --run
npm --prefix apps/admin_web run build
. ./scripts/Use-Toolchain.ps1
Push-Location apps/mobile_flutter
flutter analyze
flutter test --concurrency=1
flutter build apk --debug
Pop-Location
```

默认集成测试仍使用 PostgreSQL 17、Redis 8 Testcontainers。新增接口测试也支持通过进程环境变量 `MH_CARE_TEST_POSTGRES`、`MH_CARE_TEST_REDIS` 指向独立临时实例：必须是回环地址、非默认端口，数据库名称以 `mental_health_care_test_` 开头。每次测试使用新库，避免旧合成账号重复；外部实例由启动者清理。Redis 5 不能代替 Redis 8 的短信回归。

## 演示及端到端测试步骤

1. 启动独立 PostgreSQL、Redis 和本地 API，使用临时 JWT 密钥、合成账号、独立对象存储目录。API 设置 `Database__InitializeOnStartup=true`、`IdentitySeed__Enabled=true`、`CatalogSeed__Enabled=true`，关闭 `PhoneLogin__Aliyun__Enabled`。不要复用真实短信配置。
2. 沿用 `scripts/LocalTestJwt.psm1` 的受控测试凭据方式。在合成账号下通过已有接口确认咨询授权（与移动端一致使用 `textVersion=ui-copy-v1`）、创建模拟订单和咨询；为医生准备空闲时段。完成咨询并提交合成手工转写。已有不同版本的授权须通过撤回后重新同意，不直接改写历史授权。
3. 启动现有分析进程，确认转写进入就绪状态。当前后台没有自动调用评分阶段。为本地验收调用测试工具：`dotnet run --project tests/MentalHealth.DemoScenario -- --synthetic-input <咨询ID>`。它只接受回环地址上指定名称前缀的独立测试库，以固定合成文本模态输入 90 调用既有 `ScoreAssessmentStage`，生成持久化报告和回访；它不是新评分模型，也不会被 API 或后台加载。所需连接和对象目录通过该子进程环境变量提供。
4. Android：今天记录 → 我的咨询 → 进入会话/报告 → 我的 → 资料共享，明确勾选后授权回访医生。未授权前医生档案不能显示日常记录。
5. 管理端医生：回访用户 → 档案 → 制定计划，填写记录或练习任务与日期，先保存草稿，再发布。用户看到进行中计划。
6. Android：打开计划，可直接进入记录或练习；完成或跳过并确认反馈说明后提交。医生刷新可读执行状态，仍需通过原有随访入口结束回访。
7. 重启 API 后，双方重新打开页面确认记录、计划与反馈仍在。撤回共享后刷新已打开医生档案，日常资料消失但已提交的计划反馈保留。运营直接调用私人接口应得到 403。最后清除合成用户资料，列表、趋势与导出均不再包含新增数据。

自动页面测试分阶段配合以上步骤，凭据文件只放系统临时目录：

```powershell
# Android 临时定义文件：API_BASE_URL（以 /api/v1/ 结尾）、USER_ACCESS_TOKEN、CARE_PHASE。
# CARE_PHASE 顺序为 record、feedback、readback；record 前需准备一条未开始 AI 咨询和一份关联回访报告。
flutter test integration_test/care_continuity_test.dart -d emulator-5554 --dart-define-from-file=<临时定义文件>

# 管理端进程环境：CARE_WEB_URL、CARE_API_URL、CARE_DOCTOR_TOKEN、CARE_USER_TOKEN、CARE_OPERATIONS_TOKEN。
# CARE_PHASE=publish 在 Android record 后执行；readback 在 Android feedback 及 API 重启后执行，会撤回授权。
npx playwright test --config playwright.care.config.ts
```

这些测试不调用短信服务。测试 APK 含临时凭据，不得作为交付 APK；运行结束后删除测试构建，并重新执行无测试定义的 `flutter build apk --debug`。
