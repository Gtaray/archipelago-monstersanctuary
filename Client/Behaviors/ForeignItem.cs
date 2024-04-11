using FuzzySharp;
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

        private string _icon;
        public override string GetIcon()
        {
            if (_icon == null)
            {
                var match = Process.ExtractOne(Name, GameData.ItemIcons);
                if (match.Score > 65)
                    _icon = match.Value;
                else
                {
                    switch (Classification)
                    {
                        case ItemClassification.Progression:
                            _icon = "icon_key";
                            break;
                        case ItemClassification.Useful:
                            _icon = "icon_lootbox";
                            break;
                        case ItemClassification.Trap:
                            _icon = "icon_shuriken";
                            break;
                        case ItemClassification.Filler:
                        default:
                            _icon = "icon_potion";
                            break;
                    }
                }
            }

            return _icon;
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
