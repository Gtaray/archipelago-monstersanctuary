using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client
{
    public enum ItemTransferType
    {
        Aquired = 0,
        Received = 1,
        Sent = 2
    }
    public class ItemTransfer
    {
        public string ItemName { get; set; }
        public string PlayerName { get; set; }
        public ItemTransferType Action { get; set; }

        public int? ItemIndex { get; set; }
        public long ItemID { get; set; }
        public int PlayerID { get; set; }
        public long LocationID { get; set; }
        public string LocationName { get; set; }
        public ItemClassification Classification { get; set; }
    }
}
