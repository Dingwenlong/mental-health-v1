# W2 本机基础设施验证

- 验证日期：2026-08-22
- 数据：只使用测试代码生成的人员 ID、状态和短文本
- 外部服务：无

## 固定版本

| 组件 | 版本 |
| --- | --- |
| EF Core / EF Design | 10.0.11 |
| Npgsql EF Provider | 10.0.3 |
| StackExchange.Redis | 3.1.31 |
| Testcontainers | 4.14.0 |
| PostgreSQL 镜像 | 17-alpine |
| Redis 镜像 | 8-alpine |

`dotnet-ef` 记录在仓库根目录 `dotnet-tools.json`，版本为 10.0.11，不使用预览版。

## 测试结果

```text
MentalHealth.UnitTests:        58/58 通过
MentalHealth.ContractTests:    64/64 通过
MentalHealth.IntegrationTests:  5/5  通过
Build: 0 警告，0 错误
NuGet: 未发现已知漏洞
```

其中本地对象存储测试为 23 条，覆盖读写删除、空文件摘要、幂等写入、内容冲突、路径穿越、取消令牌和临时文件清理。

PostgreSQL 与 Redis 集成测试使用一次性 Testcontainers。验证内容：

- 咨询完成状态和 Outbox 在同一事务提交。
- 数据库约束失败时，咨询和新 Outbox 一起回滚。
- 回访排程和两个领域事件可完整写入并读回。
- `Initial` 迁移可重建三张业务表。
- Redis 可写入、读回并删除一分钟过期的合成值。

## Compose 验证

固定端口在启动前均未占用：

```text
127.0.0.1:54329 PostgreSQL
127.0.0.1:56379 Redis
```

`docker compose config --quiet` 通过，两个容器状态为 healthy。迁移 `20260822094337_Initial` 已应用，读回表：

```text
__EFMigrationsHistory
consultation_sessions
follow_up_tasks
outbox_messages
```

第二次执行数据库更新返回 `No migrations were applied`，Redis 返回 `PONG`。

## RED 记录

1. 数据库测试最初无法编译，因为 DbContext 和 Outbox 尚不存在。
2. 本地存储测试最初无法编译，因为真实磁盘实现尚不存在。
3. 首次整套编译发现 EF 10.0.4 与 10.0.11 冲突。把 EF Core 和 Relational 统一固定到 10.0.11 后，构建恢复为 0 警告。
4. Windows 读取流未关闭时删除文件失败。读流增加 `FileShare.Delete` 后，本地存储合同通过。
5. 首次 PostgreSQL 查询用 `Contains` 直接过滤 jsonb，数据库拒绝 `LIKE`。测试改为按事件 ID 查询，再在内存检查 JSON。

## 密钥与清理

- `scripts/Initialize-LocalSecrets.ps1` 只在 `.env` 不存在时创建随机值，不输出内容，也不覆盖现有文件。
- `.env`、数据库目录和 Redis 目录均被 Git 忽略。
- Testcontainers 测试结束后自动删除测试容器。
- 本项目 Compose 容器保留运行，供下一项身份与授权功能开发使用。
