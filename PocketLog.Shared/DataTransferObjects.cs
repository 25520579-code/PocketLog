using System.Collections.Generic;

namespace PocketLog.Shared
{
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
}
