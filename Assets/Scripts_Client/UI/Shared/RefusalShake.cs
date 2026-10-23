using DG.Tweening;
using UnityEngine;

// 거절 흔들기 — 안 되는 조작을 하면 누른 버튼·칸이 좌우로 짧게 갸웃거린다 (T-177).
// 대상에 스크립트를 붙이지 않는다 — 'ToastPresenter'가 거절 토스트를 띄울 때 대상을 넘겨 부른다.
//
// ■ 위치가 아니라 회전으로 흔든다
//   버튼·칸은 대부분 레이아웃 그룹 아래라 위치를 그룹이 다시 잡는다 — 흔드는 도중 다시 배치되면 엉뚱한 자리로 돌아간다.
//   회전은 레이아웃이 건드리지 않고, 버튼 눌림('ButtonPressFx')의 크기 트윈과도 겹치지 않는다.
//
// ■ 수명 — 트윈은 대상에 묶는다(KillOnDisable). 도중에 꺼지거나 다음 흔들기가 끊어도 'OnKill'이 회전을 되돌린다.
public static class RefusalShake
{
    // 기울기(도) · 시간 · 떨림 횟수
    private const float Angle    = 7f;
    private const float Duration = 0.35f;
    private const int   Vibrato  = 10;

    // 한 번에 하나만 흔든다 — 새 거절이 오면 앞 흔들기를 끝 상태로 끊는다
    private static Tween? s_tween;

    public static void Play(Transform target)
    {
        if (s_tween != null && s_tween.IsActive())
        {
            s_tween.Kill(); // OnKill이 앞 대상의 회전을 되돌린다
        }

        Quaternion rest = target.localRotation;

        s_tween = target.DOPunchRotation(new Vector3(0f, 0f, Angle), Duration, Vibrato, 0.5f)
                        .SetUpdate(true)
                        .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                        .OnKill(() =>
                        {
                            if (target != null)
                            {
                                target.localRotation = rest;
                            }
                        });
    }
}
