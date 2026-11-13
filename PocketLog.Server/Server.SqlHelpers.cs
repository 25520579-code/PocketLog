using System.Data.SqlClient;
using PocketLog.Shared;

namespace PocketLog.Server
{
    public partial class Server
    {
        // ====================================================================
        // TRUY VẤN SQL: LỌC CHỈ BẠN BÈ MỚI THẤY BÀI NHAU & LẤY ĐỦ ACCOUNTS
        // ====================================================================
        private LoginResponseDto HandleLoginSql(LoginRequestDto req)
        {
            string rawInput = req.Username.Trim();
            string cleanUsername = rawInput.ToLower().Replace(" ", "");

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                // Tìm theo cả Username lẫn FullName để tránh tạo trùng tài khoản khi bấm chọn nhanh
                string sql = @"SELECT TOP 1 UserID, Username, FullName, ISNULL(MonthlyBudget, 3500000) 
                               FROM Users 
                               WHERE LOWER(Username) = @u OR LOWER(Username) = @raw OR LOWER(FullName) = @raw";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@u", cleanUsername);
                    cmd.Parameters.AddWithValue("@raw", rawInput.ToLower());
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new LoginResponseDto
                            {
                                Success = true,
                                UserId = r.GetInt32(0),
                                Username = r.GetString(1),
                                FullName = r.GetString(2),
                                MonthlyBudget = r.GetInt32(3),
                                Message = "OK"
                            };
                        }
                    }
                }

                string insertSql = @"INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
                                     VALUES (@u, @p, @fn, 3500000);
                                     SELECT SCOPE_IDENTITY();";
                using (SqlCommand cmdNew = new SqlCommand(insertSql, conn))
                {
                    cmdNew.Parameters.AddWithValue("@u", cleanUsername);
                    cmdNew.Parameters.AddWithValue("@p", string.IsNullOrEmpty(req.Password) ? "123" : req.Password);
                    cmdNew.Parameters.AddWithValue("@fn", rawInput);
                    int newId = Convert.ToInt32(cmdNew.ExecuteScalar());
                    return new LoginResponseDto
                    {
                        Success = true,
                        UserId = newId,
                        Username = cleanUsername,
                        FullName = rawInput,
                        MonthlyBudget = 3500000,
                        Message = "Created"
                    };
                }
            }
        }

        private SyncDataResponseDto BuildSyncDataForUser(int userId)
        {
            var res = new SyncDataResponseDto { MonthlyBudget = 3500000 };
            var onlineUserIds = connectedClients.Values.ToHashSet();

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();

                // 1. Lấy hạn mức của user
                using (SqlCommand cmdB = new SqlCommand("SELECT ISNULL(MonthlyBudget, 3500000) FROM Users WHERE UserID = @uid", conn))
                {
                    cmdB.Parameters.AddWithValue("@uid", userId);
                    object? bObj = cmdB.ExecuteScalar();
                    if (bObj != null && bObj != DBNull.Value) res.MonthlyBudget = Convert.ToInt32(bObj);
                }

                // 2. Lấy danh sách ID những người ĐÃ KẾT BẠN với userId
                var friendIds = new HashSet<int>();
                string friendSql = @"SELECT RequesterID, AddresseeID FROM Friendships
                                     WHERE (RequesterID = @uid OR AddresseeID = @uid) AND Status = 'Accepted'";
                using (SqlCommand cmdF = new SqlCommand(friendSql, conn))
                {
                    cmdF.Parameters.AddWithValue("@uid", userId);
                    using (SqlDataReader r = cmdF.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int reqId = r.GetInt32(0);
                            int addId = r.GetInt32(1);
                            friendIds.Add(reqId == userId ? addId : reqId);
                        }
                    }
                }

                // 3. Lấy TẤT CẢ các tài khoản đang có trong bảng Users của Database
                string allUsersSql = "SELECT UserID, Username, FullName FROM Users WHERE UserID <> @uid ORDER BY UserID ASC";
                using (SqlCommand cmdU = new SqlCommand(allUsersSql, conn))
                {
                    cmdU.Parameters.AddWithValue("@uid", userId);
                    using (SqlDataReader r = cmdU.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int uid = r.GetInt32(0);
                            string uname = r["Username"]?.ToString() ?? "";
                            string fname = r["FullName"]?.ToString() ?? uname;
                            // Bỏ hậu tố "(Bạn)" nếu dữ liệu cũ từ script SQL có dính chữ "(Bạn)"
                            fname = fname.Replace(" (Bạn)", "").Trim();

                            var fDto = new FriendDto
                            {
                                UserId = uid,
                                Username = "@" + uname,
                                FullName = fname,
                                IsOnline = onlineUserIds.Contains(uid)
                            };

                            if (friendIds.Contains(uid))
                                res.MyFriends.Add(fDto);
                            else
                                res.SuggestedUsers.Add(fDto);
                        }
                    }
                }

                // 4. Lấy bài đăng: CHỈ LẤY BÀI CỦA BẢN THÂN HOẶC CỦA BẠN BÈ ĐÃ KẾT NỐI
                string postSql = @"SELECT p.PostID, p.UserID, u.FullName, p.ImagePath, p.Caption, p.IsExpenseTracked, p.TotalAmount, p.CreatedAt,
                                          i.ItemName, i.Category
                                   FROM ExpensePosts p
                                   INNER JOIN Users u ON p.UserID = u.UserID
                                   LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                                   ORDER BY p.PostID DESC";
                using (SqlCommand cmdP = new SqlCommand(postSql, conn))
                using (SqlDataReader r = cmdP.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int authorId = r.GetInt32(1);

                        // CHẶN BÀI ĐĂNG CỦA NGƯỜI LẠ: Chỉ cho phép bài của mình hoặc của bạn bè trong friendIds
                        if (authorId != userId && !friendIds.Contains(authorId))
                            continue;

                        int postId = r.GetInt32(0);
                        string imgPath = r["ImagePath"]?.ToString() ?? "";
                        DateTime createdAt = r["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(r["CreatedAt"]) : DateTime.Now;
                        string authorName = (r["FullName"]?.ToString() ?? "").Replace(" (Bạn)", "").Trim();

                        res.Posts.Add(new PostDto
                        {
                            PostId = postId,
                            AuthorId = authorId,
                            AuthorName = authorName,
                            ImageBase64 = ReadImageFileAsBase64(imgPath),
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
                    string tagSql = @"SELECT u.FullName FROM SplitBills s
                                      INNER JOIN Users u ON s.DebtorID = u.UserID
                                      WHERE s.PostID = @pid";
                    using (SqlCommand cmdT = new SqlCommand(tagSql, conn))
                    {
                        cmdT.Parameters.AddWithValue("@pid", post.PostId);
                        using (SqlDataReader r = cmdT.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                string taggedName = r.GetString(0).Replace(" (Bạn)", "").Trim();
                                post.TaggedFriends.Add(taggedName);
                            }
                        }
                    }
                }

                // 5. Lấy danh sách các khoản chia tiền liên quan đến user
                string splitSql = @"SELECT s.SplitID, s.PostID, p.UserID AS CreditorID, uCred.FullName AS CreditorName,
                                           s.DebtorID, uDebt.FullName AS DebtorName,
                                           ISNULL(i.ItemName, p.Caption) AS Title, ISNULL(i.Category, N'🍜 Đồ ăn/Thức uống') AS Category,
                                           p.TotalAmount, s.AmountOwed, s.IsPaid, s.ProofImagePath, s.StatusNote, s.CreatedAt
                                    FROM SplitBills s
                                    INNER JOIN ExpensePosts p ON s.PostID = p.PostID
                                    INNER JOIN Users uCred ON p.UserID = uCred.UserID
                                    INNER JOIN Users uDebt ON s.DebtorID = uDebt.UserID
                                    LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                                    WHERE s.DebtorID = @uid OR p.UserID = @uid
                                    ORDER BY s.SplitID DESC";
                using (SqlCommand cmdS = new SqlCommand(splitSql, conn))
                {
                    cmdS.Parameters.AddWithValue("@uid", userId);
                    using (SqlDataReader r = cmdS.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            string proofPath = r["ProofImagePath"] == DBNull.Value ? "" : r["ProofImagePath"].ToString()!;
                            DateTime created = r["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(r["CreatedAt"]) : DateTime.Now;
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
                                ProofImageBase64 = ReadImageFileAsBase64(proofPath),
                                StatusNote = r["StatusNote"]?.ToString() ?? "",
                                Time = created.ToString("HH:mm dd/MM")
                            });
                        }
                    }
                }
            }

            return res;
        }

        private void SaveNewPostToSql(PostDto dto)
        {
            string savedFileName = SaveBase64ToImageFile(dto.ImageBase64, "post");

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                string insertPost = @"INSERT INTO ExpensePosts (UserID, ImagePath, Caption, IsPublicToFriends, IsExpenseTracked, TotalAmount, CreatedAt)
                                      VALUES (@uid, @img, @cap, 1, @tracked, @amt, GETDATE());
                                      SELECT SCOPE_IDENTITY();";
                int newPostId;
                using (SqlCommand cmd = new SqlCommand(insertPost, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", dto.AuthorId);
                    cmd.Parameters.AddWithValue("@img", savedFileName);
                    cmd.Parameters.AddWithValue("@cap", dto.CaptionInside);
                    cmd.Parameters.AddWithValue("@tracked", dto.HasTag ? 1 : 0);
                    cmd.Parameters.AddWithValue("@amt", dto.Amount);
                    newPostId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (dto.HasTag && newPostId > 0)
                {
                    string insertItem = @"INSERT INTO ExpenseItems (PostID, ItemName, Amount, Category, PosX, PosY)
                                          VALUES (@pid, @name, @amt, @cat, 0.5, 0.5)";
                    using (SqlCommand cmdI = new SqlCommand(insertItem, conn))
                    {
                        cmdI.Parameters.AddWithValue("@pid", newPostId);
                        cmdI.Parameters.AddWithValue("@name", dto.NoteTop);
                        cmdI.Parameters.AddWithValue("@amt", dto.Amount);
                        cmdI.Parameters.AddWithValue("@cat", dto.Category);
                        cmdI.ExecuteNonQuery();
                    }

                    if (dto.TaggedFriends != null && dto.TaggedFriends.Count > 0)
                    {
                        int share = dto.Amount / (dto.TaggedFriends.Count + 1);
                        foreach (string friendName in dto.TaggedFriends)
                        {
                            int? debtorId = GetUserIdByName(conn, friendName);
                            if (debtorId.HasValue && debtorId.Value != dto.AuthorId)
                            {
                                string insertSplit = @"INSERT INTO SplitBills (PostID, DebtorID, AmountOwed, IsPaid, StatusNote, CreatedAt)
                                                       VALUES (@pid, @did, @owed, 0, N'Chưa đính kèm ảnh chuyển khoản', GETDATE())";
                                using (SqlCommand cmdS = new SqlCommand(insertSplit, conn))
                                {
                                    cmdS.Parameters.AddWithValue("@pid", newPostId);
                                    cmdS.Parameters.AddWithValue("@did", debtorId.Value);
                                    cmdS.Parameters.AddWithValue("@owed", share);
                                    cmdS.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddFriendInSql(AddFriendRequestDto req)
        {
            string kw = req.TargetKeyword.Trim().TrimStart('@');
            if (string.IsNullOrEmpty(kw)) return;

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                int? targetId = GetUserIdByName(conn, kw);
                if (!targetId.HasValue)
                {
                    string newU = @"INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
                                    VALUES (@u, '123', @fn, 3000000);
                                    SELECT SCOPE_IDENTITY();";
                    using (SqlCommand cmd = new SqlCommand(newU, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", kw.ToLower().Replace(" ", ""));
                        cmd.Parameters.AddWithValue("@fn", kw);
                        targetId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }

                if (targetId.HasValue && targetId.Value != req.RequesterId)
                {
                    string checkSql = @"IF NOT EXISTS (SELECT 1 FROM Friendships 
                                        WHERE (RequesterID = @r AND AddresseeID = @a) OR (RequesterID = @a AND AddresseeID = @r))
                                        INSERT INTO Friendships (RequesterID, AddresseeID, Status) VALUES (@r, @a, 'Accepted')";
                    using (SqlCommand cmd = new SqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@r", req.RequesterId);
                        cmd.Parameters.AddWithValue("@a", targetId.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void SaveProofImageToSql(UploadProofRequestDto req)
        {
            string fileName = SaveBase64ToImageFile(req.ProofImageBase64, $"proof_{req.SplitId}");
            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                string sql = @"UPDATE SplitBills 
                               SET ProofImagePath = @path, StatusNote = @note 
                               WHERE SplitID = @sid";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@path", fileName);
                    cmd.Parameters.AddWithValue("@note", $"Đã gửi ảnh bill lúc {DateTime.Now:HH:mm}");
                    cmd.Parameters.AddWithValue("@sid", req.SplitId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void VerifySplitBillInSql(VerifyBillRequestDto req)
        {
            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                string sql = @"UPDATE SplitBills 
                               SET IsPaid = 1, PaidAt = GETDATE(), StatusNote = @note 
                               WHERE SplitID = @sid";
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@note", $"{req.VerifierName} đã xác nhận hoàn tất!");
                    cmd.Parameters.AddWithValue("@sid", req.SplitId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void UpdateUserBudgetInSql(UpdateBudgetRequestDto req)
        {
            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("UPDATE Users SET MonthlyBudget = @b WHERE UserID = @uid", conn))
                {
                    cmd.Parameters.AddWithValue("@b", req.NewMonthlyBudget);
                    cmd.Parameters.AddWithValue("@uid", req.UserId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private int? GetUserIdByName(SqlConnection conn, string nameOrHandle)
        {
            string clean = nameOrHandle.Trim().TrimStart('@').ToLower();
            string noSpace = clean.Replace(" ", "");
            string sql = @"SELECT TOP 1 UserID FROM Users 
                           WHERE LOWER(FullName) = @n 
                              OR LOWER(REPLACE(FullName, N' (Bạn)', '')) = @n
                              OR LOWER(Username) = @n 
                              OR LOWER(Username) = @ns";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@n", clean);
                cmd.Parameters.AddWithValue("@ns", noSpace);
                object? val = cmd.ExecuteScalar();
                return val != null && val != DBNull.Value ? Convert.ToInt32(val) : (int?)null;
            }
        }

        private string SaveBase64ToImageFile(string base64, string prefix)
        {
            if (string.IsNullOrEmpty(base64)) return "";
            try
            {
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

        private string ReadImageFileAsBase64(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return "";
            try
            {
                string fullPath = Path.Combine(storageFolder, fileName);
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
