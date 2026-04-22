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
                    Debug.LogWarning($"Failed to deserialize file {filename} because it does not match the expected pattern for document files.");
                    continue;
                }
                var json = File.ReadAllText(file);
                var root = JObject.Parse(json);
                var entry = (JObject)root.DeepClone();
                if(entry["localizations"] != null)
                    entry.Remove("localizations");
                        
                    try { l.Add(entry.ToObject<T>()); }
                    catch (Exception e) { Debug.LogError($"Error deserializing entry with id {entry["documentId"]}: {e.Message}"); }


                var localizations = root["localizations"] as JArray;

                if (localizations != null)
                {
                    foreach (JToken loc in localizations)
                    {
                        try
                        {
                            // Merge localization into root
                            var merged = (JObject)root.DeepClone();
                            foreach (var property in loc.Children<JProperty>())
                            {
                                merged[property.Name] = property.Value;
                            }

                            merged.Remove("localizations");

                            l.Add(merged.ToObject<T>());
                        }
                        catch
                        {
                            Debug.LogError($"Error deserializing localization entry with id {root["documentId"]}");
                        }
                    }
                }
            }
            
            return l;
        }

        public static List<T> DeserializeToList<T>(int endpointIndex)
        {

            List<T> l = new List<T>();

            string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{CMS.cmsSettings.restEndpoints[endpointIndex].name}";

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                string filename = Path.GetFileName(file);
                if (CMS.IsDocumentFile(filename) == false)
                {
                    Debug.LogWarning($"Failed to deserialize file {filename} because it does not match the expected pattern for document files.");
                    continue;
                }
                var json = File.ReadAllText(file);
                var root = JObject.Parse(json);
                var entry = (JObject)root.DeepClone();
                if (entry["localizations"] != null)
                    entry.Remove("localizations");

                try { l.Add(entry.ToObject<T>()); }
                catch (Exception e) { Debug.LogError($"Error deserializing entry with id {entry["documentId"]}: {e.Message}"); }


                var localizations = root["localizations"] as JArray;

                if (localizations != null)
                {
                    foreach (JToken loc in localizations)
                    {
                        try
                        {
                            // Merge localization into root
                            var merged = (JObject)root.DeepClone();
                            foreach (var property in loc.Children<JProperty>())
                            {
                                merged[property.Name] = property.Value;
                            }

                            merged.Remove("localizations");


                            l.Add(merged.ToObject<T>());
                        }
                        catch (Exception e) 
                        { 
                            Debug.LogError($"Error deserializing localization entry with id {root["documentId"]}: {e.Message}"); 
                        }
                    }
                }
            }

            return l;
        }

        public static List<T> DeserializeToList<T>(string endpoint,string locale)
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
                        var entry = root.ToObject<T>();
                        l.Add(entry);
                    }
                    catch (Exception e) { Debug.LogError($"Error deserializing entry with id {root["documentId"]}: {e.Message}"); }
                }
                else
                {
                    var localizations = root["localizations"];
                    //check if the files has a localizations field
                    if (localizations?.HasValues == true )
                    {
                        foreach (JToken loc in localizations)
                        {
                            try
                            {
                                // Merge localization into root
                                var merged = (JObject)root.DeepClone();
                                foreach (var property in loc.Children<JProperty>())
                                {
                                    merged[property.Name] = property.Value;
                                }
                                l.Add(merged.ToObject<T>());
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Error deserializing localization entry with id {root["documentId"]}: {e.Message}");
                            }
                        }
                    }                          
                }
            }
            
            return l;
        }

        public static List<T> DeserializeToList<T>(int endpointIndex, string locale)
        {

            List<T> l = new List<T>();

            string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{CMS.cmsSettings.restEndpoints[endpointIndex].name}";

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var json = File.ReadAllText(file);
                var root = JObject.Parse(json);

                //if the locale we are looking for is the root then add the root to the list
                if (root["locale"]?.Value<string>() == locale)
                {
                    try
                    {
                        var entry = root.ToObject<T>();
                        l.Add(entry);
                    }
                    catch(Exception e) { Debug.LogError($"Error deserializing entry with id {root["documentId"]}: {e.Message}"); }
                }
                else
                {
                    var localizations = root["localizations"];
                    //check if the files has a localizations field
                    if (localizations?.HasValues == true)
                    {
                        foreach (JToken loc in localizations)
                        {
                            try
                            {
                                // Merge localization into root
                                var merged = (JObject)root.DeepClone();
                                foreach (var property in loc.Children<JProperty>())
                                {
                                    merged[property.Name] = property.Value;
                                }
                                l.Add(merged.ToObject<T>());
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Error deserializing localization entry with id {root["documentId"]}: {e.Message}");
                            }
                        }
                    }
                }
            }

            return l;
        }
    
    }
}

