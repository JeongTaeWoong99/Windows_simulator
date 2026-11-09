using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// UI 틀 굽기 — 'UiSkin'의 픽셀 틀을 씬·프리팹에 저장한다 (2026-11-10 · 에디터 미리보기 'UiSkinPreview'를 대신한다).
//
// ■ 왜 굽나 — 씬 뷰에서 실행 전에도 틀이 보이고, 장식(Ui Trim · Ui Frame)을 계층 창에서 고를 수 있다.
//   그림 저장소를 직접 가리키게 된다 — 그림 저장소가 없는 쪽(CI · 안 받은 팀원)에서는 흰 상자로 보인다(사용자 허용 · 2026-11-10).
// ■ 순서 — 프리팹 원본('Assets/Prefabs') 먼저, 그다음 열린 씬. 씬 안 프리팹 인스턴스는 건너뛴다(원본에서 받는다).
// ■ 다시 눌러도 된다 — 이미 바뀐 그림('ui_*')과 이미 있는 장식은 건너뛴다. 새로 만든 UI만 입혀진다.
internal static class UiSkinBaker
{
    private const string MenuPath   = "Window/DesktopWindowControl/UI 테마/스킨 굽기 (씬·프리팹)";
    private const string PrefabRoot = "Assets/Prefabs";

    [MenuItem(MenuPath, priority = 20)]
    private static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[UI 틀] 플레이 중에는 굽지 않는다.");
            return;
        }

        if (FxSprites.UiPanel == null && FxSprites.UiButton == null)
        {
            Debug.LogWarning("[UI 틀] 틀 그림이 없다 — 그림 저장소('Assets/Art')를 받은 뒤 굽는다.");
            return;
        }

        UiSkin.Skip = go => PrefabUtility.IsPartOfPrefabInstance(go);

        try
        {
            int prefabs = BakePrefabs();

            UiSkin.ApplyAll();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneAt(i));
            }

            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[UI 틀] 구움 — 프리팹 {prefabs}개 · 열린 씬 {SceneManager.sceneCount}개");
        }
        finally
        {
            UiSkin.Skip = null;
        }
    }

    private static int BakePrefabs()
    {
        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            string     path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                UiSkin.Apply(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return count;
    }
}
