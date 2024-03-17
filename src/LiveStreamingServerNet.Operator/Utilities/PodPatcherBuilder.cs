﻿using k8s.Models;
using LiveStreamingServerNet.Operator.Utilities.Contracts;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json.Serialization;
using System.Text.Json;

namespace LiveStreamingServerNet.Operator.Utilities
{
    public class PodPatcherBuilder : IPodPatcherBuilder
    {
        private readonly JsonPatchDocument<V1Pod> _doc;

        private PodPatcherBuilder()
        {
            _doc = new JsonPatchDocument<V1Pod>();
            _doc.ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() };
        }

        public static PodPatcherBuilder Create()
        {
            return new PodPatcherBuilder();
        }

        public IPodPatcherBuilder SetLabel(string key, string value)
        {
            _doc.Replace(x => x.Metadata.Labels[key], value);
            return this;
        }

        public IPodPatcherBuilder RemoveLabel(string key)
        {
            _doc.Remove(x => x.Metadata.Labels[key]);
            return this;
        }

        public IPodPatcherBuilder SetAnnotation(string key, string value)
        {
            _doc.Replace(x => x.Metadata.Annotations[key], value);
            return this;
        }

        public IPodPatcherBuilder RemoveAnnotation(string key)
        {
            _doc.Remove(x => x.Metadata.Annotations[key]);
            return this;
        }

        public V1Patch Build()
        {
            var jsonPatch = JsonSerializer.Serialize(
                _doc.Operations.Select(o => new
                {
                    o.op,
                    o.path,
                    o.value
                })
            );

            return new V1Patch(jsonPatch, V1Patch.PatchType.JsonPatch);
        }
    }
}
