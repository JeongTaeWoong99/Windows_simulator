using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 'UIThemeColor' 태그에 팔레트 색을 구워 넣는 에디터 도구.
//
//   적용      : 열린 씬 + 'Assets/Prefabs'의 모든 태그에 팔레트 색을 칠한다
//   자동 태그 : 태그가 없는 Image·TMP에 지금 색을 보고 역할을 짐작해 붙인다 (처음 한 번 — 결과는 콘솔에 표로)
//
// 팔레트 에셋을 인스펙터에서 고치면 열린 씬은 자동으로 다시 칠해진다. 프리팹은 '적용'을 눌러야 한다
// (프리팹을 매 입력마다 열고 저장하면 느리다).
//
// ⚠️ 씬 안의 프리팹 인스턴스는 건너뛴다 — 여기서 칠하면 인스턴스 오버라이드가 쌓인다. 프리팹 원본에서 칠한다.
[InitializeOnLoad]
public static class UIThemeBaker
{
    private const string MenuRoot     = "Window/DesktopWindowControl/UI 테마/";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string AssetPath    = "Assets/Resources/" + UIThemePalette.ResourcePath + ".asset";

    // 패널 역할 — 자동 태그가 중첩 깊이를 셀 때 쓴다
    private static readonly HashSet<UIThemeRole> PanelRoles = new HashSet<UIThemeRole>
    {
        UIThemeRole.PanelBg, UIThemeRole.PanelSub, UIThemeRole.Slot,
    };

    static UIThemeBaker()
    {
        UIThemePalette.Changed += palette => EditorApplication.delayCall += () => ApplyToOpenScenes(palette);
    }

    // ── 메뉴 ──

    [MenuItem(MenuRoot + "팔레트 에셋 선택")]
    private static void SelectPalette()
    {
        Selection.activeObject = EnsurePalette();
    }

    [MenuItem(MenuRoot + "적용 (열린 씬 + 프리팹)")]
    private static void ApplyAll()
    {
        var palette = EnsurePalette();
        int count   = ApplyToOpenScenes(palette);

        foreach (var path in PrefabPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            int n    = 0;

            foreach (var tag in root.GetComponentsInChildren<UIThemeColor>(true))
            {
                n += tag.Apply(palette).Length > 0 ? 1 : 0;
            }

            if (n > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
            count += n;
        }

        Debug.Log($"[UI 테마] 적용 — 바뀐 칸 {count}개");
    }

    [MenuItem(MenuRoot + "자동 태그 (처음 한 번)")]
    private static void AutoTagAll()
    {
        EnsurePalette();

        var log = new StringBuilder();
        int count = 0;

        // 프리팹 먼저 — 씬의 인스턴스가 원본을 따라오게
        foreach (var path in PrefabPaths())
        {
            var root = PrefabUtility.LoadPrefabContents(path);

            // 프리팹은 패널 안에 들어가는 행·칸이라 깊이 1(구획)부터 센다
            int n = AutoTag(root.transform, 1, path, log);

            if (n > 0)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
            count += n;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            int n     = 0;

            foreach (var go in scene.GetRootGameObjects())
            {
                n += AutoTag(go.transform, 0, scene.name, log);
            }

            if (n > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            count += n;
        }

        Debug.Log($"[UI 테마] 자동 태그 — {count}개\n{log}");

        ApplyAll();
    }

    // ── 적용 ──

    private static int ApplyToOpenScenes(UIThemePalette palette)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return 0;
        }

        int count = 0;

        foreach (var tag in Object.FindObjectsByType<UIThemeColor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (PrefabUtility.IsPartOfPrefabInstance(tag))
            {
                continue;
            }

            var changed = tag.Apply(palette);

            if (changed.Length == 0)
            {
                continue;
            }

            foreach (var target in changed)
            {
                EditorUtility.SetDirty(target);
            }

            EditorSceneManager.MarkSceneDirty(tag.gameObject.scene);
            count++;
        }

        return count;
    }

    // ── 자동 태그 ──

    // 깊이 우선으로 내려가며 태그를 붙인다 — 부모가 먼저 태그를 받아야 자식이 패널 깊이를 셀 수 있다.
    private static int AutoTag(Transform node, int panelDepth, string where, StringBuilder log)
    {
        int count = 0;

        // 씬 안 프리팹 인스턴스는 원본에서 처리했다
        if (!PrefabUtility.IsPartOfPrefabInstance(node.gameObject))
        {
            var graphic = node.GetComponent<Graphic>();

            if (graphic != null && node.GetComponent<UIThemeColor>() == null && TryGuessRole(graphic, panelDepth, out var role))
            {
                var tag = Undo.AddComponent<UIThemeColor>(node.gameObject);
                tag.Setup(role, AlphaFor(role, graphic.color.a));

                log.AppendLine($"{where} | {PathOf(node)} | {Hex(graphic.color)} → {role}");
                count++;
            }
        }

        var own = node.GetComponent<UIThemeColor>();
        int childDepth = own != null && PanelRoles.Contains(own.Role) ? panelDepth + 1 : panelDepth;

        foreach (Transform child in node)
        {
            count += AutoTag(child, childDepth, where, log);
        }

        return count;
    }

