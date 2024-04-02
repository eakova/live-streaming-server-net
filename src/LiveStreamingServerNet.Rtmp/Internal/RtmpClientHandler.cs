﻿using LiveStreamingServerNet.Networking;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.Contracts;
using LiveStreamingServerNet.Rtmp.Internal.RtmpEvents;
using LiveStreamingServerNet.Rtmp.Logging;
using LiveStreamingServerNet.Rtmp.RateLimiting.Contracts;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Rtmp.Internal
{
    internal class RtmpClientHandler : IRtmpClientHandler
    {
        private readonly IMediator _mediator;
        private readonly IRtmpServerConnectionEventDispatcher _eventDispatcher;
        private readonly ILogger _logger;
        private readonly IBandwidthLimiter? _bandwidthLimiter;

        private IRtmpClientContext _clientContext = default!;

        public RtmpClientHandler(
            IMediator mediator,
            IRtmpServerConnectionEventDispatcher eventDispatcher,
            ILogger<RtmpClientHandler> logger,
            IBandwidthLimiterFactory? bandwidthLimiterFactory = null)
        {
            _mediator = mediator;
            _eventDispatcher = eventDispatcher;
            _logger = logger;
            _bandwidthLimiter = bandwidthLimiterFactory?.Create();
        }

        public async Task InitializeAsync(IClientHandle client)
        {
            _clientContext = new RtmpClientContext(client);
            await OnRtmpClientCreatedAsync();
        }

        public async Task<bool> HandleClientLoopAsync(ReadOnlyStream networkStream, CancellationToken cancellationToken)
        {
            try
            {
                var result = _clientContext.State switch
                {
                    RtmpClientState.HandshakeC0 => await HandleHandshakeC0(_clientContext, networkStream, cancellationToken),
                    RtmpClientState.HandshakeC1 => await HandleHandshakeC1(_clientContext, networkStream, cancellationToken),
                    RtmpClientState.HandshakeC2 => await HandleHandshakeC2(_clientContext, networkStream, cancellationToken),
                    _ => await HandleChunkAsync(_clientContext, networkStream, cancellationToken),
                };

                if (result.Succeeded && _bandwidthLimiter != null && !_bandwidthLimiter.ConsumeBandwidth(result.ConsumedBytes))
                {
                    _logger.ExceededBandwidthLimit(_clientContext.Client.ClientId);
                    return false;
                }

                return result.Succeeded;
            }
            catch (Exception ex)
            {
                _logger.ClientLoopError(_clientContext.Client.ClientId, ex);
                return false;
            }
        }

        private async Task<RtmpEventConsumingResult> HandleHandshakeC0(IRtmpClientContext clientContext, ReadOnlyStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpHandshakeC0Event(clientContext, networkStream), cancellationToken);
        }

        private async Task<RtmpEventConsumingResult> HandleHandshakeC1(IRtmpClientContext clientContext, ReadOnlyStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpHandshakeC1Event(clientContext, networkStream), cancellationToken);
        }

        private async Task<RtmpEventConsumingResult> HandleHandshakeC2(IRtmpClientContext clientContext, ReadOnlyStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpHandshakeC2Event(clientContext, networkStream), cancellationToken);
        }

        private async Task<RtmpEventConsumingResult> HandleChunkAsync(IRtmpClientContext clientContext, ReadOnlyStream networkStream, CancellationToken cancellationToken)
        {
            return await _mediator.Send(new RtmpChunkEvent(clientContext, networkStream), cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await OnRtmpClientDisposedAsync();

            if (_bandwidthLimiter != null)
                await _bandwidthLimiter.DisposeAsync();
        }

        private async Task OnRtmpClientCreatedAsync()
        {
            await _eventDispatcher.RtmpClientCreatedAsync(_clientContext);
        }

        private async Task OnRtmpClientDisposedAsync()
        {
            await _eventDispatcher.RtmpClientDisposedAsync(_clientContext);
        }
    }
}
