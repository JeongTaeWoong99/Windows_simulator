using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI 틀 에디터 미리보기 — 플레이 전 씬 뷰·게임 뷰에서도 'UiSkin'의 픽셀 틀이 보이게 한다 (2026-11-08).
//
// ■ 왜 저장하지 않고 되돌리나
//   씬이 그림 저장소의 그림을 직접 가리키면 그림 저장소를 안 받은 쪽에서 참조가 끊긴다('Art 규칙.md').
//   그래서 씬 파일은 기본 스프라이트 그대로 두고, 열려 있는 동안만 바꿔 보여 준다.
//
// ■ 언제 입히고 언제 걷나
//   입힘: 에디터가 뜰 때 · 씬을 열 때 · 저장 직후 · 플레이를 끝내고 돌아올 때 · 컴파일 뒤.
//   걷음: 저장 직전 · 플레이 들어가기 직전(실행 중에는 'UiSkin'이 스스로 입힌다) · 컴파일 직전 · 씬을 닫을 때.
//   만든 장식(Ui Trim · Ui Frame)은 'HideAndDontSave'라 계층 창에 안 보이고 저장되지 않는다.
//
// ■ 끄기 — 메뉴 'Window/DesktopWindowControl/UI 테마/스킨 미리보기 (에디터)' (개인 설정 · EditorPrefs)
[InitializeOnLoad]
internal static class UiSkinPreview
{
    private const string MenuPath = "Window/DesktopWindowControl/UI 테마/스킨 미리보기 (에디터)";
    private const string PrefKey  = "DesktopWindowControl.UiSkinPreview";

    private static readonly Changes Recorded = new();

    // 씬의 '바뀜' 표시 걷기 — 공개 API가 없어 내부 함수를 쓴다. 없으면(버전 차이) 표시가 남을 뿐이다
    private static readonly System.Reflection.MethodInfo? ClearDirty =
        typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

    private static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

    static UiSkinPreview()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Revert;
        EditorSceneManager.sceneSaving            += (_, _) => Revert();
        EditorSceneManager.sceneSaved             += _ => Schedule();
        EditorSceneManager.sceneOpened            += (_, _) => Schedule();
        EditorSceneManager.sceneClosing           += (_, _) => Revert();
        EditorApplication.playModeStateChanged    += OnPlayModeChanged;

        Schedule();
    }

    [MenuItem(MenuPath, priority = 20)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(PrefKey, !Enabled);
        Revert();
        Schedule();
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            Revert();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            Schedule();
        }
    }

    // 이벤트 안에서 바로 바꾸지 않고 한 박자 뒤에 — 저장·열기가 끝난 뒤라야 안전하다
    private static void Schedule()
    {
        EditorApplication.delayCall -= Apply;
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        if (!Enabled || Recorded.IsActive || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        // 입히기 전에 이미 바뀐 씬이었는지 — 아니었다면 입힌 뒤 '바뀜' 표시를 걷는다(미리보기는 바꾼 게 아니다)
        var cleanScenes = new List<Scene>();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.isLoaded && !scene.isDirty)
            {
                cleanScenes.Add(scene);
            }
        }

        UiSkin.Recorder = Recorded;

        try
        {
            UiSkin.ApplyAll();
        }
        finally
        {
            UiSkin.Recorder = null;
        }

        Recorded.IsActive = true;

        foreach (Scene scene in cleanScenes)
        {
            ClearDirty?.Invoke(null, new object[] { scene });
        }

        SceneView.RepaintAll();
    }

    private static void Revert()
    {
        Recorded.Revert();
    }

    // 바꾼 것 — 되돌릴 때는 거꾸로
    private sealed class Changes : UiSkin.IRecorder
    {
        private readonly List<(Image image, Sprite sprite, float multiplier)> _sprites  = new();
        private readonly List<GameObject>                                    _created  = new();
        private readonly List<(LayoutGroup layout, RectOffset padding)>      _paddings = new();

        public bool IsActive { get; set; }

        public void SpriteSwapped(Image image, Sprite oldSprite, float oldMultiplier)
            => _sprites.Add((image, oldSprite, oldMultiplier));

        public void Created(GameObject go)
        {
            go.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(go);
        }

        public void PaddingChanged(LayoutGroup layout, RectOffset oldPadding)
            => _paddings.Add((layout, oldPadding));

        public void Revert()
        {
            if (!IsActive)
            {
                return;
            }

            foreach (GameObject go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            foreach ((Image image, Sprite sprite, float multiplier) in _sprites)
            {
                if (image != null)
                {
                    image.sprite                  = sprite;
                    image.pixelsPerUnitMultiplier = multiplier;
                }
            }

            foreach ((LayoutGroup layout, RectOffset padding) in _paddings)
            {
                if (layout != null)
                {
                    layout.padding = padding;
                }
            }

            _created.Clear();
            _sprites.Clear();
            _paddings.Clear();
            IsActive = false;
        }
    }
}
