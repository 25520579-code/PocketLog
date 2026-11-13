using System.Text.Json;

namespace PocketLog.Shared
{
    // Khung gói tin TCP chuẩn
    public class TcpPacket
    {
        public string Type { get; set; } = "";
        public string PayloadJson { get; set; } = "";

        public static TcpPacket Create<T>(string type, T payload)
        {
            return new TcpPacket
            {
                Type = type,
                PayloadJson = JsonSerializer.Serialize(payload)
            };
        }

        public T GetPayload<T>()
        {
            return JsonSerializer.Deserialize<T>(PayloadJson);
        }
    }
}
