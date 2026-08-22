using MentalHealth.Domain.Analysis;

namespace MentalHealth.UnitTests.Analysis;

public sealed class CrisisRuleEngineTests
{
    [Theory]
    [InlineData("我已经准备好了工具，今晚就要结束生命", "CRISIS_INTENT_PLAN_TIME")]
    [InlineData("我正在伤害自己", "CRISIS_IN_PROGRESS")]
    [InlineData("我准备好了，马上要伤害别人", "CRISIS_OTHER_HARM")]
    public void Explicit_crisis_never_returns_normal_reply(
        string text,
        string expectedRuleId)
    {
        var result = CreateEngine().Evaluate([text]);

        Assert.True(result.IsCrisis);
        Assert.Equal(expectedRuleId, result.RuleId);
    }

    [Theory]
    [InlineData("我没有想结束生命，也没有准备任何工具，今晚会和家人在一起")]
    [InlineData("新闻里说有人准备好了工具，今晚想结束生命")]
    [InlineData("他说，我已经准备好了工具，今晚要结束生命")]
    [InlineData("他说他马上要伤害别人，但我现在很安全")]
    public void Negation_and_quoted_other_people_do_not_trigger(string text)
    {
        var result = CreateEngine().Evaluate([text]);

        Assert.False(result.IsCrisis);
        Assert.Equal("NO_CRISIS", result.RuleId);
    }

    [Fact]
    public void Intent_plan_and_time_can_accumulate_in_recent_user_turns()
    {
        var result = CreateEngine().Evaluate([
            "我想结束生命",
            "工具已经准备好了",
            "时间就是今晚"
        ]);

        Assert.True(result.IsCrisis);
        Assert.Equal("CRISIS_INTENT_PLAN_TIME", result.RuleId);
    }

    [Fact]
    public void First_person_pivot_after_a_quote_is_evaluated_as_the_user()
    {
        var result = CreateEngine().Evaluate([
            "新闻里说有人准备好了工具，但我也准备好了，今晚想结束生命"
        ]);

        Assert.True(result.IsCrisis);
        Assert.Equal("CRISIS_INTENT_PLAN_TIME", result.RuleId);
    }

    [Fact]
    public void Messages_outside_the_lookback_window_do_not_trigger()
    {
        var result = CreateEngine().Evaluate([
            "我想结束生命",
            "工具已经准备好了",
            "1", "2", "3", "4", "5", "6", "7", "今晚"
        ]);

        Assert.False(result.IsCrisis);
    }

    private static CrisisRuleEngine CreateEngine() => new(
        crisisReply: "先保证安全。请马上联系身边可信任的人，并拨打 12356。如果危险正在发生，请拨打 120 或 110。",
        lookbackMessageCount: 8,
        negationWindow: 8,
        negationTerms: ["没有", "并不", "不会", "从未"],
        quotationTerms: ["他说", "新闻里", "电影里", "别人说"],
        rules:
        [
            new CrisisRule(
                "CRISIS_IN_PROGRESS",
                ["我正在伤害自己", "危险正在发生", "我控制不住自己，马上要伤害别人"],
                []),
            new CrisisRule(
                "CRISIS_INTENT_PLAN_TIME",
                [],
                [
                    ["结束生命", "不想活了", "要伤害自己"],
                    ["准备好了", "有具体计划", "已经拿到工具"],
                    ["今晚", "现在", "马上", "具体时间"]
                ]),
            new CrisisRule(
                "CRISIS_OTHER_HARM",
                [],
                [
                    ["要伤害别人", "想杀死他"],
                    ["有计划", "准备好了"],
                    ["马上", "现在"]
                ])
        ]);
}
