﻿using LiveStreamingServerNet.Flv.Internal.Contracts;
using LiveStreamingServerNet.Flv.Internal.Logging;
using LiveStreamingServerNet.Flv.Internal.MediaPackageDiscarding.Contracts;
using LiveStreamingServerNet.Rtmp;
using LiveStreamingServerNet.Utilities.Contracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace LiveStreamingServerNet.Flv.Internal.Services
{
    internal partial class FlvMediaTagManagerService
    {
        private readonly ConcurrentDictionary<IFlvClient, ClientMediaContext> _clientMediaContexts = new();
        private readonly ConcurrentDictionary<IFlvClient, Task> _clientTasks = new();

        private ClientMediaContext? GetMediaContext(IFlvClient clientContext)
        {
            return _clientMediaContexts.GetValueOrDefault(clientContext);
        }

        public void RegisterClient(IFlvClient client)
        {
            var mediaPackageDiscarder = _mediaPackageDiscarderFactory.Create(client.ClientId);
            var context = new ClientMediaContext(client, mediaPackageDiscarder);
            _clientMediaContexts[client] = context;

            var clientTask = Task.Run(() => ClientTask(context));
            _clientTasks[client] = clientTask;
            _ = clientTask.ContinueWith(_ => _clientTasks.TryRemove(client, out var _));
        }

        public void UnregisterClient(IFlvClient client)
        {
            if (_clientMediaContexts.TryRemove(client, out var context))
            {
                context.Stop();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Task.WhenAll(_clientTasks.Values);
        }

        private async Task ClientTask(ClientMediaContext context)
        {
            var client = context.Client;
            var cancellation = context.CancellationToken;

            try
            {
                await client.UntilIntializationComplete();

                while (!cancellation.IsCancellationRequested)
                {
                    var package = await context.ReadPackageAsync(cancellation);

                    try
                    {
                        await SendMediaTagAsync(
                            client,
                            package.MediaType,
                            package.RentedPayload.Buffer,
                            package.RentedPayload.Size,
                            package.Timestamp,
                            cancellation);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                    catch (Exception ex)
                    {
                        _logger.FailedToSendMediaMessage(client.ClientId, ex);
                    }
                    finally
                    {
                        package.RentedPayload.Unclaim();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (ChannelClosedException) { }

            while (context.ReadPackage(out var package))
            {
                package.RentedPayload.Unclaim();
            }
        }

        private class ClientMediaContext
        {
            public readonly IFlvClient Client;
            public readonly CancellationToken CancellationToken;
            public long OutstandingPackagesSize => _outstandingPackagesSize;
            public long OutstandingPackagesCount => _packageChannel.Reader.Count;

            private readonly IMediaPackageDiscarder _mediaPackageDiscarder;

            private readonly Channel<ClientMediaPackage> _packageChannel;
            private readonly CancellationTokenSource _cts;

            private long _outstandingPackagesSize;

            public ClientMediaContext(IFlvClient client, IMediaPackageDiscarder mediaPackageDiscarder)
            {
                Client = client;
                _mediaPackageDiscarder = mediaPackageDiscarder;

                _packageChannel = Channel.CreateUnbounded<ClientMediaPackage>();
                _cts = new CancellationTokenSource();
                CancellationToken = _cts.Token;
            }

            public void Stop()
            {
                _packageChannel.Writer.Complete();
                _cts.Cancel();
            }

            public bool AddPackage(ref ClientMediaPackage package)
            {
                if (ShouldSkipPackage(this, ref package))
                {
                    return false;
                }

                if (!_packageChannel.Writer.TryWrite(package))
                {
                    return false;
                }

                Interlocked.Add(ref _outstandingPackagesSize, package.RentedPayload.Size);
                return true;
            }

            public async ValueTask<ClientMediaPackage> ReadPackageAsync(CancellationToken cancellation)
            {
                var package = await _packageChannel.Reader.ReadAsync(cancellation);
                Interlocked.Add(ref _outstandingPackagesSize, -package.RentedPayload.Size);
                return package;
            }

            public bool ReadPackage(out ClientMediaPackage package)
            {
                return _packageChannel.Reader.TryRead(out package);
            }

            private bool ShouldSkipPackage(ClientMediaContext context, ref ClientMediaPackage package)
            {
                return _mediaPackageDiscarder.ShouldDiscardMediaPackage(
                    package.IsSkippable, context.OutstandingPackagesSize, context.OutstandingPackagesCount);
            }
        }

        private record struct ClientMediaPackage(
            MediaType MediaType,
            uint Timestamp,
            IRentedBuffer RentedPayload,
            bool IsSkippable);
    }
}
