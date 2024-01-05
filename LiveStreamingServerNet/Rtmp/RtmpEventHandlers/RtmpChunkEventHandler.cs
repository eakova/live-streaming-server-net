﻿using LiveStreamingServerNet.Newtorking.Contracts;
using LiveStreamingServerNet.Rtmp.Contracts;
using LiveStreamingServerNet.Rtmp.RtmpEventHandlers.MessageDispatcher.Contracts;
using LiveStreamingServerNet.Rtmp.RtmpEvents;
using LiveStreamingServerNet.Rtmp.RtmpHeaders;
using LiveStreamingServerNet.Rtmp.Services.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Rtmp.RtmpEventHandlers
{
    public class RtmpChunkEventHandler : IRequestHandler<RtmpChunkEvent, bool>
    {
        private readonly INetBufferPool _netBufferPool;
        private readonly IRtmpMessageDispatcher _dispatcher;
        private readonly IRtmpProtocolControlMessageSenderService _protocolControlMessageSender;
        private readonly ILogger _logger;

        public RtmpChunkEventHandler(
            INetBufferPool netBufferPool,
            IRtmpMessageDispatcher dispatcher,
            IRtmpProtocolControlMessageSenderService protocolControlMessageSender,
            ILogger<RtmpChunkEventHandler> logger)
        {
            _netBufferPool = netBufferPool;
            _dispatcher = dispatcher;
            _protocolControlMessageSender = protocolControlMessageSender;
            _logger = logger;
        }

        public async Task<bool> Handle(RtmpChunkEvent @event, CancellationToken cancellationToken)
        {
            using var netBuffer = _netBufferPool.Obtain();

            if (await HandleChunkEvent(@event, netBuffer, cancellationToken))
            {
                HandleAcknowlegement(@event, netBuffer.Size);
                return true;
            }

            _logger.LogError("PeerId: {PeerId} | Failed to handle chunk event", @event.PeerContext.Peer.PeerId);

            return false;
        }

        private async Task<bool> HandleChunkEvent(RtmpChunkEvent @event, INetBuffer netBuffer, CancellationToken cancellationToken)
        {
            var basicHeader = await RtmpChunkBasicHeader.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);

            var chunkType = basicHeader.ChunkType;
            var chunkStreamId = basicHeader.ChunkStreamId;

            var chunkStreamContext = @event.PeerContext.GetChunkStreamContext(chunkStreamId);

            var success = chunkType switch
            {
                0 => await HandleChunkType0Async(chunkStreamContext, @event, netBuffer, cancellationToken),
                1 => await HandleChunkType1Async(chunkStreamContext, @event, netBuffer, cancellationToken),
                2 => await HandleChunkType2Async(chunkStreamContext, @event, netBuffer, cancellationToken),
                3 => await HandleChunkType3Async(chunkStreamContext, @event, netBuffer, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(chunkType))
            };

            success &= await HandlePayloadAsync(chunkStreamContext, @event, netBuffer, cancellationToken);
            return success;
        }

        private void HandleAcknowlegement(RtmpChunkEvent @event, int bufferSize)
        {
            var peerContext = @event.PeerContext;

            if (peerContext.OutWindowAcknowledgementSize == 0)
                return;

            peerContext.SequenceNumber += (uint)bufferSize;
            if ((peerContext.SequenceNumber - peerContext.LastAcknowledgedSequenceNumber) >= peerContext.OutWindowAcknowledgementSize)
            {
                peerContext.LastAcknowledgedSequenceNumber = peerContext.SequenceNumber;
                _protocolControlMessageSender.Acknowledgement(peerContext, peerContext.SequenceNumber);

                const uint overflow = 0xf0000000;
                if (peerContext.SequenceNumber >= overflow)
                {
                    peerContext.SequenceNumber -= overflow;
                    peerContext.LastAcknowledgedSequenceNumber -= overflow;
                }
            }
        }

        private async Task<bool> HandleChunkType0Async(
            IRtmpChunkStreamContext chunkStreamContext,
            RtmpChunkEvent @event,
            INetBuffer netBuffer,
            CancellationToken cancellationToken)
        {
            var messageHeader = await RtmpChunkMessageHeaderType0.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);

            chunkStreamContext.ChunkType = 0;
            chunkStreamContext.MessageHeader.MessageLength = messageHeader.MessageLength;
            chunkStreamContext.MessageHeader.MessageTypeId = messageHeader.MessageTypeId;
            chunkStreamContext.MessageHeader.MessageStreamId = messageHeader.MessageStreamId;

            chunkStreamContext.MessageHeader.TimestampDelta = 0;
            chunkStreamContext.MessageHeader.HasExtendedTimestamp = messageHeader.HasExtendedTimestamp();
            if (chunkStreamContext.MessageHeader.HasExtendedTimestamp)
            {
                var extendedTimestampHeader = await RtmpChunkExtendedTimestampHeader.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);
                chunkStreamContext.MessageHeader.Timestamp = extendedTimestampHeader.extendedTimestamp;
            }
            else
            {
                chunkStreamContext.MessageHeader.Timestamp = messageHeader.Timestamp;
            }

            return true;
        }

        private async Task<bool> HandleChunkType1Async(IRtmpChunkStreamContext chunkStreamContext,
            RtmpChunkEvent @event,
            INetBuffer netBuffer,
            CancellationToken cancellationToken)
        {
            var messageHeader = await RtmpChunkMessageHeaderType1.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);

            chunkStreamContext.ChunkType = 1;
            chunkStreamContext.MessageHeader.MessageLength = messageHeader.MessageLength;
            chunkStreamContext.MessageHeader.MessageTypeId = messageHeader.MessageTypeId;

            chunkStreamContext.MessageHeader.HasExtendedTimestamp = messageHeader.HasExtendedTimestamp();
            if (chunkStreamContext.MessageHeader.HasExtendedTimestamp)
            {
                var extendedTimestampHeader = await RtmpChunkExtendedTimestampHeader.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);
                chunkStreamContext.MessageHeader.TimestampDelta = extendedTimestampHeader.extendedTimestamp;
            }
            else
            {
                chunkStreamContext.MessageHeader.TimestampDelta = messageHeader.TimestampDelta;
            }
            chunkStreamContext.MessageHeader.Timestamp += chunkStreamContext.MessageHeader.TimestampDelta;

            return true;
        }

        private async Task<bool> HandleChunkType2Async(
            IRtmpChunkStreamContext chunkStreamContext,
            RtmpChunkEvent @event,
            INetBuffer netBuffer,
            CancellationToken cancellationToken)
        {
            var messageHeader = await RtmpChunkMessageHeaderType2.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);

            chunkStreamContext.ChunkType = 2;

            chunkStreamContext.MessageHeader.HasExtendedTimestamp = messageHeader.HasExtendedTimestamp();
            if (chunkStreamContext.MessageHeader.HasExtendedTimestamp)
            {
                var extendedTimestampHeader = await RtmpChunkExtendedTimestampHeader.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken);
                chunkStreamContext.MessageHeader.TimestampDelta = extendedTimestampHeader.extendedTimestamp;
            }
            else
            {
                chunkStreamContext.MessageHeader.TimestampDelta = messageHeader.TimestampDelta;
            }
            chunkStreamContext.MessageHeader.Timestamp += chunkStreamContext.MessageHeader.TimestampDelta;

            return true;
        }

        private async Task<bool> HandleChunkType3Async(
            IRtmpChunkStreamContext chunkStreamContext,
            RtmpChunkEvent @event,
            INetBuffer netBuffer,
            CancellationToken cancellationToken)
        {
            chunkStreamContext.ChunkType = 3;

            var timestampDelta = chunkStreamContext.MessageHeader.HasExtendedTimestamp ?
                (await RtmpChunkExtendedTimestampHeader.ReadAsync(netBuffer, @event.NetworkStream, cancellationToken)).extendedTimestamp :
                chunkStreamContext.MessageHeader.TimestampDelta;

            if (chunkStreamContext.IsFirstChunkOfMessage)
            {
                chunkStreamContext.MessageHeader.TimestampDelta = timestampDelta;
                chunkStreamContext.MessageHeader.Timestamp += timestampDelta;
            }

            return true;
        }

        private async Task<bool> HandlePayloadAsync(IRtmpChunkStreamContext chunkStreamContext, RtmpChunkEvent @event, INetBuffer netBuffer, CancellationToken cancellationToken)
        {
            if (chunkStreamContext.IsFirstChunkOfMessage)
            {
                chunkStreamContext.PayloadBuffer = _netBufferPool.Obtain();
            }

            var peerContext = @event.PeerContext;
            var payloadBuffer = chunkStreamContext.PayloadBuffer!;
            int messageLength = chunkStreamContext.MessageHeader.MessageLength;

            if (payloadBuffer.Size < messageLength)
            {
                var chunkedPayloadLength = (int)Math.Min(
                    messageLength - payloadBuffer.Size,
                    peerContext.InChunkSize - payloadBuffer.Size % peerContext.InChunkSize
                );

                await netBuffer.CopyStreamData(@event.NetworkStream, chunkedPayloadLength, cancellationToken);
                netBuffer.CopyAllTo(payloadBuffer);
            }

            if (payloadBuffer.Size == messageLength)
            {
                payloadBuffer.Position = 0;
                return await DoHandlePayloadAsync(chunkStreamContext, @event.PeerContext, cancellationToken);
            }

            return true;
        }

        private async Task<bool> DoHandlePayloadAsync(IRtmpChunkStreamContext chunkStreamContext, IRtmpClientPeerContext peerContext, CancellationToken cancellationToken)
        {
            try
            {
                using var payloadBuffer = chunkStreamContext.PayloadBuffer ?? throw new InvalidOperationException();
                return await _dispatcher.DispatchAsync(chunkStreamContext, peerContext, cancellationToken);
            }
            finally
            {
                chunkStreamContext.PayloadBuffer = null;
            }
        }
    }
}
