using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace PocketLog.Client
{
    public partial class Form1 : Form
    {
        private Guna2BorderlessForm borderlessForm;
        private Guna2DragControl dragControl;
        private Guna2Panel pnlHeader;
        private Guna2Panel pnlMainContainer;
        private Guna2Panel pnlBottomNav;

        private Guna2Button btnTabFeed;
        private Guna2Button btnTabCamera;
        private Guna2Button btnTabExpense;

        // Bảng màu lấy cảm hứng từ thiết kế Modern Light UI
        private readonly Color clrAppBg = Color.FromArgb(245, 247, 246);        // Nền xám kem nhạt
        private readonly Color clrCardBg = Color.White;                         // Nền thẻ trắng
        private readonly Color clrPrimaryGreen = Color.FromArgb(29, 99, 76);    // Xanh lục bảo đậm (#1D634C)
        private readonly Color clrSoftGreenBg = Color.FromArgb(232, 245, 239);  // Nền xanh pastel nhạt
        private readonly Color clrSoftGreenBorder = Color.FromArgb(148, 197, 178); // Viền xanh sáng
        private readonly Color clrIndigo = Color.FromArgb(57, 66, 167);         // Xanh tím nút hành động
        private readonly Color clrSoftIndigo = Color.FromArgb(234, 236, 252);   // Nền xanh tím nhạt
        private readonly Color clrRedDanger = Color.FromArgb(166, 58, 58);      // Đỏ nâu viền hủy
        private readonly Color clrBadgeOrangeBg = Color.FromArgb(253, 238, 228);// Nền cam pastel
        private readonly Color clrBadgeOrangeTxt = Color.FromArgb(168, 85, 34); // Chữ cam nâu
        private readonly Color clrBorderLight = Color.FromArgb(224, 228, 226);  // Viền thẻ xám sáng
        private readonly Color clrTextMain = Color.FromArgb(28, 32, 30);        // Chữ chính đen đậm
        private readonly Color clrTextMuted = Color.FromArgb(115, 122, 118);    // Chữ phụ xám

        // Dữ liệu mẫu để test tương tác ngay trên UI
        private class ExpensePost
        {
            public string Author { get; set; }
            public string Title { get; set; }
            public string Detail { get; set; }
            public string Price { get; set; }
            public string Time { get; set; }
            public string Category { get; set; }
            public string Status { get; set; }
            public Color ThumbColor { get; set; }
        }

        private List<ExpensePost> posts = new List<ExpensePost>();

        public Form1()
        {
            InitializeComponent();
            SeedSampleData();
            SetupPhoneShell();
        }

        private void SeedSampleData()
        {
            posts.Add(new ExpensePost
            {
                Author = "Gia Huy",
                Title = "Cà phê sữa đá & Bạc xỉu",
                Detail = "Bàn T05 • Tầng 1 (Size M/L)",
                Price = "64.000đ",
                Time = "14:32",
                Category = "Cà phê",
                Status = "Đã chia bill",
                ThumbColor = Color.FromArgb(196, 178, 160)
            });
            posts.Add(new ExpensePost
            {
                Author = "Huy Hoàng",
                Title = "Cơm tấm sườn bì chả",
                Detail = "Quán Phúc Lộc Thọ • Thủ Đức",
                Price = "55.000đ",
                Time = "12:15",
                Category = "Ăn uống",
                Status = "Cá nhân",
                ThumbColor = Color.FromArgb(162, 136, 112)
            });
            posts.Add(new ExpensePost
            {
                Author = "Gia Huy",
                Title = "Trà đào cam sả & Bánh ngọt",
                Detail = "Học nhóm đồ án NT106",
                Price = "89.000đ",
                Time = "09:40",
                Category = "Cà phê",
                Status = "Chưa thu tiền",
                ThumbColor = Color.FromArgb(124, 102, 85)
            });
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

            // 1. Thanh Header trên cùng
            pnlHeader = new Guna2Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                FillColor = clrCardBg
            };

            Panel headerLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = clrBorderLight
            };
            pnlHeader.Controls.Add(headerLine);

            Label lblBrand = new Label
            {
                Text = "● PocketLog",
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = clrPrimaryGreen,
                AutoSize = true,
                Location = new Point(18, 13)
            };

            Guna2ControlBox btnClose = new Guna2ControlBox
            {
                Location = new Point(372, 11),
                Size = new Size(34, 30),
                FillColor = Color.FromArgb(245, 247, 246),
                IconColor = clrTextMain,
                BorderRadius = 8,
                HoverState = { FillColor = Color.FromArgb(232, 17, 35), IconColor = Color.White }
            };

            Guna2ControlBox btnMin = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MinimizeBox,
                Location = new Point(332, 11),
                Size = new Size(34, 30),
                FillColor = Color.FromArgb(245, 247, 246),
                IconColor = clrTextMain,
                BorderRadius = 8
            };

            pnlHeader.Controls.Add(lblBrand);
            pnlHeader.Controls.Add(btnMin);
            pnlHeader.Controls.Add(btnClose);

            dragControl = new Guna2DragControl { TargetControl = pnlHeader };

            // 2. Thanh điều hướng 3 Menu dưới đáy
            pnlBottomNav = new Guna2Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                FillColor = clrCardBg
            };

            Panel navTopBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = clrBorderLight
            };
            pnlBottomNav.Controls.Add(navTopBorder);

            btnTabFeed = CreateNavTabButton("Bảng tin", 16, 118);
            btnTabCamera = CreateNavTabButton("Chụp ảnh", 142, 118);
            btnTabExpense = CreateNavTabButton("Quản lý chi tiêu", 268, 136);

            btnTabFeed.Click += (s, e) => ActivateMenu(btnTabFeed, BuildFeedScreen());
            btnTabCamera.Click += (s, e) => ActivateMenu(btnTabCamera, BuildCameraScreen());
            btnTabExpense.Click += (s, e) => ActivateMenu(btnTabExpense, BuildExpenseScreen());

            pnlBottomNav.Controls.Add(btnTabFeed);
            pnlBottomNav.Controls.Add(btnTabCamera);
            pnlBottomNav.Controls.Add(btnTabExpense);

            // 3. Khung nội dung ở giữa
            pnlMainContainer = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = clrAppBg
            };

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottomNav);

            // Mặc định mở Bảng tin trước
            ActivateMenu(btnTabFeed, BuildFeedScreen());
        }

        private Guna2Button CreateNavTabButton(string text, int x, int width)
        {
            return new Guna2Button
            {
                Text = text,
                Location = new Point(x, 15),
                Size = new Size(width, 46),
                BorderRadius = 23,
                BorderThickness = 1,
                BorderColor = clrBorderLight,
                FillColor = clrCardBg,
                ForeColor = clrTextMain,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
        }

        private void ActivateMenu(Guna2Button selectedBtn, Control screenContent)
        {
            foreach (var btn in new[] { btnTabFeed, btnTabCamera, btnTabExpense })
            {
                btn.FillColor = clrCardBg;
                btn.ForeColor = clrTextMain;
                btn.BorderColor = clrBorderLight;
            }

            // Nút đang chọn có nền xanh đậm và viền xanh sáng nổi bật
            selectedBtn.FillColor = clrPrimaryGreen;
            selectedBtn.ForeColor = Color.White;
            selectedBtn.BorderColor = clrSoftGreenBorder;

            pnlMainContainer.Controls.Clear();
            screenContent.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(screenContent);
        }

        // ====================================================================
        // MÀN HÌNH 1: BẢNG TIN (FEED)
        // ====================================================================
        private Control BuildFeedScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };

            Label lblHello = new Label
            {
                Text = "Xin chào, Huy Hoàng",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = clrTextMuted,
                AutoSize = true,
                Location = new Point(20, 14)
            };

            Label lblHeadline = new Label
            {
                Text = "Hôm nay nhóm chi gì?",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(18, 34)
            };

            // Nút thông báo hóa đơn góc phải (có viền sáng giống mẫu)
            Guna2Button btnReceiptIcon = new Guna2Button
            {
                Text = "🧾",
                Font = new Font("Segoe UI Emoji", 13F),
                Size = new Size(44, 44),
                Location = new Point(354, 20),
                BorderRadius = 12,
                FillColor = clrCardBg,
                ForeColor = clrTextMain,
                BorderThickness = 1,
                BorderColor = clrBorderLight,
                Cursor = Cursors.Hand
            };
            btnReceiptIcon.Click += (s, e) => ActivateMenu(btnTabExpense, BuildExpenseScreen());

            // Thanh trạng thái nhóm màu xanh pastel có viền xanh sáng
            Guna2Panel pnlRoomBanner = new Guna2Panel
            {
                Location = new Point(20, 76),
                Size = new Size(378, 44),
                BorderRadius = 12,
                FillColor = clrSoftGreenBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1
            };

            Label lblRoomInfo = new Label
            {
                Text = "🏠 Nhóm Đồ Án NT106 • 2 thành viên online",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = clrPrimaryGreen,
                AutoSize = true,
                Location = new Point(14, 12)
            };

            Label lblSwitchGroup = new Label
            {
                Text = "Đổi nhóm ⌄",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = clrPrimaryGreen,
                AutoSize = true,
                Location = new Point(288, 13),
                Cursor = Cursors.Hand
            };
            pnlRoomBanner.Controls.Add(lblRoomInfo);
            pnlRoomBanner.Controls.Add(lblSwitchGroup);

            // Ô tìm kiếm bo tròn
            Guna2TextBox txtSearch = new Guna2TextBox
            {
                PlaceholderText = "🔍 Tìm khoản chi, món ăn, người đăng...",
                Location = new Point(20, 130),
                Size = new Size(378, 40),
                BorderRadius = 12,
                FillColor = Color.FromArgb(236, 240, 238),
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Font = new Font("Segoe UI", 9.5F)
            };

            // Hàng nút lọc (Filter Pills) có viền sáng
            string[] categories = { "Tất cả", "Cà phê", "Ăn uống", "Mua sắm" };
            int pillX = 20;
            List<Guna2Button> filterButtons = new List<Guna2Button>();

            Panel pnlCardsHolder = new Panel
            {
                Location = new Point(20, 228),
                Size = new Size(378, 360),
                AutoSize = true
            };

            foreach (string cat in categories)
            {
                Guna2Button pill = new Guna2Button
                {
                    Text = cat,
                    Location = new Point(pillX, 180),
                    Size = new Size(86, 36),
                    BorderRadius = 18,
                    BorderThickness = 1,
                    BorderColor = cat == "Tất cả" ? clrPrimaryGreen : clrBorderLight,
                    FillColor = cat == "Tất cả" ? clrPrimaryGreen : clrCardBg,
                    ForeColor = cat == "Tất cả" ? Color.White : clrTextMain,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };

                pill.Click += (s, e) =>
                {
                    foreach (var b in filterButtons)
                    {
                        b.FillColor = clrCardBg;
                        b.ForeColor = clrTextMain;
                        b.BorderColor = clrBorderLight;
                    }
                    pill.FillColor = clrPrimaryGreen;
                    pill.ForeColor = Color.White;
                    pill.BorderColor = clrPrimaryGreen;
                    RenderFeedCards(pnlCardsHolder, cat);
                };

                filterButtons.Add(pill);
                root.Controls.Add(pill);
                pillX += 94;
            }

            RenderFeedCards(pnlCardsHolder, "Tất cả");

            // Thanh tóm tắt nổi dưới đáy giống thanh "Xem giỏ hàng"
            Guna2Panel pnlFloatingSummary = new Guna2Panel
            {
                Location = new Point(20, 615),
                Size = new Size(378, 54),
                BorderRadius = 16,
                FillColor = clrPrimaryGreen,
                Cursor = Cursors.Hand
            };

            Guna2CircleButton badgeCount = new Guna2CircleButton
            {
                Text = posts.Count.ToString(),
                Size = new Size(28, 28),
                Location = new Point(16, 13),
                FillColor = Color.White,
                ForeColor = clrPrimaryGreen,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            Label lblSummaryText = new Label
            {
                Text = "Xem sổ chi tiêu nhóm",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(54, 16)
            };

            Label lblSummaryTotal = new Label
            {
                Text = "208.000đ",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(282, 16)
            };

            pnlFloatingSummary.Controls.Add(badgeCount);
            pnlFloatingSummary.Controls.Add(lblSummaryText);
            pnlFloatingSummary.Controls.Add(lblSummaryTotal);
            pnlFloatingSummary.Click += (s, e) => ActivateMenu(btnTabExpense, BuildExpenseScreen());

            root.Controls.Add(lblHello);
            root.Controls.Add(lblHeadline);
            root.Controls.Add(btnReceiptIcon);
            root.Controls.Add(pnlRoomBanner);
            root.Controls.Add(txtSearch);
            root.Controls.Add(pnlCardsHolder);
            root.Controls.Add(pnlFloatingSummary);

            return root;
        }

        private void RenderFeedCards(Panel holder, string filterCategory)
        {
            holder.Controls.Clear();
            int yOffset = 0;

            foreach (var item in posts)
            {
                if (filterCategory != "Tất cả" && item.Category != filterCategory)
                    continue;

                Guna2Panel card = new Guna2Panel
                {
                    Location = new Point(0, yOffset),
                    Size = new Size(378, 114),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                // Hình vuông minh họa món ăn/ảnh chụp bên trái
                Guna2Panel thumb = new Guna2Panel
                {
                    Location = new Point(12, 12),
                    Size = new Size(90, 90),
                    BorderRadius = 14,
                    FillColor = item.ThumbColor
                };
                Label lblCupIcon = new Label
                {
                    Text = item.Category == "Cà phê" ? "☕" : "🍱",
                    Font = new Font("Segoe UI Emoji", 22F),
                    ForeColor = Color.White,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent
                };
                thumb.Controls.Add(lblCupIcon);

                Label lblTitle = new Label
                {
                    Text = item.Title,
                    Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                    ForeColor = clrTextMain,
                    AutoSize = true,
                    Location = new Point(114, 14)
                };

                Label lblSub = new Label
                {
                    Text = $"{item.Author} • {item.Detail}",
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = clrTextMuted,
                    AutoSize = true,
                    Location = new Point(114, 38)
                };

                Label lblPrice = new Label
                {
                    Text = item.Price,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = clrPrimaryGreen,
                    AutoSize = true,
                    Location = new Point(114, 72)
                };

                // Nhãn trạng thái cam pastel giống "Tạm hết / Chưa thanh toán"
                Guna2Button badgeStatus = new Guna2Button
                {
                    Text = item.Status,
                    Location = new Point(212, 70),
                    Size = new Size(96, 26),
                    BorderRadius = 8,
                    FillColor = clrBadgeOrangeBg,
                    ForeColor = clrBadgeOrangeTxt,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold)
                };

                // Nút dấu + màu xanh góc phải
                Guna2Button btnPlus = new Guna2Button
                {
                    Text = "+",
                    Location = new Point(322, 58),
                    Size = new Size(42, 42),
                    BorderRadius = 12,
                    FillColor = clrPrimaryGreen,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnPlus.Click += (s, e) => ActivateMenu(btnTabExpense, BuildExpenseScreen());

                card.Controls.Add(thumb);
                card.Controls.Add(lblTitle);
                card.Controls.Add(lblSub);
                card.Controls.Add(lblPrice);
                card.Controls.Add(badgeStatus);
                card.Controls.Add(btnPlus);

                holder.Controls.Add(card);
                yOffset += 124;
            }
        }

        // ====================================================================
        // MÀN HÌNH 2: CHỤP ẢNH LOCKET & GẮN TAG GIÁ TIỀN
        // ====================================================================
        private Control BuildCameraScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg };

            Label lblHeader = new Label
            {
                Text = "Chụp & Gắn nhãn chi tiêu",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(20, 16)
            };

            Label lblGuide = new Label
            {
                Text = "Bấm trực tiếp lên khung ảnh để ghim nhãn giá tiền (Tag)",
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = clrTextMuted,
                AutoSize = true,
                Location = new Point(20, 44)
            };

            // Khung ngắm Locket vuông bo góc tròn
            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(20, 74),
                Size = new Size(378, 350),
                BorderRadius = 24,
                FillColor = Color.FromArgb(42, 52, 48),
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                Cursor = Cursors.Cross
            };

            PictureBox picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };

            Label lblCamHint = new Label
            {
                Text = "📷\nKhung ảnh Locket (1:1)\nClick vào vị trí món đồ để gắn giá",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(210, 225, 218),
                AutoSize = false,
                Size = new Size(340, 100),
                Location = new Point(19, 120),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            pnlViewfinder.Controls.Add(lblCamHint);
            pnlViewfinder.Controls.Add(picPreview);

            // Thẻ nhập thông tin món đồ & giá tiền
            Guna2Panel pnlInputCard = new Guna2Panel
            {
                Location = new Point(20, 438),
                Size = new Size(378, 150),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            Guna2TextBox txtItemName = new Guna2TextBox
            {
                PlaceholderText = "Tên món (VD: Bạc xỉu size L)...",
                Text = "Cà phê muối",
                Location = new Point(14, 14),
                Size = new Size(210, 38),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F)
            };

            Guna2TextBox txtItemPrice = new Guna2TextBox
            {
                PlaceholderText = "Giá tiền...",
                Text = "35.000đ",
                Location = new Point(234, 14),
                Size = new Size(130, 38),
                BorderRadius = 10,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = clrPrimaryGreen
            };

            // Các nút chọn nhanh danh mục có viền sáng
            string[] quickTags = { "☕ Cà phê", "🍜 Ăn uống", "🛒 Mua sắm" };
            string selectedCat = "Cà phê";
            int tagX = 14;
            List<Guna2Button> catBtns = new List<Guna2Button>();

            foreach (var qt in quickTags)
            {
                Guna2Button btnCat = new Guna2Button
                {
                    Text = qt,
                    Location = new Point(tagX, 62),
                    Size = new Size(110, 32),
                    BorderRadius = 10,
                    BorderThickness = 1,
                    BorderColor = qt.Contains("Cà phê") ? clrIndigo : clrBorderLight,
                    FillColor = qt.Contains("Cà phê") ? clrSoftIndigo : clrCardBg,
                    ForeColor = qt.Contains("Cà phê") ? clrIndigo : clrTextMain,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnCat.Click += (s, e) =>
                {
                    foreach (var b in catBtns)
                    {
                        b.FillColor = clrCardBg;
                        b.ForeColor = clrTextMain;
                        b.BorderColor = clrBorderLight;
                    }
                    btnCat.FillColor = clrSoftIndigo;
                    btnCat.ForeColor = clrIndigo;
                    btnCat.BorderColor = clrIndigo;
                    selectedCat = qt.Substring(2).Trim();
                };
                catBtns.Add(btnCat);
                pnlInputCard.Controls.Add(btnCat);
                tagX += 118;
            }

            Guna2Button btnPickImage = new Guna2Button
            {
                Text = "🖼️ Chọn ảnh từ máy",
                Location = new Point(14, 104),
                Size = new Size(165, 34),
                BorderRadius = 10,
                FillColor = clrSoftGreenBg,
                ForeColor = clrPrimaryGreen,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btnPickImage.Click += (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        lblCamHint.Visible = false;
                        picPreview.Image = Image.FromFile(ofd.FileName);
                    }
                }
            };

            Guna2Button btnClearTags = new Guna2Button
            {
                Text = "Xoá các nhãn ghim",
                Location = new Point(190, 104),
                Size = new Size(174, 34),
                BorderRadius = 10,
                FillColor = clrCardBg,
                ForeColor = clrRedDanger,
                BorderColor = clrRedDanger,
                BorderThickness = 1,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            pnlInputCard.Controls.Add(txtItemName);
            pnlInputCard.Controls.Add(txtItemPrice);
            pnlInputCard.Controls.Add(btnPickImage);
            pnlInputCard.Controls.Add(btnClearTags);

            // Sự kiện click lên ảnh để ghim nhãn giá tiền nổi ngay tại chỗ click
            MouseEventHandler pinTagHandler = (s, e) =>
            {
                lblCamHint.Visible = false;
                Guna2Button floatingTag = new Guna2Button
                {
                    Text = $"🏷️ {txtItemName.Text}: {txtItemPrice.Text}",
                    AutoSize = true,
                    Location = new Point(Math.Max(10, Math.Min(e.X - 40, 200)), Math.Max(10, Math.Min(e.Y - 15, 300))),
                    BorderRadius = 12,
                    FillColor = clrPrimaryGreen,
                    ForeColor = Color.White,
                    BorderColor = Color.White,
                    BorderThickness = 1,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
                };
                pnlViewfinder.Controls.Add(floatingTag);
                floatingTag.BringToFront();
            };

            pnlViewfinder.MouseClick += pinTagHandler;
            picPreview.MouseClick += pinTagHandler;
            lblCamHint.MouseClick += pinTagHandler;

            btnClearTags.Click += (s, e) =>
            {
                for (int i = pnlViewfinder.Controls.Count - 1; i >= 0; i--)
                {
                    if (pnlViewfinder.Controls[i] is Guna2Button)
                        pnlViewfinder.Controls.RemoveAt(i);
                }
            };

            // Nút Đăng lên Bảng tin (Broadcast TCP)
            Guna2Button btnPostNow = new Guna2Button
            {
                Text = "🚀 Đăng lên Bảng tin nhóm",
                Location = new Point(20, 602),
                Size = new Size(378, 52),
                BorderRadius = 16,
                FillColor = clrPrimaryGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btnPostNow.Click += (s, e) =>
            {
                posts.Insert(0, new ExpensePost
                {
                    Author = "Huy Hoàng",
                    Title = string.IsNullOrWhiteSpace(txtItemName.Text) ? "Khoản chi mới" : txtItemName.Text,
                    Detail = "Vừa chụp qua Locket Camera",
                    Price = string.IsNullOrWhiteSpace(txtItemPrice.Text) ? "0đ" : txtItemPrice.Text,
                    Time = DateTime.Now.ToString("HH:mm"),
                    Category = selectedCat,
                    Status = "Mới đăng",
                    ThumbColor = Color.FromArgb(88, 128, 110)
                });
                ActivateMenu(btnTabFeed, BuildFeedScreen());
            };

            root.Controls.Add(lblHeader);
            root.Controls.Add(lblGuide);
            root.Controls.Add(pnlViewfinder);
            root.Controls.Add(pnlInputCard);
            root.Controls.Add(btnPostNow);

            return root;
        }

        // ====================================================================
        // MÀN HÌNH 3: QUẢN LÝ CHI TIÊU & CHIA BILL (Chuẩn mẫu hình 2)
        // ====================================================================
        private Control BuildExpenseScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };

            // Banner đầu trang màu xanh đậm giống thanh "Đơn #A-0142"
            Guna2Panel pnlTopBanner = new Guna2Panel
            {
                Location = new Point(20, 12),
                Size = new Size(378, 48),
                BorderRadius = 12,
                FillColor = Color.FromArgb(31, 41, 79)
            };

            Label lblBillCode = new Label
            {
                Text = "‹  Sổ chi tiêu #NT106-01",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(14, 13)
            };

            Guna2Button badgeState = new Guna2Button
            {
                Text = "Đang quyết toán",
                Location = new Point(250, 10),
                Size = new Size(116, 28),
                BorderRadius = 14,
                FillColor = Color.White,
                ForeColor = Color.FromArgb(31, 41, 79),
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            pnlTopBanner.Controls.Add(lblBillCode);
            pnlTopBanner.Controls.Add(badgeState);

            // Thẻ chi tiết các khoản chi (Card 1)
            Guna2Panel cardReceipt = new Guna2Panel
            {
                Location = new Point(20, 70),
                Size = new Size(378, 200),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            cardReceipt.Controls.Add(new Label
            {
                Text = "Nhóm PocketLog • Gia Huy & Huy Hoàng",
                Font = new Font("Segoe UI", 9F),
                ForeColor = clrTextMuted,
                AutoSize = true,
                Location = new Point(16, 14)
            });

            cardReceipt.Controls.Add(new Label
            {
                Text = "1 × Cà phê sữa đá (M)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(16, 42)
            });
            cardReceipt.Controls.Add(new Label
            {
                Text = "35.000đ",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(296, 42)
            });
            cardReceipt.Controls.Add(new Label
            {
                Text = "50% đường • 70% đá • Gia Huy trả trước",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = clrTextMuted,
                AutoSize = true,
                Location = new Point(16, 63)
            });

            cardReceipt.Controls.Add(new Label
            {
                Text = "2 × Bạc xỉu & Bánh ngọt (L)",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(16, 92)
            });
            cardReceipt.Controls.Add(new Label
            {
                Text = "104.000đ",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(288, 92)
            });

            Panel divider = new Panel
            {
                Location = new Point(16, 136),
                Size = new Size(346, 1),
                BackColor = clrBorderLight
            };
            cardReceipt.Controls.Add(divider);

            cardReceipt.Controls.Add(new Label
            {
                Text = "Tổng cộng",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(16, 154)
            });

            Label lblTotalMoney = new Label
            {
                Text = "139.000đ",
                Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
                ForeColor = clrPrimaryGreen,
                AutoSize = true,
                Location = new Point(280, 154)
            };
            cardReceipt.Controls.Add(lblTotalMoney);

            // 2 nút hành động song song (1 nút xanh Indigo đậm + 1 nút trắng viền đỏ giống hệt mẫu)
            Guna2Button btnSplitEqual = new Guna2Button
            {
                Text = "Chia đều (69.500đ/người)",
                Location = new Point(20, 282),
                Size = new Size(242, 46),
                BorderRadius = 14,
                FillColor = clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            Guna2Button btnRemind = new Guna2Button
            {
                Text = "Nhắc nợ TCP",
                Location = new Point(272, 282),
                Size = new Size(126, 46),
                BorderRadius = 14,
                FillColor = clrCardBg,
                ForeColor = clrRedDanger,
                BorderColor = clrRedDanger,
                BorderThickness = 1,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            // Thẻ Thanh toán (Card 2 - có nút viền sáng Tiền mặt / Chuyển khoản QR)
            Guna2Panel cardPayment = new Guna2Panel
            {
                Location = new Point(20, 340),
                Size = new Size(378, 232),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            cardPayment.Controls.Add(new Label
            {
                Text = "Thanh toán",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = clrTextMain,
                AutoSize = true,
                Location = new Point(16, 16)
            });

            Guna2Button badgePayState = new Guna2Button
            {
                Text = "Chưa thanh toán",
                Location = new Point(236, 13),
                Size = new Size(126, 26),
                BorderRadius = 8,
                FillColor = clrBadgeOrangeBg,
                ForeColor = clrBadgeOrangeTxt,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            cardPayment.Controls.Add(badgePayState);

            // Hai nút chọn phương thức có viền màu sáng nổi bật khi chọn
            Guna2Button btnCash = new Guna2Button
            {
                Text = "Tiền mặt",
                Location = new Point(16, 50),
                Size = new Size(166, 44),
                BorderRadius = 12,
                FillColor = clrSoftIndigo,
                ForeColor = clrIndigo,
                BorderColor = clrIndigo,
                BorderThickness = 2,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            Guna2Button btnQr = new Guna2Button
            {
                Text = "Chuyển khoản QR",
                Location = new Point(196, 50),
                Size = new Size(166, 44),
                BorderRadius = 12,
                FillColor = clrCardBg,
                ForeColor = clrTextMain,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            btnCash.Click += (s, e) =>
            {
                btnCash.FillColor = clrSoftIndigo;
                btnCash.ForeColor = clrIndigo;
                btnCash.BorderColor = clrIndigo;
                btnCash.BorderThickness = 2;

                btnQr.FillColor = clrCardBg;
                btnQr.ForeColor = clrTextMain;
                btnQr.BorderColor = clrBorderLight;
                btnQr.BorderThickness = 1;
            };

            btnQr.Click += (s, e) =>
            {
                btnQr.FillColor = clrSoftIndigo;
                btnQr.ForeColor = clrIndigo;
                btnQr.BorderColor = clrIndigo;
                btnQr.BorderThickness = 2;

                btnCash.FillColor = clrCardBg;
                btnCash.ForeColor = clrTextMain;
                btnCash.BorderColor = clrBorderLight;
                btnCash.BorderThickness = 1;
            };

            cardPayment.Controls.Add(btnCash);
            cardPayment.Controls.Add(btnQr);

            cardPayment.Controls.Add(new Label
            {
                Text = "Mã giao dịch / Ghi chú chuyển khoản",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = clrTextMuted,
                AutoSize = true,
                Location = new Point(16, 104)
            });

            Guna2TextBox txtRefCode = new Guna2TextBox
            {
                PlaceholderText = "reference_code (VD: FT26310998)...",
                Location = new Point(16, 126),
                Size = new Size(346, 38),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F)
            };
            cardPayment.Controls.Add(txtRefCode);

            Guna2Button btnConfirmPaid = new Guna2Button
            {
                Text = "Xác nhận đã thu 139.000đ",
                Location = new Point(16, 174),
                Size = new Size(346, 44),
                BorderRadius = 12,
                FillColor = clrPrimaryGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            Label lblHistoryLog = new Label
            {
                Text = "Lịch sử đồng bộ TCP\n14:32 Gia Huy tạo hóa đơn (PENDING)\n14:35 Huy Hoàng nhận thông báo (SYNCED)",
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = clrTextMuted,
                AutoSize = true,
                Location = new Point(22, 582)
            };

            btnConfirmPaid.Click += (s, e) =>
            {
                badgePayState.Text = "Đã quyết toán";
                badgePayState.FillColor = clrSoftGreenBg;
                badgePayState.ForeColor = clrPrimaryGreen;
                btnConfirmPaid.Text = "✓ Đã hoàn tất thu tiền";
                lblHistoryLog.Text += $"\n{DateTime.Now:HH:mm} Đã xác nhận thanh toán ({txtRefCode.Text})";
            };

            btnRemind.Click += (s, e) =>
            {
                lblHistoryLog.Text += $"\n{DateTime.Now:HH:mm} Đã bắn thông báo nhắc nợ sang Client Gia Huy";
            };

            cardPayment.Controls.Add(btnConfirmPaid);

            root.Controls.Add(pnlTopBanner);
            root.Controls.Add(cardReceipt);
            root.Controls.Add(btnSplitEqual);
            root.Controls.Add(btnRemind);
            root.Controls.Add(cardPayment);
            root.Controls.Add(lblHistoryLog);

            return root;
        }
    }
}