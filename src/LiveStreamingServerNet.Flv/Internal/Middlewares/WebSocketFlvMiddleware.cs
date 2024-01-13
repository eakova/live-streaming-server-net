﻿using LiveStreamingServerNet.Flv.Configurations;
using LiveStreamingServerNet.Flv.Contracts;
using LiveStreamingServerNet.Flv.Internal.Contracts;
using LiveStreamingServerNet.Flv.Internal.Extensions;
using LiveStreamingServerNet.Flv.Internal.Services.Contracts;
using LiveStreamingServerNet.Flv.Internal.WebSocketClients.Contracts;
using LiveStreamingServerNet.Networking.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace LiveStreamingServerNet.Flv.Internal.Middlewares
{
    internal class WebSocketFlvMiddleware
    {
        private readonly IWebSocketFlvClientFactory _clientFactory;
        private readonly IFlvStreamManagerService _streamManager;
        private readonly IFlvClientHandler _clientHandler;

        private readonly IStreamPathResolver _streamPathResolver;
        private readonly WebSocketAcceptContext _webSocketAcceptContext;

        private readonly RequestDelegate _next;

        public WebSocketFlvMiddleware(IServer server, WebSocketFlvOptions options, RequestDelegate next)
        {
            _clientFactory = server.Services.GetRequiredService<IWebSocketFlvClientFactory>();
            _streamManager = server.Services.GetRequiredService<IFlvStreamManagerService>();
            _clientHandler = server.Services.GetRequiredService<IFlvClientHandler>();

            _streamPathResolver = options?.StreamPathResolver ?? new DefaultStreamPathResolver();
            _webSocketAcceptContext = options?.WebSocketAcceptContext ?? new WebSocketAcceptContext();

            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest ||
                !context.ValidateNoEndpointDelegate() ||
                !context.ValidateGetOrHeadMethod() ||
                !_streamPathResolver.ResolveStreamPathAndArguments(context, out var streamPath, out var streamArguments))
            {
                await _next.Invoke(context);
                return;
            }

            await TryServeWebSocketFlv(context, streamPath, streamArguments);
        }

        private async Task TryServeWebSocketFlv(HttpContext context, string streamPath, IDictionary<string, string> streamArguments)
        {
            if (!_streamManager.IsStreamPathPublishing(streamPath))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await SubscribeToStreamAsync(context, streamPath, streamArguments);
        }

        private IFlvClient CreateClient(WebSocket webSocket, string streamPath, CancellationToken cancellation)
        {
            return _clientFactory.CreateClient(webSocket, streamPath, cancellation);
        }

        private async Task SubscribeToStreamAsync(HttpContext context, string streamPath, IDictionary<string, string> streamArguments)
        {
            var cancellation = context.RequestAborted;

            var webSocket = await context.WebSockets.AcceptWebSocketAsync(_webSocketAcceptContext);
            await using var client = CreateClient(webSocket, streamPath, cancellation);

            switch (_streamManager.StartSubscribingStream(client, streamPath))
            {
                case SubscribingStreamResult.Succeeded:
                    await _clientHandler.RunClientAsync(client);
                    return;
                case SubscribingStreamResult.StreamDoesntExist:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                case SubscribingStreamResult.AlreadySubscribing:
                    throw new InvalidOperationException("Already subscribing");
            }
        }
    }
}
