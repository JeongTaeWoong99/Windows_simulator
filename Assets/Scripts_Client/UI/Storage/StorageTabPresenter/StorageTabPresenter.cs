using System;
using UnityEngine;
using UnityEngine.UI;

// 창고 열의 탭. 인스펙터에 넣은 버튼과 짝지어 무엇을 그릴지 정한다.
//
// ⚠️ 값을 중간에 끼우지 않는다 — 씬에 int로 저장돼 있어 순서가 밀리면 'Tabs' 배선이
// 조용히 어긋난다(컴파일도 경고도 통과한다). 늘릴 때는 끝에 붙인다.
// 이 enum이 seam인 이유와 탭을 채우는 절차는 'Storage 규칙.md' 참조.
public enum StorageTab
{
    // 보유 캐릭터. 배치 중인지 아닌지를 함께 보여 준다.
    Character,

    // 장비 — ⏸ 아직 화면이 없다. 데이터가 없을 뿐 아니라 'ItemTable.ItemType'이 산업 축이라
    // 장비를 담을 칸이 테이블에 없다. 컬럼 축부터 정해야 한다 (T-041 · T-002).
    Equipment,

    // 채취로 모은 자원. 창고의 기본 탭이다.
    Resource,

    // 특성 — ⏸ 기획은 있으나 서버 구현·패킷이 없다 (T-041).
    Trait,
}

// 창고 열의 탭 줄 — 자원 · 캐릭터 · 장비 · 특성 4탭 (씬 왼쪽부터). (기획 2.2)
//
// **전환은 여기 한 곳이다.** 탭마다 화면을 두지 않고 격자('StorageGridPresenter') 하나가
// 내용을 갈아 끼우므로, 이 클래스는 "어느 탭인가"만 넘긴다
// ('Storage 규칙.md'의 "탭 전환도 층은 하나다").
//
// ■ 아직 데이터가 없는 탭은 버튼을 잠근다
//   잠금 여부를 여기 적어 두지 않고 **격자에 공급자가 있는지**로 판정한다.
//   두 곳에 적으면 공급자를 붙이고도 버튼이 잠긴 채 남는다 — 탭을 채우는 사람이
//   이 파일을 안 열어도 되게 한다.
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

        // ※ 배열 순서는 화면의 버튼 순서와 달라도 된다 — 짝만 맞으면 된다.
        //   실제로 씬은 왼쪽부터 자원·캐릭터·장비·특성이고, enum 순서(Character 먼저)와 다르다.
        [Tooltip("그 탭의 버튼")]
        public Button button;
    }

    // ※ NonReorderable로 두 가지를 동시에 얻는다 —
    //   [1] 드래그로 순서가 뒤바뀌어도 tab 값이 함께 따라가니 의미는 안 깨지지만,
    //       화면의 버튼 순서와 목록 순서가 어긋나면 사람이 배선을 확인하기 어려워진다.
    //   [2] reorderable list로 그려지면 Unity가 그 위의 [CenterHeader]를 건너뛴다 ('UI 규칙.md'의 "공통 작성 규약")
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("탭 버튼들. StorageTab 값마다 정확히 한 줄씩 넣는다")]
    private TabEntry[] tabs = new TabEntry[0];

    [SerializeField, Tooltip("탭 내용을 그리는 격자. 같은 캔버스의 Grid Presenter")]
    private StorageGridPresenter grid = null!;

    [CenterHeader("선택 표시")]
    [SerializeField, Tooltip("지금 열려 있는 탭의 버튼 색")]
    private Color selectedColor = new Color(0.62f, 0.78f, 1f, 1f);

    [SerializeField, Tooltip("열려 있지 않은 탭의 버튼 색")]
    private Color normalColor = Color.white;

    // 지금 열려 있는 탭. 창고를 닫아도 유지된다 — 다시 열면 보던 탭이 그대로 있다.
    public StorageTab CurrentTab { get; private set; } = DefaultTab;

    // 참조 확보 → 배선 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(grid, nameof(grid));

        ValidateTabs();
        BindButtons();
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
            entry.button.interactable = grid.HasSource(tab);
        }
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
