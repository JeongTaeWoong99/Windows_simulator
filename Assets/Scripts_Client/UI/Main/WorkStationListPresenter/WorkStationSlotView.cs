using System.Collections.Generic;
using DG.Tweening;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 한 칸의 표시. 프리팹에 붙는다.
//
// ■ 스스로 시간을 세지 않는다
//   Update·코루틴을 두지 않고 'WorkStationListPresenter'가 계산해 넘겨 준 값만 그린다.
//   상시 실행 앱이라 칸마다 루프를 돌리면 슬롯 수만큼 낭비가 곱해진다.
//
// ■ 표시는 값의 진실이 아니다
//   진행 막대는 연출이고 판정은 서버가 한다. 어긋나도 다음 스냅샷이 교정한다.
//
// ■ 칸의 생김새 (2026-10-11 A안 — 목업 https://claude.ai/artifact/VxGmcpHuVn1BJaWK4PhAeo)
//   머리 줄  : [#번호] [산업 Lv]          속도(최대)   — 어디서 · 얼마나 빠른지
//   무대     : 왼쪽 아래 캐릭터 이름 · 아래 가장자리 초록 진행 막대(칸 폭 끝까지) — 누가 · 언제
//   아래 줄  : 남은 시간 '3.1초 후 수확'     장비 4칸(+보석) — 언제 · 무엇을 꼈는지
//   예전엔 네 정보를 한 줄('슬롯 2 · 채굴 Lv1 · 별빛드래곤 Lv10 · 6.86배')에 '·'로만 갈라 읽기 어려웠고,
//   진행 막대가 줄 하나(칸 높이의 1/9)를 통째로 썼다.
//   ※ 위젯 칸('WidgetMiniSlotView')은 남은 시간 글자를 두지 않는다 — 막대만(2026-10-11 사용자 결정).
//
// ■ 번호는 1부터 보인다
//   서버·클라는 슬롯을 0~7로 다루지만 사람에게는 #1~#8로 보인다('DisplayNumber'). 잠긴 칸 문구('작업슬롯 2번 해금')와 같은 셈이다.
//   ⚠️ 이름·막대는 무대('Visible Panel') **안이 아니라** 무대 위 형제 층('Stage Overlay')에 둔다 —
//      무대는 실행 중에 배우 그림을 자기 자식 맨 뒤에 만들어서, 안에 두면 배우에게 가려진다.
public class WorkStationSlotView : MonoBehaviour
{
    [CenterHeader("참조")]
    // 칸 바탕을 배치된 캐릭터의 등급 색으로 칠한다('SetRarity'). 인벤토리 칸('SlotView')과 같은 표('RarityPalette')를 쓴다.
    // 🎨 등급 이미지가 나오면 색 대신 여기에 스프라이트를 넣는다.
    [SerializeField, Tooltip("칸 바탕 — 프리팹 루트의 Image. 등급 색으로 칠해진다")]
    private Image backgroundImage = null!;

    [SerializeField, Tooltip("머리 줄 왼쪽 칩 — 슬롯 번호 '#2'")]
    private TMP_Text indexText = null!;

    [SerializeField, Tooltip("머리 줄 칩 — 산업 + 레벨 '채굴 Lv1'. 돌지 않는 칸은 '대기'")]
    private TMP_Text industryText = null!;

    [SerializeField, Tooltip("머리 줄 오른쪽 끝 — 속도 배율 '6.86배' (최소 주기에 걸리면 금색 '최대')")]
    private TMP_Text speedText = null!;

    [SerializeField, Tooltip("무대 왼쪽 아래 — 캐릭터 이름 + 레벨")]
    private TMP_Text characterText = null!;

    [SerializeField, Tooltip("판정 진행도 (0~1). 무대 아래 가장자리 · 칸 폭 끝까지. 표시 전용이라 interactable은 꺼 둔다")]
    private Slider progressSlider = null!;

    [SerializeField, Tooltip("아래 줄 왼쪽 — 남은 시간 '3.1초 후 수확'")]
    private TMP_Text remainText = null!;

    // ※ 아래 줄 오른쪽에 붙는다 — 인벤토리 캐릭터 칸에서 LV 배지 줄 오른쪽에 붙는 것과 짝이다(T-104).
    //   여기는 칸이 넓어 'EquipIconsView'(등급 바탕 + 장비 아이콘 + 큐브 보석)를 쓴다.
    [SerializeField, Tooltip("배치된 캐릭터가 낀 장비 4칸 — 무기·장신구1·장신구2·보석 (EquipIconsView 프리팹)")]
    private EquipPipsView equipPips = null!;

    [SerializeField, Tooltip("슬롯 무대 (Visible Panel). 비우면 연출 없이 바탕만 남는다")]
    private SlotStageView? stage;

    // 무대 안이 아니라 칸 루트에 붙는다 — 무대는 'RectMask2D'라 위로 오르는 아이콘이 잘린다.
    [SerializeField, Tooltip("아이템 획득 연출 (칸 루트의 ItemGainEffectView). 비우면 연출 없이 지나간다")]
    private ItemGainEffectView? gainEffect;

