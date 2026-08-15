using FluentAssertions;
using SDPP.Classification.Domain.Aggregates;
using SDPP.Classification.Domain.Enums;
using Xunit;

namespace SDPP.Classification.UnitTests;

public sealed class DlpRuleTests
{
    [Theory]
    [InlineData(Severity.Critical, 100)]
    [InlineData(Severity.High, 50)]
    [InlineData(Severity.Medium, 20)]
    [InlineData(Severity.Low, 5)]
    public void Create_WithoutExplicitWeight_DerivesWeightFromSeverity(Severity severity, int expectedWeight)
    {
        var rule = DlpRule.Create("Cédula colombiana", DetectorType.Regex, @"\d{6,10}", FindingCategory.PII, severity);

        rule.Weight.Should().Be(expectedWeight);
    }

    [Fact]
    public void Create_WithExplicitWeight_OverridesSeverityDefault()
    {
        var rule = DlpRule.Create(
            "Regla ajustada", DetectorType.Regex, @"\d+", FindingCategory.PII, Severity.Low, weight: 999);

        rule.Weight.Should().Be(999);
    }

    [Fact]
    public void Create_IsEnabledByDefaultAtVersionOne()
    {
        var rule = DlpRule.Create("Regla", DetectorType.Keyword, "confidencial", FindingCategory.Legal, Severity.Medium);

        rule.Enabled.Should().BeTrue();
        rule.Version.Should().Be(1);
    }

    [Fact]
    public void UpdatePattern_IncrementsVersion()
    {
        var rule = DlpRule.Create("Regla", DetectorType.Regex, @"\d+", FindingCategory.Financial, Severity.High);

        rule.UpdatePattern(@"\d{4}-\d{4}");

        rule.PatternOrConfigJson.Should().Be(@"\d{4}-\d{4}");
        rule.Version.Should().Be(2);
    }

    [Fact]
    public void SetEnabled_False_DisablesRuleWithoutChangingVersion()
    {
        var rule = DlpRule.Create("Regla", DetectorType.Regex, @"\d+", FindingCategory.Medical, Severity.Critical);

        rule.SetEnabled(false);

        rule.Enabled.Should().BeFalse();
        rule.Version.Should().Be(1, "disabling a rule is not a content change");
    }
}
