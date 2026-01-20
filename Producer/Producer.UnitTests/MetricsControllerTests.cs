using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Producer.Controllers;
using Producer.Dto;
using Producer.Services;
using Xunit;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for MetricsController - validates metrics endpoints behavior.
/// </summary>
public class MetricsControllerTests
{
    private readonly Mock<IQueueMetricsService> _mockMetricsService;
    private readonly MetricsController _controller;

    public MetricsControllerTests()
    {
        _mockMetricsService = new Mock<IQueueMetricsService>();
        _controller = new MetricsController(_mockMetricsService.Object);
    }

    [Fact]
    public async Task GetQueueLength_ShouldReturnQueueLengthDto()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueLengthAsync())
            .ReturnsAsync(42);

        // Act
        var result = await _controller.GetQueueLength();

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().BeOfType<QueueLengthDto>();
        result.Value!.QueueLength.Should().Be(42);
        result.Value.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetQueueLength_ShouldCallServiceMethod()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueLengthAsync())
            .ReturnsAsync(10);

        // Act
        await _controller.GetQueueLength();

        // Assert
        _mockMetricsService.Verify(x => x.GetQueueLengthAsync(), Times.Once);
    }

    [Fact]
    public async Task GetQueueLength_ShouldReturnOkResult()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueLengthAsync())
            .ReturnsAsync(5);

        // Act
        var result = await _controller.GetQueueLength();

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeOfType<QueueLengthDto>();
    }

    [Fact]
    public async Task GetQueueLength_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueLengthAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetQueueLength();

        // Assert
        var statusCodeResult = result.Result as ObjectResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public async Task GetQueueLength_ShouldHandleVariousQueueSizes(int length)
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueLengthAsync())
            .ReturnsAsync(length);

        // Act
        var result = await _controller.GetQueueLength();

        // Assert
        result.Value!.QueueLength.Should().Be(length);
    }

    [Fact]
    public async Task GetQueueSpeed_ShouldReturnQueueSpeedDto()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(3.67);

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().BeOfType<QueueSpeedDto>();
        result.Value!.QueueSpeed.Should().Be(3.67);
        result.Value.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetQueueSpeed_ShouldCallServiceMethod()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(5.0);

        // Act
        await _controller.GetQueueSpeed();

        // Assert
        _mockMetricsService.Verify(x => x.GetQueueSpeedAsync(), Times.Once);
    }

    [Fact]
    public async Task GetQueueSpeed_ShouldReturnOkResult()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(2.5);

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeOfType<QueueSpeedDto>();
    }

    [Fact]
    public async Task GetQueueSpeed_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        var statusCodeResult = result.Result as ObjectResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Theory]
    [InlineData(0.0)]           // Balanced
    [InlineData(5.5)]           // Growing
    [InlineData(-3.2)]          // Shrinking
    [InlineData(10.0)]          // Fast growth
    [InlineData(-10.0)]         // Fast consumption
    public async Task GetQueueSpeed_ShouldHandleVariousSpeeds(double speed)
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(speed);

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        result.Value!.QueueSpeed.Should().Be(speed);
    }

    [Fact]
    public async Task GetQueueSpeed_ShouldIndicateGrowth_WithPositiveValue()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(3.5);

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        result.Value!.QueueSpeed.Should().BePositive("positive speed indicates queue growth");
    }

    [Fact]
    public async Task GetQueueSpeed_ShouldIndicateConsumption_WithNegativeValue()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(-2.5);

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        result.Value!.QueueSpeed.Should().BeNegative("negative speed indicates queue consumption");
    }

    [Fact]
    public async Task GetQueueSpeed_ShouldIndicateBalance_WithZeroValue()
    {
        // Arrange
        _mockMetricsService.Setup(x => x.GetQueueSpeedAsync())
            .ReturnsAsync(0.0);

        // Act
        var result = await _controller.GetQueueSpeed();

        // Assert
        result.Value!.QueueSpeed.Should().Be(0.0, "zero speed indicates balanced production/consumption");
    }
}
