using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace PocketLog.Client
{
    public partial class Client
    {
        private static readonly Color[] FeedAvatarTints =
        {
            Color.FromArgb(79, 70, 229),
            Color.FromArgb(14, 165, 233),
            Color.FromArgb(236, 72, 153),
            Color.FromArgb(245, 158, 11),
            Color.FromArgb(16, 185, 129),
            Color.FromArgb(139, 92, 246)
        };

        private sealed class FeedBufferedPanel : Panel
        {
            public FeedBufferedPanel()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);
                DoubleBuffered = true;
                BackColor = Color.Transparent;
            }
        }

        private Control BuildNewFeedScreen() => BuildFeedScreen();

        private Control BuildFeedScreen()
        {
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 14;

            Panel topBar = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            AttachEdgeResizeRedirector(topBar);

            int myPhotoCount = posts.Count(p => p.IsMine);
            string galText = myPhotoCount > 0 ? $"Kho ảnh ({myPhotoCount})" : "Kho ảnh";
            string frText = $"Bạn bè ({myFriends.Count})";

            int frBtnW = Math.Max(114, MeasurePillBtnWidth(frText, 8.2F, 18, true, 15));
            var btnFriendsTop = MakeBtn(frText, w - frBtnW, 3, frBtnW, 36, 18, clrCardBg, clrIndigo, clrBorderLight, 1, 8.2F);
            btnFriendsTop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnFriendsTop.HoverState.FillColor = clrSoftGreenBg;
            btnFriendsTop.HoverState.BorderColor = clrSoftGreenBorder;
            AttachBtnIcon(btnFriendsTop, UiIcon.Users, clrIndigo, 15);
            AttachEdgeResizeRedirector(btnFriendsTop);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            int galBtnW = Math.Max(104, MeasurePillBtnWidth(galText, 8.2F, 18, true, 15));
            var btnGallery = MakeBtn(galText, w - frBtnW - 6 - galBtnW, 3, galBtnW, 36, 18, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.2F);
            btnGallery.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGallery.HoverState.FillColor = clrCardBg;
            btnGallery.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnGallery, UiIcon.Photo, clrPrimaryGreen, 15);
            AttachEdgeResizeRedirector(btnGallery);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var lblTitle = MakeLabel("Bảng tin", 2, 5, 12.2F, FontStyle.Bold, clrTextMain, Math.Max(70, btnGallery.Left - 8));
            lblTitle.Font = FontBold(12.2F);
            lblTitle.Height = TextRenderer.MeasureText("Ág", lblTitle.Font).Height + 4;
            lblTitle.Top = Math.Max(2, (42 - lblTitle.Height) / 2);
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            void LayoutTopBar()
            {
                if (topBar.IsDisposed) return;
                int tw = topBar.Width;
                int btnGap = tw < 440 ? 6 : 8;
                btnFriendsTop.SetBounds(tw - frBtnW, 3, frBtnW, 36);
                btnGallery.SetBounds(btnFriendsTop.Left - btnGap - galBtnW, 3, galBtnW, 36);
                lblTitle.Width = Math.Max(60, btnGallery.Left - 8);
            }

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(btnGallery);
            topBar.Controls.Add(btnFriendsTop);
            topBar.Resize += (s, e) => LayoutTopBar();
            LayoutTopBar();
            col.Controls.Add(topBar);
            y += 50;

            var storyStrip = new StoryAvatarStrip(this, myFriends, friendSuggestions.Count, OpenFriendsDrawer)
            {
                Location = new Point(0, y),
                Size = new Size(w, 96),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            col.Controls.Add(storyStrip);
            y += storyStrip.Height + 14;

            LocketPost latestMine = posts.FirstOrDefault(p => p.IsMine);
            if (latestMine != null)
            {
                var hero = CreateLocketPostCard(latestMine, true, w);
                hero.Location = new Point(0, y);
                col.Controls.Add(hero);
                y += hero.Height + 16;
            }
            else
            {
                var emptyMine = CreateEmptyHeroCard(
                    w,
                    useGradientBadge: true,
                    UiIcon.Camera,
                    "Bạn chưa đăng khoảnh khắc nào",
                    "Chạm vào đây để mở Chụp ảnh và chia sẻ ngay",
                    clrSoftGreenBorder,
                    clrPrimaryGreen,
                    () => EnsureTabVisible("CAMERA"));
                emptyMine.Location = new Point(0, y);
                col.Controls.Add(emptyMine);
                y += emptyMine.Height + 14;
            }

            Panel secHeader = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            AttachEdgeResizeRedirector(secHeader);

            var lblSec = MakeLabel("Khoảnh khắc từ bạn bè", 4, 5, 10F, FontStyle.Bold, clrIndigo);
            lblSec.Top = Math.Max(2, (34 - lblSec.Height) / 2);
            secHeader.Controls.Add(lblSec);

            var lineSec = new Panel
            {
                Height = 1,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = clrBorderLight
            };

            void LayoutSecDivider()
            {
                if (secHeader.IsDisposed) return;
                int lx = lblSec.Right + 12;
                lineSec.SetBounds(lx, lblSec.Top + lblSec.Height / 2, Math.Max(16, secHeader.Width - lx - 4), 1);
            }

            secHeader.Resize += (s, e) => LayoutSecDivider();
            secHeader.Controls.Add(lineSec);
            LayoutSecDivider();
            col.Controls.Add(secHeader);
            y += 40;

            var friendPosts = posts.Where(p => !p.IsMine).ToList();
            if (friendPosts.Count == 0)
            {
                var noFriendPostBox = CreateEmptyHeroCard(
                    w,
                    useGradientBadge: false,
                    UiIcon.Users,
                    "Chưa có bài đăng nào từ bạn bè",
                    "Chạm vào đây để kết nối thêm bạn bè mới",
                    clrBorderLight,
                    clrTextMain,
                    OpenFriendsDrawer);
                noFriendPostBox.Location = new Point(0, y);
                col.Controls.Add(noFriendPostBox);
                y += noFriendPostBox.Height + 14;
            }
            else
            {
                foreach (var fp in friendPosts)
                {
                    var card = CreateLocketPostCard(fp, false, w);
                    card.Location = new Point(0, y);
                    col.Controls.Add(card);
                    y += card.Height + 16;
                }
            }

            col.Height = y + 20;
            AttachFeedScrollWidthGuard(page, col);
            return page;
        }

        private Guna2Panel CreateLocketPostCard(LocketPost post, bool isMyTopCard, int cardWidth)
        {
            AppPalette theme = CurrentTheme;
            Guna2Panel card = new Guna2Panel
            {
                Size = new Size(cardWidth, 474),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 28,
                FillColor = clrCardBg,
                BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };
            AttachEdgeResizeRedirector(card);

            FriendUser matchedFriend = FindAuthorFriend(post);
            bool isAuthorOnline = matchedFriend != null && matchedFriend.IsOnline;

            Control avatarHost = BuildMiniAuthorAvatar(post, matchedFriend, isMyTopCard, isAuthorOnline);

            Guna2Panel timePill = null;
            Label lblTime = null;
            string timeText = (post.Time ?? "").Trim();
            if (!string.IsNullOrEmpty(timeText))
            {
                using Font timeMeasureFont = FontMedium(7.8F);
                Size timeSz = TextRenderer.MeasureText(timeText, timeMeasureFont, new Size(260, 40), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                int timeH = Math.Max(22, TextRenderer.MeasureText("Ág", timeMeasureFont).Height + 8);
                int timeW = Math.Clamp(timeSz.Width + 18, 58, 124);

                timePill = new Guna2Panel
                {
                    Location = new Point(Math.Max(140, cardWidth - 14 - timeW), 14),
                    Size = new Size(timeW, timeH),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    BorderRadius = timeH / 2,
                    FillColor = isMyTopCard ? clrSoftGreenBg : clrInputBg,
                    BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
                };
                AttachEdgeResizeRedirector(timePill);

                lblTime = new Label
                {
                    Text = timeText,
                    Dock = DockStyle.Fill,
                    Font = FontMedium(7.8F),
                    ForeColor = isMyTopCard ? clrPrimaryGreen : clrTextMuted,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoEllipsis = true,
                    UseMnemonic = false,
                    Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
                };
                timePill.Controls.Add(lblTime);
                card.Controls.Add(timePill);
            }

            string authorName = string.IsNullOrWhiteSpace(post.Author) ? "Người dùng PocketLog" : post.Author.Trim();
            int initAuthorW = timePill != null
                ? Math.Max(90, timePill.Left - 66 - 8)
                : Math.Max(120, cardWidth - 80);
            var lblAuthor = MakeLabel(authorName, 66, 11, 10F, FontStyle.Bold, clrTextMain, initAuthorW);
            lblAuthor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            string subText = BuildPostHeaderSubtitle(post, isMyTopCard);
            var lblSub = MakeLabel(
                subText,
                66,
                lblAuthor.Bottom - 1,
                8.6F,
                post.HasTag ? FontStyle.Bold : FontStyle.Regular,
                post.HasTag ? clrPrimaryGreen : clrTextMuted,
                Math.Max(120, cardWidth - 80));
            lblSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            void LayoutCardHeader()
            {
                if (card.IsDisposed) return;
                int cw = card.Width;
                if (timePill != null && !timePill.IsDisposed)
                {
                    timePill.Left = Math.Max(140, cw - 14 - timePill.Width);
                    lblAuthor.Width = Math.Max(80, timePill.Left - lblAuthor.Left - 8);
                }
                else
                {
                    lblAuthor.Width = Math.Max(80, cw - lblAuthor.Left - 14);
                }
                lblSub.Width = Math.Max(80, cw - lblSub.Left - 14);
            }

            int photoY = Math.Max(68, lblSub.Bottom + 6);
            const int photoH = 390;
            var photoCanvas = new SocialPhotoCanvas(this, post, isMyTopCard, compactThumbnail: false)
            {
                Location = new Point(12, photoY),
                Size = new Size(Math.Max(180, cardWidth - 24), photoH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            card.Height = photoY + photoH + 14;

            card.Resize += (s, e) => LayoutCardHeader();
            LayoutCardHeader();

            card.Controls.Add(avatarHost);
            card.Controls.Add(lblAuthor);
            card.Controls.Add(lblSub);
            card.Controls.Add(photoCanvas);

            if (isMyTopCard)
            {
                EventHandler openGallery = (s, e) => OpenMyGalleryScreen();
                card.Click += openGallery;
                avatarHost.Click += openGallery;
                lblAuthor.Click += openGallery;
                lblSub.Click += openGallery;
                photoCanvas.Click += openGallery;
                if (timePill != null) timePill.Click += openGallery;
                if (lblTime != null) lblTime.Click += openGallery;
            }

            return card;
        }

        private string BuildPostHeaderSubtitle(LocketPost post, bool isMyTopCard)
        {
            if (post != null && post.HasTag)
            {
                return !string.IsNullOrWhiteSpace(post.NoteTop)
                    ? post.NoteTop.Trim()
                    : GetCategoryShortClean(post.Category);
            }
            return isMyTopCard ? "Khoảnh khắc của bạn • Chạm để mở Kho ảnh" : "Ảnh khoảnh khắc Pocket";
        }

        private FriendUser FindAuthorFriend(LocketPost post)
        {
            if (post == null || post.IsMine) return null;
            string author = (post.Author ?? "").Trim();
            if (string.IsNullOrEmpty(author)) return null;

            return myFriends.FirstOrDefault(f => f != null && (
                string.Equals((f.Name ?? "").Trim(), author, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(f.Handle) && author.Contains(f.Handle, StringComparison.OrdinalIgnoreCase))));
        }

        private Control BuildMiniAuthorAvatar(LocketPost post, FriendUser matchedFriend, bool isMyTopCard, bool isAuthorOnline)
        {
            string author = (post?.Author ?? "").Trim();
            string initial = string.IsNullOrEmpty(author) ? "U" : author.Substring(0, 1).ToUpper();

            var avatarHost = new FeedBufferedPanel
            {
                Size = new Size(46, 46),
                Location = new Point(12, 12),
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };
            AttachEdgeResizeRedirector(avatarHost);

            avatarHost.Paint += (s, e) =>
            {
                AppPalette theme = CurrentTheme;
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                var ringRect = new RectangleF(2.5f, 2.5f, 40f, 40f);
                if (isMyTopCard || isAuthorOnline)
                {
                    using var ringBrush = new LinearGradientBrush(ringRect, theme.StoryRingStart, theme.StoryRingEnd, 45f);
                    ringBrush.InterpolationColors = new ColorBlend(3)
                    {
                        Colors = new[] { theme.StoryRingStart, theme.StoryRingMid, theme.StoryRingEnd },
                        Positions = new[] { 0f, 0.5f, 1f }
                    };
                    using var ringPen = new Pen(ringBrush, 2.3f);
                    g.DrawEllipse(ringPen, ringRect);
                }
                else
                {
                    using var ringPen = new Pen(theme.BorderLight, 1.6f);
                    g.DrawEllipse(ringPen, ringRect);
                }

                Color innerFill;
                if (isMyTopCard || (post != null && post.IsMine))
                {
                    innerFill = theme.AccentPrimary;
                }
                else if (matchedFriend != null)
                {
                    Color tint = FeedAvatarTints[Math.Abs(matchedFriend.UserId) % FeedAvatarTints.Length];
                    innerFill = isAuthorOnline ? tint : BlendControlColor(tint, theme.TextMuted, 0.6f);
                }
                else
                {
                    int hash = Math.Abs(author.GetHashCode());
                    Color tint = FeedAvatarTints[hash % FeedAvatarTints.Length];
                    innerFill = BlendControlColor(tint, theme.TextMuted, 0.45f);
                }

                var innerRect = new RectangleF(6f, 6f, 33f, 33f);
                using (var avatarBrush = new SolidBrush(innerFill))
                {
                    g.FillEllipse(avatarBrush, innerRect);
                }

                using (Font initFont = FontBold(10.5F))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(initial, initFont, Brushes.White, innerRect, fmt);
                }

                if (isAuthorOnline)
                {
                    using var haloBrush = new SolidBrush(theme.CardBg);
                    using var dotBrush = new SolidBrush(theme.Success);
                    g.FillEllipse(haloBrush, 30f, 30f, 13f, 13f);
                    g.FillEllipse(dotBrush, 32f, 32f, 9f, 9f);
                }
            };

            return avatarHost;
        }

        private Guna2Panel CreateEmptyHeroCard(
            int width,
            bool useGradientBadge,
            UiIcon icon,
            string title,
            string subtitle,
            Color borderColor,
            Color titleColor,
            Action onClick)
        {
            AppPalette theme = CurrentTheme;
            Guna2Panel card = new Guna2Panel
            {
                Size = new Size(width, 148),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 26,
                FillColor = clrCardBg,
                BorderColor = borderColor,
                BorderThickness = 1,
                UseTransparentBackground = true,
                Cursor = Cursors.Hand
            };
            AttachEdgeResizeRedirector(card);

            Control badge;
            if (useGradientBadge)
            {
                var gradBadge = new Guna2GradientPanel
                {
                    Location = new Point((width - 52) / 2, 18),
                    Size = new Size(52, 52),
                    BorderRadius = 17,
                    FillColor = theme.AccentPrimary,
                    FillColor2 = theme.AccentSecondary,
                    GradientMode = LinearGradientMode.ForwardDiagonal,
                    BackColor = Color.Transparent,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                var icoPic = new PictureBox
                {
                    Size = new Size(24, 24),
                    Location = new Point(14, 14),
                    Image = CreateVectorIcon(icon, Color.White, 24),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    BackColor = Color.Transparent,
                    Enabled = false
                };
                gradBadge.Disposed += (s, e) => { try { icoPic.Image?.Dispose(); } catch { } };
                gradBadge.Controls.Add(icoPic);
                badge = gradBadge;
            }
            else
            {
                var softBadge = MakeSoftIconBadge(icon, (width - 46) / 2, 18, 46, 15, clrSoftGreenBg, clrPrimaryGreen);
                softBadge.Cursor = Cursors.Hand;
                softBadge.Disposed += (s, e) =>
                {
                    foreach (PictureBox p in softBadge.Controls.OfType<PictureBox>())
                    {
                        try { p.Image?.Dispose(); } catch { }
                    }
                };
                badge = softBadge;
            }

            int textStartY = badge.Bottom + 10;
            var lbl1 = MakeLabel(title, 16, textStartY, 10.2F, FontStyle.Bold, titleColor, Math.Max(140, width - 32));
            lbl1.TextAlign = ContentAlignment.MiddleCenter;
            lbl1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lbl1.Cursor = Cursors.Hand;

            var lbl2 = MakeLabel(subtitle, 16, lbl1.Bottom + 4, 8.8F, FontStyle.Regular, clrTextMuted, Math.Max(140, width - 32), lines: 2);
            lbl2.TextAlign = ContentAlignment.MiddleCenter;
            lbl2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lbl2.Cursor = Cursors.Hand;

            card.Height = lbl2.Bottom + 16;
            card.Resize += (s, e) => badge.Left = (card.Width - badge.Width) / 2;

            EventHandler handler = (s, e) => onClick?.Invoke();
            card.Click += handler;
            badge.Click += handler;
            lbl1.Click += handler;
            lbl2.Click += handler;

            card.Controls.AddRange(new Control[] { badge, lbl1, lbl2 });
            return card;
        }

        private void OpenMyGalleryScreen()
        {
            isFeedShowingGallery = true;
            if (IsTabActive("FEED"))
            {
                RefreshPaneContent("FEED");
            }
            else
            {
                HandleBottomTabClick("FEED");
                isFeedShowingGallery = true;
                RefreshPaneContent("FEED");
            }
        }

        private Control BuildMyGalleryControl()
        {
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 14;

            var myPosts = posts.Where(p => p.IsMine).ToList();

            int backW = Math.Max(188, MeasurePillBtnWidth("Quay lại Bảng tin", 8.6F, 19, true, 15));
            var btnBack = MakeBtn("Quay lại Bảng tin", 0, y, backW, 38, 19, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.6F);
            btnBack.HoverState.FillColor = clrSoftGreenBg;
            btnBack.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnBack, UiIcon.ChevronLeft, clrPrimaryGreen, 15);
            AttachEdgeResizeRedirector(btnBack);
            btnBack.Click += (s, e) =>
            {
                isFeedShowingGallery = false;
                RefreshPaneContent("FEED");
            };
            col.Controls.Add(btnBack);

            string countText = $"{myPosts.Count} khoảnh khắc";
            int countW = Math.Max(104, MeasurePillBtnWidth(countText, 8.2F, 17));
            var countChip = new Guna2Panel
            {
                Location = new Point(Math.Max(backW + 8, w - countW), y + 2),
                Size = new Size(countW, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BorderRadius = 17,
                FillColor = clrSoftGreenBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            AttachEdgeResizeRedirector(countChip);
            var lblCount = new Label
            {
                Text = countText,
                Dock = DockStyle.Fill,
                Font = FontMedium(8.2F),
                ForeColor = clrPrimaryGreen,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            countChip.Controls.Add(lblCount);
            col.Controls.Add(countChip);
            y += 48;

            var lblTitle = MakeLabel($"Kho ảnh của {currentFullName}", 2, y, 12F, FontStyle.Bold, clrTextMain, Math.Max(140, w - 8));
            lblTitle.Font = FontBold(12F);
            lblTitle.Height = TextRenderer.MeasureText("Ág", lblTitle.Font).Height + 4;
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            col.Controls.Add(lblTitle);
            y += lblTitle.Height + 10;

            if (myPosts.Count == 0)
            {
                var emptyGalCard = CreateEmptyHeroCard(
                    w,
                    useGradientBadge: true,
                    UiIcon.Photo,
                    "Chưa có tấm ảnh nào trong kho lưu trữ",
                    "Chạm vào đây để mở tab Chụp ảnh và lưu khoảnh khắc đầu tiên",
                    clrSoftGreenBorder,
                    clrPrimaryGreen,
                    () => EnsureTabVisible("CAMERA"));
                emptyGalCard.Location = new Point(0, y);
                col.Controls.Add(emptyGalCard);
                y += emptyGalCard.Height + 16;
            }
            else
            {
                const int thumbH = 132;
                int l1H;
                int l2H;
                int timeH;
                using (Font f1 = FontMedium(8.8F))
                using (Font f2 = FontRegular(8.4F))
                using (Font ft = FontRegular(7.6F))
                {
                    l1H = TextRenderer.MeasureText("Ág", f1).Height + 4;
                    l2H = TextRenderer.MeasureText("Ág", f2).Height * 2 + 4;
                    timeH = TextRenderer.MeasureText("Ág", ft).Height + 4;
                }
                int cellH = 8 + thumbH + 8 + l1H + 2 + l2H + 10;
                int rowHeight = cellH + 12;

                int rows = (myPosts.Count + 1) / 2;
                var grid = new BufferedTableLayout
                {
                    Location = new Point(0, y),
                    Size = new Size(w, rows * rowHeight),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    ColumnCount = 2,
                    RowCount = rows
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                for (int r = 0; r < rows; r++)
                {
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
                }
                AttachEdgeResizeRedirector(grid);

                int initCellW = Math.Max(140, (w - 12) / 2);
                for (int i = 0; i < myPosts.Count; i++)
                {
                    var item = myPosts[i];
                    Guna2Panel cell = new Guna2Panel
                    {
                        Size = new Size(initCellW, cellH),
                        Dock = DockStyle.Fill,
                        Margin = new Padding(i % 2 == 0 ? 0 : 6, 4, i % 2 == 0 ? 6 : 0, 8),
                        BorderRadius = 22,
                        FillColor = clrCardBg,
                        BorderColor = clrBorderLight,
                        BorderThickness = 1,
                        UseTransparentBackground = true
                    };
                    AttachEdgeResizeRedirector(cell);

                    var miniCanvas = new SocialPhotoCanvas(this, item, isMyTopCard: false, compactThumbnail: true)
                    {
                        Location = new Point(8, 8),
                        Size = new Size(Math.Max(100, initCellW - 16), thumbH),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                    };

                    int line1Y = 8 + thumbH + 8;
                    string itemTime = (item.Time ?? "").Trim();
                    Label lblItemTime = null;
                    if (!string.IsNullOrEmpty(itemTime))
                    {
                        lblItemTime = new Label
                        {
                            Text = itemTime,
                            AutoSize = true,
                            Font = FontRegular(7.6F),
                            ForeColor = clrTextMuted,
                            BackColor = Color.Transparent,
                            UseMnemonic = false
                        };
                        int measuredTimeW = lblItemTime.PreferredWidth;
                        lblItemTime.Location = new Point(Math.Max(70, initCellW - 10 - measuredTimeW), line1Y + Math.Max(0, (l1H - timeH) / 2));
                    }

                    int catAvailW = lblItemTime != null
                        ? Math.Max(48, lblItemTime.Left - 10 - 4)
                        : Math.Max(80, initCellW - 20);
                    var l1 = MakeLabel(
                        item.HasTag ? GetCategoryShortClean(item.Category) : "Ảnh Pocket",
                        10,
                        line1Y,
                        8.8F,
                        FontStyle.Bold,
                        item.HasTag ? clrPrimaryGreen : clrIndigo,
                        catAvailW);
                    l1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                    string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0)
                        ? $"Tag: {string.Join(", ", item.TaggedFriends)}"
                        : "Cá nhân";
                    string l2Text = item.HasTag
                        ? $"{item.NoteTop} ({tagTxt})"
                        : (string.IsNullOrWhiteSpace(item.CaptionInside) ? "Khoảnh khắc Pocket" : item.CaptionInside.Trim());
                    var l2 = MakeLabel(l2Text, 10, line1Y + l1H + 2, 8.4F, FontStyle.Regular, clrTextMain, Math.Max(80, initCellW - 20), lines: 2);
                    l2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                    void LayoutGalleryCell()
                    {
                        if (cell.IsDisposed) return;
                        int cw = cell.Width;
                        if (lblItemTime != null && !lblItemTime.IsDisposed)
                        {
                            lblItemTime.Left = Math.Max(70, cw - 10 - lblItemTime.Width);
                            l1.Width = Math.Max(48, lblItemTime.Left - l1.Left - 4);
                        }
                        else
                        {
                            l1.Width = Math.Max(80, cw - 20);
                        }
                        l2.Width = Math.Max(80, cw - 20);
                    }

                    cell.Resize += (s, e) => LayoutGalleryCell();
                    cell.Layout += (s, e) => LayoutGalleryCell();

                    cell.Controls.Add(miniCanvas);
                    cell.Controls.Add(l1);
                    if (lblItemTime != null) cell.Controls.Add(lblItemTime);
                    cell.Controls.Add(l2);
                    grid.Controls.Add(cell, i % 2, i / 2);
                }

                col.Controls.Add(grid);
                y += rows * rowHeight;
            }

            col.Height = y + 20;
            AttachFeedScrollWidthGuard(page, col);
            return page;
        }

        private void AttachFeedScrollWidthGuard(Panel page, Panel col)
        {
            void SyncScrollWidth()
            {
                if (page.IsDisposed || col.IsDisposed) return;
                int availW = page.ClientSize.Width;
                if (availW <= 0) return;

                bool needsVScroll = col.Height > page.ClientSize.Height;
                int vBarReserve = (needsVScroll && !page.VerticalScroll.Visible)
                    ? Math.Max(SystemInformation.VerticalScrollBarWidth, (int)Math.Ceiling(18.0 * page.DeviceDpi / 96.0))
                    : 0;
                int effectiveW = Math.Max(260, availW - vBarReserve);
                int sidePad = effectiveW < 450 ? 10 : 16;
                int targetW = Math.Clamp(effectiveW - sidePad * 2, 260, MaxColWidth);
                int targetX = Math.Max(sidePad, (effectiveW - targetW) / 2);

                if (col.Left != targetX || col.Width != targetW)
                {
                    col.Location = new Point(targetX, page.AutoScrollPosition.Y);
                    col.Width = targetW;
                }

                if (page.HorizontalScroll.Visible && col.Right <= page.ClientSize.Width && page.IsHandleCreated)
                {
                    page.BeginInvoke(new Action(() =>
                    {
                        if (!page.IsDisposed && !col.IsDisposed && page.HorizontalScroll.Visible && col.Right <= page.ClientSize.Width)
                        {
                            page.HorizontalScroll.Value = 0;
                            page.PerformLayout();
                        }
                    }));
                }
            }

            page.Resize += (s, e) => SyncScrollWidth();
            page.Layout += (s, e) => SyncScrollWidth();
            page.HandleCreated += (s, e) => SyncScrollWidth();
            SyncScrollWidth();
        }
    }
}
