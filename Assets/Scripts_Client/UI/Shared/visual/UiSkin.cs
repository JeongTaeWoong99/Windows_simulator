using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI 틀 입히기 — 씬의 Unity 기본 스프라이트(둥근 네모)를 우리 픽셀 틀로 바꾼다 (2026-11-08).
//
// ■ 무엇을 무엇으로 — 'UI 규칙.md' "스프라이트"의 역할 그대로
//   'Background'(영역 상자) → 역할('UIThemeColor')로 갈린다 — 제목 띠(Header) → 'UiHeader', 그 밖 → 'UiInput'(파인 칸).
//   단 '창'(아래 액자)인 창 바탕(PanelBg · PanelSub + 세로 레이아웃 · 크기 맞춤 없음 — 확인·안내 팝업)은 'UiPanel' 바탕 + 액자.
//
// ■ 창 액자 — 무늬 띠('ui_panel_trim', Tiled)를 창 맨 위(마지막 자식)에 덮는다
//   창 = 열 · 오버레이 창의 'Body Panel'(부모가 '(MAIN VIEW)' 캔버스) + 위의 팝업 바탕.
//   띠가 내용을 가리지 않게 레이아웃 여백을 띠 두께('FramePadding')만큼 늘린다. 작은 구획에는 두르지 않는다 — 칸이 테두리로 꽉 찬다.
//   'UISprite'(누르는 것) → 'UiButton' · 'InputFieldBackground'(입력칸) → 'UiInput'.
//   'null'(캔버스 본체 · 격자 칸) · 'UIMask'(뷰포트)는 건드리지 않는다.
//
// ■ 두 겹 — 바탕 + 장식
//   바탕은 흰 판이라 Image 색(팔레트 역할 · 버튼 ColorTint)이 그대로 곱해진다.
//   장식('_trim' — 외곽선 · 빛줄 · 그늘 · 놋쇠 귀퉁이)은 색이 든 덧그림이라 곱하면 놋쇠가 죽는다 —
//   그래서 따로 첫 자식 Image('Ui Trim')로 깐다. 글씨·아이콘(뒤 형제)이 그 위에 그려진다.
//   레이아웃에 끼지 않고(LayoutElement.ignoreLayout) 클릭을 막지 않는다(raycastTarget 끔).
//
// ■ 씬·프리팹에 구워 두고, 실행 중에도 한 번 더 입힌다(안 구운 새 UI의 안전망)
//   구운 씬은 그림 저장소를 직접 가리킨다 — 그림 저장소가 없으면 흰 상자로 보인다(허용 · 'Art 규칙.md').
//   실행 중 입히기는 그림이 없으면(그림 저장소 없음 · 'fx/ui/' 없음) 아무것도 안 바꾼다.
//
// ■ 언제 — 씬이 열릴 때 한 번, 꺼진 오브젝트까지. 실행 중에 프리팹으로 만드는 UI는 그 뷰의 Awake에서 'Apply(this)'.
//   ★ 씬·프리팹에 구워 둔다 (2026-11-10) — 메뉴 'UI 테마/스킨 굽기'('UiSkinBaker' · Editor). 구운 것은 그림 이름이 'ui_*'라 실행 중 다시 바꾸지 않는다.
//     실행 중 입히기는 안 구운 새 UI를 위한 안전망이다. 그림 저장소가 없으면 구운 자리는 흰 상자로 보인다(사용자 허용).
public static class UiSkin
{
    // 픽셀 하나를 캔버스 2칸으로 — 1이면 테두리가 너무 가늘어 픽셀 느낌이 안 난다
    private const float PixelsPerUnitMultiplier = 0.5f;

    private const string TrimName  = "Ui Trim";
    private const string FrameName = "Ui Frame";

    // 액자 여백(캔버스 픽셀) — 띠 두께('ui_panel_trim' 경계 12px ÷ PixelsPerUnitMultiplier = 24) + 띠와 내용 사이 틈 8.
    //   틈이 없으면 버튼이 띠에 붙어 겹쳐 보인다(2026-11-10 사용자).
    private const int FramePadding = 32;

    // 이보다 가는 것(선택 테두리 선 3px · 슬라이더 막대 8px)에는 틀을 입히지 않는다 — 틀 경계(버튼 10px)보다 얇아 뭉개진다
    private const float MinSkinSize = 14f;

    // 드롭다운 목록 틀 안쪽 여백(캔버스 픽셀) — 줄 바탕(불투명)이 목록 테두리를 덮지 않게
    private const float ListInset = 4f;

    // 건너뛸 오브젝트 — 굽기('UiSkinBaker')가 씬 안 프리팹 인스턴스를 거른다(프리팹 원본에서 굽는다). 실행 중에는 null
    public static System.Func<GameObject, bool>? Skip { get; set; }

