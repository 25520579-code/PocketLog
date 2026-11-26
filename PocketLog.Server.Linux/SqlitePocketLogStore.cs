using Microsoft.Data.Sqlite;
using PocketLog.Shared;

namespace PocketLog.Server.Linux
{
    /// <summary>
    /// Kho dữ liệu SQLite tự động tích hợp sẵn (tốn ~35MB RAM, không cần cài đặt Database ngoài).
    /// Giúp Server chạy mượt trên mọi máy ảo Linux miễn phí (Azure B1s 1GB, Oracle Free, Codespaces, Docker).
    /// </summary>
    public sealed class SqlitePocketLogStore : IPocketLogStore
    {
        private readonly string dbFilePath;
        private readonly string connStr;
        private readonly string storageFolder;
        private readonly object writeLock = new object();

        public string BackendDescription => $"SQLite ({dbFilePath})";

        public SqlitePocketLogStore(string dataDirectory, string storageFolder)
        {
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(storageFolder);
            this.dbFilePath = Path.Combine(dataDirectory, "PocketLogDB.sqlite");
            this.storageFolder = storageFolder;
            this.connStr = new SqliteConnectionStringBuilder
            {
                DataSource = dbFilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 10
            }.ToString();
        }

        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection(connStr);
            conn.Open();
            // Hỗ trợ so sánh chữ thường đầy đủ dấu tiếng Việt (Đ/đ, Ă/ă, Ê/ê...) giống SQL Server NVARCHAR
            conn.CreateFunction("VN_LOWER", (string? s) => (s ?? "").ToLowerInvariant());
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
            return conn;
        }

