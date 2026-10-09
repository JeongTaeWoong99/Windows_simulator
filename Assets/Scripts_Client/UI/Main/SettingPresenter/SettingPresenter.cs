using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 설정 화면의 탭. 씬의 탭 버튼·페이지와 1:1이다.
public enum SettingTab
{
    General,
    Graphic,
    Audio,
    Etc,
}

// 설정 패널 — 사용자가 바꿀 수 있는 값을 입력받아 넘기는 곳.
//
// ■ 탭 4개 · 줄 목록
// 탭마다 페이지 하나가 있고, 페이지는 "이름 | 조작" 줄을 세로로 쌓는다. 탭을 누르면 그 페이지만 켠다.
// 여기서 배선하는 건 **동작하는 줄뿐**이다. '(기능 없음)' 줄은 씬에만 있고 조작이 잠겨 있다 —
// 기능이 생기면 그 줄의 조작을 여기 필드로 받아 배선하고 '(기능 없음)' 글씨를 지운다('Main 규칙.md').
//
// ■ 위치 드롭다운은 하나로 창·위젯을 함께 정한다
// 창의 데스크톱 위치(앵커)와 위젯의 창 안 위치는 같은 6칸이라, 드롭다운 하나가
// 'WindowManager'와 'WidgetPositionLayout' 양쪽을 같은 인덱스로 몰이한다.
//
// ■ 드롭다운 옵션은 코드가 채운다
// 라벨을 인스펙터에 손으로 넣으면 enum이 바뀔 때 조용히 어긋난다.
//
// ⚠️ 창 관련 변화는 빌드(.exe)에서만 실제로 일어난다 — 에디터에서는 값만 바뀌고 창은 그대로다
// ('DesktopWindow 규칙.md' 5-1).
public class SettingPresenter : MonoBehaviour
{
    // 설정을 처음 열었을 때의 탭.
    private const SettingTab DefaultTab = SettingTab.General;

    // 탭 하나 — 버튼과 그 버튼이 여는 페이지. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 줄이 어느 탭인가")]
        public SettingTab tab;

        [Tooltip("그 탭의 버튼 (씬 왼쪽부터 일반·그래픽·오디오·기타)")]
        public Button button;

