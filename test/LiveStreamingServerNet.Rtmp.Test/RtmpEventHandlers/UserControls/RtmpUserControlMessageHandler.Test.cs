﻿using FluentAssertions;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers.UserControls;
using NSubstitute;

namespace LiveStreamingServerNet.Rtmp.Test.RtmpEventHandlers.UserControls
{
    public class RtmpUserControlMessageHandlerTest
    {
        [Fact]
        public async Task HandleAsync_Should_ReturnTrue()
        {
            // Arrange
            var chunkStreamContext = Substitute.For<IRtmpChunkStreamContext>();
            var clientContext = Substitute.For<IRtmpClientContext>();
            var payloadBuffer = Substitute.For<INetBuffer>();
            var sut = new RtmpUserControlMessageHandler();

            // Act
            var result = await sut.HandleAsync(chunkStreamContext, clientContext, payloadBuffer, default);

            // Assert
            result.Should().BeTrue();
        }
    }
}
