using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 작업슬롯 한 칸의 설정 화면. 목록에서 칸을 누르면 목록 대신 이 화면이 열린다.
//
// 머리 둘(Header · Industry)은 늘 보이고, 몸통 둘(배치 목록 ↔ 세팅)이 갈아 끼워진다.
//
// ■ 화면이 상태를 기억하지 않는다
// "지금 배치돼 있는가"는 서버 스냅샷('PlayerDataModel.WorkStationSlots')에서 읽는다.
// 자체 플래그를 들면 실패 응답이 왔을 때 화면과 서버가 어긋난다.
// 고른 산업만은 서버에 없는 값이라 여기서 들고 있는데, 단계마다 주인이 다르다 —
// 캐릭터 목록에선 화면이 소유한 '걸러 보는 값'이고, 세팅에선 슬롯의 실제 산업에서 '파생'된다
// ('SyncSelectedIndustryToSlot').
//
// ⚠️ 지금 고를 수 없는 캐릭터는 목록에서 뺀다 — 적성 0이거나 다른 슬롯에서 일하는 중이면 걸러진다.
// 예전에는 "숨기지 않고 잠근다"였다. 16마리를 기르면 낚시를 눌러도 16줄이 그대로 남아
// **누를 수 있는 것을 골라내는 일을 사람이 하게 되기 때문에** 뒤집었다(T-046).
// 목록이 통째로 비면 안내 문구 하나만 뜨고(빈 목록은 고장과 구분되지 않는다),
// "내 캐릭터가 어디 갔나"의 답은 창고 캐릭터 탭이 맡는다 — 거기에 적성 스트립(5칸 · 위치=산업)과 '배' 마크가 있다.
// 적성은 패킷('CharacterInfo.Aptitudes')에서 온다 — 테이블을 직접 읽지 않는다.
//
// ■ 세팅 단계의 구성 (목업 'GameDesign/design/ui/게임UI목업(2026-07-30 업데이트).html'의 슬롯 상세)
// 캐릭터 카드 · 장비 4칸 · 효율 계산. 카드는 목록 줄과 **같은 프리팹**이다('CharacterStateRowView').
// 임시로 둔 자리 — 데이터·리소스가 없어 흰 네모와 "추가 예정"만 있고, 코드는 건드리지 않는다.
//   · 산업 탭 아이콘 · 캐릭터 초상화 · 종족 → 엑셀 · 기획 · 리소스가 나오면 추가 예정 (일감 'T-054')
//   · 효율 계산의 가산 항목(장비 · 특성 · 액티브) → 서버가 내역을 주면 추가 예정 (일감 'T-055')
//
// ■ 장비 4칸 — 누르면 장비 칸만 남고 그 아래가 목록이 된다 (T-074)
// 'Equip Picker Panel'이 켜지면서 **위(레벨 정보·캐릭터 칸)와 아래(효율 계산)를 모두 접는다.**
// 팝업을 띄우지 않는 이유는 이 캔버스가 이미 쓰는 수법이기 때문이다 — 한 자리를 여러 화면이
// 갈아 끼운다('Main 규칙.md'). 같은 칸을 다시 누르면 접힌다.
//
// ⚠️ **끼울 목록을 거르는 것은 표시용이다.** 거절은 서버가 한다('EquipKindMismatch').
// ⚠️ **다른 캐릭터가 낀 장비도 그대로 보낸다** — 서버가 옮긴다. 클라가 해제를 먼저 보내면
//    실패했을 때 장비가 아무 데도 안 낀 상태로 남는다.
// ⚠️ **캐릭터를 슬롯에서 뺄 때만은 클라가 해제를 먼저 보낸다** — 서버의 배치 해제가 착용을
//    건드리지 않기 때문이다('UnequipAllWorn').
//
// 세 단계 흐름 · 응답을 기다렸다 넘어가는 규칙은 'Main 규칙.md'의 "전환 층은 하나다" 절 참조.
public class WorkStationSelectPresenter : MonoBehaviour
{
    [CenterHeader("공통 Header Panel (항상 보인다)")]
    [SerializeField, Tooltip("'슬롯 N 설정' — 어느 칸을 눌러 들어왔는지 알리는 유일한 단서다")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("어느 단계에 있든 슬롯 목록으로 나간다. OnClick은 코드가 연결한다")]
    private Button backButton = null!;

    // ※ NonReorderable 두 가지를 동시에 얻는다 —
    //   [1] 순서가 곧 산업이라 드래그로 뒤바뀌면 조용히 엉뚱한 산업이 나간다. 아예 못 끌게 막는다.
    //   [2] reorderable list 로 그려지면 Unity 가 그 위의 [CenterHeader] 를 건너뛴다 ('UI 규칙.md'의 "공통 작성 규약")
    // 세 색이 세 상태와 1:1이다 — 고름 / 고르지 않음 / 못 고름.
    // 못 고르는 것은 색만 흐린 게 아니라 실제로 잠긴다('CanSelectIndustry').
    [CenterHeader("공통 Industry Panel (항상 보인다)")]
    [SerializeField, Tooltip("고른 산업 버튼의 바탕색 (노랑)")]
    private Color selectedIndustryColor = new Color(0.839f, 0.682f, 0.067f, 1f);

    [SerializeField, Tooltip("고르지 않았지만 고를 수 있는 산업 버튼의 바탕색 (하양)")]
    private Color unselectedIndustryColor = Color.white;

    [SerializeField, Tooltip("잠긴 산업 버튼의 바탕색 (회색). 'Selectable'이 disabledColor로 따로 칠한다")]
    private Color disabledIndustryColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    // 고르는 중인 장비 칸을 **눌러 둔 것처럼** 어둡게 만든다. 칸 바탕은 등급색이라 이 값이 그 위에 곱해진다
    // ('Button'의 색 전이 규칙). 표시용 채널을 따로 만들지 않고 버튼이 이미 가진 것을 쓴다 —
    // 🎨 나중에 ON/OFF 스프라이트로 갈아 끼울 자리이기도 하다.
    [SerializeField, Tooltip("고르는 중인 장비 칸에 곱하는 색 (어둡게). 다른 칸은 하양이라 등급색 그대로다")]
    private Color pickingEquipSlotColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    // ※ 버튼마다 위 'Icon (임시)' · 아래 텍스트다. 아이콘은 흰 네모 — 산업 아이콘 스프라이트가 나오면
    //   각 버튼의 Icon Image에 넣는다(일감 'T-054'). 코드는 버튼 바탕색만 칠하므로 고칠 곳이 없다.
    [SerializeField, NonReorderable, Tooltip("산업 버튼 5개. 인스펙터에 넣은 순서가 곧 산업 순서다(농사·낚시·채굴·벌목·사냥)")]
    private Button[] industryButtons = new Button[0];