        [Tooltip("그 탭을 누르면 켜지는 페이지")]
        public GameObject page;
    }

    // ※ 아래는 인스펙터에서 반드시 연결해야 하는 "필수" 참조다.
    //   nullable:enable 상태라 ? 없이 두면 "생성자 종료 시 non-null" 검사(CS8618)에 걸리는데,
    //   ? 로 두면 미연결 시 조용히 무시돼 "왜 안 되지?"가 되어버린다. 그래서 = null! 로 non-null
    //   타입을 유지(경고 제거)하고, Start()에서 == null 검사로 미연결을 예외로 즉시 드러낸다(fail-fast).
    // ※ 이 화면엔 제목을 두지 않는다 — #Main Canvas 의 Title 이 이미 "Setting"을 띄운다.
    [CenterHeader("공통 Header Panel (항상 보인다)")]
    [SerializeField, Tooltip("작업슬롯 목록으로 나간다. OnClick은 코드가 연결한다")]
    private Button backButton = null!;

    // ※ NonReorderable — reorderable list로 그려지면 Unity가 그 위의 [CenterHeader]를 건너뛴다('UI 규칙.md')
    [SerializeField, NonReorderable, Tooltip("탭 버튼과 페이지. SettingTab 값마다 정확히 한 줄씩, 화면과 같은 순서로 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];

    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private UIThemeRole selectedRole = UIThemeRole.ButtonSelected;

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private UIThemeRole normalRole = UIThemeRole.Button;

    [CenterHeader("일반")]
    [SerializeField] private Toggle   topmostToggle = null!; // 항상 위에 고정
    [SerializeField] private TMP_Text versionText   = null!; // 빌드 버전

    [CenterHeader("그래픽")]
    [SerializeField] private TMP_Dropdown frameRateDropdown       = null!; // 최대 FPS (30·60·90·144·모니터 동기화)
    [SerializeField] private TMP_Dropdown fpsTextPositionDropdown = null!; // FPS 표시 위치 (숨김·네 구석)
    [SerializeField] private TMP_Dropdown sizeDropdown            = null!; // 창 크기 배율 프리셋
    // 위치 드롭다운 하나가 창의 데스크톱 위치(앵커)와 위젯의 창 안 위치(6칸)를 함께 정한다.
    // 둘은 같은 6칸 나열 순서라 인덱스가 1:1이다 — 짝이 맞는 모서리 조합만 유효.
    [SerializeField] private TMP_Dropdown windowPositionDropdown  = null!; // 창+위젯 위치 (6칸)
    [SerializeField] private Slider       opacitySlider           = null!; // 화면 투명도 (불투명도 10~100%)
    [SerializeField] private TMP_Text     opacityValueText        = null!; // 슬라이더 왼쪽 — 지금 값 (예: 80%)
    [SerializeField] private Button       resetSizeButton         = null!; // 창 크기 복원
    [SerializeField] private Button       resetPositionButton     = null!; // 창 위치 복원

    // ※ WidgetPositionLayout은 Services에 등록되지 않는다([ExecuteAlways] 레이아웃 컴포넌트라
    //   에디터에서도 돌아야 해서 서비스 로케이터에 묶지 않았다). 그래서 인스펙터로 직접 받는다.
    //
    // ★ 그 컴포넌트는 이 패널이 아니라 !Horizontal Columns 에 붙어 있다 — 같은 오브젝트에 두면
    //   설정 화면이 꺼져 있는 동안 OnEnable 이 안 돌아 3열 순서와 위젯 위치가 아예 반영되지 않는다.
    [SerializeField, Tooltip("위젯 위치를 실제로 반영할 레이아웃 컴포넌트 — !Horizontal Columns 에 있다")]
    private WidgetPositionLayout widgetLayout = null!;

    // 지금 열려 있는 탭. 설정을 닫아도 유지된다 — 다시 열면 보던 탭이 그대로 있다.
    private SettingTab _currentTab = DefaultTab;

    // 토글/드롭다운을 현재 값으로 맞추고, 조작을 각 담당자에게 연결한다 (Unity 메시지)
    private void Start()
    {
        // 필수 참조 검증 — 미연결(null)이면 조용히 넘어가지 않고 즉시 예외로 어떤 참조인지 알린다.
        this.RequireRef(backButton,              nameof(backButton));
        this.RequireRef(topmostToggle,           nameof(topmostToggle));
        this.RequireRef(versionText,             nameof(versionText));
        this.RequireRef(frameRateDropdown,       nameof(frameRateDropdown));
        this.RequireRef(fpsTextPositionDropdown, nameof(fpsTextPositionDropdown));
        this.RequireRef(sizeDropdown,            nameof(sizeDropdown));
        this.RequireRef(windowPositionDropdown,  nameof(windowPositionDropdown));
        this.RequireRef(opacitySlider,           nameof(opacitySlider));
        this.RequireRef(opacityValueText,        nameof(opacityValueText));
        this.RequireRef(resetSizeButton,         nameof(resetSizeButton));
        this.RequireRef(resetPositionButton,     nameof(resetPositionButton));
        this.RequireRef(widgetLayout,            nameof(widgetLayout));

        var window  = Services.Get<WindowManager>();
        var ui      = Services.Get<UIManager>();
        var display = Services.Get<DisplayManager>();

        // ─── 헤더 ───
        // 이 화면을 직접 끄지 않는다 — UIManager 가 목록을 켜면서 같은 자리의 이 화면을 끈다.
        // 스스로 끄면 목록이 켜지기 전 빈 칸이 남는다 ('Main 규칙.md'의 "전환 층은 하나다").
        backButton.onClick.AddListener(() => ui.ShowMainScreen(MainScreen.WorkStationList));

        ValidateTabs();
        BindTabs();
        ShowTab(_currentTab);

        // ─── 일반 ───
        // 초기값은 WindowManager가 저장에서 복원해 둔 "현재" 값이다.
        BindToggle(topmostToggle, window.Topmost, window.SetTopmost);
        versionText.text = Application.version;

        // ─── 그래픽 · 표시 ───
        // 창 설정과 달리 에디터에서도 저장값이 시작값이고, 고르는 즉시 저장·반영된다('DisplayManager').
        BindDropdown(frameRateDropdown,       display.GetFrameRateLabels(),       display.FrameRateIndex,       display.SetFrameRateByIndex);
        BindDropdown(fpsTextPositionDropdown, display.GetFpsTextPositionLabels(), display.FpsTextPositionIndex, display.SetFpsTextPositionByIndex);
        BindOpacitySlider(display);

        // ─── 그래픽 · 창 ───
        // ⚠️ 크기 드롭다운을 채우기 전에 알려야 한다 — '작업표시줄 맞춤' 항목의 라벨(계산된 배율)이
        //   이 값에서 나오기 때문이다. 순서가 뒤바뀌면 배율 없이 이름만 있는 라벨이 굳는다.
        //
        // 위젯 칸 높이는 씬 레이아웃(3열 구조 · widgetWeight)에서 파생되는 값이라 창 쪽에서 알 수 없다.
        // 여기서 건네야 'WindowManager'가 "위젯을 작업표시줄 높이로 만들 창 크기"를 역산할 수 있다.
        window.SetWidgetSlotHeight(widgetLayout.WidgetSlotHeight);

        BindDropdown(sizeDropdown, window.GetSizeLabels(), window.SizeIndex, window.SetWindowSizeByIndex);

        // 위치 드롭다운 하나가 창 앵커와 위젯 위치를 함께 몰이한다. 시작 인덱스는 창 앵커를 권위 소스로 삼는다.
        // ※ 이 Start는 부팅이 아니라 "설정 패널을 처음 열 때" 돈다(UIManager가 시작 시 Setting 패널을 꺼 둠).
        //   그래서 아래 SetPosition은 부팅 정렬이 아니라, 옛 저장값이 서로 어긋나 있으면 설정을 여는 이 순간
        //   위젯을 창 앵커에 맞춰 흡수하는 역할이다. 부팅 직후 일치는 두 공장 기본값을 같은 모서리로 맞춰 보장한다.
        int startPosition = window.AnchorIndex;
        widgetLayout.SetPosition((WidgetPosition)startPosition);
        BindDropdown(windowPositionDropdown, window.GetAnchorLabels(), startPosition, index =>
        {
            window.SetAnchorByIndex(index);
            widgetLayout.SetPosition((WidgetPosition)index);
        });

        // 복원 버튼은 값을 바꾼 뒤 드롭다운 표시도 따라 맞춘다 — 알림 없이 맞춰 같은 적용이 두 번 돌지 않게 한다.
        resetSizeButton.onClick.AddListener(() =>
        {
            window.ResetWindowSize();
            sizeDropdown.SetValueWithoutNotify(window.SizeIndex);
        });
        resetPositionButton.onClick.AddListener(() =>
        {
            window.ResetWindowPosition();
            widgetLayout.SetPosition((WidgetPosition)window.AnchorIndex);
            windowPositionDropdown.SetValueWithoutNotify(window.AnchorIndex);
        });
    }

    #region 탭

    // 인스펙터 배선이 'SettingTab'과 맞는지 본다 (Start에서 한 번).
    // 빠진 탭은 버튼을 눌러도 아무 일이 없어 고장처럼 보이므로, 원인이 인스펙터라는 걸 먼저 알린다.
    private void ValidateTabs()
    {
        foreach (SettingTab tab in Enum.GetValues(typeof(SettingTab)))
        {
            int count = 0;

            foreach (TabEntry entry in tabs)
            {
                if (entry.tab == tab && entry.button != null && entry.page != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"설정 탭 '{tab}'의 버튼·페이지 짝이 {count}개다 (정확히 1개여야 한다). " +
                    $"Setting Presenter의 Tabs를 확인할 것.", this);
            }
        }
    }

    // 탭 버튼을 배선한다 (Start에서 호출).
    private void BindTabs()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            SettingTab tab = entry.tab;
            entry.button.onClick.AddListener(() => ShowTab(tab));
        }
    }

    // 그 탭의 페이지만 켜고 버튼 선택 색을 맞춘다 (Start · 탭 버튼).
    //
    // ⚠️ 버튼 Image.color를 직접 건드리지 않는다 — Transition이 ColorTint라 다음 상태 변화에
    //   덮어써진다. 색의 주인은 ColorBlock이다('InventoryTabPresenter.RefreshSelection'과 같다).
    private void ShowTab(SettingTab tab)
    {
        _currentTab = tab;

        foreach (TabEntry entry in tabs)
        {
            bool selected = entry.tab == tab;

            if (entry.page != null)
            {
                entry.page.SetActive(selected);
            }

            if (entry.button == null)
            {
                continue;
            }

            Color target = UIThemePalette.Of(selected ? selectedRole : normalRole);

            ColorBlock colors = entry.button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    #endregion

    #region 배선

    // 토글을 시작값으로 세팅(알림 없이)하고, 값 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindToggle(Toggle toggle, bool startValue, UnityAction<bool> onChanged)
    {
        toggle.SetIsOnWithoutNotify(startValue);   // 시작 상태에 맞춰 체크(콜백 없이)
        toggle.onValueChanged.AddListener(onChanged);
    }

    // 드롭다운 옵션을 채우고 시작 인덱스로 세팅(알림 없이)한 뒤, 선택 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindDropdown(TMP_Dropdown dropdown, List<string> options, int startIndex, UnityAction<int> onChanged)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(startIndex); // 시작 인덱스에 맞춤(콜백 없이)
        dropdown.onValueChanged.AddListener(onChanged);
    }

    // 투명도 슬라이더 — 끌면 즉시 반영하고, 손을 떼면 한 번 저장한다.
    // ※ 범위는 코드가 정한다 — 인스펙터 min·max를 손으로 맞추면 'DisplayManager'의 범위와 어긋난다.
    //   왼쪽 글씨는 지금 값을 보여 준다 — 왼쪽 끝이 0%가 아니라 최소값(10%)이라는 것도 이걸로 드러난다.
    private void BindOpacitySlider(DisplayManager display)
    {
        opacitySlider.wholeNumbers = true;
        opacitySlider.minValue     = DisplayManager.MinOpacityPercent;
        opacitySlider.maxValue     = DisplayManager.MaxOpacityPercent;
        opacitySlider.SetValueWithoutNotify(display.OpacityPercent);
        ShowOpacity(display.OpacityPercent);
        opacitySlider.onValueChanged.AddListener(value =>
        {
            int percent = Mathf.RoundToInt(value);
            display.SetOpacity(percent);
            ShowOpacity(percent);
        });

        // 슬라이더에는 "손을 뗐다" 이벤트가 없어 PointerUp을 따로 받는다.
        var trigger = opacitySlider.gameObject.AddComponent<EventTrigger>();
        var entry   = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entry.callback.AddListener(_ => display.SaveOpacity());
        trigger.triggers.Add(entry);
    }

    // 투명도 값 글씨를 갱신한다 (BindOpacitySlider · 슬라이더 onValueChanged).
    private void ShowOpacity(int percent)
    {
        opacityValueText.text = $"{percent}%";
    }

    #endregion
}