        public void EnsureDatabaseSetup()
        {
            lock (writeLock)
            {
                using var conn = OpenConnection();
                using var walCmd = conn.CreateCommand();
                walCmd.CommandText = "PRAGMA journal_mode = WAL;";
                walCmd.ExecuteNonQuery();

                string schemaSql = @"
CREATE TABLE IF NOT EXISTS Users (
    UserID INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    FullName TEXT NOT NULL,
    MonthlyBudget INTEGER DEFAULT 3500000
);
CREATE TABLE IF NOT EXISTS Friendships (
    FriendshipID INTEGER PRIMARY KEY AUTOINCREMENT,
    RequesterID INTEGER NOT NULL REFERENCES Users(UserID),
    AddresseeID INTEGER NOT NULL REFERENCES Users(UserID),
    Status TEXT DEFAULT 'Accepted',
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ExpenseGroups (
    GroupID INTEGER PRIMARY KEY AUTOINCREMENT,
    CreatedBy INTEGER NOT NULL REFERENCES Users(UserID),
    GroupName TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS GroupMembers (
    GroupID INTEGER NOT NULL REFERENCES ExpenseGroups(GroupID),
    UserID INTEGER NOT NULL REFERENCES Users(UserID),
    JoinedAt TEXT NOT NULL,
    PRIMARY KEY (GroupID, UserID)
);
CREATE TABLE IF NOT EXISTS ExpensePosts (
    PostID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID INTEGER NOT NULL REFERENCES Users(UserID),
    GroupID INTEGER NULL REFERENCES ExpenseGroups(GroupID),
    ImagePath TEXT NOT NULL,
    Caption TEXT,
    IsPublicToFriends INTEGER DEFAULT 1,
    IsExpenseTracked INTEGER DEFAULT 1,
    TotalAmount INTEGER DEFAULT 0,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS ExpenseItems (
    ItemID INTEGER PRIMARY KEY AUTOINCREMENT,
    PostID INTEGER NOT NULL REFERENCES ExpensePosts(PostID),
    ItemName TEXT NOT NULL,
    Amount INTEGER NOT NULL,
    Category TEXT NOT NULL,
    PosX REAL DEFAULT 0.5,
    PosY REAL DEFAULT 0.5
);
CREATE TABLE IF NOT EXISTS SplitBills (
    SplitID INTEGER PRIMARY KEY AUTOINCREMENT,
    PostID INTEGER NOT NULL REFERENCES ExpensePosts(PostID),
    DebtorID INTEGER NOT NULL REFERENCES Users(UserID),
    AmountOwed INTEGER NOT NULL,
    IsPaid INTEGER DEFAULT 0,
    ProofImagePath TEXT NULL,
    StatusNote TEXT DEFAULT 'Chưa đính kèm ảnh chuyển khoản',
    PaidAt TEXT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS UserCategoryBudgets (
    UserID INTEGER NOT NULL REFERENCES Users(UserID),
    Category TEXT NOT NULL,
    BudgetLimit INTEGER NOT NULL,
    PRIMARY KEY (UserID, Category)
);";
                using var cmd = conn.CreateCommand();
                cmd.CommandText = schemaSql;
                cmd.ExecuteNonQuery();
            }
        }

        public LoginResponseDto HandleLogin(LoginRequestDto? req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Username))
                return new LoginResponseDto { Success = false, Message = "Tên tài khoản không được để trống!" };
            if (string.IsNullOrWhiteSpace(req.Password))
                return new LoginResponseDto { Success = false, Message = "Vui lòng nhập mật khẩu!" };

            string rawInput = req.Username.Trim().TrimStart('@');
            string cleanUsername = rawInput.ToLowerInvariant().Replace(" ", "");
            string rawLower = rawInput.ToLowerInvariant();
            string passInput = req.Password.Trim();

            lock (writeLock)
            {
                using var conn = OpenConnection();
                if (req.IsRegister)
                {
                    using (var chk = conn.CreateCommand())
                    {
                        chk.CommandText = "SELECT COUNT(1) FROM Users WHERE VN_LOWER(Username) = @u OR VN_LOWER(FullName) = @raw";
                        chk.Parameters.AddWithValue("@u", cleanUsername);
                        chk.Parameters.AddWithValue("@raw", rawLower);
                        if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        {
                            return new LoginResponseDto
                            {
                                Success = false,
                                Message = "Tài khoản này đã tồn tại! Vui lòng chọn Đăng nhập."
                            };
                        }
                    }

                    int initBudget = req.InitialBudget > 0 ? req.InitialBudget : 3500000;
                    string displayFullName = string.IsNullOrWhiteSpace(req.FullName) ? rawInput : req.FullName.Trim();
                    string passHash = ServerCommon.HashPassword(passInput);

                    using var ins = conn.CreateCommand();
                    ins.CommandText = @"INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
                                        VALUES (@u, @p, @fn, @mb);
                                        SELECT last_insert_rowid();";
                    ins.Parameters.AddWithValue("@u", cleanUsername);
                    ins.Parameters.AddWithValue("@p", passHash);
                    ins.Parameters.AddWithValue("@fn", displayFullName);
                    ins.Parameters.AddWithValue("@mb", initBudget);
                    int newId = Convert.ToInt32(ins.ExecuteScalar());

                    return new LoginResponseDto
                    {
                        Success = true,
                        UserId = newId,
                        Username = cleanUsername,
                        FullName = displayFullName,
                        MonthlyBudget = initBudget,
                        Message = "Tạo tài khoản thành công!"
                    };
                }
                else
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"SELECT UserID, Username, FullName, IFNULL(MonthlyBudget, 3500000), PasswordHash
                                        FROM Users
                                        WHERE VN_LOWER(Username) = @u OR VN_LOWER(Username) = @raw OR VN_LOWER(FullName) = @raw
                                        ORDER BY CASE WHEN VN_LOWER(Username) = @u THEN 0
                                                      WHEN VN_LOWER(Username) = @raw THEN 1
                                                      ELSE 2 END, UserID
                                        LIMIT 1";
                    cmd.Parameters.AddWithValue("@u", cleanUsername);
                    cmd.Parameters.AddWithValue("@raw", rawLower);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read())
                    {
                        return new LoginResponseDto
                        {
                            Success = false,
                            Message = "Tài khoản chưa tồn tại! Hãy chuyển sang tab Tạo tài khoản mới."
                        };
                    }

                    int uid = r.GetInt32(0);
                    string uname = r.GetString(1);
                    string fname = r.GetString(2).Replace(" (Bạn)", "").Trim();
                    int budget = r.GetInt32(3);
                    string storedHash = r.GetString(4);

                    if (!ServerCommon.VerifyPassword(passInput, storedHash))
                    {
                        return new LoginResponseDto
                        {
                            Success = false,
                            Message = "Mật khẩu không chính xác! Vui lòng kiểm tra lại."
                        };
                    }

                    return new LoginResponseDto
                    {
                        Success = true,
                        UserId = uid,
                        Username = uname,
                        FullName = fname,
                        MonthlyBudget = budget,
                        Message = "OK"
                    };
                }
            }
        }

