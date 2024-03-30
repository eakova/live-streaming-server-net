﻿using LiveStreamingServerNet.KubernetesOperator.Entities;
using LiveStreamingServerNet.KubernetesOperator.Models;

namespace LiveStreamingServerNet.KubernetesOperator.Services.Contracts
{
    public interface IDesiredStateApplier
    {
        Task ApplyDesiredStateAsync(
            V1LiveStreamingServerFleet entity,
            FleetState currentState,
            DesiredFleetStateChange desiredStateChange,
            CancellationToken cancellationToken);
    }
}