    private WorkStationSlotInfo? _slot;

    // 레벨업 연출 — 처음 레벨업할 때 붙인다(T-052). 칸 프리팹은 건드리지 않는다
    private LevelUpFx? _levelUp;

    // 이름 줄 튐 — 연달아 오르면 앞의 것을 끊고 다시
    private Tween? _namePunch;

    // 마지막으로 그린 남은 시간 (0.1초 단위). 같은 값이면 다시 쓰지 않는다 — -1은 "아직 안 썼다"
    private int _shownRemainTenths = -1;

    // 이 뷰가 그리고 있는 슬롯 번호. 미바인딩이면 -1.
    public int SlotIndex => _slot?.SlotIndex ?? -1;

    // 사람에게 보이는 번호 — 0번 슬롯이 '#1'이다
    public static int DisplayNumber(int slotIndex) => slotIndex + 1;

    // 배치돼 있고 속도가 0이 아니어서 카운트다운을 돌릴 수 있는가.
    public bool IsRunning => _slot != null && _slot.CharacterId != 0 && _slot.CurrentWorkSpeed > 0;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 WorkStationListPresenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(backgroundImage, nameof(backgroundImage));
        this.RequireRef(indexText,      nameof(indexText));
        this.RequireRef(industryText,   nameof(industryText));
        this.RequireRef(speedText,      nameof(speedText));
        this.RequireRef(characterText,  nameof(characterText));
        this.RequireRef(progressSlider, nameof(progressSlider));
        this.RequireRef(remainText,     nameof(remainText));
        this.RequireRef(equipPips,      nameof(equipPips));
    }

    // 슬롯 스냅샷을 반영한다 (WorkStationListPresenter가 호출).
    //   characterName : 배치된 캐릭터의 표시 이름. 뷰가 직접 조회하지 않는다 —
    //                   'slot.CharacterId'는 개체 번호라 테이블에서 이름이 안 나오고,
    //                   보유 목록을 거쳐야 한다. 그 변환은 세션을 아는 패널의 몫이다.
    //   characterTid  : 배치된 캐릭터의 종류(TID) — 무대가 그림을 고른다. 이름과 같은 이유로 패널이 넘긴다
    //   characterLevel : 배치된 캐릭터의 레벨 — 이름 바로 뒤에 붙인다. 0이면 적지 않는다(모르는 개체)
    public void Bind(WorkStationSlotInfo slot, string characterName, int characterTid, int characterLevel)
    {
        _slot = slot;

        if (stage != null)
        {
            stage.Show(slot.CharacterId != 0 ? characterTid : 0, slot.Industry, slot.IndustryLevel);
            stage.DrawIdle(); // 돌지 않는 칸은 이 장면에 멈춰 있다. 도는 칸은 다음 Tick이 덮는다
        }

        indexText.text     = $"#{DisplayNumber(slot.SlotIndex)}";
        characterText.text = slot.CharacterId == 0 ? Lang.Get("UI.WorkStation.NoCharacter")
                           : characterLevel > 0     ? $"{characterName} {UIRichText.Small($"Lv{characterLevel}")}"
                           : characterName;

        if (!IsRunning)
        {
            industryText.text    = Lang.Get("UI.WorkStation.Idle");
            speedText.text       = "";
            progressSlider.value = 0f;
            remainText.text      = "";
            _shownRemainTenths   = -1;

            return;
        }

        // 산업 레벨은 산업 바로 뒤에 붙인다 — 떨어뜨리면 무엇의 레벨인지 모호해진다.
        // 레벨 이름('저수지')은 세팅 화면의 레벨 버튼에만 쓴다 — 최대 6자('신성한 대지')라 칩이 길어진다.
        // 레벨 0은 적지 않는다 — 서버 기본값이 1이라 올 일이 없고, 'Lv0'은 없는 값이다.
        string industry = IndustryLabel.Get(slot.Industry);

        industryText.text = slot.IndustryLevel > 0 ? $"{industry} Lv{slot.IndustryLevel}" : industry;

        // 천분율 → 배율 ('Constants.WorkSpeedScale' = 1.0배). 최소 주기로 잘린 속도를 적고, 잘렸으면 금색 '최대'를 붙인다 (T-102)
        float  speedMultiplier = WorkStationProgress.GetEffectiveSpeed(slot) / (float)Constants.WorkSpeedScale;
        string capMark         = WorkStationProgress.IsAtMinCycle(slot)
                               ? UIRichText.Small(UIRichText.Paint(" " + Lang.Get("UI.WorkStation.Max"), UIThemeRole.Highlight))
                               : "";

        speedText.text = Lang.Format("UI.WorkStation.SpeedMul", speedMultiplier.ToString("0.00")) + capMark;
    }

    // 칸 바탕을 배치된 캐릭터의 등급 색으로 칠한다 (WorkStationListPresenter가 Bind 뒤에 호출).
    //   rarity : 종류(TID)로 읽은 등급. 모르는 개체면 'None' → 회색('Unknown')
    public void SetRarity(GlobalRarity rarity)
    {
        backgroundImage.color = RarityPalette.Get(rarity);
    }

    // 배치된 캐릭터의 장착 네모를 그린다 (WorkStationListPresenter가 Bind 뒤에 호출).
    //   grades : 'EquipLabel.WornSlots' 순서의 등급 — 'None'은 빈 칸
    //   icons  : 같은 순서의 장비 아이콘 — 빈 칸·그림 없음은 null
    //   gems   : 같은 순서의 인챈트 등급 — 아이콘 오른쪽 아래 보석. 'None'은 빈 보석(빈 칸은 보석이 꺼진다)
    //   tooltip : 장비 줄에 올리면 띄울 내용 — 칸마다 낀 장비 이름·등급
    public void SetEquipPips(IReadOnlyList<GlobalRarity> grades, IReadOnlyList<Sprite?> icons, IReadOnlyList<GlobalRarity> gems,
                             System.Func<TooltipContent?> tooltip)
    {
        equipPips.Bind(grades, icons, gems);
        equipPips.SetTooltip(tooltip);
    }

    // 이 칸에서 얻은 아이템을 띄운다 — 쓰러지는 대상 자리에서, 무대 그림이 없으면 무대 가운데에서
    // (WorkStationListPresenter의 GatherResultReceived 구독에서 호출).
    // 땅이 흐르면 대상과 함께 흘러간다 — 화면에 박혀 있으면 캐릭터를 따라오는 것처럼 보인다.
    public void PlayGain(IReadOnlyList<ItemGainEffectView.Gain> gains)
    {
        if (gainEffect == null)
        {
            return;
        }

        if (stage == null)
        {
            gainEffect.Play(transform.position, gains);

            return;
        }

        SlotStageView stageView = stage;
        Vector3       point     = stageView.TryGetHarvestPoint(out Vector3 harvest) ? harvest : stageView.transform.position;
        double        ground    = stageView.GroundDistance;

        gainEffect.Play(point, gains, () => stageView.FollowGround(point, ground));
    }

    // 배치된 캐릭터의 레벨이 올랐다 — 캐릭터 자리에서 금빛 기둥·화살표·'LV.12'가 솟고 이름 줄이 한 번 튄다
    // (WorkStationListPresenter의 CharacterLeveledUp 구독에서 호출 · T-052).
    //   level : 새 레벨. 한 정산에 여러 레벨이 올라도 한 번만 재생하고 마지막 레벨을 적는다
    public void PlayLevelUp(int level)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _levelUp ??= LevelUpFx.Attach(gameObject, characterText);

        if (stage != null && stage.TryGetCharacterPoint(out Vector3 feet, out Vector3 center))
        {
            _levelUp.Play(feet, center, level);
        }
        else
        {
            // 캐릭터 그림이 없다 — 무대(없으면 칸) 가운데에서
            var     area = stage != null ? (RectTransform)stage.transform : (RectTransform)transform;
            Rect    box  = area.rect;
            Vector3 feet0   = area.TransformPoint(new Vector3(box.center.x, box.yMin, 0f));
            Vector3 center0 = area.TransformPoint(box.center);

            _levelUp.Play(feet0, center0, level);
        }

        _namePunch?.Kill(complete: true);
        _namePunch = characterText.rectTransform.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f)
                                                .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                                                .OnKill(() => characterText.rectTransform.localScale = Vector3.one);
    }

    // 진행도를 갱신한다 (WorkStationListPresenter의 Update가 매 프레임 호출).
    //   remainSeconds : 다음 판정까지 남은 초
    //   cycleSeconds  : 판정 1회의 초 — 무대가 진행도를 주기 안의 순간으로 바꾼다
    public void Tick(float progress, float remainSeconds, float cycleSeconds)
    {
        progressSlider.value = progress;
        SetRemainText(remainSeconds);

        if (stage != null)
        {
            stage.Tick(progress * cycleSeconds, cycleSeconds, Time.deltaTime);
        }
    }

    // 남은 시간을 0.1초 단위로 적는다 (Tick에서 호출).
    //
    // ■ 표시값이 바뀔 때만 쓴다 — 문자열도 만들지 않는다
    //   예전엔 매 프레임 '$"…"'로 새 문자열을 만들었다(칸 8개 × 프레임마다 GC).
    //   'SetText'만 매 프레임 부르면 TMP는 같은 값이어도 글자를 다시 만든다 — 그래서 바뀔 때만 부른다.
    private void SetRemainText(float remainSeconds)
    {
        int tenths = Mathf.Max(0, Mathf.RoundToInt(remainSeconds * 10f));

        if (tenths == _shownRemainTenths)
        {
            return;
        }

        _shownRemainTenths = tenths;
        remainText.SetText(Lang.Get("UI.WorkStation.HarvestIn"), tenths / 10, tenths % 10);
    }
}
