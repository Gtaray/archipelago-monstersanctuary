using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Behaviors
{
    public enum TrapTrigger
    {
        CombatStart,
        PlayerTurnStart,
        EnemyTurnStart
    }

    public class TrapItem : ArchipelagoItem
    {
        public TrapTrigger Trigger { get; set; }

        void Awake()
        {
            DontDestroyOnLoad(transform.gameObject);
        }

        public override string GetItemType()
        {
            return "Trap";
        }

        public int GetPriority()
        {
            if (Name == "Death Trap")
                return 0;

            if (Name == "Ambush Trap")
                return 5;

            if (Name == "Flash-Bang Trap")
                return 10;

            return 15;
        }
    }
}
