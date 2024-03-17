using KubeOps.Operator;
using LiveStreamingServerNet.Operator.Installers;
using Polly;

namespace LiveStreamingServerNet.Operator
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddKubernetes()
                            .AddControllerServices()
                            .AddResiliencePipelines();

            builder.Services.AddKubernetesOperator()
                            .RegisterComponents();

            builder.Services.AddHealthChecks();

            var app = builder.Build();

            app.UseHealthChecks("/healthz");

            await app.RunAsync();
        }
    }
}
