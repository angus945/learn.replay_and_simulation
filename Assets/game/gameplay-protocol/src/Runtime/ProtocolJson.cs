using System.IO;
using System.Text;
using System.Runtime.Serialization.Json;

namespace GameplayProtocol.Game
{
    public static class ProtocolJson
    {
        public static string Write<T>(T value)
        {
            using (MemoryStream stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
        public static T Read<T>(string json)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    T value = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                    if (value == null) throw new ProtocolFault("payload.invalid");
                    return value;
                }
            }
            catch (System.Exception) { throw new ProtocolFault("payload.invalid"); }
        }
    }
}
