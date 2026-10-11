#if UNITY_5_3_OR_NEWER

using System;
using System.IO;
using UnityEngine;

namespace MikaNetwork
{
    // 클라가 붙을 서버 주소. 빌드는 StreamingAssets/ServerSettings.json(진우 서버)을 읽는다 — 빌드를 다시 하지 않고 파일만 고쳐 바꾼다.
    // 에디터는 메뉴(Window/DesktopWindowControl/서버 접속)에서 고른 곳 — EditorPrefs라 개인 선택이 커밋되지 않는다. 기본은 로컬.
    public static class ServerEndpoint
    {
        public const string LocalHost        = "127.0.0.1";
        public const int    DefaultPort      = 30329;
        public const string SettingsFileName = "ServerSettings.json";

        public const string TargetPrefKey = "MikaNetwork.ServerTarget";
        public const string CustomPrefKey = "MikaNetwork.ServerCustom";

        public enum Target
        {
            Local  = 0,
            Remote = 1,   // ServerSettings.json — 빌드와 같은 곳
            Custom = 2,
        }

        [Serializable]
        private class SettingsFile
        {
            public string Host = "";
            public int    Port = DefaultPort;
        }

        public static (string Host, int Port) Resolve()
        {
#if UNITY_EDITOR
            switch ((Target)UnityEditor.EditorPrefs.GetInt(TargetPrefKey, (int)Target.Local))
            {
                case Target.Remote:
                    return LoadRemote();
                case Target.Custom:
                    return Parse(UnityEditor.EditorPrefs.GetString(CustomPrefKey, ""));
                default:
                    return (LocalHost, DefaultPort);
            }
#else
            return LoadRemote();
#endif
        }

        /// <summary>ServerSettings.json의 주소. 파일이 없거나 Host가 비면 에러를 남기고 로컬로 붙는다.</summary>
        public static (string Host, int Port) LoadRemote()
        {
            var path = Path.Combine(Application.streamingAssetsPath, SettingsFileName);
            if (!File.Exists(path))
            {
                Debug.LogError($"서버 주소 파일이 없어 로컬로 붙는다: {path}");
                return (LocalHost, DefaultPort);
            }

            var settings = JsonUtility.FromJson<SettingsFile>(File.ReadAllText(path));
            if (settings == null || string.IsNullOrWhiteSpace(settings.Host))
            {
                Debug.LogError($"서버 주소 파일에 Host가 없어 로컬로 붙는다: {path}");
                return (LocalHost, DefaultPort);
            }

            return (settings.Host.Trim(), settings.Port);
        }

        /// <summary>"host" 또는 "host:port". 비어 있으면 로컬, 포트가 없거나 숫자가 아니면 기본 포트.</summary>
        public static (string Host, int Port) Parse(string text)
        {
            var trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0)
            {
                return (LocalHost, DefaultPort);
            }

            var colon = trimmed.LastIndexOf(':');
            if (colon < 0 || !int.TryParse(trimmed.Substring(colon + 1), out var port))
            {
                return (colon < 0 ? trimmed : trimmed.Substring(0, colon), DefaultPort);
            }

            return (trimmed.Substring(0, colon), port);
        }
    }
}

#endif
