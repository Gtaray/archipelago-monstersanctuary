using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Behaviors
{
    public class ExploreAbilityItem : ArchipelagoItem
    {
        void Awake()
        {
            DontDestroyOnLoad(transform.gameObject);
        }

        public override string GetItemType()
        {
            return "Exploration Item";
        }
    }
}
