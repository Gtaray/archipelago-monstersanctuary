using FuzzySharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Behaviors
{
    public class ExploreAbilityItem : BaseItem
    {
        void Awake()
        {
            DontDestroyOnLoad(transform.gameObject);
        }

        public string Tooltip { get; set; }
        public List<string> Monsters { get; set; }

        public override string GetName()
        {
            return base.GetName();
        }

        public override string GetItemType()
        {
            return "Exploration Item";
        }

        public override string GetTooltip(int variation) => Tooltip;

        private string _icon;
        public override string GetIcon()
        {
            if (_icon == null)
            {
                var match = Process.ExtractOne(Name, GameData.ItemIcons);
                if (match.Score > 50)
                    _icon = match.Value;
                else
                    _icon = "icon_potion";
            }

            return _icon;
        }
    }
}
