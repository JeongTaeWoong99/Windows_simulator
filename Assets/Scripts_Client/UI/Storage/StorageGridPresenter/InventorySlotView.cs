using GameData;
using TMPro;
using UnityEngine;
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
public class InventorySlotView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("등급 배경. 칸 전체를 덮는다 — 색만 등급에 따라 바뀐다")]
    private Image rarityImage = null!;

    [SerializeField, Tooltip("아이콘. 아이콘 에셋이 아직 없어 지금은 비어 있다")]
    private Image itemImage = null!;

    [SerializeField, Tooltip("칸 이름 — 아이템 이름 · 캐릭터 이름")]
    private TMP_Text nameText = null!;

    // ※ 'Count Text'(countText)에서 이름이 바뀐 자리다. 자원은 수량, 캐릭터는 배치 상태가 들어와
    //   더 이상 '수량 칸'이 아니다 — 무엇이 오든 "이름 아래 한 줄"이라는 자리로 부른다.
    //   프리팹은 새 이름으로 이미 저장했으므로 [FormerlySerializedAs]는 걷어냈다.
    [SerializeField, Tooltip("이름 아래 보조 문구 — 자원은 수량, 캐릭터는 배치 상태")]
    private TMP_Text subText = null!;

    // 이 칸이 그리고 있는 대상. 자원은 ItemId, 캐릭터는 개체 번호. 비어 있으면 0.
    public long Key { get; private set; }

    // 아무것도 그리고 있지 않은 빈 칸인가.
    public bool IsEmpty => Key == 0;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 부르는 Presenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(rarityImage, nameof(rarityImage));
        this.RequireRef(itemImage,   nameof(itemImage));
        this.RequireRef(nameText,    nameof(nameText));
        this.RequireRef(subText,     nameof(subText));
    }

    // 완성된 표시값을 그린다 ('StorageGridPresenter'가 호출).
    //
    // 이름도 등급도 여기서 조회하지 않는다 — 출처가 탭마다 다르기 때문이다.
    // 자원은 'ItemTable', 캐릭터는 'CharacterTable', 가챠 보상은 패킷이 등급을 실어 온다.
    // 칸이 한쪽을 골라 버리면 다른 쪽이 조용히 무시된다.
    //
    // TODO: 아이콘 에셋이 생기면 'itemImage.sprite'를 여기서 채운다.
    //       등급 이미지가 생기면 'rarityImage'도 색 대신 sprite를 넣는다 ('RarityPalette' 참조).
    public void Bind(in StorageSlotData data)
    {
        Key               = data.Key;
        rarityImage.color = RarityPalette.Get(data.Rarity);
        nameText.text     = data.Name;
        subText.text      = data.Sub;
    }

    // 아이템을 표시한다 ('GachaResultPresenter'가 호출).
    //
    // 등급을 받는 이유 — 창고는 테이블에서 읽고 가챠 보상은 패킷이 실어 온다.
    // 이름은 출처가 'ItemTable' 하나뿐이라 여기서 조회해도 어긋날 데가 없다.
    public void Bind(int itemId, int count, GlobalRarity rarity)
    {
        Bind(new StorageSlotData(itemId, GameDataLoader.GetItemName(itemId), count.ToString(), rarity));
    }

    // 보조 문구를 켜고 끈다 (칸을 만든 화면이 한 번만 부른다).
    //
    // 창고는 "몇 개 갖고 있나 · 배치 중인가"가 칸의 핵심이라 켜 두고, 가챠 결과는 뽑힌 것을
    // 그대로 늘어놓는 자리라 끈다 — 거기서는 개수가 칸이 아니라 목록의 길이로 드러난다.
    // ※ Clear는 이 상태를 되돌리지 않는다 — 풀에서 재사용돼도 화면의 결정이 유지돼야 한다.
    public void SetSubVisible(bool on)
    {
        subText.gameObject.SetActive(on);
    }

    // 칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    // ※ 등급색도 되돌린다 — 안 그러면 다음에 이 칸을 쓸 때 이전 등급의 색이 잠깐 남는다.
    public void Clear()
    {
        Key               = 0;
        rarityImage.color = RarityPalette.Unknown;
        nameText.text     = "";
        subText.text      = "";
    }
}
