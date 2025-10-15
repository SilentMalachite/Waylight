using LocalLlmAssistant.Models;
using LocalLlmAssistant.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace LocalLlmAssistant.Tests;

public class HistoryCompressorTests
{
    [Fact]
    public void CompressIfNeeded_ShouldReturnEmptyList_WhenMessagesIsNull()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<HistoryCompressor>>();
        var compressor = new HistoryCompressor(loggerMock.Object);

        // Act
        var result = compressor.CompressIfNeeded(null!);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CompressIfNeeded_ShouldReturnEmptyList_WhenMessagesIsEmpty()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<HistoryCompressor>>();
        var compressor = new HistoryCompressor(loggerMock.Object);
        var messages = new List<Message>();

        // Act
        var result = compressor.CompressIfNeeded(messages);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void CompressIfNeeded_ShouldReturnOriginalList_WhenTotalTokensBelowThreshold()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<HistoryCompressor>>();
        var compressor = new HistoryCompressor(loggerMock.Object);
        var messages = new List<Message>
        {
            new Message { Id = 1, TokenCount = 100 },
            new Message { Id = 2, TokenCount = 200 }
        };

        // Act
        var result = compressor.CompressIfNeeded(messages);

        // Assert
        Assert.Equal(messages.Count, result.Count);
        Assert.Equal(messages, result);
    }

    [Fact]
    public void CompressIfNeeded_ShouldCompress_WhenTotalTokensExceedThreshold()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<HistoryCompressor>>();
        var compressor = new HistoryCompressor(loggerMock.Object);
        var messages = new List<Message>
        {
            new Message { Id = 1, TokenCount = 4000 },
            new Message { Id = 2, TokenCount = 4000 },
            new Message { Id = 3, TokenCount = 4000 }
        };

        // Act
        var result = compressor.CompressIfNeeded(messages);

        // Assert
        Assert.True(result.Count < messages.Count); // Should be compressed
    }
}