        public SyncDataResponseDto BuildSyncDataForUser(int userId, HashSet<int> onlineUserIds)
        {
            var res = new SyncDataResponseDto { MonthlyBudget = 3500000 };
            using var conn = OpenConnection();

            using (var cmdB = conn.CreateCommand())
            {
                cmdB.CommandText = "SELECT IFNULL(MonthlyBudget, 3500000) FROM Users WHERE UserID = @uid";
                cmdB.Parameters.AddWithValue("@uid", userId);
                object? bObj = cmdB.ExecuteScalar();
                if (bObj != null && bObj != DBNull.Value) res.MonthlyBudget = Convert.ToInt32(bObj);
            }

            using (var cmdCB = conn.CreateCommand())
            {
                cmdCB.CommandText = "SELECT Category, BudgetLimit FROM UserCategoryBudgets WHERE UserID = @uid";
                cmdCB.Parameters.AddWithValue("@uid", userId);
                using var r = cmdCB.ExecuteReader();
                while (r.Read()) res.CategoryBudgets[r.GetString(0)] = r.GetInt32(1);
            }

            var friendIds = new HashSet<int>();
            using (var cmdF = conn.CreateCommand())
            {
                cmdF.CommandText = @"SELECT RequesterID, AddresseeID FROM Friendships
                                     WHERE (RequesterID = @uid OR AddresseeID = @uid) AND Status = 'Accepted'";
                cmdF.Parameters.AddWithValue("@uid", userId);
                using var r = cmdF.ExecuteReader();
                while (r.Read())
                {
                    int reqId = r.GetInt32(0);
                    int addId = r.GetInt32(1);
                    friendIds.Add(reqId == userId ? addId : reqId);
                }
            }

            using (var cmdU = conn.CreateCommand())
            {
                cmdU.CommandText = "SELECT UserID, Username, FullName FROM Users WHERE UserID <> @uid ORDER BY FullName ASC";
                cmdU.Parameters.AddWithValue("@uid", userId);
                using var r = cmdU.ExecuteReader();
                while (r.Read())
                {
                    int uid = r.GetInt32(0);
                    string uname = r.GetString(1);
                    string fname = r.GetString(2).Replace(" (Bạn)", "").Trim();
                    var dto = new FriendDto
                    {
                        UserId = uid,
                        Username = "@" + uname,
                        FullName = fname,
                        IsOnline = onlineUserIds.Contains(uid)
                    };
                    if (friendIds.Contains(uid)) res.MyFriends.Add(dto);
                    else res.SuggestedUsers.Add(dto);
                }
            }

            using (var cmdP = conn.CreateCommand())
            {
                cmdP.CommandText = @"SELECT p.PostID, p.UserID, u.FullName, p.ImagePath, p.Caption, p.IsExpenseTracked, p.TotalAmount, p.CreatedAt,
                                            i.ItemName, i.Category
                                     FROM ExpensePosts p
                                     INNER JOIN Users u ON p.UserID = u.UserID
                                     LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                                     ORDER BY p.PostID DESC";
                using var r = cmdP.ExecuteReader();
                while (r.Read())
                {
                    int authorId = r.GetInt32(1);
                    if (authorId != userId && !friendIds.Contains(authorId)) continue;

                    int postId = r.GetInt32(0);
                    string imgPath = r.IsDBNull(3) ? "" : r.GetString(3);
                    DateTime createdAt = ParseStoredDate(r.IsDBNull(7) ? null : r.GetString(7));
                    string authorName = r.GetString(2).Replace(" (Bạn)", "").Trim();

                    res.Posts.Add(new PostDto
                    {
                        PostId = postId,
                        AuthorId = authorId,
                        AuthorName = authorName,
                        ImageBase64 = ServerCommon.ReadImageFileAsBase64(storageFolder, imgPath),
                        CaptionInside = r.IsDBNull(4) ? "" : r.GetString(4),
                        HasTag = !r.IsDBNull(5) && r.GetInt32(5) != 0,
                        Amount = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                        Time = createdAt.ToString("HH:mm dd/MM"),
                        NoteTop = r.IsDBNull(8) ? "" : r.GetString(8),
                        Category = r.IsDBNull(9) ? "🍜 Đồ ăn/Thức uống" : r.GetString(9)
                    });
                }
            }

            foreach (var post in res.Posts)
            {
                using var cmdT = conn.CreateCommand();
                cmdT.CommandText = @"SELECT u.FullName FROM SplitBills s
                                     INNER JOIN Users u ON s.DebtorID = u.UserID
                                     WHERE s.PostID = @pid";
                cmdT.Parameters.AddWithValue("@pid", post.PostId);
                using var r = cmdT.ExecuteReader();
                while (r.Read())
                {
                    post.TaggedFriends.Add(r.GetString(0).Replace(" (Bạn)", "").Trim());
                }
            }

            using (var cmdS = conn.CreateCommand())
            {
                cmdS.CommandText = @"SELECT s.SplitID, s.PostID, p.UserID AS CreditorID, uCred.FullName AS CreditorName,
                                            s.DebtorID, uDebt.FullName AS DebtorName,
                                            IFNULL(i.ItemName, IFNULL(p.Caption, '')) AS Title,
                                            IFNULL(i.Category, '🍜 Đồ ăn/Thức uống') AS Category,
                                            p.TotalAmount, s.AmountOwed, s.IsPaid, s.ProofImagePath, s.StatusNote, s.CreatedAt
                                     FROM SplitBills s
                                     INNER JOIN ExpensePosts p ON s.PostID = p.PostID
                                     INNER JOIN Users uCred ON p.UserID = uCred.UserID
                                     INNER JOIN Users uDebt ON s.DebtorID = uDebt.UserID
                                     LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                                     WHERE s.DebtorID = @uid OR p.UserID = @uid
                                     ORDER BY s.SplitID DESC";
                cmdS.Parameters.AddWithValue("@uid", userId);
                using var r = cmdS.ExecuteReader();
                while (r.Read())
                {
                    string proofPath = r.IsDBNull(11) ? "" : r.GetString(11);
                    DateTime created = ParseStoredDate(r.IsDBNull(13) ? null : r.GetString(13));
                    string credName = r.GetString(3).Replace(" (Bạn)", "").Trim();
                    string debtName = r.GetString(5).Replace(" (Bạn)", "").Trim();

                    res.SplitBills.Add(new SplitBillDto
                    {
                        SplitId = r.GetInt32(0),
                        PostId = r.GetInt32(1),
                        CreditorId = r.GetInt32(2),
                        CreditorName = credName,
                        DebtorId = r.GetInt32(4),
                        DebtorName = debtName,
                        Title = r.GetString(6),
                        Category = r.GetString(7),
                        TotalAmount = r.GetInt32(8),
                        AmountOwed = r.GetInt32(9),
                        SplitDetail = $"{credName} tag {debtName}",
                        IsPaid = r.GetInt32(10) != 0,
                        IsSentProof = !string.IsNullOrEmpty(proofPath),
                        ProofImageBase64 = ServerCommon.ReadImageFileAsBase64(storageFolder, proofPath),
                        StatusNote = r.IsDBNull(12) ? "" : r.GetString(12),
                        Time = created.ToString("HH:mm dd/MM")
                    });
                }
            }

            return res;
        }

