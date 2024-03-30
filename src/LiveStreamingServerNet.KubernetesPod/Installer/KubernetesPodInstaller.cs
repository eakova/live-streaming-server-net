﻿using LiveStreamingServerNet.KubernetesPod.Installer.Contracts;
using LiveStreamingServerNet.KubernetesPod.Internal.HostedServices;
using LiveStreamingServerNet.KubernetesPod.Internal.Services;
using LiveStreamingServerNet.KubernetesPod.Internal.Services.Contracts;
using LiveStreamingServerNet.KubernetesPod.Services.Contracts;
using LiveStreamingServerNet.Rtmp.Installer.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace LiveStreamingServerNet.KubernetesPod.Installer
{
    public static class KubernetesPodInstaller
    {
        public static IRtmpServerConfigurator AddKubernetesPodServices(
            this IRtmpServerConfigurator configurator, Action<IKubernetesPodConfigurator>? configure = null)
        {
            var services = configurator.Services;

            services.AddHostedService<PodWatcherService>()
                    .AddSingleton<IKubernetesContext, KubernetesContext>()
                    .AddSingleton<IPodLifetimeManager, PodLifetimeManager>()
                    .AddSingleton<IPodStatus>(svc => svc.GetRequiredService<IPodLifetimeManager>());

            configurator.AddConnectionEventHandler(svc => svc.GetRequiredService<IPodLifetimeManager>())
                        .AddStreamEventHandler(svc => svc.GetRequiredService<IPodLifetimeManager>())
                        .AddAuthorizationHandler<PodAuthorizationHandler>();

            configure?.Invoke(new KubernetesPodConfigurator(services));

            return configurator;
        }
    }
}
