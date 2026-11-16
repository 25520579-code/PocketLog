using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

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

        // Quản lý Socket TCP
        private TcpClient tcpClient;
        private NetworkStream netStream;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private bool isConnected = false;

        private string currentServerIp = "127.0.0.1";
        private int currentServerPort = 8888;
        private int currentUserId = 0;
        private string currentUsername = "";
        private string currentFullName = "";

        // Bảng màu chủ đạo: Xanh dương nhạt (Sky Blue) & Xám lạnh (Slate Grey)
        private readonly Color clrAppBg = Color.FromArgb(241, 245, 249);
        private readonly Color clrCardBg = Color.FromArgb(255, 255, 255);
        private readonly Color clrPrimaryGreen = Color.FromArgb(56, 130, 246);
        private readonly Color clrSoftGreenBg = Color.FromArgb(224, 242, 254);
        private readonly Color clrSoftGreenBorder = Color.FromArgb(147, 197, 253);
        private readonly Color clrIndigo = Color.FromArgb(71, 85, 105);
        private readonly Color clrSoftIndigo = Color.FromArgb(226, 232, 240);
        private readonly Color clrRedDanger = Color.FromArgb(225, 29, 72);
        private readonly Color clrBadgeOrangeBg = Color.FromArgb(226, 232, 240);
        private readonly Color clrBadgeOrangeTxt = Color.FromArgb(51, 65, 85);
        private readonly Color clrBorderLight = Color.FromArgb(203, 213, 225);
        private readonly Color clrTextMain = Color.FromArgb(30, 41, 59);
        private readonly Color clrTextMuted = Color.FromArgb(100, 116, 139);

        private readonly (string Key, string ShortLabel, string Emoji)[] standardCategories = new[]
        {
            ("🍜 Đồ ăn/Thức uống", "🍜 Ăn uống", "🍜"),
            ("⛽ Xăng cộ", "⛽ Xăng cộ", "⛽"),
            ("👕 Quần áo", "👕 Quần áo", "👕"),
            ("🏠 Tiền trọ", "🏠 Tiền trọ", "🏠"),
            ("🛒 Mua sắm", "🛒 Mua sắm", "🛒"),
            ("📚 Học tập/Khác", "📚 Học tập", "📚")
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

        private class ControlSnapshot
        {
            public Rectangle Bounds { get; set; }
            public float FontSize { get; set; }
            public int BorderRadius { get; set; }
        }

        private readonly Dictionary<Control, ControlSnapshot> designSnapshots = new Dictionary<Control, ControlSnapshot>();
        private const int BaseDesignWidth = 420;
        private const int BaseDesignHeight = 694;

        public Client()
        {
            this.DoubleBuffered = true;
            InitializeDefaultBudgets();
            SetupPhoneShell();
            ShowAuthScreen();
            this.FormClosing += (s, e) =>
            {
                StopWebcam();
                CloseCurrentSocket();
            };
        }

        private void InitializeDefaultBudgets()
        {
            categoryBudgets["🍜 Đồ ăn/Thức uống"] = 1200000;
            categoryBudgets["⛽ Xăng cộ"] = 300000;
            categoryBudgets["👕 Quần áo"] = 500000;
            categoryBudgets["🏠 Tiền trọ"] = 1500000;
            categoryBudgets["🛒 Mua sắm"] = 400000;
            categoryBudgets["📚 Học tập/Khác"] = 300000;
        }

        private Label MakeLabel(string text, int x, int y, float size, FontStyle style, Color color, int maxWidth = 0)
        {
            var lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };

            if (maxWidth > 0)
            {
                lbl.AutoSize = false;
                lbl.Size = new Size(maxWidth, (int)(size * 2.4f));
                lbl.AutoEllipsis = true;
            }
            else
            {
                lbl.AutoSize = true;
            }
            return lbl;
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
                TextOffset = Point.Empty,
                Padding = Padding.Empty,
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                AutoRoundedCorners = false,
                Cursor = Cursors.Hand
            };
        }

        private void SetupPhoneShell()
        {
            this.Controls.Clear();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(520, 860);
            this.MinimumSize = new Size(420, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrBorderLight;
            this.Padding = new Padding(4);

            borderlessForm = new Guna2BorderlessForm
            {
                ContainerControl = this,
                BorderRadius = 22,
                ResizeForm = true,
                HasFormShadow = true
            };

            pnlHeader = new Guna2Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                FillColor = clrCardBg,
                UseTransparentBackground = true
            };
            pnlHeader.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorderLight });
            pnlHeader.Controls.Add(MakeLabel("● PocketLog", 16, 16, 11.5F, FontStyle.Bold, clrPrimaryGreen));

            lblHeaderUser = new Label
            {
                Text = "",
                Location = new Point(132, 18),
                Size = new Size(140, 24),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = clrIndigo,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };
            pnlHeader.Controls.Add(lblHeaderUser);

            btnLogoutTop = MakeBtn("Đăng xuất", 254, 12, 108, 32, 10, clrSoftIndigo, clrTextMain, clrBorderLight, 1, 7.8F);
            btnLogoutTop.Visible = false;
            btnLogoutTop.Click += (s, e) => DisconnectAndLogout();
            pnlHeader.Controls.Add(btnLogoutTop);

            Guna2ControlBox btnMin = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MinimizeBox,
                Size = new Size(34, 32),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8,
                UseTransparentBackground = true
            };
            Guna2ControlBox btnMax = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MaximizeBox,
                Size = new Size(34, 32),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8,
                UseTransparentBackground = true
            };
            Guna2ControlBox btnClose = new Guna2ControlBox
            {
                Size = new Size(34, 32),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8,
                UseTransparentBackground = true,
                HoverState = { FillColor = clrRedDanger, IconColor = Color.White }
            };

            Action layoutHeaderControls = () =>
            {
                int w = pnlHeader.ClientSize.Width;
                btnClose.Location = new Point(w - 44, 12);
                btnMax.Location = new Point(w - 82, 12);
                btnMin.Location = new Point(w - 120, 12);
                btnLogoutTop.Location = new Point(w - 236, 12);
                lblHeaderUser.Width = Math.Max(80, btnLogoutTop.Left - lblHeaderUser.Left - 8);
            };
            pnlHeader.Resize += (s, e) => layoutHeaderControls();

            pnlHeader.DoubleClick += (s, e) =>
            {
                this.WindowState = this.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };

            pnlHeader.Controls.AddRange(new Control[] { btnMin, btnMax, btnClose });
            layoutHeaderControls();
            dragControl = new Guna2DragControl { TargetControl = pnlHeader };

            pnlBottomNav = new Guna2Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                FillColor = clrCardBg,
                Visible = false,
                UseTransparentBackground = true
            };
            pnlBottomNav.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorderLight });

            btnTabFeed = MakeBtn("Bảng tin", 14, 13, 114, 46, 20, clrAppBg, clrTextMain, clrBorderLight, 1, 9F);
            btnTabCamera = MakeBtn("Chụp ảnh", 136, 13, 114, 46, 20, clrAppBg, clrTextMain, clrBorderLight, 1, 9F);
            btnTabExpense = MakeBtn("Quản lý chi tiêu", 258, 13, 148, 46, 20, clrAppBg, clrTextMain, clrBorderLight, 1, 9F);

            btnTabFeed.Click += (s, e) => { activeTabName = "FEED"; ActivateMenu(btnTabFeed, BuildFeedScreen()); };
            btnTabCamera.Click += (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
            btnTabExpense.Click += (s, e) => { activeTabName = "EXPENSE"; ActivateMenu(btnTabExpense, BuildExpenseScreen()); };

            pnlBottomNav.Resize += (s, e) => LayoutBottomNavButtons();
            pnlBottomNav.Controls.AddRange(new Control[] { btnTabFeed, btnTabCamera, btnTabExpense });

            pnlMainContainer = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = clrAppBg,
                UseTransparentBackground = true
            };

            pnlMainContainer.ControlAdded += (s, e) =>
            {
                if (e.Control != null)
                {
                    NormalizeTransparentBackgrounds(e.Control);
                    CaptureScreenSnapshots(e.Control);
                    ApplyResponsiveToCurrentScreen();
                }
            };

            pnlMainContainer.Resize += (s, e) => ApplyResponsiveToCurrentScreen();

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottomNav);

            SetupFriendsDrawer();
            LayoutBottomNavButtons();
        }

        private void NormalizeTransparentBackgrounds(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Guna2Panel gp)
                {
                    gp.UseTransparentBackground = true;
                    gp.BackColor = Color.Transparent;
                }
                else if (c is Guna2Button gb)
                {
                    gb.UseTransparentBackground = true;
                    gb.BackColor = Color.Transparent;
                }
                else if (c is Guna2CircleButton gcb)
                {
                    gcb.UseTransparentBackground = true;
                    gcb.BackColor = Color.Transparent;
                }
                else if (c is Guna2TextBox gtb)
                {
                    // Guna2TextBox không có UseTransparentBackground nên đồng bộ màu nền với thẻ chứa nó
                    if (c.Parent is Guna2Panel parentPanel && parentPanel.FillColor != Color.Transparent)
                        gtb.BackColor = parentPanel.FillColor;
                    else
                        gtb.BackColor = clrCardBg;
                }
                else if (c is Guna2ProgressBar gpb)
                {
                    gpb.UseTransparentBackground = true;
                    gpb.BackColor = Color.Transparent;
                }
                else if (c is Guna2PictureBox gpic)
                {
                    gpic.UseTransparentBackground = true;
                    gpic.BackColor = Color.Transparent;
                }

                if (c.HasChildren)
                {
                    NormalizeTransparentBackgrounds(c);
                }
            }
        }

        private void LayoutBottomNavButtons()
        {
            if (pnlBottomNav == null || btnTabFeed == null) return;
            int totalW = pnlBottomNav.ClientSize.Width;
            int totalH = pnlBottomNav.ClientSize.Height;
            int pad = 16;
            int gap = 12;
            int availW = Math.Max(240, totalW - pad * 2 - gap * 2);

            int w1 = (int)(availW * 0.30f);
            int w2 = (int)(availW * 0.30f);
            int w3 = availW - w1 - w2;
            int btnH = Math.Max(42, totalH - 24);
            int topY = (totalH - btnH) / 2;

            btnTabFeed.SetBounds(pad, topY, w1, btnH);
            btnTabCamera.SetBounds(pad + w1 + gap, topY, w2, btnH);
            btnTabExpense.SetBounds(pad + w1 + gap + w2 + gap, topY, w3, btnH);

            int rad = Math.Min(20, btnH / 2);
            btnTabFeed.BorderRadius = rad;
            btnTabCamera.BorderRadius = rad;
            btnTabExpense.BorderRadius = rad;
        }

        private void CaptureScreenSnapshots(Control screenRoot)
        {
            designSnapshots.Clear();
            CaptureRecursive(screenRoot);
        }

        private void CaptureRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (!designSnapshots.ContainsKey(c))
                {
                    designSnapshots[c] = new ControlSnapshot
                    {
                        Bounds = c.Bounds,
                        FontSize = c.Font != null ? c.Font.Size : 9F,
                        BorderRadius = GetControlBorderRadius(c)
                    };
                }
                if (c.HasChildren)
                {
                    CaptureRecursive(c);
                }
            }
        }

        private void ApplyResponsiveToCurrentScreen()
        {
            if (pnlMainContainer == null || pnlMainContainer.Controls.Count == 0) return;
            Control screenRoot = pnlMainContainer.Controls[0];
            if (screenRoot.Width <= 0 || screenRoot.Height <= 0) return;

            Panel scrollPanel = screenRoot as Panel;
            Point savedScroll = Point.Empty;
            bool isAutoScroll = scrollPanel != null && scrollPanel.AutoScroll;

            if (isAutoScroll)
            {
                savedScroll = scrollPanel.AutoScrollPosition;
                scrollPanel.AutoScrollPosition = Point.Empty;
            }

            int scrollReserve = isAutoScroll ? SystemInformation.VerticalScrollBarWidth + 4 : 0;
            int targetW = Math.Max(380, pnlMainContainer.ClientSize.Width - scrollReserve);
            int targetH = Math.Max(520, pnlMainContainer.ClientSize.Height);

            float scaleX = (float)targetW / BaseDesignWidth;
            float rawScaleY = (float)targetH / BaseDesignHeight;

            float scaleY = isAutoScroll
                ? Math.Max(1.0f, 1.0f + (scaleX - 1.0f) * 0.55f)
                : Math.Max(1.0f, rawScaleY);

            float fontScale = Math.Clamp(Math.Min(scaleX, scaleY) * 0.96f, 0.9f, 1.55f);

            screenRoot.SuspendLayout();
            try
            {
                ScaleChildrenRecursive(screenRoot, scaleX, scaleY, fontScale);
            }
            finally
            {
                screenRoot.ResumeLayout(true);
                if (isAutoScroll)
                {
                    scrollPanel.AutoScrollPosition = new Point(-savedScroll.X, -savedScroll.Y);
                }
            }
        }

        private void ScaleChildrenRecursive(Control parent, float scaleX, float scaleY, float fontScale)
        {
            bool parentIsFlow = parent is FlowLayoutPanel;

            foreach (Control c in parent.Controls)
            {
                ControlSnapshot snap;
                if (!designSnapshots.TryGetValue(c, out snap))
                {
                    snap = new ControlSnapshot
                    {
                        Bounds = c.Bounds,
                        FontSize = c.Font != null ? c.Font.Size : 9F,
                        BorderRadius = GetControlBorderRadius(c)
                    };
                    designSnapshots[c] = snap;
                }

                if (c is Guna2CircleButton)
                {
                    float uniform = Math.Min(scaleX, scaleY);
                    int d = Math.Max(26, (int)Math.Round(snap.Bounds.Width * uniform));
                    int nx = (int)Math.Round(snap.Bounds.X * scaleX);
                    int ny = (int)Math.Round(snap.Bounds.Y * scaleY);
                    c.SetBounds(nx, ny, d, d);
                }
                else if (c is Label lbl && lbl.AutoSize && c.Dock == DockStyle.None)
                {
                    int nx = (int)Math.Round(snap.Bounds.X * scaleX);
                    int ny = (int)Math.Round(snap.Bounds.Y * scaleY);
                    c.Location = new Point(nx, ny);
                }
                else if (c.Dock == DockStyle.None)
                {
                    int nx = (int)Math.Round(snap.Bounds.X * scaleX);
                    int ny = (int)Math.Round(snap.Bounds.Y * scaleY);
                    int nw = Math.Max(16, (int)Math.Round(snap.Bounds.Width * scaleX));
                    int nh = Math.Max(16, (int)Math.Round(snap.Bounds.Height * scaleY));

                    if (parentIsFlow)
                        c.Size = new Size(nw, nh);
                    else
                        c.SetBounds(nx, ny, nw, nh);
                }

                if (c.Font != null)
                {
                    float newFontSz = Math.Max(7.8F, snap.FontSize * fontScale);
                    if (Math.Abs(c.Font.Size - newFontSz) > 0.2F)
                    {
                        c.Font = new Font(c.Font.FontFamily, newFontSz, c.Font.Style);
                    }
                }

                if (snap.BorderRadius > 0)
                {
                    int newRadius = Math.Max(6, (int)Math.Round(snap.BorderRadius * Math.Min(scaleX, scaleY)));
                    SetControlBorderRadius(c, newRadius);
                }

                if (c.HasChildren)
                {
                    ScaleChildrenRecursive(c, scaleX, scaleY, fontScale);
                }
            }
        }

        private int GetControlBorderRadius(Control c)
        {
            if (c is Guna2Panel p) return p.BorderRadius;
            if (c is Guna2Button b) return b.BorderRadius;
            if (c is Guna2TextBox t) return t.BorderRadius;
            if (c is Guna2ProgressBar prg) return prg.BorderRadius;
            if (c is Guna2PictureBox pic) return pic.BorderRadius;
            return 0;
        }

        private void SetControlBorderRadius(Control c, int radius)
        {
            if (c is Guna2Panel p) p.BorderRadius = radius;
            else if (c is Guna2Button b) b.BorderRadius = radius;
            else if (c is Guna2TextBox t) t.BorderRadius = radius;
            else if (c is Guna2ProgressBar prg) prg.BorderRadius = radius;
            else if (c is Guna2PictureBox pic) pic.BorderRadius = radius;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int WM_NCHITTEST = 0x84;
            const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
                      HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST && this.WindowState == FormWindowState.Normal)
            {
                Point pt = this.PointToClient(Cursor.Position);
                int grip = 8;
                int w = this.ClientSize.Width;
                int h = this.ClientSize.Height;

                if (pt.X <= grip && pt.Y <= grip) m.Result = (IntPtr)HTTOPLEFT;
                else if (pt.X >= w - grip && pt.Y <= grip) m.Result = (IntPtr)HTTOPRIGHT;
                else if (pt.X <= grip && pt.Y >= h - grip) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (pt.X >= w - grip && pt.Y >= h - grip) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (pt.X <= grip) m.Result = (IntPtr)HTLEFT;
                else if (pt.X >= w - grip) m.Result = (IntPtr)HTRIGHT;
                else if (pt.Y <= grip) m.Result = (IntPtr)HTTOP;
                else if (pt.Y >= h - grip) m.Result = (IntPtr)HTBOTTOM;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Nếu đang ở tab Chụp ảnh và nhấn Ctrl + V (khiClipboard đang chứa ảnh)
            if (activeTabName == "CAMERA" && keyData == (Keys.Control | Keys.V))
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


        private void ActivateMenu(Guna2Button selectedBtn, Control screenContent)
        {
            if (selectedBtn != btnTabCamera)
            {
                StopWebcam();
            }

            foreach (var btn in new[] { btnTabFeed, btnTabCamera, btnTabExpense })
            {
                btn.FillColor = clrAppBg;
                btn.ForeColor = clrTextMain;
                btn.BorderColor = clrBorderLight;
                btn.BorderThickness = 1;
            }
            selectedBtn.FillColor = clrPrimaryGreen;
            selectedBtn.ForeColor = Color.White;
            selectedBtn.BorderColor = clrPrimaryGreen;
            selectedBtn.BorderThickness = 1;

            screenContent.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(screenContent);
        }
    }
}