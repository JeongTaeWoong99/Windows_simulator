using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

// 가챠 결과 팝업 — 이번에 뽑힌 보상을 5열로 늘어놓고, 닫기를 누르면 사라진다.
// 칸은 인벤토리와 같은 프리팹('InventorySlotView')을 쓴다.
//
// ■ 왜 거래 열이 아니라 '!System Canvas'인가
// 이 팝업은 'PlayerDataModel.GachaCompleted'를 스스로 구독해서 뜬다 — 나를 켜 줄 주체가 밖에 없다.
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

    [SerializeField, Tooltip("보상 칸이 들어갈 부모 — GridLayoutGroup(5열)이 붙어 있다")]
    private Transform slotParent = null!;

    [SerializeField, Tooltip("보상 한 칸 프리팹 (InventorySlotView 포함). 인벤토리와 같은 프리팹이다")]
    private InventorySlotView slotPrefab = null!;

    [SerializeField, Tooltip("닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    // 만들어 둔 칸들. 뽑기 횟수가 1회와 10회를 오가므로 파괴하지 않고 켜고 끄며 돌려쓴다.
    private readonly List<InventorySlotView> _slots = new List<InventorySlotView>();

    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(group,       nameof(group));
        this.RequireRef(slotParent,  nameof(slotParent));
        this.RequireRef(slotPrefab,  nameof(slotPrefab));
        this.RequireRef(closeButton, nameof(closeButton));

        _data = Services.Get<PlayerDataModel>();

        Subscribe();
        closeButton.onClick.AddListener(OnCloseClicked);

        SetVisible(false); // 시작은 숨김 — 가챠 결과가 오면 켜진다
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

    // 가챠 성공 도착 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed        = true;
        _data.GachaCompleted += OnGachaCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed        = false;
        _data.GachaCompleted -= OnGachaCompleted;
    }

    #endregion

    #region 결과 표시

    // 뽑힌 보상을 그리고 팝업을 띄운다 (PlayerDataModel.GachaCompleted 구독)
    //
    // 보상이 비어 있으면 띄우지 않는다 — 빈 창이 뜨면 사용자가 닫기를 누를 때까지 화면이 막힌다.
    // 성공 응답에 보상이 없는 건 서버 쪽 이상이므로 경고만 남기고 조용히 지나간다.
    private void OnGachaCompleted(List<GachaRewardInfo> rewards)
    {
        if (rewards.Count == 0)
        {
            ClientLogger.Warn(ClientLogger.UI, "가챠 성공 응답에 보상이 비어 있다 — 결과 팝업을 띄우지 않는다.", this);

            return;
        }

        for (int i = 0; i < rewards.Count; i++)
        {
            GachaRewardInfo reward = rewards[i];
            InventorySlotView slot = GetOrCreateSlot(i);

            slot.gameObject.SetActive(true);
            slot.Bind(ToSlotData(reward));
        }

        HideSlotsFrom(rewards.Count);
        SetVisible(true);
    }

    // 보상 하나를 칸에 그릴 값으로 옮긴다 (OnGachaCompleted에서 호출).
    //
    // ⚠️ 'RewardType'이 어느 TID 필드를 읽을지 정한다 — 아이템이면 'ItemId', 캐릭터면 'CharacterTid'이고
    //   나머지 하나는 0이다. 분기하지 않고 'ItemId'만 읽으면 캐릭터 보상이 빈 칸으로 그려지는데,
    //   필드가 추가만 된 형태라 컴파일도 경고도 통과한다.
    // ※ 등급은 테이블을 다시 뒤지지 않고 패킷 값을 쓴다 — 두 값이 어긋났을 때 조용히 패킷 쪽을
    //   무시하게 된다. 캐릭터도 아이템과 같은 등급 축이다.
    // ※ 캐릭터 이름은 **종류(TID)** 로 읽는다. 개체 번호가 아니다 — 개체 PK는 DB가 늦게 발급해서
    //   이 응답에 실리지 않고, 뒤이어 오는 'S_CharacterListResponse'로 온다.
    private static StorageSlotData ToSlotData(GachaRewardInfo reward)
    {
        GlobalRarity rarity = RarityPalette.ToTableRarity(reward.Rarity);

        if (reward.RewardType == EGachaRewardType.Character)
        {
            return new StorageSlotData(reward.CharacterTid,
                                       GameDataLoader.GetCharacterName(reward.CharacterTid),
                                       reward.Count.ToString(),
                                       rarity);
        }

        return new StorageSlotData(reward.ItemId,
                                   GameDataLoader.GetItemName(reward.ItemId),
                                   reward.Count.ToString(),
                                   rarity);
    }

    // 'index'번째 칸을 돌려준다. 아직 없으면 그때 만든다 (OnGachaCompleted에서 호출)
    //
    // ※ 만드는 순간 수량 표시를 끈다 — 여기서는 뽑힌 것을 그대로 늘어놓으므로 개수가
    //   칸이 아니라 목록의 길이로 드러난다. 만들 때 한 번이면 되고, 'Clear'는 이 결정을 되돌리지 않는다.
    private InventorySlotView GetOrCreateSlot(int index)
    {
        while (_slots.Count <= index)
        {
            InventorySlotView slot = Instantiate(slotPrefab, slotParent);

            slot.SetSubVisible(false);
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
