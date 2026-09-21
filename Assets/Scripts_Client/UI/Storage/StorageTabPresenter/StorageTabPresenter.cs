using System;
using UnityEngine;
using UnityEngine.UI;

// 창고 열의 탭. 인스펙터에 넣은 버튼과 짝지어 무엇을 그릴지 정한다.
//
// 값의 순서는 **화면의 탭 순서와 같게 유지한다** — 코드만 읽어도 화면이 그려져야 한다.
//
// ⚠️ 값을 중간에 끼우거나 재정렬하면 **씬 배선을 반드시 함께 고친다.**
// 씬에는 이 enum이 int로 저장돼 있어, 코드만 바꾸면 같은 숫자가 다른 탭으로 읽힌다
// — 컴파일도 경고도 통과하고 배선만 조용히 어긋난다.
// 그냥 늘리기만 할 때는 **끝에 붙이면** 씬을 안 건드려도 된다.
// 이 enum이 seam인 이유와 탭을 채우는 절차는 'Storage 규칙.md' 참조.
public enum StorageTab
{
    // 채취로 모은 자원. 창고의 기본 탭이라 화면에서도 맨 왼쪽이다.
    Resource,

    // 보유 캐릭터. 배치 중인지 아닌지를 함께 보여 준다.
    Character,

    // 보유 장비. 장착 중인지 아닌지를 함께 보여 준다.
    // ※ 자원이 아니라 캐릭터와 같은 모양이다 — 개체마다 번호가 있고 정의는 'EquipTable'에 따로 있다.
    Equipment,

    // 특성 — 계정 레벨로 얻은 포인트로 찍는 트리.
    // ⚠️ **이 탭만 격자를 쓰지 않는다** — 칸 목록이 아니라 선으로 이어진 트리라
    //    'TraitPresenter'가 따로 그린다 ('Storage 규칙.md'의 "탭이 달라도 격자는 하나다" 예외).
    Trait,
}

// 창고 열의 탭 줄 — 자원 · 캐릭터 · 장비 · 특성 4탭 (씬 왼쪽부터). (기획 2.2)
//
// **전환은 여기 한 곳이다.** 탭마다 화면을 두지 않고 격자('StorageGridPresenter') 하나가
// 내용을 갈아 끼우므로, 이 클래스는 "어느 탭인가"만 넘긴다
// ('Storage 규칙.md'의 "탭 전환도 층은 하나다").
//
// ■ 아직 데이터가 없는 탭은 버튼을 잠근다
//   잠금 여부를 여기 적어 두지 않고 **그릴 것이 있는지**로 판정한다('HasScreen').
//   두 곳에 적으면 공급자를 붙이고도 버튼이 잠긴 채 남는다 — 탭을 채우는 사람이
//   이 파일을 안 열어도 되게 한다.
//   ⚠️ 특성 탭만은 격자가 아니라 'TraitPresenter'가 그린다. 판정이 갈리는 곳은
//   'HasScreen' 한 군데뿐이고, 화면을 여닫는 일은 각 Presenter가 스스로 한다.
public class StorageTabPresenter : MonoBehaviour
{
    // 창고를 열었을 때의 탭. 자원이 실제 내용물이 가장 많은 탭이라 여기서 시작한다.
    private const StorageTab DefaultTab = StorageTab.Resource;

    // 탭 하나 — 버튼과 그 버튼이 여는 탭. 인스펙터에서 짝지어 넣는다.
    [Serializable]
    private struct TabEntry
    {
        [Tooltip("이 줄이 어느 탭인가")]
        public StorageTab tab;

        [Tooltip("그 탭의 버튼 (씬 왼쪽부터 자원·캐릭터·장비·특성)")]
        public Button button;
    }

    // ※ NonReorderable로 두 가지를 동시에 얻는다 —
    //   [1] 드래그로 순서가 뒤바뀌어도 tab 값이 함께 따라가니 배선은 안 깨지지만,
    //       목록 순서를 화면·enum과 나란히 두어야 사람이 한눈에 대조할 수 있다.
    //   [2] reorderable list로 그려지면 Unity가 그 위의 [CenterHeader]를 건너뛴다 ('UI 규칙.md'의 "공통 작성 규약")
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("탭 버튼들. StorageTab 값마다 정확히 한 줄씩, 화면과 같은 순서로 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];

    [SerializeField, Tooltip("탭 내용을 그리는 격자. 같은 캔버스의 Grid Presenter")]
    private StorageGridPresenter grid = null!;

    [SerializeField, Tooltip("특성 탭을 그리는 화면. 같은 캔버스의 Trait Presenter (격자를 쓰지 않는 유일한 탭)")]
    private TraitPresenter traitScreen = null!;


    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private Color selectedColor = new Color(0.62f, 0.78f, 1f, 1f);

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private Color normalColor = Color.white;

    // 지금 열려 있는 탭. 창고를 닫아도 유지된다 — 다시 열면 보던 탭이 그대로 있다.
    public StorageTab CurrentTab { get; private set; } = DefaultTab;

    // 탭이 바뀌었다 ('StorageToolPresenter'가 구독 — 특성 탭에서는 도구 줄이 숨는다).
    //
    // ※ 격자에게는 이 이벤트로 알리지 않는다 — 여기서 직접 'ShowTab'을 부른다.
    //   내용을 갈아 끼우는 일은 전환의 본체라 구독으로 돌리면 순서가 흐려진다.
    public event Action<StorageTab>? TabChanged;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(grid, nameof(grid));

        ValidateTabs();
        BindButtons();
        WakeTabScreens();
        ShowTab(DefaultTab);
    }

