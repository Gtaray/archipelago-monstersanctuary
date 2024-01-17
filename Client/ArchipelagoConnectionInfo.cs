using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Archipelago.MonsterSanctuary.Client
{
    public class ArchipelagoConnectionInfo
    {
        public string host_name;
        public string slot_name;
        public string password;

        public bool Valid => !(string.IsNullOrEmpty(host_name) || string.IsNullOrEmpty(slot_name));
    }

    public class ArchipelagoConnection
    {
        public static ArchipelagoConnectionInfo LoadFromFile(string path)
        {
            if (File.Exists(path))
            {
                var reader = File.OpenText(path);
                var content = reader.ReadToEnd();
                reader.Close();
                return JsonConvert.DeserializeObject<ArchipelagoConnectionInfo>(content);
            }
            return null;
        }

        public static void WriteToFile(ArchipelagoConnectionInfo connInfo, string path)
        {
            if (!File.Exists(path))
            {
                File.Create(path);
            }
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(connInfo));
            File.WriteAllBytes(path, bytes);
        }
    }
}
