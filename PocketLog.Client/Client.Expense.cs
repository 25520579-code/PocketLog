using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        private sealed class WalletGradientPanel : Guna2GradientPanel
        {
            private readonly Client owner;
            private int unpaidCount;
            private Color unpaidDotColor = Color.FromArgb(244, 63, 94);
            private int dividerX;
            private Rectangle pillRect;
            private string pillText = "";

            public Rectangle InnerWalletBounds => new Rectangle(
                4,
                2,
                Math.Max(4, ClientSize.Width - 8),
                Math.Max(4, ClientSize.Height - 8));

            public WalletGradientPanel(Client owner)
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

            public int UnpaidCount
            {
                get => unpaidCount;
                set
                {
                    int safe = Math.Max(0, value);
                    if (unpaidCount != safe)
                    {
                        unpaidCount = safe;
                        Invalidate();
                    }
                }
            }

            public Color UnpaidDotColor
            {
                get => unpaidDotColor;
                set
                {
                    if (unpaidDotColor != value)
                    {
                        unpaidDotColor = value;
                        Invalidate();
                    }
                }
            }

            public int DividerX
            {
                get => dividerX;
                set
                {
                    if (dividerX != value)
                    {
                        dividerX = value;
                        Invalidate();
                    }
                }
            }

            public Rectangle UnpaidPillBounds => pillRect;

            public void UpdateBadgeLayout(int divX, Rectangle badgeBounds, string badgeLabel)
            {
                dividerX = divX;
                pillRect = badgeBounds;
                pillText = badgeLabel ?? "";
                Invalidate();
            }

            private void PaintWalletBaseSurface(Graphics g)
            {
                Color backdrop = owner != null
                    ? owner.clrAppBg
                    : (BackColor.A == 255 ? BackColor : Color.White);
                using (var bgBrush = new SolidBrush(backdrop))
                {
                    g.FillRectangle(bgBrush, ClientRectangle);
                }

                Rectangle inner = InnerWalletBounds;
                if (inner.Width < 8 || inner.Height < 8) return;

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                float radius = Math.Max(4f, Math.Min(BorderRadius, Math.Min(inner.Width, inner.Height) / 2f));
                bool dark = owner != null && owner.isDarkMode;

                // Soft bottom ambient drop shadow subtly tinted with WalletGradStart
                Color shadowBase = dark
                    ? Color.Black
                    : BlendControlColor(FillColor, Color.FromArgb(15, 23, 42), 0.42f);

                using (GraphicsPath s3 = CreateRoundedPath(inner.X - 2.5f, inner.Y + 5f, inner.Width + 5f, inner.Height, radius + 2f))
                using (var b3 = new SolidBrush(Color.FromArgb(dark ? 28 : 14, shadowBase)))
                {
                    g.FillPath(b3, s3);
                }
                using (GraphicsPath s2 = CreateRoundedPath(inner.X - 1.5f, inner.Y + 3f, inner.Width + 3f, inner.Height, radius + 1f))
                using (var b2 = new SolidBrush(Color.FromArgb(dark ? 50 : 24, shadowBase)))
                {
                    g.FillPath(b2, s2);
                }
                using (GraphicsPath s1 = CreateRoundedPath(inner.X - 0.5f, inner.Y + 1.5f, inner.Width + 1f, inner.Height, radius + 0.5f))
                using (var b1 = new SolidBrush(Color.FromArgb(dark ? 78 : 36, shadowBase)))
                {
                    g.FillPath(b1, s1);
                }

                var rect = new RectangleF(inner.X + 0.5f, inner.Y + 0.5f, Math.Max(2f, inner.Width - 1f), Math.Max(2f, inner.Height - 1f));
                using GraphicsPath path = CreateRoundedPath(rect.X, rect.Y, rect.Width, rect.Height, radius);
                using (var grad = new LinearGradientBrush(rect, FillColor, FillColor2, LinearGradientMode.ForwardDiagonal))
                {
                    grad.WrapMode = WrapMode.TileFlipXY;
                    g.FillPath(grad, path);
                }

                PaintDecorativeGlows(g, path, inner);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                if (Width <= 8 || Height <= 8) return;
                PaintWalletBaseSurface(e.Graphics);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width <= 8 || Height <= 8) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                Rectangle inner = InnerWalletBounds;
                float radius = Math.Max(4f, Math.Min(BorderRadius, Math.Min(inner.Width, inner.Height) / 2f));
                var rect = new RectangleF(inner.X + 0.5f, inner.Y + 0.5f, Math.Max(2f, inner.Width - 1f), Math.Max(2f, inner.Height - 1f));
                using GraphicsPath path = CreateRoundedPath(rect.X, rect.Y, rect.Width, rect.Height, radius);

                GraphicsState saved = g.Save();
                try
                {
                    g.SetClip(path, CombineMode.Intersect);

                    if (dividerX > inner.Left + 40 && dividerX < inner.Right - 40)
                    {
                        using var divPen = new Pen(Color.FromArgb(68, 255, 255, 255), 1f);
                        g.DrawLine(divPen, dividerX, inner.Top + 14f, dividerX, inner.Bottom - 14f);
                    }

                    if (unpaidCount > 0 && pillRect.Width >= 24 && pillRect.Height >= 16 && !string.IsNullOrEmpty(pillText))
                    {
                        using GraphicsPath pPath = CreateRoundedPath(pillRect.X, pillRect.Y, pillRect.Width, pillRect.Height, pillRect.Height / 2f);
                        using var pFill = new SolidBrush(Color.FromArgb(64, 15, 23, 42));
                        using var pBorder = new Pen(Color.FromArgb(95, 255, 255, 255), 1f);
                        g.FillPath(pFill, pPath);
                        g.DrawPath(pBorder, pPath);

                        const int dotSize = 6;
                        float dotX = pillRect.X + 7f;
                        float dotY = pillRect.Y + (pillRect.Height - dotSize) / 2f;
                        Color dotClr = unpaidDotColor.A == 0 ? Color.FromArgb(244, 63, 94) : unpaidDotColor;
                        using (var dotBrush = new SolidBrush(dotClr))
                        {
                            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                        }

                        using Font pillFont = owner != null ? owner.FontMedium(7.4F) : new Font("Segoe UI", 7.4F, FontStyle.Bold);
                        var textRect = new Rectangle(
                            pillRect.X + 14,
                            pillRect.Y,
                            Math.Max(12, pillRect.Width - 18),
                            pillRect.Height);
                        TextRenderer.DrawText(
                            g,
                            pillText,
                            pillFont,
                            textRect,
                            Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                    }
                }
                finally
                {
                    g.Restore(saved);
                }

                using var sheenPen = new Pen(Color.FromArgb(46, 255, 255, 255), 1f);
                g.DrawPath(sheenPen, path);
            }

            private void PaintDecorativeGlows(Graphics g, GraphicsPath clipPath, Rectangle inner)
            {
                GraphicsState saved = g.Save();
                try
                {
                    g.SetClip(clipPath, CombineMode.Intersect);
                    using (var glowTop = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                    using (var glowBot = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                    {
                        g.FillEllipse(glowTop, inner.Right - 88, inner.Top - 32, 118, 118);
                        g.FillEllipse(glowBot, inner.Left - 24, inner.Bottom - 44, 92, 92);
                    }

                    // Subtle diagonal translucent glass highlight arc
                    using (var sheenPath = new GraphicsPath())
                    {
                        float startX = inner.Left + inner.Width * 0.14f;
                        float endX = inner.Left + inner.Width * 0.62f;
                        sheenPath.AddBezier(
                            new PointF(startX, inner.Top - 6f),
                            new PointF(startX + inner.Width * 0.18f, inner.Top + inner.Height * 0.34f),
                            new PointF(endX - inner.Width * 0.08f, inner.Top + inner.Height * 0.56f),
                            new PointF(endX + 26f, inner.Bottom + 8f));
                        sheenPath.AddLine(endX + 26f, inner.Bottom + 8f, endX - 24f, inner.Bottom + 8f);
                        sheenPath.AddBezier(
                            new PointF(endX - 24f, inner.Bottom + 8f),
                            new PointF(endX - inner.Width * 0.18f, inner.Top + inner.Height * 0.54f),
                            new PointF(startX + inner.Width * 0.05f, inner.Top + inner.Height * 0.30f),
                            new PointF(startX - 38f, inner.Top - 6f));
                        sheenPath.CloseFigure();

                        using var glassBrush = new SolidBrush(Color.FromArgb(22, 255, 255, 255));
                        g.FillPath(glassBrush, sheenPath);
                    }
                }
                finally
                {
                    g.Restore(saved);
                }
            }
        }

        private sealed class CategoryPercentPill : Label
        {
            public Color PillFillColor { get; set; }
            public Color PillBorderColor { get; set; }

            public CategoryPercentPill()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                DoubleBuffered = true;
                AutoSize = false;
                UseMnemonic = false;
                TextAlign = ContentAlignment.MiddleCenter;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var bg = new SolidBrush(BackColor))
                {
                    g.FillRectangle(bg, ClientRectangle);
                }

                if (Width < 8 || Height < 8) return;
                var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
                float r = rect.Height / 2f;
                using GraphicsPath path = CreateRoundedPath(rect.X, rect.Y, rect.Width, rect.Height, r);
                if (PillFillColor.A > 0)
                {
                    using var fill = new SolidBrush(PillFillColor);
                    g.FillPath(fill, path);
                }
                if (PillBorderColor.A > 0)
                {
                    using var border = new Pen(PillBorderColor, 1f);
                    g.DrawPath(border, path);
                }

                TextRenderer.DrawText(
                    g,
                    Text ?? "",
                    Font,
                    ClientRectangle,
                    ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }
        }

        private static string GetShortDisplayName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "bạn";
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[parts.Length - 1] : fullName.Trim();
        }

        private static void ApplyGradientButtonIcon(Guna2GradientButton btn, Bitmap iconBmp, int iconPx = 15)
        {
            if (btn == null) return;
            Image old = btn.Image;
            btn.Image = iconBmp;
            btn.ImageSize = new Size(iconPx, iconPx);
            btn.ImageAlign = HorizontalAlignment.Center;
            btn.ImageOffset = new Point(-2, 0);
            btn.TextOffset = new Point(2, 0);
            old?.Dispose();
        }

        private int CalculatePersonalSpentByCategory(string categoryName)
        {
            int myPostsShare = 0;
            foreach (var p in posts.Where(p => p.IsMine && p.HasTag && p.Category == categoryName))
            {
                int friendsShare = taggedDebts
                    .Where(d => d.PostId == p.PostId && d.CreditorId == currentUserId)
                    .Sum(d => d.MyShare);
                myPostsShare += Math.Max(0, p.Amount - friendsShare);
            }

            int taggedAsDebtorShare = taggedDebts
                .Where(d => d.DebtorId == currentUserId && d.Category == categoryName)
                .Sum(d => d.MyShare);

            return myPostsShare + taggedAsDebtorShare;
        }

        private int CalculateTotalPersonalSpent()
        {
            int myPostsShare = 0;
            foreach (var p in posts.Where(p => p.IsMine && p.HasTag))
            {
                int friendsShare = taggedDebts
                    .Where(d => d.PostId == p.PostId && d.CreditorId == currentUserId)
                    .Sum(d => d.MyShare);
                myPostsShare += Math.Max(0, p.Amount - friendsShare);
            }

            int taggedAsDebtorShare = taggedDebts
                .Where(d => d.DebtorId == currentUserId)
                .Sum(d => d.MyShare);

            return myPostsShare + taggedAsDebtorShare;
        }

        private Control BuildExpenseScreen()
        {
            AppPalette theme = CurrentTheme;
            Color accentText = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
            Color softTextInk = isDarkMode ? clrTextMain : clrPrimaryGreen;
            Color positiveColor = theme.Success;

            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 10;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalIOwe = unpaidList.Where(d => d.DebtorId == currentUserId).Sum(d => d.MyShare);
            int totalOwedToMe = unpaidList.Where(d => d.CreditorId == currentUserId).Sum(d => d.MyShare);

            TaggedDebtItem currentDebt = taggedDebts.FirstOrDefault(d => d.SplitId == selectedSplitId)
                                         ?? unpaidList.FirstOrDefault()
                                         ?? taggedDebts.FirstOrDefault();

            var stackRelayoutActions = new List<(Control Ctrl, int GapAfter, Action<int> OnLayout)>();

            // Thẻ tổng quan công nợ Gradient chéo 88px (WalletGradStart -> WalletGradEnd) với bóng đổ & glass sheen
            var pnlBanner = new WalletGradientPanel(this)
            {
                Location = new Point(0, y),
                Size = new Size(w, 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BorderRadius = 24,
                FillColor = theme.WalletGradStart,
                FillColor2 = theme.WalletGradEnd,
                GradientMode = LinearGradientMode.ForwardDiagonal,
                BackColor = clrAppBg,
                UseTransparentBackground = false,
                UnpaidCount = unpaidList.Count,
                UnpaidDotColor = theme.Danger,
                Cursor = Cursors.Default
            };
            AttachEdgeResizeRedirector(pnlBanner);

            Color bannerTitleColor = Color.FromArgb(214, 222, 255);
            Color oweValueColor = totalIOwe > 0 ? Color.FromArgb(254, 205, 211) : Color.White;
            Color owedValueColor = totalOwedToMe > 0 ? Color.FromArgb(167, 243, 208) : Color.White;

            var lblOweTitle = MakeLabel("Bạn đang nợ", 20, 12, 8.2F, FontStyle.Regular, bannerTitleColor, 150);
            var lblOweVal = MakeLabel($"{totalIOwe:N0}đ", 20, 38, 12.2F, FontStyle.Bold, oweValueColor, 150);
            SetControlFont(lblOweVal, FontBold(12.2F));
            lblOweVal.Height = TextRenderer.MeasureText("Ág", lblOweVal.Font).Height + 4;

            var lblOwedTitle = MakeLabel("Bạn bè nợ bạn", 200, 12, 8.2F, FontStyle.Regular, bannerTitleColor, 150);
            var lblOwedVal = MakeLabel($"{totalOwedToMe:N0}đ", 200, 38, 12.2F, FontStyle.Bold, owedValueColor, 150);
            SetControlFont(lblOwedVal, FontBold(12.2F));
            lblOwedVal.Height = TextRenderer.MeasureText("Ág", lblOwedVal.Font).Height + 4;

            void LayoutBanner()
            {
                if (pnlBanner.IsDisposed) return;
                Rectangle inner = pnlBanner.InnerWalletBounds;
                if (inner.Width <= 120) return;

                double splitRatio = pnlBanner.Width < 450 ? 0.415 : 0.465;
                int minDiv = inner.Left + Math.Min(116, inner.Width / 2);
                int maxDiv = Math.Max(minDiv, inner.Right - 116);
                int divX = Math.Clamp(inner.Left + (int)Math.Round(inner.Width * splitRatio), minDiv, maxDiv);

                int leftX = inner.Left + 16;
                int leftW = Math.Max(64, divX - leftX - 10);
                int rightX = divX + 12;
                int rightMaxW = Math.Max(64, inner.Right - rightX - 14);
                int titleY = inner.Top + 10;
                int valY = inner.Top + 36;

                lblOweTitle.Location = new Point(leftX, titleY);
                lblOweTitle.Width = leftW;
                lblOweVal.Location = new Point(leftX, valY);
                lblOweVal.Width = leftW;

                Rectangle pillBounds = Rectangle.Empty;
                string badgeLabel = "";
                int owedTitleWidth = rightMaxW;

                if (unpaidList.Count > 0)
                {
                    Font owedF = lblOwedTitle.Font;
                    using Font pillF = FontMedium(7.4F);
                    int owedTextW = TextRenderer.MeasureText("Bạn bè nợ bạn", owedF, new Size(400, 40), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;

                    string fullPill = $"{unpaidList.Count} bill chờ";
                    string compactPill = $"{unpaidList.Count} chờ";
                    int fullPillW = TextRenderer.MeasureText(fullPill, pillF, new Size(200, 30), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 20;
                    int compactPillW = TextRenderer.MeasureText(compactPill, pillF, new Size(200, 30), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 20;

                    int availPillW = rightMaxW - owedTextW - 6;
                    int chosenPillW;
                    if (availPillW >= fullPillW)
                    {
                        badgeLabel = fullPill;
                        chosenPillW = fullPillW;
                    }
                    else
                    {
                        badgeLabel = compactPill;
                        chosenPillW = Math.Min(compactPillW, Math.Max(44, availPillW));
                    }

                    pillBounds = new Rectangle(inner.Right - 14 - chosenPillW, inner.Top + 9, chosenPillW, 22);
                    owedTitleWidth = Math.Max(68, pillBounds.Left - rightX - 4);
                }

                lblOwedTitle.Location = new Point(rightX, titleY);
                lblOwedTitle.Width = owedTitleWidth;
                lblOwedVal.Location = new Point(rightX, valY);
                lblOwedVal.Width = rightMaxW;

                pnlBanner.UpdateBadgeLayout(divX, pillBounds, badgeLabel);
            }

            pnlBanner.Controls.AddRange(new Control[] { lblOweTitle, lblOweVal, lblOwedTitle, lblOwedVal });
            pnlBanner.Resize += (s, e) => LayoutBanner();
            if (unpaidList.Count > 0)
            {
                pnlBanner.MouseMove += (s, e) =>
                {
                    Cursor target = pnlBanner.UnpaidPillBounds.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
                    if (pnlBanner.Cursor != target) pnlBanner.Cursor = target;
                };
                pnlBanner.MouseLeave += (s, e) =>
                {
                    if (pnlBanner.Cursor != Cursors.Default) pnlBanner.Cursor = Cursors.Default;
                };
                pnlBanner.MouseClick += (s, e) =>
                {
                    if (pnlBanner.UnpaidPillBounds.Contains(e.Location))
                    {
                        isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                        RefreshPaneContent("EXPENSE");
                    }
                };
            }
            LayoutBanner();
            col.Controls.Add(pnlBanner);
            stackRelayoutActions.Add((pnlBanner, 10, curW =>
            {
                pnlBanner.Width = curW;
                LayoutBanner();
            }));
            y += pnlBanner.Height + 10;

            if (currentDebt != null)
            {
                var cardActiveDebt = new ElevatedCardPanel(this)
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 276),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    CornerRadius = 22,
                    CardFillColor = clrCardBg,
                    CardBorderColor = clrBorderLight,
                    CardBorderThickness = 1,
                    ElevationLevel = 2,
                    BackColor = clrAppBg
                };

                bool isDebtor = (currentDebt.DebtorId == currentUserId);
                string roleDesc = isDebtor
                    ? $"Bạn nợ {currentDebt.CreditorName}"
                    : $"{currentDebt.DebtorName} nợ bạn";

                Rectangle dInner = cardActiveDebt.InnerCardBounds;
                var lblDebtTitle = MakeLabel($"Khoản chia: {currentDebt.Title}", dInner.Left + 16, dInner.Top + 12, 10.2F, FontStyle.Bold, clrTextMain, Math.Max(100, dInner.Width - 32));
                var lblDebtRole = MakeLabel(
                    $"{roleDesc}  •  Phần chia: {currentDebt.MyShare:N0}đ",
                    dInner.Left + 16, dInner.Top + 40, 9.2F, FontStyle.Bold,
                    isDebtor ? clrRedDanger : positiveColor,
                    Math.Max(100, dInner.Width - 32));

                string stateTxt = currentDebt.IsConfirmedPaid ? "Đã hoàn thành" : (currentDebt.IsSentProof ? "Chờ xác nhận" : "Chưa thanh toán");
                Color stateBg = currentDebt.IsConfirmedPaid
                    ? (isDarkMode ? Color.FromArgb(16, 62, 46) : Color.FromArgb(220, 252, 231))
                    : (currentDebt.IsSentProof ? clrSoftGreenBg : clrSoftRedBg);
                Color stateFore = currentDebt.IsConfirmedPaid
                    ? positiveColor
                    : (currentDebt.IsSentProof ? accentText : clrRedDanger);
                Color stateBorder = currentDebt.IsConfirmedPaid
                    ? (isDarkMode ? Color.FromArgb(32, 110, 82) : Color.FromArgb(167, 243, 208))
                    : (currentDebt.IsSentProof ? clrSoftGreenBorder : (isDarkMode ? Color.FromArgb(136, 19, 55) : Color.FromArgb(254, 205, 211)));

                int stateBadgeW = Math.Max(148, MeasurePillBtnWidth(stateTxt, 8.2F, 15));
                var badgeState = MakeBtn(stateTxt, dInner.Left + 16, dInner.Top + 68, stateBadgeW, 30, 15, stateBg, stateFore, stateBorder, 1, 8.2F);
                badgeState.Cursor = Cursors.Default;
                badgeState.TabStop = false;
                badgeState.HoverState.FillColor = stateBg;
                badgeState.HoverState.BorderColor = stateBorder;

                int noteLeft = badgeState.Right + 10;
                Label lblNote = MakeLabel(currentDebt.StatusNote, noteLeft, dInner.Top + 72, 8.2F, FontStyle.Regular, clrTextMuted, Math.Max(70, dInner.Right - noteLeft - 16));

                cardActiveDebt.Controls.AddRange(new Control[] { lblDebtTitle, lblDebtRole, badgeState, lblNote });

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(dInner.Left + 16, dInner.Top + 108),
                    Size = new Size(132, 144),
                    BorderRadius = 14,
                    FillColor = clrInputBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                Guna2PictureBox picProof = new Guna2PictureBox
                {
                    Dock = DockStyle.Fill,
                    BorderRadius = 13,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = currentDebt.ProofImage,
                    Visible = currentDebt.ProofImage != null,
                    UseTransparentBackground = true
                };

                Panel emptyProofWrap = new Panel
                {
                    Dock = DockStyle.Fill,
                    Visible = currentDebt.ProofImage == null,
                    BackColor = Color.Transparent
                };
                var emptyProofBadge = MakeSoftIconBadge(UiIcon.Receipt, 47, 26, 38, 12, clrSoftGreenBg, accentText);
                var lblEmptyProof = new Label
                {
                    Text = "Ảnh bill\nchuyển khoản",
                    Font = FontRegular(8.2F),
                    ForeColor = clrTextMuted,
                    UseMnemonic = false,
                    Location = new Point(6, 72),
                    Size = new Size(120, 56),
                    TextAlign = ContentAlignment.TopCenter,
                    BackColor = Color.Transparent
                };
                emptyProofWrap.Controls.Add(emptyProofBadge);
                emptyProofWrap.Controls.Add(lblEmptyProof);

                pnlBillImg.Controls.Add(emptyProofWrap);
                pnlBillImg.Controls.Add(picProof);
                cardActiveDebt.Controls.Add(pnlBillImg);

                var btnUploadProof = MakeBtn("Gửi ảnh bill CK", dInner.Left + 160, dInner.Top + 108, Math.Max(140, dInner.Width - 176), 66, 16, clrSoftGreenBg, softTextInk, clrSoftGreenBorder, 1, 8.5F);
                btnUploadProof.HoverState.FillColor = clrCardBg;
                btnUploadProof.HoverState.ForeColor = softTextInk;
                btnUploadProof.HoverState.BorderColor = clrPrimaryGreen;
                AttachControlImageDisposal(btnUploadProof);

                var btnFriendVerify = new Guna2GradientButton
                {
                    Text = "Xác nhận hoàn thành",
                    Location = new Point(dInner.Left + 160, dInner.Top + 186),
                    Size = new Size(Math.Max(140, dInner.Width - 176), 66),
                    BorderRadius = 16,
                    FillColor = theme.AccentPrimary,
                    FillColor2 = theme.AccentSecondary,
                    GradientMode = LinearGradientMode.ForwardDiagonal,
                    ForeColor = Color.White,
                    Font = FontMedium(8.5F),
                    BackColor = Color.Transparent,
                    UseTransparentBackground = true,
                    AutoRoundedCorners = false,
                    Cursor = Cursors.Hand
                };
                btnFriendVerify.DisabledState.FillColor = clrInputBg;
                btnFriendVerify.DisabledState.FillColor2 = clrInputBg;
                btnFriendVerify.DisabledState.ForeColor = clrTextMuted;
                btnFriendVerify.DisabledState.BorderColor = clrBorderLight;
                AttachControlImageDisposal(btnFriendVerify);

                string uploadFullText;
                string uploadShortText;
                bool uploadEnabled;
                string verifyFullText;
                string verifyShortText;
                bool verifyEnabled;

                if (currentDebt.IsConfirmedPaid)
                {
                    uploadEnabled = false;
                    uploadFullText = "Đã đính kèm bill";
                    uploadShortText = uploadFullText;
                    verifyEnabled = false;
                    verifyFullText = "Đã tất toán xong";
                    verifyShortText = verifyFullText;
                }
                else if (isDebtor)
                {
                    uploadEnabled = true;
                    uploadFullText = currentDebt.IsSentProof ? "Đổi ảnh bill khác" : "Gửi ảnh bill CK";
                    uploadShortText = uploadFullText;
                    verifyEnabled = false;
                    verifyFullText = $"Chờ {currentDebt.CreditorName} duyệt";
                    verifyShortText = $"Chờ {GetShortDisplayName(currentDebt.CreditorName)} duyệt";
                }
                else
                {
                    uploadEnabled = false;
                    uploadFullText = currentDebt.IsSentProof ? $"{currentDebt.DebtorName} đã gửi bill" : $"Chờ {currentDebt.DebtorName} gửi bill";
                    uploadShortText = currentDebt.IsSentProof
                        ? $"{GetShortDisplayName(currentDebt.DebtorName)} đã gửi bill"
                        : $"Chờ {GetShortDisplayName(currentDebt.DebtorName)} gửi bill";
                    verifyEnabled = true;
                    verifyFullText = "Xác nhận đã trả";
                    verifyShortText = verifyFullText;
                }

                btnUploadProof.Enabled = uploadEnabled;
                AttachBtnIcon(btnUploadProof, currentDebt.IsConfirmedPaid ? UiIcon.Check : UiIcon.Upload, uploadEnabled ? accentText : clrTextMuted, 15);

                btnFriendVerify.Enabled = verifyEnabled;
                btnFriendVerify.BorderThickness = verifyEnabled ? 0 : 1;
                btnFriendVerify.BorderColor = verifyEnabled ? Color.Transparent : clrBorderLight;
                ApplyGradientButtonIcon(btnFriendVerify, CreateVectorIcon(UiIcon.Check, verifyEnabled ? Color.White : clrTextMuted, 15), 15);

                void LayoutActiveDebtCard()
                {
                    if (cardActiveDebt.IsDisposed) return;
                    Rectangle inner = cardActiveDebt.InnerCardBounds;
                    if (inner.Width < 180) return;

                    lblDebtTitle.Location = new Point(inner.Left + 16, inner.Top + 12);
                    lblDebtTitle.Width = Math.Max(100, inner.Width - 32);

                    lblDebtRole.Location = new Point(inner.Left + 16, inner.Top + 40);
                    lblDebtRole.Width = Math.Max(100, inner.Width - 32);

                    badgeState.Location = new Point(inner.Left + 16, inner.Top + 68);
                    int nLeft = badgeState.Right + 10;
                    lblNote.Location = new Point(nLeft, inner.Top + 72);
                    lblNote.Width = Math.Max(70, inner.Right - nLeft - 16);

                    int imgW = inner.Width < 432 ? 120 : 148;
                    pnlBillImg.SetBounds(inner.Left + 16, inner.Top + 108, imgW, 144);
                    emptyProofBadge.Left = (imgW - emptyProofBadge.Width) / 2;
                    lblEmptyProof.Size = new Size(Math.Max(60, imgW - 12), 56);

                    int rightX = pnlBillImg.Right + 12;
                    int rightW = Math.Max(120, inner.Right - rightX - 16);
                    btnUploadProof.SetBounds(rightX, inner.Top + 108, rightW, 66);
                    btnFriendVerify.SetBounds(rightX, inner.Top + 186, rightW, 66);

                    btnUploadProof.Text = (uploadFullText == uploadShortText || MeasurePillBtnWidth(uploadFullText, 8.5F, 16, true, 15) <= rightW)
                        ? uploadFullText
                        : uploadShortText;
                    btnFriendVerify.Text = (verifyFullText == verifyShortText || MeasurePillBtnWidth(verifyFullText, 8.5F, 16, true, 15) <= rightW)
                        ? verifyFullText
                        : verifyShortText;
                }

                cardActiveDebt.Resize += (s, e) => LayoutActiveDebtCard();
                LayoutActiveDebtCard();

                btnUploadProof.Click += async (s, e) =>
                {
                    using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                    {
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            using var img = Image.FromFile(ofd.FileName);
                            string b64 = ImageToBase64(img);
                            await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPLOAD_PROOF_REQ, new UploadProofRequestDto
                            {
                                SplitId = currentDebt.SplitId,
                                UserId = currentUserId,
                                ProofImageBase64 = b64
                            }));
                        }
                    }
                };

                btnFriendVerify.Click += async (s, e) =>
                {
                    if (!currentDebt.IsSentProof)
                    {
                        lblNote.Text = "Người nợ chưa gửi ảnh bill!";
                        lblNote.ForeColor = clrRedDanger;
                        return;
                    }
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.VERIFY_BILL_REQ, new VerifyBillRequestDto
                    {
                        SplitId = currentDebt.SplitId,
                        VerifierName = currentFullName
                    }));
                };

                cardActiveDebt.Controls.Add(btnUploadProof);
                cardActiveDebt.Controls.Add(btnFriendVerify);
                col.Controls.Add(cardActiveDebt);
                stackRelayoutActions.Add((cardActiveDebt, 10, curW =>
                {
                    cardActiveDebt.Width = curW;
                    LayoutActiveDebtCard();
                }));
                y += cardActiveDebt.Height + 10;
            }
            else
            {
                var compactEmpty = new ElevatedCardPanel(this)
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 104),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    CornerRadius = 20,
                    CardFillColor = clrCardBg,
                    CardBorderColor = clrBorderLight,
                    CardBorderThickness = 1,
                    ElevationLevel = 2,
                    BackColor = clrAppBg
                };
                Rectangle eInner = compactEmpty.InnerCardBounds;
                var emptyIcon = MakeSoftIconBadge(UiIcon.Receipt, eInner.Left + 14, eInner.Top + 24, 42, 13, clrSoftGreenBg, accentText);
                var lEmpty1 = MakeLabel("Chưa có khoản chia tiền nào được gắn tag", eInner.Left + 66, eInner.Top + 16, 8.4F, FontStyle.Bold, clrTextMain, Math.Max(120, eInner.Width - 78));
                var lEmpty2 = MakeLabel("Gắn tag bạn bè lúc đăng ảnh chi tiêu để tự động chia hóa đơn", eInner.Left + 66, lEmpty1.Bottom + 4, 8.2F, FontStyle.Regular, clrTextMuted, Math.Max(120, eInner.Width - 78), lines: 2);
                compactEmpty.Controls.AddRange(new Control[] { emptyIcon, lEmpty1, lEmpty2 });

                void LayoutCompactEmpty()
                {
                    if (compactEmpty.IsDisposed) return;
                    Rectangle inner = compactEmpty.InnerCardBounds;
                    int txtW = Math.Max(120, inner.Width - 78);
                    emptyIcon.Location = new Point(inner.Left + 14, inner.Top + 24);
                    lEmpty1.SetBounds(inner.Left + 66, inner.Top + 16, txtW, lEmpty1.Height);
                    lEmpty2.SetBounds(inner.Left + 66, lEmpty1.Bottom + 4, txtW, lEmpty2.Height);
                    compactEmpty.Height = Math.Max(104, lEmpty2.Bottom + 14 + 6);
                }

                LayoutCompactEmpty();
                col.Controls.Add(compactEmpty);
                stackRelayoutActions.Add((compactEmpty, 10, curW =>
                {
                    compactEmpty.Width = curW;
                    LayoutCompactEmpty();
                }));
                y += compactEmpty.Height + 10;
            }

            var btnToggleUnpaid = MakeBtn(
                $"Các khoản chi gắn tag chưa tất toán ({unpaidList.Count})",
                4, y, Math.Max(180, w - 8), 46, 16, clrSoftGreenBg, softTextInk, clrSoftGreenBorder, 1, 8.2F);
            btnToggleUnpaid.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnToggleUnpaid.HoverState.FillColor = clrCardBg;
            btnToggleUnpaid.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnToggleUnpaid, isUnpaidDropdownOpen ? UiIcon.ChevronUp : UiIcon.ChevronDown, accentText, 15);
            AttachControlImageDisposal(btnToggleUnpaid);
            AttachEdgeResizeRedirector(btnToggleUnpaid);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                RefreshPaneContent("EXPENSE");
            };
            col.Controls.Add(btnToggleUnpaid);
            stackRelayoutActions.Add((btnToggleUnpaid, 10, curW =>
            {
                btnToggleUnpaid.Left = 4;
                btnToggleUnpaid.Width = Math.Max(180, curW - 8);
            }));
            y += 56;

            if (isUnpaidDropdownOpen)
            {
                if (unpaidList.Count == 0)
                {
                    Color paidBg = isDarkMode ? Color.FromArgb(16, 62, 46) : clrSoftGreenBg;
                    Color paidBorder = isDarkMode ? Color.FromArgb(32, 110, 82) : clrSoftGreenBorder;
                    var emptyBox = new ElevatedCardPanel(this)
                    {
                        Location = new Point(0, y),
                        Size = new Size(w, 66),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left,
                        CornerRadius = 16,
                        CardFillColor = clrCardBg,
                        CardBorderColor = paidBorder,
                        CardBorderThickness = 1,
                        ElevationLevel = 1,
                        BackColor = clrAppBg
                    };
                    Rectangle ebInner = emptyBox.InnerCardBounds;
                    var paidBadge = MakeSoftIconBadge(UiIcon.Check, ebInner.Left + 14, ebInner.Top + 14, 30, 10, paidBg, positiveColor);
                    var lblAllPaid = MakeLabel("Tất cả các khoản chia tiền đều đã tất toán xong!", ebInner.Left + 54, ebInner.Top + 10, 8.4F, FontStyle.Bold, positiveColor, Math.Max(120, ebInner.Width - 70), lines: 2);
                    lblAllPaid.TextAlign = ContentAlignment.MiddleLeft;
                    lblAllPaid.Height = Math.Max(34, ebInner.Height - 20);
                    emptyBox.Controls.AddRange(new Control[] { paidBadge, lblAllPaid });
                    col.Controls.Add(emptyBox);
                    stackRelayoutActions.Add((emptyBox, 10, curW =>
                    {
                        emptyBox.Width = curW;
                        Rectangle inner = emptyBox.InnerCardBounds;
                        paidBadge.Location = new Point(inner.Left + 14, inner.Top + 14);
                        lblAllPaid.SetBounds(inner.Left + 54, inner.Top + 10, Math.Max(120, inner.Width - 70), Math.Max(34, inner.Height - 20));
                    }));
                    y += emptyBox.Height + 10;
                }
                else
                {
                    int selBtnW = Math.Max(118, Math.Max(
                        MeasurePillBtnWidth("Đang chọn", 8.4F, 18),
                        MeasurePillBtnWidth("Kiểm tra", 8.4F, 18)));

                    foreach (var debt in unpaidList)
                    {
                        bool isSelected = currentDebt != null && debt.SplitId == currentDebt.SplitId;
                        bool rowIsDebtor = (debt.DebtorId == currentUserId);
                        string roleTag = rowIsDebtor ? $"Bạn nợ {debt.CreditorName}" : $"{debt.DebtorName} nợ bạn";

                        var itemRow = new ElevatedCardPanel(this)
                        {
                            Location = new Point(0, y),
                            Size = new Size(w, 102),
                            Anchor = AnchorStyles.Top | AnchorStyles.Left,
                            CornerRadius = 18,
                            CardFillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                            CardBorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                            CardBorderThickness = 1,
                            ElevationLevel = 1,
                            BackColor = clrAppBg
                        };

                        Rectangle rInner = itemRow.InnerCardBounds;
                        int textMaxW = Math.Max(96, rInner.Width - selBtnW - 36);
                        var r1 = MakeLabel($"{GetCategoryShortClean(debt.Category)}  •  {debt.Title}", rInner.Left + 16, rInner.Top + 10, 9.2F, FontStyle.Bold, clrTextMain, textMaxW);
                        var r2 = MakeLabel($"{roleTag}  •  {debt.MyShare:N0}đ", rInner.Left + 16, rInner.Top + 37, 8.8F, FontStyle.Bold, rowIsDebtor ? clrRedDanger : positiveColor, textMaxW);
                        var r3 = MakeLabel($"{debt.Time}  •  {(debt.IsSentProof ? "Đã gửi bill" : "Chưa gửi bill")}", rInner.Left + 16, rInner.Top + 64, 8.2F, FontStyle.Regular, clrTextMuted, textMaxW);

                        var btnSelectDebt = MakeBtn(
                            isSelected ? "Đang chọn" : (rowIsDebtor ? "Trả ngay" : "Kiểm tra"),
                            rInner.Right - selBtnW - 14, rInner.Top + 27, selBtnW, 40, 20,
                            isSelected ? clrPrimaryGreen : clrInputBg,
                            isSelected ? Color.White : (isDarkMode ? clrTextMain : clrIndigo),
                            isSelected ? clrPrimaryGreen : clrBorderLight, 1, 8.4F);
                        btnSelectDebt.HoverState.FillColor = isSelected ? clrPrimaryGreen : clrSoftGreenBg;
                        btnSelectDebt.HoverState.BorderColor = clrPrimaryGreen;

                        btnSelectDebt.Click += (s, e) =>
                        {
                            selectedSplitId = debt.SplitId;
                            RefreshPaneContent("EXPENSE");
                        };

                        void LayoutUnpaidRow()
                        {
                            if (itemRow.IsDisposed) return;
                            Rectangle inner = itemRow.InnerCardBounds;
                            int txtW = Math.Max(96, inner.Width - selBtnW - 36);
                            r1.SetBounds(inner.Left + 16, inner.Top + 10, txtW, r1.Height);
                            r2.SetBounds(inner.Left + 16, inner.Top + 37, txtW, r2.Height);
                            r3.SetBounds(inner.Left + 16, inner.Top + 64, txtW, r3.Height);
                            btnSelectDebt.SetBounds(inner.Right - selBtnW - 14, inner.Top + 27, selBtnW, 40);
                        }

                        itemRow.Controls.AddRange(new Control[] { r1, r2, r3, btnSelectDebt });
                        LayoutUnpaidRow();
                        col.Controls.Add(itemRow);
                        stackRelayoutActions.Add((itemRow, 8, curW =>
                        {
                            itemRow.Width = curW;
                            LayoutUnpaidRow();
                        }));
                        y += itemRow.Height + 8;
                    }
                }
            }

            // --- Monthly Budget Overview Card (BudgetDonutCard + Total Budget Editor) ABOVE Category List ---
            int grandTotal = CalculateTotalPersonalSpent();
            bool isTotalOver = totalBudgetLimit > 0 && grandTotal > totalBudgetLimit;
            var donutSlices = new List<BudgetSlice>(standardCategories.Length);
            foreach (var cat in standardCategories)
            {
                int cSpent = CalculatePersonalSpentByCategory(cat.Key);
                donutSlices.Add(new BudgetSlice(cat.ShortLabel, cSpent, GetCategoryAccentColor(cat.Key)));
            }

            Color overCardBorder = isDarkMode ? Color.FromArgb(136, 19, 55) : Color.FromArgb(254, 205, 211);
            var cardBudgetOverview = new ElevatedCardPanel(this)
            {
                Location = new Point(0, y),
                Size = new Size(w, 260),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                CornerRadius = 22,
                CardFillColor = clrCardBg,
                CardBorderColor = isTotalOver ? overCardBorder : clrBorderLight,
                CardBorderThickness = 1,
                ElevationLevel = 2,
                BackColor = clrAppBg
            };

            var donutCard = new BudgetDonutCard(this)
            {
                BackColor = clrCardBg
            };
            donutCard.SetData(grandTotal, totalBudgetLimit, donutSlices);

            var dividerBudget = new Panel
            {
                Height = 1,
                BackColor = clrBorderLight
            };

            int saveTotalBtnW = Math.Max(158, MeasurePillBtnWidth("Lưu tổng hạn mức", 8.5F, 14));
            var totalActionGrid = new BufferedTableLayout
            {
                Size = new Size(Math.Max(220, w - 32), 46),
                ColumnCount = 2,
                RowCount = 1
            };
            totalActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            totalActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, saveTotalBtnW));
            totalActionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtTotalIn = MakeTextBox(totalBudgetLimit.ToString("N0"), "Tổng hạn mức", 0, 0, 140, 42, clrCardBg, true);
            txtTotalIn.Dock = DockStyle.Fill;
            txtTotalIn.FillColor = theme.InputBg;
            txtTotalIn.FocusedState.FillColor = theme.CardBg;
            txtTotalIn.BorderColor = clrSoftGreenBorder;
            txtTotalIn.Margin = new Padding(0, 1, 8, 1);

            var btnSetTotal = new Guna2GradientButton
            {
                Text = "Lưu tổng hạn mức",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 1, 0, 1),
                BorderRadius = 14,
                FillColor = theme.AccentPrimary,
                FillColor2 = theme.AccentSecondary,
                GradientMode = LinearGradientMode.ForwardDiagonal,
                ForeColor = Color.White,
                Font = FontMedium(8.5F),
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                AutoRoundedCorners = false,
                Cursor = Cursors.Hand
            };
            btnSetTotal.DisabledState.FillColor = clrInputBg;
            btnSetTotal.DisabledState.FillColor2 = clrInputBg;
            btnSetTotal.DisabledState.ForeColor = clrTextMuted;
            AttachEdgeResizeRedirector(btnSetTotal);
            btnSetTotal.Click += async (s, e) =>
            {
                if (int.TryParse(txtTotalIn.Text.Replace(".", "").Replace(",", "").Trim(), out int nTot) && nTot > 0)
                {
                    totalBudgetLimit = nTot;
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPDATE_BUDGET_REQ, new UpdateBudgetRequestDto
                    {
                        UserId = currentUserId,
                        NewMonthlyBudget = nTot
                    }));
                    RefreshPaneContent("EXPENSE");
                }
            };

            totalActionGrid.Controls.Add(txtTotalIn, 0, 0);
            totalActionGrid.Controls.Add(btnSetTotal, 1, 0);

            cardBudgetOverview.Controls.AddRange(new Control[] { donutCard, dividerBudget, totalActionGrid });

            void LayoutBudgetOverviewCard()
            {
                if (cardBudgetOverview.IsDisposed) return;
                Rectangle inner = cardBudgetOverview.InnerCardBounds;
                int contentW = Math.Max(220, inner.Width - 24);
                int donutH = donutCard.GetRequiredHeight(contentW);

                donutCard.SetBounds(inner.Left + 12, inner.Top + 12, contentW, donutH);
                dividerBudget.SetBounds(inner.Left + 14, donutCard.Bottom + 8, Math.Max(100, inner.Width - 28), 1);
                totalActionGrid.SetBounds(inner.Left + 12, dividerBudget.Bottom + 10, contentW, 46);

                int targetH = totalActionGrid.Bottom + 12 + 6;
                if (cardBudgetOverview.Height != targetH)
                {
                    cardBudgetOverview.Height = targetH;
                }
            }

            LayoutBudgetOverviewCard();
            col.Controls.Add(cardBudgetOverview);
            stackRelayoutActions.Add((cardBudgetOverview, 14, curW =>
            {
                cardBudgetOverview.Width = curW;
                LayoutBudgetOverviewCard();
            }));
            y += cardBudgetOverview.Height + 14;

            var lblSecCat = MakeLabel("Chi tiêu thực tế theo danh mục", 6, y, 11F, FontStyle.Bold, clrTextMain, Math.Max(160, w - 12));
            SetControlFont(lblSecCat, FontBold(11F));
            lblSecCat.Height = TextRenderer.MeasureText("Ág", lblSecCat.Font).Height + 4;
            lblSecCat.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            col.Controls.Add(lblSecCat);
            stackRelayoutActions.Add((lblSecCat, 10, curW =>
            {
                lblSecCat.Left = 6;
                lblSecCat.Width = Math.Max(160, curW - 12);
            }));
            y += lblSecCat.Height + 10;

            int limBtnW = Math.Max(118, MeasurePillBtnWidth("Lưu hạn mức", 8.3F, 12));

            foreach (var cat in standardCategories)
            {
                string catName = cat.Key;
                int spent = CalculatePersonalSpentByCategory(catName);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;
                bool isOver = spent > limit;
                Color catAccent = isOver ? clrRedDanger : GetCategoryAccentColor(catName);
                Color badgeBg = BlendControlColor(catAccent, clrCardBg, isDarkMode ? 0.80f : 0.88f);
                Color pillInk = isOver
                    ? clrRedDanger
                    : (isDarkMode ? BlendControlColor(catAccent, Color.White, 0.20f) : BlendControlColor(catAccent, clrTextMain, 0.24f));
                Color pillBorder = BlendControlColor(catAccent, clrCardBg, isDarkMode ? 0.55f : 0.65f);
                Color prgEnd = BlendControlColor(catAccent, Color.White, isDarkMode ? 0.18f : 0.25f);

                var catCard = new ElevatedCardPanel(this)
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 114),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left,
                    CornerRadius = 18,
                    CardFillColor = clrCardBg,
                    CardBorderColor = isOver ? overCardBorder : clrBorderLight,
                    CardBorderThickness = 1,
                    ElevationLevel = 1,
                    BackColor = clrAppBg
                };

                Rectangle cInner = catCard.InnerCardBounds;
                var iconBadge = MakeSoftIconBadge(
                    GetCategoryIcon(catName), cInner.Left + 12, cInner.Top + 16, 38, 12,
                    badgeBg,
                    catAccent);
                catCard.Controls.Add(iconBadge);

                string cleanCatName = GetCategoryCleanName(catName);
                var lblCatName = MakeLabel(cleanCatName, cInner.Left + 58, cInner.Top + 12, 9.5F, FontStyle.Bold, clrTextMain, 140);

                int rawPct = limit > 0
                    ? (int)Math.Round(spent * 100.0 / limit, MidpointRounding.AwayFromZero)
                    : (spent > 0 ? 100 : 0);
                string pctTxt = $"{rawPct}%";
                var pctPill = new CategoryPercentPill
                {
                    Text = pctTxt,
                    Font = FontBold(7.5F),
                    ForeColor = pillInk,
                    BackColor = clrCardBg,
                    PillFillColor = badgeBg,
                    PillBorderColor = pillBorder,
                    Height = 20
                };
                int pctPillW = Math.Max(34, TextRenderer.MeasureText(pctTxt, pctPill.Font, new Size(120, 24), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + 14);
                pctPill.Width = pctPillW;

                string spentFullText = $"Đã tiêu: {spent:N0}đ / {limit:N0}đ";
                string spentCompactText = $"{spent:N0}đ / {limit:N0}đ";
                var lblSpent = MakeLabel(spentFullText, cInner.Left + 58, cInner.Top + 39, 8.2F, FontStyle.Bold, isOver ? clrRedDanger : pillInk, 170);

                int pct = limit > 0 ? Math.Min(100, (int)Math.Round(spent * 100.0 / limit, MidpointRounding.AwayFromZero)) : (spent > 0 ? 100 : 0);
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(cInner.Left + 12, cInner.Top + 78),
                    Size = new Size(Math.Max(80, cInner.Width - limBtnW - 36), 12),
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = clrAppBg,
                    ProgressColor = catAccent,
                    ProgressColor2 = prgEnd,
                    UseTransparentBackground = true
                };

                var txtLim = MakeTextBox(limit.ToString("N0"), "Hạn mức", cInner.Right - limBtnW - 12, cInner.Top + 14, limBtnW, 36, clrCardBg, true);
                txtLim.TextAlign = HorizontalAlignment.Right;

                var btnSetLim = MakeBtn("Lưu hạn mức", cInner.Right - limBtnW - 12, cInner.Top + 56, limBtnW, 36, 12, clrSoftGreenBg, softTextInk, clrSoftGreenBorder, 1, 8.3F);
                btnSetLim.HoverState.FillColor = clrPrimaryGreen;
                btnSetLim.HoverState.ForeColor = Color.White;
                btnSetLim.HoverState.BorderColor = clrPrimaryGreen;
                btnSetLim.Click += async (s, e) =>
                {
                    if (int.TryParse(txtLim.Text.Replace(".", "").Replace(",", "").Trim(), out int nLim) && nLim > 0)
                    {
                        categoryBudgets[catName] = nLim;
                        await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPDATE_CATEGORY_BUDGET_REQ, new UpdateCategoryBudgetRequestDto
                        {
                            UserId = currentUserId,
                            Category = catName,
                            NewLimit = nLim
                        }));
                        RefreshPaneContent("EXPENSE");
                    }
                };

                void LayoutCatCard()
                {
                    if (catCard.IsDisposed) return;
                    Rectangle inner = catCard.InnerCardBounds;
                    if (inner.Width < 200) return;

                    iconBadge.Location = new Point(inner.Left + 12, inner.Top + 16);

                    int rightCtrlX = inner.Right - limBtnW - 12;
                    txtLim.SetBounds(rightCtrlX, inner.Top + 14, limBtnW, 36);
                    btnSetLim.SetBounds(rightCtrlX, inner.Top + 56, limBtnW, 36);

                    int textLeft = inner.Left + 58;
                    int midAvailW = Math.Max(80, rightCtrlX - textLeft - 4);
                    int maxNameW = Math.Max(40, midAvailW - pctPillW - 5);
                    int measuredNameW = TextRenderer.MeasureText(
                        cleanCatName,
                        lblCatName.Font,
                        new Size(400, 40),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width + 4;
                    int nameW = Math.Min(maxNameW, measuredNameW);

                    lblCatName.SetBounds(textLeft, inner.Top + 12, nameW, lblCatName.Height);
                    int pillY = lblCatName.Top + Math.Max(0, (lblCatName.Height - pctPill.Height) / 2 - 1);
                    pctPill.SetBounds(lblCatName.Right + 5, pillY, pctPillW, 20);

                    int fullSpentW = TextRenderer.MeasureText(
                        spentFullText,
                        lblSpent.Font,
                        new Size(400, 30),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix).Width;
                    lblSpent.Text = fullSpentW <= midAvailW ? spentFullText : spentCompactText;
                    lblSpent.SetBounds(textLeft, inner.Top + 39, midAvailW, lblSpent.Height);
                    prg.SetBounds(inner.Left + 12, inner.Top + 78, Math.Max(80, rightCtrlX - (inner.Left + 12) - 10), 12);
                }

                catCard.Controls.AddRange(new Control[] { lblCatName, pctPill, lblSpent, prg, txtLim, btnSetLim });
                catCard.Resize += (s, e) => LayoutCatCard();
                LayoutCatCard();
                col.Controls.Add(catCard);
                stackRelayoutActions.Add((catCard, 8, curW =>
                {
                    catCard.Width = curW;
                    LayoutCatCard();
                }));
                y += catCard.Height + 8;
            }

            y += 4;
            var btnExportCsv = MakeBtn("Xuất báo cáo chi tiêu (.CSV / Excel)", 4, y, Math.Max(180, w - 8), 48, 16, clrCardBg, softTextInk, clrSoftGreenBorder, 1, 8.8F);
            btnExportCsv.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            btnExportCsv.HoverState.FillColor = clrSoftGreenBg;
            btnExportCsv.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnExportCsv, UiIcon.Receipt, accentText, 17);
            AttachControlImageDisposal(btnExportCsv);
            AttachEdgeResizeRedirector(btnExportCsv);

            var lblExportStatus = MakeLabel("", 6, y + 54, 8.2F, FontStyle.Regular, clrTextMuted, Math.Max(160, w - 12));
            lblExportStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            btnExportCsv.Click += (s, e) =>
            {
                using SaveFileDialog sfd = new SaveFileDialog
                {
                    Title = "Lưu báo cáo chi tiêu PocketLog",
                    Filter = "CSV (Mở bằng Excel)|*.csv",
                    FileName = $"PocketLog_BaoCao_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string CsvEsc(string val)
                    {
                        string text = (val ?? "").Replace("\r", " ").Replace("\n", " ");
                        return $"\"{text.Replace("\"", "\"\"")}\"";
                    }

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("BÁO CÁO CHI TIÊU CÁ NHÂN & CÔNG NỢ - POCKETLOG");
                    sb.AppendLine($"Người dùng,{CsvEsc(currentFullName)}");
                    sb.AppendLine($"Ngày xuất,{DateTime.Now:dd/MM/yyyy HH:mm}");
                    sb.AppendLine($"Tổng chi tiêu cá nhân,{grandTotal}");
                    sb.AppendLine($"Hạn mức tháng,{totalBudgetLimit}");
                    sb.AppendLine($"Bạn đang nợ bạn bè,{totalIOwe}");
                    sb.AppendLine($"Bạn bè đang nợ bạn,{totalOwedToMe}");
                    sb.AppendLine();

                    sb.AppendLine("1. CHI TIÊU THEO DANH MỤC");
                    sb.AppendLine("Danh mục,Đã tiêu cá nhân (VNĐ),Hạn mức (VNĐ),Tình trạng");
                    foreach (var cat in standardCategories)
                    {
                        int cSpent = CalculatePersonalSpentByCategory(cat.Key);
                        int cLimit = categoryBudgets.ContainsKey(cat.Key) ? categoryBudgets[cat.Key] : 500000;
                        string cState = cSpent > cLimit ? "Vượt hạn mức" : "Trong hạn mức";
                        sb.AppendLine($"{CsvEsc(GetCategoryCleanName(cat.Key))},{cSpent},{cLimit},{CsvEsc(cState)}");
                    }
                    sb.AppendLine();

                    sb.AppendLine("2. LỊCH SỬ BÀI ĐĂNG CHI TIÊU & CHIA TIỀN");
                    sb.AppendLine("Thời gian,Khoản chi,Danh mục,Tổng hóa đơn (VNĐ),Vai trò / Ghi chú");
                    foreach (var p in posts.Where(p => p.IsMine && p.HasTag))
                    {
                        string tagNote = (p.TaggedFriends != null && p.TaggedFriends.Count > 0)
                            ? $"Bạn trả trước (Tag: {string.Join("; ", p.TaggedFriends)})"
                            : "Chi tiêu cá nhân";
                        sb.AppendLine($"{CsvEsc(p.Time)},{CsvEsc(p.NoteTop)},{CsvEsc(GetCategoryCleanName(p.Category))},{p.Amount},{CsvEsc(tagNote)}");
                    }
                    sb.AppendLine();

                    sb.AppendLine("3. CHI TIẾT CÔNG NỢ CHIA HÓA ĐƠN");
                    sb.AppendLine("Thời gian,Khoản chia,Chủ chi,Người nợ,Phần chia (VNĐ),Trạng thái");
                    foreach (var d in taggedDebts)
                    {
                        string st = d.IsConfirmedPaid ? "Đã tất toán" : (d.IsSentProof ? "Chờ xác nhận" : "Chưa thanh toán");
                        sb.AppendLine($"{CsvEsc(d.Time)},{CsvEsc(d.Title)},{CsvEsc(d.CreditorName)},{CsvEsc(d.DebtorName)},{d.MyShare},{CsvEsc(st)}");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
                    lblExportStatus.Text = $"✓ Đã xuất file: {Path.GetFileName(sfd.FileName)}";
                    lblExportStatus.ForeColor = positiveColor;
                }
                catch (Exception ex)
                {
                    lblExportStatus.Text = "Lỗi xuất file: " + ex.Message;
                    lblExportStatus.ForeColor = clrRedDanger;
                }
            };

            col.Controls.Add(btnExportCsv);
            col.Controls.Add(lblExportStatus);
            stackRelayoutActions.Add((btnExportCsv, 6, curW =>
            {
                btnExportCsv.Left = 4;
                btnExportCsv.Width = Math.Max(180, curW - 8);
            }));
            stackRelayoutActions.Add((lblExportStatus, 14, curW =>
            {
                lblExportStatus.Left = 6;
                lblExportStatus.Width = Math.Max(160, curW - 12);
            }));

            bool relayoutingExpense = false;
            void RelayoutExpenseStack()
            {
                if (relayoutingExpense || col.IsDisposed || page.IsDisposed) return;
                int curW = col.Width;
                if (curW < 200) return;

                relayoutingExpense = true;
                try
                {
                    int curY = 10;
                    foreach (var item in stackRelayoutActions)
                    {
                        if (item.Ctrl.IsDisposed) continue;
                        item.Ctrl.Top = curY;
                        item.OnLayout(curW);
                        curY = item.Ctrl.Bottom + item.GapAfter;
                    }

                    if (col.Height != curY)
                    {
                        col.Height = curY;
                    }
                    (page as AndroidOverlayScrollPanel)?.SyncLayout();
                }
                finally
                {
                    relayoutingExpense = false;
                }
            }

            col.Resize += (s, e) => RelayoutExpenseStack();
            RelayoutExpenseStack();
            return page;
        }

        private string GetCategoryEmoji(string category)
        {
            foreach (var c in standardCategories)
            {
                if (c.Key == category || c.ShortLabel == category) return c.Emoji;
            }
            return "📷";
        }

        private string ImageToBase64(Image img)
        {
            if (img == null) return "";
            try
            {
                int maxSide = 600;
                int w = img.Width, h = img.Height;
                if (w > maxSide || h > maxSide)
                {
                    if (w > h) { h = (int)((double)h * maxSide / w); w = maxSide; }
                    else { w = (int)((double)w * maxSide / h); h = maxSide; }
                }
                using Bitmap resized = new Bitmap(img, new Size(Math.Max(1, w), Math.Max(1, h)));
                using MemoryStream ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Jpeg);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return "";
            }
        }

        private Image Base64ToImage(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using MemoryStream ms = new MemoryStream(bytes);
                using Image temp = Image.FromStream(ms);
                return new Bitmap(temp);
            }
            catch
            {
                return null;
            }
        }
    }
}