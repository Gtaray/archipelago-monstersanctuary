using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Behaviors
{
    public class ForeignItem : BaseItem
    {
        public string Player { get; set; }
        public ItemClassification Classification => GetComponent<RandomizedShopItem>().Classification;

        public override string GetIcon()
        {
            switch (Classification)
            {
                case ItemClassification.Progression:
                    return "icon_key";
                case ItemClassification.Useful:
                    return "icon_lootbox";
                case ItemClassification.Filler:
                    return "icon_potion";
                case ItemClassification.Trap:
                    return "icon_shuriken";
                default:
                    return "icon_potion";
            }
        }

        public override string GetName()
        {
            return $"{Name} ({Player})";
        }

        public override string GetItemType()
        {
            return Enum.GetName(typeof(ItemClassification), Classification);
        }

        public override string GetTooltip(int variation)
        {
            return $"{Patcher.FormatPlayer(Player)}'s {Patcher.FormatItem(Name, Classification)}";
        }
    }
}
