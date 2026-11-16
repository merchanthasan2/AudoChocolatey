using QuantumUpdater.Services.Scan;
using Xunit;

namespace QuantumUpdater.Tests;

public class ScanJobTests
{
    [Fact]
    public void Fraction_from_items_is_three_quarters()
    {
        var progress = new ScanProgress(Step: 0, StepCount: 0, Items: 3, ItemTotal: 4);

        Assert.Equal(0.75, progress.Fraction);
    }

    [Fact]
    public void Fraction_from_steps_is_one_half()
    {
        var progress = new ScanProgress(Step: 1, StepCount: 2, Items: 0, ItemTotal: 0);

        Assert.Equal(0.5, progress.Fraction);
    }

    [Fact]
    public void Items_are_preferred_over_steps()
    {
        var progress = new ScanProgress(Step: 1, StepCount: 2, Items: 1, ItemTotal: 4);

        Assert.Equal(0.25, progress.Fraction);
    }

    [Fact]
    public void No_items_and_no_steps_gives_no_number()
    {
        var progress = new ScanProgress(Step: 0, StepCount: 0, Items: 0, ItemTotal: 0);

        Assert.Null(progress.Fraction);
    }

    [Fact]
    public void Fraction_over_one_hundred_percent_clamps_to_one()
    {
        var progress = new ScanProgress(Step: 0, StepCount: 0, Items: 9, ItemTotal: 4);

        Assert.Equal(1.0, progress.Fraction);
    }

    [Fact]
    public void Negative_fraction_clamps_to_zero()
    {
        var progress = new ScanProgress(Step: 0, StepCount: 0, Items: -2, ItemTotal: 4);

        Assert.Equal(0.0, progress.Fraction);
    }

    [Fact]
    public void Done_sets_success_and_summary()
    {
        var result = ScanResult.Done("7 updates");

        Assert.True(result.Success);
        Assert.Equal("7 updates", result.Summary);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void Failed_sets_success_false_and_reason()
    {
        var result = ScanResult.Failed("Took too long");

        Assert.False(result.Success);
        Assert.Equal("Took too long", result.Reason);
    }
}
