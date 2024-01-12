﻿using LiveStreamingServerNet.Flv.Internal.Contracts;

namespace LiveStreamingServerNet.Flv.Internal
{
    internal class FlvClient : IFlvClient
    {
        public uint ClientId { get; private set; }
        public string StreamPath { get; private set; } = default!;
        public IFlvWriter FlvWriter { get; private set; } = default!;

        public Task InitializationTask => _initializationTcs.Task;
        private readonly TaskCompletionSource _initializationTcs = new();

        private CancellationTokenSource? _stoppingCts;
        private TaskCompletionSource? _taskCompletionSource;
        private Task? _completeTask;

        public void Initialize(uint clientId, string streamPath, IStreamWriter streamWriter, CancellationToken stoppingToken)
        {
            ClientId = clientId;
            StreamPath = streamPath;
            FlvWriter = new FlvWriter(this, streamWriter);

            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _taskCompletionSource = new TaskCompletionSource();
            _stoppingCts.Token.Register(() => _taskCompletionSource.TrySetResult());

            _completeTask = _taskCompletionSource.Task;
        }

        public void CompleteInitialization()
        {
            _initializationTcs.SetResult();
        }

        public Task UntilComplete()
        {
            return _completeTask ?? Task.CompletedTask;
        }

        public void Stop()
        {
            _stoppingCts?.Cancel();
        }

        public async ValueTask DisposeAsync()
        {
            if (_stoppingCts != null)
                _stoppingCts.Dispose();

            await FlvWriter.DisposeAsync();
        }
    }
}
