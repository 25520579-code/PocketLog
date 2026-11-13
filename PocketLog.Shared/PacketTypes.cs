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
}
