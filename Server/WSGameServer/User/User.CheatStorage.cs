using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    private (EResultCode, string) CheatSetCharacterLevel(long characterId, long requestedLevel, DateTime now)
    {
        if (characterId < 0 || requestedLevel < 1)
        {
            return (EResultCode.InvalidCheatArgs, "캐릭터 ID 또는 레벨 범위 오류");
        }

        var level = (int)Math.Min(requestedLevel, _characterLevels.MaxLevel);
        if (level > 1 && !_characterLevels.TryGetRequiredExp(level, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"성장표에 없는 레벨 {level}");
        }

        List<Character> targets;
        if (characterId == 0)
        {
            targets = _characters.Values.Concat(_warehouseCharacters.Values).ToList();
        }
        else if (TryGetCharacter(characterId, out var character))
        {
            targets = new List<Character> { character };
        }
        else
        {
            return (EResultCode.CharacterNotOwned, $"미보유 캐릭터 {characterId}");
        }

        if (targets.Count == 0)
        {
            return (EResultCode.Ok, "보유 캐릭터 없음");
        }

        // 레벨·장비 보정이 이미 진행된 채취에 소급되지 않도록 먼저 정산한다.
        SettleWorkStation(now);
        var changed = targets.Where(target => target.Level != level || target.Exp != 0).ToList();
        var targetIds = targets.Select(target => target.Id).ToHashSet();
        var removedGear = _worn.Where(pair => targetIds.Contains(pair.Key.CharacterId) && pair.Value.ItemLevel > level).ToArray();
        var removedMappings = new List<EquipMappingChange>(removedGear.Length);

        foreach (var target in changed)
        {
            target.SetLevel(level);
        }

        foreach (var (key, equip) in removedGear)
        {
            equip.TakeOff();
            _worn.Remove(key);
            removedMappings.Add(new EquipMappingChange(key.CharacterId, key.Slot, 0));
        }

        if (changed.Count > 0 || removedMappings.Count > 0)
        {
            PostDBTask(new SetCheatCharacterLevelsRepository(this, changed.Select(target => target.Id).ToArray(), level, removedMappings));
            foreach (var target in changed)
            {
                Send(new S_CharacterSyncResponse { Character = ToCharacterInfo(target) });
            }

            if (removedGear.Length > 0)
            {
                Send(new S_EquipSyncResponse { Equips = removedGear.Select(pair => ToEquipInfo(pair.Value)).ToList() });
            }

            ApplyWorkStationSpeed(notify: true);
        }

        return (EResultCode.Ok, $"캐릭터 {targets.Count}명 Lv{level} · 장착 해제 {removedGear.Length}개");
    }

    private (EResultCode, string) CheatClearItems(long target, long scope)
    {
        if (target is < 0 or > 3 || scope is < 0 or > 2)
        {
            return (EResultCode.InvalidCheatArgs, "삭제 종류(0~3) 또는 보관함(0~2) 범위 오류");
        }

        var itemChanges = new List<ItemChangeInfo>();
        var equips = new List<long>();
        var characters = new List<long>();

        if (scope != 1)
        {
            Collect(EContainer.Inventory, Inventory);
        }

        if (scope != 0)
        {
            Collect(EContainer.Warehouse, Warehouse);
        }

        // 판매와 같은 보호 규칙을 유지한다. 모든 캐릭터가 대상이면 가장 먼저 얻은 한 명을 남긴다.
        if (characters.Count > 0 && characters.Count == OwnedCharacterCount)
        {
            characters.Remove(characters.Min());
        }

        foreach (var equipId in equips)
        {
            RemoveEquip(equipId);
        }

        foreach (var characterId in characters)
        {
            RemoveCharacter(characterId);
        }

        if (itemChanges.Count + equips.Count + characters.Count > 0)
        {
            PostDBTask(new ClearCheatItemsRepository(this, itemChanges, equips, characters));
        }

        if (itemChanges.Count > 0)
        {
            Send(new S_UpdateItemResponse { Result = EResultCode.Ok, ItemChangeInfos = itemChanges });
        }

        if (equips.Count > 0)
        {
            SendEquipList();
        }

        if (characters.Count > 0)
        {
            SendCharacters();
        }

        return (EResultCode.Ok, $"자원 {itemChanges.Count}칸 · 장비 {equips.Count}개 · 캐릭터 {characters.Count}명 삭제");

        void Collect(EContainer container, Inventory resources)
        {
            if (target is 0 or 1)
            {
                var quantities = resources.Items.Where(item => !IsItemLocked(item.Id))
                    .GroupBy(item => item.Id)
                    .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
                if (quantities.Count > 0 && resources.TryRemoveItems(quantities, out var removed))
                {
                    itemChanges.AddRange(removed);
                }
            }

            if (target is 0 or 2)
            {
                equips.AddRange(EquipsOf(container).Values
                    .Where(equip => !equip.IsLocked && !equip.IsEquipped && !equip.HasPendingEnchant)
                    .Select(equip => equip.Id));
            }

            if (target is 0 or 3)
            {
                characters.AddRange(CharactersOf(container).Values
                    .Where(character => !character.IsLocked && !IsCharacterBusy(character.Id))
                    .Select(character => character.Id));
            }
        }
    }
}
