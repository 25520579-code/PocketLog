using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PocketLog.Shared;

namespace PocketLog.Server
{
    public partial class Server
    {
        private static string HashPassword(string password)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(password ?? "");
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static bool VerifyPassword(string inputPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(inputPassword)) return false;
            string hashedInput = HashPassword(inputPassword);
            if (string.Equals(storedHash, hashedInput, StringComparison.OrdinalIgnoreCase))
                return true;

            // Tương thích ngược với các tài khoản mẫu cũ lưu mật khẩu thường hoặc 'hash123'
            if (storedHash == inputPassword) return true;
            if (storedHash == "hash123" && inputPassword == "123") return true;

            return false;
        }

        private LoginResponseDto HandleLoginSql(LoginRequestDto req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Username))
            {
                return new LoginResponseDto { Success = false, Message = "Tên tài khoản không được để trống!" };
            }
            if (string.IsNullOrWhiteSpace(req.Password))
            {
                return new LoginResponseDto { Success = false, Message = "Vui lòng nhập mật khẩu!" };
            }

            string rawInput = req.Username.Trim().TrimStart('@');
            string cleanUsername = rawInput.ToLower().Replace(" ", "");
            string passInput = req.Password.Trim();

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();

                if (req.IsRegister)
                {
                    string checkSql = @"SELECT COUNT(1) FROM Users 
                                        WHERE LOWER(Username) = @u OR LOWER(FullName) = @raw";
                    using (SqlCommand chkCmd = new SqlCommand(checkSql, conn))
                    {
                        chkCmd.Parameters.AddWithValue("@u", cleanUsername);
                        chkCmd.Parameters.AddWithValue("@raw", rawInput.ToLower());
                        int exists = Convert.ToInt32(chkCmd.ExecuteScalar());
                        if (exists > 0)
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
                    string passHash = HashPassword(passInput);

                    string insertSql = @"INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
                                         VALUES (@u, @p, @fn, @mb);
                                         SELECT SCOPE_IDENTITY();";
                    using (SqlCommand cmdNew = new SqlCommand(insertSql, conn))
                    {
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
                }
                else
                {
                    string sql = @"SELECT TOP 1 UserID, Username, FullName, ISNULL(MonthlyBudget, 3500000), PasswordHash
                                   FROM Users 
                                   WHERE LOWER(Username) = @u OR LOWER(Username) = @raw OR LOWER(FullName) = @raw";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@u", cleanUsername);
                        cmd.Parameters.AddWithValue("@raw", rawInput.ToLower());
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
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

                            if (!VerifyPassword(passInput, storedHash))
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
            }
        }

        private SyncDataResponseDto BuildSyncDataForUser(int userId)
        {
            var res = new SyncDataResponseDto { MonthlyBudget = 3500000 };
            var onlineUserIds = connectedClients.Values.ToHashSet();

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();

                // 1. Lấy tổng hạn mức tháng & hạn mức từng danh mục của user
                using (SqlCommand cmdB = new SqlCommand("SELECT ISNULL(MonthlyBudget, 3500000) FROM Users WHERE UserID = @uid", conn))
                {
                    cmdB.Parameters.AddWithValue("@uid", userId);
                    object? bObj = cmdB.ExecuteScalar();
                    if (bObj != null && bObj != DBNull.Value) res.MonthlyBudget = Convert.ToInt32(bObj);
                }

                using (SqlCommand cmdCB = new SqlCommand("SELECT Category, BudgetLimit FROM UserCategoryBudgets WHERE UserID = @uid", conn))
                {
                    cmdCB.Parameters.AddWithValue("@uid", userId);
                    using (SqlDataReader r = cmdCB.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            res.CategoryBudgets[r.GetString(0)] = r.GetInt32(1);
                        }
                    }
                }

                // 2. Lấy danh sách ID những người đã kết bạn
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

                // 3. Phân loại toàn bộ người dùng khác thành Bạn bè (MyFriends) & Gợi ý kết bạn (SuggestedUsers)
                string allOtherUsersSql = "SELECT UserID, Username, FullName FROM Users WHERE UserID <> @uid ORDER BY FullName ASC";
                using (SqlCommand cmdU = new SqlCommand(allOtherUsersSql, conn))
                {
                    cmdU.Parameters.AddWithValue("@uid", userId);
                    using (SqlDataReader r = cmdU.ExecuteReader())
                    {
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

                            if (friendIds.Contains(uid))
                                res.MyFriends.Add(dto);
                            else
                                res.SuggestedUsers.Add(dto);
                        }
                    }
                }

                // 4. Lấy bài đăng của bản thân hoặc bạn bè đã kết nối
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

        private AddFriendResponseDto AddFriendInSql(AddFriendRequestDto req)
        {
            string kw = (req?.TargetKeyword ?? "").Trim().TrimStart('@');
            if (string.IsNullOrEmpty(kw))
            {
                return new AddFriendResponseDto { Success = false, Message = "Vui lòng nhập tên hoặc username cần kết bạn!" };
            }

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                int? targetId = GetUserIdByName(conn, kw);
                if (!targetId.HasValue)
                {
                    return new AddFriendResponseDto
                    {
                        Success = false,
                        Message = $"Không tìm thấy người dùng '{kw}' trên hệ thống!"
                    };
                }

                if (targetId.Value == req!.RequesterId)
                {
                    return new AddFriendResponseDto
                    {
                        Success = false,
                        Message = "Bạn không thể tự kết bạn với chính mình!"
                    };
                }

                string existsSql = @"SELECT COUNT(1) FROM Friendships 
                                     WHERE (RequesterID = @r AND AddresseeID = @a) OR (RequesterID = @a AND AddresseeID = @r)";
                using (SqlCommand chk = new SqlCommand(existsSql, conn))
                {
                    chk.Parameters.AddWithValue("@r", req.RequesterId);
                    chk.Parameters.AddWithValue("@a", targetId.Value);
                    if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                    {
                        return new AddFriendResponseDto
                        {
                            Success = false,
                            Message = $"Bạn đã kết nối bạn bè với '{kw}' trước đó rồi!"
                        };
                    }
                }

                string insertSql = "INSERT INTO Friendships (RequesterID, AddresseeID, Status) VALUES (@r, @a, 'Accepted')";
                using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@r", req.RequesterId);
                    cmd.Parameters.AddWithValue("@a", targetId.Value);
                    cmd.ExecuteNonQuery();
                }

                return new AddFriendResponseDto
                {
                    Success = true,
                    Message = $"✓ Đã kết bạn thành công với '{kw}'!"
                };
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

        private void UpdateCategoryBudgetInSql(UpdateCategoryBudgetRequestDto req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Category) || req.NewLimit <= 0) return;

            using (SqlConnection conn = new SqlConnection(dbConnStr))
            {
                conn.Open();
                string upsertSql = @"
IF EXISTS (SELECT 1 FROM UserCategoryBudgets WHERE UserID = @uid AND Category = @cat)
    UPDATE UserCategoryBudgets SET BudgetLimit = @lim WHERE UserID = @uid AND Category = @cat;
ELSE
    INSERT INTO UserCategoryBudgets (UserID, Category, BudgetLimit) VALUES (@uid, @cat, @lim);";

                using (SqlCommand cmd = new SqlCommand(upsertSql, conn))
                {
                    cmd.Parameters.AddWithValue("@uid", req.UserId);
                    cmd.Parameters.AddWithValue("@cat", req.Category);
                    cmd.Parameters.AddWithValue("@lim", req.NewLimit);
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