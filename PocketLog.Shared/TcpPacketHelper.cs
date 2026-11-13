using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PocketLog.Shared
{
    // Bộ truyền/nhận gói tin TCP chống phân mảnh (Length-Prefixed Framing)
    public static class TcpPacketHelper
    {
        public static async Task SendPacketAsync(NetworkStream stream, TcpPacket packet)
        {
            string json = JsonSerializer.Serialize(packet);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);
            byte[] lengthPrefix = BitConverter.GetBytes(payloadBytes.Length);

            await stream.WriteAsync(lengthPrefix, 0, lengthPrefix.Length);
            await stream.WriteAsync(payloadBytes, 0, payloadBytes.Length);
            await stream.FlushAsync();
        }

        public static async Task<TcpPacket?> ReadPacketAsync(NetworkStream stream)
        {
            byte[] lengthBuffer = new byte[4];
            int readLen = await ReadExactAsync(stream, lengthBuffer, 4);
            if (readLen < 4) return null;

            int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);
            if (payloadLength <= 0 || payloadLength > 50 * 1024 * 1024) return null;

            byte[] payloadBuffer = new byte[payloadLength];
            int totalRead = await ReadExactAsync(stream, payloadBuffer, payloadLength);
            if (totalRead < payloadLength) return null;

            string json = Encoding.UTF8.GetString(payloadBuffer);
            return JsonSerializer.Deserialize<TcpPacket>(json);
        }

        private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset);
                if (read == 0) return offset;
                offset += read;
            }
            return offset;
        }
    }
}
