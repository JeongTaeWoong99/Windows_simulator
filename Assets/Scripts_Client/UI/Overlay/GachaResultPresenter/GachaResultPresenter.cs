using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 보상 결과 팝업 — 이번에 얻은 것을 5열로 늘어놓고, 닫기를 누르면 사라진다.
// 칸은 인벤토리와 **공유하는** 프리팹('SlotView')이다 — 인벤토리 전용이 아니다.
// ⚠️ 그래서 인벤토리 쪽을 고치면 이 팝업도 함께 바뀐다.
//
// ■ 가챠와 상자 개봉이 같은 팝업을 쓴다
// 서버가 상자 개봉 결과를 가챠와 **같은 모양**('GachaRewardInfo')으로 내려주기 때문이다.
// 연출을 두 벌 만들면 한쪽만 고쳐지므로 여기 하나로 둔다(T-033).
// 늘어놓는 방식만 갈린다 — 가챠는 뽑힌 순서 그대로, 상자는 종류별 합계다('OnItemUseCompleted').
// 우편 수령도 여기로 온다 — 받은 우편의 첨부를 상자처럼 종류별로 합친다('OnMailRewardsClaimed').
//
// ■ [n회 더 뽑기] — 가챠 결과에서만 보인다 (2026-10-09)
// 닫기 왼쪽에 둔다. 방금 뽑은 횟수 그대로(1회면 1회, 10회면 10회) 같은 풀을 다시 뽑는다.
// 요청은 'GachaPresenter.DrawAgain'에 맡긴다 — 대기·연타 방지·실패 알림이 그쪽에 한 벌 있다.
// 결과가 오면 이 팝업이 새 결과로 다시 그려진다(닫았다 열 필요 없다).
// 상자·우편 결과에서는 숨긴다 — 다시 할 '뽑기'가 없다.
//
// ■ 하나씩 공개한다 — 공개 연출 (T-031 · T-033, 2026-10-09 목업 A + D)
// 창은 다 자란 채로 뜨고(칸은 자리를 지키되 투명), 칸이 받은 순서대로 하나씩 튀어나온다.
// 영웅↑은 멈칫 → 차오름 → 빛 폭발로 공개하고, 전설↑은 창까지 흔든다. 공개 뒤 고등급 칸은 빛이 남는다.
// 가챠·상자·우편 세 경로가 같은 연출을 쓴다 — 늘어놓는 방식(낱개·합계)만 갈린다.
// 칸 하나의 움직임은 'RewardRevealFx', 폭발은 'RewardBurstFx', 조정값은 'RewardRevealSettings'(인스펙터 'reveal')에 있다.
//
// ■ 유물 감정은 창 대신 무대가 결과다 (2026-10-25 · T-151 · 목업 '유물감정목업.html' · #88 2026-10-28)
// 흔들림 → 금 → 번쩍 → 조각 → 보상 등장 → (아티팩트면) 띠 — 'RelicAppraiseFx'. 그동안 창(Panel)은 투명하다.
// 유물은 1개씩 감정하므로 나온 1개가 결과 전부다 — 무대가 끝나면(또는 눌러 건너뛰면) 보상이 놓인 채로 머물고,
// 한 번 더 누르면 닫힌다. 결과 칸 목록은 띄우지 않는다. 상자 개봉 · 가챠 · 우편은 무대 없이 바로 칸을 공개한다.
//
// 공개 중에는 [n회 더 뽑기] · 닫기가 잠긴다 — 결과를 다 보기 전에 창을 닫거나 새 결과로 덮지 못한다.
// 공개 중에 창 아무 곳(칸 포함)을 누르면 **스킵** — 남은 칸이 한꺼번에 끝 모습으로 놓이고 버튼이 풀린다.
// 칸 클릭은 'SlotView'가 먼저 받으므로('IPointerClickHandler') 칸의 클릭 이벤트도 함께 구독해 스킵으로 읽는다.
//
// ■ 왜 길드 열이 아니라 '!Overlay Canvas'인가
// 이 팝업은 'PlayerDataModel'의 결과 이벤트를 스스로 구독해서 뜬다 — 나를 켜 줄 주체가 밖에 없다.
// 길드 열의 자식으로 두면 요청을 보낸 뒤 열을 닫는 순간 결과가 통째로 사라진다.
// 최상단 상주 오버레이로 두면 어느 열이 열려 있든 결과가 뜬다.
//
// ⚠️ 오브젝트를 끄지 않고 'CanvasGroup'으로 표시/숨김한다 — 자기 자신을 끄면 다시 켤 이벤트를
// 받지 못한다(꺼진 오브젝트는 콜백이 오지 않는다). 'NoticePresenter'와 같은 부류다.
//
// ⚠️ 'Rewards'는 이번에 얻은 델타(연출용)다
// 인벤토리 수량은 'ItemChangeInfos'(누적 총량)로 'PlayerDataModel'이 이미 반영해 두었다.
// 여기서 다시 더하면 수량이 두 배가 된다 — 이 팝업은 오직 보여 주기만 한다.
public class GachaResultPresenter : MonoBehaviour, IPointerClickHandler
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("팝업 몸통의 CanvasGroup. alpha·blocksRaycasts로 표시/숨김한다(오브젝트는 끄지 않는다)")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("팝업 제목 — 띄울 때마다 경로에 맞춰 바꾼다('가챠 결과' · '상자 개봉 결과' · '우편 수령 결과')")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("보상 칸이 들어갈 부모 — GridLayoutGroup(5열)이 붙어 있다")]
    private Transform slotParent = null!;

    [SerializeField, Tooltip("보상 한 칸 프리팹 (SlotView 포함). 인벤토리 격자와 공유하는 칸이다")]
    private SlotView slotPrefab = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    [SerializeField, Tooltip("[n회 더 뽑기] 버튼 — 닫기 왼쪽. 가챠 결과에서만 보인다. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button drawAgainButton = null!;

    [SerializeField, Tooltip("[n회 더 뽑기] 버튼 문구 — 방금 뽑은 횟수로 코드가 채운다")]
    private TMP_Text drawAgainText = null!;

    [SerializeField, Tooltip("다시 뽑기를 맡길 가챠 패널 (길드 열)")]
    private GachaPresenter gacha = null!;

    [SerializeField, Tooltip("결과 창 상자(Panel) — 폭발 층을 이 위에 만들고, 전설↑ 공개 때 흔든다")]
    private RectTransform panel = null!;

    [SerializeField, Tooltip("하나씩 공개하는 연출의 조정값")]
    private RewardRevealSettings reveal = new RewardRevealSettings();

    // 만들어 둔 칸들. 뽑기 횟수가 1회와 10회를 오가므로 파괴하지 않고 켜고 끄며 돌려쓴다.
    private UIRowList<SlotView> _slots = null!;

    // 칸마다 붙인 공개 연출 — '_slots'와 같은 순서
    private readonly List<RewardRevealFx> _revealFxs = new List<RewardRevealFx>();

    // 이번 결과의 칸별 등급 — 공개 순서와 스킵이 읽는다
    private readonly List<GlobalRarity> _revealRarities = new List<GlobalRarity>();

    private RewardBurstFx?           _burst;
    private CancellationTokenSource? _revealCts;
    private Tween?                   _panelShake;
    private Vector2                  _panelRestPosition; // 흔들기를 끊었을 때 돌아갈 창 자리
    private bool                     _isRevealing;       // 하나씩 공개하는 중인가 — 스킵할 것이 있는가

    private RelicAppraiseFx?         _appraise;
    private CanvasGroup?             _panelGroup;        // 감정 무대 동안 창을 투명하게
    private CancellationTokenSource? _appraiseCts;
    private bool                     _isAppraising;      // 감정 무대가 도는 중인가 — 누르면 건너뛰고 끝 모습으로 간다
    private bool                     _isAppraiseHeld;    // 감정 무대가 끝 모습으로 머무는 중인가 — 누르면 닫는다

    // 이번 결과의 골드 칸 — 그 칸이 공개되면 동전이 상단 골드로 날아간다. 날렸거나 골드가 없으면 -1
    private int  _goldSlot = -1;
    private long _gold;

    private PlayerDataModel   _data = null!;
    private ServerWaitManager _wait = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(group,           nameof(group));
        this.RequireRef(titleText,       nameof(titleText));
        this.RequireRef(slotParent,      nameof(slotParent));
        this.RequireRef(slotPrefab,      nameof(slotPrefab));
        this.RequireRef(closeButton,     nameof(closeButton));
        this.RequireRef(drawAgainButton, nameof(drawAgainButton));
        this.RequireRef(drawAgainText,   nameof(drawAgainText));
        this.RequireRef(gacha,           nameof(gacha));
        this.RequireRef(panel,           nameof(panel));

        _data              = Services.Get<PlayerDataModel>();
        _wait              = Services.Get<ServerWaitManager>();
        _burst             = RewardBurstFx.Create(panel, reveal);
        _appraise          = RelicAppraiseFx.Create((RectTransform)group.transform, slotPrefab, titleText);
        _panelGroup        = panel.TryGetComponent(out CanvasGroup existing) ? existing : panel.gameObject.AddComponent<CanvasGroup>(); // ⚠️ '??'는 Unity의 가짜 null을 못 거른다
        _panelRestPosition = panel.anchoredPosition;
        _slots             = new UIRowList<SlotView>(slotPrefab, slotParent, OnSlotCreated, OnSlotHide);

        Subscribe();
        closeButton.onClick.AddListener(OnCloseClicked);
        drawAgainButton.onClick.AddListener(OnDrawAgainClicked);

        SetVisible(false); // 시작은 숨김 — 가챠·개봉 결과가 오면 켜진다
        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 · 공개 중이면 끊는다 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        CancelReveal();
        CancelAppraise();
    }

    #region 구독

    // 가챠·상자 개봉·우편 수령 성공 도착 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed             = true;
        _data.GachaCompleted     += OnGachaCompleted;
        _data.ItemUseCompleted   += OnItemUseCompleted;
        _data.MailRewardsClaimed += OnMailRewardsClaimed;
        gacha.DrawStateChanged   += RefreshDrawAgain;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed             = false;
        _data.GachaCompleted     -= OnGachaCompleted;
        _data.ItemUseCompleted   -= OnItemUseCompleted;
        _data.MailRewardsClaimed -= OnMailRewardsClaimed;
        gacha.DrawStateChanged   -= RefreshDrawAgain;
    }

    #endregion

    #region 결과 표시

    // 가챠 결과 도착 — 뽑힌 순서 그대로 늘어놓는다 (PlayerDataModel.GachaCompleted 구독)
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        Show(ToSlots(rewards), Lang.Get("UI.Gacha.Source"), showCount: false, GoldOf(rewards));
        ShowDrawAgain(true);
    }

    // 상자 개봉 결과 도착 — 종류별로 합쳐서 늘어놓는다 (PlayerDataModel.ItemUseCompleted 구독)
    //
    // ■ 왜 가챠와 달리 합치는가
    // 상자는 한 번에 'Constants.BoxOpenMax'(지금 50)개까지 깐다 — 낱개로 늘어놓으면 칸이 수백 개가 되어 화면이 그동안 잠긴다.
    // 사람이 알고 싶은 것도 "무엇을 얼마나 얻었나"이지 몇 번째로 무엇이 나왔는지가 아니다.
    // 가챠 쪽을 함께 합치지 않는 이유는, 거기가 **뽑힌 순서대로 하나씩 공개하는 연출**이
    // 들어올 자리이기 때문이다(T-031) — 지금 합쳐 두면 그때 되돌려야 한다.
    // ※ 유물 감정도 같은 요청이다(T-150) — 골드를 낸 것이면 칸 대신 감정 무대가 결과를 보여 준다(T-151 · #88).
    // ※ 제목은 연 물건을 따른다 — 상자는 '상자 개봉', 상자가 아닌 것(뽑기권·열쇠)은 '<이름> 개봉'(T-184).
    private void OnItemUseCompleted(int itemId, List<GachaRewardInfo> rewards, bool storedInMail)
    {
        bool   isAppraise = GameDataLoader.GetOpenCostGold(itemId) > 0L;
        string source     = isAppraise                                       ? Lang.Get("UI.Inventory.Appraise")
                          : GameDataLoader.GetItemType(itemId) == ItemType.Box ? Lang.Get("UI.Gacha.SourceBox")
                          : Lang.Format("UI.Gacha.SourceItem", GameDataLoader.GetItemName(itemId));

        ShowDrawAgain(false);

        if (isAppraise && rewards.Count > 0)
        {
            AppraiseAsync(itemId, rewards, source).Forget();

            return;
        }

        Show(Summarize(rewards), source, showCount: true, GoldOf(rewards));
    }

    // 우편 수령 도착 — 받은 우편들의 첨부를 종류별로 합쳐 늘어놓는다 (PlayerDataModel.MailRewardsClaimed 구독)
    //
    // ■ 왜 'GachaRewardInfo'로 바꾸지 않고 칸 값을 바로 만드나
    // 우편 첨부는 종류마다 필드가 따로 온다(Gold · Items · CharacterTids · EquipTids · Equips). 한 목록으로 옮겼다 되읽느니 바로 합친다.
    // ※ 모두 받기로 여러 통을 받으면 상자와 같은 이유로 합친다 — 몇 번째 우편에서 무엇이 나왔나는 우편함 목록에 남아 있다.
    // ※ 등급은 테이블에서 읽는다 — 우편 첨부에는 등급이 실려 오지 않는다(가챠 보상과 다른 점).
    private void OnMailRewardsClaimed(List<MailInfo> mails)
    {
        Show(SummarizeMails(mails), Lang.Get("UI.Gacha.SourceMail"), showCount: true, mails.Sum(mail => mail.Gold));
        ShowDrawAgain(false);
    }

    // 칸 값들을 그리고 팝업을 띄운다 (가챠·상자 개봉 공통).
    //
    // 보상이 비어 있으면 띄우지 않는다 — 빈 창이 뜨면 사용자가 닫기를 누를 때까지 화면이 막힌다.
    // 성공 응답에 보상이 없는 건 서버 쪽 이상이므로 경고만 남기고 조용히 지나간다.
    //   source    : 출처 — 제목('<출처> 결과')과 경고에 쓴다. 여러 경로가 같은 팝업을 쓰므로 어느 쪽인지 갈린다
    //   showCount : 칸에 수량을 적는가. 가챠는 뽑힌 것을 한 건씩 늘어놓아 개수가 목록 길이로 드러나지만,
    //               종류별로 합친 상자·우편은 수량을 적지 않으면 "골드"만 보이고 얼마인지 모른다
    //   gold      : 받은 골드 합계 — 골드 칸이 공개되면 그 칸에서 동전이 날아간다(0이면 없음)
    private void Show(List<SlotData> slots, string source, bool showCount, long gold)
    {
        if (slots.Count == 0)
        {
            ClientLogger.Warn(ClientLogger.UI, $"{source} 성공 응답에 보상이 비어 있다 — 결과 팝업을 띄우지 않는다.", this);

            return;
        }

        CancelReveal(); // 공개 중에 [n회 더 뽑기] 결과가 오면 앞의 공개를 끊고 새로 시작한다
        CancelAppraise();
        _revealRarities.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = _slots.Get(i);

            slot.SetSubVisible(showCount);
            slot.Bind(slots[i]);

            _revealFxs[i].Hide(); // 자리는 지키되 투명 — 창이 처음부터 다 자란 크기로 뜬다
            _revealRarities.Add(slots[i].Rarity);
        }

        _slots.HideFrom(slots.Count);

        // ※ 골드 칸은 'Key'가 0이다 — 골드는 TID가 없어 0으로 떨어뜨린다('TidOf' · 'SummarizeMails'). 0번 아이템·캐릭터는 없다
        _gold     = gold;
        _goldSlot = gold > 0L ? slots.FindIndex(slot => slot.Key == 0L) : -1;

        titleText.text = Lang.Format("UI.Gacha.ResultTitle", source);
        SetVisible(true);

        _revealCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        SetRevealing(true);
        RevealAsync(_revealCts.Token).Forget();
    }

    // 보상 중 골드의 합계 (결과 도착 때 호출 — 동전 연출의 양)
    private static long GoldOf(List<GachaRewardInfo> rewards)
    {
        long gold = 0L;

        foreach (GachaRewardInfo reward in rewards)
        {
            if (reward.RewardType == EGachaRewardType.Gold)
            {
                gold += reward.Count;
            }
        }

        return gold;
    }

    // 보상 목록을 받은 순서 그대로 칸 값으로 옮긴다 (OnGachaCompleted에서 호출).
    private static List<SlotData> ToSlots(List<GachaRewardInfo> rewards)
    {
        var slots = new List<SlotData>(rewards.Count);

        foreach (GachaRewardInfo reward in rewards)
        {
            slots.Add(ToSlotData(reward, reward.Count));
        }

        return slots;
    }

    // 같은 종류끼리 수량을 합쳐 칸 값으로 옮긴다 — 칸 수가 '뽑힌 건수'가 아니라 '종류 수'가 된다
    // (OnItemUseCompleted에서 호출).
    //
    // ⚠️ 묶는 열쇠는 TID 하나가 아니라 **보상 종류 + TID**다. 아이템 1001과 장비 1001은 다른 물건이라
    //   TID만으로 묶으면 조용히 한 칸으로 합쳐진다. 골드는 TID가 없어 종류 하나로 전부 모인다.
    // ※ 순서는 처음 나온 순서를 지킨다 — 등급이나 이름으로 정렬하면 "무엇이 먼저 나왔나"가 사라진다.
    //   받은 보상('GachaRewardInfo')은 고쳐 쓰지 않는다. 다른 구독자도 같은 객체를 보고 있다.
    private static List<SlotData> Summarize(List<GachaRewardInfo> rewards)
    {
        // 종류마다 처음 나온 보상(이름·등급의 출처)과 그 종류의 합계 수량을 나란히 쌓는다.
        var firsts  = new List<GachaRewardInfo>();
        var totals  = new List<long>();
        var indexOf = new Dictionary<(EGachaRewardType Type, int Tid), int>();

        foreach (GachaRewardInfo reward in rewards)
        {
            (EGachaRewardType Type, int Tid) key = (reward.RewardType, TidOf(reward));

            if (!indexOf.TryGetValue(key, out int index))
            {
                index = firsts.Count;

                indexOf.Add(key, index);
                firsts.Add(reward);
                totals.Add(0L);
            }

            totals[index] += reward.Count;
        }

        var slots = new List<SlotData>(firsts.Count);

        for (int i = 0; i < firsts.Count; i++)
        {
            slots.Add(ToSlotData(firsts[i], totals[i]));
        }

        return slots;
    }

    // 우편 첨부를 종류별로 합쳐 칸 값으로 옮긴다 (OnMailRewardsClaimed에서 호출).
    // 순서는 골드 → 아이템 → 캐릭터 → 장비, 각 안에서는 처음 나온 순서다.
    // ⚠️ 묶는 열쇠는 'Summarize'와 같이 **종류 + TID**다 — 아이템 1001과 장비 1001은 다른 물건이다.
    private static List<SlotData> SummarizeMails(List<MailInfo> mails)
    {
        long gold = 0L;

        var order  = new List<(char Kind, int Tid)>();
        var totals = new Dictionary<(char Kind, int Tid), long>();

        void Add(char kind, int tid, long count)
        {
            var key = (kind, tid);

            if (totals.TryGetValue(key, out long total))
            {
                totals[key] = total + count;
            }
            else
            {
                totals.Add(key, count);
                order.Add(key);
            }
        }

        foreach (MailInfo mail in mails)
        {
            gold += mail.Gold;

            if (mail.Items != null)
            {
                foreach (ItemInfo item in mail.Items)
                {
                    Add('I', item.ItemId, item.Count);
                }
            }

            if (mail.CharacterTids != null)
            {
                foreach (int tid in mail.CharacterTids)
                {
                    Add('C', tid, 1L);
                }
            }

            if (mail.EquipTids != null)
            {
                foreach (int tid in mail.EquipTids)
                {
                    Add('E', tid, 1L);
                }
            }

            // 개체 장비(경매 구매·반환)도 칸으로는 종류가 같으면 합친다 — 인챈트 차이는 인벤토리에서 본다.
            if (mail.Equips != null)
            {
                foreach (EquipInfo equip in mail.Equips)
                {
                    Add('E', equip.EquipTid, 1L);
                }
            }
        }

        var slots = new List<SlotData>(order.Count + 2);

        if (gold > 0L)
        {
            slots.Add(new SlotData(0L, Lang.Get("UI.Common.Gold"), gold.ToString("N0"), GlobalRarity.None));
        }

        foreach (var key in order)
        {
            string count = totals[key].ToString("N0");

            slots.Add(key.Kind switch
            {
                'C' => new SlotData(key.Tid, GameDataLoader.GetCharacterName(key.Tid), count, GameDataLoader.GetCharacterRarity(key.Tid), VisualCatalog.PortraitOf(key.Tid)),
                'E' => new SlotData(key.Tid, GameDataLoader.GetEquipName(key.Tid),     count, GameDataLoader.GetEquipRarity(key.Tid),     VisualCatalog.EquipIconOf(key.Tid)),
                _   => new SlotData(key.Tid, GameDataLoader.GetItemName(key.Tid),      count, GameDataLoader.GetItemRarity(key.Tid),      VisualCatalog.ItemIconOf(key.Tid)),
            });
        }

        return slots;
    }

    // 이 보상이 가리키는 **종류 번호**. 묶음 열쇠와 칸의 'Key'가 같은 값을 써야 해서 한곳에 모았다.
    //
    // ⚠️ 개체 번호가 아니다 — 개체 PK는 DB가 늦게 발급해서 이 응답에 실리지 않고,
    //   뒤이어 오는 'S_CharacterListResponse'·'S_EquipSyncResponse'로 온다.
    // ※ 골드는 TID가 없다. 0으로 떨어뜨려도 종류가 갈라 주므로 다른 보상과 섞이지 않는다.
    private static int TidOf(GachaRewardInfo reward) => reward.RewardType switch
    {
        EGachaRewardType.Character => reward.CharacterTid,
        EGachaRewardType.Equip     => reward.EquipTid,
        EGachaRewardType.Artifact  => reward.ArtifactTid,
        EGachaRewardType.Gold      => 0,
        _                          => reward.ItemId,
    };

    // 보상 하나를 칸에 그릴 값으로 옮긴다 (ToSlots · Summarize에서 호출).
    //   count : 칸에 적을 수량. 가챠는 보상 한 건의 수량이고, 상자는 그 종류의 합계다
    //
    // ⚠️ 'RewardType'이 어느 필드를 읽을지 정한다 — 분기하지 않고 'ItemId'만 읽으면 그 보상이
    //   빈 칸으로 그려지는데, 필드가 **추가만 된 형태라 컴파일도 경고도 통과한다.**
    //   실제로 캐릭터 축이 들어왔을 때 이 사고가 한 번 났다.
    // ※ 등급은 테이블을 다시 뒤지지 않고 패킷 값을 쓴다 — 두 값이 어긋났을 때 조용히 패킷 쪽을
    //   무시하게 된다. 캐릭터·장비도 아이템과 같은 등급 축이다.
    // ※ 골드는 등급이 'None'으로 와서 'RarityPalette'가 회색으로 떨어뜨린다 — 그대로 둔다.
    private static SlotData ToSlotData(GachaRewardInfo reward, long count)
    {
        GlobalRarity rarity = RarityPalette.ToTableRarity(reward.Rarity);
        int          tid    = TidOf(reward);

        string name = reward.RewardType switch
        {
            EGachaRewardType.Character => GameDataLoader.GetCharacterName(tid),
            EGachaRewardType.Equip     => GameDataLoader.GetEquipName(tid),
            EGachaRewardType.Artifact  => GameDataLoader.GetArtifactName(tid),
            EGachaRewardType.Gold      => Lang.Get("UI.Common.Gold"),
            _                          => GameDataLoader.GetItemName(tid),
        };

        Sprite? icon = reward.RewardType switch
        {
            EGachaRewardType.Character => VisualCatalog.PortraitOf(tid),
            EGachaRewardType.Equip     => VisualCatalog.EquipIconOf(tid),
            EGachaRewardType.Artifact  => VisualCatalog.ArtifactIconOf(tid), // 없는 번호는 칸이 이름 첫 글자로 그린다
            EGachaRewardType.Gold      => null,
            _                          => VisualCatalog.ItemIconOf(tid),
        };

        return new SlotData(tid, name, count.ToString("N0"), rarity, icon);
    }

    // 칸을 만들 때 한 번 — 공개 연출('RewardRevealFx')을 붙이고 클릭을 스킵으로 구독한다.
    // 칸 프리팹은 인벤토리와 공유라 손대지 않는다.
    //
    // ※ 수량 표시는 여기서 정하지 않는다 — 가챠(끔)와 상자·우편(켬)이 같은 칸을 돌려쓰므로
    //   'Show'가 띄울 때마다 정한다.
    private void OnSlotCreated(SlotView slot)
    {
        RewardRevealFx fx = slot.gameObject.AddComponent<RewardRevealFx>();

        fx.Init(reveal);

        slot.LeftClicked  += OnSlotClicked;
        slot.RightClicked += OnSlotClicked;

        _revealFxs.Add(fx); // 칸은 앞에서부터 하나씩 만들어지므로 순서가 '_slots'와 같다
    }

    // 이번 결과에 쓰이지 않은 칸을 끄기 직전 — 연출과 내용을 비운다.
    //
    // 10연차 뒤에 1회를 뽑으면 남은 아홉 칸이 이전 결과를 그대로 들고 있다.
    // 끄기 전에 'Clear'까지 하는 이유는 다음에 이 칸을 다시 켤 때 옛 값이 한 프레임 비치지 않게 하기 위해서다.
    private static void OnSlotHide(SlotView slot)
    {
        slot.GetComponent<RewardRevealFx>().Hide();
        slot.Clear();
    }

    #endregion

    #region 감정 무대

    // 감정 무대를 돌리고, 끝나거나 건너뛰면 보상이 놓인 채로 머문다 — 누르면 닫힌다 (OnItemUseCompleted에서 시작).
    //   relicTid : 감정한 유물 — 무대에 그린다. 수량은 받은 보상 건수(유물 하나 = 보상 하나)
    // ※ 건너뛰기는 토큰 취소지만 '_isAppraising'을 그대로 둔다 — 창 닫기 · 새 결과 · 꺼짐은 그것까지 내려 머물지 않는다.
    // ※ 보상이 여럿 와도(서버가 1개씩만 받는다 — 생길 일은 없다) 대표 하나만 세운다.
    private async UniTaskVoid AppraiseAsync(int relicTid, List<GachaRewardInfo> rewards, string source)
    {
        CancelReveal();
        CancelAppraise();

        _appraiseCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        GachaRewardInfo headline  = PickHeadline(rewards);
        GlobalRarity    rarity    = RarityPalette.ToTableRarity(headline.Rarity);
        bool            isArt     = headline.RewardType == EGachaRewardType.Artifact;
        bool            isSpRelic = !isArt && headline.RewardType == EGachaRewardType.Item && rarity == GlobalRarity.Special
                                    && GameDataLoader.GetOpenCostGold(headline.ItemId) > 0L;

        string? banner = isArt ? Lang.Get("UI.Gacha.ArtifactFound") : isSpRelic ? Lang.Get("UI.Gacha.SpecialRelicFound") : null;
        Color?  accent = isArt ? RarityPalette.Get(rarity) : isSpRelic ? Color.white : null;

        var relic = new SlotData(relicTid, GameDataLoader.GetItemName(relicTid), rewards.Count.ToString("N0"),
                                 GameDataLoader.GetItemRarity(relicTid), VisualCatalog.ItemIconOf(relicTid));

        titleText.text               = Lang.Format("UI.Gacha.ResultTitle", source);
        _panelGroup!.alpha           = 0f;
        _panelGroup.blocksRaycasts   = false; // 투명한 창의 칸·버튼이 클릭을 가로채지 않게 — 바탕(건너뛰기)이 받는다
        SetVisible(true);
        SetAppraising(true);

        CancellationTokenSource cts = _appraiseCts;

        SlotData reward = ToSlotData(headline, headline.Count);

        bool skipped = await _appraise!.PlayAsync(relic, reward, banner, accent, cts.Token)
                                       .SuppressCancellationThrow();

        // ⚠️ 건너뛰기면 한 프레임 쉬고 정리한다 — 여기는 UniTask가 트윈을 Kill하는 **그 Kill 안에서** 동기로 깨어난다.
        //   바로 'CancelAppraise'(→ 'DOTween.Kill(this)')를 부르면 죽는 중인 트윈을 또 Kill해 DOTween 목록이 깨진다
        //   ('IndexOutOfRangeException' — TweenManager.RemoveActiveTween · dotween 스킬 함정 표).
        if (skipped && await UniTask.Yield(PlayerLoopTiming.Update, destroyCancellationToken).SuppressCancellationThrow())
        {
            return; // 쉬는 사이 파괴됐다
        }

        // 닫기 · 새 결과 · 꺼짐 — 정리는 끊은 쪽이 했다. 쉬는 사이 새 감정이 시작됐으면 그쪽 것이다
        if (!_isAppraising || _appraiseCts != cts)
        {
            return;
        }

        if (skipped)
        {
            _appraise.ShowEnd(relic, reward, banner, accent);
        }

        // 무대는 그대로 두고 감정 중만 내린다 — 다음 클릭이 닫기다('OnPointerClick')
        SetAppraising(false);
        _isAppraiseHeld = true;

        long gold = GoldOf(rewards);

        if (gold > 0L)
        {
            _wait.RaiseGoldGain(_appraise.RewardTransform, gold);
        }
    }

    // 무대에 세울 대표 보상 — 아티팩트(높은 등급) → Special 유물 → 가장 높은 등급, 같으면 먼저 나온 것 (AppraiseAsync에서 호출)
    private static GachaRewardInfo PickHeadline(List<GachaRewardInfo> rewards)
    {
        GachaRewardInfo best      = rewards[0];
        int             bestScore = int.MinValue;

        foreach (GachaRewardInfo reward in rewards)
        {
            int score = (int)RarityPalette.ToTableRarity(reward.Rarity);

            if (reward.RewardType == EGachaRewardType.Artifact)
            {
                score += 1000;
            }

            if (score > bestScore)
            {
                best      = reward;
                bestScore = score;
            }
        }

        return best;
    }

    // 감정 무대 중인가를 바꾸고 닫기 잠금을 맞춘다 — 무대 중에는 닫지 못한다(누르면 건너뛴다)
    private void SetAppraising(bool on)
    {
        _isAppraising            = on;
        closeButton.interactable = !on;
    }

    // 감정 무대를 끊고 숨기고 창을 되돌린다 (새 결과 · 닫기 · OnDisable)
    private void CancelAppraise()
    {
        if (_isAppraising)
        {
            SetAppraising(false);
        }

        _isAppraiseHeld = false;

        _appraiseCts?.Cancel();
        _appraiseCts?.Dispose();
        _appraiseCts = null;

        if (_appraise == null)
        {
            return; // Start 전 — 무대가 돈 적이 없다
        }

        _appraise.Stop();
        _panelGroup!.alpha          = 1f;
        _panelGroup.blocksRaycasts  = true;
    }

    // 건너뛰기 — 무대를 끊는다. 뒤이어 'AppraiseAsync'가 끝 모습으로 놓는다 (OnPointerClick에서 호출)
    private void SkipAppraise()
    {
        _appraiseCts?.Cancel();
    }

    #endregion

    #region 공개 연출

    // 칸을 받은 순서대로 하나씩 공개한다 — 보통은 튀어나오고, 영웅↑은 멈칫 → 차오름 → 폭발 (Show에서 시작).
    //
    // ■ 고등급 연출이 뒤 칸을 막지 않는다 (2026-10-09 사용자 요청)
    // 칸마다 연출을 띄워 두고 기다리지 않은 채 'Gap'만큼 지나 다음 칸을 띄운다 — 영웅이 차오르는 동안에도 뒤 칸이 계속 나온다.
    // 공개가 끝난 때는 **마지막 칸이 뜬 때가 아니라 모든 칸의 연출이 끝난 때**다 — 그때 버튼이 풀린다.
    private async UniTask RevealAsync(CancellationToken ct)
    {
        var reveals = new List<UniTask>(_revealRarities.Count);

        await DelayAsync(reveal.StartDelay, ct);

        for (int i = 0; i < _revealRarities.Count; i++)
        {
            RewardRevealFx fx     = _revealFxs[i];
            GlobalRarity   rarity = _revealRarities[i];

            UniTask pop = RewardRevealSettings.IsBig(rarity) ? RevealBigAsync(fx, rarity, ct) : fx.PopAsync(rarity, ct);

            reveals.Add(i == _goldSlot ? FlyGoldAfterAsync(pop) : pop);

            await DelayAsync(reveal.Gap, ct);
        }

        await UniTask.WhenAll(reveals);

        SetRevealing(false);
    }

    // 골드 칸이 튀어나온 뒤 동전을 날린다 (RevealAsync에서 호출). 끊기면 날리지 않는다 — 스킵이 대신 날린다
    private async UniTask FlyGoldAfterAsync(UniTask pop)
    {
        await pop;

        FlyGold();
    }

    // 골드 칸에서 동전이 상단 골드로 날아간다 — 결과 하나에 한 번 (골드 칸 공개 · 스킵에서 호출)
    private void FlyGold()
    {
        if (_goldSlot < 0)
        {
            return;
        }

        Transform from = _revealFxs[_goldSlot].transform;

        _goldSlot = -1;
        _wait.RaiseGoldGain(from, _gold);
    }

    // 고등급 칸 하나 — 멈칫 → 차오름 → 폭발(+ 전설↑ 창 흔들기)과 함께 튀어나옴 (RevealAsync에서 호출)
    private async UniTask RevealBigAsync(RewardRevealFx fx, GlobalRarity rarity, CancellationToken ct)
    {
        bool isTop = RewardRevealSettings.IsTop(rarity);

        await DelayAsync(reveal.BigPause, ct);
        await fx.ChargeAsync(rarity, ct);

        _burst!.Play(fx.transform.position, RarityPalette.Get(rarity), isTop);

        if (isTop)
        {
            ShakePanel();
        }

        await fx.PopAsync(rarity, ct);
    }

    // 공개 중인가를 바꾸고 버튼 잠금을 맞춘다 — 공개 중에는 [n회 더 뽑기] · 닫기가 잠긴다 (Show · RevealAsync · CancelReveal에서 호출).
    // 결과를 다 보기 전에 창을 닫거나 새 결과로 덮지 못하게 한다(2026-10-09 사용자 요청). 다 보고 싶지 않으면 창을 눌러 스킵한다.
    private void SetRevealing(bool on)
    {
        _isRevealing = on;

        closeButton.interactable = !on;
        RefreshDrawAgain();
    }

    // 창을 흔든다 — 창 자리는 레이아웃이 아니라 이 Presenter 아래 고정 자리라 흔들어도 된다 (RevealBigAsync에서 호출)
    private void ShakePanel()
    {
        StopPanelShake();

        _panelShake = panel.DOShakeAnchorPos(reveal.PanelShakeDuration, reveal.PanelShakeStrength, 20, 90f, false, true)
                           .SetLink(gameObject);
    }

    // 창 흔들기를 끊고 제자리로 (ShakePanel · CancelReveal에서 호출)
    private void StopPanelShake()
    {
        if (_panelShake != null && _panelShake.IsActive())
        {
            _panelShake.Kill();
        }

        _panelShake            = null;
        panel.anchoredPosition = _panelRestPosition;
    }

    // 스킵 — 공개를 끊고 남은 칸까지 끝 모습으로 놓는다 (OnPointerClick · OnSlotClicked에서 호출)
    private void SkipReveal()
    {
        if (!_isRevealing)
        {
            return;
        }

        CancelReveal();

        for (int i = 0; i < _revealRarities.Count; i++)
        {
            _revealFxs[i].ShowAtRest(_revealRarities[i]);
        }

        FlyGold(); // 골드 칸이 아직 안 나왔으면 지금 — 건너뛰어도 받은 골드는 보여 준다
    }

    // 공개를 끊는다 — 칸은 그 자리 모습에 멈춘다. 끝 모습으로 놓는 것은 부르는 쪽 몫이다
    // (Show · SkipReveal · 닫기 · OnDisable에서 호출).
    // ⚠️ 토큰을 먼저 취소한다 — 기다리던 트윈은 UniTask가 Kill한다. 칸 쪽에서 다시 Kill하지 않는다(dotween 스킬 2장).
    private void CancelReveal()
    {
        if (_isRevealing)
        {
            SetRevealing(false);
        }

        _revealCts?.Cancel();
        _revealCts?.Dispose();
        _revealCts = null;

        if (_burst == null)
        {
            return; // Start 전 — 연출이 시작된 적이 없다
        }

        _burst.Stop();
        StopPanelShake();
    }

    // 공개 중이면 스킵한다 — 창 바탕·제목 클릭 (EventSystem 클릭 콜백). 버튼은 자기가 먼저 받는다.
    // 감정 무대가 끝 모습으로 머무는 중이면 닫는다 — 창(닫기 버튼)이 투명해 누를 곳이 바탕뿐이다.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isAppraising)
        {
            SkipAppraise();

            return;
        }

        if (_isAppraiseHeld)
        {
            OnCloseClicked();

            return;
        }

        SkipReveal();
    }

    // 칸 클릭 — 결과 창에서 칸의 뜻은 스킵 하나다 (SlotView.LeftClicked · RightClicked 구독)
    private void OnSlotClicked(SlotView slot)
    {
        SkipReveal();
    }

    // 연출 간격 기다리기 — 0이면 기다리지 않는다
    private static UniTask DelayAsync(float seconds, CancellationToken ct)
    {
        return seconds > 0f
            ? UniTask.Delay(TimeSpan.FromSeconds(seconds), cancellationToken: ct)
            : UniTask.CompletedTask;
    }

    #endregion

    #region 다시 뽑기

    // [n회 더 뽑기]를 보일지 정하고 문구·잠금을 맞춘다 (가챠·상자·우편 결과 도착 시).
    private void ShowDrawAgain(bool on)
    {
        drawAgainButton.gameObject.SetActive(on);

        if (on)
        {
            drawAgainText.text = Lang.Format("UI.Gacha.DrawAgain", gacha.LastDrawCount);
            RefreshDrawAgain();
        }
    }

    // 버튼 잠금을 가챠 패널 상태에 맞춘다 — 대기 중·골드 부족·패널이 꺼짐 · 공개 중이면 잠긴다
    // (GachaPresenter.DrawStateChanged 구독 · SetRevealing에서 호출)
    private void RefreshDrawAgain()
    {
        drawAgainButton.interactable = gacha.CanDrawAgain && !_isRevealing;
    }

    // [n회 더 뽑기] — 같은 풀을 같은 횟수로 다시 뽑는다. 팝업은 띄운 채로 두고 결과가 오면 새로 그린다 (drawAgainButton OnClick에 코드로 연결)
    private void OnDrawAgainClicked()
    {
        gacha.DrawAgain();
    }

    #endregion

    // 닫기 — 팝업만 내린다. 보상은 이미 인벤토리에 반영돼 있어 여기서 할 일이 없다 (closeButton OnClick에 코드로 연결)
    //
    // 공개 중이면 끊고 칸 연출을 걷는다 — 숨긴 창에서 강조 반복 트윈이 계속 돌지 않게.
    private void OnCloseClicked()
    {
        CancelReveal();
        CancelAppraise();

        foreach (RewardRevealFx fx in _revealFxs)
        {
            fx.Hide();
        }

        SetVisible(false);
    }

    // 몸통을 켜고 끈다 — 오브젝트는 항상 활성이라 이벤트를 계속 받는다(자기를 끄지 않는다).
    private void SetVisible(bool on)
    {
        group.alpha          = on ? 1f : 0f;
        group.blocksRaycasts = on; // 표시 중 뒤 UI 클릭 차단(모달)
        group.interactable   = on;
    }
}
