﻿using FluentAssertions;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers.ProtocolControls;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LiveStreamingServerNet.Rtmp.Test.RtmpEventHandlers.ProtocolControls
{
    public class RtmpAcknowledgementHandlerTest
    {
        [Fact]
        public async Task HandleAsync_Should_ReturnTrue()
        {
            // Arrange
            var logger = Substitute.For<ILogger<RtmpAcknowledgementHandler>>();
            var chunkStreamContext = Substitute.For<IRtmpChunkStreamContext>();
            var clientContext = Substitute.For<IRtmpClientContext>();
            var payloadBuffer = Substitute.For<INetBuffer>();
            var sut = new RtmpAcknowledgementHandler(logger);

            // Act
            var result = await sut.HandleAsync(chunkStreamContext, clientContext, payloadBuffer, default);

            // Assert
            result.Should().BeTrue();
        }
    }
}
