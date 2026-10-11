#if UNITY_5_3_OR_NEWER

using System;
using System.IO;
using UnityEngine;

namespace MikaNetwork
{
    // 클라가 붙을 서버 주소. 에디터(개발)는 무조건 이 PC의 개발 서버, 빌드만 원격 서버(StreamingAssets/ServerSettings.json)에 붙는다.
    // 원격 주소는 JSON이라 빌드를 다시 하지 않고 파일만 고쳐 바꾼다.
    public static class ServerEndpoint
    {
        public const string DevHost          = "127.0.0.1";
        public const int    DevPort          = 10050;
        public const int    RemotePort       = 30329;
        public const string SettingsFileName = "ServerSettings.json";

        [Serializable]
        private class SettingsFile
        {
            public string Host = "";
            public int    Port = RemotePort;
        }

        public static (string Host, int Port) Resolve()
        {
#if UNITY_EDITOR
            return (DevHost, DevPort);
#else
            return LoadRemote();
#endif
        }

        /// <summary>ServerSettings.json의 원격 주소. 파일이 없거나 Host가 비면 에러를 남기고 개발 서버로 붙는다.</summary>
        public static (string Host, int Port) LoadRemote()
        {
            var path = Path.Combine(Application.streamingAssetsPath, SettingsFileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"서버 주소 파일이 없어 개발 서버로 붙는다: {path}");
                return (DevHost, DevPort);
            }

            var settings = JsonUtility.FromJson<SettingsFile>(File.ReadAllText(path));
            if (settings == null || string.IsNullOrWhiteSpace(settings.Host))
            {
                Debug.LogError($"서버 주소 파일에 Host가 없어 개발 서버로 붙는다: {path}");
                return (DevHost, DevPort);
            }

            return (settings.Host.Trim(), settings.Port);
        }
    }
}

#endif
