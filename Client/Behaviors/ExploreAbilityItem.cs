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
        public override string GetName()
        {
            return base.GetName();
        }

        public override string GetItemType()
        {
            return "Exploration Item";
        }

        public override string GetTooltip(int variation) => Tooltip;

        public override string GetIcon() => "icon_potion";
    }
}
