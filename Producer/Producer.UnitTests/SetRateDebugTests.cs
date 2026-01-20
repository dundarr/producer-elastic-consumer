using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Producer.Configuration;
using Producer.Services;
using Xunit;
using Xunit.Abstractions;

namespace Producer.UnitTests;

/// <summary>
/// Diagnostic tests to investigate the SetRate issue where the rate sticks to 1.
/// </summary>
public class SetRateDebugTests
{
    private readonly ITestOutputHelper _output;

    public SetRateDebugTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SetRate_ShouldUpdate_From1To10()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);
        _output.WriteLine($"Initial rate: {control.GetRate()}");

        // Act
        control.SetRate(10);
        _output.WriteLine($"After SetRate(10): {control.GetRate()}");

        // Assert
        control.GetRate().Should().Be(10, "SetRate should update the rate to 10");
    }

    [Fact]
    public void SetRate_ShouldUpdate_Multiple_Times()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);
        _output.WriteLine($"Initial rate: {control.GetRate()}");

        // Act & Assert - Update multiple times
        control.SetRate(5);
        _output.WriteLine($"After SetRate(5): {control.GetRate()}");
        control.GetRate().Should().Be(5);

        control.SetRate(15);
        _output.WriteLine($"After SetRate(15): {control.GetRate()}");
        control.GetRate().Should().Be(15);

        control.SetRate(3);
        _output.WriteLine($"After SetRate(3): {control.GetRate()}");
        control.GetRate().Should().Be(3);

        control.SetRate(100);
        _output.WriteLine($"After SetRate(100): {control.GetRate()}");
        control.GetRate().Should().Be(100);
    }

    [Fact]
    public void SetRate_WithConfiguration_ShouldStillWork()
    {
        // Arrange - Start with configured rate of 5
        var mockMonitor = new Mock<IOptionsMonitor<QueueOptions>>();
        var options = new QueueOptions { MessagesPerSecond = 5 };
        
        mockMonitor.Setup(x => x.CurrentValue).Returns(options);
        mockMonitor.Setup(x => x.OnChange(It.IsAny<Action<QueueOptions, string?>>()))
            .Returns(Mock.Of<IDisposable>());

        var control = new ProducerWorkerControl(mockMonitor.Object);
        _output.WriteLine($"Initial rate from config: {control.GetRate()}");
        control.GetRate().Should().Be(5);

        // Act - Update via SetRate
        control.SetRate(20);
        _output.WriteLine($"After SetRate(20): {control.GetRate()}");

        // Assert
        control.GetRate().Should().Be(20, "SetRate should override configuration");
    }

    [Fact]
    public void SetRate_AfterConfigurationChange_ShouldWork()
    {
        // Arrange
        var mockMonitor = new Mock<IOptionsMonitor<QueueOptions>>();
        var options = new QueueOptions { MessagesPerSecond = 5 };
        
        mockMonitor.Setup(x => x.CurrentValue).Returns(options);
        
        Action<QueueOptions, string?>? changeListener = null;
        mockMonitor.Setup(x => x.OnChange(It.IsAny<Action<QueueOptions, string?>>()))
            .Callback<Action<QueueOptions, string?>>((listener) =>
            {
                changeListener = listener;
            })
            .Returns(Mock.Of<IDisposable>());

        var control = new ProducerWorkerControl(mockMonitor.Object);
        _output.WriteLine($"Initial rate: {control.GetRate()}");
        control.GetRate().Should().Be(5);

        // Simulate configuration change
        if (changeListener != null)
        {
            var newOptions = new QueueOptions { MessagesPerSecond = 15 };
            changeListener(newOptions, null);
            _output.WriteLine($"After config change to 15: {control.GetRate()}");
            control.GetRate().Should().Be(15);
        }

        // Act - Update via SetRate after config change
        control.SetRate(30);
        _output.WriteLine($"After SetRate(30): {control.GetRate()}");

        // Assert
        control.GetRate().Should().Be(30, "SetRate should work after config change");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(1000)]
    public void SetRate_ShouldAcceptVariousValidRates(int rate)
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);

        // Act
        control.SetRate(rate);
        _output.WriteLine($"SetRate({rate}) -> GetRate() = {control.GetRate()}");

        // Assert
        control.GetRate().Should().Be(rate, $"rate should be set to {rate}");
    }

    [Fact]
    public void SetRate_Repeatedly_ShouldMaintainLastValue()
    {
        // Arrange
        var control = new ProducerWorkerControl(null!);

        // Act - Set rate multiple times rapidly
        for (int i = 1; i <= 10; i++)
        {
            control.SetRate(i * 10);
            _output.WriteLine($"Iteration {i}: SetRate({i * 10}) -> GetRate() = {control.GetRate()}");
        }

        // Assert - Should be the last value (100)
        control.GetRate().Should().Be(100, "should retain the last set value");
    }
}
