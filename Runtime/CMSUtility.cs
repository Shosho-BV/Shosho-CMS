using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace Shosho.CMS
{
    public static class CMSUtility
    {
        /// <summary>
        ///  Find an item in the CMS
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <param name="locale"></param>
        /// <returns> Returns the first item in an endpoint of where fieldName matches value of a specified locale</returns>
        public static string Find(string endpoint, string fieldName, string value, string locale)
        {
            try
            {
                string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{endpoint}";

                foreach (var file in Directory.EnumerateFiles(folderPath))
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);

                    if (root["locale"]?.ToString() == locale)
                    {
                        var match = root.Properties()
                        .FirstOrDefault(p =>
                            p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) &&
                            p.Value?.Type == JTokenType.String &&
                            p.Value.ToString().Equals(value, StringComparison.OrdinalIgnoreCase));

                        if (match != null)
                            return root.ToString();
                    }

                    // 2️⃣ Check localizations
                    var localizations = root["localizations"] as JArray;

                    if (localizations != null)
                    {
                        foreach (JToken loc in localizations)
                        {
                                if (loc["locale"]?.ToString() == locale)
                                {
                                    var match = loc
                                    .OfType<JProperty>()
                                    .FirstOrDefault(p =>
                                        p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) &&
                                        p.Value?.Type == JTokenType.String &&
                                    p.Value.ToString().Equals(value, StringComparison.OrdinalIgnoreCase));

                                    if (match != null)
                                    {
                                        // Merge localization into root
                                        var merged = (JObject)root.DeepClone();
                                        foreach (var property in loc.Children<JProperty>())
                                        {
                                            merged[property.Name] = property.Value;
                                        }
                                        merged.Remove("localizations");
                                        return merged.ToString();
                                    }
                                }                            
                        }
                    }
                }
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Unexpected error: {e.Message}");
                return null;
            }
        }

        public static List<T> DeserializeToList<T>(string endpoint)
        {

            List<T> l = new List<T>();

            string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{endpoint}";

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                string filename = Path.GetFileName(file);
                if(CMS.IsDocumentFile(filename) == false)
                {
                    Debug.LogWarning($"Failed to deserialize file '{filename}' from '{endpoint}' because it does not match the expected pattern for document files.");
                    continue;
                }
                var json = File.ReadAllText(file);
                try
                {
                    var jObject = JObject.Parse(json);

                    l.Add(jObject.ToObject<T>());
                }
                catch(Exception e) 
                {
                        Debug.Log($"Failed to deserialize file '{filename}' from '{endpoint}':  {e}");
                }
            }

            return l;
        }

        public static List<T> DeserializeToList<T>(int endpointIndex) => DeserializeToList<T>(CMS.cmsSettings.restEndpoints[endpointIndex].name);

        public static List<T> DeserializeToListFromLocale<T>(string endpoint,string locale)
        {

            List<T> l = new List<T>();

            string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{endpoint}";

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var json = File.ReadAllText(file);
                var root = JObject.Parse(json);

                //if the locale we are looking for is the root then add the root to the list
                if (root["locale"]?.Value<string>() == locale)
                {
                    try
                    {
                        root.Remove("localizations");
                        var entry = root.ToObject<T>();
                        l.Add(entry);
                    }
                    catch (Exception e) { Debug.LogError($"Error deserializing entry with id {root["documentId"]}: {e.Message}"); }
                }
                else
                {
                    var localizations = root["localizations"];
                    //check if the file has a localizations field
                    if (localizations?.HasValues == true )
                    {
                        foreach (JObject jObject in localizations)
                        {
                            if (jObject["locale"].Value<string>() == locale)
                                l.Add(jObject.ToObject<T>());                        
                        }
                    }
                    else
                    {
                        Debug.LogError($"Locale: {locale} not found for item {root["documentId"]}");

                    }
                }
            }
            
            return l;
        }

        public static List<T> DeserializeToListFromLocale<T>(int endpointIndex, string locale) => DeserializeToListFromLocale<T>(CMS.cmsSettings.restEndpoints[endpointIndex].name, locale);
    
    }
}