    // 산업 레벨 버튼 하나 — 버튼과 그 라벨. 이름이 산업마다 달라('밭'·'저수지') 코드가 갈아 쓴다.
    [Serializable]
    private struct IndustryLevelButton
    {
        [Tooltip("레벨 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
        public Button button;

        [Tooltip("그 버튼의 라벨. 산업이 바뀌면 코드가 'Lv2 밭'처럼 채운다")]
        public TMP_Text label;
    }

    // ※ 산업 버튼 줄 바로 아래의 'Industry Level Panel'이다. 순서가 곧 레벨(1~)이라
    //   인덱스 + 1이 레벨이 된다 — 산업 버튼이 인덱스로 산업을 가리키는 것과 같은 축이다.
    [SerializeField, NonReorderable, Tooltip("산업 레벨 버튼들. 인스펙터에 넣은 순서가 곧 레벨(Lv1부터)이다")]
    private IndustryLevelButton[] industryLevelButtons = new IndustryLevelButton[0];

    // ※ 레벨 버튼 바로 아래에 붙는다. **1·2단계 공통 영역**이라 배치 목록에서도 보인다 —
    //   거기서 산업·레벨은 걸러 보는 수단이고, "이 레벨은 무엇이 나오나"는 그때도 묻는 질문이다.
    [SerializeField, Tooltip("Industry Level Info Panel 오브젝트 — 고른 레벨의 스펙·자원 목록")]
    private GameObject industryLevelInfoPanel = null!;

    [SerializeField, Tooltip("산업 레벨 정보 줄이 쌓이는 부모 — Industry Level Info Panel > Viewport > Content")]
    private RectTransform industryLevelInfoRowParent = null!;

    // 자원 줄만 프리팹이 다르다 — 값이 둘(확률·판매가)이라 한 칸에 넣으면 잘린다
    // ('IndustryDropRowView'의 머리 주석). 스펙 줄은 위 'efficiencyRowPrefab'을 그대로 쓴다.
    [SerializeField, Tooltip("나오는 자원 한 줄 프리팹 (IndustryDropRowView)")]
    private IndustryDropRowView industryDropRowPrefab = null!;

    [CenterHeader("1단계 캐릭터 할당 패널 (전환)")]
    [SerializeField, Tooltip("Character Assign Scroll View Panel 오브젝트")]
    private GameObject assignPanel = null!;

    [SerializeField, Tooltip("캐릭터 줄 프리팹 (CharacterStateRowView 포함). 보일 수만큼 만들어 재사용한다")]
    private CharacterStateRowView rowPrefab = null!;

    [SerializeField, Tooltip("캐릭터 줄이 쌓이는 부모 — Viewport > Content")]
    private RectTransform rowParent = null!;

    [SerializeField, Tooltip("고를 캐릭터가 하나도 없을 때만 켜지는 안내. 빈 목록은 고장과 구분되지 않는다")]
    private TMP_Text emptyText = null!;

    [CenterHeader("2단계 캐릭터 세팅 패널 (전환)")]
    [SerializeField, Tooltip("Character Setting Panel 오브젝트")]
    private GameObject settingPanel = null!;

    // ※ 목록 줄과 같은 프리팹의 인스턴스다 — 버튼 라벨만 "해제"로 바꿔 쓴다.
    [SerializeField, Tooltip("배치된 캐릭터 카드 (CharacterStateRowView). 버튼은 해제만 한다")]
    private CharacterStateRowView assignedCard = null!;

    // 장비를 고르는 동안 카드와 함께 접힌다 — 카드는 'assignedCard.gameObject'로 잡는다.
    [SerializeField, Tooltip("Character Label 오브젝트 — 장비를 고르는 동안 꺼진다")]
    private GameObject characterLabel = null!;

    // 장비 칸 하나 — 버튼과 그 위젯들. 부위 이름은 고정이고 장비 이름·등급색만 코드가 채운다.
    [Serializable]
    private struct EquipSlotButton
    {
        [Tooltip("장비 칸 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
        public Button button;

        // 칸 버튼의 바탕 그래픽이다. 등급 색은 **여기** 칠한다 — 창고 칸·캐릭터 카드와 같은 축이라
        // 🎨 등급 스프라이트가 오면 **네 곳이 함께** 색에서 스프라이트로 바뀐다.
        // ※ 'Button'의 색 전이가 이 색과 곱해진다(누르면 어두워진다). 색은 임시 표기라 감수한다.
        [Tooltip("칸 바탕 — 버튼의 Target Graphic. 낀 장비의 등급 색으로 칠해진다, 비었으면 회색")]
        public Image background;

        [Tooltip("부위 이름. 코드가 '무기'·'장신구1'처럼 채우고, 고르는 중이면 '▼'를 붙인다")]
        public TMP_Text partLabel;

        [Tooltip("낀 장비 이름. 비었으면 '비어있음'")]
        public TMP_Text nameLabel;

        // 무엇을 끼웠는지만으로는 **왜 이걸 끼웠는지**를 알 수 없다 — 창고 장비 탭과 같은 문구를 칸에도 적는다.
        [Tooltip("낀 장비의 효과 한 줄('채굴 +10%'). 비었으면 빈 문자열이라 줄이 사라진다")]
        public TMP_Text effectLabel;
    }

    // ※ 'Equipment Panel'의 칸 4개다. **순서가 곧 칸**이라 인덱스 + 1이 'EEquipSlot'이 된다
    //   (Weapon=1 · Accessory1=2 · Accessory2=3 · Gem=4) — 산업 레벨 버튼과 같은 축이다.
    [SerializeField, NonReorderable, Tooltip("장비 칸 4개. 인스펙터에 넣은 순서가 곧 칸이다(무기·장신구1·장신구2·보석)")]
    private EquipSlotButton[] equipSlotButtons = new EquipSlotButton[0];

    [SerializeField, Tooltip("효율 계산 줄 프리팹 (EfficiencyRowView 포함). 그릴 항목 수만큼 만들어 재사용한다")]
    private EfficiencyRowView efficiencyRowPrefab = null!;

    [SerializeField, Tooltip("효율 계산 줄이 쌓이는 부모 — Efficiency Rows Panel")]
    private RectTransform efficiencyRowParent = null!;

    // ※ 아래 셋은 **같은 자리를 나눠 쓴다** — 장비를 고르는 동안 효율 계산 둘이 꺼지고 고르기가 켜진다.
    [CenterHeader("장비 고르기 (효율 계산 자리를 갈아 끼운다)")]
    [SerializeField, Tooltip("Efficiency Label 오브젝트 — 장비를 고르는 동안 꺼진다")]
    private GameObject efficiencyLabel = null!;

    [SerializeField, Tooltip("Efficiency Rows Panel 오브젝트 — 장비를 고르는 동안 꺼진다")]
    private GameObject efficiencyPanel = null!;

    // ■ 이 화면의 스크롤은 **이것 하나뿐이다** (2026-09-24)
    // 레벨 정보 · 캐릭터 목록 · 세팅 · 장비 목록이 저마다 스크롤을 갖고 있었는데,
    // 겹쳐 놓으니 **휠이 어디로 갈지 알 수 없었다**(안쪽이 먼저 먹고 끝에 닿아도 넘겨주지 않는다).
    // 머리(제목 · 산업 · 산업 레벨)만 고정이고 그 아래는 전부 이 하나에 담겨 함께 굴러간다.
    [SerializeField, Tooltip("Body Scroll Panel의 ScrollRect — 이 화면의 유일한 스크롤")]
    private ScrollRect bodyScroll = null!;

    // 목록 칸('WorkStationSlotView')과 같은 값을 그린다 — 세팅에 들어와도 진행도를 놓치지 않게.
    [SerializeField, Tooltip("Progress Panel 오브젝트 — 장비를 고르는 동안 꺼진다")]
    private GameObject progressPanel = null!;

    [SerializeField, Tooltip("판정 진행도 (0~1). 표시 전용이라 interactable은 꺼 둔다")]
    private Slider progressSlider = null!;

    [SerializeField, Tooltip("Equip Picker Panel 오브젝트. 평소 꺼져 있다")]
    private GameObject equipPickerPanel = null!;

    [SerializeField, Tooltip("'무기 고르기' — 어느 칸을 고르는 중인지 알리는 유일한 단서다")]
    private TMP_Text equipPickerTitle = null!;

    [SerializeField, Tooltip("고르는 칸의 장비를 뺀다. 빈 칸이면 잠긴다")]
    private Button equipUnequipButton = null!;

    [SerializeField, Tooltip("고르기를 접고 효율 계산으로 돌아간다")]
    private Button equipPickerCloseButton = null!;

    [SerializeField, Tooltip("장비 줄 프리팹 (EquipPickRowView 포함). 보일 수만큼 만들어 재사용한다")]
    private EquipPickRowView equipRowPrefab = null!;

    [SerializeField, Tooltip("장비 줄이 쌓이는 부모 — Equip Picker Panel > Viewport > Content")]
    private RectTransform equipRowParent = null!;

    [SerializeField, Tooltip("끼울 장비가 하나도 없을 때만 켜지는 안내. 빈 목록은 고장과 구분되지 않는다")]
    private TMP_Text equipEmptyText = null!;

    // ※ **순서가 곧 산업**이라 산업 버튼 줄과 같은 축이다(농사·낚시·채굴·벌목·사냥).
    //   색 규칙도 같다 — 고른 것만 노랑이다.
    // ※ '모두'는 두지 않는다(2026-09-24 사용자 결정) — 칸을 열면 **그 슬롯의 산업**이 골라진 채로 뜬다.
    [SerializeField, NonReorderable, Tooltip("장비 산업 필터 5개. 인스펙터에 넣은 순서가 곧 산업이다(농사·낚시·채굴·벌목·사냥)")]
    private Button[] equipFilterButtons = new Button[0];

    // 효율 계산 줄 수 — 적성 기본값 · 속도 가산 · 현재 작업속도 · 실효 주기.
    private const int EfficiencyRowCount = 4;

    // 현재 ÷ 기본값이 1에서 이만큼 벗어나야 전역 배수로 본다 — 서버의 천분율 반올림 오차를 흡수한다.
    private const float GlobalMultiplierTolerance = 0.005f;

    // 보낸 요청의 종류. 응답에는 배치였는지 교체였는지 해제였는지가 안 실려 와서 보낸 쪽이 기억한다.
    private enum PendingRequest
    {
        None,
        Assign,
        Replace,  // 교체 — 나가는 패킷은 배치와 같고, 실패했을 때 물러나지 않는 것만 다르다
        Unassign,
        Equip,    // 장비 장착 — 응답이 'S_EquipResponse'라 위 셋과 오는 길이 다르다
        Unequip,  // 장비 해제
    }

    // 버튼 순서와 1:1로 대응하는 산업 목록. enum 값을 인덱스로 직접 쓰면 None(0) 한 칸이 밀리므로
    // 별도 목록으로 들고 있는다.
    private readonly List<EIndustryType> _industries = new List<EIndustryType>();

    // 만들어 둔 줄. 산업을 바꿀 때마다 수가 오르내리므로 파괴하지 않고 꺼 두었다가 다시 쓴다.
    private readonly List<CharacterStateRowView> _rows = new List<CharacterStateRowView>();

    // 이번에 보일 캐릭터. 걸러 낸 결과라 'Characters'와 순번이 다르다 —
    // 매번 새로 만들지 않으려고 필드로 들고 재사용한다(상주 앱이라 GC가 쌓인다).
    private readonly List<CharacterInfo> _visible = new List<CharacterInfo>();

    // 캐릭터 줄의 순서 규칙('CompareRows'). 메서드 그룹을 매번 넘기면 호출마다 대리자가 새로 생겨 Start에서 한 번만 만든다.
    private Comparison<CharacterInfo> _rowOrder = null!;

    // 줄에 넘길 적성 5칸. 산업 목록 순서 그대로 담는다 — 줄마다 새로 만들지 않고 이 배열을 재사용한다.
    // ※ 줄이 받아 그리는 즉시 쓰임이 끝나므로 공유해도 된다('StorageGridPresenter.ReadAptitudes'와 같다).
    private byte[] _aptitudes = new byte[0];

    // 만들어 둔 효율 계산 줄. 캐릭터 줄과 같은 풀 규칙이다.
    private readonly List<EfficiencyRowView> _efficiencyRows = new List<EfficiencyRowView>();

    // 만들어 둔 장비 고르기 줄. 칸마다 목록이 통째로 바뀌므로 파괴하지 않고 꺼 두었다가 다시 쓴다.
    private readonly List<EquipPickRowView> _equipRows = new List<EquipPickRowView>();

    // 산업 레벨 정보의 **스펙** 줄 — 효율 계산과 같은 프리팹을 쓰지만 풀은 따로 둔다(동시에 보이기 때문).
    private readonly List<EfficiencyRowView> _industryLevelInfoRows = new List<EfficiencyRowView>();

    // 산업 레벨 정보의 **자원** 줄. 스펙 줄 뒤에 이어 붙는다.
    private readonly List<IndustryDropRowView> _industryDropRows = new List<IndustryDropRowView>();

    // 지금 고르는 중인 장비 칸. 'None'이면 고르는 중이 아니다 — 그때는 효율 계산이 보인다.
    private EEquipSlot _pickingSlot = EEquipSlot.None;

    // 장비 칸 수 — 'EEquipSlot'의 None을 뺀 개수다(무기·장신구1·장신구2·보석).
    private const int EquipSlotCount = 4;

    // 장비 산업 필터 수 — 채취 산업 5종.
    private const int EquipFilterCount = 5;

    // 지금 걸린 장비 산업 필터. 고르기를 열 때 **그 슬롯의 산업**으로 맞춰진다.
    // ※ 'None'은 화면에서 고를 수 없다 — 배치 전 등 산업을 모르는 때만 잠깐 들고, 그때는 거르지 않는다.
    private EIndustryType _equipFilter = EIndustryType.None;

    // 목록에 낼 장비를 골라 담는 통. 정렬하려면 한 번 모아야 해서 둔다(매번 새로 만들지 않는다).
    private readonly List<EquipInfo> _pickCandidates = new List<EquipInfo>();

    // 지금 다루는 슬롯 번호. Open이 정한다 — 아직 안 열렸으면 -1.
    private int _slotIndex = -1;

    // 지금 고른 산업. 서버가 모르는 값이라 화면이 들고 있는다.
    private int _selectedIndustry;

    // 지금 고른 산업 레벨(1~). **슬롯마다 따로 정하는 값이다** — 서버가 슬롯에 저장하고
    // 판정 시간·드롭 테이블·판정당 경험치가 전부 이 값으로 갈린다('IndustryLevelTable').
    // Lv1은 조건 없이 열려 있어 기본값이 된다.
    private int _selectedIndustryLevel = DefaultIndustryLevel;

    // 조건 없이 늘 열려 있는 기본 레벨. 서버의 'WorkStationSlot.DefaultIndustryLevel'과 같은 값이다.
    private const int DefaultIndustryLevel = 1;

    // 산업 레벨 정보가 펼쳐져 있는가. **펼친 채로 시작한다** (2026-09-24 사용자 결정).
    // 들어오자마자 보이는 편이 낫고, 접는 것은 자리가 아쉬울 때의 선택지로 남긴다.
    // 화면이 통째로 한 스크롤이라(아래 'bodyScroll') 펼쳐도 가려지는 항목 없이 길어질 뿐이다.
    private bool _industryLevelInfoOpen = true;

    // 응답을 기다리는 중인 요청. 없으면 None.
    private PendingRequest _pending = PendingRequest.None;

    // 진행 중인 대기의 손잡이. 응답이 오면 결과를 보고하고, 무응답이면 스스로 타임아웃돼 잠금을 푼다.
    private ServerWaitHandle? _waitHandle;

    // 응답을 기다리는 중인가. 그동안 배치·해제 버튼을 잠근다.
    private bool IsWaiting => _pending != PendingRequest.None;

    private PlayerDataModel   _data    = null!;
    private NetworkManager    _network = null!;
    private UIManager         _ui      = null!;
    private ServerWaitManager _wait    = null!;
    private bool              _isSubscribed;
    private bool              _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(titleText,           nameof(titleText));
        this.RequireRef(backButton,          nameof(backButton));
        this.RequireRef(assignPanel,         nameof(assignPanel));
        this.RequireRef(rowPrefab,           nameof(rowPrefab));
        this.RequireRef(rowParent,           nameof(rowParent));
        this.RequireRef(emptyText,           nameof(emptyText));
        this.RequireRef(settingPanel,        nameof(settingPanel));
        this.RequireRef(assignedCard,        nameof(assignedCard));
        this.RequireRef(characterLabel,      nameof(characterLabel));
        this.RequireRef(efficiencyRowPrefab, nameof(efficiencyRowPrefab));
        this.RequireRef(efficiencyRowParent, nameof(efficiencyRowParent));
        this.RequireRef(industryLevelInfoPanel,     nameof(industryLevelInfoPanel));
        this.RequireRef(bodyScroll,                 nameof(bodyScroll));
        this.RequireRef(industryLevelInfoRowParent, nameof(industryLevelInfoRowParent));
        this.RequireRef(industryDropRowPrefab,      nameof(industryDropRowPrefab));
        this.RequireRef(progressSlider, nameof(progressSlider));
        this.RequireRef(efficiencyLabel,     nameof(efficiencyLabel));
        this.RequireRef(efficiencyPanel,     nameof(efficiencyPanel));
        this.RequireRef(equipPickerPanel,    nameof(equipPickerPanel));
        this.RequireRef(equipPickerTitle,    nameof(equipPickerTitle));
        this.RequireRef(equipUnequipButton,  nameof(equipUnequipButton));
        this.RequireRef(equipPickerCloseButton, nameof(equipPickerCloseButton));
        this.RequireRef(equipRowPrefab,      nameof(equipRowPrefab));
        this.RequireRef(equipRowParent,      nameof(equipRowParent));
        this.RequireRef(equipEmptyText,      nameof(equipEmptyText));

        _data    = Services.Get<PlayerDataModel>();
        _network = NetworkManager.Instance;
        _ui      = Services.Get<UIManager>();
        _wait    = Services.Get<ServerWaitManager>();

        _rowOrder = CompareRows;

        Subscribe();

        BuildIndustryList();
        BindIndustryButtons();
        BindIndustryLevelButtons();
        BindEquipSlotButtons();
        BindEquipFilterButtons();

        equipUnequipButton.onClick.AddListener(RequestUnequip);
        equipPickerCloseButton.onClick.AddListener(CloseEquipPicker);

        // 씬에 켜진 채로 저장됐을 수 있다 — 첫 그림부터 효율 계산이 보이게 접어 둔다.
        CloseEquipPicker();

        backButton.onClick.AddListener(BackToSlotList);
        assignedCard.SetButtonLabel("해제");
        assignedCard.AssignClicked += OnAssignedCardClicked;

        OpenStageForSlot();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    //
    // ★ 재구독만으로는 부족하다 — 닫혀 있는 동안 슬롯 상태가 바뀌었으면 라벨이 낡은 채로 남는다.
    private void OnEnable()
    {
        if (!_isReady)
        {
            return;
        }

        Subscribe();
        Refresh();
    }

    // 구독 해제 (Unity 메시지)
    //
    // ※ 고르기도 함께 접는다 — 안 접으면 다음에 들어왔을 때 남의 칸 목록이 떠 있다.
    private void OnDisable()
    {
        Unsubscribe();

        // 다음 슬롯도 펼친 채로 연다 — 접은 것은 그 화면에서의 선택이라 들고 나가지 않는다.
        _industryLevelInfoOpen = true;

        if (_isReady)
        {
            CloseEquipPicker();
        }
    }

    // 이 슬롯을 다루도록 열린다 ('WorkStationListPresenter'가 칸 클릭에서 호출).
    //
    // ※ 켜기 전에 번호부터 넣는다. 꺼져 있던 화면은 'Start'가 아직 안 돌았을 수 있는데,
    // 그때는 Start가 이어서 단계를 정한다. 이미 돌았으면 여기서 바로 정한다.
    //
    // ※ 여기서 켜고, 부른 쪽이 이어서 'UIManager.ShowMainScreen'으로 나머지 화면을 끈다.
    // 자리를 뺏는 일은 여기서 하지 않는다 — 이 화면은 자기 형제가 몇인지 모른다.
    public void Open(int slotIndex)
    {
        _slotIndex = slotIndex;

        // 닫히는 동안 구독이 끊겨 지난 응답을 놓쳤을 수 있다. 잠금을 들고 들어가지 않는다.
        _pending    = PendingRequest.None;
        _waitHandle = null; // 지난 대기 손잡이는 버린다(남아 있어도 매니저가 타임아웃으로 정리한다)

        // 지난 칸의 목록을 들고 들어가지 않는다 — 다른 슬롯·다른 캐릭터다.
        if (_isReady)
        {
            CloseEquipPicker();
        }

        gameObject.SetActive(true);

        if (_isReady)
        {
            OpenStageForSlot();
        }
    }

    #region 단계 전환

    // 슬롯 상태가 첫 단계를 정한다 — 빈 칸이면 캐릭터를 고르러, 찬 칸이면 세팅으로.
    // ('Start' · 'Open'에서 호출)
    private void OpenStageForSlot()
    {
        if (IsAssigned(FindSlot()))
        {
            ShowSetting();
        }
        else
        {
            ShowAssignList();
        }
    }

    // 2단계 — 캐릭터 목록을 보여 준다.
    private void ShowAssignList()
    {
        CloseEquipPicker(); // 세팅 화면의 것이다 — 목록으로 물러나면 남겨 둘 이유가 없다
        assignPanel.SetActive(true);
        settingPanel.SetActive(false);
        Refresh();
    }

    // 3단계 — 배치된 캐릭터의 세팅을 보여 준다.
    private void ShowSetting()
    {
        assignPanel.SetActive(false);
        settingPanel.SetActive(true);
        Refresh();
    }

    // 슬롯 목록으로 물러난다 (뒤로가기 버튼 · 요청 실패).
    //
    // 이 화면을 직접 끄지 않는다 — 'UIManager'가 목록을 켜면서 같은 자리에 있는 이 화면을 끈다.
    // 스스로 끄면 목록이 안 켜진 빈 칸이 남는 순간이 생긴다.
    private void BackToSlotList()
    {
        _ui.ShowMainScreen(MainScreen.WorkStationList);
    }

    #endregion

    #region 구독

    // 슬롯·캐릭터 캐시 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                    = true;
        _data.WorkStationSlotsChanged   += Refresh;
        _data.CharactersChanged         += Refresh; // 보유 캐릭터가 늘면 줄도 늘어야 한다
        _data.UnlocksChanged            += Refresh; // 특성으로 산업 레벨이 열리면 레벨 버튼이 켜져야 한다
        _data.WorkStationAssignCompleted += OnAssignCompleted;
        _data.EquipsChanged             += OnEquipsChanged;   // 지급·장착·해제가 전부 이 하나로 온다
        _data.EquipCompleted            += OnEquipCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                    = false;
        _data.WorkStationSlotsChanged   -= Refresh;
        _data.CharactersChanged         -= Refresh;
        _data.UnlocksChanged            -= Refresh;
        _data.WorkStationAssignCompleted -= OnAssignCompleted;
        _data.EquipsChanged             -= OnEquipsChanged;
        _data.EquipCompleted            -= OnEquipCompleted;
    }

    #endregion

    #region 산업 선택

    // 채취 가능한 1차 산업만 목록에 담는다 (Start에서 호출)
    //
    // ※ EIndustryType은 1차 산업 5종만 담는 타입이라 범위 필터가 필요 없다 —
    //   아이템 분류(Misc·Special)가 섞여 있던 ItemType과 다르다(이슈 #13).
    private void BuildIndustryList()
    {
        _industries.Clear();

        foreach (EIndustryType industry in Enum.GetValues(typeof(EIndustryType)))
        {
            if (industry != EIndustryType.None) // None은 "배치 해제"라 고를 대상이 아니다
                _industries.Add(industry);
        }

        _aptitudes = new byte[_industries.Count];
    }

    // 산업 버튼을 목록 순서와 묶는다 (Start에서 호출).
    // 버튼 개수와 산업 개수가 다르면 조용히 어긋난다 — 그래서 여기서 먼저 알린다.
    private void BindIndustryButtons()
    {
        if (industryButtons.Length != _industries.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"산업 버튼이 {industryButtons.Length}개인데 채취 산업은 {_industries.Count}종이다. " +
                $"인스펙터의 버튼 목록을 산업 순서(농사·낚시·채굴·벌목·사냥)대로 채울 것.", this);
        }

        for (int i = 0; i < industryButtons.Length; i++)
        {
            if (industryButtons[i] == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            industryButtons[i].onClick.AddListener(() => SelectIndustry(index));
        }
    }

    // 산업을 고른다 (산업 버튼 OnClick에 코드로 연결)
    //
    // ⚠️ 같은 버튼을 두 단계가 다르게 쓴다. 캐릭터 목록에선 '걸러 보는 수단'이라 화면이 값을
    // 들고 즉시 반영하고, 세팅에선 '갈아 끼우는 수단'이라 요청만 보낸다 —
    // 세팅에서 값을 미리 바꾸지 않는 이유는 'SyncSelectedIndustryToSlot' 참조.
    //
    // ※ 색만 바꾸고 끝내지 않는다 — 산업이 바뀌면 각 캐릭터의 적성도 달라져서 잠금 상태가 뒤집힌다.
    private void SelectIndustry(int index)
    {
        if (settingPanel.activeSelf)
        {
            RequestIndustryChange(index);

            return;
        }

        _selectedIndustry = index;

        // 산업이 바뀌면 레벨 잠금이 통째로 뒤집힌다 — 칠하기 전에 고른 레벨부터 맞춘다.
        ClampSelectedIndustryLevel();

        RefreshIndustryButtons();
        RefreshIndustryLevelButtons();

        if (assignPanel.activeSelf)
        {
            RefreshRows();
        }
    }

    // 고른 산업을 슬롯에 실제로 배치된 산업으로 맞춘다 ('Refresh'가 세팅 단계에서 호출).
    //
    // 세팅 단계에서는 "켜진 버튼 = 지금 돌고 있는 산업"이어야 한다. 그래서 교체를 눌러도 값을
    // 미리 바꾸지 않고 여기서만 맞춘다 — 성공하면 슬롯 갱신이 'Refresh'를 불러 새 산업이 켜지고,
    // 실패하면 아무 일도 일어나지 않아 원래 산업이 그대로 남는다. 되돌리는 코드가 필요 없다.
    private void SyncSelectedIndustryToSlot()
    {
        var slot = FindSlot();

        if (slot == null)
        {
            return;
        }

        int index = _industries.IndexOf(slot.Industry);

        if (index < 0)
        {
            return; // 빈 칸(None)이거나 목록에 없는 산업 — 직전 값을 그대로 둔다
        }

        _selectedIndustry = index;
    }

    // 지금 고른 산업. 목록 범위를 벗어났으면 'None' (표시·송신에서 호출).
    private EIndustryType SelectedIndustry
        => _selectedIndustry >= 0 && _selectedIndustry < _industries.Count
            ? _industries[_selectedIndustry]
            : EIndustryType.None;

    // 고른 산업만 밝게 칠하고, 못 하는 산업은 잠근다 (표시 갱신 때 호출).
    //
    // ■ 잠그는 단계가 하나뿐이다
    // 배치 목록에서는 **걸러 보는 수단**이라 늘 눌려야 한다 — 못 하는 산업을 눌러야
    // "이 산업을 다루는 캐릭터가 없다"를 볼 수 있다.
    // 세팅에서는 **갈아 끼우는 수단**이라 배치된 캐릭터의 적성이 0인 산업은 잠근다.
    // 안 잠그면 눌리고 서버가 'NoAptitude'로 거절하는데, 목록 쪽은 이미 걸러 내고 있어
    // 같은 화면이 두 말을 하게 된다.
    //
    // ※ 'Selectable'은 실행 중 'colors.normalColor'로 바탕을 덮어쓰므로
    //   'Image.color'가 아니라 이쪽을 바꾼다.
    //   ⚠️ 잠긴 버튼은 'normalColor'가 아니라 **'disabledColor'로 칠해진다** —
    //   회색을 normalColor에 넣으면 잠근 순간 그 색이 무시되고 직전 색이 남는다.
    private void RefreshIndustryButtons()
    {
        var slot = settingPanel.activeSelf ? FindSlot() : null;

        for (int i = 0; i < industryButtons.Length; i++)
        {
            var button = industryButtons[i];

            if (button == null)
            {
                continue;
            }

            button.interactable = CanSelectIndustry(slot, i);

            // 네 상태를 같은 색으로 덮는다 — 이 버튼의 색은 '고른 산업인가'만 말해야 한다.
            // 기본값(highlighted·selected = 0.961 흰색)을 그대로 두면 **마우스를 올리거나 마지막으로
            // 누른 버튼이라는 이유로** 색이 바뀐다. 특히 'selected'는 EventSystem이 클릭한 버튼을
            // 계속 잡고 있어 **고른 표시가 엉뚱한 버튼에 남는다.**
            var tint = i == _selectedIndustry ? selectedIndustryColor : unselectedIndustryColor;

            var colors = button.colors;
            colors.normalColor      = tint;
            colors.highlightedColor = tint;
            colors.pressedColor     = tint;
            colors.selectedColor    = tint;
            colors.disabledColor    = disabledIndustryColor;
            button.colors           = colors;
        }
    }

    // 이 산업 버튼을 누를 수 있나 ('RefreshIndustryButtons'에서 호출).
    //   slot : 세팅 단계면 다루는 슬롯, 배치 목록 단계면 null(= 늘 누를 수 있다)
    private bool CanSelectIndustry(WorkStationSlotInfo? slot, int index)
    {
        if (slot == null || !IsAssigned(slot))
        {
            return true;
        }

        if (index < 0 || index >= _industries.Count)
        {
            return true; // 버튼과 산업 수가 어긋난 상태다 — 'BindIndustryButtons'가 이미 알렸다
        }

        return _data.GetAptitude(slot.CharacterId, _industries[index]) > 0;
    }

    #endregion

    #region 산업 레벨 선택

    // 산업 레벨 버튼을 레벨 값과 묶는다 (Start에서 호출).
    //
    // ⚠️ 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다
    //   ('BindIndustryButtons'와 같은 이유).
    private void BindIndustryLevelButtons()
    {
        for (int i = 0; i < industryLevelButtons.Length; i++)
        {
            if (industryLevelButtons[i].button == null)
            {
                continue;
            }

            int level = i + 1; // 순서가 곧 레벨
            industryLevelButtons[i].button.onClick.AddListener(() => SelectIndustryLevel(level));
        }
    }

    // 산업 레벨을 고른다 (레벨 버튼 OnClick에 코드로 연결)
    //
    // ⚠️ 산업 버튼과 똑같이 **단계마다 뜻이 다르다.** 캐릭터를 고르는 중이면 배치 요청에 실을
    // 값을 화면이 들고 있고, 세팅 중이면 이미 돌고 있는 슬롯이라 요청을 한 번 보낸다.
    //
    // ■ 누르면 그 레벨의 정보가 함께 펼쳐진다
    // 고르는 순간이 곧 "이 레벨은 무엇이 나오나"를 묻는 순간이다. **같은 탭을 다시 누르면 접는다** —
    // 정보 패널을 따로 여는 버튼을 두지 않으려는 것이고, 접으면 그 자리는 아래 목록이 되돌려 받는다.
    // ※ 세팅 단계에서 같은 레벨을 다시 누르면 요청은 나가지 않지만('RequestIndustryLevelChange')
    //   펼침 상태는 바뀌므로, 요청과 무관하게 버튼 갱신을 한 번 돌린다.
    private void SelectIndustryLevel(int level)
    {
        _industryLevelInfoOpen = level != _selectedIndustryLevel || !_industryLevelInfoOpen;

        if (settingPanel.activeSelf)
        {
            RequestIndustryLevelChange(level);
            RefreshIndustryLevelButtons();

            return;
        }

        _selectedIndustryLevel = level;
        RefreshIndustryLevelButtons();
    }

    // 고른 레벨을 슬롯에 실제로 돌고 있는 레벨로 맞춘다 ('Refresh'가 세팅 단계에서 호출).
    //
    // 산업과 같은 축이다 — 켜진 불빛의 주인은 슬롯이고, 눌러도 값을 미리 바꾸지 않는다
    // ('SyncSelectedIndustryToSlot' 참조).
    private void SyncSelectedIndustryLevelToSlot()
    {
        var slot = FindSlot();

        if (slot == null || slot.IndustryLevel == 0)
        {
            return; // 빈 칸이거나 레벨을 모르는 슬롯 — 직전 값을 그대로 둔다
        }

        _selectedIndustryLevel = slot.IndustryLevel;
    }

    // 레벨 버튼의 이름·잠금·색을 지금 산업에 맞춘다 (표시 갱신 때 호출).
    //
    // ■ 잠긴 레벨도 보인다
    // 숨기면 "더 있다"는 사실이 사라져 특성 트리로 갈 이유를 알 수 없다(해금 규칙과 같다).
    // 열렸는지는 'IndustryLevelTable.UnlockTID'가 열린 해금 목록에 있는가로 판정한다 —
    // **그 TID가 곧 특성 노드**라, 트리에서 찍으면 여기 버튼이 켜진다.
    //
    // ※ 색 규칙은 산업 버튼과 같다(네 상태를 덮고, 잠김만 'disabledColor').
    private void RefreshIndustryLevelButtons()
    {
        EIndustryType industry = SelectedIndustry;
        int           maxLevel = industry == EIndustryType.None ? 0 : GameDataLoader.GetMaxIndustryLevel(industry);

        for (int i = 0; i < industryLevelButtons.Length; i++)
        {
            var entry = industryLevelButtons[i];

            if (entry.button == null)
            {
                continue;
            }

            int level = i + 1;

            // 이 산업에 없는 레벨은 버튼째 치운다 — 산업마다 레벨 수가 달라질 수 있다.
            entry.button.gameObject.SetActive(level <= maxLevel);

            if (level > maxLevel)
            {
                continue;
            }

            bool unlocked = _data.IsUnlocked(GameDataLoader.GetIndustryLevelUnlockTid(industry, level));

            entry.button.interactable = unlocked;

            if (entry.label != null)
            {
                var name = GameDataLoader.TryGetIndustryLevel(industry, level, out var row) && row.Name.Length > 0
                    ? $"Lv{level} {row.Name}"
                    : $"Lv{level}";

                // 펼침 표시는 **고른 레벨에만** 붙는다 — 다른 버튼에 붙이면 누르면 접힌다는 뜻이 된다.
                // ▼ = 지금 접혀 있다(누르면 펼친다) · ▲ = 지금 펼쳐져 있다(누르면 접는다).
                entry.label.text = level == _selectedIndustryLevel
                    ? $"{name} {(_industryLevelInfoOpen ? "▲" : "▼")}"
                    : name;
            }

            var tint   = level == _selectedIndustryLevel ? selectedIndustryColor : unselectedIndustryColor;
            var colors = entry.button.colors;

            colors.normalColor      = tint;
            colors.highlightedColor = tint;
            colors.pressedColor     = tint;
            colors.selectedColor    = tint;
            colors.disabledColor    = disabledIndustryColor;
            entry.button.colors     = colors;
        }

        RefreshIndustryLevelInfo(industry);
    }

    // 고른 산업 레벨의 스펙과 나오는 자원을 줄로 늘어놓는다 ('RefreshIndustryLevelButtons' 끝에서 호출).
    //
    // ■ 서버가 주지 않는다 — 전부 클라 테이블이다
    // 스펙은 'IndustryLevelTable', 자원은 산업별 드롭 테이블이다('GameDataLoader.GetIndustryDrops').
    // 값이 정적이라 **레벨 버튼을 누르는 것만으로** 서버에 묻지 않고 바뀐다.
    //
    // ※ 기준 주기는 'RequiredScore / 1000'초다 — 엑셀이 '초 × 천분율'로 적고 서버는 ×1000만 한다
    //   (서버 'IndustryLevelCatalog.JudgeCostUnits'). **실효 주기와 다르다** — 이쪽은 속도 보정 전 값이고,
    //   보정이 들어간 값은 효율 계산의 '실효 주기' 줄이다.
    private void RefreshIndustryLevelInfo(EIndustryType industry)
    {
        int level = _selectedIndustryLevel;

        // 접혀 있으면 줄을 만들지도 않는다 — 펼칠 때 그려도 늦지 않다(값이 정적이라 계산이 없다).
        if (!_industryLevelInfoOpen
            || industry == EIndustryType.None
            || !GameDataLoader.TryGetIndustryLevel(industry, level, out var spec))
        {
            industryLevelInfoPanel.SetActive(false);
            HideIndustryLevelInfoRowsFrom(0);
            HideIndustryDropRowsFrom(0);

            return;
        }

        industryLevelInfoPanel.SetActive(true);

        var shown = 0;

        // 줄이 두 묶음이라 제목을 끼워 가른다 — 앞은 이 레벨 자체의 제원, 뒤는 거기서 나오는 것이다.
        GetOrCreateIndustryLevelInfoRow(shown++).Bind("■ 작업지 정보", "");
        GetOrCreateIndustryLevelInfoRow(shown++).Bind("기준 주기", $"{spec.RequiredScore / 1000f:0.#}초");
        GetOrCreateIndustryLevelInfoRow(shown++).Bind("판정당 경험치", $"{spec.ExpPerJudge}");
        GetOrCreateIndustryLevelInfoRow(shown++).Bind("■ 나오는 자원", "");
        HideIndustryLevelInfoRowsFrom(shown);

        var drops = GameDataLoader.GetIndustryDrops(industry, level);
        var total = 0;

        foreach (var drop in drops)
        {
            total += drop.Weight;
        }

        // 가중치 합이 0이면 비율을 만들 수 없다 — 테이블이 비었거나 전부 0인 경우다.
        //
        // ※ 판매가를 확률 옆 **칸**에 적는다 — "자주 나오지만 싼 것"과 "드물지만 비싼 것"이
        //   한 줄에서 갈려야 레벨을 올릴지 정할 수 있다. 값은 'ItemTable.BasePrice'다.
        var dropShown = 0;

        foreach (var drop in drops)
        {
            var row   = GetOrCreateIndustryDropRow(dropShown++);
            var rate  = total > 0 ? $"{drop.Weight * 100f / total:0.##}%" : "—";
            var price = GameDataLoader.GetItemPrice(drop.ItemTid);

            row.Bind(GameDataLoader.GetItemName(drop.ItemTid), rate, $"{price:N0} 골드");
            row.SetRarity(GameDataLoader.GetItemRarity(drop.ItemTid));
        }

        HideIndustryDropRowsFrom(dropShown);

        // 방금 켠 줄은 이 프레임 끝까지 프리팹 크기 그대로다('RefreshRows' 끝 주석).
        // ⚠️ 레벨 정보도 **제 스크롤이 없다** — 바깥 스크롤의 내용을 다시 재야 길이가 맞는다.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)bodyScroll.content);
    }

    // 'index'번째 산업 레벨 정보 줄을 켜서 돌려준다. 아직 없으면 그때 만든다 (RefreshIndustryLevelInfo에서 호출).
    private EfficiencyRowView GetOrCreateIndustryLevelInfoRow(int index)
    {
        if (index >= _industryLevelInfoRows.Count)
        {
            _industryLevelInfoRows.Add(Instantiate(efficiencyRowPrefab, industryLevelInfoRowParent));
        }

        var row = _industryLevelInfoRows[index];

        // 켜기 전에 비운다 — 묶음 제목 줄이 앞서 쓰이던 자원 줄의 등급 색을 뒤집어쓰지 않게.
        row.Clear();
        row.gameObject.SetActive(true);

        return row;
    }

    // 이번에 쓰이지 않은 산업 레벨 정보 줄을 비우고 꺼 둔다 (RefreshIndustryLevelInfo에서 호출).
    private void HideIndustryLevelInfoRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _industryLevelInfoRows.Count; i++)
        {
            _industryLevelInfoRows[i].Clear();
            _industryLevelInfoRows[i].gameObject.SetActive(false);
        }
    }

