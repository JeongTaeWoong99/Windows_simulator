using System;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 아이콘을 어디서 가져올지 — "이 TID는 원본 팩의 이 스프라이트". 'Assets/Art/icons/icon_recipe.asset' 하나다.
    //
    // 굽기가 스프라이트의 그림 부분만 잘라 'icons/items/item_<TID>.png' · 'icons/equips/equip_<TID>.png'로 쓰고,
    // 목록 동기화('ArtCatalogSync')가 파일 이름의 TID로 목록에 올린다.
    // 레시피 없이 파일을 직접 넣어도 된다 — 레시피에 같은 TID가 있으면 굽기가 덮는다.
    [CreateAssetMenu(menuName = "DesktopWindowControl/Art Recipe/Icons", fileName = "icon_recipe")]
    public class IconRecipe : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("ItemTable · EquipTable의 TID")]
            public int tid;

            [Tooltip("원본 스프라이트 — 투명 테두리는 굽기가 잘라 낸다")]
            public Sprite? sprite;
        }

        [SerializeField, Tooltip("자원·특수 아이템 (ItemTable TID)")]
        private Entry[] items = Array.Empty<Entry>();

        [SerializeField, Tooltip("장비 (EquipTable TID)")]
        private Entry[] equips = Array.Empty<Entry>();

        public Entry[] Items  => items;
        public Entry[] Equips => equips;

        [ContextMenu("이 레시피 굽기")]
        private void Bake() => ArtBaker.Bake(this);
    }
}