    // 지금 색 → 역할. 확신이 없는 것(투명·아트 스프라이트·그 밖의 Graphic)은 붙이지 않는다.
    private static bool TryGuessRole(Graphic graphic, int panelDepth, out UIThemeRole role)
    {
        role = UIThemeRole.PanelSub;

        var c = graphic.color;

        // 레이캐스트 받이·마스크 — 보이지 않는 칸
        if (c.a < 0.01f)
        {
            return false;
        }

        Color.RGBToHSV(c, out float h, out float s, out float v);

        if (graphic is TMP_Text)
        {
            role = GuessTextRole(h, s);
            return true;
        }

        if (graphic is not Image image)
        {
            return false;
        }

        // 아트 스프라이트(내장 UISprite 등이 아닌 것)는 색을 건드리지 않는다
        if (image.sprite != null && AssetDatabase.GetAssetPath(image.sprite) != "Resources/unity_builtin_extra")
        {
            return false;
        }

        var selectable = image.GetComponent<Selectable>();

        // 입력칸은 버튼이 아니라 칸이다
        if (selectable is TMP_InputField or InputField)
        {
            role = UIThemeRole.Slot;
            return true;
        }

        if (selectable != null && selectable.targetGraphic == image)
        {
            role = s > 0.3f && h > 0.06f && h < 0.18f ? UIThemeRole.ButtonPrimary : UIThemeRole.Button;
            return true;
        }

        // 화면 전체를 덮는 바탕은 패널이 아니라 딤이다 — 로그인·로딩 바탕이 뒤 화면을 가리지 않게
        if (IsFullScreen(image.rectTransform))
        {
            role = UIThemeRole.Overlay;
            return true;
        }

        if (v < 0.35f)
        {
            role = c.a < 0.9f ? UIThemeRole.Overlay : UIThemeRole.Slot;
            return true;
        }

        if (s < 0.25f)
        {
            role = v > 0.75f ? PanelByDepth(panelDepth) : UIThemeRole.Border;
            return true;
        }

        // 큰 바탕은 원래 색이 무엇이든 패널이다 — 임시로 초록·빨강을 칠해 둔 영역이 많다.
        // 의미색은 진행 바처럼 작은 칸에만 준다.
        if (IsLarge(image.rectTransform))
        {
            role = PanelByDepth(panelDepth);
            return true;
        }

        role = HueRole(h);
        return true;
    }

    // 글씨는 흰색이 기본이다 — 노랑(골드·재화)과 파랑(다이아)만 수치 강조로 남긴다.
    // 빨강·초록 글씨를 경고·긍정색으로 옮기면 색 바탕 위에서 묻힌다(2026-09-26 실측).
    private static UIThemeRole GuessTextRole(float h, float s)
    {
        if (s < 0.25f)
        {
            return UIThemeRole.TextMain;
        }

        return HueRole(h) switch
        {
            UIThemeRole.Highlight => UIThemeRole.Highlight,
            UIThemeRole.Accent    => UIThemeRole.Accent,
            _                     => UIThemeRole.TextMain,
        };
    }

    // 채도가 있는 색은 색상으로 의미를 짐작한다 — 빨강 경고 · 노랑 강조 · 초록 긍정 · 파랑 강조
    private static UIThemeRole HueRole(float h)
    {
        if (h < 0.06f || h > 0.94f) return UIThemeRole.Negative;
        if (h < 0.18f)              return UIThemeRole.Highlight;
        if (h < 0.45f)              return UIThemeRole.Positive;
        return UIThemeRole.Accent;
    }

    private static bool IsFullScreen(RectTransform rt)
    {
        var size = rt.rect.size;
        return size.x >= 1900f && size.y >= 1000f;
    }

    // 가로로 길거나(상단 바) 넓은 칸
    private static bool IsLarge(RectTransform rt)
    {
        var size = rt.rect.size;
        return size.x * size.y >= 20000f || (size.x >= 300f && size.y >= 30f);
    }

    private static UIThemeRole PanelByDepth(int depth) => depth switch
    {
        0 => UIThemeRole.PanelBg,
        1 => UIThemeRole.PanelSub,
        _ => UIThemeRole.Slot,
    };

    // 딤·흐린 글씨처럼 원래 투명도에 뜻이 있던 것만 이어받는다
    private static float AlphaFor(UIThemeRole role, float originalAlpha) => role switch
    {
        UIThemeRole.PanelBg => 1f,
        UIThemeRole.Overlay => Mathf.Min(originalAlpha, 0.6f), // 불투명하던 전체 바탕도 딤으로 낮춘다
        _                   => originalAlpha,
    };

    // ── 도우미 ──

    private static UIThemePalette EnsurePalette()
    {
        var palette = AssetDatabase.LoadAssetAtPath<UIThemePalette>(AssetPath);

        if (palette != null)
        {
            return palette;
        }

        palette = ScriptableObject.CreateInstance<UIThemePalette>();
        AssetDatabase.CreateAsset(palette, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[UI 테마] 팔레트 에셋 생성 — {AssetPath}");
        return palette;
    }

    private static IEnumerable<string> PrefabPaths()
        => AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                        .Select(AssetDatabase.GUIDToAssetPath);

    private static string PathOf(Transform t)
        => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);
}
