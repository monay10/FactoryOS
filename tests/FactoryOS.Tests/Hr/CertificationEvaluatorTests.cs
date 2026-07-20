using FactoryOS.Plugins.Hr;
using FactoryOS.Plugins.Hr.Domain;

namespace FactoryOS.Tests.Hr;

public sealed class CertificationEvaluatorTests
{
    private static readonly DateTimeOffset ShiftStart = DateTimeOffset.UnixEpoch.AddDays(100);

    [Fact]
    public void A_valid_certification_is_no_gap()
    {
        var gap = CertificationEvaluator.Evaluate(ShiftStart.AddDays(30), ShiftStart, new HrOptions());

        Assert.False(gap.IsGap);
    }

    [Fact]
    public void An_expired_certification_is_an_expired_gap()
    {
        var gap = CertificationEvaluator.Evaluate(ShiftStart.AddDays(-1), ShiftStart, new HrOptions());

        Assert.True(gap.IsGap);
        Assert.Equal("Expired", gap.Reason);
    }

    [Fact]
    public void A_missing_certification_is_a_missing_gap_by_default()
    {
        var gap = CertificationEvaluator.Evaluate(null, ShiftStart, new HrOptions());

        Assert.True(gap.IsGap);
        Assert.Equal("Missing", gap.Reason);
    }

    [Fact]
    public void A_missing_certification_is_ignored_when_configured()
    {
        var gap = CertificationEvaluator.Evaluate(null, ShiftStart, new HrOptions { TreatMissingAsGap = false });

        Assert.Equal(CertificationGap.None, gap);
    }
}
