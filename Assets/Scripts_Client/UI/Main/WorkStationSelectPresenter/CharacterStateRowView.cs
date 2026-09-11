using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 목록의 한 줄. 캐릭터 하나의 상태를 보여 주고 배치 버튼을 갖는다.
//
// 눌리면 'AssignClicked'만 쏜다 — 슬롯 번호도 고른 산업도 로그인 여부도 이 줄은 모른다.
// 이름처럼 변환이 필요한 값은 'Bind'로 완성된 문구를 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ■ 이 줄은 적성을 모른다
// 못 하는 캐릭터는 목록에서 걸러지므로 여기까지 오지 않는다('WorkStationSelectPresenter.RefreshRows').
// 예전에는 적성 0이면 버튼을 잠갔는데, 관문이 둘이면 다음에 보는 사람이 어느 쪽이 진짜인지 알 수 없다.
//
// ■ 파괴하지 않고 풀로 되돌린다
// 목록을 오갈 때마다 만들고 부수면 상주 앱에서 GC가 쌓인다. 남는 줄은 'Clear()' 후 꺼 둔다.
public class CharacterStateRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("캐릭터 이름·레벨·적성 등 한 줄 설명")]
    private TMP_Text infoText = null!;

    [SerializeField, Tooltip("배치 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button assignButton = null!;

    [SerializeField, Tooltip("배치 버튼 라벨")]
    private TMP_Text assignLabel = null!;

    // 이 줄의 배치를 눌렀다 ('WorkStationSelectPresenter'가 구독).
    public event Action<CharacterStateRowView>? AssignClicked;

    // 이 줄이 그리고 있는 캐릭터 개체 번호. 미바인딩이면 0.
    public long CharacterId { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널이 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(infoText,     nameof(infoText));
        this.RequireRef(assignButton, nameof(assignButton));
        this.RequireRef(assignLabel,  nameof(assignLabel));

        assignButton.onClick.AddListener(() => AssignClicked?.Invoke(this));
    }

    // 이 줄이 그릴 캐릭터를 정한다 ('WorkStationSelectPresenter'가 호출).
    //
    // ※ 이 줄은 배치만 한다 — 해제는 3단계 'Character Setting Panel'의 몫이라
    // 라벨이 "해제"로 바뀌는 경우가 없다.
    //   characterId : 서버가 발급한 개체 번호. 배치 요청에 그대로 실린다
    //   info        : 이름·적성처럼 이미 완성된 표시 문구
    public void Bind(long characterId, string info)
    {
        CharacterId      = characterId;
        infoText.text    = info;
        assignLabel.text = "배치";
    }

    // 버튼을 잠그거나 푼다 — 응답을 기다리는 동안만 잠긴다.
    public void SetAssignable(bool on)
    {
        assignButton.interactable = on;
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.
    public void Clear()
    {
        CharacterId   = 0;
        infoText.text = "";
    }
}
