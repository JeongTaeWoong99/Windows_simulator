using System;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 한 명의 줄. 초상화 · 이름 · 종족 · 적성 5칸과 버튼 하나를 갖는다.
//
// 눌리면 'AssignClicked'만 쏜다 — 슬롯 번호도 고른 산업도 로그인 여부도 이 줄은 모른다.
// 이름·적성처럼 조회가 필요한 값은 'Bind'·'SetAptitudes'로 완성된 값을 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 두 곳에서 같은 프리팹을 쓴다
// 2단계 캐릭터 목록의 줄(버튼 "배치")과 3단계 세팅의 배치된 캐릭터 카드(버튼 "해제")가 같은 모양이다.
// 모양을 따로 만들면 한쪽만 고쳐져 같은 캐릭터가 두 화면에서 다르게 보인다.
// 버튼의 뜻은 이 줄이 아니라 구독한 쪽이 정한다 — 라벨만 'SetButtonLabel'로 받는다.
//
// ■ 이 줄은 적성으로 판단하지 않는다
// 못 하는 캐릭터는 목록에서 걸러지므로 여기까지 오지 않는다('WorkStationSelectPresenter.RefreshRows').
// 받은 5칸을 그리기만 한다 — 관문이 둘이면 다음에 보는 사람이 어느 쪽이 진짜인지 알 수 없다.
//
// ■ 파괴하지 않고 풀로 되돌린다
// 목록을 오갈 때마다 만들고 부수면 상주 앱에서 GC가 쌓인다. 남는 줄은 'Clear()' 후 꺼 둔다.
public class CharacterStateRowView : MonoBehaviour
{
    [CenterHeader("캐릭터")]
    // 줄 바탕을 등급 색으로 칠한다('SetRarity'). 창고 칸('SlotView')과 같은 표('RarityPalette')를 쓴다.
    // 🎨 등급 테두리 스프라이트가 나오면 색 대신 여기에 스프라이트를 넣는다.
    [SerializeField, Tooltip("줄 바탕 — 프리팹 루트의 Image. 등급 색으로 칠해진다")]
    private Image backgroundImage = null!;

    // 임시 — 초상화 스프라이트가 없어 흰 네모만 둔다. 엑셀(CharacterTable) · 기획 · 리소스가 나오면
    // 여기에 캐릭터별 sprite를 넣는다(일감 'T-054'). 그때까지 코드는 이 필드를 건드리지 않는다.
    [SerializeField, Tooltip("캐릭터 초상화 (임시 — 흰 네모)")]
    private Image portraitImage = null!;

    [SerializeField, Tooltip("캐릭터 이름")]
    private TMP_Text nameText = null!;

    // 임시 — 종족은 데이터에 없다('CharacterTable'에 컬럼이 없다). 프리팹에 "종족 추가 예정"이 적혀 있고,
    // 엑셀 · 기획이 나오면 'Bind'가 값을 채운다(일감 'T-054').
    [SerializeField, Tooltip("종족 (임시 — '종족 추가 예정')")]
    private TMP_Text raceText = null!;

    // ⚠️ **칸의 순서가 곧 산업이다** — 농사 · 낚시 · 채굴 · 벌목 · 사냥. 바로 위 산업 탭과 같은 순서라
    //   위치만으로 읽힌다. **인스펙터에서 순서를 섞으면 값이 조용히 다른 산업 칸에 들어간다.**
    [CenterHeader("적성 5칸")]
    [SerializeField, NonReorderable, Tooltip("적성 칸 바탕 5개. 순서 = 농사·낚시·채굴·벌목·사냥")]
    private Image[] aptitudeCellImages = new Image[0];

    [SerializeField, NonReorderable, Tooltip("적성 값 5개. 순서 = 농사·낚시·채굴·벌목·사냥")]
    private TMP_Text[] aptitudeValueTexts = new TMP_Text[0];

    [SerializeField, Tooltip("지금 고른 산업 칸의 바탕 (노랑 — 산업 탭의 고른 색과 같다)")]
    private Color highlightCellColor = new Color(0.839f, 0.682f, 0.067f, 1f);

