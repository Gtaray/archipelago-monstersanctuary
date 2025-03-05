using Archipelago.MonsterSanctuary.Client.AP;
using Archipelago.MultiClient.Net.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Archipelago.MonsterSanctuary.Client.Behaviors
{
    [Serializable]
    public class ArchipelagoItem : BaseItem
    {
        void Awake()
        {
            DontDestroyOnLoad(transform.gameObject);
        }

        public string Description { get; set; }

        public string Icon { get; set; }

        public string Type { get; set; }

        public ItemClassification Classification { get; set; }

        public override string GetName()
        {
            return base.GetName();
        }

        public override string GetItemType()
        {
            return "Archipelago Item";
        }

        public override string GetTooltip(int variation) => Description;

        public override string GetIcon()
        {
            return Icon;
        }
    }
}
