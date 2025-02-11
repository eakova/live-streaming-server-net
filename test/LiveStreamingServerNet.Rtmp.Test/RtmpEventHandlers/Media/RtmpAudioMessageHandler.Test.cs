﻿using AutoFixture;
using FluentAssertions;
using LiveStreamingServerNet.Networking;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Rtmp.Internal;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEventHandlers.Media;
using LiveStreamingServerNet.Rtmp.Internal.Services.Contracts;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace LiveStreamingServerNet.Rtmp.Test.RtmpEventHandlers.Media
{
    public class RtmpAudioMessageHandlerTest : IDisposable
    {
        private readonly IFixture _fixture;
        private readonly IRtmpClientContext _clientContext;
        private readonly IRtmpChunkStreamContext _chunkStreamContext;
        private readonly IRtmpStreamManagerService _streamManager;
        private readonly IRtmpMediaMessageCacherService _mediaMessageCacher;
        private readonly IRtmpMediaMessageBroadcasterService _mediaMessageBroadcaster;
        private readonly ILogger<RtmpAudioMessageHandler> _logger;
        private readonly INetBuffer _netBuffer;
        private readonly RtmpAudioMessageHandler _sut;

        public RtmpAudioMessageHandlerTest()
        {
            _fixture = new Fixture();
            _clientContext = Substitute.For<IRtmpClientContext>();
            _chunkStreamContext = Substitute.For<IRtmpChunkStreamContext>();
            _streamManager = Substitute.For<IRtmpStreamManagerService>();
            _mediaMessageCacher = Substitute.For<IRtmpMediaMessageCacherService>();
            _mediaMessageBroadcaster = Substitute.For<IRtmpMediaMessageBroadcasterService>();
            _logger = Substitute.For<ILogger<RtmpAudioMessageHandler>>();

            _netBuffer = new NetBuffer();

            _sut = new RtmpAudioMessageHandler(
                _streamManager,
                _mediaMessageCacher,
                _mediaMessageBroadcaster,
                _logger);
        }

        public void Dispose()
        {
            _netBuffer.Dispose();
        }

        [Fact]
        public async Task HandleAsync_Should_ReturnFalse_When_StreamNotYetCreated()
        {
            // Arrange
            _clientContext.PublishStreamContext.Returns((IRtmpPublishStreamContext?)null);

            // Act
            var result = await _sut.HandleAsync(_chunkStreamContext, _clientContext, _netBuffer, default);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(true, AudioSoundFormat.AAC, AACPacketType.SequenceHeader)]
        [InlineData(true, AudioSoundFormat.AAC, AACPacketType.Raw)]
        [InlineData(true, AudioSoundFormat.Opus, AACPacketType.SequenceHeader)]
        [InlineData(true, AudioSoundFormat.Opus, AACPacketType.Raw)]
        [InlineData(false, AudioSoundFormat.AAC, AACPacketType.SequenceHeader)]
        [InlineData(false, AudioSoundFormat.AAC, AACPacketType.Raw)]
        [InlineData(false, AudioSoundFormat.Opus, AACPacketType.SequenceHeader)]
        [InlineData(false, AudioSoundFormat.Opus, AACPacketType.Raw)]
        internal async Task HandleAsync_Should_HandleCacheAndBroadcastAndReturnTrue(
            bool gopCacheActivated, AudioSoundFormat soundFormat, AACPacketType aacPacketType)
        {
            // Arrange
            var stremaPath = _fixture.Create<string>();

            var subscriber = Substitute.For<IRtmpClientContext>();
            var subscribers = new List<IRtmpClientContext>() { subscriber };

            var publishStreamContext = Substitute.For<IRtmpPublishStreamContext>();
            publishStreamContext.StreamPath.Returns(stremaPath);
            publishStreamContext.GroupOfPicturesCacheActivated.Returns(gopCacheActivated);

            _clientContext.PublishStreamContext.Returns(publishStreamContext);
            _streamManager.GetSubscribers(stremaPath).Returns(subscribers);

            var firstByte = (byte)((byte)soundFormat << 4);
            _netBuffer.Write(firstByte);
            _netBuffer.Write((byte)aacPacketType);
            _netBuffer.Write(_fixture.Create<byte[]>());
            _netBuffer.MoveTo(0);

            var hasHeader =
                (soundFormat is AudioSoundFormat.AAC or AudioSoundFormat.Opus) &&
                aacPacketType is AACPacketType.SequenceHeader;

            bool isPictureCachable = (soundFormat is AudioSoundFormat.AAC or AudioSoundFormat.Opus) && aacPacketType is not AACPacketType.SequenceHeader;

            var isSkippable = !hasHeader;

            // Act
            var result = await _sut.HandleAsync(_chunkStreamContext, _clientContext, _netBuffer, default);

            // Assert
            result.Should().BeTrue();

            _ = _mediaMessageCacher.Received(hasHeader ? 1 : 0)
                .CacheSequenceHeaderAsync(publishStreamContext, MediaType.Audio, _netBuffer);

            _ = _mediaMessageCacher.Received(gopCacheActivated && isPictureCachable ? 1 : 0)
                .CachePictureAsync(publishStreamContext, MediaType.Audio, _netBuffer, _chunkStreamContext.MessageHeader.Timestamp);

            _clientContext.Received(1).UpdateTimestamp(_chunkStreamContext.MessageHeader.Timestamp, MediaType.Audio);

            await _mediaMessageBroadcaster.Received(1).BroadcastMediaMessageAsync(
                publishStreamContext,
                subscribers,
                MediaType.Audio,
                _chunkStreamContext.MessageHeader.Timestamp,
                isSkippable,
                _netBuffer
            );
        }
    }
}
