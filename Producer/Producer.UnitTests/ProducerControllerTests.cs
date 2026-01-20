using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Producer.Controllers;
using Producer.Dto;
using Producer.Services;
using Xunit;

namespace Producer.UnitTests;

/// <summary>
/// Unit tests for ProducerController - validates API endpoints behavior.
/// </summary>
public class ProducerControllerTests
{
    private readonly Mock<IProducerWorkerControl> _mockControl;
    private readonly ProducerController _controller;

    public ProducerControllerTests()
    {
        _mockControl = new Mock<IProducerWorkerControl>();
        _controller = new ProducerController(_mockControl.Object);
    }

    [Fact]
    public void Start_ShouldCallStartProducingAndReturnStatusDto()
    {
        // Act
        var result = _controller.Start();

        // Assert
        _mockControl.Verify(x => x.StartProducing(), Times.Once);
        result.Should().NotBeNull();
        
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var statusDto = okResult.Value.Should().BeOfType<StatusDto>().Subject;
        statusDto.Status.Should().Be("started");
    }

    [Fact]
    public void Stop_ShouldCallStopProducingAndReturnStatusDto()
    {
        // Act
        var result = _controller.Stop();

        // Assert
        _mockControl.Verify(x => x.StopProducing(), Times.Once);
        result.Should().NotBeNull();
        
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var statusDto = okResult.Value.Should().BeOfType<StatusDto>().Subject;
        statusDto.Status.Should().Be("stopped");
    }

    [Fact]
    public void IsRunning_ShouldReturnTrue_WhenProducerIsRunning()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(true);

        // Act
        var result = _controller.IsRunning();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(true);
    }

    [Fact]
    public void IsRunning_ShouldReturnFalse_WhenProducerIsStopped()
    {
        // Arrange
        _mockControl.Setup(x => x.IsRunning()).Returns(false);

        // Act
        var result = _controller.IsRunning();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(false);
    }

    [Fact]
    public void Rate_ShouldReturnCurrentRate()
    {
        // Arrange
        _mockControl.Setup(x => x.GetRate()).Returns(15);

        // Act
        var result = _controller.Rate();

        // Assert
        result.Should().NotBeNull();
        
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rateDto = okResult.Value.Should().BeOfType<RateDto>().Subject;
        rateDto.Rate.Should().Be(15);
    }

    [Fact]
    public void UpdateRate_ShouldSetNewRateAndReturnUpdatedRate()
    {
        // Arrange
        _mockControl.Setup(x => x.GetRate()).Returns(25);

        // Act
        var result = _controller.UpdateRate(25);

        // Assert
        _mockControl.Verify(x => x.SetRate(25), Times.Once);
        result.Should().NotBeNull();
        
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rateDto = okResult.Value.Should().BeOfType<RateDto>().Subject;
        rateDto.Rate.Should().Be(25);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void UpdateRate_ShouldAcceptVariousRates(int rate)
    {
        // Arrange
        _mockControl.Setup(x => x.GetRate()).Returns(rate);

        // Act
        var result = _controller.UpdateRate(rate);

        // Assert
        _mockControl.Verify(x => x.SetRate(rate), Times.Once);
        result.Should().NotBeNull();
        
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var rateDto = okResult.Value.Should().BeOfType<RateDto>().Subject;
        rateDto.Rate.Should().Be(rate);
    }
}