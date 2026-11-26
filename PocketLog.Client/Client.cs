using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client : Form
    {
        protected class SmoothScrollPanel : Panel
        {
            public SmoothScrollPanel()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            }
        }

        protected class BufferedTableLayout : TableLayoutPanel
        {
            public BufferedTableLayout()
            {
                this.DoubleBuffered = true;
                this.BackColor = Color.Transparent;
            }
        }

        protected enum UiIcon
        {
            Brand, Feed, Camera, Wallet, Photo, Users, UserPlus,
            Send, Clipboard, SwitchCam, Close, Check, Upload,
            Tag, Receipt, Food, Fuel, Shirt, Home, Cart, Book,
            ChevronDown, ChevronUp, ChevronLeft
        }

        private Guna2BorderlessForm borderlessForm;
        private Guna2Panel pnlHeader, pnlMainContainer, pnlBottomNav;
        private Guna2Panel pnlHeaderUserChip;
        private Guna2CircleButton avtHeaderUser;
        private Guna2Button btnTabFeed, btnTabCamera, btnTabExpense, btnLogoutTop;
        private Label lblHeaderUser;
        private readonly ToolTip uiToolTip = new ToolTip();

        private Panel pnlDrawerBackdrop;
        private Guna2Panel pnlFriendsDrawer;
        private System.Windows.Forms.Timer drawerTimer;
        private int drawerTargetX;

        // Quản lý Socket TCP & Callback phản hồi UI
        private TcpClient tcpClient;
        private NetworkStream netStream;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private bool isConnected = false;
        private Action<string> onAuthFailedUI;
        private string friendDrawerFeedback = "";
        private bool friendDrawerFeedbackSuccess = true;

        private string currentServerIp = "127.0.0.1";
        private int currentServerPort = 8888;
        private int currentUserId = 0;
        private string currentUsername = "";
        private string currentFullName = "";

        // Giữ nguyên bảng màu chủ đạo Sky Blue & Slate Grey, tinh chỉnh sắc độ viền mềm mại hơn
        private readonly Color clrAppBg = Color.FromArgb(241, 245, 249);
        private readonly Color clrCardBg = Color.FromArgb(255, 255, 255);
        private readonly Color clrInputBg = Color.FromArgb(248, 250, 252);
        private readonly Color clrPrimaryGreen = Color.FromArgb(56, 130, 246);
        private readonly Color clrSoftGreenBg = Color.FromArgb(224, 242, 254);
        private readonly Color clrSoftGreenBorder = Color.FromArgb(147, 197, 253);
        private readonly Color clrIndigo = Color.FromArgb(71, 85, 105);
        private readonly Color clrSoftIndigo = Color.FromArgb(226, 232, 240);
        private readonly Color clrRedDanger = Color.FromArgb(225, 29, 72);
        private readonly Color clrSoftRedBg = Color.FromArgb(255, 228, 230);
        private readonly Color clrBorderLight = Color.FromArgb(226, 232, 240);
        private readonly Color clrTextMain = Color.FromArgb(30, 41, 59);
        private readonly Color clrTextMuted = Color.FromArgb(100, 116, 139);

        private static readonly string SemiBoldFamilyName = ResolveSemiBoldFamily();

        // Giữ nguyên Key để tương thích 100% với SQL Server
        private readonly (string Key, string ShortLabel, string Emoji)[] standardCategories = new[]
        {
            ("🍜 Đồ ăn/Thức uống", "Ăn uống", "🍜"),
            ("⛽ Xăng cộ", "Xăng cộ", "⛽"),
            ("👕 Quần áo", "Quần áo", "👕"),
            ("🏠 Tiền trọ", "Tiền trọ", "🏠"),
            ("🛒 Mua sắm", "Mua sắm", "🛒"),
            ("📚 Học tập/Khác", "Học tập", "📚")
        };

        public class FriendUser
        {
            public int UserId { get; set; }
            public string Name { get; set; } = "";
            public string Handle { get; set; } = "";
            public bool IsOnline { get; set; }
        }

        public class LocketPost
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
            public Image CustomImage { get; set; }
        }

        public class TaggedDebtItem
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
            public int MyShare { get; set; }
            public string SplitDetail { get; set; } = "";
            public string Time { get; set; } = "";
            public bool IsSentProof { get; set; }
            public bool IsConfirmedPaid { get; set; }
            public Image ProofImage { get; set; }
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
        private readonly List<string> activeTabs = new List<string>();
        private readonly Dictionary<string, Panel> paneHosts = new Dictionary<string, Panel>();
        private bool isFeedShowingGallery = false;
        private Label lblSplitModeHint;

        protected const int SplitCapableMinWidth = 1100;
        protected bool IsSplitModeCapable => this.WindowState == FormWindowState.Maximized || this.ClientSize.Width >= SplitCapableMinWidth;
        protected bool IsTabActive(string tabKey) => activeTabs.Contains(tabKey);

        protected const int DefaultColWidth = 560;
        protected const int MaxColWidth = 760;

        public Client()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            InitializeDefaultBudgets();
            SetupPhoneShell();
            ShowAuthScreen();
            this.FormClosing += (s, e) =>
            {
                StopWebcam();
                CloseCurrentSocket();
                DisposeCachedImages();
            };
        }

        private static string ResolveSemiBoldFamily()
        {
            try
            {
                using var test = new FontFamily("Segoe UI Semibold");
                return test.Name;
            }
            catch
            {
                return "Segoe UI";
            }
        }

        protected Font FontRegular(float size) => new Font("Segoe UI", size, FontStyle.Regular);
        protected Font FontMedium(float size) => SemiBoldFamilyName == "Segoe UI Semibold"
            ? new Font("Segoe UI Semibold", size, FontStyle.Regular)
            : new Font("Segoe UI", size, FontStyle.Bold);
        protected Font FontBold(float size) => new Font("Segoe UI", size, FontStyle.Bold);

        protected string GetCategoryCleanName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Khoảnh khắc Pocket";
            if (key.Contains("Đồ ăn")) return "Đồ ăn & Thức uống";
            if (key.Contains("Xăng")) return "Xăng cộ";
            if (key.Contains("Quần áo")) return "Quần áo";
            if (key.Contains("Tiền trọ")) return "Tiền trọ";
            if (key.Contains("Mua sắm")) return "Mua sắm";
            if (key.Contains("Học tập")) return "Học tập & Khác";
            return key.Trim();
        }

        protected string GetCategoryShortClean(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Cá nhân";
            foreach (var c in standardCategories)
            {
                if (c.Key == key || c.ShortLabel == key) return c.ShortLabel;
            }
            return GetCategoryCleanName(key);
        }

        protected UiIcon GetCategoryIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return UiIcon.Camera;
            if (key.Contains("Đồ ăn") || key.Contains("Ăn uống")) return UiIcon.Food;
            if (key.Contains("Xăng")) return UiIcon.Fuel;
            if (key.Contains("Quần áo")) return UiIcon.Shirt;
            if (key.Contains("Tiền trọ")) return UiIcon.Home;
            if (key.Contains("Mua sắm")) return UiIcon.Cart;
            if (key.Contains("Học tập")) return UiIcon.Book;
            return UiIcon.Tag;
        }

        private void InitializeDefaultBudgets()
        {
            categoryBudgets.Clear();
            categoryBudgets["🍜 Đồ ăn/Thức uống"] = 1200000;
            categoryBudgets["⛽ Xăng cộ"] = 300000;
            categoryBudgets["👕 Quần áo"] = 500000;
            categoryBudgets["🏠 Tiền trọ"] = 1500000;
            categoryBudgets["🛒 Mua sắm"] = 400000;
            categoryBudgets["📚 Học tập/Khác"] = 300000;
        }

        private void DisposeCachedImages()
        {
            foreach (var p in posts)
            {
                try { p.CustomImage?.Dispose(); } catch { }
                p.CustomImage = null;
            }
            posts.Clear();

            foreach (var d in taggedDebts)
            {
                try { d.ProofImage?.Dispose(); } catch { }
                d.ProofImage = null;
            }
            taggedDebts.Clear();
        }

        protected void UpdateHeaderUserUI(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                lblHeaderUser.Text = "";
                avtHeaderUser.Text = "";
                pnlHeaderUserChip.Visible = false;
                btnLogoutTop.Visible = false;
            }
            else
            {
                string clean = fullName.Trim();
                avtHeaderUser.Text = clean.Substring(0, 1).ToUpper();
                lblHeaderUser.Text = clean;
                pnlHeaderUserChip.Visible = true;
                btnLogoutTop.Visible = true;
            }
            LayoutHeaderControls();
        }

        // Bố cục trang cuộn tự căn giữa & co giãn mượt theo chiều rộng cửa sổ
        protected Panel CreateResponsiveScrollPage(out Panel colPanel)
        {
            var scrollRoot = new SmoothScrollPanel
            {
                BackColor = clrAppBg,
                AutoScroll = true,
                Dock = DockStyle.Fill
            };
            scrollRoot.MouseEnter += (s, e) => { if (!scrollRoot.ContainsFocus) scrollRoot.Focus(); };

            var column = new Panel
            {
                Location = new Point(16, 0),
                Size = new Size(DefaultColWidth, 100),
                BackColor = Color.Transparent
            };
            scrollRoot.Controls.Add(column);

            void SyncColumnBounds()
            {
                if (scrollRoot.IsDisposed || column.IsDisposed) return;
                int availW = scrollRoot.ClientSize.Width;
                if (availW <= 0) return;

                int sidePad = availW < 450 ? 10 : 16;
                int targetW = Math.Clamp(availW - sidePad * 2, 260, MaxColWidth);
                int targetX = Math.Max(sidePad, (availW - targetW) / 2);
                int currentScrollY = scrollRoot.AutoScrollPosition.Y;

                if (column.Left != targetX || column.Width != targetW)
                {
                    column.Location = new Point(targetX, currentScrollY);
                    column.Width = targetW;
                }
            }

            AttachEdgeResizeRedirector(scrollRoot);

            scrollRoot.Resize += (s, e) => SyncColumnBounds();
            scrollRoot.Layout += (s, e) => SyncColumnBounds();

            colPanel = column;
            return scrollRoot;
        }

        // Cắt ảnh tỷ lệ trung tâm (Center-Crop) giống Android ImageView CENTER_CROP
        protected Bitmap FitImageCover(Image src, int targetW, int targetH)
        {
            if (src == null || targetW <= 4 || targetH <= 4) return null;
            try
            {
                float scale = Math.Max((float)targetW / src.Width, (float)targetH / src.Height);
                int drawW = Math.Max(1, (int)Math.Ceiling(src.Width * scale));
                int drawH = Math.Max(1, (int)Math.Ceiling(src.Height * scale));
                int offsetX = (targetW - drawW) / 2;
                int offsetY = (targetH - drawH) / 2;

                Bitmap bmp = new Bitmap(targetW, targetH);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(src, new Rectangle(offsetX, offsetY, drawW, drawH));
                }
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        // Vẽ Vector Icon nét tròn mềm mại chống lỗi font trên mọi máy Windows
        protected Bitmap CreateVectorIcon(UiIcon icon, Color color, int size = 18)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float stroke = Math.Max(1.6f, size / 10.2f);
                using Pen p = new Pen(color, stroke)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                using SolidBrush b = new SolidBrush(color);

                float s = size;
                switch (icon)
                {
                    case UiIcon.Brand:
                        FillRoundedRect(g, b, s * 0.14f, s * 0.22f, s * 0.72f, s * 0.58f, s * 0.2f);
                        using (SolidBrush hole = new SolidBrush(clrCardBg))
                            g.FillEllipse(hole, s * 0.35f, s * 0.36f, s * 0.30f, s * 0.30f);
                        break;

                    case UiIcon.Feed:
                        DrawRoundedRect(g, p, s * 0.14f, s * 0.14f, s * 0.30f, s * 0.30f, s * 0.08f);
                        DrawRoundedRect(g, p, s * 0.56f, s * 0.14f, s * 0.30f, s * 0.30f, s * 0.08f);
                        DrawRoundedRect(g, p, s * 0.14f, s * 0.56f, s * 0.30f, s * 0.30f, s * 0.08f);
                        DrawRoundedRect(g, p, s * 0.56f, s * 0.56f, s * 0.30f, s * 0.30f, s * 0.08f);
                        break;

                    case UiIcon.Camera:
                        DrawRoundedRect(g, p, s * 0.12f, s * 0.26f, s * 0.76f, s * 0.56f, s * 0.14f);
                        g.DrawLine(p, s * 0.34f, s * 0.26f, s * 0.40f, s * 0.16f);
                        g.DrawLine(p, s * 0.40f, s * 0.16f, s * 0.60f, s * 0.16f);
                        g.DrawLine(p, s * 0.60f, s * 0.16f, s * 0.66f, s * 0.26f);
                        g.DrawEllipse(p, s * 0.35f, s * 0.38f, s * 0.30f, s * 0.30f);
                        break;

                    case UiIcon.Wallet:
                        DrawRoundedRect(g, p, s * 0.12f, s * 0.22f, s * 0.76f, s * 0.58f, s * 0.14f);
                        g.DrawLine(p, s * 0.12f, s * 0.38f, s * 0.88f, s * 0.38f);
                        g.FillEllipse(b, s * 0.65f, s * 0.51f, s * 0.11f, s * 0.11f);
                        break;

                    case UiIcon.Photo:
                        DrawRoundedRect(g, p, s * 0.14f, s * 0.16f, s * 0.72f, s * 0.68f, s * 0.14f);
                        g.FillEllipse(b, s * 0.30f, s * 0.30f, s * 0.13f, s * 0.13f);
                        g.DrawLines(p, new[]
                        {
                            new PointF(s * 0.20f, s * 0.74f),
                            new PointF(s * 0.44f, s * 0.50f),
                            new PointF(s * 0.62f, s * 0.66f),
                            new PointF(s * 0.74f, s * 0.54f),
                            new PointF(s * 0.84f, s * 0.72f)
                        });
                        break;

                    case UiIcon.Users:
                        g.DrawEllipse(p, s * 0.22f, s * 0.18f, s * 0.30f, s * 0.30f);
                        g.DrawArc(p, s * 0.12f, s * 0.54f, s * 0.50f, s * 0.38f, 180, 180);
                        g.DrawArc(p, s * 0.56f, s * 0.22f, s * 0.24f, s * 0.26f, 270, 180);
                        g.DrawArc(p, s * 0.56f, s * 0.56f, s * 0.32f, s * 0.32f, 250, 110);
                        break;

                    case UiIcon.UserPlus:
                        g.DrawEllipse(p, s * 0.18f, s * 0.18f, s * 0.30f, s * 0.30f);
                        g.DrawArc(p, s * 0.10f, s * 0.54f, s * 0.46f, s * 0.36f, 180, 180);
                        g.DrawLine(p, s * 0.74f, s * 0.34f, s * 0.74f, s * 0.62f);
                        g.DrawLine(p, s * 0.60f, s * 0.48f, s * 0.88f, s * 0.48f);
                        break;

                    case UiIcon.Send:
                        g.DrawPolygon(p, new[]
                        {
                            new PointF(s * 0.14f, s * 0.46f),
                            new PointF(s * 0.84f, s * 0.16f),
                            new PointF(s * 0.56f, s * 0.84f),
                            new PointF(s * 0.44f, s * 0.54f)
                        });
                        g.DrawLine(p, s * 0.84f, s * 0.16f, s * 0.44f, s * 0.54f);
                        break;

                    case UiIcon.Clipboard:
                        DrawRoundedRect(g, p, s * 0.20f, s * 0.22f, s * 0.60f, s * 0.64f, s * 0.10f);
                        DrawRoundedRect(g, p, s * 0.35f, s * 0.12f, s * 0.30f, s * 0.16f, s * 0.06f);
                        g.DrawLine(p, s * 0.33f, s * 0.48f, s * 0.67f, s * 0.48f);
                        g.DrawLine(p, s * 0.33f, s * 0.64f, s * 0.58f, s * 0.64f);
                        break;

                    case UiIcon.SwitchCam:
                        g.DrawArc(p, s * 0.18f, s * 0.18f, s * 0.64f, s * 0.64f, 200, 135);
                        g.DrawArc(p, s * 0.18f, s * 0.18f, s * 0.64f, s * 0.64f, 20, 135);
                        g.DrawLines(p, new[] { new PointF(s * 0.66f, s * 0.16f), new PointF(s * 0.80f, s * 0.28f), new PointF(s * 0.64f, s * 0.34f) });
                        g.DrawLines(p, new[] { new PointF(s * 0.34f, s * 0.84f), new PointF(s * 0.20f, s * 0.72f), new PointF(s * 0.36f, s * 0.66f) });
                        break;

                    case UiIcon.Close:
                        g.DrawLine(p, s * 0.26f, s * 0.26f, s * 0.74f, s * 0.74f);
                        g.DrawLine(p, s * 0.74f, s * 0.26f, s * 0.26f, s * 0.74f);
                        break;

                    case UiIcon.Check:
                        g.DrawLines(p, new[]
                        {
                            new PointF(s * 0.20f, s * 0.52f),
                            new PointF(s * 0.42f, s * 0.74f),
                            new PointF(s * 0.80f, s * 0.28f)
                        });
                        break;

                    case UiIcon.Upload:
                        g.DrawLine(p, s * 0.50f, s * 0.66f, s * 0.50f, s * 0.20f);
                        g.DrawLines(p, new[] { new PointF(s * 0.32f, s * 0.38f), new PointF(s * 0.50f, s * 0.20f), new PointF(s * 0.68f, s * 0.38f) });
                        g.DrawLines(p, new[]
                        {
                            new PointF(s * 0.18f, s * 0.62f),
                            new PointF(s * 0.18f, s * 0.82f),
                            new PointF(s * 0.82f, s * 0.82f),
                            new PointF(s * 0.82f, s * 0.62f)
                        });
                        break;

                    case UiIcon.Receipt:
                        g.DrawLines(p, new[]
                        {
                            new PointF(s * 0.22f, s * 0.14f),
                            new PointF(s * 0.78f, s * 0.14f),
                            new PointF(s * 0.78f, s * 0.84f),
                            new PointF(s * 0.64f, s * 0.76f),
                            new PointF(s * 0.50f, s * 0.84f),
                            new PointF(s * 0.36f, s * 0.76f),
                            new PointF(s * 0.22f, s * 0.84f),
                            new PointF(s * 0.22f, s * 0.14f)
                        });
                        g.DrawLine(p, s * 0.34f, s * 0.36f, s * 0.66f, s * 0.36f);
                        g.DrawLine(p, s * 0.34f, s * 0.54f, s * 0.60f, s * 0.54f);
                        break;

                    case UiIcon.Food:
                        g.DrawArc(p, s * 0.16f, s * 0.32f, s * 0.68f, s * 0.48f, 0, 180);
                        g.DrawLine(p, s * 0.14f, s * 0.54f, s * 0.86f, s * 0.54f);
                        g.DrawLine(p, s * 0.34f, s * 0.84f, s * 0.66f, s * 0.84f);
                        g.DrawLine(p, s * 0.38f, s * 0.22f, s * 0.38f, s * 0.38f);
                        g.DrawLine(p, s * 0.62f, s * 0.18f, s * 0.62f, s * 0.38f);
                        break;

                    case UiIcon.Fuel:
                        DrawRoundedRect(g, p, s * 0.20f, s * 0.20f, s * 0.44f, s * 0.64f, s * 0.08f);
                        g.DrawLine(p, s * 0.28f, s * 0.40f, s * 0.56f, s * 0.40f);
                        g.DrawLines(p, new[]
                        {
                            new PointF(s * 0.64f, s * 0.34f),
                            new PointF(s * 0.78f, s * 0.44f),
                            new PointF(s * 0.78f, s * 0.72f),
                            new PointF(s * 0.68f, s * 0.72f)
                        });
                        break;

                    case UiIcon.Shirt:
                        g.DrawPolygon(p, new[]
                        {
                            new PointF(s * 0.34f, s * 0.18f),
                            new PointF(s * 0.66f, s * 0.18f),
                            new PointF(s * 0.86f, s * 0.36f),
                            new PointF(s * 0.74f, s * 0.50f),
                            new PointF(s * 0.66f, s * 0.42f),
                            new PointF(s * 0.66f, s * 0.82f),
                            new PointF(s * 0.34f, s * 0.82f),
                            new PointF(s * 0.34f, s * 0.42f),
                            new PointF(s * 0.26f, s * 0.50f),
                            new PointF(s * 0.14f, s * 0.36f)
                        });
                        break;

                    case UiIcon.Home:
                        g.DrawLines(p, new[] { new PointF(s * 0.14f, s * 0.48f), new PointF(s * 0.50f, s * 0.18f), new PointF(s * 0.86f, s * 0.48f) });
                        g.DrawLines(p, new[]
                        {
                            new PointF(s * 0.24f, s * 0.42f),
                            new PointF(s * 0.24f, s * 0.82f),
                            new PointF(s * 0.76f, s * 0.82f),
                            new PointF(s * 0.76f, s * 0.42f)
                        });
                        break;

                    case UiIcon.Cart:
                        DrawRoundedRect(g, p, s * 0.20f, s * 0.34f, s * 0.60f, s * 0.50f, s * 0.10f);
                        g.DrawArc(p, s * 0.34f, s * 0.16f, s * 0.32f, s * 0.34f, 180, 180);
                        break;

                    case UiIcon.Book:
                        DrawRoundedRect(g, p, s * 0.18f, s * 0.18f, s * 0.64f, s * 0.64f, s * 0.10f);
                        g.DrawLine(p, s * 0.34f, s * 0.18f, s * 0.34f, s * 0.82f);
                        g.DrawLine(p, s * 0.46f, s * 0.38f, s * 0.68f, s * 0.38f);
                        break;

                    case UiIcon.ChevronDown:
                        g.DrawLines(p, new[] { new PointF(s * 0.24f, s * 0.38f), new PointF(s * 0.50f, s * 0.64f), new PointF(s * 0.76f, s * 0.38f) });
                        break;

                    case UiIcon.ChevronUp:
                        g.DrawLines(p, new[] { new PointF(s * 0.24f, s * 0.62f), new PointF(s * 0.50f, s * 0.36f), new PointF(s * 0.76f, s * 0.62f) });
                        break;

                    case UiIcon.ChevronLeft:
                        g.DrawLines(p, new[] { new PointF(s * 0.62f, s * 0.22f), new PointF(s * 0.34f, s * 0.50f), new PointF(s * 0.62f, s * 0.78f) });
                        break;

                    default:
                        g.DrawPolygon(p, new[]
                        {
                            new PointF(s * 0.18f, s * 0.48f),
                            new PointF(s * 0.48f, s * 0.18f),
                            new PointF(s * 0.80f, s * 0.18f),
                            new PointF(s * 0.80f, s * 0.50f),
                            new PointF(s * 0.50f, s * 0.80f)
                        });
                        g.FillEllipse(b, s * 0.60f, s * 0.28f, s * 0.10f, s * 0.10f);
                        break;
                }
            }
            return bmp;
        }

        private static void DrawRoundedRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
        {
            using GraphicsPath path = CreateRoundedPath(x, y, w, h, r);
            g.DrawPath(pen, path);
        }

        private static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float w, float h, float r)
        {
            using GraphicsPath path = CreateRoundedPath(x, y, w, h, r);
            g.FillPath(brush, path);
        }

        private static GraphicsPath CreateRoundedPath(float x, float y, float w, float h, float r)
        {
            float d = Math.Max(1f, Math.Min(r * 2f, Math.Min(w, h)));
            GraphicsPath path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected Guna2Panel MakeSoftIconBadge(UiIcon icon, int x, int y, int boxSize, int radius, Color bgFill, Color iconColor)
        {
            var badge = new Guna2Panel
            {
                Location = new Point(x, y),
                Size = new Size(boxSize, boxSize),
                BorderRadius = radius,
                FillColor = bgFill,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            int icoSz = Math.Max(14, (int)(boxSize * 0.52f));
            var pic = new PictureBox
            {
                Size = new Size(icoSz, icoSz),
                Location = new Point((boxSize - icoSz) / 2, (boxSize - icoSz) / 2),
                Image = CreateVectorIcon(icon, iconColor, icoSz),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent,
                Enabled = false
            };
            badge.Controls.Add(pic);
            return badge;
        }

        protected Label MakeLabel(string text, int x, int y, float size, FontStyle style, Color color, int maxWidth = 0, int lines = 1)
        {
            Font chosenFont = style == FontStyle.Bold
                ? FontMedium(size)
                : new Font("Segoe UI", size, style);

            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Font = chosenFont,
                ForeColor = color,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            if (maxWidth > 0)
            {
                int lineH = TextRenderer.MeasureText("Ág", chosenFont).Height;
                int safeHeight = lineH * Math.Max(1, lines) + 4;
                lbl.AutoSize = false;
                lbl.Size = new Size(maxWidth, safeHeight);
                lbl.AutoEllipsis = true;
                lbl.TextAlign = ContentAlignment.TopLeft;
            }
            else
            {
                lbl.AutoSize = true;
            }
            return lbl;
        }

        protected int MeasurePillBtnWidth(string text, float fontSize = 8.8F, int radius = 16, bool hasIcon = false, int iconPx = 16)
        {
            using Font f = FontMedium(fontSize);
            int textW = TextRenderer.MeasureText(text ?? "", f, new Size(2400, 200), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
            int iconExtra = hasIcon ? (iconPx + 12) : 0;
            int sidePad = Math.Max(38, radius + 20);
            return textW + iconExtra + sidePad;
        }

        protected Guna2Button MakeBtn(string text, int x, int y, int w, int h, int radius, Color fill, Color fore, Color border, int borderThick = 1, float fontSize = 8.8F)
        {
            var btn = new Guna2Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BorderRadius = radius,
                FillColor = fill,
                ForeColor = fore,
                BorderColor = border,
                BorderThickness = borderThick,
                Font = FontMedium(fontSize),
                TextOffset = Point.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                AutoRoundedCorners = false,
                Cursor = Cursors.Hand
            };
            btn.DisabledState.FillColor = clrAppBg;
            btn.DisabledState.ForeColor = clrTextMuted;
            btn.DisabledState.BorderColor = clrBorderLight;
            return btn;
        }

        protected void AttachBtnIcon(Guna2Button btn, UiIcon icon, Color color, int iconPx = 16)
        {
            Image old = btn.Image;
            btn.Image = CreateVectorIcon(icon, color, iconPx);
            btn.ImageSize = new Size(iconPx, iconPx);
            btn.ImageAlign = HorizontalAlignment.Center;
            btn.ImageOffset = new Point(-2, 0);
            btn.TextOffset = new Point(2, 0);
            old?.Dispose();
        }

        protected Guna2TextBox MakeTextBox(string text, string placeholder, int x, int y, int w, int h, Color parentBg, bool isBold = false)
        {
            var tb = new Guna2TextBox
            {
                Text = text,
                PlaceholderText = placeholder,
                PlaceholderForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(x, y),
                Size = new Size(w, h),
                BorderRadius = 12,
                FillColor = clrInputBg,
                BackColor = parentBg,
                BorderColor = clrBorderLight,
                ForeColor = clrTextMain,
                Font = isBold ? FontMedium(9.2F) : FontRegular(9.2F),
                TextOffset = new Point(6, 0)
            };
            tb.HoverState.BorderColor = clrSoftGreenBorder;
            tb.FocusedState.BorderColor = clrPrimaryGreen;
            tb.FocusedState.FillColor = Color.White;
            return tb;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        private Guna2ControlBox btnWinMin, btnWinMax, btnWinClose;
        private Label lblBrand;

        private sealed class ChildEdgeRedirector : NativeWindow
        {
            private readonly Client owner;
            private readonly Control target;

            public ChildEdgeRedirector(Client owner, Control target)
            {
                this.owner = owner;
                this.target = target;
                if (target.IsHandleCreated) AssignHandle(target.Handle);
                target.HandleCreated += OnHandleCreated;
                target.HandleDestroyed += OnHandleDestroyed;
            }

            private void OnHandleCreated(object sender, EventArgs e) => AssignHandle(target.Handle);
            private void OnHandleDestroyed(object sender, EventArgs e) => ReleaseHandle();

            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84;
                const int HTTRANSPARENT = -1;
                if (m.Msg == WM_NCHITTEST && owner != null && !owner.IsDisposed && owner.WindowState == FormWindowState.Normal)
                {
                    int lp = m.LParam.ToInt32();
                    Point screenPt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                    if (owner.GetResizeHitTest(screenPt) != 0)
                    {
                        m.Result = (IntPtr)HTTRANSPARENT;
                        return;
                    }
                }
                base.WndProc(ref m);
            }
        }

        protected void AttachEdgeResizeRedirector(Control ctrl)
        {
            if (ctrl == null) return;
            _ = new ChildEdgeRedirector(this, ctrl);
        }

        private int GetResizeHitTest(Point screenPt)
        {
            if (this.WindowState != FormWindowState.Normal) return 0;

            Point pt = this.PointToClient(screenPt);
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            const int edgeGrip = 8;
            const int cornerGrip = 18;

            const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                      HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

            bool nearLeft = pt.X <= edgeGrip;
            bool nearRight = pt.X >= w - edgeGrip;
            bool nearTop = pt.Y <= edgeGrip;
            bool nearBottom = pt.Y >= h - edgeGrip;

            bool inCornerLeft = pt.X <= cornerGrip;
            bool inCornerRight = pt.X >= w - cornerGrip;
            bool inCornerTop = pt.Y <= cornerGrip;
            bool inCornerBottom = pt.Y >= h - cornerGrip;

            if ((nearLeft && inCornerTop) || (nearTop && inCornerLeft)) return HTTOPLEFT;
            if ((nearRight && inCornerTop) || (nearTop && inCornerRight)) return HTTOPRIGHT;
            if ((nearLeft && inCornerBottom) || (nearBottom && inCornerLeft)) return HTBOTTOMLEFT;
            if ((nearRight && inCornerBottom) || (nearBottom && inCornerRight)) return HTBOTTOMRIGHT;

            if (nearLeft) return HTLEFT;
            if (nearRight) return HTRIGHT;
            if (nearTop) return HTTOP;
            if (nearBottom) return HTBOTTOM;

            return 0;
        }

        private void BeginDragWindowFromHeader(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (GetResizeHitTest(Cursor.Position) != 0) return;
            ReleaseCapture();
            SendMessage(this.Handle, 0xA1, 0x2, 0);
        }

        private void SetupPhoneShell()
        {
            this.Controls.Clear();
            var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1440, 900);
            int initW = Math.Clamp(560, 420, Math.Max(420, workArea.Width - 60));
            int initH = Math.Clamp(workArea.Height - 56, 640, 900);

            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(initW, initH);
            this.MinimumSize = new Size(445, 580);
            this.MaximizedBounds = workArea;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrBorderLight;
            this.Padding = new Padding(3);
            this.ResizeRedraw = true;

            borderlessForm = new Guna2BorderlessForm
            {
                ContainerControl = this,
                BorderRadius = 20,
                ResizeForm = false,
                DragForm = false,
                HasFormShadow = true
            };

            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized) return;

                bool isMax = this.WindowState == FormWindowState.Maximized;
                int targetPad = isMax ? 0 : 3;
                if (this.Padding.All != targetPad)
                    this.Padding = new Padding(targetPad);
                if (borderlessForm != null)
                {
                    int targetRad = isMax ? 0 : 20;
                    if (borderlessForm.BorderRadius != targetRad)
                        borderlessForm.BorderRadius = targetRad;
                }

                if (activeTabs.Count > 1 && !IsSplitModeCapable)
                {
                    CollapseToSingleMostRecentTab();
                }

                LayoutBottomNavButtons();

                if (pnlDrawerBackdrop != null && pnlMainContainer != null && pnlDrawerBackdrop.Visible)
                {
                    pnlDrawerBackdrop.Bounds = pnlMainContainer.Bounds;
                }
            };

            pnlHeader = new Guna2Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                FillColor = clrCardBg,
                UseTransparentBackground = true
            };
            pnlHeader.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorderLight });

            var logoBadge = MakeSoftIconBadge(UiIcon.Brand, 14, 13, 30, 10, clrSoftGreenBg, clrPrimaryGreen);
            lblBrand = new Label
            {
                Text = "PocketLog",
                Location = new Point(50, 15),
                AutoSize = true,
                UseMnemonic = false,
                Font = FontBold(11.2F),
                ForeColor = clrPrimaryGreen,
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(logoBadge);
            pnlHeader.Controls.Add(lblBrand);

            pnlHeader.MouseDown += BeginDragWindowFromHeader;
            logoBadge.MouseDown += BeginDragWindowFromHeader;
            lblBrand.MouseDown += BeginDragWindowFromHeader;

            // Chip tài khoản mềm mại thay cho ký tự 👤 bị cắt góc
            pnlHeaderUserChip = new Guna2Panel
            {
                Location = new Point(150, 11),
                Size = new Size(132, 34),
                BorderRadius = 17,
                FillColor = clrSoftGreenBg,
                Visible = false,
                BackColor = Color.Transparent,
                UseTransparentBackground = true
            };
            avtHeaderUser = new Guna2CircleButton
            {
                Text = "U",
                Size = new Size(26, 26),
                Location = new Point(4, 4),
                FillColor = clrPrimaryGreen,
                ForeColor = Color.White,
                Font = FontMedium(8F),
                UseTransparentBackground = true,
                BackColor = Color.Transparent,
                Enabled = false
            };
            lblHeaderUser = new Label
            {
                Text = "",
                Location = new Point(34, 4),
                Size = new Size(92, 26),
                Font = FontMedium(8.5F),
                ForeColor = clrPrimaryGreen,
                UseMnemonic = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            pnlHeaderUserChip.Controls.Add(avtHeaderUser);
            pnlHeaderUserChip.Controls.Add(lblHeaderUser);
            pnlHeader.Controls.Add(pnlHeaderUserChip);

            int logoutW = Math.Max(108, MeasurePillBtnWidth("Đăng xuất", 8.5F, 17));
            btnLogoutTop = MakeBtn("Đăng xuất", 268, 11, logoutW, 34, 17, clrAppBg, clrIndigo, Color.Transparent, 0, 8.5F);
            btnLogoutTop.HoverState.FillColor = clrSoftRedBg;
            btnLogoutTop.HoverState.ForeColor = clrRedDanger;
            btnLogoutTop.Visible = false;
            btnLogoutTop.Click += (s, e) => DisconnectAndLogout();
            pnlHeader.Controls.Add(btnLogoutTop);

            btnWinMin = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MinimizeBox,
                Size = new Size(32, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 9,
                UseTransparentBackground = true
            };
            btnWinMax = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MaximizeBox,
                Size = new Size(32, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 9,
                UseTransparentBackground = true
            };
            btnWinClose = new Guna2ControlBox
            {
                Size = new Size(32, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 9,
                UseTransparentBackground = true,
                HoverState = { FillColor = clrRedDanger, IconColor = Color.White }
            };

            EventHandler toggleMax = (s, e) =>
            {
                if (this.WindowState == FormWindowState.Normal)
                {
                    this.MaximizedBounds = Screen.FromControl(this).WorkingArea;
                    this.WindowState = FormWindowState.Maximized;
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                }
            };
            pnlHeader.Resize += (s, e) => LayoutHeaderControls();
            pnlHeader.DoubleClick += toggleMax;
            lblBrand.DoubleClick += toggleMax;

            pnlHeader.Controls.AddRange(new Control[] { btnWinMin, btnWinMax, btnWinClose });
            LayoutHeaderControls();

            // Thanh điều hướng đáy phong cách Android Material / Locket
            pnlBottomNav = new Guna2Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                FillColor = clrCardBg,
                Visible = false,
                UseTransparentBackground = true
            };
            pnlBottomNav.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorderLight });

            btnTabFeed = MakeBtn("Bảng tin", 14, 12, 114, 48, 24, Color.Transparent, clrTextMuted, Color.Transparent, 0, 9.2F);
            btnTabCamera = MakeBtn("Chụp ảnh", 136, 12, 114, 48, 24, Color.Transparent, clrTextMuted, Color.Transparent, 0, 9.2F);
            btnTabExpense = MakeBtn("Chi tiêu", 258, 12, 114, 48, 24, Color.Transparent, clrTextMuted, Color.Transparent, 0, 9.2F);

            btnTabFeed.Click += (s, e) => HandleBottomTabClick("FEED");
            btnTabCamera.Click += (s, e) => HandleBottomTabClick("CAMERA");
            btnTabExpense.Click += (s, e) => HandleBottomTabClick("EXPENSE");

            lblSplitModeHint = new Label
            {
                Text = "",
                AutoSize = false,
                UseMnemonic = false,
                AutoEllipsis = true,
                Font = FontMedium(8.2F),
                ForeColor = clrTextMuted,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Visible = false
            };

            pnlBottomNav.Resize += (s, e) => LayoutBottomNavButtons();
            pnlBottomNav.Controls.AddRange(new Control[] { lblSplitModeHint, btnTabFeed, btnTabCamera, btnTabExpense });

            pnlMainContainer = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = clrAppBg,
                UseTransparentBackground = true
            };

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottomNav);

            AttachEdgeResizeRedirector(pnlHeader);
            AttachEdgeResizeRedirector(pnlBottomNav);
            AttachEdgeResizeRedirector(pnlMainContainer);

            SetupFriendsDrawer();
            AttachEdgeResizeRedirector(pnlDrawerBackdrop);
            AttachEdgeResizeRedirector(pnlFriendsDrawer);
            LayoutBottomNavButtons();
        }

        private void LayoutHeaderControls()
        {
            if (pnlHeader == null || btnWinClose == null) return;
            int w = pnlHeader.ClientSize.Width;

            btnWinClose.Location = new Point(w - 42, 13);
            btnWinMax.Location = new Point(w - 78, 13);
            btnWinMin.Location = new Point(w - 114, 13);

            int logoutW = Math.Max(108, MeasurePillBtnWidth("Đăng xuất", 8.5F, 17));
            btnLogoutTop.Size = new Size(logoutW, 34);
            btnLogoutTop.Location = new Point(btnWinMin.Left - logoutW - 8, 11);

            bool hasUser = !string.IsNullOrWhiteSpace(lblHeaderUser?.Text);
            int minLeft = (lblBrand != null ? lblBrand.Right : 144) + 10;
            int availForChip = btnLogoutTop.Left - minLeft - 8;

            if (hasUser && availForChip >= 68)
            {
                int desiredTextW = TextRenderer.MeasureText(lblHeaderUser.Text, lblHeaderUser.Font).Width + 46;
                int chipW = Math.Clamp(desiredTextW, 68, Math.Min(210, availForChip));
                pnlHeaderUserChip.Location = new Point(btnLogoutTop.Left - chipW - 8, 11);
                pnlHeaderUserChip.Size = new Size(chipW, 34);
                lblHeaderUser.Width = Math.Max(28, chipW - 38);
                pnlHeaderUserChip.Visible = true;
            }
            else
            {
                pnlHeaderUserChip.Visible = false;
            }
        }

        private void LayoutBottomNavButtons()
        {
            if (pnlBottomNav == null || btnTabFeed == null) return;
            int totalW = pnlBottomNav.ClientSize.Width;
            int totalH = pnlBottomNav.ClientSize.Height;

            int navW = Math.Clamp(totalW - 20, 260, Math.Min(600, MaxColWidth));
            int startX = Math.Max(10, (totalW - navW) / 2);
            int gap = totalW < 430 ? 6 : 10;
            int tabW = (navW - gap * 2) / 3;
            int btnH = 48;
            int topY = (totalH - btnH) / 2;

            btnTabFeed.SetBounds(startX, topY, tabW, btnH);
            btnTabCamera.SetBounds(startX + tabW + gap, topY, tabW, btnH);
            btnTabExpense.SetBounds(startX + (tabW + gap) * 2, topY, navW - (tabW + gap) * 2, btnH);

            UpdateSplitHintLabel(totalW, totalH, startX);
        }

        private void UpdateSplitHintLabel(int totalW, int totalH, int navStartX)
        {
            if (lblSplitModeHint == null) return;
            int availLeftW = navStartX - 28;
            if (IsSplitModeCapable && availLeftW >= 210 && activeTabs.Count > 0)
            {
                bool dual = activeTabs.Count >= 2;
                lblSplitModeHint.Text = dual
                    ? "● Đang xem 2 mục • Bấm lại mục đang sáng để tắt bớt"
                    : "● Màn hình rộng: Bấm thêm 1 mục để mở song song";
                lblSplitModeHint.ForeColor = dual ? clrPrimaryGreen : clrTextMuted;
                lblSplitModeHint.SetBounds(18, (totalH - 28) / 2, availLeftW, 28);
                lblSplitModeHint.Visible = true;
            }
            else
            {
                lblSplitModeHint.Visible = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && this.WindowState == FormWindowState.Normal)
            {
                int lp = m.LParam.ToInt32();
                Point screenPt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
                int hit = GetResizeHitTest(screenPt);
                if (hit != 0)
                {
                    m.Result = (IntPtr)hit;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsTabActive("CAMERA") && keyData == (Keys.Control | Keys.V))
            {
                if (Clipboard.ContainsImage() || Clipboard.ContainsFileDropList())
                {
                    if (pasteImageToCameraAction != null)
                    {
                        pasteImageToCameraAction.Invoke();
                        return true;
                    }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UpdateBottomTabIcons()
        {
            var tabs = new (string Key, Guna2Button Btn, UiIcon Icon)[]
            {
                ("FEED", btnTabFeed, UiIcon.Feed),
                ("CAMERA", btnTabCamera, UiIcon.Camera),
                ("EXPENSE", btnTabExpense, UiIcon.Wallet)
            };

            foreach (var t in tabs)
            {
                bool active = activeTabs.Contains(t.Key);
                t.Btn.FillColor = active ? clrPrimaryGreen : Color.Transparent;
                t.Btn.ForeColor = active ? Color.White : clrIndigo;
                t.Btn.HoverState.FillColor = active ? clrPrimaryGreen : clrAppBg;
                t.Btn.BorderThickness = 0;
                t.Btn.BorderRadius = 22;
                AttachBtnIcon(t.Btn, t.Icon, active ? Color.White : clrIndigo, 16);
            }

            if (pnlBottomNav != null && btnTabFeed != null)
            {
                UpdateSplitHintLabel(pnlBottomNav.ClientSize.Width, pnlBottomNav.ClientSize.Height, btnTabFeed.Left);
            }
        }

        private Control CreateContentForTab(string tabKey)
        {
            return tabKey switch
            {
                "FEED" => isFeedShowingGallery ? BuildMyGalleryControl() : BuildFeedScreen(),
                "CAMERA" => BuildCameraScreen(),
                "EXPENSE" => BuildExpenseScreen(),
                _ => BuildFeedScreen()
            };
        }

        private Panel GetOrCreatePaneHost(string tabKey)
        {
            if (paneHosts.TryGetValue(tabKey, out Panel existing) && existing != null && !existing.IsDisposed)
            {
                return existing;
            }

            var host = new SmoothScrollPanel
            {
                Dock = DockStyle.Fill,
                BackColor = clrAppBg,
                Margin = Padding.Empty
            };
            AttachEdgeResizeRedirector(host);

            Control content = CreateContentForTab(tabKey);
            content.Dock = DockStyle.Fill;
            host.Controls.Add(content);

            paneHosts[tabKey] = host;
            return host;
        }

        private void DisposeRemovedPaneHosts()
        {
            var staleKeys = paneHosts.Keys.Where(k => !activeTabs.Contains(k)).ToList();
            foreach (string key in staleKeys)
            {
                if (key == "CAMERA") StopWebcam();
                if (paneHosts.TryGetValue(key, out Panel host))
                {
                    paneHosts.Remove(key);
                    try
                    {
                        host.Parent?.Controls.Remove(host);
                        host.Dispose();
                    }
                    catch { }
                }
            }
        }

        protected void ClearAllTabPanes()
        {
            StopWebcam();
            activeTabs.Clear();
            isFeedShowingGallery = false;
            foreach (var host in paneHosts.Values.ToList())
            {
                try
                {
                    host.Parent?.Controls.Remove(host);
                    host.Dispose();
                }
                catch { }
            }
            paneHosts.Clear();
            UpdateBottomTabIcons();
        }

        protected void RenderActiveTabLayout()
        {
            if (activeTabs.Count == 0) return;
            activeTabName = activeTabs[activeTabs.Count - 1];

            pnlMainContainer.SuspendLayout();
            try
            {
                // Tháo các paneHost đang giữ ra khỏi container cũ trước khi Dispose container cũ
                foreach (var keptHost in paneHosts.Values)
                {
                    try { keptHost.Parent?.Controls.Remove(keptHost); } catch { }
                }

                DisposeRemovedPaneHosts();

                var oldRootControls = pnlMainContainer.Controls.Cast<Control>().ToList();
                pnlMainContainer.Controls.Clear();
                foreach (var oldCtrl in oldRootControls)
                {
                    if (!paneHosts.Values.Contains(oldCtrl))
                    {
                        try { oldCtrl.Dispose(); } catch { }
                    }
                }

                if (activeTabs.Count == 1)
                {
                    Panel singleHost = GetOrCreatePaneHost(activeTabs[0]);
                    singleHost.Dock = DockStyle.Fill;
                    pnlMainContainer.Controls.Add(singleHost);
                }
                else
                {
                    var splitGrid = new BufferedTableLayout
                    {
                        Dock = DockStyle.Fill,
                        ColumnCount = 3,
                        RowCount = 1,
                        BackColor = clrAppBg,
                        Margin = Padding.Empty,
                        Padding = Padding.Empty
                    };
                    splitGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    splitGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
                    splitGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    splitGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                    AttachEdgeResizeRedirector(splitGrid);

                    Panel leftHost = GetOrCreatePaneHost(activeTabs[0]);
                    leftHost.Dock = DockStyle.Fill;

                    Panel divider = new Panel
                    {
                        Dock = DockStyle.Fill,
                        Margin = Padding.Empty,
                        BackColor = clrBorderLight
                    };

                    Panel rightHost = GetOrCreatePaneHost(activeTabs[1]);
                    rightHost.Dock = DockStyle.Fill;

                    splitGrid.Controls.Add(leftHost, 0, 0);
                    splitGrid.Controls.Add(divider, 1, 0);
                    splitGrid.Controls.Add(rightHost, 2, 0);

                    pnlMainContainer.Controls.Add(splitGrid);
                }

                UpdateBottomTabIcons();
            }
            finally
            {
                pnlMainContainer.ResumeLayout(true);
            }
        }

        protected void RefreshPaneContent(string tabKey)
        {
            if (!paneHosts.TryGetValue(tabKey, out Panel host) || host == null || host.IsDisposed)
                return;

            if (tabKey == "CAMERA") StopWebcam();

            host.SuspendLayout();
            try
            {
                var oldChildren = host.Controls.Cast<Control>().ToList();
                host.Controls.Clear();
                foreach (var oc in oldChildren)
                {
                    try { oc.Dispose(); } catch { }
                }

                Control fresh = CreateContentForTab(tabKey);
                fresh.Dock = DockStyle.Fill;
                host.Controls.Add(fresh);
            }
            finally
            {
                host.ResumeLayout(true);
            }
        }

        protected void RefreshDataDrivenPanesOnSync()
        {
            if (IsTabActive("FEED"))
                RefreshPaneContent("FEED");
            if (IsTabActive("EXPENSE"))
                RefreshPaneContent("EXPENSE");
            if (IsTabActive("CAMERA"))
                refreshCameraFriendsAction?.Invoke();
        }

        protected void OpenInitialAuthenticatedTab(string tabKey = "FEED")
        {
            ClearAllTabPanes();
            activeTabs.Add(tabKey);
            activeTabName = tabKey;
            RenderActiveTabLayout();
        }

        protected void HandleBottomTabClick(string clickedTab)
        {
            if (!IsSplitModeCapable)
            {
                if (activeTabs.Count == 1 && activeTabs[0] == clickedTab)
                {
                    if (clickedTab == "FEED" && isFeedShowingGallery)
                    {
                        isFeedShowingGallery = false;
                        RefreshPaneContent("FEED");
                    }
                    return;
                }

                activeTabs.Clear();
                activeTabs.Add(clickedTab);
                if (clickedTab == "FEED") isFeedShowingGallery = false;
                RenderActiveTabLayout();
                return;
            }

            // Chế độ Full Screen / Màn hình rộng: hỗ trợ bật song song 2 mục và bấm lại để tắt bớt
            if (activeTabs.Contains(clickedTab))
            {
                if (activeTabs.Count >= 2)
                {
                    activeTabs.Remove(clickedTab);
                    RenderActiveTabLayout();
                }
                else if (clickedTab == "FEED" && isFeedShowingGallery)
                {
                    isFeedShowingGallery = false;
                    RefreshPaneContent("FEED");
                }
                return;
            }

            if (activeTabs.Count >= 2)
            {
                activeTabs.RemoveAt(0);
            }
            if (clickedTab == "FEED") isFeedShowingGallery = false;
            activeTabs.Add(clickedTab);
            RenderActiveTabLayout();
        }

        protected void EnsureTabVisible(string tabKey)
        {
            if (activeTabs.Contains(tabKey))
            {
                activeTabName = tabKey;
                if (tabKey == "FEED" && isFeedShowingGallery)
                {
                    isFeedShowingGallery = false;
                    RefreshPaneContent("FEED");
                }
                UpdateBottomTabIcons();
                return;
            }
            HandleBottomTabClick(tabKey);
        }

        private void CollapseToSingleMostRecentTab()
        {
            if (activeTabs.Count <= 1) return;
            string keep = activeTabs[activeTabs.Count - 1];
            activeTabs.Clear();
            activeTabs.Add(keep);
            RenderActiveTabLayout();
        }

        protected void SetMainScreen(Control screenContent)
        {
            screenContent.Dock = DockStyle.Fill;
            pnlMainContainer.SuspendLayout();
            var oldControls = pnlMainContainer.Controls.Cast<Control>().ToList();
            pnlMainContainer.Controls.Clear();
            foreach (var oldCtrl in oldControls)
            {
                try { oldCtrl.Dispose(); } catch { }
            }
            pnlMainContainer.Controls.Add(screenContent);
            pnlMainContainer.ResumeLayout(true);
        }
    }
}