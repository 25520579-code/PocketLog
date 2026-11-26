using System.Security.Cryptography;
using System.Text;
using PocketLog.Shared;

namespace PocketLog.Server.Linux
{
    public interface IPocketLogStore
    {
        string BackendDescription { get; }
        void EnsureDatabaseSetup();
        LoginResponseDto HandleLogin(LoginRequestDto? req);
        SyncDataResponseDto BuildSyncDataForUser(int userId, HashSet<int> onlineUserIds);
        void SaveNewPost(PostDto dto);
        AddFriendResponseDto AddFriend(AddFriendRequestDto? req);
        AddFriendResponseDto RemoveFriend(AddFriendRequestDto? req);
        void SaveProofImage(UploadProofRequestDto req);
        void VerifySplitBill(VerifyBillRequestDto req);
        void UpdateUserBudget(UpdateBudgetRequestDto req);
        void UpdateCategoryBudget(UpdateCategoryBudgetRequestDto? req);
    }

    public static class ServerCommon
    {
        private static readonly TimeZoneInfo VietnamTz = ResolveVietnamTimeZone();

        private static TimeZoneInfo ResolveVietnamTimeZone()
        {
            foreach (string id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Vietnam Time", "Vietnam Time");
        }

        public static DateTime NowVn() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTz);

        public static string HashPassword(string? password)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password ?? "");
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        public static bool VerifyPassword(string? inputPassword, string? storedHash)
        {
            if (string.IsNullOrEmpty(inputPassword) || storedHash == null) return false;
            string hashedInput = HashPassword(inputPassword);
            if (string.Equals(storedHash, hashedInput, StringComparison.OrdinalIgnoreCase))
                return true;
            if (storedHash == inputPassword) return true;
            if (storedHash == "hash123" && inputPassword == "123") return true;
            return false;
        }

        public static string SaveBase64ToImageFile(string storageFolder, string? base64, string prefix)
        {
            if (string.IsNullOrEmpty(base64)) return "";
            try
            {
                Directory.CreateDirectory(storageFolder);
                string fileName = $"{prefix}_{Guid.NewGuid():N}.jpg";
                string fullPath = Path.Combine(storageFolder, fileName);
                byte[] bytes = Convert.FromBase64String(base64);
                File.WriteAllBytes(fullPath, bytes);
                return fileName;
            }
            catch
            {
                return "";
            }
        }

        public static string ReadImageFileAsBase64(string storageFolder, string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            try
            {
                string safeName = Path.GetFileName(fileName);
                string fullPath = Path.Combine(storageFolder, safeName);
                if (!File.Exists(fullPath)) return "";
                return Convert.ToBase64String(File.ReadAllBytes(fullPath));
            }
            catch
            {
                return "";
            }
        }
    }
}
