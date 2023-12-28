﻿using LiveStreamingServer.Newtorking;
using LiveStreamingServer.Newtorking.Contracts;

namespace LiveStreamingServer.Rtmp.Core.RtmpHeaders
{
    public record struct RtmpChunkBasicHeader(int ChunkType, uint ChunkStreamId)
    {
        public static async Task<RtmpChunkBasicHeader> ReadAsync(INetBuffer netBuffer, ReadOnlyNetworkStream networkStream, CancellationToken cancellationToken)
        {
            await netBuffer.CopyStreamData(networkStream, 1, cancellationToken);

            var firstByte = netBuffer.ReadByte();
            var chunkStreamIdAttempt = (uint)(firstByte & 0x3f);

            var chunkType = firstByte >> 6;

            switch (chunkStreamIdAttempt)
            {
                case 0:
                    await netBuffer.CopyStreamData(networkStream, 1, cancellationToken);
                    return new RtmpChunkBasicHeader(chunkType, 64u + netBuffer.ReadByte());
                case 1:
                    await netBuffer.CopyStreamData(networkStream, 2, cancellationToken);
                    return new RtmpChunkBasicHeader(chunkType, 64u + netBuffer.ReadByte() + 256u * netBuffer.ReadByte());
                default:
                    return new RtmpChunkBasicHeader(chunkType, chunkStreamIdAttempt);
            }
        }

        public void Write(INetBuffer netBuffer)
        {
            var chunkStreamIdAttempt = ChunkStreamId;

            if (ChunkStreamId >= 64 && ChunkStreamId <= 319)
            {
                chunkStreamIdAttempt = 0;
            }
            else if (ChunkStreamId > 319)
            {
                chunkStreamIdAttempt = 1;
            }

            var firstByte = (ChunkType << 6) | (byte)(chunkStreamIdAttempt & 0x3f);

            netBuffer.Write(firstByte);

            switch (chunkStreamIdAttempt)
            {
                case 0:
                    netBuffer.Write((byte)(ChunkStreamId - 64));
                    break;
                case 1:
                    netBuffer.Write((byte)((ChunkStreamId - 64) % 256));
                    netBuffer.Write((byte)((ChunkStreamId - 64) / 256));
                    break;
            }
        }
    }
}
