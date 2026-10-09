using System;
using System.Collections.Generic;
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
// 공개 중에는 [n회 더 뽑기] · 닫기가 잠긴다 — 결과를 다 보기 전에 창을 닫거나 새 결과로 덮지 못한다.
// 공개 중에 창 아무 곳(칸 포함)을 누르면 **스킵** — 남은 칸이 한꺼번에 끝 모습으로 놓이고 버튼이 풀린다.
// 칸 클릭은 'SlotView'가 먼저 받으므로('IPointerClickHandler') 칸의 클릭 이벤트도 함께 구독해 스킵으로 읽는다.
//
// ■ 왜 거래 열이 아니라 '!System Canvas'인가
// 이 팝업은 'PlayerDataModel'의 결과 이벤트를 스스로 구독해서 뜬다 — 나를 켜 줄 주체가 밖에 없다.
// 거래 열의 자식으로 두면 요청을 보낸 뒤 열을 닫는 순간 결과가 통째로 사라진다.
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

    [SerializeField, Tooltip("다시 뽑기를 맡길 가챠 패널 (거래 열)")]
    private GachaPresenter gacha = null!;

    [SerializeField, Tooltip("결과 창 상자(Panel) — 폭발 층을 이 위에 만들고, 전설↑ 공개 때 흔든다")]
    private RectTransform panel = null!;

    [SerializeField, Tooltip("하나씩 공개하는 연출의 조정값")]
    private RewardRevealSettings reveal = new RewardRevealSettings();

    // 만들어 둔 칸들. 뽑기 횟수가 1회와 10회를 오가므로 파괴하지 않고 켜고 끄며 돌려쓴다.
    private readonly List<SlotView> _slots = new List<SlotView>();

    // 칸마다 붙인 공개 연출 — '_slots'와 같은 순서
    private readonly List<RewardRevealFx> _revealFxs = new List<RewardRevealFx>();

    // 이번 결과의 칸별 등급 — 공개 순서와 스킵이 읽는다
    private readonly List<GlobalRarity> _revealRarities = new List<GlobalRarity>();

    private RewardBurstFx?           _burst;
    private CancellationTokenSource? _revealCts;
    private Tween?                   _panelShake;
    private Vector2                  _panelRestPosition; // 흔들기를 끊었을 때 돌아갈 창 자리
    private bool                     _isRevealing;       // 하나씩 공개하는 중인가 — 스킵할 것이 있는가

    private PlayerDataModel _data = null!;

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
        _burst             = RewardBurstFx.Create(panel, reveal);
        _panelRestPosition = panel.anchoredPosition;

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
        Show(ToSlots(rewards), "가챠", showCount: false);
        ShowDrawAgain(true);
    }

    // 상자 개봉 결과 도착 — 종류별로 합쳐서 늘어놓는다 (PlayerDataModel.ItemUseCompleted 구독)
    //
    // ■ 왜 가챠와 달리 합치는가
    // 상자는 한 번에 'Constants.BoxOpenMax'(지금 50)개까지 깐다 — 낱개로 늘어놓으면 칸이 수백 개가 되어 화면이 그동안 잠긴다.
    // 사람이 알고 싶은 것도 "무엇을 얼마나 얻었나"이지 몇 번째로 무엇이 나왔는지가 아니다.
    // 가챠 쪽을 함께 합치지 않는 이유는, 거기가 **뽑힌 순서대로 하나씩 공개하는 연출**이
    // 들어올 자리이기 때문이다(T-031) — 지금 합쳐 두면 그때 되돌려야 한다.
    private void OnItemUseCompleted(List<GachaRewardInfo> rewards, bool storedInMail)
    {
        Show(Summarize(rewards), "상자 개봉", showCount: true);
        ShowDrawAgain(false);
    }

    // 우편 수령 도착 — 받은 우편들의 첨부를 종류별로 합쳐 늘어놓는다 (PlayerDataModel.MailRewardsClaimed 구독)
    //
    // ■ 왜 'GachaRewardInfo'로 바꾸지 않고 칸 값을 바로 만드나
    // 우편 첨부는 종류마다 필드가 따로 온다(Gold · Items · CharacterTids · EquipTids · Equips). 한 목록으로 옮겼다 되읽느니 바로 합친다.
    // ※ 모두 받기로 여러 통을 받으면 상자와 같은 이유로 합친다 — 몇 번째 우편에서 무엇이 나왔나는 우편함 목록에 남아 있다.
    // ※ 등급은 테이블에서 읽는다 — 우편 첨부에는 등급이 실려 오지 않는다(가챠 보상과 다른 점).
    private void OnMailRewardsClaimed(List<MailInfo> mails)
    {
        Show(SummarizeMails(mails), "우편 수령", showCount: true);
        ShowDrawAgain(false);
    }

    // 칸 값들을 그리고 팝업을 띄운다 (가챠·상자 개봉 공통).
    //
    // 보상이 비어 있으면 띄우지 않는다 — 빈 창이 뜨면 사용자가 닫기를 누를 때까지 화면이 막힌다.
    // 성공 응답에 보상이 없는 건 서버 쪽 이상이므로 경고만 남기고 조용히 지나간다.
    //   source    : 출처 — 제목('<출처> 결과')과 경고에 쓴다. 여러 경로가 같은 팝업을 쓰므로 어느 쪽인지 갈린다
    //   showCount : 칸에 수량을 적는가. 가챠는 뽑힌 것을 한 건씩 늘어놓아 개수가 목록 길이로 드러나지만,
    //               종류별로 합친 상자·우편은 수량을 적지 않으면 "골드"만 보이고 얼마인지 모른다
    private void Show(List<SlotData> slots, string source, bool showCount)
    {
        if (slots.Count == 0)
        {
            ClientLogger.Warn(ClientLogger.UI, $"{source} 성공 응답에 보상이 비어 있다 — 결과 팝업을 띄우지 않는다.", this);

            return;
        }

        CancelReveal(); // 공개 중에 [n회 더 뽑기] 결과가 오면 앞의 공개를 끊고 새로 시작한다
        _revealRarities.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = GetOrCreateSlot(i);

            slot.gameObject.SetActive(true);
            slot.SetSubVisible(showCount);
            slot.Bind(slots[i]);

            _revealFxs[i].Hide(); // 자리는 지키되 투명 — 창이 처음부터 다 자란 크기로 뜬다
            _revealRarities.Add(slots[i].Rarity);
        }

        HideSlotsFrom(slots.Count);
        titleText.text = $"{source} 결과";
        SetVisible(true);

        _revealCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        SetRevealing(true);
        RevealAsync(_revealCts.Token).Forget();
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
            slots.Add(new SlotData(0L, "골드", gold.ToString("N0"), GlobalRarity.None));
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
            EGachaRewardType.Gold      => "골드",
            _                          => GameDataLoader.GetItemName(tid),
        };

        Sprite? icon = reward.RewardType switch
        {
            EGachaRewardType.Character => VisualCatalog.PortraitOf(tid),
            EGachaRewardType.Equip     => VisualCatalog.EquipIconOf(tid),
            EGachaRewardType.Gold      => null,
            _                          => VisualCatalog.ItemIconOf(tid),
        };

        return new SlotData(tid, name, count.ToString("N0"), rarity, icon);
    }

    // 'index'번째 칸을 돌려준다. 아직 없으면 그때 만든다 (OnGachaCompleted에서 호출)
    //
    // ※ 수량 표시는 여기서 정하지 않는다 — 가챠(끔)와 상자·우편(켬)이 같은 칸을 돌려쓰므로
    //   'Show'가 띄울 때마다 정한다.
    //
    // 칸을 만들 때 공개 연출('RewardRevealFx')을 붙이고 클릭을 스킵으로 구독한다 — 칸 프리팹은 인벤토리와 공유라 손대지 않는다.
    private SlotView GetOrCreateSlot(int index)
    {
        while (_slots.Count <= index)
        {
            SlotView       slot = Instantiate(slotPrefab, slotParent);
            RewardRevealFx fx   = slot.gameObject.AddComponent<RewardRevealFx>();

            fx.Init(reveal);

            slot.LeftClicked  += OnSlotClicked;
            slot.RightClicked += OnSlotClicked;

            _slots.Add(slot);
            _revealFxs.Add(fx);
        }

        return _slots[index];
    }

    // 이번 결과에 쓰이지 않은 칸을 비우고 끈다 (OnGachaCompleted에서 호출)
    //
    // 10연차 뒤에 1회를 뽑으면 남은 아홉 칸이 이전 결과를 그대로 들고 있다.
    // 끄기 전에 'Clear'까지 하는 이유는 다음에 이 칸을 다시 켤 때 옛 값이 한 프레임 비치지 않게 하기 위해서다.
    private void HideSlotsFrom(int startIndex)
    {
        for (int i = startIndex; i < _slots.Count; i++)
        {
            _revealFxs[i].Hide();
            _slots[i].Clear();
            _slots[i].gameObject.SetActive(false);
        }
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

            reveals.Add(RewardRevealSettings.IsBig(rarity) ? RevealBigAsync(fx, rarity, ct) : fx.PopAsync(rarity, ct));

            await DelayAsync(reveal.Gap, ct);
        }

        await UniTask.WhenAll(reveals);

        SetRevealing(false);
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
    public void OnPointerClick(PointerEventData eventData)
    {
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
            drawAgainText.text = $"{gacha.LastDrawCount}회 더 뽑기";
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
