using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 배치/해제 테스트 버튼 하나. 버튼 1개 + 산업 드롭다운 1개를 묶는다.
///
/// ■ 패킷은 하나로 끝난다
///   배치와 해제가 같은 <c>C_WorkStationAssignRequest</c>다. 산업·캐릭터를 0으로 주면 해제라
///   메서드를 따로 만들지 않는다.
///
/// ■ 버튼이 상태를 기억하지 않는다
///   "지금 배치돼 있는가"는 서버 스냅샷(<see cref="SessionManager.WorkStationSlots"/>)에서 읽는다.
///   버튼이 자체 플래그를 들면 실패 응답이 왔을 때 화면과 서버가 어긋난다.
/// </summary>
public class WorkStationTestButtonUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("배치/해제 토글 버튼")]
    private Button assignButton = null!;

    [SerializeField, Tooltip("버튼 라벨 — '배치' / '해제'로 바뀐다")]
    private TMP_Text buttonLabel = null!;

    [SerializeField, Tooltip("배치할 산업 선택. 항목은 코드가 채우므로 비워 둬도 된다")]
    private TMP_Dropdown industryDropdown = null!;

    [CenterHeader("< 설정 >")]
    [SerializeField, Tooltip("이 버튼이 담당할 슬롯 번호 (0부터)")]
    private int slotIndex = 0;

    // ⚠️ 배치에 넣는 캐릭터 값은 인스펙터에 적을 수 없다 — 서버가 발급한 개체 번호라 계정마다 다르다.
    //    로그인 때 받은 보유 목록(SessionManager.Characters)에서 꺼내 쓴다.
    //    캐릭터 선택 UI가 생기기 전까지는 첫 번째 캐릭터로 고정한다.

    // 드롭다운 항목 순서와 1:1로 대응하는 산업 목록. enum 값을 직접 인덱스로 쓰면
    // Misc·Special이 끼어 있어 어긋나므로 별도 목록으로 들고 있는다.
    private readonly List<ItemType> _industries = new List<ItemType>();

    private SessionManager _session = null!;
    private bool           _isSubscribed;

    // 서비스 확보 후 드롭다운 구성 + 구독 (Unity 메시지)
    // ※ OnEnable에서 Get 하지 않는다 — MonoService 주석의 초기화 순서 규칙 참조.
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다(WindowPanelUI와 같은 규칙).
        this.RequireRef(assignButton,     nameof(assignButton));
        this.RequireRef(buttonLabel,      nameof(buttonLabel));
        this.RequireRef(industryDropdown, nameof(industryDropdown));

        _session = Services.Get<SessionManager>();

        BuildIndustryOptions();
        assignButton.onClick.AddListener(OnAssignButtonClicked);

        Subscribe();
        Refresh();
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_session != null)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 슬롯 스냅샷 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed                    = true;
        _session.WorkStationSlotsChanged += Refresh;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed                    = false;
        _session.WorkStationSlotsChanged -= Refresh;
    }

    #endregion

    #region 표시

    // 채취 가능한 1차 산업만 드롭다운에 채운다 (Start에서 호출)
    private void BuildIndustryOptions()
    {
        _industries.Clear();

        // None·Misc·Special·Max는 배치 대상이 아니다. 채취하는 5종만 남긴다.
        foreach (ItemType industry in System.Enum.GetValues(typeof(ItemType)))
        {
            if (industry >= ItemType.Farming && industry <= ItemType.Hunting)
                _industries.Add(industry);
        }

        var labels = new List<string>(_industries.Count);
        foreach (var industry in _industries)
            labels.Add(industry.ToString());

        industryDropdown.ClearOptions();
        industryDropdown.AddOptions(labels);
    }

    // 슬롯 상태에 맞춰 라벨과 드롭다운 활성 여부를 맞춘다 (WorkStationSlotsChanged 구독)
    private void Refresh()
    {
        var  slot       = FindSlot();
        bool isAssigned = IsAssigned(slot);

        // 버튼이 8개라 라벨만 보고는 어느 슬롯인지 알 수 없다. 슬롯 번호를 항상 붙이고,
        // 배치 중일 때는 실제로 들어가 있는 캐릭터까지 보여 준다(인스펙터 값이 아니라 서버 값).
        buttonLabel.text = isAssigned
            ? $"{slotIndex}슬롯 {_session.GetCharacterName(slot!.CharacterId)} 해제"
            : $"{slotIndex}슬롯 배치";

        industryDropdown.interactable = !isAssigned; // 배치된 슬롯은 산업을 바꿀 수 없다
    }

    // 담당 슬롯의 현재 상태를 찾는다. 서버가 주지 않은 번호면 null (Refresh·클릭 처리에서 호출)
    private WorkStationSlotInfo? FindSlot()
    {
        foreach (var slot in _session.WorkStationSlots)
        {
            if (slot.SlotIndex == slotIndex)
                return slot;
        }

        return null; // 눌러 보면 실패 응답이 온다
    }

    // 슬롯이 배치 상태인가 — 산업과 캐릭터가 둘 다 차 있어야 배치다 (Refresh·클릭 처리에서 호출)
    private static bool IsAssigned(WorkStationSlotInfo? slot)
        => slot != null && slot.Industry != 0 && slot.CharacterId != 0;

    #endregion

    #region 송신

    // 배치 또는 해제 요청 (assignButton OnClick에 코드로 연결)
    private void OnAssignButtonClicked()
    {
        // 이미 배치돼 있으면 이 클릭은 해제다
        if (IsAssigned(FindSlot()))
        {
            _session.AssignWorkStation(slotIndex, 0, 0); // 산업·캐릭터 0 = 해제
            ClientLog.Info(ClientLog.Send, $"작업슬롯 해제 요청 — 슬롯={slotIndex}");
            return;
        }

        // 비어 있으면 배치
        // 드롭다운 항목은 BuildIndustryOptions가 채운다. 인스펙터에서 항목을 손으로 넣었거나
        // 산업 enum이 바뀌면 개수가 어긋날 수 있어, 인덱스로 꺼내기 전에 확인한다.
        int selected = industryDropdown.value;
        if (selected < 0 || selected >= _industries.Count)
        {
            ClientLog.Error(ClientLog.UI,
                $"산업 드롭다운 선택({selected})이 목록 범위(0~{_industries.Count - 1})를 벗어났다. " +
                $"드롭다운 항목을 코드가 채우도록 비워 둘 것.", this);
            return;
        }

        // 서버는 캐릭터 종류(TID)가 아니라 개체 번호를 받는다. 보유 목록에서 꺼낸다.
        long characterId = _session.FirstCharacterId;
        if (characterId == 0)
        {
            ClientLog.Error(ClientLog.UI,
                "보유 캐릭터가 없어 배치할 수 없다 — 로그인이 끝났는지, 서버가 시작 캐릭터를 지급했는지 확인할 것.", this);
            return;
        }

        ItemType industry = _industries[selected];
        _session.AssignWorkStation(slotIndex, (byte)industry, characterId);
        ClientLog.Info(ClientLog.Send, $"작업슬롯 배치 요청 — 슬롯={slotIndex}, 산업={industry}, 캐릭터개체={characterId}");
    }

    #endregion
}
