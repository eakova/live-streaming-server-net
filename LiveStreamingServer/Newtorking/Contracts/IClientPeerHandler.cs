﻿namespace LiveStreamingServer.Newtorking.Contracts
{
    public interface IClientPeerHandler : IDisposable
    {
        Task<bool> HandleClientPeerLoopAsync(ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken);
    }
}
