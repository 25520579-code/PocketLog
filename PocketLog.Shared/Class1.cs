using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PocketLog.Shared
{
    // Các loại lệnh giao tiếp giữa Client và Server
    public static class PacketType
    {
        public const string LOGIN_REQ = "LOGIN_REQ";
        public const string LOGIN_RES = "LOGIN_RES";
        public const string SYNC_REQ = "SYNC_REQ";
        public const string SYNC_RES = "SYNC_RES";
        public const string CREATE_POST_REQ = "CREATE_POST_REQ";
        public const string ADD_FRIEND_REQ = "ADD_FRIEND_REQ";
        public const string UPLOAD_PROOF_REQ = "UPLOAD_PROOF_REQ";
        public const string VERIFY_BILL_REQ = "VERIFY_BILL_REQ";
        public const string UPDATE_BUDGET_REQ = "UPDATE_BUDGET_REQ";
        public const string REFRESH_BROADCAST = "REFRESH_BROADCAST";
    }

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

    // 1. Gói tin Đăng nhập
    public class LoginRequestDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class LoginResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public int MonthlyBudget { get; set; }
    }

    // 2. Gói tin Bạn bè
    public class FriendDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string FullName { get; set; } = "";
        public bool IsOnline { get; set; }
    }

    public class AddFriendRequestDto
    {
        public int RequesterId { get; set; }
        public string TargetKeyword { get; set; } = "";
    }

    // 3. Gói tin Bài đăng Locket & Chi tiêu
    public class PostDto
    {
        public int PostId { get; set; }
        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = "";
        public bool HasTag { get; set; }
        public List<string> TaggedFriends { get; set; } = new List<string>();
        public string Category { get; set; } = "";
        public string NoteTop { get; set; } = "";
        public string CaptionInside { get; set; } = "";
        public int Amount { get; set; }
        public string Time { get; set; } = "";
        public string ImageBase64 { get; set; } = ""; // Chứa ảnh thật truyền qua TCP
    }

    // 4. Gói tin Chia tiền hóa đơn (SplitBills)
    public class SplitBillDto
    {
        public int SplitId { get; set; }
        public int PostId { get; set; }
        public int CreditorId { get; set; }
        public string CreditorName { get; set; } = "";
        public int DebtorId { get; set; }
        public string DebtorName { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public int TotalAmount { get; set; }
        public int AmountOwed { get; set; }
        public string SplitDetail { get; set; } = "";
        public string Time { get; set; } = "";
        public bool IsSentProof { get; set; }
        public bool IsPaid { get; set; }
        public string ProofImageBase64 { get; set; } = "";
        public string StatusNote { get; set; } = "";
    }

    public class UploadProofRequestDto
    {
        public int SplitId { get; set; }
        public int UserId { get; set; }
        public string ProofImageBase64 { get; set; } = "";
    }

    public class VerifyBillRequestDto
    {
        public int SplitId { get; set; }
        public string VerifierName { get; set; } = "";
    }

    public class UpdateBudgetRequestDto
    {
        public int UserId { get; set; }
        public int NewMonthlyBudget { get; set; }
    }

    // 5. Gói tin Đồng bộ toàn bộ dữ liệu về Client
    public class SyncDataResponseDto
    {
        public int MonthlyBudget { get; set; }
        public List<FriendDto> MyFriends { get; set; } = new List<FriendDto>();
        public List<FriendDto> SuggestedUsers { get; set; } = new List<FriendDto>();
        public List<PostDto> Posts { get; set; } = new List<PostDto>();
        public List<SplitBillDto> SplitBills { get; set; } = new List<SplitBillDto>();
    }

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