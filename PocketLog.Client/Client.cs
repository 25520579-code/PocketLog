using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using Guna.UI2.WinForms;

namespace PocketLog.Client
{
    public partial class Client : Form
    {
        private Guna2BorderlessForm borderlessForm;
        private Guna2DragControl dragControl;
        private Guna2Panel pnlHeader, pnlMainContainer, pnlBottomNav;
        private Guna2Button btnTabFeed, btnTabCamera, btnTabExpense, btnLogoutTop;
        private Label lblHeaderUser;

        private Panel pnlDrawerBackdrop;
        private Guna2Panel pnlFriendsDrawer;
        private System.Windows.Forms.Timer drawerTimer;
        private int drawerTargetX;

        // Quản lý kết nối TCP
        private TcpClient? tcpClient;
        private NetworkStream? netStream;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private bool isConnected = false;

        private string currentServerIp = "127.0.0.1";
        private int currentServerPort = 8888;
        private int currentUserId = 0;
        private string currentUsername = "";
        private string currentFullName = "";

        // Quản lý Webcam Laptop / PC
        private VideoCaptureDevice? webcamDevice;
        private bool isWebcamStreaming = false;
        private Bitmap? latestWebcamFrame;
        private readonly object frameLock = new object();

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

        // Giữ nguyên Key lưu CSDL và thêm tên ngắn gọn để không bị tràn chữ trên nút bấm
        private readonly (string Key, string ShortLabel, string Emoji)[] standardCategories = new[]
        {
            ("🍜 Đồ ăn/Thức uống", "🍜 Ăn uống", "🍜"),
            ("⛽ Xăng cộ", "⛽ Xăng cộ", "⛽"),
            ("👕 Quần áo", "👕 Quần áo", "👕"),
            ("🏠 Tiền trọ", "🏠 Tiền trọ", "🏠"),
            ("🛒 Mua sắm", "🛒 Mua sắm", "🛒"),
            ("📚 Học tập/Khác", "📚 Học tập", "📚")
        };

        private class FriendUser
        {
            public int UserId { get; set; }
            public string Name { get; set; } = "";
            public string Handle { get; set; } = "";
            public bool IsOnline { get; set; }
        }

        private class LocketPost
        {
            public int PostId { get; set; }
            public bool IsMine { get; set; }
            public string Author { get; set; } = "";
            public bool HasTag { get; set; }
            public List<string> TaggedFriends { get; set; } = new List<string>();
            public string Category { get; set; } = "";
            public string NoteTop { get; set; } = "";
            public string CaptionInside { get; set; } = "";
            public int Amount { get; set; }
            public string Time { get; set; } = "";
            public string Emoji { get; set; } = "📷";
            public Color PhotoBg { get; set; }
            public Image? CustomImage { get; set; }
        }

        private class TaggedDebtItem
        {
            public int SplitId { get; set; }
            public int CreditorId { get; set; }
            public string CreditorName { get; set; } = "";
            public int DebtorId { get; set; }
            public string DebtorName { get; set; } = "";
            public string Title { get; set; } = "";
            public string Category { get; set; } = "";
            public int TotalAmount { get; set; }
            public int MyShare { get; set; }
            public string SplitDetail { get; set; } = "";
            public string Time { get; set; } = "";
            public bool IsSentProof { get; set; }
            public bool IsConfirmedPaid { get; set; }
            public Image? ProofImage { get; set; }
            public string StatusNote { get; set; } = "";
        }

        private readonly List<FriendUser> myFriends = new List<FriendUser>();
        private readonly List<FriendUser> friendSuggestions = new List<FriendUser>();
        private readonly List<LocketPost> posts = new List<LocketPost>();
        private readonly List<TaggedDebtItem> taggedDebts = new List<TaggedDebtItem>();
        private readonly Dictionary<string, int> categoryBudgets = new Dictionary<string, int>();

        private int totalBudgetLimit = 3500000;
        private bool isUnpaidDropdownOpen = false;
        private int selectedSplitId = 0;
        private string activeTabName = "AUTH";

        public Client()
        {
            InitializeComponent();
            InitDefaultCategoryBudgets();
            SetupPhoneShell();
            ShowAuthScreen();
            this.FormClosing += (s, e) =>
            {
                StopWebcam();
                CloseCurrentSocket();
            };
        }

        private void InitDefaultCategoryBudgets()
        {
            categoryBudgets["🍜 Đồ ăn/Thức uống"] = 1200000;
            categoryBudgets["⛽ Xăng cộ"] = 300000;
            categoryBudgets["👕 Quần áo"] = 500000;
            categoryBudgets["🏠 Tiền trọ"] = 1500000;
            categoryBudgets["🛒 Mua sắm"] = 400000;
            categoryBudgets["📚 Học tập/Khác"] = 300000;
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

        private Guna2Button MakeBtn(string text, int x, int y, int w, int h, int radius, Color fill, Color fore, Color border, int borderThick = 1, float fontSize = 8.2F)
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
                TextOffset = Point.Empty,
                Padding = Padding.Empty,
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
            pnlHeader.Controls.Add(MakeLabel("● PocketLog", 14, 15, 11F, FontStyle.Bold, clrPrimaryGreen));

            lblHeaderUser = MakeLabel("", 130, 17, 8.2F, FontStyle.Bold, clrIndigo);
            pnlHeader.Controls.Add(lblHeaderUser);

            btnLogoutTop = MakeBtn("Đăng xuất", 232, 11, 94, 30, 8, clrBadgeOrangeBg, clrBadgeOrangeTxt, Color.Transparent, 0, 7.6F);
            btnLogoutTop.Visible = false;
            btnLogoutTop.Click += (s, e) => DisconnectAndLogout();
            pnlHeader.Controls.Add(btnLogoutTop);

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

            pnlBottomNav = new Guna2Panel { Dock = DockStyle.Bottom, Height = 74, FillColor = clrCardBg, Visible = false };
            pnlBottomNav.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorderLight });

            btnTabFeed = MakeBtn("Bảng tin", 14, 14, 114, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.5F);
            btnTabCamera = MakeBtn("Chụp ảnh", 136, 14, 114, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.5F);
            btnTabExpense = MakeBtn("Quản lý chi tiêu", 258, 14, 148, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.5F);

            btnTabFeed.Click += (s, e) => { activeTabName = "FEED"; ActivateMenu(btnTabFeed, BuildFeedScreen()); };
            btnTabCamera.Click += (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
            btnTabExpense.Click += (s, e) => { activeTabName = "EXPENSE"; ActivateMenu(btnTabExpense, BuildExpenseScreen()); };

            pnlBottomNav.Controls.AddRange(new Control[] { btnTabFeed, btnTabCamera, btnTabExpense });

            pnlMainContainer = new Guna2Panel { Dock = DockStyle.Fill, FillColor = clrAppBg };

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottomNav);

            SetupFriendsDrawer();
        }

        private void ActivateMenu(Guna2Button selectedBtn, Control screenContent)
        {
            // Nếu rời khỏi tab Chụp ảnh thì tự động tắt Webcam để giải phóng thiết bị
            if (selectedBtn != btnTabCamera)
            {
                StopWebcam();
            }

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
    }
}
