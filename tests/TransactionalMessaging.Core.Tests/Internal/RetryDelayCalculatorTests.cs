using TransactionalMessaging.Core.Internal;
using TransactionalMessaging.Core.Options;
using Xunit;

namespace TransactionalMessaging.Core.Tests.Internal;

public class RetryDelayCalculatorTests
{
    [Fact]
    public void CalculateDelay_FirstAttempt_ReturnsInitialDelay()
    {
        var options = new RetryOptions { UseJitter = false };

        var delay = RetryDelayCalculator.CalculateDelay(1, options);

        Assert.Equal(options.InitialDelay, delay);
    }

    [Fact]
    public void CalculateDelay_SecondAttempt_ReturnsDoubledDelay()
    {
        var options = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        var delay = RetryDelayCalculator.CalculateDelay(2, options);

        Assert.Equal(TimeSpan.FromSeconds(2), delay);
    }

    [Fact]
    public void CalculateDelay_ExceedsMax_ReturnsMaxDelay()
    {
        var options = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        var delay = RetryDelayCalculator.CalculateDelay(10, options);

        Assert.Equal(options.MaxDelay, delay);
    }

    [Fact]
    public void CalculateDelay_WithJitter_ReturnsDelayWithinBounds()
    {
        var options = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            BackoffMultiplier = 2.0,
            UseJitter = true
        };

        var delay = RetryDelayCalculator.CalculateDelay(3, options);

        // With jitter, delay should be between 0 and calculated exponential value
        var expectedMax = TimeSpan.FromSeconds(4); // 1 * 2^2
        Assert.True(delay >= TimeSpan.Zero);
        Assert.True(delay <= expectedMax);
    }

    [Fact]
    public void CalculateDelay_ZeroAttempt_ReturnsInitialDelay()
    {
        var options = new RetryOptions { UseJitter = false };

        var delay = RetryDelayCalculator.CalculateDelay(0, options);

        Assert.Equal(options.InitialDelay, delay);
    }
}
