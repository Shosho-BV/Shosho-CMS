using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Shosho.CMS
{
    public static class CMSFileHandler
    {

        public static IReadOnlyCollection<MediaRef> Extract(JToken root)
        {
            var results = new Dictionary<int, MediaRef>();
            Traverse(root, results);
            return results.Values;
        }

        private static void Traverse(JToken token, IDictionary<int, MediaRef> results)
        {
            if (token == null)
                return;

            // Check if this token is a media container
            if (TryExtractMedia(token, results))
                return;

            // Recurse
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var property in ((JObject)token).Properties())
                        Traverse(property.Value, results);
                    break;

                case JTokenType.Array:
                    foreach (var item in (JArray)token)
                        Traverse(item, results);
                    break;
            }
        }

        private static bool TryExtractMedia(JToken token, IDictionary<int, MediaRef> results)
        {
            
            var data = token;
            if (data == null)
                return false;

            // Single media
            if (data.Type == JTokenType.Object && IsMediaItem(data))
            {
                AddMedia(data, results);
                return true;
            }

            // Multiple media
            if (data.Type == JTokenType.Array)
            {
                bool found = false;
                foreach (var item in data)
                {
                    if (IsMediaItem(item))
                    {
                        AddMedia(item, results);
                        found = true;
                    }
                }
                return found;
            }

            return false;
        }

        private static bool IsMediaItem(JToken item)
        {
            return item?["mime"] != null;
        }

        private static void AddMedia(JToken item, IDictionary<int, MediaRef> results)
        {
            int id = item["id"]!.Value<int>();

            // Deduplicate by ID
            if (results.ContainsKey(id))
                return;


            results[id] = new MediaRef
            {
                Id = id,
                Mime = item!["mime"]!.Value<string>(),
                Url = item["url"]!.Value<string>(),
                Hash = item["hash"]?.Value<string>(),
                UpdatedAt = item["updatedAt"].Value<string>()
            };
        }

    }

    public class MediaRef
    {
        public int Id { get; set; }
        public string Mime { get; set; }
        public string Url { get; set; }
        public string Hash { get; set; }
        public string UpdatedAt { get; set; }

    }
}
