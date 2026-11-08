using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PocketLog.Client
{
    public partial class Form1 : Form
    {
        private Guna2BorderlessForm borderlessForm;
        private Guna2DragControl dragControl;
        private Guna2Panel pnlHeader, pnlMainContainer, pnlBottomNav;
        private Guna2Button btnTabFeed, btnTabCamera, btnTabExpense;

        // Ngăn kéo Bạn bè trượt ngang
        private Panel pnlDrawerBackdrop;
        private Guna2Panel pnlFriendsDrawer;
        private System.Windows.Forms.Timer drawerTimer;
        private int drawerTargetX;

        // Bảng màu Clean Light UI
        private readonly Color clrAppBg = Color.FromArgb(245, 247, 246);
        private readonly Color clrCardBg = Color.White;
        private readonly Color clrPrimaryGreen = Color.FromArgb(29, 99, 76);
       
        private readonly Color clrSoftGreenBg = Color.FromArgb(232, 245, 239);
        private readonly Color clrSoftGreenBorder = Color.FromArgb(132, 190, 168);
        private readonly Color clrIndigo = Color.FromArgb(57, 66, 167);
        private readonly Color clrSoftIndigo = Color.FromArgb(234, 236, 252);
        private readonly Color clrRedDanger = Color.FromArgb(166, 58, 58);
        private readonly Color clrBadgeOrangeBg = Color.FromArgb(253, 238, 228);
        private readonly Color clrBadgeOrangeTxt = Color.FromArgb(168, 85, 34);
        private readonly Color clrBorderLight = Color.FromArgb(218, 224, 221);
        private readonly Color clrTextMain = Color.FromArgb(28, 32, 30);
        private readonly Color clrTextMuted = Color.FromArgb(110, 118, 114); 
        private readonly string connString = ConfigurationManager.ConnectionStrings["PocketLogDBConn"]?.ConnectionString;

        private readonly (string Name, string Emoji)[] standardCategories = new[]
        {
            ("🍜 Đồ ăn/Thức uống", "🍜"),
            ("⛽ Xăng cộ", "⛽"),
            ("👕 Quần áo", "👕"),
            ("🏠 Tiền trọ", "🏠"),
            ("🛒 Mua sắm", "🛒"),
            ("📚 Học tập/Khác", "📚")
        };

        private class FriendUser
        {
            public string Name { get; set; }
            public string Handle { get; set; }
            public bool IsOnline { get; set; }
        }

        private class LocketPost
        {
            public bool IsMine { get; set; }
            public string Author { get; set; }
            public bool HasTag { get; set; }
            public List<string> TaggedFriends { get; set; } = new List<string>();
            public string Category { get; set; }
            public string NoteTop { get; set; }
            public string CaptionInside { get; set; }
            public int Amount { get; set; }
            public string Time { get; set; }
            public string Emoji { get; set; }
            public Color PhotoBg { get; set; }
            public Image CustomImage { get; set; }
        }

        private class TaggedDebtItem
        {
            public string Id { get; set; }
            public string FriendName { get; set; }
            public string Title { get; set; }
            public string Category { get; set; }
            public int TotalAmount { get; set; }
            public int MyShare { get; set; }
            public string SplitDetail { get; set; }
            public string Time { get; set; }
            public bool IsSentProof { get; set; }
            public bool IsConfirmedPaid { get; set; }
            public Image ProofImage { get; set; }
            public string StatusNote { get; set; }
        }

        private readonly List<FriendUser> myFriends = new List<FriendUser>();
        private readonly List<FriendUser> friendSuggestions = new List<FriendUser>();
        private readonly List<LocketPost> posts = new List<LocketPost>();
        private readonly List<TaggedDebtItem> taggedDebts = new List<TaggedDebtItem>();
        private readonly Dictionary<string, int> categoryBudgets = new Dictionary<string, int>();

        private int totalBudgetLimit = 3500000;
        private bool isUnpaidDropdownOpen = false;
        private string selectedDebtId = "D1";

        public Form1()
        {
            InitializeComponent();
            SeedInitialData();
            LoadPostsFromDatabase();
            SetupPhoneShell();
        }

        private void SeedInitialData()
        {
            myFriends.Add(new FriendUser { Name = "Gia Huy", Handle = "@giahuy", IsOnline = true });
            myFriends.Add(new FriendUser { Name = "Minh Khoa", Handle = "@minhkhoa", IsOnline = true });

            friendSuggestions.Add(new FriendUser { Name = "Thanh Thảo", Handle = "@thanhthao", IsOnline = true });
            friendSuggestions.Add(new FriendUser { Name = "Tuấn Kiệt", Handle = "@tuankiet", IsOnline = false });
            friendSuggestions.Add(new FriendUser { Name = "Hoàng Yến", Handle = "@hoangyen", IsOnline = true });

            categoryBudgets["🍜 Đồ ăn/Thức uống"] = 1200000;
            categoryBudgets["⛽ Xăng cộ"] = 300000;
            categoryBudgets["👕 Quần áo"] = 500000;
            categoryBudgets["🏠 Tiền trọ"] = 1500000;
            categoryBudgets["🛒 Mua sắm"] = 400000;
            categoryBudgets["📚 Học tập/Khác"] = 300000;

            posts.Add(new LocketPost
            {
                IsMine = true,
                Author = "Huy Hoàng (Bạn)",
                HasTag = true,
                TaggedFriends = new List<string>(),
                Category = "⛽ Xăng cộ",
                NoteTop = "Đổ đầy bình Sirius • 50.000đ",
                CaptionInside = "Đổ xăng sáng sớm chạy lên trường học mạng máy tính 🛵",
                Amount = 50000,
                Time = "Vừa xong",
                Emoji = "⛽",
                PhotoBg = Color.FromArgb(92, 128, 114)
            });

            posts.Add(new LocketPost
            {
                IsMine = true,
                Author = "Huy Hoàng (Bạn)",
                HasTag = true,
                TaggedFriends = new List<string> { "Gia Huy" },
                Category = "🍜 Đồ ăn/Thức uống",
                NoteTop = "Cơm tấm và Trà tắc • 90.000đ",
                CaptionInside = "Ăn trưa cùng Gia Huy lấy sức làm đồ án NT106",
                Amount = 90000,
                Time = "Hôm qua",
                Emoji = "🍱",
                PhotoBg = Color.FromArgb(158, 126, 102)
            });

            posts.Add(new LocketPost
            {
                IsMine = false,
                Author = "Gia Huy",
                HasTag = true,
                TaggedFriends = new List<string> { "Huy Hoàng" },
                Category = "🍜 Đồ ăn/Thức uống",
                NoteTop = "Cà phê sữa đá và Bạc xỉu • 64.000đ",
                CaptionInside = "Ngồi bàn T05 đợi ông qua code chung nhé ☕",
                Amount = 64000,
                Time = "14:32",
                Emoji = "☕",
                PhotoBg = Color.FromArgb(172, 148, 128)
            });

            posts.Add(new LocketPost
            {
                IsMine = false,
                Author = "Gia Huy",
                HasTag = true,
                TaggedFriends = new List<string> { "Huy Hoàng" },
                Category = "🏠 Tiền trọ",
                NoteTop = "Tiền điện nước tháng này • 280.000đ",
                CaptionInside = "Tháng này bật máy lạnh hơi nhiều anh em ơi 💸",
                Amount = 280000,
                Time = "11:20",
                Emoji = "🏠",
                PhotoBg = Color.FromArgb(106, 118, 148)
            });

            posts.Add(new LocketPost
            {
                IsMine = false,
                Author = "Minh Khoa",
                HasTag = true,
                TaggedFriends = new List<string> { "Huy Hoàng", "Gia Huy" },
                Category = "🍜 Đồ ăn/Thức uống",
                NoteTop = "Trà sữa và bánh tráng • 135.000đ",
                CaptionInside = "Nạp đường buổi tối chạy deadline Socket 🔥",
                Amount = 135000,
                Time = "Hôm qua",
                Emoji = "🧋",
                PhotoBg = Color.FromArgb(128, 112, 138)
            });

            // Danh sách các khoản chi được gắn tag với bạn bè mà bản thân chưa thanh toán
            taggedDebts.Add(new TaggedDebtItem
            {
                Id = "D1",
                FriendName = "Gia Huy",
                Title = "Cà phê sữa đá và Bạc xỉu",
                Category = "🍜 Đồ ăn/Thức uống",
                TotalAmount = 64000,
                MyShare = 32000,
                SplitDetail = "Tag chung với Gia Huy (Chia 2)",
                Time = "14:32 hôm nay",
                StatusNote = "Chưa đính kèm ảnh chuyển khoản"
            });

            taggedDebts.Add(new TaggedDebtItem
            {
                Id = "D2",
                FriendName = "Gia Huy",
                Title = "Tiền điện nước phòng trọ",
                Category = "🏠 Tiền trọ",
                TotalAmount = 280000,
                MyShare = 140000,
                SplitDetail = "Tag chung với Gia Huy (Chia 2)",
                Time = "11:20 hôm nay",
                StatusNote = "Chưa đính kèm ảnh chuyển khoản"
            });

            taggedDebts.Add(new TaggedDebtItem
            {
                Id = "D3",
                FriendName = "Minh Khoa",
                Title = "Trà sữa và bánh tráng tối",
                Category = "🍜 Đồ ăn/Thức uống",
                TotalAmount = 135000,
                MyShare = 45000,
                SplitDetail = "Tag chung với Minh Khoa, Gia Huy (Chia 3)",
                Time = "20:15 hôm qua",
                StatusNote = "Chưa đính kèm ảnh chuyển khoản"
            });
        }

        private Label MakeLabel(string text, int x, int y, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };
        }
        private List<(string Name, string Emoji)> GetCategoriesFromDatabase()
        {
            return new List<(string Name, string Emoji)>
            {
                ("🍜 Đồ ăn/Thức uống", "🍜"),
                ("⛽ Xăng cộ", "⛽"),
                ("👕 Quần áo", "👕"),
                ("🏠 Tiền trọ", "🏠"),
                ("🛒 Mua sắm", "🛒"),
                ("📚 Học tập/Khác", "📚")
            };

        }
        private void LoadPostsFromDatabase()
        {
            string connString = ConfigurationManager.ConnectionStrings["PocketLogDBConn"]?.ConnectionString;
            if (string.IsNullOrEmpty(connString)) return;

            using (SqlConnection conn = new SqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    // Đọc các bài đăng từ CSDL, xếp bài mới nhất lên đầu
                    string query = @"SELECT p.PostID, p.Caption, p.TotalAmount, p.CreatedAt, i.ItemName, i.Category 
                               FROM ExpensePosts p
                               LEFT JOIN ExpenseItems i ON p.PostID = i.PostID
                               ORDER BY p.PostID ASC"; // Sắp xếp tăng dần để khi Insert(0) bài mới nhất sẽ nằm trên cùng

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string caption = reader["Caption"]?.ToString() ?? "";
                            int totalAmt = reader["TotalAmount"] != DBNull.Value ? Convert.ToInt32(reader["TotalAmount"]) : 0;
                            string itemName = reader["ItemName"]?.ToString() ?? "Khoản chi";
                            string category = reader["Category"]?.ToString() ?? "🍜 Đồ ăn/Thức uống";

                            // ĐỔI THÀNH Insert(0, ...) ĐỂ BÀI MỚI CHÈN LÊN ĐẦU DANH SÁCH MÀN HÌNH CHÍNH
                            posts.Insert(0, new LocketPost
                            {
                                IsMine = true,
                                Author = "Huy Hoàng (Bạn)",
                                HasTag = true,
                                Category = category,
                                NoteTop = $"{itemName} • {totalAmt:N0}đ",
                                CaptionInside = caption,
                                Amount = totalAmt,
                                Time = "Vừa xong",
                                Emoji = "🍜",
                                PhotoBg = Color.FromArgb(68, 118, 98)
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Tránh crash nếu CSDL bận
                }
            }
        }

        private Guna2Button MakeBtn(string text, int x, int y, int w, int h, int radius, Color fill, Color fore, Color border, int borderThick = 1, float fontSize = 8.5F)
        {
            return new Guna2Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BorderRadius = radius,
                FillColor = fill,
                ForeColor = fore,
                BorderColor = border,
                BorderThickness = borderThick,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
        }

        private void SetupPhoneShell()
        {
            this.Controls.Clear();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(420, 820);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrAppBg;

            borderlessForm = new Guna2BorderlessForm
            {
                ContainerControl = this,
                BorderRadius = 28,
                ResizeForm = false,
                HasFormShadow = true
            };

            pnlHeader = new Guna2Panel { Dock = DockStyle.Top, Height = 52, FillColor = clrCardBg };
            pnlHeader.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorderLight });
            pnlHeader.Controls.Add(MakeLabel("● PocketLog", 18, 13, 13F, FontStyle.Bold, clrPrimaryGreen));

            Guna2ControlBox btnClose = new Guna2ControlBox
            {
                Location = new Point(372, 11),
                Size = new Size(34, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8,
                HoverState = { FillColor = Color.FromArgb(232, 17, 35), IconColor = Color.White }
            };
            Guna2ControlBox btnMin = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MinimizeBox,
                Location = new Point(332, 11),
                Size = new Size(34, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8
            };
            pnlHeader.Controls.Add(btnMin);
            pnlHeader.Controls.Add(btnClose);
            dragControl = new Guna2DragControl { TargetControl = pnlHeader };

            // Thanh điều hướng 3 nút dưới đáy (cỡ chữ 8.8F nằm gọn trên 1 dòng)
            pnlBottomNav = new Guna2Panel { Dock = DockStyle.Bottom, Height = 74, FillColor = clrCardBg };
            pnlBottomNav.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorderLight });

            btnTabFeed = MakeBtn("Bảng tin", 14, 14, 114, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.8F);
            btnTabCamera = MakeBtn("Chụp ảnh", 136, 14, 114, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.8F);
            btnTabExpense = MakeBtn("Quản lý chi tiêu", 258, 14, 148, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.8F);

            btnTabFeed.Click += (s, e) => ActivateMenu(btnTabFeed, BuildFeedScreen());
            btnTabCamera.Click += (s, e) => ActivateMenu(btnTabCamera, BuildCameraScreen());
            btnTabExpense.Click += (s, e) => ActivateMenu(btnTabExpense, BuildExpenseScreen());

            pnlBottomNav.Controls.AddRange(new Control[] { btnTabFeed, btnTabCamera, btnTabExpense });

            pnlMainContainer = new Guna2Panel { Dock = DockStyle.Fill, FillColor = clrAppBg };

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottomNav);

            SetupFriendsDrawer();
            ActivateMenu(btnTabFeed, BuildFeedScreen());
        }

        private void ActivateMenu(Guna2Button selectedBtn, Control screenContent)
        {
            foreach (var btn in new[] { btnTabFeed, btnTabCamera, btnTabExpense })
            {
                btn.FillColor = clrCardBg;
                btn.ForeColor = clrTextMain;
                btn.BorderColor = clrBorderLight;
                btn.BorderThickness = 1;
            }
            selectedBtn.FillColor = clrPrimaryGreen;
            selectedBtn.ForeColor = Color.White;
            selectedBtn.BorderColor = clrSoftGreenBorder;
            selectedBtn.BorderThickness = 2;

            pnlMainContainer.Controls.Clear();
            screenContent.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(screenContent);
        }

        // ====================================================================
        // NGĂN KÉO DANH SÁCH BẠN BÈ & TÌM KIẾM KẾT BẠN (TRƯỢT NGANG TỪ GÓC TRÊN)
        // ====================================================================
        private void SetupFriendsDrawer()
        {
            pnlDrawerBackdrop = new Panel
            {
                Location = new Point(0, 52),
                Size = new Size(420, 768),
                BackColor = Color.FromArgb(222, 230, 226),
                Visible = false
            };
            pnlDrawerBackdrop.Click += (s, e) => CloseFriendsDrawer();

            pnlFriendsDrawer = new Guna2Panel
            {
                Location = new Point(420, 0),
                Size = new Size(344, 768),
                FillColor = clrCardBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2
            };
            pnlDrawerBackdrop.Controls.Add(pnlFriendsDrawer);
            this.Controls.Add(pnlDrawerBackdrop);

            drawerTimer = new System.Windows.Forms.Timer { Interval = 12 };
            drawerTimer.Tick += (s, e) =>
            {
                int cur = pnlFriendsDrawer.Left;
                if (cur > drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Max(drawerTargetX, cur - Math.Max(20, (cur - drawerTargetX) / 2));
                }
                else if (cur < drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Min(drawerTargetX, cur + Math.Max(20, (drawerTargetX - cur) / 2));
                    if (pnlFriendsDrawer.Left >= 420)
                    {
                        pnlDrawerBackdrop.Visible = false;
                        drawerTimer.Stop();
                    }
                }
                else drawerTimer.Stop();
            };
        }

        private void OpenFriendsDrawer()
        {
            RenderFriendsDrawerContent("");
            pnlDrawerBackdrop.Visible = true;
            pnlDrawerBackdrop.BringToFront();
            pnlFriendsDrawer.Left = 420;
            drawerTargetX = 76;
            drawerTimer.Start();
        }

        private void CloseFriendsDrawer()
        {
            drawerTargetX = 420;
            drawerTimer.Start();
            if (btnTabFeed.FillColor == clrPrimaryGreen)
                ActivateMenu(btnTabFeed, BuildFeedScreen());
        }

        private void RenderFriendsDrawerContent(string keyword)
        {
            pnlFriendsDrawer.Controls.Clear();
            pnlFriendsDrawer.Controls.Add(MakeLabel($"👥 Danh sách bạn bè ({myFriends.Count})", 16, 16, 11.5F, FontStyle.Bold, clrTextMain));

            var btnClose = MakeBtn("✕", 294, 12, 34, 32, 10, clrAppBg, clrTextMain, clrBorderLight);
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            Guna2TextBox txtSearch = new Guna2TextBox
            {
                Text = keyword,
                PlaceholderText = "🔍 Tìm tên hoặc nhập tên kết bạn...",
                Location = new Point(16, 54),
                Size = new Size(216, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnAddNew = MakeBtn("+ Kết bạn", 238, 54, 90, 36, 10, clrPrimaryGreen, Color.White, clrSoftGreenBorder);
            Panel listScroll = new Panel { Location = new Point(16, 100), Size = new Size(312, 650), AutoScroll = true };

            Action<string> refreshList = (kw) =>
            {
                listScroll.Controls.Clear();
                string q = (kw ?? "").Trim().ToLower();
                int y = 4;

                listScroll.Controls.Add(MakeLabel("Gợi ý kết bạn mới", 2, y, 8.8F, FontStyle.Bold, clrIndigo));
                y += 22;

                var sugs = friendSuggestions
                    .Where(u => !myFriends.Any(f => f.Name.Equals(u.Name, StringComparison.OrdinalIgnoreCase)))
                    .Where(u => string.IsNullOrEmpty(q) || u.Name.ToLower().Contains(q) || u.Handle.ToLower().Contains(q))
                    .ToList();

                foreach (var sUser in sugs)
                {
                    var card = BuildFriendItemRow(sUser, false, () =>
                    {
                        myFriends.Add(sUser);
                        RenderFriendsDrawerContent(txtSearch.Text);
                    });
                    card.Location = new Point(0, y);
                    listScroll.Controls.Add(card);
                    y += 56;
                }

                y += 8;
                listScroll.Controls.Add(MakeLabel("Bạn bè hiện tại (Có thể gắn tag chia tiền)", 2, y, 8.8F, FontStyle.Bold, clrPrimaryGreen));
                y += 22;

                foreach (var fUser in myFriends.Where(f => string.IsNullOrEmpty(q) || f.Name.ToLower().Contains(q) || f.Handle.ToLower().Contains(q)))
                {
                    var card = BuildFriendItemRow(fUser, true, null);
                    card.Location = new Point(0, y);
                    listScroll.Controls.Add(card);
                    y += 56;
                }
            };

            txtSearch.TextChanged += (s, e) => refreshList(txtSearch.Text);
            btnAddNew.Click += (s, e) =>
            {
                string raw = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(raw)) return;
                string name = raw.StartsWith("@") ? raw.Substring(1) : raw;
                if (!myFriends.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    myFriends.Add(new FriendUser
                    {
                        Name = char.ToUpper(name[0]) + name.Substring(1),
                        Handle = "@" + name.ToLower().Replace(" ", ""),
                        IsOnline = true
                    });
                    txtSearch.Text = "";
                    refreshList("");
                }
            };

            refreshList(keyword);
            pnlFriendsDrawer.Controls.AddRange(new Control[] { txtSearch, btnAddNew, listScroll });
        }

        private Guna2Panel BuildFriendItemRow(FriendUser u, bool isFriend, Action onAdd)
        {
            Guna2Panel row = new Guna2Panel
            {
                Size = new Size(308, 50),
                BorderRadius = 12,
                FillColor = isFriend ? clrAppBg : clrSoftIndigo,
                BorderColor = isFriend ? clrBorderLight : Color.FromArgb(180, 188, 240),
                BorderThickness = 1
            };

            Guna2CircleButton avt = new Guna2CircleButton
            {
                Text = u.Name.Substring(0, 1),
                Size = new Size(32, 32),
                Location = new Point(10, 9),
                FillColor = isFriend ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            row.Controls.Add(avt);
            row.Controls.Add(MakeLabel(u.Name, 48, 7, 9F, FontStyle.Bold, clrTextMain));
            row.Controls.Add(MakeLabel($"{u.Handle} • {(u.IsOnline ? "Online" : "Offline")}", 48, 26, 7.8F, FontStyle.Regular, u.IsOnline ? clrPrimaryGreen : clrTextMuted));

            if (!isFriend && onAdd != null)
            {
                var btn = MakeBtn("+ Kết bạn", 218, 10, 80, 30, 8, clrIndigo, Color.White, clrIndigo, 1, 7.8F);
                btn.Click += (s, e) => onAdd();
                row.Controls.Add(btn);
            }
            else
            {
                row.Controls.Add(MakeBtn("✓ Bạn bè", 222, 11, 76, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F));
            }
            return row;
        }

        // ====================================================================
        // MÀN HÌNH 1: BẢNG TIN (GÓC TRÊN MỞ BẠN BÈ + ẢNH GẦN NHẤT + BẢNG TIN BẠN BÈ)
        // ====================================================================
        private Control BuildFeedScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            root.Controls.Add(MakeLabel("Ảnh gần đây nhất", 20, y + 4, 11.5F, FontStyle.Bold, clrTextMain));

            var btnGallery = MakeBtn("Kho ảnh ›", 174, y, 88, 28, 14, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var btnFriendsTop = MakeBtn($"👥 Bạn bè ({myFriends.Count}) ›", 268, y, 126, 28, 14, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8F);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            root.Controls.Add(btnGallery);
            root.Controls.Add(btnFriendsTop);
            y += 36;

            LocketPost latestMine = posts.FirstOrDefault(p => p.IsMine);
            if (latestMine != null)
            {
                var hero = CreateLocketPostCard(latestMine, true);
                hero.Location = new Point(20, y);
                root.Controls.Add(hero);
                y += hero.Height + 16;
            }

            Guna2Panel divider = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 34),
                BorderRadius = 10,
                FillColor = clrSoftIndigo,
                BorderColor = Color.FromArgb(180, 188, 240),
                BorderThickness = 1
            };
            divider.Controls.Add(new Label
            {
                Text = "⬇ Lướt xuống • Bảng tin của bạn bè",
                Font = new Font("Segoe UI", 8.8F, FontStyle.Bold),
                ForeColor = clrIndigo,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            root.Controls.Add(divider);
            y += 44;

            foreach (var fp in posts.Where(p => !p.IsMine))
            {
                var card = CreateLocketPostCard(fp, false);
                card.Location = new Point(20, y);
                root.Controls.Add(card);
                y += card.Height + 16;
            }

            root.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            return root;
        }

        private Guna2Panel CreateLocketPostCard(LocketPost post, bool isMyTopCard)
        {
            Guna2Panel card = new Guna2Panel
            {
                Size = new Size(374, 356),
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = isMyTopCard ? 2 : 1,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            Guna2CircleButton avatar = new Guna2CircleButton
            {
                Text = post.Author.Substring(0, 1),
                Size = new Size(34, 34),
                Location = new Point(14, 12),
                FillColor = isMyTopCard ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            string tagFriendsStr = (post.TaggedFriends != null && post.TaggedFriends.Count > 0)
                ? $"👥 Tag: {string.Join(", ", post.TaggedFriends)}"
                : "🔒 Cá nhân";

            string topText = post.HasTag
                ? $"{post.Category} — {post.NoteTop} ({tagFriendsStr})"
                : "📷 Ảnh khoảnh khắc (Không gắn tag)";

            card.Controls.Add(avatar);
            card.Controls.Add(MakeLabel($"{post.Author} • {post.Time}", 56, 10, 9.2F, FontStyle.Bold, clrTextMain));
            card.Controls.Add(MakeLabel(topText, 56, 30, 8F, FontStyle.Bold, post.HasTag ? clrPrimaryGreen : clrTextMuted));

            Guna2Panel photoFrame = new Guna2Panel
            {
                Location = new Point(14, 56),
                Size = new Size(346, 284),
                BorderRadius = 20,
                FillColor = post.PhotoBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            if (post.CustomImage != null)
            {
                PictureBox pic = new PictureBox { Dock = DockStyle.Fill, Image = post.CustomImage, SizeMode = PictureBoxSizeMode.StretchImage };
                if (isMyTopCard) pic.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(pic);
            }
            else
            {
                Label art = new Label
                {
                    Text = isMyTopCard ? $"{post.Emoji}\n(Bấm vào ảnh để mở Kho ảnh cá nhân)" : post.Emoji,
                    Font = new Font("Segoe UI Emoji", isMyTopCard ? 12.5F : 40F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
                };
                if (isMyTopCard) art.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(art);
            }

            // Caption lọt lòng giữa phía dưới ảnh (BackColor = Transparent xóa góc trắng)
            var pillCaption = MakeBtn(post.CaptionInside, 23, 224, 300, 44, 18, Color.FromArgb(32, 38, 35), Color.White, Color.FromArgb(180, 210, 195), 1, 8.4F);
            if (isMyTopCard) pillCaption.Click += (s, e) => OpenMyGalleryScreen();
            photoFrame.Controls.Add(pillCaption);
            pillCaption.BringToFront();

            if (isMyTopCard)
            {
                card.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Click += (s, e) => OpenMyGalleryScreen();
            }

            card.Controls.Add(photoFrame);
            return card;
        }

        private void OpenMyGalleryScreen()
        {
            Panel galleryRoot = new Panel { BackColor = clrAppBg, AutoScroll = true };
            var btnBack = MakeBtn("‹ Quay lại Bảng tin", 20, 14, 150, 36, 12, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.8F);
            btnBack.Click += (s, e) => ActivateMenu(btnTabFeed, BuildFeedScreen());

            galleryRoot.Controls.Add(btnBack);
            galleryRoot.Controls.Add(MakeLabel("Kho ảnh chụp của bản thân", 20, 58, 12.5F, FontStyle.Bold, clrTextMain));

            var myPosts = posts.Where(p => p.IsMine).ToList();
            for (int i = 0; i < myPosts.Count; i++)
            {
                var item = myPosts[i];
                int col = i % 2, row = i / 2;
                Guna2Panel cell = new Guna2Panel
                {
                    Location = new Point(20 + col * 192, 92 + row * 212),
                    Size = new Size(182, 198),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                Guna2Panel mini = new Guna2Panel { Location = new Point(8, 8), Size = new Size(166, 122), BorderRadius = 12, FillColor = item.PhotoBg };
                if (item.CustomImage != null)
                    mini.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = item.CustomImage, SizeMode = PictureBoxSizeMode.StretchImage });
                else
                    mini.Controls.Add(new Label { Text = item.Emoji, Font = new Font("Segoe UI Emoji", 24F), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });

                string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0) ? $"Tag: {string.Join(", ", item.TaggedFriends)}" : "Cá nhân";
                cell.Controls.Add(mini);
                cell.Controls.Add(MakeLabel(item.HasTag ? item.Category : "📷 Không gắn tag", 10, 136, 8F, FontStyle.Bold, item.HasTag ? clrPrimaryGreen : clrTextMuted));
                cell.Controls.Add(MakeLabel(item.HasTag ? $"{item.NoteTop}\n({tagTxt})" : item.CaptionInside, 10, 154, 7.6F, FontStyle.Regular, clrTextMain));
                galleryRoot.Controls.Add(cell);
            }

            pnlMainContainer.Controls.Clear();
            galleryRoot.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(galleryRoot);
        }

        // ====================================================================
        // MÀN HÌNH 2: CHỤP ẢNH (6 MỤC + BỎ CHỌN TAG + TAG BẠN BÈ CHIA TIỀN)
        // ====================================================================
        private Control BuildCameraScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();

            string selectedCategory = "🍜 Đồ ăn/Thức uống";
            string selectedEmoji = "🍜";
            HashSet<string> taggedFriendNames = new HashSet<string>();
            Image uploadedPhoto = null;

            var badgeLiveTop = MakeBtn("🍜 Đồ ăn/Thức uống — Bún bò Huế • 45.000đ (🔒 Cá nhân)", 20, 10, 374, 32, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);

            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(20, 46),
                Size = new Size(374, 212),
                BorderRadius = 22,
                FillColor = Color.FromArgb(46, 62, 56),
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                Cursor = Cursors.Hand
            };

            PictureBox picPreview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, Visible = false };
            Label lblPrompt = new Label
            {
                Text = "📷\nBấm vào khung để chọn/chụp ảnh",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(215, 230, 224),
                Size = new Size(340, 65),
                Location = new Point(17, 46),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var pillLiveCaption = MakeBtn("Bữa trưa ngon bổ rẻ cùng đồng đội ✨", 32, 158, 310, 40, 18, Color.FromArgb(28, 34, 31), Color.White, Color.FromArgb(180, 210, 195), 1, 8.4F);

            pnlViewfinder.Controls.AddRange(new Control[] { lblPrompt, picPreview, pillLiveCaption });
            pillLiveCaption.BringToFront();

            EventHandler pickImg = (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        uploadedPhoto = Image.FromFile(ofd.FileName);
                        picPreview.Image = uploadedPhoto;
                        picPreview.Visible = true;
                        lblPrompt.Visible = false;
                        pillLiveCaption.BringToFront();
                    }
                }
            };
            pnlViewfinder.Click += pickImg;
            lblPrompt.Click += pickImg;
            picPreview.Click += pickImg;

            root.Controls.Add(MakeLabel("Mục chi tiêu (Bấm lại để bỏ tag):", 20, 266, 8.8F, FontStyle.Bold, clrTextMain));
            var btnNoTag = MakeBtn("🚫 Không gắn tag", 278, 262, 116, 26, 8, clrCardBg, clrRedDanger, clrRedDanger, 1, 7.8F);

            List<Guna2Button> catButtons = new List<Guna2Button>();

            Guna2Panel pnlNoteCard = new Guna2Panel
            {
                Location = new Point(20, 370),
                Size = new Size(374, 224),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            Guna2TextBox txtNoteTop = new Guna2TextBox
            {
                Text = "Bún bò Huế • 45.000đ",
                PlaceholderText = "Ghi chú món...",
                Location = new Point(12, 10),
                Size = new Size(230, 34),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            Guna2TextBox txtAmountNum = new Guna2TextBox
            {
                Text = "45000",
                PlaceholderText = "Số tiền (đ)",
                Location = new Point(250, 10),
                Size = new Size(112, 34),
                BorderRadius = 10,
                BorderColor = clrSoftGreenBorder,
                ForeColor = clrPrimaryGreen,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold)
            };

            Label lblTagFriends = MakeLabel("👥 Tag bạn bè chia tiền cùng (Không chọn = Cá nhân):", 12, 50, 8.2F, FontStyle.Bold, clrIndigo);

            FlowLayoutPanel flowFriends = new FlowLayoutPanel
            {
                Location = new Point(10, 70),
                Size = new Size(354, 46),
                AutoScroll = true,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            Guna2TextBox txtCaption = new Guna2TextBox
            {
                Text = "Bữa trưa ngon bổ rẻ cùng đồng đội ✨",
                PlaceholderText = "Caption lọt trong ảnh (giữa phía dưới)...",
                Location = new Point(12, 124),
                Size = new Size(350, 36),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnChooseFile = MakeBtn("🖼️ Chọn ảnh từ máy tính", 12, 168, 220, 36, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.2F);
            btnChooseFile.Click += pickImg;

            var btnAddMoreFriend = MakeBtn("+ Thêm bạn", 240, 168, 122, 36, 10, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8.2F);
            btnAddMoreFriend.Click += (s, e) => OpenFriendsDrawer();

            Action updatePreview = () =>
            {
                if (string.IsNullOrEmpty(selectedCategory))
                {
                    badgeLiveTop.Text = "📷 Ảnh khoảnh khắc (Không gắn tag chi tiêu)";
                    badgeLiveTop.FillColor = Color.FromArgb(235, 238, 236);
                    badgeLiveTop.ForeColor = clrTextMuted;
                    txtNoteTop.Enabled = false;
                    txtAmountNum.Enabled = false;
                    flowFriends.Enabled = false;
                }
                else
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out int amt);
                    if (amt <= 0) amt = 45000;
                    string splitInfo = taggedFriendNames.Count == 0
                        ? "🔒 Cá nhân"
                        : $"👥 Tag {string.Join(", ", taggedFriendNames)} ({amt / (taggedFriendNames.Count + 1):N0}đ/người)";

                    badgeLiveTop.Text = $"{selectedCategory} — {txtNoteTop.Text} ({splitInfo})";
                    badgeLiveTop.FillColor = clrSoftGreenBg;
                    badgeLiveTop.ForeColor = clrPrimaryGreen;
                    txtNoteTop.Enabled = true;
                    txtAmountNum.Enabled = true;
                    flowFriends.Enabled = true;
                }
            };

            foreach (var f in myFriends)
            {
                var fBtn = MakeBtn($"+ {f.Name}", 0, 0, 96, 32, 14, clrCardBg, clrTextMain, clrBorderLight, 1, 8F);
                fBtn.Margin = new Padding(2, 2, 6, 2);
                fBtn.Click += (s, e) =>
                {
                    if (taggedFriendNames.Contains(f.Name))
                    {
                        taggedFriendNames.Remove(f.Name);
                        fBtn.Text = $"+ {f.Name}";
                        fBtn.FillColor = clrCardBg;
                        fBtn.ForeColor = clrTextMain;
                        fBtn.BorderColor = clrBorderLight;
                        fBtn.BorderThickness = 1;
                    }
                    else
                    {
                        taggedFriendNames.Add(f.Name);
                        fBtn.Text = $"✓ {f.Name}";
                        fBtn.FillColor = clrSoftIndigo;
                        fBtn.ForeColor = clrIndigo;
                        fBtn.BorderColor = clrIndigo;
                        fBtn.BorderThickness = 2;
                    }
                    updatePreview();
                };
                flowFriends.Controls.Add(fBtn);
            }

            for (int i = 0; i < standardCategories.Length; i++)
            {
                var cat = standardCategories[i];
                var btnCat = MakeBtn(cat.Name, 20 + (i % 3) * 127, 292 + (i / 3) * 36, 120, 32, 10,
                    i == 0 ? clrSoftIndigo : clrCardBg, i == 0 ? clrIndigo : clrTextMain, i == 0 ? clrIndigo : clrBorderLight, i == 0 ? 2 : 1, 7.8F);

                btnCat.Click += (s, e) =>
                {
                    if (selectedCategory == cat.Name)
                    {
                        selectedCategory = null;
                        selectedEmoji = "📷";
                        btnCat.FillColor = clrCardBg;
                        btnCat.ForeColor = clrTextMain;
                        btnCat.BorderColor = clrBorderLight;
                        btnCat.BorderThickness = 1;
                    }
                    else
                    {
                        foreach (var b in catButtons)
                        {
                            b.FillColor = clrCardBg;
                            b.ForeColor = clrTextMain;
                            b.BorderColor = clrBorderLight;
                            b.BorderThickness = 1;
                        }
                        btnCat.FillColor = clrSoftIndigo;
                        btnCat.ForeColor = clrIndigo;
                        btnCat.BorderColor = clrIndigo;
                        btnCat.BorderThickness = 2;
                        selectedCategory = cat.Name;
                        selectedEmoji = cat.Emoji;
                    }
                    updatePreview();
                };
                catButtons.Add(btnCat);
                root.Controls.Add(btnCat);
            }

            btnNoTag.Click += (s, e) =>
            {
                selectedCategory = null;
                selectedEmoji = "📷";
                foreach (var b in catButtons)
                {
                    b.FillColor = clrCardBg;
                    b.ForeColor = clrTextMain;
                    b.BorderColor = clrBorderLight;
                    b.BorderThickness = 1;
                }
                updatePreview();
            };

            txtNoteTop.TextChanged += (s, e) => updatePreview();
            txtAmountNum.TextChanged += (s, e) => updatePreview();
            txtCaption.TextChanged += (s, e) => pillLiveCaption.Text = txtCaption.Text;

            pnlNoteCard.Controls.AddRange(new Control[] { txtNoteTop, txtAmountNum, lblTagFriends, flowFriends, txtCaption, btnChooseFile, btnAddMoreFriend });

            var btnPostPhoto = MakeBtn("Đăng ảnh", 20, 602, 374, 48, 16, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 2, 11F);
            btnPostPhoto.Click += (s, e) =>
            {
                bool hasTag = !string.IsNullOrEmpty(selectedCategory);
                int parsedAmt = 0;
                if (hasTag)
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out parsedAmt);
                    if (parsedAmt <= 0) parsedAmt = 45000;
                }
                var taggedList = hasTag ? taggedFriendNames.ToList() : new List<string>();

                string connString = ConfigurationManager.ConnectionStrings["PocketLogDBConn"]?.ConnectionString;

                // 1. Lưu xuống SQL Server LocalDB
                if (!string.IsNullOrEmpty(connString) && hasTag)
                {
                    using (SqlConnection conn = new SqlConnection(connString))
                    {
                        try
                        {
                            conn.Open();

                            // Lưu vào bảng ExpensePosts
                            string insertPostQuery = @"INSERT INTO ExpensePosts (UserID, ImagePath, Caption, IsPublicToFriends, IsExpenseTracked, TotalAmount, CreatedAt) 
                                                       VALUES (@UserID, @ImagePath, @Caption, 1, 1, @TotalAmount, GETDATE());
                                                       SELECT SCOPE_IDENTITY();";

                            int newPostId = 0;
                            using (SqlCommand cmdPost = new SqlCommand(insertPostQuery, conn))
                            {
                                cmdPost.Parameters.AddWithValue("@UserID", 1); // ID Huy Hoàng (Bạn)
                                cmdPost.Parameters.AddWithValue("@ImagePath", "default_path.jpg");
                                cmdPost.Parameters.AddWithValue("@Caption", string.IsNullOrWhiteSpace(txtCaption.Text) ? "Khoảnh khắc mới ✨" : txtCaption.Text);
                                cmdPost.Parameters.AddWithValue("@TotalAmount", parsedAmt);

                                object postIdObj = cmdPost.ExecuteScalar();
                                if (postIdObj != null) newPostId = Convert.ToInt32(postIdObj);
                            }

                            // Lưu món vào bảng ExpenseItems
                            if (newPostId > 0)
                            {
                                string insertItemQuery = @"INSERT INTO ExpenseItems (PostID, ItemName, Amount, Category, PosX, PosY) 
                                                           VALUES (@PostID, @ItemName, @Amount, @Category, 0.5, 0.5)";
                                using (SqlCommand cmdItem = new SqlCommand(insertItemQuery, conn))
                                {
                                    cmdItem.Parameters.AddWithValue("@PostID", newPostId);
                                    cmdItem.Parameters.AddWithValue("@ItemName", string.IsNullOrWhiteSpace(txtNoteTop.Text) ? "Khoản chi mới" : txtNoteTop.Text);
                                    cmdItem.Parameters.AddWithValue("@Amount", parsedAmt);
                                    cmdItem.Parameters.AddWithValue("@Category", selectedCategory);
                                    cmdItem.ExecuteNonQuery();
                                }
                            }

                            // Nếu có tag bạn bè -> Lưu công nợ vào bảng SplitBills
                            if (newPostId > 0 && taggedList.Count > 0)
                            {
                                int sharePerPerson = parsedAmt / (taggedList.Count + 1);
                                foreach (var friendName in taggedList)
                                {
                                    int debtorId = (friendName == "Gia Huy") ? 2 : 3;
                                    string insertSplit = @"INSERT INTO SplitBills (PostID, DebtorID, AmountOwed, IsPaid, StatusNote, CreatedAt)
                                                           VALUES (@PostID, @DebtorID, @AmountOwed, 0, N'Chưa đính kèm ảnh chuyển khoản', GETDATE());";
                                    using (SqlCommand cmdSplit = new SqlCommand(insertSplit, conn))
                                    {
                                        cmdSplit.Parameters.AddWithValue("@PostID", newPostId);
                                        cmdSplit.Parameters.AddWithValue("@DebtorID", debtorId);
                                        cmdSplit.Parameters.AddWithValue("@AmountOwed", sharePerPerson);
                                        cmdSplit.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Lỗi lưu CSDL: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }

                // Cập nhật lại giao diện Bảng tin

                posts.Insert(0, new LocketPost
                {
                    IsMine = true,
                    Author = "Huy Hoàng (Bạn)",
                    HasTag = hasTag,
                    TaggedFriends = taggedList,
                    Category = hasTag ? selectedCategory : "",
                    NoteTop = hasTag ? (string.IsNullOrWhiteSpace(txtNoteTop.Text) ? "Khoản chi mới" : txtNoteTop.Text) : "",
                    CaptionInside = string.IsNullOrWhiteSpace(txtCaption.Text) ? "Khoảnh khắc mới ✨" : txtCaption.Text,
                    Amount = hasTag ? parsedAmt : 0,
                    Time = "Vừa xong",
                    Emoji = selectedEmoji,
                    PhotoBg = Color.FromArgb(68, 118, 98),
                    CustomImage = uploadedPhoto
                });

                // Nếu có gắn tag bạn bè chia tiền -> Thêm vào danh sách các khoản chi gắn tag chưa thanh toán
                if (hasTag && taggedList.Count > 0)
                {
                    int sharePerPerson = parsedAmt / (taggedList.Count + 1);
                    string newId = "D" + (taggedDebts.Count + 1);
                    taggedDebts.Insert(0, new TaggedDebtItem
                    {
                        Id = newId,
                        FriendName = string.Join(", ", taggedList),
                        Title = string.IsNullOrWhiteSpace(txtNoteTop.Text) ? "Khoản chi mới" : txtNoteTop.Text,
                        Category = selectedCategory,
                        TotalAmount = parsedAmt,
                        MyShare = sharePerPerson,
                        SplitDetail = $"Tag cùng {string.Join(", ", taggedList)} (Chia {taggedList.Count + 1})",
                        Time = "Vừa xong",
                        StatusNote = "Chưa đính kèm ảnh chuyển khoản"
                    });
                    selectedDebtId = newId;
                }

                ActivateMenu(btnTabFeed, BuildFeedScreen());
            };

            root.Controls.AddRange(new Control[] { badgeLiveTop, pnlViewfinder, btnNoTag, pnlNoteCard, btnPostPhoto });
            return root;
        }

        // ====================================================================
        // MÀN HÌNH 3: QUẢN LÝ CHI TIÊU
        // (1. Thanh toán khoản tag & Gửi ảnh CK | 2. Thả xuống các khoản chưa thanh toán | 3. Hạn mức 6 mục & Tổng)
        // ====================================================================
        private Control BuildExpenseScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalUnpaidMyShare = unpaidList.Sum(d => d.MyShare);

            TaggedDebtItem currentDebt = taggedDebts.FirstOrDefault(d => d.Id == selectedDebtId)
                                         ?? unpaidList.FirstOrDefault()
                                         ?? taggedDebts.FirstOrDefault();

            // Banner Tổng khoản chi được gắn tag với bạn bè
            Guna2Panel pnlBanner = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 46),
                BorderRadius = 12,
                FillColor = Color.FromArgb(31, 41, 79)
            };
            pnlBanner.Controls.Add(MakeLabel($"Chia tiền bạn bè • Chưa tất toán: {totalUnpaidMyShare:N0}đ", 14, 13, 9.8F, FontStyle.Bold, Color.White));
            root.Controls.Add(pnlBanner);
            y += 54;

            // Thẻ gửi ảnh chuyển khoản cho khoản đang chọn
            Guna2Panel cardActiveDebt = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 218),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            if (currentDebt != null)
            {
                cardActiveDebt.Controls.Add(MakeLabel($"Đang chọn: {currentDebt.Title}", 14, 10, 9.5F, FontStyle.Bold, clrTextMain));
                cardActiveDebt.Controls.Add(MakeLabel($"{currentDebt.SplitDetail} • Phần bạn: {currentDebt.MyShare:N0}đ", 14, 30, 8.2F, FontStyle.Bold, clrPrimaryGreen));

                var badgeState = MakeBtn(
                    currentDebt.IsConfirmedPaid ? "✓ Đã hoàn thành" : (currentDebt.IsSentProof ? "⏳ Chờ bạn xác nhận" : "Chưa thanh toán"),
                    14, 52, 136, 24, 8,
                    currentDebt.IsConfirmedPaid ? clrSoftGreenBg : clrBadgeOrangeBg,
                    currentDebt.IsConfirmedPaid ? clrPrimaryGreen : clrBadgeOrangeTxt,
                    Color.Transparent, 0, 7.8F);
                cardActiveDebt.Controls.Add(badgeState);

                Label lblNote = MakeLabel(currentDebt.StatusNote, 156, 56, 7.8F, FontStyle.Italic, clrTextMuted);
                cardActiveDebt.Controls.Add(lblNote);

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(14, 84),
                    Size = new Size(136, 118),
                    BorderRadius = 12,
                    FillColor = Color.FromArgb(238, 242, 240),
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1
                };

                PictureBox picProof = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = currentDebt.ProofImage,
                    Visible = currentDebt.ProofImage != null
                };
                Label lblNoImg = new Label
                {
                    Text = "🧾\nẢnh bill CK\ncủa bản thân",
                    Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
                    ForeColor = clrTextMuted,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Visible = currentDebt.ProofImage == null
                };
                pnlBillImg.Controls.Add(lblNoImg);
                pnlBillImg.Controls.Add(picProof);
                cardActiveDebt.Controls.Add(pnlBillImg);

                var btnUploadProof = MakeBtn("📤 Gửi ảnh CK của bản thân", 162, 86, 198, 50, 12, clrSoftIndigo, clrIndigo, clrIndigo, 2, 8.5F);
                var btnFriendVerify = MakeBtn($"✓ {currentDebt.FriendName} xác nhận xong", 162, 146, 198, 54, 12, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 1, 8.4F);

                btnUploadProof.Click += (s, e) =>
                {
                    using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                    {
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            currentDebt.ProofImage = Image.FromFile(ofd.FileName);
                            currentDebt.IsSentProof = true;
                            currentDebt.IsConfirmedPaid = false;
                            currentDebt.StatusNote = $"Đã gửi bill lúc {DateTime.Now:HH:mm}";
                            ActivateMenu(btnTabExpense, BuildExpenseScreen());
                        }
                    }
                };

                btnFriendVerify.Click += (s, e) =>
                {
                    if (!currentDebt.IsSentProof)
                    {
                        currentDebt.StatusNote = "Vui lòng tải ảnh CK lên trước!";
                        lblNote.Text = currentDebt.StatusNote;
                        lblNote.ForeColor = clrRedDanger;
                        return;
                    }
                    currentDebt.IsConfirmedPaid = true;
                    currentDebt.StatusNote = $"{currentDebt.FriendName} đã xác nhận hoàn tất!";
                    ActivateMenu(btnTabExpense, BuildExpenseScreen());
                };

                cardActiveDebt.Controls.Add(btnUploadProof);
                cardActiveDebt.Controls.Add(btnFriendVerify);
            }
            root.Controls.Add(cardActiveDebt);
            y += 226;

            // ----------------------------------------------------------------
            // NÚT BẤM THẢ XUỐNG (DROPDOWN) CÁC KHOẢN CHI GẮN TAG CHƯA THANH TOÁN
            // ----------------------------------------------------------------
            string arrow = isUnpaidDropdownOpen ? "▲" : "▼";
            var btnToggleUnpaid = MakeBtn(
                $"{arrow} Các khoản chi gắn tag với bạn bè chưa thanh toán ({unpaidList.Count})",
                20, y, 374, 40, 12, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8.4F);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                ActivateMenu(btnTabExpense, BuildExpenseScreen());
            };
            root.Controls.Add(btnToggleUnpaid);
            y += 46;

            if (isUnpaidDropdownOpen)
            {
                if (unpaidList.Count == 0)
                {
                    Guna2Panel emptyBox = new Guna2Panel
                    {
                        Location = new Point(20, y),
                        Size = new Size(374, 48),
                        BorderRadius = 12,
                        FillColor = clrSoftGreenBg,
                        BorderColor = clrSoftGreenBorder,
                        BorderThickness = 1
                    };
                    emptyBox.Controls.Add(MakeLabel("🎉 Bạn đã thanh toán hết mọi khoản chi gắn tag với bạn bè!", 14, 14, 8.5F, FontStyle.Bold, clrPrimaryGreen));
                    root.Controls.Add(emptyBox);
                    y += 56;
                }
                else
                {
                    foreach (var debt in unpaidList)
                    {
                        bool isSelected = currentDebt != null && debt.Id == currentDebt.Id;
                        Guna2Panel itemRow = new Guna2Panel
                        {
                            Location = new Point(20, y),
                            Size = new Size(374, 68),
                            BorderRadius = 12,
                            FillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                            BorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                            BorderThickness = isSelected ? 2 : 1
                        };

                        itemRow.Controls.Add(MakeLabel($"{debt.Category} • {debt.Title}", 12, 8, 8.8F, FontStyle.Bold, clrTextMain));
                        itemRow.Controls.Add(MakeLabel($"{debt.SplitDetail} • Phần bạn: {debt.MyShare:N0}đ", 12, 28, 8F, FontStyle.Bold, clrPrimaryGreen));
                        itemRow.Controls.Add(MakeLabel($"{debt.Time} • {(debt.IsSentProof ? "Đã đính kèm bill" : "Chưa gửi bill")}", 12, 46, 7.6F, FontStyle.Regular, clrTextMuted));

                        var btnSelectDebt = MakeBtn(
                            isSelected ? "Đang chọn" : "Chọn trả ›",
                            272, 16, 90, 34, 10,
                            isSelected ? clrPrimaryGreen : clrSoftIndigo,
                            isSelected ? Color.White : clrIndigo,
                            isSelected ? clrPrimaryGreen : clrIndigo, 1, 8F);

                        btnSelectDebt.Click += (s, e) =>
                        {
                            selectedDebtId = debt.Id;
                            ActivateMenu(btnTabExpense, BuildExpenseScreen());
                        };

                        itemRow.Controls.Add(btnSelectDebt);
                        root.Controls.Add(itemRow);
                        y += 74;
                    }
                }
            }

            y += 6;
            // ----------------------------------------------------------------
            // PHẦN CÁC MỤC CHI TIÊU THEO TAG & SETUP HẠN MỨC TỪNG MỤC
            // ----------------------------------------------------------------
            root.Controls.Add(MakeLabel("Chi tiêu theo danh mục Tag (Tự đặt hạn mức):", 20, y, 10F, FontStyle.Bold, clrTextMain));
            y += 26;

            foreach (var cat in standardCategories)
            {
                string catName = cat.Name;
                int spent = posts.Where(p => p.HasTag && p.Category == catName).Sum(p => p.Amount);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;

                Guna2Panel catCard = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(374, 84),
                    BorderRadius = 14,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                catCard.Controls.Add(MakeLabel(catName, 12, 10, 9.2F, FontStyle.Bold, clrTextMain));
                catCard.Controls.Add(MakeLabel($"Đã tiêu: {spent:N0}đ / Mức: {limit:N0}đ", 12, 32, 8F, FontStyle.Bold, spent > limit ? clrRedDanger : clrPrimaryGreen));

                int pct = limit > 0 ? Math.Min(100, (int)((long)spent * 100 / limit)) : 100;
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(12, 56),
                    Size = new Size(210, 12),
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = Color.FromArgb(232, 236, 234),
                    ProgressColor = spent > limit ? clrRedDanger : clrPrimaryGreen,
                    ProgressColor2 = spent > limit ? Color.OrangeRed : clrSoftGreenBorder
                };

                Guna2TextBox txtLim = new Guna2TextBox
                {
                    Text = limit.ToString(),
                    Location = new Point(234, 12),
                    Size = new Size(126, 28),
                    BorderRadius = 8,
                    BorderColor = clrBorderLight,
                    Font = new Font("Segoe UI", 8.5F)
                };

                var btnSetLim = MakeBtn("Set hạn mức", 234, 46, 126, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F);
                btnSetLim.Click += (s, e) =>
                {
                    if (int.TryParse(txtLim.Text.Replace(".", "").Replace(",", ""), out int nLim) && nLim > 0)
                    {
                        categoryBudgets[catName] = nLim;
                        ActivateMenu(btnTabExpense, BuildExpenseScreen());
                    }
                };

                catCard.Controls.AddRange(new Control[] { prg, txtLim, btnSetLim });
                root.Controls.Add(catCard);
                y += 92;
            }

            // ----------------------------------------------------------------
            // DƯỚI CÙNG: TỔNG CHI TIÊU & SETUP TỔNG HẠN MỨC
            // ----------------------------------------------------------------
            int grandTotal = posts.Where(p => p.HasTag).Sum(p => p.Amount);

            Guna2Panel cardTotal = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 132),
                BorderRadius = 18,
                FillColor = clrSoftGreenBg,
                BorderColor = clrPrimaryGreen,
                BorderThickness = 2
            };

            cardTotal.Controls.Add(MakeLabel("TỔNG CHI TIÊU TẤT CẢ CÁC MỤC", 14, 10, 9.2F, FontStyle.Bold, clrPrimaryGreen));
            cardTotal.Controls.Add(MakeLabel($"{grandTotal:N0}đ  /  Tổng hạn mức: {totalBudgetLimit:N0}đ", 14, 32, 10.5F, FontStyle.Bold, grandTotal > totalBudgetLimit ? clrRedDanger : clrTextMain));

            int totalPct = totalBudgetLimit > 0 ? Math.Min(100, (int)((long)grandTotal * 100 / totalBudgetLimit)) : 100;
            Guna2ProgressBar prgTotal = new Guna2ProgressBar
            {
                Location = new Point(14, 58),
                Size = new Size(346, 14),
                BorderRadius = 7,
                Value = totalPct,
                FillColor = Color.White,
                ProgressColor = grandTotal > totalBudgetLimit ? clrRedDanger : clrPrimaryGreen,
                ProgressColor2 = grandTotal > totalBudgetLimit ? Color.OrangeRed : clrIndigo
            };

            Guna2TextBox txtTotalIn = new Guna2TextBox
            {
                Text = totalBudgetLimit.ToString(),
                Location = new Point(14, 82),
                Size = new Size(210, 34),
                BorderRadius = 10,
                FillColor = Color.White,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var btnSetTotal = MakeBtn("Set tổng hạn mức", 232, 82, 128, 34, 10, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 8.2F);
            btnSetTotal.Click += (s, e) =>
            {
                if (int.TryParse(txtTotalIn.Text.Replace(".", "").Replace(",", ""), out int nTot) && nTot > 0)
                {
                    totalBudgetLimit = nTot;
                    ActivateMenu(btnTabExpense, BuildExpenseScreen());
                }
            };

            cardTotal.Controls.AddRange(new Control[] { prgTotal, txtTotalIn, btnSetTotal });
            root.Controls.Add(cardTotal);
            y += 148;

            root.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            return root;
        }
    }
}