using System;
using System.Collections;
using System.Collections.Generic;

namespace Shosho.CMS
{
    public class CMSSettings
    {
        public string localFileDir = "content";
        public string mediaFolder = "media";
        public string baseUrl = "https://example.com/";
        public string apiToken = "your_api_token_here";
        public List<RestEndpoint> restEndpoints = new List<RestEndpoint>();

        public string lastsync = "1970-01-01T00:00:00.000Z";

        public urlStatus baseUrlStatus = urlStatus.Unknown;
        public urlStatus languageUrlStatus = urlStatus.Unknown;
        public urlStatus apiTokenStatus = urlStatus.Unknown;

    }

    [System.Serializable]
    public class RestEndpoint
    {
        public string name;
        public urlStatus status = urlStatus.Unknown;
    }

    public enum urlStatus
    {
        Valid,
        Invalid,
        Validating,
        Unknown
    }

}
