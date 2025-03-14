using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.MonsterSanctuary.Client.Persistence
{
    public class ApDataSerializationSurrogate : ISerializationSurrogate
    {
        public void GetObjectData(object obj, SerializationInfo info, StreamingContext context)
        {
            ApDataFile file = (ApDataFile)obj;
            info.AddValue("NextExpectedItemIndex", file.NextExpectedItemIndex);
            info.AddValue("LocationsChecked", file.LocationsChecked.ToArray());
            info.AddValue("ChampionsDefeated", file.ChampionsDefeated.ToArray());
        }

        public object SetObjectData(
            object obj, 
            SerializationInfo info, 
            StreamingContext context, 
            ISurrogateSelector selector)
        {
            var file = new ApDataFile();
            file.NextExpectedItemIndex = (int)info.GetValue("NextExpectedItemIndex", typeof(int));
            file.LocationsChecked = ((string[])info.GetValue("LocationsChecked", typeof(string[]))).ToList();
            file.ChampionsDefeated = ((string[])info.GetValue("ChampionsDefeated", typeof(string[]))).ToList();

            return file;
        }
    }
}
