using System;
using System.Collections.Generic;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 인벤토리 칸 하나의 표시. 프리팹에 붙는다.
// 비어 있는 칸은 파괴하지 않고 'Clear'로 비워 두었다가 재사용한다
// — 채취가 도는 동안, 탭을 오가는 동안 생성·파괴가 반복되면 GC 부담이 쌓인다.
//
// ■ 아이템 전용이 아니다
// 인벤토리 격자('InventoryGridPresenter')가 자원·캐릭터를 같은 칸으로 그리고,
// 가챠 결과 팝업('GachaResultPresenter')도 같은 프리팹을 쓴다 — 칸의 생김새가 같아야 하고,
// 같아야 할 것을 여러 벌로 두면 한쪽만 고쳐지기 때문이다.
// 그래서 이 뷰는 '어느 화면에 있는지'도 '무엇을 그리는지'도 모른다. 값은 부르는 쪽이 완성해서 넘긴다.
//
// ■ 클릭도 마찬가지다 — 여기서는 '무슨 뜻인지' 모른다
// 좌·우클릭을 이벤트로 위에 던지기만 하고, 그것이 판매 담기인지 상자 개봉인지는 격자가 정한다.
// 가챠 결과 팝업은 이 이벤트들을 구독하지 않아 아무 일도 일어나지 않는다.
public class SlotView : MonoBehaviour, IPointerClickHandler
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("등급 배경. 칸 전체를 덮는다 — 색만 등급에 따라 바뀐다")]
    private Image rarityImage = null!;

    [SerializeField, Tooltip("아이콘 — 자원·장비 아이콘 · 캐릭터 상반신. 그림이 없으면 자리 표시 네모(프리팹 색)")]
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

    // ※ 아이콘 왼쪽 여백(20px)에 세로로 선다 — 아이콘과 높이를 같게 두고, 아래에서 위로 찬다.
    //   높이는 고정값이 아니라 위아래 20px 안쪽 스트레치다 — 인벤토리 격자가 칸을 프레임 크기로 늘려도 아이콘과 함께 늘어난다.
    //   조작하는 슬라이더가 아니라 표시 전용이다(interactable 꺼짐 · 핸들 없음 · raycast 끔 — 우클릭을 가로채지 않는다).
    [CenterHeader("레벨 · 경험치 (캐릭터 탭)")]
    [SerializeField, Tooltip("현재 레벨의 경험치 진행. 왼쪽 벽 세로 막대 · 아래→위. 자원 탭·가챠 결과에서는 꺼진다")]
    private Slider expGauge = null!;

    // ※ 경험치 게이지의 **오른쪽 아래에 붙는다** — 게이지 바닥과 높이를 맞춘다.
    //   아이콘 모서리에 두면 초상화 형태(얼굴·전신…)에 따라 배지만 붕 뜬다. 게이지에 붙이면 둘이 한 덩어리로 읽힌다.
    [SerializeField, Tooltip("레벨 배지(바탕 포함). 게이지 오른쪽 아래 · 자원 탭·가챠 결과에서는 꺼진다")]
    private GameObject levelBadge = null!;

    [SerializeField, Tooltip("레벨 배지 안의 문구 — 'LV.19' · 만렙 'LV.MAX'")]
    private TMP_Text levelText = null!;

    // ※ 장비 탭에서는 보조 문구(효과)를 끈다 — 기본 능력치 문구는 툴팁으로 갔다(T-095).
    // ※ 칸 수만큼 네모를 그리던 것을 보석 하나로 바꿨다(2026-10-10 — 'EnchantGemView' 주석).
    [CenterHeader("인챈트 보석 (장비 탭)")]
    [SerializeField, Tooltip("오른쪽 아래 인챈트 보석 — 색 = 인챈트 등급, 인챈트 전이면 빈 보석. 장비 탭에서만 켜진다")]
    private EnchantGemView enchantGem = null!;

    // ※ LV 배지와 **같은 줄**, 오른쪽 끝은 **적성 스트립의 오른쪽 끝**과 맞춘다 — 왼쪽의 경험치 게이지가
    //   스트립 왼쪽 끝과 맞는 것과 짝이다(2026-10-07). 네모 하나가 낀 장비 하나의 등급이다(장비 탭의 인챈트 보석과는 다른 정보).
    [CenterHeader("장착 네모 (캐릭터 탭)")]
    [SerializeField, Tooltip("캐릭터가 낀 장비 4칸 — 무기·장신구1·장신구2·보석. 캐릭터 탭에서만 켜진다")]
    private EquipPipsView equipPips = null!;

    // ※ 글자가 아니라 그림이다 — 옛 폰트에 '★' 글리프가 없어 그림으로 정했다('Inventory 규칙.md').
    // ※ 얻은 ★만 켠다. ★0이면 줄째 꺼져 대부분의 칸이 깨끗하게 남는다(T-130 목업).
    [CenterHeader("응축 ★ (캐릭터 탭)")]
    [SerializeField, Tooltip("★ 줄 — 이름 아래 왼쪽. ★0이면 꺼진다")]
    private GameObject starRow = null!;

    [SerializeField, NonReorderable, Tooltip("★ 그림 — 최고 ★(4) 이상. 왼쪽부터 ★ 수만큼 켜진다")]
    private Image[] starImages = new Image[0];

    // ※ 응축 창이 열린 동안만 켜진다 — 대상은 파랑 테두리, 고른 재료는 노랑 테두리 + 체크.
    //   못 넣는 칸(다른 캐릭터 · 배치·착용 중)은 표시를 따로 두지 않고 흑백으로 그린다('SlotCondenseMark.Excluded').
    [SerializeField, Tooltip("응축 대상 표시 — 파랑 테두리. 평소에는 꺼져 있다")]
    private GameObject condenseTargetMark = null!;

    [SerializeField, Tooltip("응축 재료로 고름 표시 — 노랑 테두리 + 체크. 평소에는 꺼져 있다")]
    private GameObject condensePickMark = null!;

    [SerializeField, Tooltip("판매 목록에 담겼음을 알리는 표시. 평소에는 꺼져 있다")]
    private GameObject sellMark = null!;

    // ※ 판매 표시와 자리가 같아도 겹치지 않는다 — 담기는 자원 탭에서만, 이 표시는 캐릭터·장비 탭에서만 켜진다.
    // ※ 뜻이 탭마다 다르다 — 캐릭터는 '작업슬롯에서 일하는 중', 장비는 '캐릭터가 끼고 있는 중'이다.
    //   칸은 그 판단을 모른다. 무엇을 뜻하든 "지금 다른 데 나가 있다"라서 같은 표시를 쓴다.
    [SerializeField, Tooltip("지금 쓰이고 있음을 알리는 표시 — 캐릭터는 작업 중, 장비는 장착 중. 평소에는 꺼져 있다")]
    private GameObject assignMark = null!;

    // 적성 칸 수 = 1차 산업 5종. 표기 규칙("0은 X")과 함께 'AptitudeLabel'이 쥔다 —
    // 작업슬롯 선택 화면의 캐릭터 줄도 같은 5칸을 그린다.
    public static readonly int AptitudeCount = AptitudeLabel.Count;

    // 칸이 그릴 수 있어야 하는 ★ 수 — 'CharacterStarTable' 행 수(4)와 맞춘다. 프리팹의 ★ 그림 수 확인에만 쓴다.
    private const int MaxStarImages = 4;

    // 이 칸이 그리고 있는 대상. 자원은 ItemId, 캐릭터는 개체 번호. 비어 있으면 0.
    public long Key { get; private set; }

    // 아무것도 그리고 있지 않은 빈 칸인가.
    public bool IsEmpty => Key == 0;

    // 이 칸을 우클릭했다 — 무슨 뜻인지는 이 칸을 만든 화면이 정한다.
    public event Action<SlotView>? RightClicked;

    // 이 칸을 좌클릭했다 — 위와 같다. 인벤토리 격자가 '상자 개봉'으로 읽는다.
    public event Action<SlotView>? LeftClicked;

    // 나가 있는 칸을 얼마나 어둡게 할지 (RGB 배수). 알파는 건드리지 않는다 —
    // 반투명으로 만들면 뒤의 프레임이 비쳐 "빈 칸"과 헷갈린다.
    private const float AwayTint = 0.55f;

    // 찾기 조건에 안 맞는 칸의 흑백 밝기 배수와 글씨 투명도.
    // ※ 흑백으로 가르는 이유 — 딤(나가 있음)은 색조를 남기고 어둡게만 해서, 둘이 겹쳐도 구분된다.
    //   반투명은 쓰지 않는다(위 'AwayTint'와 같은 이유 — 빈 칸과 헷갈린다). 글씨만 옅게 한다.
    private const float FilteredOutTint      = 0.75f;
    private const float FilteredOutTextAlpha = 0.45f;

    // 화면이 보조 문구를 쓰겠다고 했나('SetSubVisible'). 적성 스트립과 자리가 같아
    // 문구를 되돌릴 때 이 값이 필요하다 — 스트립을 끈다고 팝업에서 꺼 둔 문구가 살아나선 안 된다.
    private bool _isSubAllowed = true;

    // 지금 딤 처리 중인가('SetDimmed'). 'Bind'가 등급색을 새로 칠하므로 여기서 다시 곱해 줘야 한다 —
    // 안 그러면 배치 중인 칸이 다음 갱신 때 밝아진다.
    private bool _isDimmed;

    // 지금 흑백 처리 중인가('SetFilteredOut'). 딤과 같은 이유로 'Bind' 뒤에 다시 반영한다.
    private bool _isFilteredOut;

    // 응축 창이 이 칸을 재료로 못 쓴다고 했나('SetCondenseMark'). 찾기 제외와 같은 흑백으로 그린다.
    // ※ 찾기와 플래그를 나눈 이유 — 응축 창을 닫으면 이것만 걷혀야 한다. 찾기 조건은 그대로 남는다.
    private bool _isCondenseExcluded;

    // 아이콘의 원래 색. 딤을 되돌릴 기준값이라 프리팹 값을 한 번만 읽어 둔다.
    private Color _itemBaseColor = Color.white;
    private Color _itemPlaceholderColor; // 그림이 없을 때의 네모 색 — 프리팹 값

    // 이름·보조 문구의 원래 투명도. 흑백을 걷을 때 돌아갈 값이다 — 1로 되돌리면 테마가 준 투명도를 덮는다.
    private float _nameBaseAlpha = 1f;
    private float _subBaseAlpha  = 1f;

    // 딤을 걷었을 때 돌아갈 등급색. 'rarityImage.color'를 그대로 읽으면 이미 어두워진 값이라
    // 껐다 켤 때마다 점점 검어진다.
    private Color _rarityColor = RarityPalette.Unknown;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 부르는 Presenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(rarityImage,    nameof(rarityImage));
        this.RequireRef(itemImage,      nameof(itemImage));
        this.RequireRef(nameText,       nameof(nameText));
        this.RequireRef(subText,        nameof(subText));
        this.RequireRef(aptitudeStrip,  nameof(aptitudeStrip));
        this.RequireRef(expGauge,       nameof(expGauge));
        this.RequireRef(levelBadge,     nameof(levelBadge));
        this.RequireRef(levelText,      nameof(levelText));
        this.RequireRef(sellMark,       nameof(sellMark));
        this.RequireRef(assignMark,     nameof(assignMark));
        this.RequireRef(enchantGem,      nameof(enchantGem));
        this.RequireRef(equipPips,       nameof(equipPips));
        this.RequireRef(starRow,            nameof(starRow));
        this.RequireRef(condenseTargetMark, nameof(condenseTargetMark));
        this.RequireRef(condensePickMark,   nameof(condensePickMark));

        // ★ 그림이 최고 ★보다 적으면 높은 ★이 덜 그려진다 — 조용히 틀리므로 경고한다.
        if (starImages.Length < MaxStarImages)
        {
            ClientLogger.Warn(ClientLogger.UI, $"★ 그림이 {starImages.Length}개다 — 최고 ★ {MaxStarImages}보다 적다.", this);
        }

        // 칸이 5개가 아니면 값이 다른 산업 자리에 들어간다 — 위치가 곧 산업이라 조용히 틀린다.
        if (aptitudeValueTexts.Length != AptitudeCount)
        {
            ClientLogger.Warn(ClientLogger.UI,
                $"적성 칸이 {aptitudeValueTexts.Length}개다 — 1차 산업은 {AptitudeCount}종이라 자리가 어긋난다.", this);
        }

        _itemPlaceholderColor = itemImage.color;
        _itemBaseColor        = _itemPlaceholderColor;
        _nameBaseAlpha = nameText.alpha;
        _subBaseAlpha  = subText.alpha;

        aptitudeStrip.SetActive(false);
        enchantGem.gameObject.SetActive(false);
        equipPips.gameObject.SetActive(false);
        expGauge.gameObject.SetActive(false);
        levelBadge.SetActive(false);
        sellMark.SetActive(false);
        assignMark.SetActive(false);
        starRow.SetActive(false);
        condenseTargetMark.SetActive(false);
        condensePickMark.SetActive(false);
    }

    // 칸을 클릭했다 — 좌·우를 갈라 위로 던진다 (EventSystem 클릭 콜백).
    //
    // 가운데 버튼은 버린다. 갈라 두지 않으면 판매 담기가 아무 버튼에나 걸린다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            RightClicked?.Invoke(this);

            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            LeftClicked?.Invoke(this);
        }
    }

    // 완성된 표시값을 그린다 ('InventoryGridPresenter'·'GachaResultPresenter'가 호출).
    //
    // 이름도 등급도 여기서 조회하지 않는다 — 출처가 탭마다 다르기 때문이다.
    // 자원은 'ItemTable', 캐릭터는 'CharacterTable', 가챠 보상은 패킷이 등급을 실어 온다.
    // 칸이 한쪽을 골라 버리면 다른 쪽이 조용히 무시된다.
    //
    // TODO: 등급 이미지가 생기면 'rarityImage'도 색 대신 sprite를 넣는다 ('RarityPalette' 참조).
    public void Bind(in SlotData data)
    {
        Key           = data.Key;
        _rarityColor  = RarityPalette.Get(data.Rarity);
        nameText.text = data.Name;
        subText.text  = data.Sub;

        SetIcon(data.Icon);
        ApplyTint();
    }

    // 아이콘 칸 — 그림이 있으면 제 색(흰색 곱)으로, 없으면 프리팹의 자리 표시 네모로 (Bind · Clear에서 호출)
    private void SetIcon(Sprite? icon)
    {
        itemImage.sprite         = icon;
        itemImage.preserveAspect = icon != null;
        _itemBaseColor           = icon != null ? Color.white : _itemPlaceholderColor;
    }

    // 보조 문구를 켜고 끈다 (칸을 만든 화면이 한 번만 부른다).
    //
    // 인벤토리는 "몇 개 갖고 있나"가 칸의 핵심이라 켜 두고, 가챠 결과는 뽑힌 것을
    // 그대로 늘어놓는 자리라 끈다 — 거기서는 개수가 칸이 아니라 목록의 길이로 드러난다.
    // ※ Clear는 이 상태를 되돌리지 않는다 — 풀에서 재사용돼도 화면의 결정이 유지돼야 한다.
    public void SetSubVisible(bool on)
    {
        _isSubAllowed = on;

        RefreshSubText();
    }

    // 하단 밴드의 주인을 정한다 — 적성 스트립이 켜져 있거나 장비 칸(보석)이면 문구는 비킨다
    // (SetSubVisible · SetAptitudes · SetStatSockets에서 호출).
    // ※ 셋이 같은 자리라 판정을 한 곳에 둔다 — 셋에 흩어 두면 하나를 더할 때 한 곳이 빠진다.
    private void RefreshSubText()
    {
        subText.gameObject.SetActive(_isSubAllowed && !aptitudeStrip.activeSelf && !enchantGem.gameObject.activeSelf);
    }

    // 적성 5종을 스트립에 그린다. 'null'이면 스트립을 끄고 보조 문구 자리를 돌려준다
    // (인벤토리 격자가 매번 그릴 때 호출 — 캐릭터 탭에서만 값이 온다).
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
        RefreshSubText();

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

            text.text  = AptitudeLabel.GetText(value);
            text.color = AptitudeLabel.GetColor(value);
        }
    }

    // 장비의 인챈트 보석을 그린다. 'null'이면 보석을 끄고 보조 문구 자리를 돌려준다
    // (인벤토리 격자가 매번 그릴 때 호출 — 장비 탭에서만 값이 온다 · T-095).
    //
    // grades는 칸마다의 등급이다 — 칸 수·등급의 판단은 공급자가 한다('EquipSlotSource'). 보석은 그중 인챈트 등급 하나만 그린다.
    public void SetStatSockets(IReadOnlyList<GlobalRarity>? grades)
    {
        enchantGem.Bind(grades);
        RefreshSubText();
        ApplyTint();
    }

    // 장착 네모를 그린다. 'null'이면 줄을 끈다 (인벤토리 격자가 매번 그릴 때 호출 — 캐릭터 탭에서만 값이 온다, T-104).
    //
    // grades는 'EquipLabel.WornSlots' 순서의 등급이고 'None'은 빈 칸이다. 읽는 것은 공급자다('CharacterSlotSource')
    // — 이 칸은 캐릭터를 모른다('SetAptitudes'와 같은 이유).
    public void SetEquipPips(IReadOnlyList<GlobalRarity>? grades)
    {
        equipPips.Bind(grades);

        ApplyTint();
    }

    // 레벨 배지를 그린다. 'null'이면 배지를 끈다 (인벤토리 격자가 매번 그릴 때 호출 — 캐릭터 탭에서만 값이 온다).
    // 문구('LV.19' · 'LV.MAX')는 격자가 짓는다 — 이 칸은 만렙이 몇인지 모른다.
    public void SetLevelBadge(string? label)
    {
        bool on = label != null;

        levelBadge.SetActive(on);

        if (!on)
        {
            return;
        }

        levelText.text = label!;
    }

    // 경험치 진행(0~1)을 세로 게이지에 그린다. 'null'이면 게이지를 끈다
    // (인벤토리 격자가 매번 그릴 때 호출 — 캐릭터 탭에서만 값이 온다).
    //
    // 적성 스트립과 같은 이유로 값을 받아 온다 — 이 칸은 캐릭터를 모르고,
    // 진행률 계산(레벨 곡선 조회)은 'PlayerDataModel.GetExpProgress'가 한다.
    public void SetExpGauge(float? progress)
    {
        bool on = progress.HasValue;

        expGauge.gameObject.SetActive(on);

        if (!on)
        {
            return;
        }

        expGauge.SetValueWithoutNotify(progress!.Value);
    }

    // 응축 ★을 그린다 — 왼쪽부터 ★ 수만큼 켜고, 0이면 줄을 끈다 (인벤토리 격자가 매번 그릴 때 호출 — 캐릭터 탭에서만 0이 아니다).
    public void SetStars(int star)
    {
        starRow.SetActive(star > 0);

        for (int i = 0; i < starImages.Length; i++)
        {
            starImages[i].gameObject.SetActive(i < star);
        }
    }

    // 응축 창에서 이 칸이 무엇인가를 그린다 (인벤토리 격자가 매번 그릴 때 호출 — 응축 창이 닫혀 있으면 늘 'None').
    public void SetCondenseMark(SlotCondenseMark mark)
    {
        condenseTargetMark.SetActive(mark == SlotCondenseMark.Target);
        condensePickMark.SetActive(mark == SlotCondenseMark.Material);

        bool excluded = mark == SlotCondenseMark.Excluded;

        if (_isCondenseExcluded == excluded)
        {
            return;
        }

        _isCondenseExcluded = excluded;

        ApplyTint();
    }

    // 판매 목록에 담겼음을 표시한다 (인벤토리 격자가 매번 그릴 때 호출).
    //
    // 'SetSubVisible'과 달리 한 번 정하고 끝나는 스위치가 아니다 — 담기·빼기로 계속 바뀐다.
    // 가챠 결과 팝업은 이걸 부르지 않으므로 거기서는 늘 꺼져 있다.
    public void SetSellMark(bool on)
    {
        sellMark.SetActive(on);
    }

    // 이 칸이 지금 쓰이고 있음을 표시한다 (인벤토리 격자가 매번 그릴 때 호출).
    // 캐릭터는 작업슬롯에서 일하는 중, 장비는 캐릭터가 끼고 있는 중이다 — 무엇인지는 격자가 정한다.
    //
    // 'SetSellMark'와 같은 성격이다 — 배치·해제, 장착·해제로 계속 바뀐다.
    // 가챠 결과 팝업은 이걸 부르지 않으므로 거기서는 늘 꺼져 있다.
    public void SetAssignMark(bool on)
    {
        assignMark.SetActive(on);
    }

    // 이 칸을 어둡게 한다 — 지금 인벤토리 밖에 나가 있다는 뜻 (인벤토리 격자가 매번 그릴 때 호출).
    //
    // '배' 마크와 **짝으로만 쓴다.** 딤만 두면 "왜 어두운가"를 알 수 없고,
    // 마크만 두면 칸이 200개일 때 작은 배지가 눈에 안 띈다.
    // ※ 끄고 켜기만 하면 되므로 마크와 합치지 않았다 — 가챠 결과 팝업은 둘 다 부르지 않는다.
    public void SetDimmed(bool on)
    {
        if (_isDimmed == on)
        {
            return;
        }

        _isDimmed = on;

        ApplyTint();
    }

    // 이 칸을 흑백으로 그린다 — 인벤토리 찾기 조건에 안 맞는다는 뜻 (인벤토리 격자가 매번 그릴 때 호출, T-069).
    //
    // 칸은 그대로 두고 색만 뺀다 — 빼 버리면 인벤토리가 비어 보인다('InventorySlotSource' 주석).
    // ⚠️ 색 곱셈이라 **등급 바탕·단색 아이콘까지만** 흑백이 된다. 아이콘 스프라이트가 들어오면
    //    그 색은 곱셈으로 빠지지 않는다 — 그때는 흑백 머티리얼로 바꾼다(🎨).
    public void SetFilteredOut(bool on)
    {
        if (_isFilteredOut == on)
        {
            return;
        }

        _isFilteredOut = on;

        ApplyTint();
    }

    // 흑백으로 그리나 — 찾기 제외 또는 응축 재료로 못 씀. 둘은 같은 모양이다.
    private bool IsGray => _isFilteredOut || _isCondenseExcluded;

    // 보관해 둔 등급색·아이콘 색에 딤·흑백을 반영한다 (Bind · SetDimmed · SetFilteredOut · SetCondenseMark · Clear에서 호출).
    private void ApplyTint()
    {
        rarityImage.color = Tint(_rarityColor);
        itemImage.color   = Tint(_itemBaseColor);

        enchantGem.SetTint(_isDimmed, IsGray);
        equipPips.SetTint(_isDimmed, IsGray);

        float textAlpha = IsGray ? FilteredOutTextAlpha : 1f;

        nameText.alpha = _nameBaseAlpha * textAlpha;
        subText.alpha  = _subBaseAlpha  * textAlpha;
    }

    // 원래 색에 이 칸의 흑백·딤을 입힌다 (ApplyTint에서 호출).
    private Color Tint(Color source) => TintColor(source, _isDimmed, IsGray);

    // 원래 색에 흑백(찾기 제외) → 딤(나가 있음)을 차례로 입힌다. 알파는 건드리지 않는다.
    // ※ 칸 안에 끼운 다른 View('EquipPipsView')도 이 규칙 하나로 어두워진다 — 따로 두면 네모만 떠 보인다.
    public static Color TintColor(Color source, bool dimmed, bool filteredOut)
    {
        Color color = source;

        if (filteredOut)
        {
            float gray = (color.r * 0.299f + color.g * 0.587f + color.b * 0.114f) * FilteredOutTint;

            color = new Color(gray, gray, gray, color.a);
        }

        float dim = dimmed ? AwayTint : 1f;

        return new Color(color.r * dim, color.g * dim, color.b * dim, color.a);
    }

    // 칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // ※ 등급색과 마크도 되돌린다 — 안 그러면 다음에 이 칸을 쓸 때 이전 칸의 흔적이 남는다.
    public void Clear()
    {
        Key            = 0;
        _rarityColor   = RarityPalette.Unknown;
        _isDimmed      = false;
        _isFilteredOut = false;
        _isCondenseExcluded = false;
        nameText.text  = "";
        subText.text   = "";

        SetIcon(null);
        ApplyTint();

        SetAptitudes(null); // 스트립을 끄고 보조 문구 자리를 원래대로 돌려준다
        SetStatSockets(null);
        SetEquipPips(null);
        SetExpGauge(null);
        SetLevelBadge(null);
        sellMark.SetActive(false);
        assignMark.SetActive(false);
        SetStars(0);
        condenseTargetMark.SetActive(false);
        condensePickMark.SetActive(false);
    }
}

// 응축 창이 열린 동안 칸 하나가 무엇인가 ('SlotView.SetCondenseMark'). 뜻은 인벤토리 격자가 정한다.
public enum SlotCondenseMark
{
    // 응축 창이 닫혀 있다 — 아무것도 그리지 않는다
    None,

    // ★을 올릴 대상 — 파랑 테두리
    Target,

    // 재료로 고른 칸 — 노랑 테두리 + 체크
    Material,

    // 고를 수 있지만 아직 안 고른 칸 — 평소 모습 그대로
    Candidate,

    // 재료로 못 쓴다(다른 캐릭터 · 배치·착용 중) — 흑백
    Excluded,
}
