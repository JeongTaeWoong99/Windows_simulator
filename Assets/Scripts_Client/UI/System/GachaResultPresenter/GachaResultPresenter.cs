using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보상 결과 팝업 — 이번에 얻은 것을 5열로 늘어놓고, 닫기를 누르면 사라진다.
// 칸은 창고와 **공유하는** 프리팹('SlotView')이다 — 인벤토리 전용이 아니다.
// ⚠️ 그래서 창고 쪽을 고치면 이 팝업도 함께 바뀐다.
//
// ■ 가챠와 상자 개봉이 같은 팝업을 쓴다
// 서버가 상자 개봉 결과를 가챠와 **같은 모양**('GachaRewardInfo')으로 내려주기 때문이다.
// 연출을 두 벌 만들면 한쪽만 고쳐지므로 여기 하나로 둔다(T-033).
// 늘어놓는 방식만 갈린다 — 가챠는 뽑힌 순서 그대로, 상자는 종류별 합계다('OnItemUseCompleted').
// 우편 수령도 여기로 온다 — 받은 우편의 첨부를 상자처럼 종류별로 합친다('OnMailRewardsClaimed').
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
public class GachaResultPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("팝업 몸통의 CanvasGroup. alpha·blocksRaycasts로 표시/숨김한다(오브젝트는 끄지 않는다)")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("팝업 제목 — 띄울 때마다 경로에 맞춰 바꾼다('가챠 결과' · '상자 개봉 결과' · '우편 수령 결과')")]
    private TMP_Text titleText = null!;

    [SerializeField, Tooltip("보상 칸이 들어갈 부모 — GridLayoutGroup(5열)이 붙어 있다")]
    private Transform slotParent = null!;

    [SerializeField, Tooltip("보상 한 칸 프리팹 (SlotView 포함). 창고 격자와 공유하는 칸이다")]
    private SlotView slotPrefab = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    // 만들어 둔 칸들. 뽑기 횟수가 1회와 10회를 오가므로 파괴하지 않고 켜고 끄며 돌려쓴다.
    private readonly List<SlotView> _slots = new List<SlotView>();

    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(group,       nameof(group));
        this.RequireRef(titleText,   nameof(titleText));
        this.RequireRef(slotParent,  nameof(slotParent));
        this.RequireRef(slotPrefab,  nameof(slotPrefab));
        this.RequireRef(closeButton, nameof(closeButton));

        _data = Services.Get<PlayerDataModel>();

        Subscribe();
        closeButton.onClick.AddListener(OnCloseClicked);

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

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
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
    }

    #endregion

    #region 결과 표시

    // 가챠 결과 도착 — 뽑힌 순서 그대로 늘어놓는다 (PlayerDataModel.GachaCompleted 구독)
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        Show(ToSlots(rewards), "가챠", showCount: false);
    }

    // 상자 개봉 결과 도착 — 종류별로 합쳐서 늘어놓는다 (PlayerDataModel.ItemUseCompleted 구독)
    //
    // ■ 왜 가챠와 달리 합치는가
    // 상자는 한 번에 99개까지 깐다 — 낱개로 늘어놓으면 칸이 수백 개가 되어 화면이 그동안 잠긴다.
    // 사람이 알고 싶은 것도 "무엇을 얼마나 얻었나"이지 몇 번째로 무엇이 나왔는지가 아니다.
    // 가챠 쪽을 함께 합치지 않는 이유는, 거기가 **뽑힌 순서대로 하나씩 공개하는 연출**이
    // 들어올 자리이기 때문이다(T-031) — 지금 합쳐 두면 그때 되돌려야 한다.
    private void OnItemUseCompleted(List<GachaRewardInfo> rewards, bool storedInMail)
    {
        Show(Summarize(rewards), "상자 개봉", showCount: true);
    }

    // 우편 수령 도착 — 받은 우편들의 첨부를 종류별로 합쳐 늘어놓는다 (PlayerDataModel.MailRewardsClaimed 구독)
    //
    // ■ 왜 'GachaRewardInfo'로 바꾸지 않고 칸 값을 바로 만드나
    // 우편에는 다이아가 붙는데 'EGachaRewardType'에 다이아가 없다. 억지로 끼우면 패킷 enum을 클라가 늘려야 한다.
    // ※ 모두 받기로 여러 통을 받으면 상자와 같은 이유로 합친다 — 몇 번째 우편에서 무엇이 나왔나는 우편함 목록에 남아 있다.
    // ※ 등급은 테이블에서 읽는다 — 우편 첨부에는 등급이 실려 오지 않는다(가챠 보상과 다른 점).
    private void OnMailRewardsClaimed(List<MailInfo> mails)
    {
        Show(SummarizeMails(mails), "우편 수령", showCount: true);
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

        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = GetOrCreateSlot(i);

            slot.gameObject.SetActive(true);
            slot.SetSubVisible(showCount);
            slot.Bind(slots[i]);
        }

        HideSlotsFrom(slots.Count);
        titleText.text = $"{source} 결과";
        SetVisible(true);
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
    // 순서는 골드 → 다이아 → 아이템 → 캐릭터 → 장비, 각 안에서는 처음 나온 순서다.
    // ⚠️ 묶는 열쇠는 'Summarize'와 같이 **종류 + TID**다 — 아이템 1001과 장비 1001은 다른 물건이다.
    private static List<SlotData> SummarizeMails(List<MailInfo> mails)
    {
        long gold = 0L;
        long dia  = 0L;

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
            dia  += mail.Dia;

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

            // 개체 장비(경매 구매·반환)도 칸으로는 종류가 같으면 합친다 — 인챈트 차이는 창고에서 본다.
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

        if (dia > 0L)
        {
            slots.Add(new SlotData(0L, "다이아", dia.ToString("N0"), GlobalRarity.None));
        }

        foreach (var key in order)
        {
            string count = totals[key].ToString("N0");

            slots.Add(key.Kind switch
            {
                'C' => new SlotData(key.Tid, GameDataLoader.GetCharacterName(key.Tid), count, GameDataLoader.GetCharacterRarity(key.Tid)),
                'E' => new SlotData(key.Tid, GameDataLoader.GetEquipName(key.Tid),     count, GameDataLoader.GetEquipRarity(key.Tid)),
                _   => new SlotData(key.Tid, GameDataLoader.GetItemName(key.Tid),      count, GameDataLoader.GetItemRarity(key.Tid)),
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

        return new SlotData(tid, name, count.ToString("N0"), rarity);
    }

    // 'index'번째 칸을 돌려준다. 아직 없으면 그때 만든다 (OnGachaCompleted에서 호출)
    //
    // ※ 수량 표시는 여기서 정하지 않는다 — 가챠(끔)와 상자·우편(켬)이 같은 칸을 돌려쓰므로
    //   'Show'가 띄울 때마다 정한다.
    private SlotView GetOrCreateSlot(int index)
    {
        while (_slots.Count <= index)
        {
            SlotView slot = Instantiate(slotPrefab, slotParent);

            _slots.Add(slot);
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
            _slots[i].Clear();
            _slots[i].gameObject.SetActive(false);
        }
    }

    #endregion

    // 닫기 — 팝업만 내린다. 보상은 이미 인벤토리에 반영돼 있어 여기서 할 일이 없다 (closeButton OnClick에 코드로 연결)
    private void OnCloseClicked()
    {
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
