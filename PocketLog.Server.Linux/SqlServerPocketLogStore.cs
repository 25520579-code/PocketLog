using System.Data.SqlClient;
using PocketLog.Shared;

namespace PocketLog.Server.Linux
{
    /// <summary>
    /// Kho dữ liệu SQL Server (dùng khi đặt biến môi trường POCKETLOG_DB_CONN trỏ vào Azure SQL hoặc Docker MSSQL).
    /// </summary>
    public sealed class SqlServerPocketLogStore : IPocketLogStore
    {
        private readonly string dbConnStr;
        private readonly string storageFolder;

        public string BackendDescription => "SQL Server (POCKETLOG_DB_CONN)";

        public SqlServerPocketLogStore(string dbConnStr, string storageFolder)
        {
            this.dbConnStr = dbConnStr;
            this.storageFolder = storageFolder;
            Directory.CreateDirectory(storageFolder);
        }

        public void EnsureDatabaseSetup()
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(dbConnStr);
                string targetDb = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "PocketLogDB" : builder.InitialCatalog;
                builder.InitialCatalog = "master";
                using var masterConn = new SqlConnection(builder.ConnectionString);
                masterConn.Open();
                using var mkCmd = new SqlCommand($"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{targetDb.Replace("'", "''")}') CREATE DATABASE [{targetDb}];", masterConn);
                mkCmd.ExecuteNonQuery();
            }
            catch
            {
                // Trên Azure SQL Database Free Tier, database đã được tạo sẵn trên Portal
            }

            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            string schemaSql = @"
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE Users (
        UserID INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(255) NOT NULL,
        FullName NVARCHAR(100) NOT NULL,
        MonthlyBudget INT DEFAULT 3500000
    );
    CREATE TABLE Friendships (
        FriendshipID INT IDENTITY(1,1) PRIMARY KEY,
        RequesterID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        AddresseeID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        Status NVARCHAR(20) DEFAULT 'Accepted',
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    CREATE TABLE ExpenseGroups (
        GroupID INT IDENTITY(1,1) PRIMARY KEY,
        CreatedBy INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        GroupName NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    CREATE TABLE GroupMembers (
        GroupID INT NOT NULL FOREIGN KEY REFERENCES ExpenseGroups(GroupID),
        UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        JoinedAt DATETIME DEFAULT GETDATE(),
        PRIMARY KEY (GroupID, UserID)
    );
    CREATE TABLE ExpensePosts (
        PostID INT IDENTITY(1,1) PRIMARY KEY,
        UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        GroupID INT NULL FOREIGN KEY REFERENCES ExpenseGroups(GroupID),
        ImagePath NVARCHAR(255) NOT NULL,
        Caption NVARCHAR(255),
        IsPublicToFriends BIT DEFAULT 1,
        IsExpenseTracked BIT DEFAULT 1,
        TotalAmount INT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    CREATE TABLE ExpenseItems (
        ItemID INT IDENTITY(1,1) PRIMARY KEY,
        PostID INT NOT NULL FOREIGN KEY REFERENCES ExpensePosts(PostID),
        ItemName NVARCHAR(100) NOT NULL,
        Amount INT NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        PosX FLOAT DEFAULT 0.5,
        PosY FLOAT DEFAULT 0.5
    );
    CREATE TABLE SplitBills (
        SplitID INT IDENTITY(1,1) PRIMARY KEY,
        PostID INT NOT NULL FOREIGN KEY REFERENCES ExpensePosts(PostID),
        DebtorID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        AmountOwed INT NOT NULL,
        IsPaid BIT DEFAULT 0,
        ProofImagePath NVARCHAR(255) NULL,
        StatusNote NVARCHAR(255) DEFAULT N'Chưa đính kèm ảnh chuyển khoản',
        PaidAt DATETIME NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END

IF OBJECT_ID('dbo.UserCategoryBudgets', 'U') IS NULL
BEGIN
    CREATE TABLE UserCategoryBudgets (
        UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        Category NVARCHAR(50) NOT NULL,
        BudgetLimit INT NOT NULL,
        PRIMARY KEY (UserID, Category)
    );
END";
            using var cmd = new SqlCommand(schemaSql, conn);
            cmd.ExecuteNonQuery();
        }

        public LoginResponseDto HandleLogin(LoginRequestDto? req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Username))
                return new LoginResponseDto { Success = false, Message = "Tên tài khoản không được để trống!" };
            if (string.IsNullOrWhiteSpace(req.Password))
                return new LoginResponseDto { Success = false, Message = "Vui lòng nhập mật khẩu!" };

            string rawInput = req.Username.Trim().TrimStart('@');
            string cleanUsername = rawInput.ToLower().Replace(" ", "");
            string passInput = req.Password.Trim();

            using var conn = new SqlConnection(dbConnStr);
            conn.Open();

            if (req.IsRegister)
            {
                using (var chkCmd = new SqlCommand("SELECT COUNT(1) FROM Users WHERE LOWER(Username) = @u OR LOWER(FullName) = @raw", conn))
                {
                    chkCmd.Parameters.AddWithValue("@u", cleanUsername);
                    chkCmd.Parameters.AddWithValue("@raw", rawInput.ToLower());
                    if (Convert.ToInt32(chkCmd.ExecuteScalar()) > 0)
                        return new LoginResponseDto { Success = false, Message = "Tài khoản này đã tồn tại! Vui lòng chọn Đăng nhập." };
                }

                int initBudget = req.InitialBudget > 0 ? req.InitialBudget : 3500000;
                string displayFullName = string.IsNullOrWhiteSpace(req.FullName) ? rawInput : req.FullName.Trim();
                string passHash = ServerCommon.HashPassword(passInput);

                using var cmdNew = new SqlCommand(@"INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
                                                    VALUES (@u, @p, @fn, @mb);
                                                    SELECT SCOPE_IDENTITY();", conn);
                cmdNew.Parameters.AddWithValue("@u", cleanUsername);
                cmdNew.Parameters.AddWithValue("@p", passHash);
                cmdNew.Parameters.AddWithValue("@fn", displayFullName);
                cmdNew.Parameters.AddWithValue("@mb", initBudget);
                int newId = Convert.ToInt32(cmdNew.ExecuteScalar());

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
                string sql = @"SELECT TOP 1 UserID, Username, FullName, ISNULL(MonthlyBudget, 3500000), PasswordHash
                               FROM Users
                               WHERE LOWER(Username) = @u OR LOWER(Username) = @raw OR LOWER(FullName) = @raw
                               ORDER BY CASE WHEN LOWER(Username) = @u THEN 0
                                             WHEN LOWER(Username) = @raw THEN 1
                                             ELSE 2 END, UserID";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@u", cleanUsername);
                cmd.Parameters.AddWithValue("@raw", rawInput.ToLower());
                using var r = cmd.ExecuteReader();
                if (!r.Read())
                    return new LoginResponseDto { Success = false, Message = "Tài khoản chưa tồn tại! Hãy chuyển sang tab Tạo tài khoản mới." };

                int uid = r.GetInt32(0);
                string uname = r.GetString(1);
                string fname = r.GetString(2).Replace(" (Bạn)", "").Trim();
                int budget = r.GetInt32(3);
                string storedHash = r.GetString(4);

                if (!ServerCommon.VerifyPassword(passInput, storedHash))
                    return new LoginResponseDto { Success = false, Message = "Mật khẩu không chính xác! Vui lòng kiểm tra lại." };

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

        public SyncDataResponseDto BuildSyncDataForUser(int userId, HashSet<int> onlineUserIds)
        {
            var res = new SyncDataResponseDto { MonthlyBudget = 3500000 };
            using var conn = new SqlConnection(dbConnStr);
            conn.Open();

            using (var cmdB = new SqlCommand("SELECT ISNULL(MonthlyBudget, 3500000) FROM Users WHERE UserID = @uid", conn))
            {
                cmdB.Parameters.AddWithValue("@uid", userId);
                object? bObj = cmdB.ExecuteScalar();
                if (bObj != null && bObj != DBNull.Value) res.MonthlyBudget = Convert.ToInt32(bObj);
            }

            using (var cmdCB = new SqlCommand("SELECT Category, BudgetLimit FROM UserCategoryBudgets WHERE UserID = @uid", conn))
            {
                cmdCB.Parameters.AddWithValue("@uid", userId);
                using var r = cmdCB.ExecuteReader();
                while (r.Read()) res.CategoryBudgets[r.GetString(0)] = r.GetInt32(1);
            }

            var friendIds = new HashSet<int>();
            using (var cmdF = new SqlCommand(@"SELECT RequesterID, AddresseeID FROM Friendships
                                               WHERE (RequesterID = @uid OR AddresseeID = @uid) AND Status = 'Accepted'", conn))
            {
                cmdF.Parameters.AddWithValue("@uid", userId);
                using var r = cmdF.ExecuteReader();
                while (r.Read())
                {
                    int reqId = r.GetInt32(0);
                    int addId = r.GetInt32(1);
                    friendIds.Add(reqId == userId ? addId : reqId);
                }
            }

            using (var cmdU = new SqlCommand("SELECT UserID, Username, FullName FROM Users WHERE UserID <> @uid ORDER BY FullName ASC", conn))
            {
                cmdU.Parameters.AddWithValue("@uid", userId);
                using var r = cmdU.ExecuteReader();
                while (r.Read())
                {
                    int uid = r.GetInt32(0);
                    string uname = r["Username"]?.ToString() ?? "";
                    string fname = (r["FullName"]?.ToString() ?? uname).Replace(" (Bạn)", "").Trim();
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

            using (var cmdP = new SqlCommand(@"SELECT p.PostID, p.UserID, u.FullName, p.ImagePath, p.Caption, p.IsExpenseTracked, p.TotalAmount, p.CreatedAt,
                                                      i.ItemName, i.Category
                                               FROM ExpensePosts p
                                               INNER JOIN Users u ON p.UserID = u.UserID
                                               LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                                               ORDER BY p.PostID DESC", conn))
            using (var r = cmdP.ExecuteReader())
            {
                while (r.Read())
                {
                    int authorId = r.GetInt32(1);
                    if (authorId != userId && !friendIds.Contains(authorId)) continue;

                    int postId = r.GetInt32(0);
                    string imgPath = r["ImagePath"]?.ToString() ?? "";
                    DateTime createdAt = r["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(r["CreatedAt"]) : ServerCommon.NowVn();
                    string authorName = (r["FullName"]?.ToString() ?? "").Replace(" (Bạn)", "").Trim();

                    res.Posts.Add(new PostDto
                    {
                        PostId = postId,
                        AuthorId = authorId,
                        AuthorName = authorName,
                        ImageBase64 = ServerCommon.ReadImageFileAsBase64(storageFolder, imgPath),
                        CaptionInside = r["Caption"]?.ToString() ?? "",
                        HasTag = r["IsExpenseTracked"] != DBNull.Value && Convert.ToBoolean(r["IsExpenseTracked"]),
                        Amount = r["TotalAmount"] != DBNull.Value ? Convert.ToInt32(r["TotalAmount"]) : 0,
                        Time = createdAt.ToString("HH:mm dd/MM"),
                        NoteTop = r["ItemName"]?.ToString() ?? "",
                        Category = r["Category"]?.ToString() ?? "🍜 Đồ ăn/Thức uống"
                    });
                }
            }

            foreach (var post in res.Posts)
            {
                using var cmdT = new SqlCommand(@"SELECT u.FullName FROM SplitBills s
                                                  INNER JOIN Users u ON s.DebtorID = u.UserID
                                                  WHERE s.PostID = @pid", conn);
                cmdT.Parameters.AddWithValue("@pid", post.PostId);
                using var r = cmdT.ExecuteReader();
                while (r.Read()) post.TaggedFriends.Add(r.GetString(0).Replace(" (Bạn)", "").Trim());
            }

            using (var cmdS = new SqlCommand(@"SELECT s.SplitID, s.PostID, p.UserID AS CreditorID, uCred.FullName AS CreditorName,
                                                      s.DebtorID, uDebt.FullName AS DebtorName,
                                                      ISNULL(i.ItemName, p.Caption) AS Title, ISNULL(i.Category, N'🍜 Đồ ăn/Thức uống') AS Category,
                                                      p.TotalAmount, s.AmountOwed, s.IsPaid, s.ProofImagePath, s.StatusNote, s.CreatedAt
                                               FROM SplitBills s
                                               INNER JOIN ExpensePosts p ON s.PostID = p.PostID
                                               INNER JOIN Users uCred ON p.UserID = uCred.UserID
                                               INNER JOIN Users uDebt ON s.DebtorID = uDebt.UserID
                                               LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                                               WHERE s.DebtorID = @uid OR p.UserID = @uid
                                               ORDER BY s.SplitID DESC", conn))
            {
                cmdS.Parameters.AddWithValue("@uid", userId);
                using var r = cmdS.ExecuteReader();
                while (r.Read())
                {
                    string proofPath = r["ProofImagePath"] == DBNull.Value ? "" : r["ProofImagePath"].ToString()!;
                    DateTime created = r["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(r["CreatedAt"]) : ServerCommon.NowVn();
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
                        IsPaid = r.GetBoolean(10),
                        IsSentProof = !string.IsNullOrEmpty(proofPath),
                        ProofImageBase64 = ServerCommon.ReadImageFileAsBase64(storageFolder, proofPath),
                        StatusNote = r["StatusNote"]?.ToString() ?? "",
                        Time = created.ToString("HH:mm dd/MM")
                    });
                }
            }

            return res;
        }

        public void SaveNewPost(PostDto dto)
        {
            string savedFileName = ServerCommon.SaveBase64ToImageFile(storageFolder, dto.ImageBase64, "post");
            DateTime nowVn = ServerCommon.NowVn();

            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            int newPostId;
            using (var cmd = new SqlCommand(@"INSERT INTO ExpensePosts (UserID, ImagePath, Caption, IsPublicToFriends, IsExpenseTracked, TotalAmount, CreatedAt)
                                              VALUES (@uid, @img, @cap, 1, @tracked, @amt, @now);
                                              SELECT SCOPE_IDENTITY();", conn))
            {
                cmd.Parameters.AddWithValue("@uid", dto.AuthorId);
                cmd.Parameters.AddWithValue("@img", savedFileName);
                cmd.Parameters.AddWithValue("@cap", dto.CaptionInside ?? "");
                cmd.Parameters.AddWithValue("@tracked", dto.HasTag ? 1 : 0);
                cmd.Parameters.AddWithValue("@amt", dto.Amount);
                cmd.Parameters.AddWithValue("@now", nowVn);
                newPostId = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (dto.HasTag && newPostId > 0)
            {
                using (var cmdI = new SqlCommand(@"INSERT INTO ExpenseItems (PostID, ItemName, Amount, Category, PosX, PosY)
                                                   VALUES (@pid, @name, @amt, @cat, 0.5, 0.5)", conn))
                {
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
                                using var cmdS = new SqlCommand(@"INSERT INTO SplitBills (PostID, DebtorID, AmountOwed, IsPaid, StatusNote, CreatedAt)
                                                                  VALUES (@pid, @did, @owed, 0, N'Chưa đính kèm ảnh chuyển khoản', @now)", conn);
                                cmdS.Parameters.AddWithValue("@pid", newPostId);
                                cmdS.Parameters.AddWithValue("@did", debtorId.Value);
                                cmdS.Parameters.AddWithValue("@owed", share);
                                cmdS.Parameters.AddWithValue("@now", nowVn);
                                cmdS.ExecuteNonQuery();
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

            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            int? targetId = GetUserIdByName(conn, kw);
            if (!targetId.HasValue)
                return new AddFriendResponseDto { Success = false, Message = $"Không tìm thấy người dùng '{kw}' trên hệ thống!" };
            if (targetId.Value == req.RequesterId)
                return new AddFriendResponseDto { Success = false, Message = "Bạn không thể tự kết bạn với chính mình!" };

            using (var chk = new SqlCommand(@"SELECT COUNT(1) FROM Friendships
                                              WHERE (RequesterID = @r AND AddresseeID = @a) OR (RequesterID = @a AND AddresseeID = @r)", conn))
            {
                chk.Parameters.AddWithValue("@r", req.RequesterId);
                chk.Parameters.AddWithValue("@a", targetId.Value);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                    return new AddFriendResponseDto { Success = false, Message = $"Bạn đã kết nối bạn bè với '{kw}' trước đó rồi!" };
            }

            using (var cmd = new SqlCommand("INSERT INTO Friendships (RequesterID, AddresseeID, Status, CreatedAt) VALUES (@r, @a, 'Accepted', @now)", conn))
            {
                cmd.Parameters.AddWithValue("@r", req.RequesterId);
                cmd.Parameters.AddWithValue("@a", targetId.Value);
                cmd.Parameters.AddWithValue("@now", ServerCommon.NowVn());
                cmd.ExecuteNonQuery();
            }

            return new AddFriendResponseDto { Success = true, Message = $"✓ Đã kết bạn thành công với '{kw}'!" };
        }

        public AddFriendResponseDto RemoveFriend(AddFriendRequestDto? req)
        {
            string kw = (req?.TargetKeyword ?? "").Trim().TrimStart('@');
            if (req == null || string.IsNullOrEmpty(kw))
                return new AddFriendResponseDto { Success = false, Message = "Không xác định được bạn bè cần xóa!" };

            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            int? targetId = GetUserIdByName(conn, kw);
            if (!targetId.HasValue)
                return new AddFriendResponseDto { Success = false, Message = $"Không tìm thấy người dùng '{kw}'!" };

            using var cmd = new SqlCommand(@"DELETE FROM Friendships
                                             WHERE (RequesterID = @r AND AddresseeID = @a) OR (RequesterID = @a AND AddresseeID = @r)", conn);
            cmd.Parameters.AddWithValue("@r", req.RequesterId);
            cmd.Parameters.AddWithValue("@a", targetId.Value);
            if (cmd.ExecuteNonQuery() > 0)
                return new AddFriendResponseDto { Success = true, Message = $"Đã hủy kết bạn với '{kw}'." };

            return new AddFriendResponseDto { Success = false, Message = $"Bạn và '{kw}' hiện không phải bạn bè." };
        }

        public void SaveProofImage(UploadProofRequestDto req)
        {
            string fileName = ServerCommon.SaveBase64ToImageFile(storageFolder, req.ProofImageBase64, $"proof_{req.SplitId}");
            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE SplitBills SET ProofImagePath = @path, StatusNote = @note WHERE SplitID = @sid", conn);
            cmd.Parameters.AddWithValue("@path", fileName);
            cmd.Parameters.AddWithValue("@note", $"Đã gửi ảnh bill lúc {ServerCommon.NowVn():HH:mm}");
            cmd.Parameters.AddWithValue("@sid", req.SplitId);
            cmd.ExecuteNonQuery();
        }

        public void VerifySplitBill(VerifyBillRequestDto req)
        {
            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE SplitBills SET IsPaid = 1, PaidAt = @now, StatusNote = @note WHERE SplitID = @sid", conn);
            cmd.Parameters.AddWithValue("@now", ServerCommon.NowVn());
            cmd.Parameters.AddWithValue("@note", $"{req.VerifierName} đã xác nhận hoàn tất!");
            cmd.Parameters.AddWithValue("@sid", req.SplitId);
            cmd.ExecuteNonQuery();
        }

        public void UpdateUserBudget(UpdateBudgetRequestDto req)
        {
            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE Users SET MonthlyBudget = @b WHERE UserID = @uid", conn);
            cmd.Parameters.AddWithValue("@b", req.NewMonthlyBudget);
            cmd.Parameters.AddWithValue("@uid", req.UserId);
            cmd.ExecuteNonQuery();
        }

        public void UpdateCategoryBudget(UpdateCategoryBudgetRequestDto? req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Category) || req.NewLimit <= 0) return;
            using var conn = new SqlConnection(dbConnStr);
            conn.Open();
            string upsertSql = @"
IF EXISTS (SELECT 1 FROM UserCategoryBudgets WHERE UserID = @uid AND Category = @cat)
    UPDATE UserCategoryBudgets SET BudgetLimit = @lim WHERE UserID = @uid AND Category = @cat;
ELSE
    INSERT INTO UserCategoryBudgets (UserID, Category, BudgetLimit) VALUES (@uid, @cat, @lim);";
            using var cmd = new SqlCommand(upsertSql, conn);
            cmd.Parameters.AddWithValue("@uid", req.UserId);
            cmd.Parameters.AddWithValue("@cat", req.Category);
            cmd.Parameters.AddWithValue("@lim", req.NewLimit);
            cmd.ExecuteNonQuery();
        }

        private static int? GetUserIdByName(SqlConnection conn, string nameOrHandle)
        {
            string clean = nameOrHandle.Trim().TrimStart('@').ToLower();
            string noSpace = clean.Replace(" ", "");
            string sql = @"SELECT TOP 1 UserID FROM Users
                           WHERE LOWER(FullName) = @n
                              OR LOWER(REPLACE(FullName, N' (Bạn)', '')) = @n
                              OR LOWER(Username) = @n
                              OR LOWER(Username) = @ns
                           ORDER BY CASE WHEN LOWER(Username) = @ns THEN 0
                                         WHEN LOWER(Username) = @n THEN 1
                                         ELSE 2 END, UserID";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@n", clean);
            cmd.Parameters.AddWithValue("@ns", noSpace);
            object? val = cmd.ExecuteScalar();
            return val != null && val != DBNull.Value ? Convert.ToInt32(val) : null;
        }
    }
}
