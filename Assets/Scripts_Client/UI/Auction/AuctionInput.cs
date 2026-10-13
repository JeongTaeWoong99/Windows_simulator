using TMPro;

// 경매장 입력칸(수량·단가)의 범위 맞추기 — 넘치게 넣으면 최대값으로 바로 고쳐 준다.
//
// ■ 위로 넘친 값은 입력 중에 바로 고친다 ('ClampMax' — onValueChanged)
//   6개 가진 자원에 10을 넣으면 곧바로 6이 된다. 자리를 더 칠수록 값은 커지기만 하므로 치는 도중에 고쳐도 안전하다.
// ■ 아래로 모자란 값은 입력이 끝났을 때만 고친다 ('ClampMin' — onEndEdit)
//   하한 150을 치려고 '1'을 누른 순간 150으로 바꾸면 이어 칠 수가 없다.
public static class AuctionInput
{
    // 입력값이 'max'를 넘으면 'max'로 고친다. 고쳤으면 true — 부르는 쪽이 이어서 표시를 갱신한다.
    // 비었거나 숫자가 아니면 건드리지 않는다. 'max'가 1보다 작으면(살 것·올릴 것이 없다) 고치지 않는다.
    public static bool ClampMax(TMP_InputField input, long max)
    {
        if (max < 1L || !long.TryParse(input.text, out long value) || value <= max)
        {
            return false;
        }

        SetValue(input, max);

        return true;
    }

    // 입력값이 'min'보다 작으면 'min'으로 고친다. 비었거나 숫자가 아니면 'min'을 넣는다.
    public static bool ClampMin(TMP_InputField input, long min)
    {
        if (long.TryParse(input.text, out long value) && value >= min)
        {
            return false;
        }

        SetValue(input, min);

        return true;
    }

    // 값을 넣고 커서를 끝으로 옮긴다. ★ 알림 없이 넣는다 — onValueChanged 안에서 부르므로 다시 불리지 않게.
    private static void SetValue(TMP_InputField input, long value)
    {
        input.SetTextWithoutNotify(value.ToString());
        input.stringPosition = input.text.Length;
    }
}
