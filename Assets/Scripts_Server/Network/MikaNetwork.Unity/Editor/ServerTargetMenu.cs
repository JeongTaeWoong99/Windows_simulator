using UnityEditor;
using UnityEngine;

namespace MikaNetwork
{
    // 에디터에서 붙을 서버를 고른다 — 플레이 모드에 들어갈 때 NetworkManager가 ServerEndpoint로 읽는다.
    // EditorPrefs에 저장해 개인 선택이 커밋되지 않는다. 기본은 로컬.
    public static class ServerTargetMenu
    {
        private const string Root       = "Window/DesktopWindowControl/서버 접속/";
        private const string LocalPath  = Root + "로컬 (127.0.0.1)";
        private const string RemotePath = Root + "진우 서버 (ServerSettings.json)";
        private const string CustomPath = Root + "직접 입력...";

        private static ServerEndpoint.Target Current
            => (ServerEndpoint.Target)EditorPrefs.GetInt(ServerEndpoint.TargetPrefKey, (int)ServerEndpoint.Target.Local);

        [MenuItem(LocalPath, priority = 0)]
        private static void SelectLocal() => Select(ServerEndpoint.Target.Local);

        [MenuItem(LocalPath, true)]
        private static bool ValidateLocal() => Check(LocalPath, ServerEndpoint.Target.Local);

        [MenuItem(RemotePath, priority = 1)]
        private static void SelectRemote() => Select(ServerEndpoint.Target.Remote);

        [MenuItem(RemotePath, true)]
        private static bool ValidateRemote() => Check(RemotePath, ServerEndpoint.Target.Remote);

        [MenuItem(CustomPath, priority = 2)]
        private static void OpenCustom() => ServerAddressWindow.Open();

        [MenuItem(CustomPath, true)]
        private static bool ValidateCustom() => Check(CustomPath, ServerEndpoint.Target.Custom);

        public static void Select(ServerEndpoint.Target target)
        {
            EditorPrefs.SetInt(ServerEndpoint.TargetPrefKey, (int)target);

            var (host, port) = ServerEndpoint.Resolve();
            Debug.Log($"[서버 접속] {target} → {host}:{port} — 다음 플레이부터 적용");
        }

        // 메뉴가 열릴 때마다 불린다 — 지금 고른 항목에 체크를 단다.
        private static bool Check(string path, ServerEndpoint.Target target)
        {
            Menu.SetChecked(path, Current == target);
            return true;
        }
    }

    /// <summary>"host:port"를 직접 넣는 작은 창.</summary>
    public class ServerAddressWindow : EditorWindow
    {
        private string _address = "";

        public static void Open()
        {
            var window = GetWindow<ServerAddressWindow>(true, "서버 주소 직접 입력");
            window.minSize  = new Vector2(320, 70);
            window._address = EditorPrefs.GetString(ServerEndpoint.CustomPrefKey, "");
        }

        private void OnGUI()
        {
            _address = EditorGUILayout.TextField("주소 (host:port)", _address);

            if (GUILayout.Button("적용"))
            {
                EditorPrefs.SetString(ServerEndpoint.CustomPrefKey, _address.Trim());
                ServerTargetMenu.Select(ServerEndpoint.Target.Custom);
                Close();
            }
        }
    }
}
