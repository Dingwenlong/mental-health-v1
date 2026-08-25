using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Encodings.Web;
using MentalHealth.Infrastructure.Content;

namespace MentalHealth.ContractTests.Content;

public sealed partial class UserFacingCopyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly JsonSerializerOptions GeneratedJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Fact]
    public void Copy_catalog_contains_required_safety_messages()
    {
        var copy = ReadCopy();

        Assert.Equal(
            "你正在和 AI 虚拟人对话。它不是真人，也不是医生。",
            copy["ai.identity"]);
        Assert.Contains("12356", copy["crisis.help"], StringComparison.Ordinal);
        Assert.Equal(
            "此结果不能替代诊断。需要医疗帮助时，请联系医生。",
            copy["result.notDiagnosis"]);
        Assert.Equal("医疗急救：120", copy["crisis.medicalPhone"]);
        Assert.Equal("模拟收费，不会真实扣款", copy["order.demoPaid"]);
    }

    [Fact]
    public void User_facing_copy_does_not_contain_forbidden_patterns()
    {
        var copy = ReadCopy();
        var patterns = ReadForbiddenPatterns();

        var violations = copy
            .SelectMany(pair => patterns
                .Where(pattern => pattern.IsMatch(pair.Value))
                .Select(pattern => $"{pair.Key}: {pattern}"))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Questionnaire_copy_does_not_contain_forbidden_patterns()
    {
        var questionnaire = File.ReadAllText(PathInRepository(
            "config",
            "demo-questionnaire.v1.json"));
        var violations = ReadForbiddenPatterns()
            .Where(pattern => pattern.IsMatch(questionnaire))
            .Select(pattern => pattern.ToString())
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Generated_clients_contain_every_copy_key()
    {
        var copy = ReadCopy();
        var dart = File.ReadAllText(PathInRepository(
            "apps",
            "mobile_flutter",
            "lib",
            "generated",
            "ui_copy.g.dart"));
        var typescript = File.ReadAllText(PathInRepository(
            "apps",
            "admin_web",
            "src",
            "generated",
            "uiCopy.generated.ts"));

        foreach (var pair in copy)
        {
            Assert.Contains(
                JsonSerializer.Serialize(pair.Key, GeneratedJsonOptions),
                dart,
                StringComparison.Ordinal);
            Assert.Contains(
                JsonSerializer.Serialize(pair.Value, GeneratedJsonOptions),
                dart,
                StringComparison.Ordinal);
            Assert.Contains(
                JsonSerializer.Serialize(pair.Key, GeneratedJsonOptions),
                typescript,
                StringComparison.Ordinal);
            Assert.Contains(
                JsonSerializer.Serialize(pair.Value, GeneratedJsonOptions),
                typescript,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Client_production_code_uses_generated_copy_instead_of_inline_Chinese()
    {
        var roots = new[]
        {
            PathInRepository("apps", "mobile_flutter", "lib"),
            PathInRepository("apps", "admin_web", "src")
        };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ui_copy.g.dart",
            "uiCopy.generated.ts"
        };
        var violations = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !excluded.Contains(Path.GetFileName(path)))
            .Where(path => Path.GetExtension(path) is ".dart" or ".ts" or ".vue")
            .Where(path => ChineseCharacter().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Server_catalog_fails_for_missing_key()
    {
        var catalog = new JsonUiCopyCatalog(PathInRepository(
            "content",
            "zh-CN",
            "ui-copy.v1.json"));

        Assert.Equal("模拟收费，不会真实扣款", catalog.Get("order.demoPaid"));
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("missing.key"));
    }

    [Fact]
    public void Server_catalog_rejects_duplicate_keys()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mental-health-ui-copy-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{\"copy.key\":\"first\",\"copy.key\":\"second\"}");

            Assert.Throws<InvalidDataException>(() => new JsonUiCopyCatalog(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static IReadOnlyDictionary<string, string> ReadCopy()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathInRepository(
            "content",
            "zh-CN",
            "ui-copy.v1.json")));
        return document.RootElement
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.GetString()!,
                StringComparer.Ordinal);
    }

    private static Regex[] ReadForbiddenPatterns() =>
        File.ReadAllLines(PathInRepository(
                "content",
                "zh-CN",
                "forbidden-copy-patterns.txt"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => new Regex(line, RegexOptions.CultureInvariant))
            .ToArray();

    private static string PathInRepository(params string[] parts) =>
        parts.Aggregate(RepositoryRoot, Path.Combine);

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MentalHealth.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    [GeneratedRegex("[\\u3400-\\u9FFF]", RegexOptions.CultureInvariant)]
    private static partial Regex ChineseCharacter();
}
