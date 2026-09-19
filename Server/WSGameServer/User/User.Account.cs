using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>계정(섬주인) 레벨. 캐릭터가 얻은 경험치가 그대로 쌓여 오른다(특성 3장).</summary>
    public int AccountLevel { get; private set; } = 1;

    /// <summary>현재 레벨에서 쌓은 계정 경험치. 곡선이 캐릭터의 8배라 long이다.</summary>
    public long AccountExp { get; private set; }

    /// <summary>남은(안 쓴) 특성 포인트. 레벨업으로 받고 특성 노드를 찍을 때 쓴다.</summary>
    public int TraitPoint { get; private set; }

    /// <summary>DB에서 읽은 계정 행을 적재한다(로그인 시 1회). 행이 없으면 레벨 1 — 재화처럼 가입 시 행을 만들지 않는다.</summary>
    public void LoadAccount(AccountRow? row)
    {
        AccountLevel = row?.level ?? 1;
        AccountExp   = row?.exp ?? 0;
        TraitPoint   = row?.trait_point ?? 0;
    }

    /// <summary>계정 경험치를 더한다. 레벨이 오르면 도달한 레벨의 특성 포인트를 준다. 바뀐 것이 있으면 저장하고 밀어 준다.</summary>
    public void GainAccountExp(long amount, bool notify)
    {
        if (amount <= 0 || AccountLevel >= _accountLevels.MaxLevel)
        {
            return;
        }

        AccountExp += amount;

        while (AccountLevel < _accountLevels.MaxLevel &&
               _accountLevels.TryGetRequiredExp(AccountLevel + 1, out var required) &&
               AccountExp >= required)
        {
            AccountExp -= required;
            AccountLevel++;
            TraitPoint += _accountLevels.TraitPointAt(AccountLevel);
        }

        // 만렙에 닿으면 남은 조각은 버린다 — 캐릭터 레벨과 같은 규칙이다.
        if (AccountLevel >= _accountLevels.MaxLevel)
        {
            AccountExp = 0;
        }

        SaveAccount(notify);
    }

    /// <summary>특성 포인트를 쓴다. 모자라면 아무것도 바꾸지 않고 false.</summary>
    private bool TrySpendTraitPoint(int cost)
    {
        if (cost < 0 || TraitPoint < cost)
        {
            return false;
        }

        TraitPoint -= cost;
        SaveAccount(notify: true);
        return true;
    }

    /// <summary>계정 레벨 스냅샷을 보낸다(로그인 직후).</summary>
    public void SendAccountLevel()
    {
        Send(new S_AccountLevelResponse { Level = AccountLevel, Exp = AccountExp, TraitPoint = TraitPoint });
    }

    private void SaveAccount(bool notify)
    {
        PostDBTask(new SaveAccountRepository(this, AccountLevel, AccountExp, TraitPoint));

        if (notify)
        {
            SendAccountLevel();
        }
    }
}
