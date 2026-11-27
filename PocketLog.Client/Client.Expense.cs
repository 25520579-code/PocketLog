using System;
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

            public WalletGradientPanel(Client owner)
            {
                this.owner = owner;
                DoubleBuffered = true;
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

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                base.OnPaintBackground(e);
                if (Width <= 4 || Height <= 4) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var rect = new RectangleF(0.5f, 0.5f, Math.Max(2f, Width - 1f), Math.Max(2f, Height - 1f));
                using GraphicsPath path = CreateRoundedPath(rect.X, rect.Y, rect.Width, rect.Height, BorderRadius);
                using var grad = new LinearGradientBrush(rect, FillColor, FillColor2, LinearGradientMode.ForwardDiagonal);
                grad.WrapMode = WrapMode.TileFlipXY;
                g.FillPath(grad, path);

                PaintDecorativeGlows(g, path);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (Width <= 4 || Height <= 4) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                var rect = new RectangleF(0.5f, 0.5f, Math.Max(2f, Width - 1f), Math.Max(2f, Height - 1f));
                using GraphicsPath path = CreateRoundedPath(rect.X, rect.Y, rect.Width, rect.Height, BorderRadius);

                PaintDecorativeGlows(g, path);

                GraphicsState saved = g.Save();
                g.SetClip(path, CombineMode.Intersect);

                if (dividerX > 40 && dividerX < Width - 40)
                {
                    using var divPen = new Pen(Color.FromArgb(68, 255, 255, 255), 1f);
                    g.DrawLine(divPen, dividerX, 16f, dividerX, Height - 16f);
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

                g.Restore(saved);

                using var sheenPen = new Pen(Color.FromArgb(42, 255, 255, 255), 1f);
                g.DrawPath(sheenPen, path);
            }

            private void PaintDecorativeGlows(Graphics g, GraphicsPath clipPath)
            {
                GraphicsState saved = g.Save();
                g.SetClip(clipPath, CombineMode.Intersect);
                using (var glowTop = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
                using (var glowBot = new SolidBrush(Color.FromArgb(12, 255, 255, 255)))
                {
                    g.FillEllipse(glowTop, Width - 88, -32, 118, 118);
                    g.FillEllipse(glowBot, -24, Height - 44, 92, 92);
                }
                g.Restore(saved);
            }
        }

        private static Guna2Panel AttachSoftBadgeDisposal(Guna2Panel badge)
        {
            if (badge == null) return null;
            foreach (PictureBox pic in badge.Controls.OfType<PictureBox>().ToList())
            {
                Image capturedImg = pic.Image;
                pic.Disposed += (s, e) =>
                {
                    try
                    {
                        Image current = pic.Image;
                        pic.Image = null;
                        current?.Dispose();
                    }
                    catch { }
                    try { capturedImg?.Dispose(); } catch { }
                };
            }
            return badge;
        }

        private static void AttachControlImageDisposal(Control ctrl, Func<Image> getImg, Action<Image> setImg)
        {
            if (ctrl == null) return;
            ctrl.Disposed += (s, e) =>
            {
                try
                {
                    Image current = getImg();
                    setImg(null);
                    current?.Dispose();
                }
                catch { }
            };
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
            int y = 12;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalIOwe = unpaidList.Where(d => d.DebtorId == currentUserId).Sum(d => d.MyShare);
            int totalOwedToMe = unpaidList.Where(d => d.CreditorId == currentUserId).Sum(d => d.MyShare);

            TaggedDebtItem currentDebt = taggedDebts.FirstOrDefault(d => d.SplitId == selectedSplitId)
                                         ?? unpaidList.FirstOrDefault()
                                         ?? taggedDebts.FirstOrDefault();

            // Thẻ tổng quan công nợ Gradient chéo 88px (WalletGradStart -> WalletGradEnd)
            var pnlBanner = new WalletGradientPanel(this)
            {
                Location = new Point(0, y),
                Size = new Size(w, 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = theme.WalletGradStart,
                FillColor2 = theme.WalletGradEnd,
                GradientMode = LinearGradientMode.ForwardDiagonal,
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                UnpaidCount = unpaidList.Count,
                UnpaidDotColor = theme.Danger,
                Cursor = unpaidList.Count > 0 ? Cursors.Hand : Cursors.Default
            };
            AttachEdgeResizeRedirector(pnlBanner);

            Color bannerTitleColor = Color.FromArgb(214, 222, 255);
            Color oweValueColor = totalIOwe > 0 ? Color.FromArgb(254, 205, 211) : Color.White;
            Color owedValueColor = totalOwedToMe > 0 ? Color.FromArgb(167, 243, 208) : Color.White;

            var lblOweTitle = MakeLabel("Bạn đang nợ", 18, 15, 8.2F, FontStyle.Regular, bannerTitleColor, 150);
            var lblOweVal = MakeLabel($"{totalIOwe:N0}đ", 18, 41, 12.2F, FontStyle.Bold, oweValueColor, 150);
            lblOweVal.Font = FontBold(12.2F);
            lblOweVal.Height = TextRenderer.MeasureText("Ág", lblOweVal.Font).Height + 4;

            var lblOwedTitle = MakeLabel("Bạn bè nợ bạn", 200, 15, 8.2F, FontStyle.Regular, bannerTitleColor, 150);
            var lblOwedVal = MakeLabel($"{totalOwedToMe:N0}đ", 200, 41, 12.2F, FontStyle.Bold, owedValueColor, 150);
            lblOwedVal.Font = FontBold(12.2F);
            lblOwedVal.Height = TextRenderer.MeasureText("Ág", lblOwedVal.Font).Height + 4;

            void LayoutBanner()
            {
                if (pnlBanner.IsDisposed) return;
                int bw = pnlBanner.Width;
                if (bw <= 120) return;

                double splitRatio = bw < 450 ? 0.415 : 0.465;
                int minDiv = Math.Min(120, bw / 2);
                int maxDiv = Math.Max(minDiv, bw - 120);
                int divX = Math.Clamp((int)Math.Round(bw * splitRatio), minDiv, maxDiv);
                int leftW = Math.Max(64, divX - 28);
                int rightX = divX + 12;
                int rightMaxW = Math.Max(64, bw - rightX - 14);

                lblOweTitle.Location = new Point(18, 15);
                lblOweTitle.Width = leftW;
                lblOweVal.Location = new Point(18, 41);
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

                    pillBounds = new Rectangle(bw - 14 - chosenPillW, 12, chosenPillW, 22);
                    owedTitleWidth = Math.Max(68, pillBounds.Left - rightX - 4);
                }

                lblOwedTitle.Location = new Point(rightX, 15);
                lblOwedTitle.Width = owedTitleWidth;
                lblOwedVal.Location = new Point(rightX, 41);
                lblOwedVal.Width = rightMaxW;

                pnlBanner.UpdateBadgeLayout(divX, pillBounds, badgeLabel);
            }

            pnlBanner.Controls.AddRange(new Control[] { lblOweTitle, lblOweVal, lblOwedTitle, lblOwedVal });
            pnlBanner.Resize += (s, e) => LayoutBanner();
            if (unpaidList.Count > 0)
            {
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
            y += 102;

            if (currentDebt != null)
            {
                Guna2Panel cardActiveDebt = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 268),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 22,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };
                AttachEdgeResizeRedirector(cardActiveDebt);

                bool isDebtor = (currentDebt.DebtorId == currentUserId);
                string roleDesc = isDebtor
                    ? $"Bạn nợ {currentDebt.CreditorName}"
                    : $"{currentDebt.DebtorName} nợ bạn";

                var lblDebtTitle = MakeLabel($"Khoản chia: {currentDebt.Title}", 16, 12, 10.2F, FontStyle.Bold, clrTextMain, w - 32);
                lblDebtTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                var lblDebtRole = MakeLabel(
                    $"{roleDesc}  •  Phần chia: {currentDebt.MyShare:N0}đ",
                    16, 40, 9.2F, FontStyle.Bold,
                    isDebtor ? clrRedDanger : positiveColor,
                    w - 32);
                lblDebtRole.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

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
                var badgeState = MakeBtn(stateTxt, 16, 68, stateBadgeW, 30, 15, stateBg, stateFore, stateBorder, 1, 8.2F);
                badgeState.Cursor = Cursors.Default;
                badgeState.TabStop = false;
                badgeState.HoverState.FillColor = stateBg;
                badgeState.HoverState.BorderColor = stateBorder;

                int noteLeft = badgeState.Right + 10;
                Label lblNote = MakeLabel(currentDebt.StatusNote, noteLeft, 72, 8.2F, FontStyle.Regular, clrTextMuted, Math.Max(80, w - noteLeft - 16));
                lblNote.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                cardActiveDebt.Controls.AddRange(new Control[] { lblDebtTitle, lblDebtRole, badgeState, lblNote });

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(16, 108),
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
                var emptyProofBadge = AttachSoftBadgeDisposal(MakeSoftIconBadge(UiIcon.Receipt, 47, 26, 38, 12, clrSoftGreenBg, accentText));
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

                var btnUploadProof = MakeBtn("Gửi ảnh bill CK", 160, 108, Math.Max(140, w - 176), 66, 16, clrSoftGreenBg, softTextInk, clrSoftGreenBorder, 1, 8.5F);
                btnUploadProof.HoverState.FillColor = clrPrimaryGreen;
                btnUploadProof.HoverState.ForeColor = Color.White;
                btnUploadProof.HoverState.BorderColor = clrPrimaryGreen;
                AttachControlImageDisposal(btnUploadProof, () => btnUploadProof.Image, img => btnUploadProof.Image = img);

                var btnFriendVerify = new Guna2GradientButton
                {
                    Text = "Xác nhận hoàn thành",
                    Location = new Point(160, 186),
                    Size = new Size(Math.Max(140, w - 176), 66),
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
                AttachControlImageDisposal(btnFriendVerify, () => btnFriendVerify.Image, img => btnFriendVerify.Image = img);

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
                    int cw = cardActiveDebt.Width;
                    if (cw < 180) return;

                    lblDebtTitle.Width = Math.Max(100, cw - 32);
                    lblDebtRole.Width = Math.Max(100, cw - 32);

                    int nLeft = badgeState.Right + 10;
                    lblNote.Left = nLeft;
                    lblNote.Width = Math.Max(70, cw - nLeft - 16);

                    int imgW = cw < 440 ? 124 : 148;
                    pnlBillImg.SetBounds(16, 108, imgW, 144);
                    emptyProofBadge.Left = (imgW - emptyProofBadge.Width) / 2;
                    lblEmptyProof.Size = new Size(Math.Max(60, imgW - 12), 56);

                    int rightX = pnlBillImg.Right + 12;
                    int rightW = Math.Max(120, cw - rightX - 16);
                    btnUploadProof.SetBounds(rightX, 108, rightW, 66);
                    btnFriendVerify.SetBounds(rightX, 186, rightW, 66);

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
                y += 282;
            }
            else
            {
                Guna2Panel compactEmpty = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 96),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 20,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };
                AttachEdgeResizeRedirector(compactEmpty);
                compactEmpty.Controls.Add(AttachSoftBadgeDisposal(MakeSoftIconBadge(UiIcon.Receipt, 16, 25, 44, 14, clrSoftGreenBg, accentText)));
                var lEmpty1 = MakeLabel("Chưa có khoản chia tiền nào được gắn tag", 72, 16, 8.4F, FontStyle.Bold, clrTextMain, w - 88);
                lEmpty1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                var lEmpty2 = MakeLabel("Gắn tag bạn bè lúc đăng ảnh chi tiêu để tự động chia hóa đơn", 72, lEmpty1.Bottom + 4, 8.2F, FontStyle.Regular, clrTextMuted, w - 88, lines: 2);
                lEmpty2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                compactEmpty.Height = Math.Max(96, lEmpty2.Bottom + 14);
                compactEmpty.Controls.Add(lEmpty1);
                compactEmpty.Controls.Add(lEmpty2);
                col.Controls.Add(compactEmpty);
                y += compactEmpty.Height + 12;
            }

            var btnToggleUnpaid = MakeBtn(
                $"Các khoản chi gắn tag chưa tất toán ({unpaidList.Count})",
                0, y, w, 46, 16, clrSoftGreenBg, softTextInk, clrSoftGreenBorder, 1, 8.2F);
            btnToggleUnpaid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnToggleUnpaid.HoverState.FillColor = clrCardBg;
            btnToggleUnpaid.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnToggleUnpaid, isUnpaidDropdownOpen ? UiIcon.ChevronUp : UiIcon.ChevronDown, accentText, 15);
            AttachControlImageDisposal(btnToggleUnpaid, () => btnToggleUnpaid.Image, img => btnToggleUnpaid.Image = img);
            AttachEdgeResizeRedirector(btnToggleUnpaid);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                RefreshPaneContent("EXPENSE");
            };
            col.Controls.Add(btnToggleUnpaid);
            y += 54;

            if (isUnpaidDropdownOpen)
            {
                if (unpaidList.Count == 0)
                {
                    Color paidBg = isDarkMode ? Color.FromArgb(16, 62, 46) : clrSoftGreenBg;
                    Color paidBorder = isDarkMode ? Color.FromArgb(32, 110, 82) : clrSoftGreenBorder;
                    Guna2Panel emptyBox = new Guna2Panel
                    {
                        Location = new Point(0, y),
                        Size = new Size(w, 58),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        BorderRadius = 16,
                        FillColor = clrCardBg,
                        BorderColor = paidBorder,
                        BorderThickness = 1,
                        UseTransparentBackground = true
                    };
                    AttachEdgeResizeRedirector(emptyBox);
                    emptyBox.Controls.Add(AttachSoftBadgeDisposal(MakeSoftIconBadge(UiIcon.Check, 14, 14, 30, 10, paidBg, positiveColor)));
                    var lblAllPaid = MakeLabel("Tất cả các khoản chia tiền đều đã tất toán xong!", 54, 10, 8.4F, FontStyle.Bold, positiveColor, w - 70, lines: 2);
                    lblAllPaid.TextAlign = ContentAlignment.MiddleLeft;
                    lblAllPaid.Height = emptyBox.Height - 20;
                    lblAllPaid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    emptyBox.Controls.Add(lblAllPaid);
                    col.Controls.Add(emptyBox);
                    y += 66;
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

                        Guna2Panel itemRow = new Guna2Panel
                        {
                            Location = new Point(0, y),
                            Size = new Size(w, 94),
                            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                            BorderRadius = 18,
                            FillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                            BorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                            BorderThickness = 1,
                            UseTransparentBackground = true
                        };
                        AttachEdgeResizeRedirector(itemRow);

                        int textMaxW = Math.Max(96, w - selBtnW - 36);
                        var r1 = MakeLabel($"{GetCategoryShortClean(debt.Category)}  •  {debt.Title}", 16, 10, 9.2F, FontStyle.Bold, clrTextMain, textMaxW);
                        r1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        var r2 = MakeLabel($"{roleTag}  •  {debt.MyShare:N0}đ", 16, 37, 8.8F, FontStyle.Bold, rowIsDebtor ? clrRedDanger : positiveColor, textMaxW);
                        r2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        var r3 = MakeLabel($"{debt.Time}  •  {(debt.IsSentProof ? "Đã gửi bill" : "Chưa gửi bill")}", 16, 64, 8.2F, FontStyle.Regular, clrTextMuted, textMaxW);
                        r3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                        var btnSelectDebt = MakeBtn(
                            isSelected ? "Đang chọn" : (rowIsDebtor ? "Trả ngay" : "Kiểm tra"),
                            w - selBtnW - 14, 27, selBtnW, 40, 20,
                            isSelected ? clrPrimaryGreen : clrInputBg,
                            isSelected ? Color.White : (isDarkMode ? clrTextMain : clrIndigo),
                            isSelected ? clrPrimaryGreen : clrBorderLight, 1, 8.4F);
                        btnSelectDebt.HoverState.FillColor = isSelected ? clrPrimaryGreen : clrSoftGreenBg;
                        btnSelectDebt.HoverState.BorderColor = clrPrimaryGreen;
                        btnSelectDebt.Anchor = AnchorStyles.Top | AnchorStyles.Right;

                        btnSelectDebt.Click += (s, e) =>
                        {
                            selectedSplitId = debt.SplitId;
                            RefreshPaneContent("EXPENSE");
                        };

                        itemRow.Controls.AddRange(new Control[] { r1, r2, r3, btnSelectDebt });
                        col.Controls.Add(itemRow);
                        y += 102;
                    }
                }
            }

            y += 8;
            var lblSecCat = MakeLabel("Chi tiêu thực tế theo danh mục", 2, y, 11F, FontStyle.Bold, clrTextMain, w - 8);
            lblSecCat.Font = FontBold(11F);
            lblSecCat.Height = TextRenderer.MeasureText("Ág", lblSecCat.Font).Height + 4;
            lblSecCat.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            col.Controls.Add(lblSecCat);
            y += 36;

            int limBtnW = Math.Max(126, MeasurePillBtnWidth("Lưu hạn mức", 8.3F, 12));

            foreach (var cat in standardCategories)
            {
                string catName = cat.Key;
                int spent = CalculatePersonalSpentByCategory(catName);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;
                bool isOver = spent > limit;

                Guna2Panel catCard = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 106),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 18,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };
                AttachEdgeResizeRedirector(catCard);

                var iconBadge = AttachSoftBadgeDisposal(MakeSoftIconBadge(
                    GetCategoryIcon(catName), 14, 15, 40, 13,
                    isOver ? clrSoftRedBg : clrSoftGreenBg,
                    isOver ? clrRedDanger : accentText));
                catCard.Controls.Add(iconBadge);

                int leftColW = Math.Max(90, w - 64 - limBtnW - 24);
                var lblCatName = MakeLabel(GetCategoryCleanName(catName), 64, 12, 9.8F, FontStyle.Bold, clrTextMain, leftColW);
                lblCatName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                var lblSpent = MakeLabel($"Đã tiêu: {spent:N0}đ / {limit:N0}đ", 64, 39, 8.2F, FontStyle.Bold, isOver ? clrRedDanger : accentText, leftColW);
                lblSpent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                int pct = limit > 0 ? Math.Min(100, (int)((long)spent * 100 / limit)) : 100;
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(14, 78),
                    Size = new Size(Math.Max(80, w - limBtnW - 40), 12),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = clrAppBg,
                    ProgressColor = isOver ? clrRedDanger : theme.AccentPrimary,
                    ProgressColor2 = isOver ? Color.OrangeRed : theme.AccentSecondary,
                    UseTransparentBackground = true
                };

                var txtLim = MakeTextBox(limit.ToString("N0"), "Hạn mức", w - limBtnW - 14, 14, limBtnW, 36, clrCardBg, true);
                txtLim.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                txtLim.TextAlign = HorizontalAlignment.Right;

                var btnSetLim = MakeBtn("Lưu hạn mức", w - limBtnW - 14, 56, limBtnW, 36, 12, clrSoftGreenBg, softTextInk, clrSoftGreenBorder, 1, 8.3F);
                btnSetLim.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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

                catCard.Controls.AddRange(new Control[] { lblCatName, lblSpent, prg, txtLim, btnSetLim });
                col.Controls.Add(catCard);
                y += 116;
            }

            int grandTotal = CalculateTotalPersonalSpent();
            bool isTotalOver = grandTotal > totalBudgetLimit;

            Guna2Panel cardTotal = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 162),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrSoftGreenBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            AttachEdgeResizeRedirector(cardTotal);

            var lblTotalHeader = MakeLabel("TỔNG CHI TIÊU CÁ NHÂN TRONG THÁNG", 18, 12, 9.2F, FontStyle.Bold, accentText, w - 36);
            lblTotalHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardTotal.Controls.Add(lblTotalHeader);

            var lblGrand = MakeLabel($"{grandTotal:N0}đ   /   Hạn mức: {totalBudgetLimit:N0}đ", 18, 38, 11F, FontStyle.Bold, isTotalOver ? clrRedDanger : clrTextMain, w - 36);
            lblGrand.Font = FontBold(11F);
            lblGrand.Height = TextRenderer.MeasureText("Ág", lblGrand.Font).Height + 4;
            lblGrand.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardTotal.Controls.Add(lblGrand);

            int totalPct = totalBudgetLimit > 0 ? Math.Min(100, (int)((long)grandTotal * 100 / totalBudgetLimit)) : 100;
            Guna2ProgressBar prgTotal = new Guna2ProgressBar
            {
                Location = new Point(18, 72),
                Size = new Size(w - 36, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 7,
                Value = totalPct,
                FillColor = theme.CardBg,
                ProgressColor = isTotalOver ? clrRedDanger : theme.AccentPrimary,
                ProgressColor2 = isTotalOver ? Color.OrangeRed : theme.AccentSecondary,
                UseTransparentBackground = true
            };

            int saveTotalBtnW = Math.Max(158, MeasurePillBtnWidth("Lưu tổng hạn mức", 8.5F, 14));
            var totalActionGrid = new BufferedTableLayout
            {
                Location = new Point(16, 98),
                Size = new Size(w - 32, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            totalActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            totalActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, saveTotalBtnW));
            totalActionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtTotalIn = MakeTextBox(totalBudgetLimit.ToString("N0"), "Tổng hạn mức", 0, 0, 140, 42, clrSoftGreenBg, true);
            txtTotalIn.Dock = DockStyle.Fill;
            txtTotalIn.FillColor = theme.InputBg;
            txtTotalIn.FocusedState.FillColor = theme.CardBg;
            txtTotalIn.BorderColor = clrSoftGreenBorder;
            txtTotalIn.Margin = new Padding(2, 1, 6, 1);

            var btnSetTotal = new Guna2GradientButton
            {
                Text = "Lưu tổng hạn mức",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 1, 2, 1),
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

            cardTotal.Controls.Add(prgTotal);
            cardTotal.Controls.Add(totalActionGrid);
            col.Controls.Add(cardTotal);
            y += 176;

            var btnExportCsv = MakeBtn("Xuất báo cáo chi tiêu (.CSV / Excel)", 0, y, w, 48, 16, clrCardBg, softTextInk, clrSoftGreenBorder, 1, 8.8F);
            btnExportCsv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnExportCsv.HoverState.FillColor = clrSoftGreenBg;
            btnExportCsv.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnExportCsv, UiIcon.Receipt, accentText, 17);
            AttachControlImageDisposal(btnExportCsv, () => btnExportCsv.Image, img => btnExportCsv.Image = img);
            AttachEdgeResizeRedirector(btnExportCsv);

            var lblExportStatus = MakeLabel("", 4, y + 54, 8.2F, FontStyle.Regular, clrTextMuted, w - 8);
            lblExportStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

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
            y += 80;

            col.Height = y;
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