    #region 초기화

    // 인스펙터 배선이 'StorageTab'과 맞는지 본다 (Start에서 한 번).
    //
    // 빠진 탭은 조용히 안 열린다 — 버튼을 눌러도 아무 일이 없어서 버튼이 고장 난 것처럼 보인다.
    // 원인이 인스펙터라는 걸 드러내려고 여기서 먼저 알린다.
    private void ValidateTabs()
    {
        foreach (StorageTab tab in Enum.GetValues(typeof(StorageTab)))
        {
            int count = 0;

            foreach (TabEntry entry in tabs)
            {
                if (entry.tab == tab && entry.button != null)
                {
                    count++;
                }
            }

            if (count != 1)
            {
                ClientLogger.Error(ClientLogger.UI,
                    $"창고 탭 '{tab}'의 버튼이 {count}개다 (정확히 1개여야 한다). " +
                    $"Tab Presenter의 Tabs를 확인할 것.", this);
            }
        }
    }

    // 탭 버튼을 배선하고, 데이터가 없는 탭은 잠근다 (Start에서 호출).
    private void BindButtons()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            StorageTab tab = entry.tab;
            entry.button.onClick.AddListener(() => OnTabClicked(tab));

            // 화면이 준비되지 않은 탭은 누를 수 없게 둔다 — 눌러도 아무 일이 없으면
            // 고장과 구분되지 않고, 잠긴 버튼은 유니티 기본 Disabled 색으로 흐려진다.
            entry.button.interactable = HasScreen(tab);
        }
    }

    // 이 탭에 그릴 것이 있는가 (BindButtons에서 호출).
    //
    // 셋은 격자가 공급자를 갖고 있는지로, 특성 하나는 전용 화면이 배선돼 있는지로 갈린다.
    // **판정이 갈리는 곳은 여기 하나뿐이다.**
    private bool HasScreen(StorageTab tab)
    {
        return tab == StorageTab.Trait ? traitScreen != null : grid.HasSource(tab);
    }

    // 탭을 타는 화면들을 일단 켠다 (Start에서 'ShowTab'보다 먼저).
    //
    // ■ 왜 켜 놓고 시작하나
    //   이 화면들은 자기 탭이 아니면 **스스로 꺼진다.** 그래서 꺼진 채로 씬에 저장되기 쉬운데,
    //   꺼진 오브젝트는 'Start'가 돌지 않아 'TabChanged'를 **구독하지 못한다.**
    //   그 상태로는 자기 탭을 눌러도 켜 줄 사람이 없어 영영 안 돌아온다
    //   (2026-09-22: 특성 화면이 꺼진 채 저장돼 특성 탭이 빈 화면이 됐다).
    //
    // ■ 그래서 최초 진입도 탭 전환과 같은 경로를 탄다
    //   켜 둔 뒤 'ShowTab'을 부르면 각 화면이 자기 'Start'에서 현재 탭을 보고 스스로 물러난다.
    //   이 지점엔 아직 아무도 구독하기 전이라 방금 켠 화면이 'TabChanged'로 다시 꺼지지 않는다
    //   — 꺼지면 'Start'가 또 안 돌아 같은 덫에 빠진다.
    //   그 'Start'는 첫 'Update' 전 같은 프레임 안에서 돌므로 화면이 깜빡이지 않는다.
    //
    // ※ 격자는 여기 없다 — 구독이 아니라 'ShowTab'이 직접 부르고 초기화도 지연이라,
    //   꺼진 채로 저장돼도 스스로 깨어난다('StorageGridPresenter.EnsureInitialized').
    private void WakeTabScreens()
    {
        // 탭 줄과 화면들은 모두 캔버스 직속 형제다. 부모가 없으면(테스트 등) 자기를 기준으로 본다.
        Transform root = transform.parent != null ? transform.parent : transform;

        WakeScreen(traitScreen);
        WakeScreen(root.GetComponentInChildren<StorageToolPresenter>(true));
        WakeScreen(root.GetComponentInChildren<SellCartPresenter>(true));
    }

    // 그 화면을 켠다 (WakeTabScreens에서 호출). 아직 없는 화면은 건너뛴다.
    private static void WakeScreen(Component? screen)
    {
        if (screen == null)
        {
            return;
        }

        screen.gameObject.SetActive(true);
    }

    #endregion

    #region 탭 전환

    // 탭을 눌렀다 (탭 버튼 OnClick에 코드로 연결)
    private void OnTabClicked(StorageTab tab)
    {
        ShowTab(tab);
    }

    // 그 탭을 열고 나머지 버튼의 선택 표시를 끈다 (Start · 탭 버튼).
    private void ShowTab(StorageTab tab)
    {
        CurrentTab = tab;

        grid.ShowTab(tab);
        RefreshSelection();

        TabChanged?.Invoke(tab);
    }

    // 지금 열린 탭의 버튼만 선택 색으로 칠한다 (ShowTab에서 호출).
    //
    // ⚠️ Image.color를 직접 건드리지 않는다 — 버튼의 Transition이 ColorTint라
    // 다음 상태 변화(마우스가 스치기만 해도)에 덮어써진다. 색의 주인은 ColorBlock이다.
    private void RefreshSelection()
    {
        foreach (TabEntry entry in tabs)
        {
            if (entry.button == null)
            {
                continue;
            }

            Color target = entry.tab == CurrentTab ? selectedColor : normalColor;

            ColorBlock colors = entry.button.colors;
            colors.normalColor   = target;
            colors.selectedColor = target;
            entry.button.colors  = colors;
        }
    }

    #endregion
}
