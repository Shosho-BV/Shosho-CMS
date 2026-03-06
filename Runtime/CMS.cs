using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using Unity.EditorCoroutines.Editor;
#endif
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Shosho.CMS 
{
    public static class CMS
    {
        public static bool isValidating { get; private set; }

        public static bool isSyncing { get; private set; }
        public static float progress { get; private set; }
        public static string progressStatus;

        public static CMSSettings cmsSettings = null;
        public static string cmsSettingsPath { get; private set; } = Application.streamingAssetsPath + "/CMSSettings.json";
        private static string localFilePath;
        private static string apiURL;

        private static string maxUpdatedAt;

        private static List<string> languageCodes = new List<string>();


#if UNITY_EDITOR
        [InitializeOnLoadMethodAttribute]
#elif UNITY_STANDALONE
        [RuntimeInitializeOnLoadMethod]
#endif

        static void Init()
        {
            Debug.Log("Initializing cms");
            if (File.Exists(cmsSettingsPath))
            {
                cmsSettings = JsonConvert.DeserializeObject<CMSSettings>(File.ReadAllText(cmsSettingsPath));
            }
            else
            {
#if UNITY_EDITOR
                cmsSettings = new CMSSettings();
#endif
            }

            localFilePath = Application.persistentDataPath + '/' + "content";
            apiURL = cmsSettings.baseUrl + "/api";
            if (cmsSettings.lastsync == null)
                cmsSettings.lastsync = "1970-01-01T00:00:00.000Z";
            maxUpdatedAt = cmsSettings.lastsync;
        }

        public static IEnumerator Sync()
        {

            if(isSyncing)
            {
                Debug.LogWarning("Already syncing CMS, please wait for the current sync to finish before starting a new one.");
                yield break;
            }

            if (cmsSettings == null)
            {
                Debug.LogError("Failed to sync CMS, no cms settings set");
                yield break;
            }

            Debug.Log("Syncing cms: " + apiURL);
            Debug.Log("Previous sync moment: " + cmsSettings.lastsync);

            // check for network connecting
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("Failed to sync CMS, no network capabilities on device");
                yield break;
            }

            //check if all urls are valid
            yield return ValidateAll();

            if(cmsSettings.baseUrlStatus == urlStatus.Invalid)
            {
                Debug.LogError("Failed to sync CMS, cant connect to server.");
                yield break;
            }

            isSyncing = true;

            progress = 0.0f;

            if (!Directory.Exists(localFilePath))
            {
                Directory.CreateDirectory(localFilePath);
            }

            yield return FetchLanguageOptions();

            for(int i=0; i< cmsSettings.restEndpoints.Count;i++)
            {

                var endpoint = cmsSettings.restEndpoints[i];

                if (endpoint.status == urlStatus.Invalid) 
                {
                    Debug.LogWarning($"Skipping {endpoint.name}, endpoint could not be reached");
                    continue;
                }

                yield return Fetch(endpoint.name);
                yield return DeleteLocalFilesMissingFromRemote(endpoint.name);
            }

            Debug.Log("Sync complete");
            cmsSettings.lastsync = maxUpdatedAt;
            SaveSettings();
            
            isSyncing = false;
            yield break;

        }

        public static void SaveSettings()
        {
            string json = JsonConvert.SerializeObject(cmsSettings, Formatting.Indented);
            File.WriteAllText(cmsSettingsPath, json);
        }


        private static IEnumerator FetchLanguageOptions()
        {
            Debug.Log("Fetching Language options");
            using (UnityWebRequest request = UnityWebRequest.Get(cmsSettings.baseUrl + "/api/i18n/locales"))
            {
                request.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
                // Request and wait for the desired page.
                yield return request.SendWebRequest();

                string[] pages = apiURL.Split('/');
                int page = pages.Length - 1;

                switch (request.result)
                {
                    case UnityWebRequest.Result.ConnectionError:
                    case UnityWebRequest.Result.DataProcessingError:
                        Debug.LogError(pages[page] + ": Error: " + request.error);
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        Debug.LogError(pages[page] + ": HTTP Error: " + request.error);
                        break;
                    case UnityWebRequest.Result.Success:
                        string filename = "languagesettings.json";
                        string json = request.downloadHandler.text;
                        JArray fetchedLanguageOptions = JArray.Parse(json);

                        JArray languageOptions = new JArray();
                        foreach (var option in fetchedLanguageOptions) 
                        { 
                            JObject languageOption = new JObject();
                            languageOption["code"] = option["code"]?.Value<string>();
                            languageOption["name"] = option["name"]?.Value<string>();
                            languageOptions.Add(languageOption);
                            languageCodes.Add(option["code"]?.Value<string>());
                        }
                        File.WriteAllText(Application.streamingAssetsPath + "/" + filename, JsonConvert.SerializeObject(languageOptions), System.Text.Encoding.UTF8);
                        break;
                }
            }
        }


        private static IEnumerator Fetch(string endpoint)
        {
            Debug.Log("Fetching " + endpoint);
            string dir = localFilePath + '/' + endpoint;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);



            string requestURL = $"{apiURL}/{endpoint}?populate=*&pagination[pageSize]=100&filters[updatedAt][$gt]={cmsSettings.lastsync}&sort=updatedAt:asc";

            UnityWebRequest request = UnityWebRequest.Get(requestURL);
            request.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                JObject jObject = ParseNoDates(jsonResponse);
                JObject metaData = jObject["meta"].Value<JObject>();
                int pagecount = metaData["pagination"]["pageCount"].Value<int>();

                for (int i = 1; i <= pagecount; i++)
                {
                    yield return FetchPage(i, endpoint);
                }
            }
            else
            {
                Debug.LogError("Failed to fetch " + endpoint + " from CMS: " + request.error);
                yield break;
                }

                    
        }

 

        private static IEnumerator FetchPage(int page, string endpoint)
        {
            string requestURL = $"{apiURL}/{endpoint}?populate=*&pagination[page]={page}&pagination[pageSize]=100&filters[updatedAt][$gt]={cmsSettings.lastsync}&sort=updatedAt:asc";
            JArray jArray;
            UnityWebRequest request = UnityWebRequest.Get(requestURL);
            request.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;

                JObject jObject = ParseNoDates(jsonResponse);
                jArray = jObject["data"].Value<JArray>();

                int progress = 0;
                List<string> savedItems = new List<string>();
                foreach (JObject item in jArray)
                {
                    string filename = $"{item["documentId"].Value<string>()}.json";
                    string filepath = $"{localFilePath}/{endpoint}/{filename}";
                   
                    yield return SaveItem(filepath, item);
                    DateTime updatedAt = DateTime.Parse(item["updatedAt"].Value<string>());
                    DateTime currentUpdatedAt = DateTime.Parse(maxUpdatedAt);
                    if (updatedAt > currentUpdatedAt)
                    {
                        maxUpdatedAt = item["updatedAt"].Value<string>();
                    }
 
                    progress++;
                    yield return null;
                }
            }
            else
            {
                Debug.LogError("Failed to fetch page: " + requestURL + " from CMS: " + request.error);
                yield break;
            }
        }

        private static  IEnumerator SaveItem(string path, JObject item)
        {
            var media = CMSFileHandler.Extract(item);
            foreach (MediaRef m in media)
            {
                yield return DownloadFile(cmsSettings.baseUrl + m.Url, localFilePath + m.Url);
            }

            File.WriteAllText(path, item.ToString(), System.Text.Encoding.UTF8);
        }


        private static IEnumerator DeleteLocalFilesMissingFromRemote(string endpoint)
        {
            List<string> filesToCheck = new List<string>();

                string requestURL = $"{apiURL}/{endpoint}?pagination[pageSize]=100&fields[0]=documentId";

                UnityWebRequest request = UnityWebRequest.Get(requestURL);
            request.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
            yield return request.SendWebRequest();

                //fetch all ids from remote CMS
                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = request.downloadHandler.text;
                    JObject jObject = ParseNoDates(jsonResponse);
                    JObject metaData = jObject["meta"].Value<JObject>();
                    int pagecount = metaData["pagination"]["pageCount"].Value<int>();

                    for (int i = 1; i <= pagecount; i++)
                    {
                        string pagerequestURL = $"{apiURL}/{endpoint}?pagination[page]={i}&pagination[pageSize]=100&fields[0]=documentId";
                        UnityWebRequest pagerequest = UnityWebRequest.Get(pagerequestURL);
                    pagerequest.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
                    yield return pagerequest.SendWebRequest();
                        if (pagerequest.result == UnityWebRequest.Result.Success)
                        {
                            string pagejsonResponse = pagerequest.downloadHandler.text;
                            JObject pagejObject = ParseNoDates(pagejsonResponse);
                            JArray jArrayPage = pagejObject["data"].Value<JArray>();
                            foreach (JObject item in jArrayPage)
                            {
                                string file = $"{item["documentId"].ToString()}";
                                filesToCheck.Add(file);
                            }
                        }
                        else
                        {
                            Debug.LogError("Failed to fetch page: " + pagerequestURL + " from CMS: " + pagerequest.error);
                            yield break;
                        }
                    }
                }
                else
                {
                    Debug.LogError("Failed to fetch " + endpoint + " from CMS: " + request.error);
                    yield break;
                }
            

            //check for missing local files
            foreach (string file in filesToCheck)
            {

                string filename = $"{file}.json";
                string filepath = $"{localFilePath}/{endpoint}/{filename}";

                if (!File.Exists(filepath))
                {
                    Debug.Log("Fetching missing item: " + file);
                    UnityWebRequest requestMissing = UnityWebRequest.Get($"{apiURL}/{endpoint}/{file}?populate=*");
                    requestMissing.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
                    yield return requestMissing.SendWebRequest();
                    if (requestMissing.result == UnityWebRequest.Result.Success)
                    {
                        string jsonResponse = requestMissing.downloadHandler.text;
                        JObject jObject = ParseNoDates(jsonResponse);
                        var item = jObject["data"].Value<JObject>();

                        yield return SaveItem(filepath, item);
                    }

                }
            }

            //delete local files not in remote CMS
            foreach (string f in Directory.GetFiles($"{localFilePath}/{endpoint}"))
            {
                string localFileName = Path.GetFileNameWithoutExtension(f);
                if (!filesToCheck.Contains(localFileName))
                {
                    string json = File.ReadAllText(f);
                    JObject jObject = ParseNoDates(json);

                    // check if the item has any files that need to be deleted
                    var media = CMSFileHandler.Extract(jObject);
                    foreach (MediaRef mediaRef in media)
                    {
                        File.Delete(localFilePath + mediaRef.Url);
                    }
                    // delete the json file
                    File.Delete(f);
                    Debug.Log("Deleted local file: " + f + " as it is missing from remote CMS");
                }
            }
        }


        private static IEnumerator DownloadFile(string url, string path)
        {
            Debug.Log("Downloading: " + url);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
            {

                yield return webRequest.SendWebRequest();
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    byte[] bytes = webRequest.downloadHandler.data;
                    File.WriteAllBytes(path, bytes);
                }
                else
                {
                    Debug.LogError($"File:{url} download failed. Error: " + webRequest.error);
                }
            }
        }


        public static IEnumerator ValidateAll()
        {
            isValidating = true;

            cmsSettings.apiTokenStatus = urlStatus.Unknown;
            cmsSettings.baseUrlStatus = urlStatus.Unknown;
            cmsSettings.languageUrlStatus = urlStatus.Unknown;
            foreach(RestEndpoint endpoint in cmsSettings.restEndpoints)
                endpoint.status = urlStatus.Unknown;

            yield return ValidateBaseUrl();

            if (cmsSettings.baseUrlStatus == urlStatus.Invalid)
            {
                isValidating = false;
                yield break;
            }

            yield return ValidateLanguageURL();

            if (cmsSettings.languageUrlStatus == urlStatus.Invalid) 
            {
                isValidating = false;
                yield break;
            }

            yield return ValidateEndpoints();

            isValidating = false;
        }


        private static IEnumerator ValidateBaseUrl()
        {
            bool connectedToServer = false;
            float maxTime = 10f;
            Debug.Log("Connecting to server...");

            var startTime = Time.realtimeSinceStartup;
            UnityWebRequest.Result requestResult = UnityWebRequest.Result.InProgress;
            cmsSettings.baseUrlStatus = urlStatus.Validating;
            while (!connectedToServer && Time.realtimeSinceStartup - startTime < maxTime)
            {
                UnityWebRequest request = UnityWebRequest.Get(cmsSettings.baseUrl);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    cmsSettings.baseUrlStatus = urlStatus.Valid;
                    connectedToServer = true;
                    Debug.Log("Succes");
                    yield break;
                }
                else
                {
                    requestResult = request.result;
#if UNITY_EDITOR
                    yield return new EditorWaitForSeconds(1f);
#endif
#if UNITY_STANDALONE
                    yield return new WaitForSeconds(1f);
#endif
                }
            }
            Debug.LogError("Failed to connect to server: " + requestResult);
            cmsSettings.baseUrlStatus = urlStatus.Invalid;

        }

        private static IEnumerator ValidateEndpoints()
        {
            foreach (var endpoint in cmsSettings.restEndpoints)
            {
                yield return ValidateEndpoint(endpoint);
            }
        }


        private static IEnumerator ValidateLanguageURL()
        {
            bool connectedToServer = false;
            float maxTime = 10f;
            Debug.Log("Fetching languages");

            var startTime = Time.realtimeSinceStartup;
            UnityWebRequest.Result requestResult = UnityWebRequest.Result.InProgress;
            cmsSettings.languageUrlStatus = urlStatus.Validating;
            while (!connectedToServer && Time.realtimeSinceStartup - startTime < maxTime)
            {
                UnityWebRequest request = UnityWebRequest.Get(cmsSettings.baseUrl + "/api/i18n/locales");
                request.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    cmsSettings.languageUrlStatus = urlStatus.Valid;
                    connectedToServer = true;
                    Debug.Log("Succes");
                    yield break;
                }
                else
                {
                    requestResult = request.result;
#if UNITY_EDITOR
                    yield return new EditorWaitForSeconds(1f);
#endif
#if UNITY_STANDALONE
                    yield return new WaitForSeconds(1f);
#endif
                }
            }
            Debug.LogError("Failed to connect to server: " + requestResult);
            cmsSettings.languageUrlStatus = urlStatus.Invalid;
        }

        private static  IEnumerator ValidateEndpoint(RestEndpoint endpoint)
        {
            bool connectedToServer = false;
            endpoint.status = urlStatus.Validating;
            float maxTime = 5f;
            Debug.Log($"Validating endpoint {endpoint.name}...");
            var startTime = Time.realtimeSinceStartup;
            string error = "";
            while (!connectedToServer && Time.realtimeSinceStartup - startTime < maxTime)
            {
                UnityWebRequest request = UnityWebRequest.Get(cmsSettings.baseUrl + "/api/" + endpoint.name);
                request.SetRequestHeader("Authorization", "Bearer " + cmsSettings.apiToken);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    endpoint.status = urlStatus.Valid;
                    connectedToServer = true;
                    Debug.Log($"Connected to endpoint {endpoint.name}");
                    yield break;
                }
                else
                {
                    error = request.error;
#if UNITY_EDITOR
                    yield return new EditorWaitForSeconds(1f);
#endif
#if UNITY_STANDALONE
                    yield return new WaitForSeconds(1f);
#endif
                }
            }
            Debug.LogError($"Failed to connect to endpoint {endpoint.name}: {error}");
            endpoint.status = urlStatus.Invalid;
        }

        public static JObject ParseNoDates(string json)
        {
            using var sr = new StringReader(json);
            using var jr = new JsonTextReader(sr)
            {
                DateParseHandling = DateParseHandling.None
            };
            return JObject.Load(jr);
        }


    }
}


