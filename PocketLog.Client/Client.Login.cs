using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        private sealed class HeroBannerPanel : Panel
        {
            private readonly Client owner;
            private Rectangle badgeBounds;
            private Rectangle cloudChipBounds;

            public Rectangle InnerHeroBounds => new Rectangle(
                4,
                2,
                Math.Max(4, ClientSize.Width - 8),
                Math.Max(4, ClientSize.Height - 8));

            public HeroBannerPanel(Client owner)
            {
                this.owner = owner;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                DoubleBuffered = true;
                BackColor = owner != null ? owner.clrAppBg : Color.White;
            }

            public void SetAccentRegions(Rectangle brandBadgeRect, Rectangle cloudRect)
            {
                badgeBounds = brandBadgeRect;
                cloudChipBounds = cloudRect;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 10 || Height <= 10) return;
                Graphics g = e.Graphics;
                Rectangle inner = InnerHeroBounds;

                PaintElevatedCardSurface(
                    g,
                    inner,
                    26,
                    Color.Transparent,
                    Color.Transparent,
                    0,
                    elevationLevel: 2,
                    isDarkMode: owner.isDarkMode);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                AppPalette p = owner.CurrentTheme;
                var rect = new RectangleF(inner.X + 0.5f, inner.Y + 0.5f, Math.Max(2f, inner.Width - 1f), Math.Max(2f, inner.Height - 1f));
                using GraphicsPath path = CreateRoundedPath(rect.X, rect.Y, rect.Width, rect.Height, 26f);

                using (var grad = new LinearGradientBrush(rect, p.HeroGradStart, p.HeroGradEnd, 28f))
                {
                    grad.InterpolationColors = new ColorBlend
                    {
                        Colors = new[] { p.HeroGradStart, p.HeroGradMid, p.HeroGradEnd },
                        Positions = new[] { 0f, 0.52f, 1f }
                    };
                    g.FillPath(grad, path);
                }

                GraphicsState gState = g.Save();
                try
                {
                    g.SetClip(path, CombineMode.Intersect);
                    using (var glow1 = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
                    using (var glow2 = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
                    {
                        g.FillEllipse(glow1, inner.Right - 118, inner.Top - 36, 150, 150);
                        g.FillEllipse(glow2, inner.Left - 26, inner.Bottom - 58, 120, 120);
                    }

                    if (badgeBounds.Width > 8 && badgeBounds.Height > 8)
                    {
                        using GraphicsPath badgePath = CreateRoundedPath(badgeBounds.X, badgeBounds.Y, badgeBounds.Width, badgeBounds.Height, 16f);
                        using var badgeFill = new SolidBrush(Color.FromArgb(46, 255, 255, 255));
                        using var badgeBorder = new Pen(Color.FromArgb(85, 255, 255, 255), 1f);
                        g.FillPath(badgeFill, badgePath);
                        g.DrawPath(badgeBorder, badgePath);

                        int icoSz = Math.Max(18, (int)(badgeBounds.Width * 0.52f));
                        using Bitmap brandIco = owner.CreateVectorIcon(UiIcon.Brand, Color.White, icoSz);
                        g.DrawImage(
                            brandIco,
                            badgeBounds.X + (badgeBounds.Width - icoSz) / 2,
                            badgeBounds.Y + (badgeBounds.Height - icoSz) / 2,
                            icoSz,
                            icoSz);
                    }

                    if (cloudChipBounds.Width > 16 && cloudChipBounds.Height > 8)
                    {
                        float r = cloudChipBounds.Height / 2f;
                        using GraphicsPath chipPath = CreateRoundedPath(cloudChipBounds.X, cloudChipBounds.Y, cloudChipBounds.Width, cloudChipBounds.Height, r);
                        using var chipFill = new SolidBrush(Color.FromArgb(42, 15, 23, 42));
                        using var chipBorder = new Pen(Color.FromArgb(80, 255, 255, 255), 1f);
                        g.FillPath(chipFill, chipPath);
                        g.DrawPath(chipBorder, chipPath);
                    }

                    float topBandH = Math.Min(inner.Height * 0.48f, 34f);
                    g.SetClip(new RectangleF(rect.X, rect.Y, rect.Width, topBandH), CombineMode.Intersect);
                    using (GraphicsPath innerSheenPath = CreateRoundedPath(rect.X + 1f, rect.Y + 1f, Math.Max(2f, rect.Width - 2f), Math.Max(2f, rect.Height - 2f), 25f))
                    using (var topSheenBrush = new LinearGradientBrush(
                        new PointF(rect.X, rect.Y + 1f),
                        new PointF(rect.X, rect.Y + 1f + topBandH),
                        Color.FromArgb(owner.isDarkMode ? 76 : 96, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)))
                    using (var topSheenPen = new Pen(topSheenBrush, 1f))
                    {
                        g.DrawPath(topSheenPen, innerSheenPath);
                    }
                }
                finally
                {
                    g.Restore(gState);
                }

                using (var sheenPen = new Pen(Color.FromArgb(owner.isDarkMode ? 44 : 52, 255, 255, 255), 1f))
                {
                    g.DrawPath(sheenPen, path);
                }
            }
        }

        private void ShowLoginScreen() => ShowAuthScreen();

        private void ShowAuthScreen(string initialNotice = "")
        {
            ClearAllTabPanes();
            activeTabName = "AUTH";
            pnlBottomNav.Visible = false;
            pnlDrawerBackdrop.Visible = false;
            UpdateHeaderUserUI("");

            AppPalette theme = CurrentTheme;
            Color accentText = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 14;
            bool isRegisterMode = false;
            bool isPasswordShown = false;

            var savedEp = LoadSavedServerEndpoint();
            string cloudChipText = $"Cloud: {savedEp.Host}:{savedEp.Port}";

            var pnlHero = new HeroBannerPanel(this)
            {
                Location = new Point(0, y),
                Size = new Size(w, 126),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AttachEdgeResizeRedirector(pnlHero);

            Rectangle heroInner = pnlHero.InnerHeroBounds;
            var lblHeroTitle = MakeLabel("PocketLog", heroInner.X + 76, heroInner.Y + 20, 13.5F, FontStyle.Bold, Color.White, 140);
            SetControlFont(lblHeroTitle, FontBold(13.5F));
            lblHeroTitle.Height = TextRenderer.MeasureText("Ág", lblHeroTitle.Font).Height + 4;

            var lblCloudChip = new Label
            {
                Text = cloudChipText,
                Font = FontMedium(7.6F),
                ForeColor = Color.FromArgb(241, 245, 249),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                UseMnemonic = false,
                UseCompatibleTextRendering = false
            };

            var lblHeroSub = MakeLabel(
                "Chia sẻ khoảnh khắc • Chia bill sòng phẳng",
                heroInner.X + 76,
                heroInner.Y + 56,
                9.0F,
                FontStyle.Regular,
                Color.FromArgb(236, 242, 255),
                Math.Max(140, heroInner.Width - 92),
                lines: 2);
            lblHeroSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            void LayoutHeroHeader()
            {
                if (pnlHero.IsDisposed) return;
                Rectangle inner = pnlHero.InnerHeroBounds;
                var badgeRect = new Rectangle(inner.X + 16, inner.Y + 33, 48, 48);

                int chipTextW = TextRenderer.MeasureText(
                    lblCloudChip.Text,
                    lblCloudChip.Font,
                    new Size(600, 40),
                    TextFormatFlags.SingleLine).Width;
                int maxChipW = Math.Max(92, inner.Width - 206);
                int chipW = Math.Clamp(chipTextW + 18, 92, maxChipW);
                var chipRect = new Rectangle(inner.Right - chipW - 14, inner.Y + 20, chipW, 24);

                lblCloudChip.Bounds = new Rectangle(chipRect.X + 4, chipRect.Y, Math.Max(16, chipRect.Width - 8), chipRect.Height);
                lblHeroTitle.Width = Math.Max(110, chipRect.Left - (inner.X + 76) - 6);
                lblHeroSub.Width = Math.Max(140, inner.Width - 92);

                pnlHero.SetAccentRegions(badgeRect, chipRect);
            }

            pnlHero.Controls.Add(lblHeroTitle);
            pnlHero.Controls.Add(lblCloudChip);
            pnlHero.Controls.Add(lblHeroSub);
            pnlHero.Resize += (s, e) => LayoutHeroHeader();
            LayoutHeroHeader();

            col.Controls.Add(pnlHero);
            y += 134;

            var cardServer = new ElevatedCardPanel(this)
            {
                Name = "cardServer",
                Location = new Point(0, y),
                Size = new Size(w, 142),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CornerRadius = 24,
                ElevationLevel = 2,
                CardFillColor = clrCardBg,
                CardBorderColor = clrBorderLight,
                CardBorderThickness = 1,
                BackColor = clrAppBg
            };

            Rectangle srvInner = cardServer.InnerCardBounds;
            int discBtnW = Math.Max(144, MeasurePillBtnWidth("Dò Server LAN", 8.5F, 17));
            var btnDiscoverServer = MakeBtn("Dò Server LAN", srvInner.Right - discBtnW - 16, srvInner.Y + 14, discBtnW, 34, 17, clrSoftGreenBg, accentText, clrSoftGreenBorder, 1, 8.5F);
            btnDiscoverServer.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDiscoverServer.HoverState.FillColor = clrPrimaryGreen;
            btnDiscoverServer.HoverState.ForeColor = Color.White;
            btnDiscoverServer.HoverState.BorderColor = clrPrimaryGreen;

            var lblSrvTitle = MakeLabel("Máy chủ kết nối", srvInner.X + 18, srvInner.Y + 17, 10F, FontStyle.Bold, accentText, Math.Max(100, btnDiscoverServer.Left - (srvInner.X + 18) - 8));
            lblSrvTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var lblSrvSub = MakeLabel("Nhập IP/Domain hoặc bấm Dò Server LAN (UDP 8889)", srvInner.X + 18, srvInner.Y + 49, 8.4F, FontStyle.Regular, clrTextMuted, Math.Max(120, srvInner.Width - 36));
            lblSrvSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            cardServer.Controls.Add(lblSrvTitle);
            cardServer.Controls.Add(lblSrvSub);
            cardServer.Controls.Add(btnDiscoverServer);

            var gridServer = new BufferedTableLayout
            {
                Location = new Point(srvInner.X + 16, srvInner.Y + 78),
                Size = new Size(Math.Max(120, srvInner.Width - 32), 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            gridServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67F));
            gridServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            gridServer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtServerIp = MakeTextBox(savedEp.Host, "Thêm địa chỉ IP / Domain", 0, 0, 100, 42, clrCardBg, true);
            txtServerIp.Name = "txtServerIp";
            txtServerIp.BorderRadius = 14;
            txtServerIp.Dock = DockStyle.Fill;
            txtServerIp.Margin = new Padding(2, 0, 6, 0);

            var txtServerPort = MakeTextBox(savedEp.Port, "Thêm Port", 0, 0, 80, 42, clrCardBg, true);
            txtServerPort.Name = "txtServerPort";
            txtServerPort.BorderRadius = 14;
            txtServerPort.Dock = DockStyle.Fill;
            txtServerPort.Margin = new Padding(4, 0, 2, 0);
            txtServerPort.ForeColor = clrTextMain;

            gridServer.Controls.Add(txtServerIp, 0, 0);
            gridServer.Controls.Add(txtServerPort, 1, 0);
            cardServer.Controls.Add(gridServer);
            col.Controls.Add(cardServer);
            y += 150;

            var cardAuth = new ElevatedCardPanel(this)
            {
                Name = "cardAuth",
                Location = new Point(0, y),
                Size = new Size(w, 390),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CornerRadius = 24,
                ElevationLevel = 2,
                CardFillColor = clrCardBg,
                CardBorderColor = clrBorderLight,
                CardBorderThickness = 1,
                BackColor = clrAppBg
            };

            Rectangle authInner = cardAuth.InnerCardBounds;
            int authPadX = authInner.X + 18;
            int authTopY = authInner.Y;
            int authFieldW = Math.Max(120, authInner.Width - 36);

            Guna2Panel segTrack = new Guna2Panel
            {
                Location = new Point(authPadX, authTopY + 18),
                Size = new Size(authFieldW, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 16,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            var gridSeg = new BufferedTableLayout
            {
                Location = new Point(3, 3),
                Size = new Size(segTrack.Width - 6, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            gridSeg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridSeg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridSeg.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var btnModeLogin = MakeBtn("Đăng nhập", 0, 0, 100, 38, 13, clrPrimaryGreen, Color.White, Color.Transparent, 0, 9.5F);
            btnModeLogin.Dock = DockStyle.Fill;
            btnModeLogin.Margin = new Padding(1);

            var btnModeReg = MakeBtn("Tạo tài khoản", 0, 0, 100, 38, 13, Color.Transparent, clrIndigo, Color.Transparent, 0, 9.5F);
            btnModeReg.Dock = DockStyle.Fill;
            btnModeReg.Margin = new Padding(1);

            gridSeg.Controls.Add(btnModeLogin, 0, 0);
            gridSeg.Controls.Add(btnModeReg, 1, 0);
            segTrack.Controls.Add(gridSeg);

            Label lblUserTitle = MakeLabel("Tên tài khoản hoặc họ tên hiển thị", authPadX, authTopY + 78, 9.5F, FontStyle.Bold, clrTextMain, authFieldW);
            lblUserTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var txtUsername = MakeTextBox("", "Nhập tên tài khoản...", authPadX, authTopY + 106, authFieldW, 44, clrCardBg);
            txtUsername.Name = "txtUsername";
            txtUsername.BorderRadius = 14;
            txtUsername.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblPassTitle = MakeLabel("Mật khẩu", authPadX, authTopY + 162, 9.5F, FontStyle.Bold, clrTextMain, authFieldW);
            lblPassTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var passRow = new BufferedTableLayout
            {
                Location = new Point(authPadX, authTopY + 190),
                Size = new Size(authFieldW, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            passRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            passRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
            passRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtPassword = MakeTextBox("", "Nhập mật khẩu...", 0, 0, 100, 44, clrCardBg);
            txtPassword.Name = "txtPassword";
            txtPassword.BorderRadius = 14;
            txtPassword.PasswordChar = '●';
            txtPassword.Dock = DockStyle.Fill;
            txtPassword.Margin = new Padding(0, 0, 6, 0);

            var btnTogglePass = MakeBtn("", 0, 0, 44, 44, 14, clrInputBg, clrIndigo, clrBorderLight, 1, 9F);
            btnTogglePass.Name = "btnTogglePass";
            btnTogglePass.Dock = DockStyle.Fill;
            btnTogglePass.Margin = Padding.Empty;
            btnTogglePass.Image = CreateVectorIcon(UiIcon.Eye, clrIndigo, 18);
            btnTogglePass.ImageSize = new Size(18, 18);
            btnTogglePass.ImageAlign = HorizontalAlignment.Center;
            btnTogglePass.ImageOffset = Point.Empty;
            btnTogglePass.HoverState.FillColor = clrSoftGreenBg;
            btnTogglePass.HoverState.BorderColor = clrSoftGreenBorder;
            AttachControlImageDisposal(btnTogglePass);
            uiToolTip.SetToolTip(btnTogglePass, "Hiện mật khẩu");

            passRow.Controls.Add(txtPassword, 0, 0);
            passRow.Controls.Add(btnTogglePass, 1, 0);

            Label lblInitBudget = MakeLabel("Hạn mức chi tiêu tháng (VNĐ)", authPadX, authTopY + 246, 9.5F, FontStyle.Bold, accentText, authFieldW);
            lblInitBudget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            var txtInitBudget = MakeTextBox("3500000", "Ví dụ: 3500000", authPadX, authTopY + 274, authFieldW, 42, clrCardBg, true);
            txtInitBudget.Name = "txtInitBudget";
            txtInitBudget.BorderRadius = 14;
            txtInitBudget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblInitBudget.Visible = false;
            txtInitBudget.Visible = false;

            string defaultHint = string.IsNullOrEmpty(initialNotice)
                ? "Nhập tài khoản đã có để đăng nhập vào hệ thống."
                : initialNotice;

            Label lblAuthStatus = MakeLabel(
                defaultHint, authPadX, authTopY + 248, 8.8F, FontStyle.Regular,
                string.IsNullOrEmpty(initialNotice) ? clrTextMuted : clrRedDanger,
                authFieldW, lines: 2);
            lblAuthStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var btnSubmitAuth = new Guna2GradientButton
            {
                Text = "Đăng nhập",
                Location = new Point(authPadX, authTopY + 308),
                Size = new Size(authFieldW, 52),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = theme.AccentPrimary,
                FillColor2 = theme.AccentSecondary,
                GradientMode = LinearGradientMode.ForwardDiagonal,
                ForeColor = Color.White,
                Font = FontBold(10.5F),
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                AutoRoundedCorners = false,
                Cursor = Cursors.Hand
            };
            btnSubmitAuth.DisabledState.FillColor = clrInputBg;
            btnSubmitAuth.DisabledState.FillColor2 = clrInputBg;
            btnSubmitAuth.DisabledState.ForeColor = clrTextMuted;

            void ApplyPasswordVisibility(bool show)
            {
                isPasswordShown = show;
                txtPassword.PasswordChar = show ? '\0' : '●';
                Image oldEye = btnTogglePass.Image;
                btnTogglePass.Image = CreateVectorIcon(show ? UiIcon.EyeOff : UiIcon.Eye, show ? accentText : clrIndigo, 18);
                btnTogglePass.BorderColor = show ? clrSoftGreenBorder : clrBorderLight;
                btnTogglePass.FillColor = show ? clrSoftGreenBg : clrInputBg;
                oldEye?.Dispose();
                uiToolTip.SetToolTip(btnTogglePass, show ? "Ẩn mật khẩu" : "Hiện mật khẩu");
            }

            void ApplyAuthMode(bool register, bool updateStatusHint)
            {
                isRegisterMode = register;
                if (register)
                {
                    btnModeReg.FillColor = clrPrimaryGreen;
                    btnModeReg.ForeColor = Color.White;
                    btnModeLogin.FillColor = Color.Transparent;
                    btnModeLogin.ForeColor = clrIndigo;

                    lblInitBudget.Visible = true;
                    txtInitBudget.Visible = true;
                    lblAuthStatus.Location = new Point(authPadX, authTopY + 322);
                    btnSubmitAuth.Location = new Point(authPadX, authTopY + 382);
                    cardAuth.Height = 464;
                    txtUsername.PlaceholderText = "Nhập tên hiển thị / tài khoản mới...";
                    btnSubmitAuth.Text = "Tạo tài khoản";
                    if (updateStatusHint)
                    {
                        lblAuthStatus.Text = "Điền tên hiển thị, mật khẩu và hạn mức chi tiêu tháng.";
                        lblAuthStatus.ForeColor = clrTextMuted;
                    }
                }
                else
                {
                    btnModeLogin.FillColor = clrPrimaryGreen;
                    btnModeLogin.ForeColor = Color.White;
                    btnModeReg.FillColor = Color.Transparent;
                    btnModeReg.ForeColor = clrIndigo;

                    lblInitBudget.Visible = false;
                    txtInitBudget.Visible = false;
                    lblAuthStatus.Location = new Point(authPadX, authTopY + 248);
                    btnSubmitAuth.Location = new Point(authPadX, authTopY + 308);
                    cardAuth.Height = 390;
                    txtUsername.PlaceholderText = "Nhập tên tài khoản...";
                    btnSubmitAuth.Text = "Đăng nhập";
                    if (updateStatusHint)
                    {
                        lblAuthStatus.Text = string.IsNullOrEmpty(initialNotice)
                            ? "Nhập tài khoản đã có để đăng nhập vào hệ thống."
                            : initialNotice;
                        lblAuthStatus.ForeColor = string.IsNullOrEmpty(initialNotice) ? clrTextMuted : clrRedDanger;
                    }
                }
                col.Height = y + cardAuth.Height + 20;
            }

            btnTogglePass.Click += (s, e) => ApplyPasswordVisibility(!isPasswordShown);

            btnDiscoverServer.Click += async (s, e) =>
            {
                btnDiscoverServer.Enabled = false;
                btnDiscoverServer.Text = "Đang dò...";
                lblAuthStatus.Text = "Đang dò tìm Server qua UDP Broadcast (Port 8889)...";
                lblAuthStatus.ForeColor = accentText;

                var found = await DiscoverServerViaUdpAsync();
                if (page.IsDisposed || btnDiscoverServer.IsDisposed) return;

                if (found.Success)
                {
                    txtServerIp.Text = found.Ip;
                    txtServerPort.Text = found.Port.ToString();
                    lblCloudChip.Text = $"LAN: {found.Ip}:{found.Port}";
                    LayoutHeroHeader();
                    lblAuthStatus.Text = $"✓ Tìm thấy Server ({found.HostName}) tại {found.Ip}:{found.Port}!";
                    lblAuthStatus.ForeColor = CurrentTheme.Success;
                }
                else
                {
                    lblAuthStatus.Text = "Không thấy Server phản hồi UDP 8889. Kiểm tra Server/Firewall!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                }

                btnDiscoverServer.Enabled = true;
                btnDiscoverServer.Text = "Dò Server LAN";
            };

            btnModeLogin.Click += (s, e) => ApplyAuthMode(false, updateStatusHint: true);
            btnModeReg.Click += (s, e) => ApplyAuthMode(true, updateStatusHint: true);

            btnSubmitAuth.Click += async (s, e) =>
            {
                string ip = string.IsNullOrWhiteSpace(txtServerIp.Text) ? "127.0.0.1" : txtServerIp.Text.Trim();
                int.TryParse(txtServerPort.Text.Trim(), out int port);
                if (port <= 0) port = 8888;
                string user = txtUsername.Text.Trim();
                string pass = txtPassword.Text.Trim();

                if (string.IsNullOrEmpty(user))
                {
                    lblAuthStatus.Text = "Vui lòng nhập tên tài khoản!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                    return;
                }

                if (string.IsNullOrEmpty(pass))
                {
                    lblAuthStatus.Text = "Vui lòng nhập mật khẩu!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                    return;
                }

                btnSubmitAuth.Enabled = false;
                lblAuthStatus.Text = $"Đang kết nối tới {ip}:{port}...";
                lblAuthStatus.ForeColor = accentText;

                onAuthFailedUI = (errMsg) =>
                {
                    if (lblAuthStatus.IsDisposed || btnSubmitAuth.IsDisposed) return;
                    lblAuthStatus.Text = errMsg;
                    lblAuthStatus.ForeColor = clrRedDanger;
                    btnSubmitAuth.Enabled = true;
                };

                bool ok = await ConnectAndAuthenticateAsync(ip, port, user, pass, isRegisterMode, txtInitBudget.Text, lblAuthStatus);
                if (page.IsDisposed || btnSubmitAuth.IsDisposed) return;
                if (!ok)
                {
                    btnSubmitAuth.Enabled = true;
                }
            };

            cardAuth.Controls.AddRange(new Control[]
            {
                segTrack, lblUserTitle, txtUsername,
                lblPassTitle, passRow,
                lblInitBudget, txtInitBudget,
                lblAuthStatus, btnSubmitAuth
            });

            col.Controls.Add(cardAuth);
            col.Height = y + cardAuth.Height + 20;

            authDraftCapture = () =>
            {
                if (txtServerIp.IsDisposed || txtPassword.IsDisposed)
                    return default;
                return (
                    txtServerIp.Text,
                    txtServerPort.Text,
                    txtUsername.Text,
                    txtPassword.Text,
                    isPasswordShown,
                    isRegisterMode,
                    txtInitBudget.Text
                );
            };

            authDraftRestore = (draft) =>
            {
                if (txtServerIp.IsDisposed || txtPassword.IsDisposed)
                    return;
                if (draft.Ip == null && draft.User == null && draft.Pass == null)
                    return;
                txtServerIp.Text = draft.Ip ?? "";
                txtServerPort.Text = draft.Port ?? "";
                txtUsername.Text = draft.User ?? "";
                txtPassword.Text = draft.Pass ?? "";
                txtInitBudget.Text = string.IsNullOrEmpty(draft.Budget) ? "3500000" : draft.Budget;
                ApplyAuthMode(draft.IsRegister, updateStatusHint: string.IsNullOrEmpty(initialNotice));
                ApplyPasswordVisibility(draft.ShowPass);
            };

            SetMainScreen(page);
        }

        private async Task<(bool Success, string Ip, int Port, string HostName)> DiscoverServerViaUdpAsync()
        {
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;
                byte[] reqBytes = Encoding.UTF8.GetBytes("POCKETLOG_DISCOVER_REQ");

                var targets = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.Broadcast };
                try
                {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                            continue;
                        foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ua.Address.AddressFamily == AddressFamily.InterNetwork && ua.IPv4Mask != null)
                            {
                                byte[] ipB = ua.Address.GetAddressBytes();
                                byte[] maskB = ua.IPv4Mask.GetAddressBytes();
                                if (ipB.Length == 4 && maskB.Length == 4)
                                {
                                    byte[] bcast = new byte[4];
                                    for (int i = 0; i < 4; i++) bcast[i] = (byte)(ipB[i] | ~maskB[i]);
                                    targets.Add(new IPAddress(bcast));
                                }
                            }
                        }
                    }
                }
                catch { }

                foreach (var targetIp in targets)
                {
                    try
                    {
                        await udp.SendAsync(reqBytes, reqBytes.Length, new IPEndPoint(targetIp, 8889));
                    }
                    catch { }
                }

                using var cts = new CancellationTokenSource(1500);
                while (!cts.IsCancellationRequested)
                {
                    var recv = await udp.ReceiveAsync(cts.Token);
                    string reply = Encoding.UTF8.GetString(recv.Buffer).Trim();
                    if (reply.StartsWith("POCKETLOG_DISCOVER_RES|"))
                    {
                        string[] parts = reply.Split('|');
                        int port = (parts.Length > 1 && int.TryParse(parts[1], out int p)) ? p : 8888;
                        string host = parts.Length > 2 ? parts[2] : "PocketLog Server";
                        string serverIp = recv.RemoteEndPoint.Address.ToString();
                        return (true, serverIp, port, host);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }

            return (false, "", 8888, "");
        }

        private static string GetEndpointCachePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketLog");
            return Path.Combine(dir, "server_endpoint.txt");
        }

        private static (string Host, string Port) LoadSavedServerEndpoint()
        {
            try
            {
                string path = GetEndpointCachePath();
                if (File.Exists(path))
                {
                    string[] parts = File.ReadAllText(path).Trim().Split('|');
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        return (parts[0].Trim(), parts[1].Trim());
                    }
                }
            }
            catch { }

            string cfgHost = ConfigurationManager.AppSettings["DefaultServerHost"]?.Trim() ?? "";
            string cfgPort = ConfigurationManager.AppSettings["DefaultServerPort"]?.Trim() ?? "";
            if (!string.IsNullOrEmpty(cfgHost))
            {
                return (cfgHost, string.IsNullOrEmpty(cfgPort) ? "8888" : cfgPort);
            }
            return ("20.239.65.18", "8888");
        }

        private static void SaveServerEndpoint(string host, int port)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(host) || host == "127.0.0.1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    return;
                string path = GetEndpointCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, $"{host.Trim()}|{port}");
            }
            catch { }
        }

        private async Task<bool> ConnectAndAuthenticateAsync(string ip, int port, string username, string password, bool isRegister, string initBudgetStr, Label lblStatus)
        {
            try
            {
                CloseCurrentSocket();

                tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(5000);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    CloseCurrentSocket();
                    if (lblStatus != null && !lblStatus.IsDisposed)
                    {
                        lblStatus.Text = $"Không tìm thấy Server tại {ip}:{port}.";
                        lblStatus.ForeColor = clrRedDanger;
                    }
                    return false;
                }

                await connectTask;
                netStream = tcpClient.GetStream();
                isConnected = true;
                currentServerIp = ip;
                currentServerPort = port;
                SaveServerEndpoint(ip, port);

                int.TryParse(initBudgetStr.Replace(".", "").Replace(",", ""), out int customBudget);
                if (customBudget <= 0) customBudget = 3500000;

                _ = Task.Run(() => ListenFromServerLoopAsync());

                var loginReq = new LoginRequestDto
                {
                    Username = username,
                    FullName = username,
                    Password = password,
                    IsRegister = isRegister,
                    InitialBudget = customBudget
                };
                await SendPacketSafeAsync(TcpPacket.Create(PacketType.LOGIN_REQ, loginReq));

                return true;
            }
            catch (Exception ex)
            {
                CloseCurrentSocket();
                if (lblStatus != null && !lblStatus.IsDisposed)
                {
                    lblStatus.Text = "Lỗi kết nối: " + ex.Message;
                    lblStatus.ForeColor = clrRedDanger;
                }
                return false;
            }
        }

        private void DisconnectAndLogout()
        {
            StopWebcam();
            CloseCurrentSocket();
            DisposeCachedImages();
            InitializeDefaultBudgets();
            currentUserId = 0;
            currentUsername = "";
            currentFullName = "";
            ShowAuthScreen();
        }
    }
}