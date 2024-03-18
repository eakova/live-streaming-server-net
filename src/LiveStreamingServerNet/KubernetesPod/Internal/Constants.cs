﻿namespace LiveStreamingServerNet.KubernetesPod.Internal
{
    internal static class Constants
    {
        public const string PendingStopLabel = "live-streaming-server-net/pending-stop";
        public const string StreamsCountAnnotation = "live-streaming-server-net/streams-count";
    }
}
