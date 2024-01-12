﻿using Microsoft.Extensions.Logging;

namespace LiveStreamingServerNet.Flv.Internal.Logging
{
    internal static partial class LoggerExtensions
    {
        [LoggerMessage(LogLevel.Error, "ClientId: {ClientId} | An error occurred while sending media message")]
        public static partial void FailedToSendMediaMessage(this ILogger logger, uint clientId, Exception exception);

        [LoggerMessage(LogLevel.Debug, "ClientId: {ClientId} | Resume media package | Outstanding media message size: {OutstandingPackagesSize} | count: {OutstandingPackagesCount}")]
        public static partial void ResumeMediaPackage(this ILogger logger, uint clientId, long outstandingPackagesSize, long outstandingPackagesCount);

        [LoggerMessage(LogLevel.Debug, "ClientId: {ClientId} | Pause media package | Outstanding media message size: {OutstandingPackagesSize} | count: {OutstandingPackagesCount}")]
        public static partial void PauseMediaPackage(this ILogger logger, uint clientId, long outstandingPackagesSize, long outstandingPackagesCount);
    }
}