    private readonly struct Skin
    {
        public readonly Sprite  Body;
        public readonly Sprite? Trim;
        public readonly bool    TrimTiled; // 장식 가장자리가 되풀이 무늬 — 늘리면 무늬가 뭉개진다

        public Skin(Sprite body, Sprite? trim, bool trimTiled = false)
        {
            Body      = body;
            Trim      = trim;
            TrimTiled = trimTiled;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        ApplyAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll();

    private static bool Skipped(GameObject go) => Skip != null && Skip(go);

    // 열린 씬의 Image 전부(꺼진 것 포함) — 씬이 열릴 때 · 에디터 미리보기('UiSkinPreview')
    public static void ApplyAll()
    {
        Dictionary<string, Skin>? map = BuildMap();

        if (map == null)
        {
            return;
        }

        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!Skipped(image.gameObject))
            {
                Swap(image, map);
            }
        }

        // 드롭다운 목록
        foreach (TMP_Dropdown dropdown in Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!Skipped(dropdown.gameObject))
            {
                InsetList(dropdown.template, false);
            }
        }

        foreach (CheckDropdownView dropdown in Object.FindObjectsByType<CheckDropdownView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!Skipped(dropdown.gameObject))
            {
                InsetList(dropdown.ListRect, true);
            }
        }

        // 열·오버레이 창 몸통 — Image가 없어도 액자를 두른다
        foreach (RectTransform rect in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rect.name == "Body Panel" && rect.parent != null && rect.parent.name.EndsWith("(MAIN VIEW)") && !Skipped(rect.gameObject))
            {
                AddFrame(rect);
            }
        }
    }

    // 실행 중에 만든 UI 묶음에 입힌다 (프리팹으로 만든 줄·카드)
    public static void Apply(Component root)
    {
        Dictionary<string, Skin>? map = BuildMap();

        if (map == null)
        {
            return;
        }

        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (!Skipped(image.gameObject))
            {
                Swap(image, map);
            }
        }

        foreach (TMP_Dropdown dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if (!Skipped(dropdown.gameObject))
            {
                InsetList(dropdown.template, false);
            }
        }
    }

    // 드롭다운 목록의 보이는 칸(Viewport)을 틀 안쪽으로 들인다 — 왼쪽, 스크롤바가 없는 오른쪽, 'vertical'이면 위·아래도. 한 번만 들인다.
    //   'TMP_Dropdown'은 위·아래를 들이지 않는다(false) — 목록 높이를 내용 높이에 맞춰 줄여서 들인 만큼 마지막 줄이 잘린다.
    //   위·아래 여백은 템플릿의 내용 칸이 이미 갖고 있다(줄 템플릿 위아래 4px).
    private static void InsetList(RectTransform? list, bool vertical)
    {
        if (list == null || list.Find("Viewport") is not RectTransform viewport || viewport.offsetMin.x >= ListInset)
        {
            return;
        }

        float right = viewport.offsetMax.x < 0f ? viewport.offsetMax.x : -ListInset; // 스크롤바 자리는 그대로

        // '숨기며 넓히기' 스크롤바는 보이는 칸 크기를 덮어써 여백이 사라진다 — 숨기기만 하고, 오른쪽도 틀만큼만 들인다(스크롤바는 필요할 때 위에 뜬다)
        if (list.TryGetComponent(out ScrollRect scroll) && scroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport)
        {
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            right                              = -ListInset;
        }

        float vy = vertical ? ListInset : 0f;

        viewport.offsetMin = new Vector2(viewport.offsetMin.x + ListInset, viewport.offsetMin.y + vy);
        viewport.offsetMax = new Vector2(right, viewport.offsetMax.y - vy);
    }

    // 'Background' 자리를 역할로 가를 때 쓰는 키
    private const string PanelKey  = "Background";
    private const string HeaderKey = "Background/Header";
    private const string InsetKey  = "Background/Inset";

    // 기본 스프라이트 이름(+역할) → 우리 틀. 바탕 그림이 하나도 없으면 null
    private static Dictionary<string, Skin>? BuildMap()
    {
        var map = new Dictionary<string, Skin>();

        Add(map, PanelKey, FxSprites.UiPanel, FxSprites.UiPanelTrim, true);
        Add(map, HeaderKey, FxSprites.UiHeader, FxSprites.UiHeaderTrim);
        Add(map, InsetKey, FxSprites.UiInput, FxSprites.UiInputTrim);
        Add(map, "UISprite", FxSprites.UiButton, FxSprites.UiButtonTrim);
        Add(map, "InputFieldBackground", FxSprites.UiInput, FxSprites.UiInputTrim);

        return map.Count > 0 ? map : null;
    }

    private static void Add(Dictionary<string, Skin> map, string key, Sprite? body, Sprite? trim, bool trimTiled = false)
    {
        if (body != null)
        {
            map[key] = new Skin(body, trim, trimTiled);
        }
    }

    // 'Background'는 역할로 가른다 — 역할 표가 없으면 창으로 본다
    private static string KeyOf(Image image)
    {
        string name = image.sprite.name;

        if (name != PanelKey || !image.TryGetComponent(out UIThemeColor theme))
        {
            return name;
        }

        return theme.Role switch
        {
            UIThemeRole.PanelBg or UIThemeRole.PanelSub => PanelKey,
            UIThemeRole.Header                          => HeaderKey,
            _                                           => InsetKey,
        };
    }

    private static void Swap(Image image, Dictionary<string, Skin> map)
    {
        // Sliced가 아닌 것(아이콘·체크 표시 등)은 모양을 늘려 쓰는 틀이 아니다 — 건드리지 않는다
        if (image.sprite == null || image.type != Image.Type.Sliced || !map.TryGetValue(KeyOf(image), out Skin skin))
        {
            // 역할 틀 그림이 빠졌으면(제목 띠 · 파인 칸) 창 틀로 대신한다
            if (image.sprite == null || image.type != Image.Type.Sliced || image.sprite.name != PanelKey || !map.TryGetValue(PanelKey, out skin))
            {
                return;
            }
        }

        if (IsThin(image))
        {
            return;
        }

        // 창 바탕 — 액자를 두를 창이 아니면 파인 칸으로
        if (skin.TrimTiled)
        {
            if (IsWindow(image))
            {
                SetSprite(image, skin.Body);
                AddFrame((RectTransform)image.transform);
                return;
            }

            if (!map.TryGetValue(InsetKey, out skin))
            {
                return;
            }
        }

        SetSprite(image, skin.Body);

        if (skin.Trim != null && IsVisible(image))
        {
            AddTrim(image, skin.Trim, false);
        }
    }

    private static void SetSprite(Image image, Sprite body)
    {
        image.sprite                  = body;
        image.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;
    }

    // 크기를 아는데(꺼져서 0인 것은 모른다) 틀보다 가는 것
    private static bool IsThin(Image image)
    {
        Rect  rect = image.rectTransform.rect;
        float min  = Mathf.Min(rect.width, rect.height);

        return min > 0.5f && min < MinSkinSize;
    }

    // 액자를 두를 팝업 바탕 — 세로로 쌓는 창이고 내용 크기에 맞춰 줄지 않는다(툴팁처럼 작아지는 것 제외)
    //   열·오버레이 창('(MAIN VIEW)'의 'Body Panel') 안의 구획은 빠진다 — 액자 안에 액자가 겹친다(길드 창고 · 2026-11-10)
    private static bool IsWindow(Image image)
        => image.TryGetComponent(out VerticalLayoutGroup _) && !image.TryGetComponent(out ContentSizeFitter _) && !IsInsideColumn(image.transform);

    private static bool IsInsideColumn(Transform node)
    {
        for (Transform? t = node.parent; t != null; t = t.parent)
        {
            if (t.name == "Body Panel" && t.parent != null && t.parent.name.EndsWith("(MAIN VIEW)"))
            {
                return true;
            }
        }

        return false;
    }

    // 창 액자 — 마지막 자식으로 덮고, 레이아웃 여백을 띠만큼 늘린다
    private static void AddFrame(RectTransform window)
    {
        Sprite? trim = FxSprites.UiPanelTrim;

        if (trim == null || window.Find(FrameName) != null)
        {
            return;
        }

        Image frame = CreateOverlay(window, FrameName, trim, true);
        frame.transform.SetAsLastSibling();

        if (window.TryGetComponent(out LayoutGroup layout))
        {
            RectOffset pad = layout.padding;
            layout.padding = new RectOffset(pad.left + FramePadding, pad.right + FramePadding, pad.top + FramePadding, pad.bottom + FramePadding);
            LayoutRebuilder.MarkLayoutForRebuild(window);
        }
    }

    // 바탕이 투명한 것(표 머리의 정렬 버튼처럼 글씨만 누르는 자리)에는 장식을 깔지 않는다 — 없던 상자가 생긴다
    private static bool IsVisible(Image image)
    {
        float alpha = image.color.a;

        if (image.TryGetComponent(out Selectable selectable)
            && selectable.transition == Selectable.Transition.ColorTint
            && selectable.targetGraphic == image)
        {
            alpha *= selectable.colors.normalColor.a;
        }

        return alpha > 0.1f;
    }

    // 장식 덧그림 — 바탕 크기에 꽉 맞춘 첫 자식
    private static void AddTrim(Image body, Sprite trim, bool tiled)
    {
        if (body.transform.Find(TrimName) != null)
        {
            return;
        }

        CreateOverlay((RectTransform)body.transform, TrimName, trim, tiled).transform.SetAsFirstSibling();
    }

    // 부모에 꽉 맞춘 장식 Image — 레이아웃에 끼지 않고 클릭을 막지 않는다
    private static Image CreateOverlay(RectTransform parent, string name, Sprite trim, bool tiled)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.layer = parent.gameObject.layer;

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        go.GetComponent<LayoutElement>().ignoreLayout = true;

        var image = go.GetComponent<Image>();
        image.sprite                  = trim;
        image.type                    = tiled ? Image.Type.Tiled : Image.Type.Sliced;
        image.fillCenter              = false;
        image.raycastTarget           = false;
        image.pixelsPerUnitMultiplier = PixelsPerUnitMultiplier;

        return image;
    }
}
