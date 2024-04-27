﻿using LiveStreamingServerNet.Flv.Internal.Contracts;
using LiveStreamingServerNet.Flv.Internal.Services.Contracts;
using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Flv.Internal.Services
{
    internal class FlvClientFactory : IFlvClientFactory
    {
        private readonly IFlvWriterFactory _flvWriterFactory;
        private readonly IFlvMediaTagManagerService _mediaTagManager;
        private readonly ILogger<FlvClient> _logger;

        public FlvClientFactory(
            IFlvWriterFactory flvWriterFactory,
            IFlvMediaTagManagerService mediaTagManager,
            ILogger<FlvClient> logger)
        {
            _flvWriterFactory = flvWriterFactory;
            _mediaTagManager = mediaTagManager;
            _logger = logger;
        }

        public IFlvClient Create(string clientId, string streamPath, IStreamWriter streamWriter, CancellationToken stoppingToken)
        {
            var flvWriter = _flvWriterFactory.Create(streamWriter);
            return new FlvClient(clientId, streamPath, _mediaTagManager, flvWriter, _logger, stoppingToken);
        }
    }
}
