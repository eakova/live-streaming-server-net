﻿using LiveStreamingServerNet.Newtorking.Contracts;
using LiveStreamingServerNet.Rtmp.Contracts;

namespace LiveStreamingServerNet.Rtmp
{
    public class RtmpChunkStreamContext : IRtmpChunkStreamContext
    {
        public uint ChunkStreamId { get; }
        public int ChunkType { get; set; }
        public bool IsFirstChunkOfMessage => PayloadBuffer == null;
        public IRtmpChunkMessageHeaderContext MessageHeader { get; }
        public INetBuffer? PayloadBuffer { get; set; }

        public RtmpChunkStreamContext(uint chunkStreamId)
        {
            ChunkStreamId = chunkStreamId;
            MessageHeader = new RtmpChunkMessageHeaderContext();
        }
    }

    public class RtmpChunkMessageHeaderContext : IRtmpChunkMessageHeaderContext
    {
        public uint Timestamp { get; set; }
        public uint TimestampDelta { get; set; }
        public int MessageLength { get; set; }
        public byte MessageTypeId { get; set; }
        public uint MessageStreamId { get; set; }
        public bool HasExtendedTimestamp { get; set; }
    }
}
