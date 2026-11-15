using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 자동 사용 칸 하나 — 영약 아이콘 + 이름 · ON/OFF와 보유 개수 (이슈 #113 · #123 · T-194).
// 클릭 = 켜고 끄기(2026-11-15 사용자 결정 — 개수를 넣고 빼는 단계가 어색했다). 무엇을 할지는 'SlotAutoUsePresenter'가 정하고 칸은 클릭만 올린다.
public class AutoUseCellView : MonoBehaviour
{
    private const float OffIconAlpha = 0.4f; // 꺼진 칸은 아이콘을 흐리게 — 글자 없이도 켜짐을 구분한다

    [CenterHeader("참조")]
    [SerializeField, Tooltip("칸 버튼 — 클릭(켜고 끄기). OnClick은 코드가 연결한다")]
    private Button button = null!;

    [SerializeField, Tooltip("영약 아이콘 — 그림이 없으면 끈다")]
    private Image iconImage = null!;

    [SerializeField, Tooltip("영약 이름 — '신속의 영약'")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("켜짐 표시 + 보유 개수 — 'ON ×12' · 'OFF'")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("마우스를 올리면 효과·켜짐·보유 개수·조작 안내를 띄운다")]
    private TooltipTrigger tooltip = null!;

    public int ItemTid { get; private set; }

    public event Action<AutoUseCellView>? Clicked;

    // 필수 참조 검증 · 클릭 배선 (Unity 메시지) — 견본이 꺼진 채라 복제본이 켜질 때 돈다
    private void Awake()
    {
        this.RequireRef(button,    nameof(button));
        this.RequireRef(iconImage, nameof(iconImage));
        this.RequireRef(nameText,  nameof(nameText));
        this.RequireRef(countText, nameof(countText));
        this.RequireRef(tooltip,   nameof(tooltip));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 칸을 그린다 ('SlotAutoUsePresenter.Refresh'가 호출).
    //   isOn     : 자동 사용이 켜졌나
    //   owned    : 인벤토리·창고 보유 개수 — 켜졌을 때만 적는다
    //   provider : 툴팁 내용 — 띄우는 순간에 부른다
    public void Bind(int itemTid, bool isOn, int owned, Func<TooltipContent?> provider)
    {
        ItemTid = itemTid;

        Sprite? icon = VisualCatalog.ItemIconOf(itemTid);

        iconImage.sprite  = icon!;
        iconImage.enabled = icon != null;
        iconImage.color   = new Color(1f, 1f, 1f, isOn ? 1f : OffIconAlpha);
        nameText.text     = GameDataLoader.GetItemName(itemTid);

        if (isOn)
        {
            countText.SetText("ON ×{0}", owned);
        }
        else
        {
            countText.SetText("OFF");
        }

        tooltip.SetProvider(provider);
    }

    // 요청 응답을 기다리는 동안 누르지 못하게 한다 ('SlotAutoUsePresenter'가 호출)
    public void SetInteractable(bool on)
    {
        button.interactable = on;
    }
}
