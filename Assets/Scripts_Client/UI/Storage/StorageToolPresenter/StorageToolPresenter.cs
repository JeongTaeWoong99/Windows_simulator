using System.Collections.Generic;
using GameData;
using MikaProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 창고 탭 줄 아래의 도구 줄 — 정렬 방향 화살표와 일괄 담기(범위 + 버튼).
//
// 탭 줄보다 낮은 별도 줄이다. 탭은 "무엇을 보는가", 이 줄은 "그것을 어떻게 다루는가"라
// 같은 줄에 두면 네 탭 사이에 성격이 다른 버튼이 끼어든다.
//
// ■ 특성 탭에서는 줄째 숨는다
//   특성은 정렬할 목록도, 팔 것도 없다. 잠긴 버튼만 남겨 두면 "지금 못 하는 것"이 계속 눈에 남는다
//   (탭 버튼을 잠그는 것과는 판단이 다르다 — 거기서는 **탭 자체가 선택지**라 사라지면 무엇이 있는지 알 수 없다).
//
// ■ 일괄 담기는 '담기'까지다
//   버튼을 눌러도 팔리지 않는다. 판매 목록에 **보유 수량 전부**를 담을 뿐이고, 파는 것은 아래 [판매]다.
//   우클릭 담기와 같은 자리에 쌓이므로 담긴 뒤 하나씩 빼는 것도 그대로 된다.
//
// ■ 일괄 담기는 '더하기'가 아니라 '다시 잡기'다
//   누를 때마다 목록을 **비우고** 고른 범위만 담는다. 그래야 화면의 `○○ 이하`와 팔릴 것이 늘 같다.
public class StorageToolPresenter : MonoBehaviour
{
    // 드롭다운에 세울 등급. 'None'과 'Max'를 뺀 실제 등급만, 낮은 것부터.
    private static readonly GlobalRarity[] SellRarities =
    {
        GlobalRarity.Common,
        GlobalRarity.Uncommon,
        GlobalRarity.Rare,
        GlobalRarity.Epic,
        GlobalRarity.Legendary,
        GlobalRarity.Mythic,
    };

    // 등급의 한글 이름. ⚠️ 임시 사본이다 — 표시 이름의 출처는 엑셀로 옮겨 간다(일감 'T-047').
    private static readonly string[] RarityNames =
    {
        "일반",
        "고급",
        "희귀",
        "영웅",
        "전설",
        "신화",
    };

