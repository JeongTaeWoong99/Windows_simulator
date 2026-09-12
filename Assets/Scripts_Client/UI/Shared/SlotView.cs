using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 창고 칸 하나의 표시. 프리팹에 붙는다.
// 비어 있는 칸은 파괴하지 않고 'Clear'로 비워 두었다가 재사용한다
// — 채취가 도는 동안, 탭을 오가는 동안 생성·파괴가 반복되면 GC 부담이 쌓인다.
//
// ■ 아이템 전용이 아니다
// 창고 격자('StorageGridPresenter')가 자원·캐릭터를 같은 칸으로 그리고,
// 가챠 결과 팝업('GachaResultPresenter')도 같은 프리팹을 쓴다 — 칸의 생김새가 같아야 하고,
// 같아야 할 것을 여러 벌로 두면 한쪽만 고쳐지기 때문이다.
// 그래서 이 뷰는 '어느 화면에 있는지'도 '무엇을 그리는지'도 모른다. 값은 부르는 쪽이 완성해서 넘긴다.
//
// ■ 우클릭도 마찬가지다 — 여기서는 '무슨 뜻인지' 모른다
// 우클릭을 이벤트로 위에 던지기만 하고, 그것이 판매 담기인지는 격자가 정한다.
// 가챠 결과 팝업은 이 이벤트를 구독하지 않아 아무 일도 일어나지 않는다.
public class SlotView : MonoBehaviour, IPointerClickHandler
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("등급 배경. 칸 전체를 덮는다 — 색만 등급에 따라 바뀐다")]
    private Image rarityImage = null!;

    [SerializeField, Tooltip("아이콘. 아이콘 에셋이 아직 없어 지금은 비어 있다")]
    private Image itemImage = null!;

    [SerializeField, Tooltip("칸 이름 — 아이템 이름 · 캐릭터 이름")]
    private TMP_Text nameText = null!;

    // ※ 'Count Text'(countText)에서 이름이 바뀐 자리다. 자원은 수량이 들어오고 캐릭터는 이 자리를 쓰지 않아
    //   더 이상 '수량 칸'이 아니다 — 무엇이 오든 "이름 아래 한 줄"이라는 자리로 부른다.
    //   (캐릭터는 배치 상태 → 적성 요약 문구 → 스트립으로 두 번 바뀌어 결국 이 자리를 떠났다.)
    //   프리팹은 새 이름으로 이미 저장했으므로 [FormerlySerializedAs]는 걷어냈다.
    [SerializeField, Tooltip("이름 아래 보조 문구 — 자원은 수량. 캐릭터 탭에서는 적성 스트립이 이 자리를 쓴다")]
    private TMP_Text subText = null!;

    // ※ 보조 문구와 **같은 밴드**를 쓴다 — 자원은 수량 한 줄, 캐릭터는 5칸 스트립.
    //   자리가 하나라는 것은 프리팹의 사실이라, 어느 쪽을 켤지는 이 칸이 쥔다('SetAptitudes').
    [CenterHeader("적성 스트립 (캐릭터 탭)")]
    [SerializeField, Tooltip("적성 5칸을 담은 하단 밴드. 자원 탭에서는 꺼진다")]
    private GameObject aptitudeStrip = null!;

    // ⚠️ **칸의 순서가 곧 산업이다** — 농사 · 낚시 · 채굴 · 벌목 · 사냥('EIndustryType'의 None 제외 순서).
    //   배치 화면의 산업 버튼도 같은 순서로 만들어지므로(WorkStationSelectPresenter.BuildIndustryList)
    //   두 화면이 저절로 맞는다. **인스펙터에서 순서를 섞으면 값이 조용히 다른 산업 칸에 들어간다.**
    [SerializeField, NonReorderable, Tooltip("적성 값 칸 5개. 순서 = 농사·낚시·채굴·벌목·사냥")]
    private TMP_Text[] aptitudeValueTexts = new TMP_Text[0];

    [SerializeField, Tooltip("판매 목록에 담겼음을 알리는 표시. 평소에는 꺼져 있다")]
    private GameObject sellMark = null!;

    // ※ 판매 표시와 자리가 같아도 겹치지 않는다 — 담기는 자원 탭에서만, 배치는 캐릭터 탭에서만 켜진다.
    [SerializeField, Tooltip("이 캐릭터가 작업슬롯에서 일하는 중임을 알리는 표시. 평소에는 꺼져 있다")]
    private GameObject assignMark = null!;

    // 적성 칸 수 = 1차 산업 5종. 'EIndustryType'의 None 제외 개수와 같아야 한다.
    public const int AptitudeCount = 5;

    // 적성은 0~10이라 미리 만들어 둔다 — 200칸 × 5개를 매번 그리므로
    // 'ToString()'을 그때그때 부르면 Redraw마다 문자열 1000개가 버려진다(상주 앱이라 쌓인다).
    // ※ 0번은 'X'다 — **빈 칸으로 두면 배선이 빠진 칸과 구분되지 않는다.**
    //   "못 다루는 산업"은 알려 줄 값이 없는 게 아니라 알려 줄 것이 있는 상태다.
    private static readonly string[] ValueTexts = { "X", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

    // 적성 0은 흐리게 — 'X'가 숫자와 같은 세기로 보이면 눈이 먼저 X를 읽는다.
    private static readonly Color ValueColor = Color.white;
    private static readonly Color ZeroColor  = new Color(0.62f, 0.62f, 0.62f, 1f);

    // 이 칸이 그리고 있는 대상. 자원은 ItemId, 캐릭터는 개체 번호. 비어 있으면 0.
    public long Key { get; private set; }

    // 아무것도 그리고 있지 않은 빈 칸인가.
    public bool IsEmpty => Key == 0;

    // 이 칸을 우클릭했다 — 무슨 뜻인지는 이 칸을 만든 화면이 정한다.
    public event Action<SlotView>? RightClicked;

    // 화면이 보조 문구를 쓰겠다고 했나('SetSubVisible'). 적성 스트립과 자리가 같아
    // 문구를 되돌릴 때 이 값이 필요하다 — 스트립을 끈다고 팝업에서 꺼 둔 문구가 살아나선 안 된다.
    private bool _isSubAllowed = true;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 부르는 Presenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(rarityImage,    nameof(rarityImage));
        this.RequireRef(itemImage,      nameof(itemImage));
        this.RequireRef(nameText,       nameof(nameText));
        this.RequireRef(subText,        nameof(subText));
        this.RequireRef(aptitudeStrip,  nameof(aptitudeStrip));
        this.RequireRef(sellMark,       nameof(sellMark));
        this.RequireRef(assignMark,     nameof(assignMark));

        // 칸이 5개가 아니면 값이 다른 산업 자리에 들어간다 — 위치가 곧 산업이라 조용히 틀린다.
        if (aptitudeValueTexts.Length != AptitudeCount)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"적성 칸이 {aptitudeValueTexts.Length}개다 — 1차 산업은 {AptitudeCount}종이라 자리가 어긋난다.", this);
        }

        aptitudeStrip.SetActive(false);
        sellMark.SetActive(false);
        assignMark.SetActive(false);
    }

    // 칸을 클릭했다 — 우클릭만 위로 던진다 (EventSystem 클릭 콜백).
    //
    // 좌클릭은 아직 쓰지 않는다. 여기서 걸러 내지 않으면 판매 담기가 좌클릭에도 걸린다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right)
        {
            return;
        }

        RightClicked?.Invoke(this);
    }

    // 완성된 표시값을 그린다 ('StorageGridPresenter'·'GachaResultPresenter'가 호출).
    //
    // 이름도 등급도 여기서 조회하지 않는다 — 출처가 탭마다 다르기 때문이다.
    // 자원은 'ItemTable', 캐릭터는 'CharacterTable', 가챠 보상은 패킷이 등급을 실어 온다.
    // 칸이 한쪽을 골라 버리면 다른 쪽이 조용히 무시된다.
    //
    // TODO: 아이콘 에셋이 생기면 'itemImage.sprite'를 여기서 채운다.
    //       등급 이미지가 생기면 'rarityImage'도 색 대신 sprite를 넣는다 ('RarityPalette' 참조).
    public void Bind(in SlotData data)
    {
        Key               = data.Key;
        rarityImage.color = RarityPalette.Get(data.Rarity);
        nameText.text     = data.Name;
        subText.text      = data.Sub;
    }

    // 보조 문구를 켜고 끈다 (칸을 만든 화면이 한 번만 부른다).
    //
    // 창고는 "몇 개 갖고 있나"가 칸의 핵심이라 켜 두고, 가챠 결과는 뽑힌 것을
    // 그대로 늘어놓는 자리라 끈다 — 거기서는 개수가 칸이 아니라 목록의 길이로 드러난다.
    // ※ Clear는 이 상태를 되돌리지 않는다 — 풀에서 재사용돼도 화면의 결정이 유지돼야 한다.
    public void SetSubVisible(bool on)
    {
        _isSubAllowed = on;

        // 스트립이 켜져 있으면 그쪽이 자리를 쓰고 있다 — 여기서 켜면 둘이 겹친다.
        subText.gameObject.SetActive(on && !aptitudeStrip.activeSelf);
    }

    // 적성 5종을 스트립에 그린다. 'null'이면 스트립을 끄고 보조 문구 자리를 돌려준다
    // (창고 격자가 매번 그릴 때 호출 — 캐릭터 탭에서만 값이 온다).
    //
    // ■ 왜 값을 받아 오는가
    // 이 칸은 캐릭터를 모른다 — '배' 마크가 그렇듯 판단과 조회는 격자가 하고 칸은 그리기만 한다.
    // 적성의 주인은 서버이고('PlayerDataModel.GetAptitude'), 칸이 테이블을 직접 읽으면
    // 장비가 적성에 얹히는 날 개체마다 값이 갈라진다.
    //
    // ⚠️ **values의 순서가 곧 산업이다** (농사·낚시·채굴·벌목·사냥). 값 0도 칸을 지우지 않고
    //   'X'로 채운다 — 칸을 없애면 위치가 밀려 남은 값이 다른 산업으로 읽힌다.
    //
    // TODO: 산업 아이콘이 생기면 각 칸의 배경 Image에 sprite를 넣는다 — 자리·개수·순서는 그대로다.
    public void SetAptitudes(byte[]? values)
    {
        bool on = values != null;

        aptitudeStrip.SetActive(on);

        // 같은 밴드를 나눠 쓴다 — 스트립이 켜지면 문구는 비켜야 한다.
        subText.gameObject.SetActive(_isSubAllowed && !on);

        if (!on)
        {
            return;
        }

        for (int i = 0; i < aptitudeValueTexts.Length; i++)
        {
            TMP_Text? text = aptitudeValueTexts[i];

            if (text == null)
            {
                continue; // 배선 누락은 Awake가 이미 경고했다. 여기서 매번 다시 떠들지 않는다
            }

            byte value = i < values!.Length ? values[i] : (byte)0;

            text.text  = value < ValueTexts.Length ? ValueTexts[value] : value.ToString();
            text.color = value == 0 ? ZeroColor : ValueColor;
        }
    }

    // 판매 목록에 담겼음을 표시한다 (창고 격자가 매번 그릴 때 호출).
    //
    // 'SetSubVisible'과 달리 한 번 정하고 끝나는 스위치가 아니다 — 담기·빼기로 계속 바뀐다.
    // 가챠 결과 팝업은 이걸 부르지 않으므로 거기서는 늘 꺼져 있다.
    public void SetSellMark(bool on)
    {
        sellMark.SetActive(on);
    }

    // 이 캐릭터가 작업슬롯에서 일하는 중임을 표시한다 (창고 격자가 매번 그릴 때 호출).
    //
    // 'SetSellMark'와 같은 성격이다 — 배치·해제로 계속 바뀐다.
    // 가챠 결과 팝업은 이걸 부르지 않으므로 거기서는 늘 꺼져 있다.
    public void SetAssignMark(bool on)
    {
        assignMark.SetActive(on);
    }

    // 칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // ※ 등급색과 마크도 되돌린다 — 안 그러면 다음에 이 칸을 쓸 때 이전 칸의 흔적이 남는다.
    public void Clear()
    {
        Key               = 0;
        rarityImage.color = RarityPalette.Unknown;
        nameText.text     = "";
        subText.text      = "";

        SetAptitudes(null); // 스트립을 끄고 보조 문구 자리를 원래대로 돌려준다
        sellMark.SetActive(false);
        assignMark.SetActive(false);
    }
}
