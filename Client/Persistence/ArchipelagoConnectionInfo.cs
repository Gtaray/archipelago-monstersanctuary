using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Persistence
{
    public class ArchipelagoConnectionInfo
    {
        [JsonProperty("HostName")]
        public string HostName { get; set; }

        [JsonProperty("SlotName")]
        public string SlotName { get; set; }

        [JsonProperty("Password")]
        public string Password { get; set; }
    }
}
