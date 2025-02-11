﻿using LiveStreamingServerNet.Flv.Internal.Contracts;

namespace LiveStreamingServerNet.Flv.Internal
{
    internal class FlvStreamContext : IFlvStreamContext
    {
        public string StreamPath { get; }
        public IReadOnlyDictionary<string, string> StreamArguments { get; }
        public IReadOnlyDictionary<string, object>? StreamMetaData { get; set; }
        public byte[]? VideoSequenceHeader { get; set; }
        public byte[]? AudioSequenceHeader { get; set; }
        public IGroupOfPicturesCache GroupOfPicturesCache { get; }
        public bool IsReady => VideoSequenceHeader != null || AudioSequenceHeader != null;

        public FlvStreamContext(string streamPath, IReadOnlyDictionary<string, string> streamArguments)
        {
            StreamPath = streamPath;
            StreamArguments = new Dictionary<string, string>(streamArguments);
            GroupOfPicturesCache = new GroupOfPicturesCache();
        }
    }

    internal class GroupOfPicturesCache : IGroupOfPicturesCache
    {
        private readonly Queue<PicturesCache> _groupOfPicturesCache = new();

        public void Add(PicturesCache cache)
        {
            lock (_groupOfPicturesCache)
            {
                _groupOfPicturesCache.Enqueue(cache);
            }
        }

        public void Clear(bool unclaim)
        {
            lock (_groupOfPicturesCache)
            {
                if (unclaim)
                {
                    foreach (var cache in _groupOfPicturesCache)
                        cache.Payload.Unclaim();
                }

                _groupOfPicturesCache.Clear();
            }
        }

        public IList<PicturesCache> Get(bool claim)
        {
            lock (_groupOfPicturesCache)
            {
                if (claim)
                {
                    foreach (var cache in _groupOfPicturesCache)
                        cache.Payload.Claim();
                }

                return new List<PicturesCache>(_groupOfPicturesCache);
            }
        }
    }
}