        public void SaveNewPost(PostDto dto)
        {
            string savedFileName = ServerCommon.SaveBase64ToImageFile(storageFolder, dto.ImageBase64, "post");
            string nowIso = ServerCommon.NowVn().ToString("yyyy-MM-dd HH:mm:ss");

            lock (writeLock)
            {
                using var conn = OpenConnection();
                int newPostId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO ExpensePosts (UserID, ImagePath, Caption, IsPublicToFriends, IsExpenseTracked, TotalAmount, CreatedAt)
                                        VALUES (@uid, @img, @cap, 1, @tracked, @amt, @now);
                                        SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@uid", dto.AuthorId);
                    cmd.Parameters.AddWithValue("@img", savedFileName);
                    cmd.Parameters.AddWithValue("@cap", dto.CaptionInside ?? "");
                    cmd.Parameters.AddWithValue("@tracked", dto.HasTag ? 1 : 0);
                    cmd.Parameters.AddWithValue("@amt", dto.Amount);
                    cmd.Parameters.AddWithValue("@now", nowIso);
                    newPostId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (dto.HasTag && newPostId > 0)
                {
                    using (var cmdI = conn.CreateCommand())
                    {
                        cmdI.CommandText = @"INSERT INTO ExpenseItems (PostID, ItemName, Amount, Category, PosX, PosY)
                                             VALUES (@pid, @name, @amt, @cat, 0.5, 0.5)";
                        cmdI.Parameters.AddWithValue("@pid", newPostId);
                        cmdI.Parameters.AddWithValue("@name", dto.NoteTop ?? "");
                        cmdI.Parameters.AddWithValue("@amt", dto.Amount);
                        cmdI.Parameters.AddWithValue("@cat", dto.Category ?? "🍜 Đồ ăn/Thức uống");
                        cmdI.ExecuteNonQuery();
                    }

                    if (dto.TaggedFriends != null && dto.TaggedFriends.Count > 0 && dto.Amount > 0)
                    {
                        int share = dto.Amount / (dto.TaggedFriends.Count + 1);
                        if (share > 0)
                        {
                            foreach (string friendName in dto.TaggedFriends)
                            {
                                int? debtorId = GetUserIdByName(conn, friendName);
                                if (debtorId.HasValue && debtorId.Value != dto.AuthorId)
                                {
                                    using var cmdS = conn.CreateCommand();
                                    cmdS.CommandText = @"INSERT INTO SplitBills (PostID, DebtorID, AmountOwed, IsPaid, StatusNote, CreatedAt)
                                                         VALUES (@pid, @did, @owed, 0, 'Chưa đính kèm ảnh chuyển khoản', @now)";
                                    cmdS.Parameters.AddWithValue("@pid", newPostId);
                                    cmdS.Parameters.AddWithValue("@did", debtorId.Value);
                                    cmdS.Parameters.AddWithValue("@owed", share);
                                    cmdS.Parameters.AddWithValue("@now", nowIso);
                                    cmdS.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
        }

        public AddFriendResponseDto AddFriend(AddFriendRequestDto? req)
        {
            string kw = (req?.TargetKeyword ?? "").Trim().TrimStart('@');
            if (req == null || string.IsNullOrEmpty(kw))
                return new AddFriendResponseDto { Success = false, Message = "Vui lòng nhập tên hoặc username cần kết bạn!" };

            lock (writeLock)
            {
                using var conn = OpenConnection();
                int? targetId = GetUserIdByName(conn, kw);
                if (!targetId.HasValue)
                    return new AddFriendResponseDto { Success = false, Message = $"Không tìm thấy người dùng '{kw}' trên hệ thống!" };
                if (targetId.Value == req.RequesterId)
                    return new AddFriendResponseDto { Success = false, Message = "Bạn không thể tự kết bạn với chính mình!" };

                using (var chk = conn.CreateCommand())
                {
                    chk.CommandText = @"SELECT COUNT(1) FROM Friendships
                                        WHERE (RequesterID = @r AND AddresseeID = @a) OR (RequesterID = @a AND AddresseeID = @r)";
                    chk.Parameters.AddWithValue("@r", req.RequesterId);
                    chk.Parameters.AddWithValue("@a", targetId.Value);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                        return new AddFriendResponseDto { Success = false, Message = $"Bạn đã kết nối bạn bè với '{kw}' trước đó rồi!" };
                }

                using (var ins = conn.CreateCommand())
                {
                    ins.CommandText = "INSERT INTO Friendships (RequesterID, AddresseeID, Status, CreatedAt) VALUES (@r, @a, 'Accepted', @now)";
                    ins.Parameters.AddWithValue("@r", req.RequesterId);
                    ins.Parameters.AddWithValue("@a", targetId.Value);
                    ins.Parameters.AddWithValue("@now", ServerCommon.NowVn().ToString("yyyy-MM-dd HH:mm:ss"));
                    ins.ExecuteNonQuery();
                }

                return new AddFriendResponseDto { Success = true, Message = $"✓ Đã kết bạn thành công với '{kw}'!" };
            }
        }

        public AddFriendResponseDto RemoveFriend(AddFriendRequestDto? req)
        {
            string kw = (req?.TargetKeyword ?? "").Trim().TrimStart('@');
            if (req == null || string.IsNullOrEmpty(kw))
                return new AddFriendResponseDto { Success = false, Message = "Không xác định được bạn bè cần xóa!" };

            lock (writeLock)
            {
                using var conn = OpenConnection();
                int? targetId = GetUserIdByName(conn, kw);
                if (!targetId.HasValue)
                    return new AddFriendResponseDto { Success = false, Message = $"Không tìm thấy người dùng '{kw}'!" };

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"DELETE FROM Friendships
                                    WHERE (RequesterID = @r AND AddresseeID = @a) OR (RequesterID = @a AND AddresseeID = @r)";
                cmd.Parameters.AddWithValue("@r", req.RequesterId);
                cmd.Parameters.AddWithValue("@a", targetId.Value);
                if (cmd.ExecuteNonQuery() > 0)
                    return new AddFriendResponseDto { Success = true, Message = $"Đã hủy kết bạn với '{kw}'." };

                return new AddFriendResponseDto { Success = false, Message = $"Bạn và '{kw}' hiện không phải bạn bè." };
            }
        }

        public void SaveProofImage(UploadProofRequestDto req)
        {
            string fileName = ServerCommon.SaveBase64ToImageFile(storageFolder, req.ProofImageBase64, $"proof_{req.SplitId}");
            lock (writeLock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE SplitBills SET ProofImagePath = @path, StatusNote = @note WHERE SplitID = @sid";
                cmd.Parameters.AddWithValue("@path", fileName);
                cmd.Parameters.AddWithValue("@note", $"Đã gửi ảnh bill lúc {ServerCommon.NowVn():HH:mm}");
                cmd.Parameters.AddWithValue("@sid", req.SplitId);
                cmd.ExecuteNonQuery();
            }
        }

        public void VerifySplitBill(VerifyBillRequestDto req)
        {
            lock (writeLock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE SplitBills SET IsPaid = 1, PaidAt = @now, StatusNote = @note WHERE SplitID = @sid";
                cmd.Parameters.AddWithValue("@now", ServerCommon.NowVn().ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@note", $"{req.VerifierName} đã xác nhận hoàn tất!");
                cmd.Parameters.AddWithValue("@sid", req.SplitId);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateUserBudget(UpdateBudgetRequestDto req)
        {
            lock (writeLock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Users SET MonthlyBudget = @b WHERE UserID = @uid";
                cmd.Parameters.AddWithValue("@b", req.NewMonthlyBudget);
                cmd.Parameters.AddWithValue("@uid", req.UserId);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateCategoryBudget(UpdateCategoryBudgetRequestDto? req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Category) || req.NewLimit <= 0) return;
            lock (writeLock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO UserCategoryBudgets (UserID, Category, BudgetLimit)
                                    VALUES (@uid, @cat, @lim)
                                    ON CONFLICT(UserID, Category) DO UPDATE SET BudgetLimit = excluded.BudgetLimit";
                cmd.Parameters.AddWithValue("@uid", req.UserId);
                cmd.Parameters.AddWithValue("@cat", req.Category);
                cmd.Parameters.AddWithValue("@lim", req.NewLimit);
                cmd.ExecuteNonQuery();
            }
        }

        private static int? GetUserIdByName(SqliteConnection conn, string nameOrHandle)
        {
            string clean = nameOrHandle.Trim().TrimStart('@').ToLowerInvariant();
            string noSpace = clean.Replace(" ", "");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT UserID FROM Users
                                WHERE VN_LOWER(FullName) = @n
                                   OR VN_LOWER(REPLACE(FullName, ' (Bạn)', '')) = @n
                                   OR VN_LOWER(Username) = @n
                                   OR VN_LOWER(Username) = @ns
                                ORDER BY CASE WHEN VN_LOWER(Username) = @ns THEN 0
                                              WHEN VN_LOWER(Username) = @n THEN 1
                                              ELSE 2 END, UserID
                                LIMIT 1";
            cmd.Parameters.AddWithValue("@n", clean);
            cmd.Parameters.AddWithValue("@ns", noSpace);
            object? val = cmd.ExecuteScalar();
            return val != null && val != DBNull.Value ? Convert.ToInt32(val) : null;
        }

        private static DateTime ParseStoredDate(string? raw)
        {
            if (!string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out DateTime dt))
                return dt;
            return ServerCommon.NowVn();
        }
    }
}
