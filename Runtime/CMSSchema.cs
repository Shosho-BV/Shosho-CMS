using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace Shosho.CMS
{
    internal static class CMSSchema
    {
        private static readonly HashSet<string> SkipRelationFields = new HashSet<string>
        {
            "localizations", "createdBy", "updatedBy"
        };

        internal static IEnumerator BuildPopulateStrings(
            string baseUrl,
            string apiToken,
            List<string> endpointNames,
            System.Action<Dictionary<string, string>> onComplete)
        {
            var result = new Dictionary<string, string>();
            JArray contentTypes = null;

            yield return FetchArray(baseUrl + "/api/content-type-builder/content-types", apiToken, r => contentTypes = r);

            if (contentTypes == null)
            {
                Debug.LogWarning("[CMSSchema] Schema niet beschikbaar, terugvallen op populate=*");
                foreach (var name in endpointNames)
                    result[name] = "populate=*&populate[localizations]=*";
                onComplete(result);
                yield break;
            }

            foreach (var endpointName in endpointNames)
            {
                var (attributes, isLocalized) = FindContentType(contentTypes, endpointName);
                if (attributes == null)
                {
                    Debug.LogWarning($"[CMSSchema] Geen schema gevonden voor '{endpointName}', terugvallen op populate=*");
                    result[endpointName] = "populate=*&populate[localizations]=*";
                    continue;
                }

                var parts = BuildPopulateParts(attributes, "populate");
                if (isLocalized)
                    parts.AddRange(BuildPopulateParts(attributes, "populate[localizations][populate]"));
                result[endpointName] = parts.Count > 0 ? string.Join("&", parts) : "populate=*";
                Debug.Log($"[CMSSchema] {endpointName} populate: {result[endpointName]}");
            }

            onComplete(result);
        }

        private static IEnumerator FetchArray(string url, string apiToken, System.Action<JArray> onComplete)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", "Bearer " + apiToken);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var obj = JObject.Parse(request.downloadHandler.text);
                    onComplete(obj["data"]?.Value<JArray>());
                }
                else
                {
                    Debug.LogWarning($"[CMSSchema] {url}: {request.error}");
                    onComplete(null);
                }
            }
        }

        private static (JObject attributes, bool isLocalized) FindContentType(JArray contentTypes, string endpointName)
        {
            foreach (JObject ct in contentTypes)
            {
                if (ct["schema"]?["pluralName"]?.Value<string>() == endpointName)
                {
                    var attributes = ct["schema"]?["attributes"]?.Value<JObject>();
                    var localized = ct["schema"]?["pluginOptions"]?["i18n"]?["localized"]?.Value<bool>() ?? false;
                    return (attributes, localized);
                }
            }
            return (null, false);
        }

        private static List<string> BuildPopulateParts(JObject attributes, string prefix)
        {
            var parts = new List<string>();

            foreach (var kvp in attributes)
            {
                var fieldName = kvp.Key;
                var attr = kvp.Value as JObject;
                if (attr == null) continue;

                var type = attr["type"]?.Value<string>();

                switch (type)
                {
                    case "relation":
                        if (SkipRelationFields.Contains(fieldName)) break;
                        parts.Add($"{prefix}[{fieldName}][fields][0]=documentId");
                        break;

                    case "media":
                        parts.Add($"{prefix}[{fieldName}]=true");
                        break;

                    case "component":
                    case "dynamiczone":
                        parts.Add($"{prefix}[{fieldName}][populate]=*");
                        break;
                }
            }

            return parts;
        }
    }
}