    // 'index'번째 자원 줄을 켜서 돌려준다. 아직 없으면 그때 만든다 (RefreshIndustryLevelInfo에서 호출).
    //
    // ※ **스펙 줄과 부모가 같다.** 자원 줄이 항상 뒤에 오도록 켤 때마다 맨 뒤로 보낸다 —
    //   풀 둘이 같은 부모를 나눠 쓰므로 생성 순서에만 기대면 순서가 뒤집힐 수 있다.
    private IndustryDropRowView GetOrCreateIndustryDropRow(int index)
    {
        if (index >= _industryDropRows.Count)
        {
            _industryDropRows.Add(Instantiate(industryDropRowPrefab, industryLevelInfoRowParent));
        }

        var row = _industryDropRows[index];

        row.transform.SetAsLastSibling();
        row.gameObject.SetActive(true);

        return row;
    }

    // 이번에 쓰이지 않은 자원 줄을 비우고 꺼 둔다 (RefreshIndustryLevelInfo에서 호출).
    private void HideIndustryDropRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _industryDropRows.Count; i++)
        {
            _industryDropRows[i].Clear();
            _industryDropRows[i].gameObject.SetActive(false);
        }
    }

    // 고른 레벨이 지금 산업에서 열려 있지 않으면 기본 레벨로 되돌린다 ('Refresh'가 목록 단계에서 호출).
    //
    // **산업을 바꾸면 잠금이 통째로 뒤집힌다** — 낚시 Lv3을 고른 채 농사로 옮기면 잠긴 레벨을
    // 든 채로 배치를 눌러 서버가 'IndustryLevelLocked'로 거절한다. 누르기 전에 여기서 맞춘다.
    private void ClampSelectedIndustryLevel()
    {
        EIndustryType industry = SelectedIndustry;

        if (industry == EIndustryType.None)
        {
            return;
        }

        if (_selectedIndustryLevel > GameDataLoader.GetMaxIndustryLevel(industry)
            || !_data.IsUnlocked(GameDataLoader.GetIndustryLevelUnlockTid(industry, _selectedIndustryLevel)))
        {
            _selectedIndustryLevel = DefaultIndustryLevel;
        }
    }

    #endregion

    #region 캐릭터 줄

    // 지금 고를 수 있는 캐릭터만 줄로 그린다 ('Refresh'에서 호출).
    // 이 목록은 빈 슬롯일 때만 보이므로 해제 줄은 없다 — 해제는 세팅 쪽 일이다.
    //
    // ⚠️ 걸러 낸 목록을 먼저 만들고 그것을 태운다. 'Characters'를 인덱스 그대로 태우면
    //   걸러 낸 만큼 줄과 캐릭터가 어긋난다.
    private void RefreshRows()
    {
        var characters = _data.Characters;
        var industry   = SelectedIndustry;

        _visible.Clear();

        foreach (CharacterInfo character in characters)
        {
            if (_data.GetAptitude(character.CharacterId, industry) == 0)
            {
                continue;
            }

            // 이미 다른 슬롯에서 일하는 중이면 고를 수 없다 — 판정은 창고 캐릭터 탭과 같은 곳에서 읽는다.
            if (_data.FindSlotIndexOf(character.CharacterId) >= 0)
            {
                continue;
            }

            _visible.Add(character);
        }

        // 고른 산업을 가장 잘하는 캐릭터가 위로 온다 — 순서가 곧 추천이다('CompareRows').
        _visible.Sort(_rowOrder);

        for (int i = 0; i < _visible.Count; i++)
        {
            CharacterInfo character   = _visible[i];
            long          characterId = character.CharacterId;
            var           row         = GetOrCreateRow(i);

            row.gameObject.SetActive(true);
            row.Bind(characterId, _data.GetCharacterName(characterId));
            row.SetRarity(GameDataLoader.GetCharacterRarity(character.CharacterTid));
            row.SetAptitudes(ReadAptitudes(characterId), _selectedIndustry);
            row.SetAssignable(!IsWaiting);
        }

        HideRowsFrom(_visible.Count);

        // 빈 목록은 고장과 구분되지 않는다 — 왜 비었는지만 알린다.
        // 숨긴 캐릭터가 누구인지는 여기서 세지 않는다. 그 답은 창고 캐릭터 탭(적성 스트립·'배' 마크)에 있다.
        emptyText.gameObject.SetActive(_visible.Count == 0);

        // 방금 만든 줄은 아직 프리팹에 저장된 크기 그대로다 — uGUI의 레이아웃 계산은 이 프레임
        // **맨 끝**(Canvas.willRenderCanvases)에 돌기 때문이다. 그 사이 'WidgetPositionLayout.VerifyNoOverflow'가
        // 'LateUpdate'에서 훑고 지나가 "자식이 부모보다 넓다" → "해소됐다"가 왕복으로 찍힌다
        // (판매 목록에서 겪은 그대로 — 'SellCartPresenter.Refresh').
        LayoutRebuilder.ForceRebuildLayoutImmediate(rowParent);
    }

    // 캐릭터 줄의 순서 — 고른 산업의 적성 높은 순 → 등급 높은 순 → 개체 번호 순 (RefreshRows의 정렬 비교자).
    //
    // 산업 버튼이 곧 정렬 기준이라 따로 정렬 UI를 두지 않는다. 버튼을 바꾸면 'SelectIndustry'가 다시 그린다.
    // ※ 개체 번호까지 가서 동점을 없앤다 — 'List.Sort'는 안정 정렬이 아니라 동점이면 다시 그릴 때마다 줄이 바뀐다.
    //   등급은 종류(TID)로 읽는다. enum 'CompareTo'는 박싱이 일어나 숫자로 바꿔 비교한다.
    private int CompareRows(CharacterInfo a, CharacterInfo b)
    {
        EIndustryType industry = SelectedIndustry;

        int byAptitude = _data.GetAptitude(b.CharacterId, industry).CompareTo(_data.GetAptitude(a.CharacterId, industry));

        if (byAptitude != 0)
        {
            return byAptitude;
        }

        int byRarity = ((byte)GameDataLoader.GetCharacterRarity(b.CharacterTid))
            .CompareTo((byte)GameDataLoader.GetCharacterRarity(a.CharacterTid));

        if (byRarity != 0)
        {
            return byRarity;
        }

        return a.CharacterId.CompareTo(b.CharacterId);
    }

    // 'index'번째 줄을 돌려준다. 아직 없으면 그때 만든다 (RefreshRows에서 호출).
    private CharacterStateRowView GetOrCreateRow(int index)
    {
        if (index < _rows.Count)
        {
            return _rows[index];
        }

        CharacterStateRowView row = Instantiate(rowPrefab, rowParent);

        // 줄은 파괴하지 않고 재사용하므로 만들 때 한 번만 구독한다 — 다시 걸면 중복으로 쌓인다.
        row.SetButtonLabel("배치");
        row.AssignClicked += OnRowAssignClicked;

        _rows.Add(row);

        return row;
    }

    // 이 캐릭터의 적성 5종을 산업 목록 순서대로 담아 돌려준다 (줄·카드를 그릴 때 호출).
    // ※ 돌려주는 배열은 재사용되는 하나다 — 받은 쪽이 들고 있으면 안 된다.
    private byte[] ReadAptitudes(long characterId)
    {
        for (int i = 0; i < _aptitudes.Length; i++)
        {
            _aptitudes[i] = _data.GetAptitude(characterId, _industries[i]);
        }

        return _aptitudes;
    }

    // 이번에 쓰이지 않은 줄을 비우고 꺼 둔다 (RefreshRows에서 호출).
    private void HideRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _rows.Count; i++)
        {
            _rows[i].Clear();
            _rows[i].gameObject.SetActive(false);
        }
    }

    #endregion

    #region 표시

    // 제목·산업 버튼·캐릭터 줄을 지금 상태로 맞춘다 (단계 전환 · 데이터 변경)
    //
    // ※ 여기서 단계를 바꾸지 않는다. 어느 단계에 있을지는 사용자의 조작이 정하고,
    //   이 메서드는 그 단계의 내용만 채운다.
    private void Refresh()
    {
        titleText.text = $"슬롯 {_slotIndex} 설정";

        // 세팅 단계의 불빛은 슬롯에서 파생한다 — 칠하기 전에 맞춘다(순서가 뒤집히면 한 프레임 늦는다).
        if (settingPanel.activeSelf)
        {
            SyncSelectedIndustryToSlot();
            SyncSelectedIndustryLevelToSlot();
            RefreshAssignedCard();
            RefreshEquipSlots();

            // 같은 자리를 나눠 쓰므로 둘 중 하나만 그린다 — 꺼진 패널을 그리면 레이아웃만 헛돈다.
            if (_pickingSlot == EEquipSlot.None)
            {
                RefreshEfficiency();
            }
            else
            {
                RefreshEquipPicker();
            }
        }
        else
        {
            // 목록 단계에서는 화면이 값을 들고 있으므로, 산업이 바뀌어 잠긴 레벨이 남지 않았는지 본다.
            ClampSelectedIndustryLevel();
        }

        RefreshIndustryButtons();
        RefreshIndustryLevelButtons();

        if (assignPanel.activeSelf)
        {
            RefreshRows();
        }

        ApplyWaitingLock();
    }

    // 세팅 단계의 캐릭터 카드를 그린다 ('Refresh'가 세팅 단계에서 호출).
    //
    // ※ 산업 교체는 'WorkStationSlotsChanged', 캐릭터 값 변경은 'CharactersChanged'가 'Refresh'를
    //   부르므로 여기서 따로 구독하지 않는다. 강조 칸은 'SyncSelectedIndustryToSlot'이 맞춘 산업이다.
    private void RefreshAssignedCard()
    {
        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            assignedCard.Clear(); // 세팅 단계인데 비었다 — 해제 응답 직전 한 순간뿐이다

            return;
        }

        assignedCard.Bind(slot.CharacterId, _data.GetCharacterName(slot.CharacterId));
        assignedCard.SetRarity(GameDataLoader.GetCharacterRarity(_data.GetCharacterTid(slot.CharacterId)));
        assignedCard.SetAptitudes(ReadAptitudes(slot.CharacterId), _selectedIndustry);
    }

    // 진행도 슬라이더만 매 프레임 움직인다 (Unity 메시지).
    //
    // ■ 시간 진행은 여기 하나다
    // 목록 화면('WorkStationListPresenter.Update')과 같은 규칙이다 — 뷰마다 Update를 두지 않는다.
    // 이 화면은 슬롯 **하나**만 보므로 줄 순회도 없다.
    //
    // ※ 카운트다운은 연출이고 판정은 서버가 한다. 어긋나도 다음 슬롯 동기화가 기준점을 교정한다.
    private void Update()
    {
        if (!settingPanel.activeSelf)
        {
            return;
        }

        var slot = FindSlot();

        progressSlider.value = slot != null && WorkStationProgress.IsRunning(slot)
            ? WorkStationProgress.CalculateProgress(slot)
            : 0f;
    }

    // 효율 계산 줄을 채운다 ('Refresh'가 세팅 단계에서 호출).
    //
    // ■ 서버가 주는 것은 확정 속도 하나다
    // 'CurrentWorkSpeed'는 보정이 전부 적용된 값이고 **내역은 오지 않는다.**
    // 그래서 가산 줄은 클라가 **같은 표를 다시 읽어 되짚는다**(아래 두 헬퍼).
    //
    // ⚠️ **이건 서버 식의 사본이다** — 'User.GetEquipSpeedAdd' · 'User.GetTraitSpeedAdd'와 같은 모양이다.
    //   서버가 가산 내역을 명시 필드로 내려 주면(일감 'T-055') 두 헬퍼를 지우고 받은 값을 그린다.
    //   **표가 갈리면 화면만 틀리고 조용하다** — 속도 특성·장비 테이블을 고칠 때 여기도 본다.
    //
    // ■ 개발용 전역 배수를 역산한다
    // 서버 식은 '기본값 × (1 + Σ가산) × 전역배수'다. 가산을 알게 됐으므로 그 몫을 먼저 걷어내고
    // '현재 ÷ (기본값 × (1 + Σ가산))'으로 배수를 되짚는다 — 예전엔 기본값으로만 나눠서
    //   **특성·장비로 빨라진 몫까지 "전역 배수"로 읽혔다**(2026-09-20 ~ 2026-09-24).
    // 1이 아니면 값 아래에 알리고, 1이면(배포 설정, 일감 'T-004') 문구가 저절로 사라진다.
    private void RefreshEfficiency()
    {
        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            HideEfficiencyRowsFrom(0);

            return;
        }

        byte  aptitude  = _data.GetAptitude(slot.CharacterId, slot.Industry);
        int   baseSpeed = GameDataLoader.GetBaseWorkSpeed(aptitude);
        float cycle     = WorkStationProgress.CalculateCycleSeconds(slot);

        int traitAdd = GetTraitSpeedAdd(slot.Industry);
        int equipAdd = GetEquipSpeedAdd(slot.CharacterId, slot.Industry);

        GetOrCreateEfficiencyRow(0).Bind("적성 기본값", FormatSpeed(baseSpeed));

        // 총합을 값에 적고 내역은 주석 줄에 둔다 — "왜 이만큼인가"가 한 줄 아래에 있어야 한다.
        var addRow = GetOrCreateEfficiencyRow(1);
        addRow.Bind("속도 가산", FormatPermille(traitAdd + equipAdd));

        if (traitAdd != 0 || equipAdd != 0)
        {
            addRow.SetNote($"특성 {FormatPermille(traitAdd)} · 장비 {FormatPermille(equipAdd)}");
        }

        var speedRow = GetOrCreateEfficiencyRow(2);
        speedRow.Bind("현재 작업속도", FormatSpeed(slot.CurrentWorkSpeed));

        // 가산을 걷어낸 기대 속도 — 서버 'WorkSpeed.Resolve'의 가산 단계와 같은 식이다.
        float expected = baseSpeed * Mathf.Max(0, 1000 + traitAdd + equipAdd) / 1000f;

        if (expected > 0f)
        {
            float multiplier = slot.CurrentWorkSpeed / expected;

            // 서버가 천분율 정수로 반올림하므로 딱 1.0이 아닐 수 있다 — 반올림 오차는 배수로 치지 않는다.
            if (Mathf.Abs(multiplier - 1f) > GlobalMultiplierTolerance)
            {
                speedRow.SetNote($"개발용 전역 배수 ×{multiplier:0.00} 적용 중");
            }
        }

        GetOrCreateEfficiencyRow(3).Bind("실효 주기", cycle > 0f ? $"{cycle:0.00}초" : "—");

        HideEfficiencyRowsFrom(EfficiencyRowCount);

        // 캐릭터 줄과 같은 이유 — 방금 켠 줄은 이 프레임 끝까지 프리팹 크기 그대로다('RefreshRows' 끝 주석).
        //
        // ⚠️ **줄이 아니라 스크롤 내용을 다시 잰다.** 효율 줄은 이 화면의 스크롤 **안에** 있어서,
        //   줄만 재면 스크롤 길이(`content`)가 한 프레임 늦게 따라와 끝이 잘려 보인다.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)bodyScroll.content);
    }

    // 천분율 속도를 "2.45배"로 적는다. 0이면 모르는 값이라 "—" (효율 계산에서 호출)
    private static string FormatSpeed(int permille)
        => permille > 0 ? $"{permille / 1000f:0.00}배" : "—";

    // 천분율 가산을 "+25%"로 적는다. 0도 그대로 "+0%"다 — 빈 값은 "못 읽었다"와 구분되지 않는다.
    // ※ 감산 장비·특성이 생길 수 있어 부호를 함께 만든다.
    private static string FormatPermille(int permille)
        => $"{(permille >= 0 ? "+" : "")}{permille / 10f:0.#}%";

    // 이 캐릭터가 낀 장비 중 **이 산업에 붙는** 것들의 가산 합(천분율).
    //
    // ⚠️ 서버 'User.GetEquipSpeedAdd'의 사본이다 (일감 'T-055'가 끝나면 지운다).
    // ※ 산업 'None'은 지정을 안 한 것이 아니라 **어느 산업에나 붙는다**는 뜻이다('Equip.AppliesTo').
    private int GetEquipSpeedAdd(long characterId, EIndustryType industry)
    {
        var sum = 0;

        foreach (var equip in _data.Equips)
        {
            if (equip.EquippedCharacterId != characterId)
            {
                continue;
            }

            if (!GameDataLoader.TryGetEquip(equip.EquipTid, out var row))
            {
                continue;
            }

            if (row.Industry != IndustryType.None && (EIndustryType)(byte)row.Industry != industry)
            {
                continue;
            }

            sum += row.SpeedAddPermille;
        }

        return sum;
    }

    // 찍은 특성 중 **이 산업에 붙는** 속도 가산의 합(천분율).
    //
    // ⚠️ 서버 'User.GetTraitSpeedAdd'의 사본이다 (일감 'T-055'가 끝나면 지운다).
    // ※ 특성을 찍은 기록은 **열린 해금 목록**으로 온다 — 'UserTraitTID'가 곧 'UnlockTID'다.
    private int GetTraitSpeedAdd(EIndustryType industry)
    {
        var sum = 0;

        foreach (var trait in GameDataLoader.UserTraits)
        {
            if (trait.EffectType != UserTraitEffect.SpeedAdd)
            {
                continue;
            }

            if (trait.Industry != IndustryType.None && (EIndustryType)(byte)trait.Industry != industry)
            {
                continue;
            }

            if (!_data.IsUnlocked(trait.UserTraitTID))
            {
                continue;
            }

            sum += trait.EffectValue;
        }

        return sum;
    }

    // 'index'번째 효율 계산 줄을 켜서 돌려준다. 아직 없으면 그때 만든다 (RefreshEfficiency에서 호출).
    private EfficiencyRowView GetOrCreateEfficiencyRow(int index)
    {
        if (index >= _efficiencyRows.Count)
        {
            _efficiencyRows.Add(Instantiate(efficiencyRowPrefab, efficiencyRowParent));
        }

        var row = _efficiencyRows[index];
        row.gameObject.SetActive(true);

        return row;
    }

    // 이번에 쓰이지 않은 효율 계산 줄을 비우고 꺼 둔다 (RefreshEfficiency에서 호출).
    private void HideEfficiencyRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _efficiencyRows.Count; i++)
        {
            _efficiencyRows[i].Clear();
            _efficiencyRows[i].gameObject.SetActive(false);
        }
    }

    // 담당 슬롯의 현재 상태를 찾는다. 서버가 주지 않은 번호면 null (단계 판정·클릭 처리에서 호출)
    private WorkStationSlotInfo? FindSlot()
    {
        foreach (var slot in _data.WorkStationSlots)
        {
            if (slot.SlotIndex == _slotIndex)
            {
                return slot;
            }
        }

        return null; // 눌러 보면 실패 응답이 온다
    }

    // 슬롯이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다 (단계 판정·클릭 처리에서 호출)
    private static bool IsAssigned(WorkStationSlotInfo? slot)
        => slot != null && slot.Industry != EIndustryType.None && slot.CharacterId != 0;

    #endregion

    #region 송신

    // 어느 줄의 배치를 눌렀다 (CharacterStateRowView.AssignClicked 구독)
    private void OnRowAssignClicked(CharacterStateRowView row)
    {
        if (!CanSend())
        {
            return;
        }

        var industry = SelectedIndustry;

        if (industry == EIndustryType.None)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"고른 산업({_selectedIndustry})이 목록 범위(0~{_industries.Count - 1})를 벗어났다.", this);

            return;
        }

        // 서버는 캐릭터 종류(TID)가 아니라 개체 번호를 받는다. 누른 줄이 그 번호를 들고 있다.
        if (row.CharacterId == 0)
        {
            ClientLogger.Error(ClientLogger.UI, "누른 줄에 캐릭터가 묶여 있지 않다 — Bind를 거치지 않았다.", this);

            return;
        }

        Send(industry, row.CharacterId, _selectedIndustryLevel);
        ClientLogger.Info(ClientLogger.Send,
            $"작업슬롯 배치 요청 — 슬롯={_slotIndex}, 산업={industry} Lv{_selectedIndustryLevel}, 캐릭터개체={row.CharacterId}");

        BeginWaiting(PendingRequest.Assign); // 넘어갈지 물러날지는 응답이 정한다
    }

    // 배치된 슬롯의 산업을 갈아 끼운다 ('SelectIndustry'가 세팅 단계에서 호출)
    //
    // ⚠️ 해제 → 배치 2연발로 보내지 않는다. 서버가 정산을 두 번 돌리고, 그 사이 빈 슬롯 상태가
    // 한 번 내려와 칸이 깜빡인다. 서버의 배치는 이미 덮어쓰기라('User.AssignWorkStation')
    // 요청 한 번이면 교체가 끝난다.
    private void RequestIndustryChange(int index)
    {
        if (!CanSend())
        {
            return;
        }

        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            ClientLogger.Error(ClientLogger.UI,
                $"세팅 단계인데 슬롯 {_slotIndex}이 비어 있다 — 단계 판정이 어긋났다.", this);

            return;
        }

        if (index < 0 || index >= _industries.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"누른 산업 버튼({index})이 목록 범위(0~{_industries.Count - 1})를 벗어났다.", this);

            return;
        }

        var industry = _industries[index];

        if (industry == slot.Industry)
        {
            return; // 같은 산업 — 보내 봐야 서버 정산만 한 번 더 돈다
        }

        // 캐릭터는 지금 배치된 그대로 싣는다. 바꾸는 것은 산업뿐이다.
        //
        // ⚠️ 레벨은 **기본값으로 되돌려 보낸다.** 산업이 달라지면 지금 슬롯의 레벨이 새 산업에서
        //   열려 있다는 보장이 없어, 그대로 실으면 교체가 'IndustryLevelLocked'로 거절된다.
        //   바꾼 뒤 원하는 레벨을 다시 고르면 된다(그때는 열린 것만 눌린다).
        Send(industry, slot.CharacterId, DefaultIndustryLevel);
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 산업 교체 요청 — 슬롯={_slotIndex}, 산업={industry}, 캐릭터개체={slot.CharacterId}");

        BeginWaiting(PendingRequest.Replace);
    }

    // 배치된 슬롯의 산업 레벨을 갈아 끼운다 ('SelectIndustryLevel'이 세팅 단계에서 호출)
    //
    // 산업 교체와 **같은 패킷·같은 판단**이다 — 요청 한 번이면 끝나고, 실패해도 제자리다
    // (이미 열린 칸이라 화면이 튕기면 조작이 어렵다 → 'UI 배치 현황.md' 3장).
    private void RequestIndustryLevelChange(int level)
    {
        if (!CanSend())
        {
            return;
        }

        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            ClientLogger.Error(ClientLogger.UI,
                $"세팅 단계인데 슬롯 {_slotIndex}이 비어 있다 — 단계 판정이 어긋났다.", this);

            return;
        }

        if (level == slot.IndustryLevel)
        {
            return; // 같은 레벨 — 보내 봐야 서버 정산만 한 번 더 돈다
        }

        // 산업·캐릭터는 지금 배치된 그대로 싣는다. 바꾸는 것은 레벨뿐이다.
        Send(slot.Industry, slot.CharacterId, level);
        ClientLogger.Info(ClientLogger.Send,
            $"작업슬롯 레벨 교체 요청 — 슬롯={_slotIndex}, 산업={slot.Industry} Lv{level}, 캐릭터개체={slot.CharacterId}");

        BeginWaiting(PendingRequest.Replace);
    }

    // 카드의 해제를 눌렀다 (assignedCard.AssignClicked 구독)
    //
    // ⚠️ **끼고 있던 장비를 먼저 뺀다.** 서버의 배치 해제는 착용을 건드리지 않아
    //    ('User.AssignWorkStation'), 그냥 빼면 일하지도 않는 캐릭터가 장비를 붙들고 있는다 —
    //    창고에서 '배' 마크만 붙은 채 남아 다른 캐릭터에 끼울 때까지 돌아오지 않는다.
    private void OnAssignedCardClicked(CharacterStateRowView card)
    {
        if (!CanSend())
        {
            return;
        }

        UnequipAllWorn();

        Send(EIndustryType.None, 0, DefaultIndustryLevel); // 산업 None·캐릭터 0 = 해제 (레벨 주의 — 'Send' 주석)
        ClientLogger.Info(ClientLogger.Send, $"작업슬롯 해제 요청 — 슬롯={_slotIndex}");

        BeginWaiting(PendingRequest.Unassign);
    }

    // 배치된 캐릭터가 낀 장비를 칸마다 하나씩 뺀다 (배치 해제 직전에 'OnAssignedCardClicked'가 호출).
    //
    // 한 세션이 보낸 패킷은 보낸 순서대로 처리되므로, 해제를 먼저 보내면 캐릭터가 슬롯에서
    // 빠지기 전에 장비가 창고로 돌아간다.
    //
    // ※ 응답('S_EquipResponse')은 기다리지 않는다 — 대기는 뒤이어 보내는 배치 해제 하나만 연다.
    //   먼저 도착하는 장비 응답은 'OnEquipCompleted'가 _pending(Unassign)을 보고 흘려 보내고,
    //   창고·장비 칸 표시는 'EquipsChanged'(OnEquipsChanged)가 따로 갱신한다.
    private void UnequipAllWorn()
    {
        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            return;
        }

        for (int i = 0; i < EquipSlotCount; i++)
        {
            var part = (EEquipSlot)(i + 1);

            // 빈 칸까지 보내면 서버가 'EquipSlotEmpty'로 거절한다 — 낀 것만 보낸다.
            if (FindWornEquip(part) == null)
            {
                continue;
            }

            _network.Send(new C_UnequipRequest { CharacterId = slot.CharacterId, Slot = part });
            ClientLogger.Info(ClientLogger.Send,
                $"배치 해제에 딸린 장비 해제 요청 — 슬롯={_slotIndex}, 캐릭터개체={slot.CharacterId}, 칸={part}");
        }
    }

    // 응답이 올 때까지 배치·해제 버튼을 잠그고 대기를 연다 (요청을 보낸 뒤 호출)
    // ※ 로딩 표시·무응답 감시·알림은 ServerWaitManager가 공통으로 처리한다. 무응답이면 5초 뒤
    //   타임아웃돼 onClosed(OnWaitClosed)가 잠금을 푼다 — 예전의 "무응답 시 영구 잠김"이 사라진다.
    private void BeginWaiting(PendingRequest request)
    {
        _pending    = request;
        _waitHandle = _wait.Begin(WaitLabel(request), onClosed: OnWaitClosed);
        ApplyWaitingLock();
    }

    // 로딩·타임아웃 문구에 쓸 요청 이름 ('BeginWaiting'에서 호출)
    private static string WaitLabel(PendingRequest request)
        => request switch
        {
            PendingRequest.Assign  => "작업슬롯 배치",
            PendingRequest.Replace => "작업슬롯 산업 교체",
            PendingRequest.Equip   => "장비 장착",
            PendingRequest.Unequip => "장비 해제",
            _                      => "작업슬롯 해제",
        };

    // 대기가 끝났다(성공·실패·타임아웃 공통) — 잠금을 푼다 (ServerWaitManager.Begin의 onClosed)
    private void OnWaitClosed()
    {
        _pending    = PendingRequest.None;
        _waitHandle = null;
        ApplyWaitingLock();
    }

    // 응답이 왔다 — 성공이면 다음 단계로, 실패면 사유를 알리고 슬롯 목록으로 물러난다
    // (PlayerDataModel.WorkStationAssignCompleted 구독).
    //
    // 실패는 대개 아직 열리지 않은 슬롯이다. 그 칸에서는 배치도 해제도 할 수 없으니
    // 화면에 남겨 둘 이유가 없다. 사유('EResultCode')는 'ResultMessages'로 문구를 만들어 알림에 띄운다.
    //
    // ⚠️ 교체만 예외로 물러나지 않는다 — 아래 분기 참조.
    private void OnAssignCompleted(bool success, EResultCode code)
    {
        // Succeed/Fail이 onClosed(OnWaitClosed)를 통해 _pending을 지우므로, 그 전에 종류를 붙잡는다.
        var requested = _pending;

        if (requested == PendingRequest.None)
        {
            return; // 이 화면이 보낸 요청이 아니다(타임아웃으로 이미 닫혔거나 남의 응답)
        }

        if (!success)
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));

            // 교체 실패는 물러나지 않는다. 이미 열린 칸에서 나는 거절(적성 0 등)이라 그 칸에서
            // 할 일이 남아 있고, 산업을 눌러 봤다는 이유로 화면이 튕기면 조작이 어렵다.
            // 켜진 버튼은 손대지 않아도 원래 산업 그대로다('SyncSelectedIndustryToSlot').
            if (requested == PendingRequest.Replace)
            {
                ClientLogger.Warn(ClientLogger.UI,
                    $"슬롯 {_slotIndex} 산업 교체가 거절됐다 — 세팅 화면에 남는다.", this);

                return;
            }

            ClientLogger.Warn(ClientLogger.UI,
                $"슬롯 {_slotIndex} 변경이 거절돼 슬롯 목록으로 돌아간다 (열리지 않은 슬롯일 수 있다).", this);
            BackToSlotList();

            return;
        }

        _waitHandle?.Succeed();

        // 해제만 캐릭터 목록으로 돌아간다. 배치·교체는 둘 다 세팅에 머문다.
        if (requested == PendingRequest.Unassign)
        {
            ShowAssignList();
        }
        else
        {
            ShowSetting();
        }
    }

    // 기다리는 동안 배치·해제만 잠근다. 뒤로가기는 잠그지 않는다 — 나갈 길은 늘 열려 있어야 한다
    private void ApplyWaitingLock()
    {
        assignedCard.SetAssignable(!IsWaiting);

        foreach (var row in _rows)
        {
            row.SetAssignable(!IsWaiting);
        }

        // 장비 칸은 배치된 캐릭터가 있어야 누를 수 있다 — 빈 슬롯에는 끼울 대상이 없다.
        bool canTouchEquip = !IsWaiting && IsAssigned(FindSlot());

        foreach (var entry in equipSlotButtons)
        {
            if (entry.button != null)
            {
                entry.button.interactable = canTouchEquip;
            }
        }

        foreach (var row in _equipRows)
        {
            row.SetPickable(!IsWaiting);
        }

        equipUnequipButton.interactable = canTouchEquip && FindWornEquip(_pickingSlot) != null;
    }

    // 보낼 수 있는 상태인가 (배치·해제 클릭에서 호출)
    private bool CanSend()
    {
        // 버튼을 잠가 두지만 잠금이 늦게 반영되는 경로가 있을 수 있어 여기서 한 번 더 막는다.
        // 같은 슬롯에 두 번 보내면 응답도 두 번 와서 단계가 엉뚱하게 튄다.
        if (IsWaiting)
        {
            return false;
        }

        if (_slotIndex < 0)
        {
            ClientLogger.Error(ClientLogger.UI, "다룰 슬롯이 정해지지 않았다 — Open()을 거치지 않고 열렸다.", this);

            return false;
        }

        // 로그인 전에 보내면 서버가 User를 못 찾아 조용히 버린다 — 클라 입장에선 응답도 오류도
        // 없어서 "눌렀는데 아무 일도 안 일어난다"로만 보인다. 보내기 전에 여기서 끊고 이유를 남긴다.
        if (!_data.IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Send, "작업슬롯 요청을 보내지 않았다 — 로그인이 먼저다(서버가 응답 없이 버린다)");

            return false;
        }

        return true;
    }

    // 담당 슬롯의 배치 요청을 보낸다. 산업 None·캐릭터 0으로 주면 해제다 (클릭 처리에서 호출)
    //
    // ⚠️ **해제에는 기본 레벨을 싣는다.** 서버는 해제도 같은 경로로 받는데, 산업이 None이면
    //   'IsIndustryLevelUnlocked(None, 3)'이 거짓이라 **해제가 'IndustryLevelLocked'로 거절된다.**
    //   기본 레벨은 산업과 무관하게 늘 열려 있다('User.IsIndustryLevelUnlocked').
    private void Send(EIndustryType industry, long characterId, int industryLevel)
    {
        _network.Send(new C_WorkStationAssignRequest
        {
            SlotIndex     = _slotIndex,
            Industry      = industry,
            CharacterId   = characterId,
            IndustryLevel = (byte)industryLevel
        });
    }

    #endregion

    #region 장비 장착 · 해제

    // 장비 칸 버튼을 칸 순서와 묶는다 (Start에서 호출).
    // 칸 개수가 'EEquipSlot'의 칸 수와 다르면 조용히 어긋난다 — 그래서 여기서 먼저 알린다.
    private void BindEquipSlotButtons()
    {
        if (equipSlotButtons.Length != EquipSlotCount)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"장비 칸이 {equipSlotButtons.Length}개인데 칸은 {EquipSlotCount}개다. " +
                $"인스펙터의 칸 목록을 순서(무기·장신구1·장신구2·보석)대로 채울 것.", this);
        }

        for (int i = 0; i < equipSlotButtons.Length; i++)
        {
            if (equipSlotButtons[i].button == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            equipSlotButtons[i].button.onClick.AddListener(() => OnEquipSlotClicked(index));
        }
    }

    // 장비 칸 4개를 그린다 ('Refresh'가 세팅 단계에서 호출).
    //
    // ※ 한 칸에 하나다 — 서버가 (캐릭터, 칸)을 유일키로 들고 있어('User._worn') 같은 칸이 둘 나올 수 없다.
    private void RefreshEquipSlots()
    {
        for (int i = 0; i < equipSlotButtons.Length; i++)
        {
            var entry = equipSlotButtons[i];
            var part  = (EEquipSlot)(i + 1);
            var worn  = FindWornEquip(part);

            if (entry.partLabel != null)
            {
                entry.partLabel.text = EquipLabel.GetSlotName(part);
            }

            // 고르는 중인 칸은 **눌러 둔 것처럼 어둡게** 만든다 — 버튼이 이미 가진 색 전이를 쓴다.
            //
            // ※ 네 상태를 같은 색으로 덮는 이유는 산업 버튼과 같다('RefreshIndustryButtons' 주석) —
            //   특히 'selected'는 EventSystem이 마지막으로 누른 버튼을 계속 잡고 있어
            //   **고른 표시가 엉뚱한 칸에 남는다.**
            if (entry.button != null)
            {
                var tint   = part == _pickingSlot ? pickingEquipSlotColor : Color.white;
                var colors = entry.button.colors;

                colors.normalColor      = tint;
                colors.highlightedColor = tint;
                colors.pressedColor     = tint;
                colors.selectedColor    = tint;
                colors.disabledColor    = disabledIndustryColor;
                entry.button.colors     = colors;
            }

            // 빈 칸은 **아이콘 네모가 말한다** — 글자를 겹쳐 적지 않는다(2026-09-24 사용자 결정).
            if (entry.nameLabel != null)
            {
                entry.nameLabel.text = worn != null ? GameDataLoader.GetEquipName(worn.EquipTid) : "";
            }

            // 효과는 창고 장비 탭과 **같은 출처**다('EquipLabel') — 빈 칸이면 빈 문자열이라 줄이 사라진다.
            if (entry.effectLabel != null)
            {
                entry.effectLabel.text = worn != null ? EquipLabel.GetEffectText(worn.EquipTid) : "";
            }

            // 창고 칸·캐릭터 카드와 같은 표('RarityPalette')다. 빈 칸은 회색('Unknown').
            if (entry.background != null)
            {
                entry.background.color = worn != null
                    ? RarityPalette.Get(GameDataLoader.GetEquipRarity(worn.EquipTid))
                    : RarityPalette.Unknown;
            }
        }
    }

    // 장비 칸을 눌렀다 (칸 버튼 OnClick에 코드로 연결).
    //
    // ※ 같은 칸을 다시 누르면 접는다 — 여는 버튼이 곧 닫는 버튼이다.
    private void OnEquipSlotClicked(int index)
    {
        var part = (EEquipSlot)(index + 1);

        if (_pickingSlot == part)
        {
            CloseEquipPicker();

            return;
        }

        OpenEquipPicker(part);
    }

    // 이 칸에 낄 장비를 고르는 목록을 연다.
    //
    // ■ 고르는 동안에는 **장비 칸 넷만 남긴다**
    // 위(산업 레벨 정보 · 캐릭터 카드)와 아래(효율 계산)를 모두 접는다. 그러면 칸 넷이
    // **고정 머리 바로 아래로 올라오고** 목록이 그 아래에 바로 붙어, 굴리지 않고도 둘 다 보인다.
    // 장비가 늘어날수록 목록이 길어지므로 **자리를 최대한 목록에 준다.**
    private void OpenEquipPicker(EEquipSlot part)
    {
        _pickingSlot = part;

        _industryLevelInfoOpen = false;   // 레벨 정보도 접는다 (버튼 라벨이 ▼로 바뀐다)
        characterLabel.SetActive(false);
        assignedCard.gameObject.SetActive(false);

        efficiencyLabel.SetActive(false);
        progressPanel.SetActive(false);
        efficiencyPanel.SetActive(false);
        equipPickerPanel.SetActive(true);

        equipPickerTitle.text = $"{EquipLabel.GetSlotName(part)} 고르기";

        // 칸을 옮길 때마다 **그 슬롯의 산업**으로 되돌린다 — 앞 칸에서 건 필터가 남아 있으면
        // **다음 칸이 이유 없이 비어 보인다.** 이 캐릭터가 지금 하는 일이 곧 기본값이다.
        _equipFilter = SelectedIndustry;

        RefreshIndustryLevelButtons();  // 접은 레벨 정보와 ▼ 표시를 반영한다
        RefreshEquipFilterButtons();
        RefreshEquipSlots();  // 누른 칸을 어둡게 만든다
        RefreshEquipPicker();
        ApplyWaitingLock();   // [해제]는 낀 것이 있을 때만 눌린다

        ScrollToTop();
    }

    // 고르기를 접고 효율 계산으로 돌아간다 (닫기 · 단계 전환 · 장착 성공).
    private void CloseEquipPicker()
    {
        _pickingSlot = EEquipSlot.None;

        equipPickerPanel.SetActive(false);
        characterLabel.SetActive(true);
        assignedCard.gameObject.SetActive(true);
        efficiencyLabel.SetActive(true);
        progressPanel.SetActive(true);
        efficiencyPanel.SetActive(true);

        HideEquipRowsFrom(0);

        // ※ **레벨 정보는 접힌 채로 둔다.** 고르려고 접은 것을 되돌리면 사용자가 접어 둔 것까지
        //   덮어쓴다 — 보고 싶으면 레벨 탭을 누르면 된다(그게 펼침 토글이다).

        // 세팅 단계에 있을 때만 그린다 — 목록 단계에서는 이 패널이 통째로 꺼져 있다.
        if (settingPanel.activeSelf)
        {
            RefreshEquipSlots();  // 어둡게 해 둔 칸을 되돌린다
            RefreshEfficiency();
            ScrollToTop();        // 접었던 것이 돌아와 길이가 바뀌었다
        }
    }

    // 스크롤을 맨 위로 되돌린다 (장비 목록을 열고 닫을 때 호출).
    //
    // ⚠️ **다시 재고 나서 옮긴다** — 켜고 끈 패널의 크기가 아직 반영되지 않았으면
    // 옮길 길이 자체가 정해지지 않아 엉뚱한 곳에 선다.
    private void ScrollToTop()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)bodyScroll.content);
        bodyScroll.verticalNormalizedPosition = 1f;
    }

    // 고르는 중인 칸에 낄 수 있는 장비를 줄로 늘어놓는다 (칸 클릭 · 'EquipsChanged').
    //
    // ⚠️ **여기서 거르는 것은 표시용이다** — 진짜 거절은 서버가 한다('EquipKindMismatch').
    //
    // ※ **끼워져 있는 개체는 빼고 창고에 있는 것만 낸다**(2026-09-24 사용자 결정).
    //   서버는 남이 낀 장비도 옮겨 주지만, 그 동선을 목록에 두면 착용자를 적어 줘야 하고
    //   **같은 장비가 칸에도 목록에도 있어** 무엇이 창고에 남았는지가 흐려진다.
    //   다른 캐릭터에게서 가져오려면 **그쪽에서 먼저 해제한다.**
    private void RefreshEquipPicker()
    {
        if (_pickingSlot == EEquipSlot.None)
        {
            return;
        }

        _pickCandidates.Clear();

        foreach (var equip in _data.Equips)
        {
            // 테이블에 없는 개체는 이름도 효과도 못 그린다 — 고를 대상에서 뺀다.
            if (!GameDataLoader.TryGetEquip(equip.EquipTid, out var row))
            {
                continue;
            }

            if (!EquipLabel.CanEquip(row.EquipKind, _pickingSlot))
            {
                continue;
            }

            if (!PassesEquipFilter(row))
            {
                continue;
            }

            // 어디든 끼워져 있으면 뺀다 — 이 칸의 것은 [해제]로, 남의 것은 그 캐릭터에서 뺀다.
            if (equip.EquippedCharacterId != 0L)
            {
                continue;
            }

            _pickCandidates.Add(equip);
        }

        _pickCandidates.Sort(CompareEquips);

        for (int i = 0; i < _pickCandidates.Count; i++)
        {
            var equip = _pickCandidates[i];
            GameDataLoader.TryGetEquip(equip.EquipTid, out var row);  // 위에서 이미 걸렀다

            var view = GetOrCreateEquipRow(i);

            view.Bind(equip.EquipId, row.Name, EquipLabel.GetEffectText(equip.EquipTid));
            view.SetRarity(row.GlobalRarity);
            view.SetPickable(!IsWaiting);
        }

        var shown = _pickCandidates.Count;

        HideEquipRowsFrom(shown);

        // 빈 목록은 고장과 구분되지 않는다 — 안내 한 줄을 띄운다(캐릭터 목록과 같은 규칙).
        equipEmptyText.gameObject.SetActive(shown == 0);

        // 방금 켠 줄은 이 프레임 끝까지 프리팹 크기 그대로다('RefreshRows' 끝 주석).
        LayoutRebuilder.ForceRebuildLayoutImmediate(equipRowParent);
    }

    // 이 장비가 지금 걸린 산업 필터를 통과하나 (RefreshEquipPicker에서 호출).
    //
    // **전 산업 장비(`Industry == None`)는 어느 필터에서도 보인다** — 어느 산업에나 붙으니 거를 이유가 없다.
    //
    // ⚠️ 'None'이 두 군데서 다른 뜻이다 — 장비의 'None'은 *전 산업*, 필터의 'None'은 *아직 산업을 모른다*이다.
    //   필터 쪽 'None'은 화면에서 고를 수 없고(버튼이 5개다), 그때는 거르지 않고 다 낸다.
    private bool PassesEquipFilter(EquipTableRow row)
    {
        if (_equipFilter == EIndustryType.None)
        {
            return true;
        }

        return row.Industry == IndustryType.None || (EIndustryType)(byte)row.Industry == _equipFilter;
    }

    // 목록 정렬 — **효과 수치 높은 순 → 전 산업 먼저 → 종류(TID) → 개체 번호**.
    //
    // ■ 등급이 아니라 **수치**로 줄 세운다
    // 지금 고른 필터 안에서는 "얼마나 빨라지나"가 유일한 비교축이다. 등급으로 세우면 같은 등급 안에서
    // **더 느린 것이 위로 올라올 수 있다** — 산업 한정이 전 산업보다 수치가 크기 때문이다.
    // 필터가 이미 '그 산업 + 전 산업'만 남겨 뒀으므로, 남은 줄은 전부 **이 산업에 실제로 붙는 값**이다.
    //
    // ■ 수치가 같으면 **전 산업이 앞이다**
    // 같은 값이면 어느 산업에나 붙는 쪽이 낫다 — 슬롯의 산업을 바꿔도 그대로 남는다.
    //
    // ※ 개체 번호까지 가서 **동점을 없앤다** — 'List.Sort'는 안정 정렬이 아니라 동점이면
    //   다시 그릴 때마다 줄 순서가 바뀐다(배치 목록 'CompareRows'와 같은 이유).
    private static int CompareEquips(EquipInfo a, EquipInfo b)
    {
        GameDataLoader.TryGetEquip(a.EquipTid, out var rowA);
        GameDataLoader.TryGetEquip(b.EquipTid, out var rowB);

        int bySpeed = rowB.SpeedAddPermille.CompareTo(rowA.SpeedAddPermille);
        if (bySpeed != 0)
        {
            return bySpeed;
        }

        // 전 산업(Industry == None)이 앞 — false(0) < true(1)이므로 b를 앞에 둔다.
        int byScope = (rowB.Industry == IndustryType.None).CompareTo(rowA.Industry == IndustryType.None);
        if (byScope != 0)
        {
            return byScope;
        }

        int byTid = a.EquipTid.CompareTo(b.EquipTid);

        return byTid != 0 ? byTid : a.EquipId.CompareTo(b.EquipId);
    }

    // 산업 필터 버튼을 눌렀다 (필터 버튼 OnClick에 코드로 연결).
    //   index : 0이면 모두 보기, 1부터는 산업 버튼 줄과 같은 순서
    private void OnEquipFilterClicked(int index)
    {
        var filter = index >= 0 && index < _industries.Count
            ? _industries[index]
            : EIndustryType.None;

        if (filter == _equipFilter)
        {
            return;
        }

        _equipFilter = filter;

        RefreshEquipFilterButtons();
        RefreshEquipPicker();
    }

    // 필터 버튼을 목록과 묶는다 (Start에서 호출). 개수가 다르면 조용히 어긋나므로 먼저 알린다.
    private void BindEquipFilterButtons()
    {
        if (equipFilterButtons.Length != EquipFilterCount)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"장비 필터가 {equipFilterButtons.Length}개인데 필터는 {EquipFilterCount}개다. " +
                $"인스펙터를 순서(농사·낚시·채굴·벌목·사냥)대로 채울 것.", this);
        }

        for (int i = 0; i < equipFilterButtons.Length; i++)
        {
            if (equipFilterButtons[i] == null)
            {
                continue;
            }

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            equipFilterButtons[i].onClick.AddListener(() => OnEquipFilterClicked(index));
        }
    }

    // 고른 필터만 노랑으로 칠한다 (고르기를 열 때 · 필터를 누를 때).
    //
    // ※ 네 상태를 같은 색으로 덮는 이유는 산업 버튼과 같다('RefreshIndustryButtons' 주석).
    private void RefreshEquipFilterButtons()
    {
        for (int i = 0; i < equipFilterButtons.Length; i++)
        {
            var button = equipFilterButtons[i];

            if (button == null)
            {
                continue;
            }

            var filter = i >= 0 && i < _industries.Count
                ? _industries[i]
                : EIndustryType.None;

            var tint = filter == _equipFilter ? selectedIndustryColor : unselectedIndustryColor;

            var colors = button.colors;
            colors.normalColor      = tint;
            colors.highlightedColor = tint;
            colors.pressedColor     = tint;
            colors.selectedColor    = tint;
            colors.disabledColor    = disabledIndustryColor;
            button.colors           = colors;
        }
    }

    // 고른 장비를 지금 칸에 끼운다 (장비 줄 클릭).
    //
    // ※ 목록에는 창고에 있는 것만 있으므로 **여기서 오는 장비는 늘 비어 있는 개체다.**
    //   그래도 클라가 해제를 먼저 보내지는 않는다 — 서버가 한 번에 처리한다.
    private void OnEquipRowClicked(EquipPickRowView row)
    {
        if (!TryGetEquipTarget(out var characterId))
        {
            return;
        }

        _network.Send(new C_EquipRequest { CharacterId = characterId, EquipId = row.EquipId, Slot = _pickingSlot });
        ClientLogger.Info(ClientLogger.Send,
            $"장비 장착 요청 — 슬롯={_slotIndex}, 캐릭터개체={characterId}, 장비개체={row.EquipId}, 칸={_pickingSlot}");

        BeginWaiting(PendingRequest.Equip);
    }

    // 고르는 칸의 장비를 뺀다 ([해제] 버튼).
    private void RequestUnequip()
    {
        if (!TryGetEquipTarget(out var characterId))
        {
            return;
        }

        _network.Send(new C_UnequipRequest { CharacterId = characterId, Slot = _pickingSlot });
        ClientLogger.Info(ClientLogger.Send, $"장비 해제 요청 — 슬롯={_slotIndex}, 캐릭터개체={characterId}, 칸={_pickingSlot}");

        BeginWaiting(PendingRequest.Unequip);
    }

    // 장비 요청을 보낼 수 있는지 보고 대상 캐릭터를 얻는다 (장착·해제 공통).
    private bool TryGetEquipTarget(out long characterId)
    {
        characterId = 0L;

        if (!CanSend())
        {
            return false;
        }

        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            ClientLogger.Error(ClientLogger.UI,
                $"세팅 단계인데 슬롯 {_slotIndex}이 비어 있다 — 단계 판정이 어긋났다.", this);

            return false;
        }

        if (_pickingSlot == EEquipSlot.None)
        {
            ClientLogger.Error(ClientLogger.UI, "고르는 칸이 정해지지 않았는데 장비 요청이 나갔다.", this);

            return false;
        }

        characterId = slot.CharacterId;

        return true;
    }

    // 보유 장비가 바뀌었다 — 지급·장착·해제·남에게 밀려난 것까지 전부 (PlayerDataModel.EquipsChanged 구독).
    //
    // ※ 창고 장비 탭도 같은 이벤트로 함께 갱신된다 — '배' 마크가 여기서 켜지고 꺼진다.
    private void OnEquipsChanged()
    {
        if (!settingPanel.activeSelf)
        {
            return;
        }

        RefreshEquipSlots();
        RefreshEquipPicker();
        ApplyWaitingLock();
    }

    // 장착·해제 응답이 왔다 (PlayerDataModel.EquipCompleted 구독).
    //
    // 실패해도 물러나지 않는다 — 산업 교체 실패와 같은 판단이다. 그 칸에서 할 일이 남아 있고,
    // 장비 하나를 잘못 눌렀다고 슬롯 목록까지 튕기면 조작이 끊긴다.
    private void OnEquipCompleted(bool success, EResultCode code)
    {
        // Succeed/Fail이 onClosed(OnWaitClosed)를 통해 _pending을 지우므로, 그 전에 종류를 붙잡는다.
        var requested = _pending;

        if (requested != PendingRequest.Equip && requested != PendingRequest.Unequip)
        {
            return; // 이 화면이 보낸 요청이 아니다(타임아웃으로 이미 닫혔거나 남의 응답)
        }

        if (!success)
        {
            _waitHandle?.Fail(ResultMessages.ToText(code));
            ClientLogger.Warn(ClientLogger.UI, $"슬롯 {_slotIndex} 장비 요청이 거절됐다 — 세팅 화면에 남는다.", this);

            return;
        }

        _waitHandle?.Succeed();

        // 성공하면 고르기를 접는다 — 이제 볼 것은 바뀐 속도(효율 계산)다.
        CloseEquipPicker();
    }

    // 이 칸에 낀 장비. 없거나 슬롯이 비었으면 null (칸 그리기 · [해제] 잠금에서 호출).
    private EquipInfo? FindWornEquip(EEquipSlot part)
    {
        if (part == EEquipSlot.None)
        {
            return null;
        }

        var slot = FindSlot();

        if (slot == null || !IsAssigned(slot))
        {
            return null;
        }

        foreach (var equip in _data.Equips)
        {
            if (equip.EquippedCharacterId == slot.CharacterId && equip.EquippedSlot == part)
            {
                return equip;
            }
        }

        return null;
    }

    // 'index'번째 장비 줄을 켜서 돌려준다. 아직 없으면 그때 만든다 (RefreshEquipPicker에서 호출).
    private EquipPickRowView GetOrCreateEquipRow(int index)
    {
        if (index >= _equipRows.Count)
        {
            var created = Instantiate(equipRowPrefab, equipRowParent);
            created.PickClicked += OnEquipRowClicked;
            _equipRows.Add(created);
        }

        var row = _equipRows[index];
        row.gameObject.SetActive(true);

        return row;
    }

    // 이번에 쓰이지 않은 장비 줄을 비우고 꺼 둔다 (RefreshEquipPicker · 닫기에서 호출).
    private void HideEquipRowsFrom(int startIndex)
    {
        for (int i = startIndex; i < _equipRows.Count; i++)
        {
            _equipRows[i].Clear();
            _equipRows[i].gameObject.SetActive(false);
        }
    }

    #endregion
}
