using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MentalHealth.UnitTests.Security;

public sealed class MobileCaptchaContentSecurityPolicyTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Policy_allows_only_approved_https_sources_and_hashes_current_inline_blocks()
    {
        var html = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "MentalHealth.Api",
            "wwwroot",
            "captcha",
            "mobile.html"));
        var policy = Regex.Match(
            html,
            "<meta\\s+http-equiv=\"Content-Security-Policy\"\\s+content=\"(?<policy>[^\"]+)\"",
            RegexOptions.CultureInvariant).Groups["policy"].Value;

        Assert.NotEmpty(policy);
        Assert.DoesNotContain("data:", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("'unsafe-inline'", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nonce", policy, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nonce=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("'self'", policy, StringComparison.OrdinalIgnoreCase);

        var styleHash = HashSingleInlineBlock(html, "style");
        var scriptHash = HashSingleInlineBlock(html, "script");

        Assert.Equal(
            [$"'sha256-{scriptHash}'", "https://o.alicdn.com"],
            DirectiveSources(policy, "script-src"));
        Assert.Equal(
            [$"'sha256-{styleHash}'", "https://*.alicdn.com", "https://*.aliyuncs.com"],
            DirectiveSources(policy, "style-src"));
        Assert.Equal(
            ["https://*.alicdn.com", "https://*.aliyuncs.com"],
            DirectiveSources(policy, "img-src"));
        Assert.Equal(
            ["https://*.alicdn.com", "https://*.aliyuncs.com"],
            DirectiveSources(policy, "connect-src"));
        Assert.Equal(
            ["https://*.alicdn.com", "https://*.aliyuncs.com"],
            DirectiveSources(policy, "font-src"));
        Assert.Equal(
            ["https://*.alicdn.com", "https://*.aliyuncs.com"],
            DirectiveSources(policy, "frame-src"));
        Assert.Equal(["'none'"], DirectiveSources(policy, "default-src"));
        Assert.Equal(["'none'"], DirectiveSources(policy, "base-uri"));
        Assert.Equal(["'none'"], DirectiveSources(policy, "form-action"));
        Assert.Equal(["'none'"], DirectiveSources(policy, "object-src"));
    }

    private static string HashSingleInlineBlock(string html, string elementName)
    {
        var matches = Regex.Matches(
            html,
            $"<{elementName}(?<attributes>[^>]*)>(?<content>.*?)</{elementName}>",
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        var match = Assert.Single(
            matches.Cast<Match>(),
            candidate => !candidate.Groups["attributes"].Value.Contains(
                "src=",
                StringComparison.OrdinalIgnoreCase));
        var browserNormalizedContent = match.Groups["content"].Value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(browserNormalizedContent)));
    }

    private static string[] DirectiveSources(string policy, string directiveName)
    {
        var directive = Assert.Single(
            policy.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            value => value.StartsWith(directiveName, StringComparison.Ordinal));
        return directive
            .Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .ToArray();
    }

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
}