    [CenterHeader("참조")]
    [SerializeField, Tooltip("정렬 방향 버튼 (정사각형). OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button sortButton = null!;

    [SerializeField, Tooltip("정렬 방향 화살표 — ▼(등급 높은 순) · ▲(낮은 순). 🎨 스프라이트가 오면 Image로 바꾼다")]
    private TMP_Text sortArrowText = null!;

    [SerializeField, Tooltip("일괄 담기 범위 — '○○ 이하'. 목록은 코드가 채운다")]
    private TMP_Dropdown bulkRarityDropdown = null!;

    [SerializeField, Tooltip("일괄 담기 버튼 (정사각형). OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button bulkSellButton = null!;

    [SerializeField, Tooltip("탭 줄 — 지금 어느 탭인지 여기서 듣는다")]
    private StorageTabPresenter tabs = null!;

    [SerializeField, Tooltip("칸 격자 — 정렬을 지시할 상대")]
    private StorageGridPresenter grid = null!;

    // 등급 높은 순으로 시작한다 — 값나가는 것이 위에 오는 쪽이 기본이다.
    private StorageSortOrder _order = StorageSortOrder.Descending;

    private PlayerDataModel   _data = null!;
    private SellCartModel     _cart = null!;
    private ServerWaitManager _wait = null!;

    private bool _isSubscribed;

    // 참조 확보 → 구독 → 배선 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(sortButton,         nameof(sortButton));
        this.RequireRef(sortArrowText,      nameof(sortArrowText));
        this.RequireRef(bulkRarityDropdown, nameof(bulkRarityDropdown));
        this.RequireRef(bulkSellButton,     nameof(bulkSellButton));
        this.RequireRef(tabs,               nameof(tabs));
        this.RequireRef(grid,               nameof(grid));

        _data = Services.Get<PlayerDataModel>();
        _cart = Services.Get<SellCartModel>();
        _wait = Services.Get<ServerWaitManager>();

        Subscribe();

        BuildRarityOptions();

        sortButton.onClick.AddListener(OnSortClicked);
        bulkSellButton.onClick.AddListener(OnBulkSellClicked);

        RefreshArrow();

        // ★ 탭 줄의 'Start'가 먼저 돌았으면 이벤트를 이미 놓쳤다 — 지금 값을 물어 맞춘다.
        //   유니티는 두 'Start'의 순서를 보장하지 않는다.
        ApplyTab(tabs.CurrentTab);
    }

    // 구독 해제 (Unity 메시지)
    private void OnDestroy()
    {
        Unsubscribe();
    }

    #region 구독

    // 탭 전환 구독 (Start에서 한 번).
    //
    // ⚠️ 'OnEnable/OnDisable'이 아니라 'Start/OnDestroy'다 — 이 줄은 **특성 탭에서 스스로 꺼진다.**
    //   꺼질 때 구독을 놓으면 다시 켤 신호('TabChanged')를 받을 길이 사라져 영영 안 돌아온다
    //   ('StorageGridPresenter'가 로그인 구독을 'OnDestroy'에서 푸는 것과 같은 이유다).
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed   = true;
        tabs.TabChanged += ApplyTab;
    }

    // 구독 해제 (OnDestroy에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed   = false;
        tabs.TabChanged -= ApplyTab;
    }

    #endregion

    #region 초기화

    // 드롭다운에 '○○ 이하' 목록을 채운다 (Start에서 한 번).
    //
    // 씬에 적어 두지 않는 이유 — 등급이 늘면 목록과 'SellRarities'가 따로 놀고,
    // 어느 쪽이 진짜인지 눌러 보기 전에는 알 수 없다.
    private void BuildRarityOptions()
    {
        bulkRarityDropdown.ClearOptions();

        var options = new List<string>(SellRarities.Length);

        foreach (string name in RarityNames)
        {
            options.Add($"{name} 이하");
        }

        bulkRarityDropdown.AddOptions(options);
        bulkRarityDropdown.SetValueWithoutNotify(0); // 일반 이하 — 가장 안전한 쪽에서 시작한다
        bulkRarityDropdown.RefreshShownValue();
    }

    #endregion

    #region 정렬

    // 정렬 방향을 뒤집고 지금 탭을 다시 줄 세운다 (Sort Button OnClick에 코드로 연결)
    private void OnSortClicked()
    {
        _order = _order == StorageSortOrder.Descending
            ? StorageSortOrder.Ascending
            : StorageSortOrder.Descending;

        RefreshArrow();
        grid.SortCurrent(_order);
    }

    // 화살표를 지금 방향으로 맞춘다 (Start · 방향을 바꿀 때).
    //
    // ▼가 "위에서 아래로 낮아진다"(등급 높은 순)다 — 방향이 곧 목록의 모양이다.
    private void RefreshArrow()
    {
        sortArrowText.text = _order == StorageSortOrder.Descending ? "▼" : "▲";
    }

    #endregion

    #region 일괄 담기

    // 고른 등급 이하의 자원을 보유 수량 전부 판매 목록에 담는다 (Bulk Sell Button OnClick에 코드로 연결)
    //
    // 팔지 않고 담기까지만 한다 — 확인 절차는 아래 [판매] 버튼이다('Storage 규칙.md').
    private void OnBulkSellClicked()
    {
        // ⏸ 캐릭터·장비는 팔 수 없다 — 서버 판매 패킷이 아이템 TID 축이고, 값(BasePrice)도 없다.
        //   버튼을 잠그는 대신 왜 안 되는지 알린다. 잠가 두면 "고장 났나"가 되고, 이유는 어디에도 안 남는다.
        if (tabs.CurrentTab != StorageTab.Resource)
        {
            _wait.RaiseNotice("자원만 판매할 수 있습니다. 캐릭터·장비 판매는 서버·기획 작업을 기다리는 중입니다.");

            return;
        }

        // ★ 먼저 비운다 — 이 버튼은 "더 담기"가 아니라 **범위를 다시 잡는 것**이다.
        //   비우지 않으면 영웅 이하로 담았다가 일반 이하로 다시 누를 때 영웅·희귀가 그대로 남아,
        //   화면의 범위(`일반 이하`)와 실제로 팔릴 것이 어긋난다.
        //   ⚠️ 우클릭으로 하나씩 담아 둔 것도 함께 빠진다. 범위를 다시 잡는다는 뜻이 그것이다.
        _cart.Clear();

        GlobalRarity limit = SelectedRarity;
        int          added = 0;

        foreach (ItemInfo item in _data.Inventory)
        {
            if (item.Count <= 0)
            {
                continue; // 서버가 0개가 된 아이템도 실어 보낸다
            }

            if (GameDataLoader.GetItemRarity(item.ItemId) > limit)
            {
                continue;
            }

            _cart.Add(item.ItemId, item.Count);
            added++;
        }

        if (added == 0)
        {
            _wait.RaiseNotice($"{RarityNames[bulkRarityDropdown.value]} 이하로 담을 자원이 없습니다.");

            return;
        }

        ClientLogger.Info(ClientLogger.UI, $"일괄 담기 — {limit} 이하 {added}종을 판매 목록에 담았다");
    }

    // 드롭다운이 가리키는 등급. 목록과 값 배열이 어긋나면 가장 낮은 등급으로 떨어진다.
    private GlobalRarity SelectedRarity
    {
        get
        {
            int index = bulkRarityDropdown.value;

            return index >= 0 && index < SellRarities.Length ? SellRarities[index] : GlobalRarity.Common;
        }
    }

    #endregion

    #region 표시

    // 이 탭에서 도구 줄을 보일지 정한다 (Start · TabChanged 구독).
    //
    // 특성 탭에서는 **줄째** 감춘다 — 정렬할 목록도 팔 것도 없다.
    // 자식만 끄면 배경과 줄 높이(40)가 빈 띠로 남는다. 자기를 끄면 부모 세로 배치에서 아예 빠진다.
    //
    // 스스로 꺼져도 구독은 살아 있다 — 구독을 'Start/OnDestroy'에 건 이유가 이것이다('Subscribe' 주석).
    private void ApplyTab(StorageTab tab)
    {
        gameObject.SetActive(tab != StorageTab.Trait);
    }

    #endregion
}