    [SerializeField, Tooltip("나머지 칸의 바탕 (반투명 어둠)")]
    private Color normalCellColor = new Color(0f, 0f, 0f, 0.25f);

    [CenterHeader("버튼")]
    [SerializeField, Tooltip("배치/해제 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button assignButton = null!;

    [SerializeField, Tooltip("버튼 라벨 — 쓰는 쪽이 'SetButtonLabel'로 정한다")]
    private TMP_Text assignLabel = null!;

    // 이 줄의 버튼을 눌렀다 ('WorkStationSelectPresenter'가 구독).
    public event Action<CharacterStateRowView>? AssignClicked;

    // 이 줄이 그리고 있는 캐릭터 개체 번호. 미바인딩이면 0.
    public long CharacterId { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널이 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(backgroundImage, nameof(backgroundImage));
        this.RequireRef(portraitImage, nameof(portraitImage));
        this.RequireRef(nameText,      nameof(nameText));
        this.RequireRef(raceText,      nameof(raceText));
        this.RequireRef(assignButton,  nameof(assignButton));
        this.RequireRef(assignLabel,   nameof(assignLabel));

        if (aptitudeCellImages.Length != AptitudeLabel.Count || aptitudeValueTexts.Length != AptitudeLabel.Count)
        {
            ClientLogger.Error(ClientLogger.UI,
                $"적성 칸이 바탕 {aptitudeCellImages.Length}개 · 값 {aptitudeValueTexts.Length}개다 — " +
                $"1차 산업은 {AptitudeLabel.Count}종이라 자리가 어긋난다.", this);
        }

        assignButton.onClick.AddListener(() => AssignClicked?.Invoke(this));
    }

    // 이 줄이 그릴 캐릭터를 정한다 ('WorkStationSelectPresenter'가 호출).
    //   characterId : 서버가 발급한 개체 번호. 배치 요청에 그대로 실린다
    //   displayName : 보유 목록을 거쳐 얻은 표시 이름
    public void Bind(long characterId, string displayName)
    {
        CharacterId   = characterId;
        nameText.text = displayName;
    }

    // 줄 바탕을 이 캐릭터의 등급 색으로 칠한다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   rarity : 종류(TID)로 읽은 등급. 개체 번호로 읽으면 'None'이 되어 회색으로 칠해진다
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 적성 5칸을 그린다 ('WorkStationSelectPresenter'가 Bind 뒤에 호출).
    //   values         : 산업 순서대로 담긴 적성 5개. 값 0도 칸을 지우지 않고 'X'로 채운다
    //   highlightIndex : 지금 고른 산업의 칸 번호. 범위 밖이면 강조 없음
    public void SetAptitudes(byte[] values, int highlightIndex)
    {
        int count = Mathf.Min(aptitudeValueTexts.Length, aptitudeCellImages.Length);

        for (int i = 0; i < count; i++)
        {
            byte value = i < values.Length ? values[i] : (byte)0;

            // 배선 누락은 Awake가 이미 알렸다. 여기서 매번 다시 떠들지 않는다
            if (aptitudeValueTexts[i] != null)
            {
                aptitudeValueTexts[i].text  = AptitudeLabel.GetText(value);
                aptitudeValueTexts[i].color = AptitudeLabel.GetColor(value);
            }

            if (aptitudeCellImages[i] != null)
            {
                aptitudeCellImages[i].color = i == highlightIndex ? highlightCellColor : normalCellColor;
            }
        }
    }

    // 버튼 라벨을 정한다 — 목록 줄은 "배치", 세팅의 카드는 "해제" (만들 때 한 번 호출).
    public void SetButtonLabel(string label)
    {
        assignLabel.text = label;
    }

    // 버튼을 잠그거나 푼다 — 응답을 기다리는 동안만 잠긴다.
    public void SetAssignable(bool on)
    {
        assignButton.interactable = on;
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // 바탕색도 되돌린다 — 풀에서 다시 쓰일 때 이전 캐릭터의 등급 색이 남지 않게.
    public void Clear()
    {
        CharacterId           = 0;
        nameText.text         = "";
        backgroundImage.color = RarityPalette.Unknown;
    }
}
