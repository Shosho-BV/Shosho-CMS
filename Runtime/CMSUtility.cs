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
            string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{endpoint}";

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                var json = File.ReadAllText(file);
                var root = JObject.Parse(json);

                if (root["locale"]?.ToString() == locale)
                {
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
                                p.Value.Type == JTokenType.String &&
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
                            else
                            {
                                return null;
                            }
                        }
                    }
                }


            }
            return null;
        }

        public static List<T> DeserializeToList<T>()
        {

            List<T> l = new List<T>();


            for (int i = 0; i < CMS.cmsSettings.restEndpoints.Count; i++)
            {

                string folderPath = $"{Application.persistentDataPath}/content/{CMS.cmsSettings.restEndpoints[i].name}";

                foreach (var file in Directory.EnumerateFiles(folderPath))
                {
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
                            // Merge localization into root
                            var merged = (JObject)root.DeepClone();
                            foreach (var property in loc.Children<JProperty>())
                            {
                                merged[property.Name] = property.Value;
                            }

                            merged.Remove("localizations");

                            l.Add(merged.ToObject<T>());
                        }
                    }
                }
            }
            return l;
        }

        public static List<T> DeserializeToList<T>(string locale)
        {

            List<T> l = new List<T>();


            for (int i = 0; i < CMS.cmsSettings.restEndpoints.Count; i++)
            {

                string folderPath = $"{Application.persistentDataPath}/{CMS.cmsSettings.localFileDir}/{CMS.cmsSettings.restEndpoints[i].name}";

                foreach (var file in Directory.EnumerateFiles(folderPath))
                {
                    var json = File.ReadAllText(file);
                    var root = JObject.Parse(json);

                    //if the locale we are looking for is the root then add the root to the list
                    if (root["locale"].Value<string>() == locale)
                    {
                       var entry = root.ToObject<T>();
                        l.Add(entry);
                    }
                    else
                    {
                        var localizations = root["localizations"];
                        //check if the files has a localizations field
                        if (localizations?.HasValues == true )
                        {
                            foreach (JToken loc in localizations)
                            {
                                // Merge localization into root
                                var merged = (JObject)root.DeepClone();
                                foreach (var property in loc.Children<JProperty>())
                                {
                                    merged[property.Name] = property.Value;
                                }
                                l.Add(merged.ToObject<T>());
                            }
                        }                          
                    }
               }
            }
            return l;
        }


        public static List<string> GetLanguageOptions() 
        { 
            List <string> l = new List<string>();
            string languageSettingsPath = Application.streamingAssetsPath + "languagesettings.json";
            if (File.Exists(languageSettingsPath))
            {
                string json = File.ReadAllText(languageSettingsPath);
                return JsonConvert.DeserializeObject<List<string>>(json);

            }
            else
            {
                Debug.LogError("Error: Language settings not found");
                return null;
            }
        }


        

    }
}

