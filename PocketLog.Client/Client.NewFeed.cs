using System;
using System.Collections.Generic;
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
            Color accentText = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
            Panel page = CreateResponsiveScrollPage(out Panel col);
            ToolTip feedLikeTip = new ToolTip();
            page.Disposed += (s, e) =>
            {
                try { feedLikeTip.Dispose(); } catch { }
            };

            int w = col.Width;
            var stackItems = new List<(Control Ctrl, int BottomGap, Action UpdateLayout)>();

            Panel topBar = new Panel
            {
                Location = new Point(0, 14),
                Size = new Size(w, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            AttachEdgeResizeRedirector(topBar);

            int myPhotoCount = posts.Count(p => p.IsMine);
            string galText = myPhotoCount > 0 ? $"Kho ảnh ({myPhotoCount})" : "Kho ảnh";
            string frText = $"Bạn bè ({myFriends.Count})";

            int frBtnW = Math.Max(114, MeasurePillBtnWidth(frText, 8.2F, 18, true, 15));
            var btnFriendsTop = MakeBtn(frText, w - frBtnW - 4, 3, frBtnW, 36, 18, clrCardBg, clrIndigo, clrBorderLight, 1, 8.2F);
            btnFriendsTop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnFriendsTop.HoverState.FillColor = clrSoftGreenBg;
            btnFriendsTop.HoverState.BorderColor = clrSoftGreenBorder;
            AttachBtnIcon(btnFriendsTop, UiIcon.Users, clrIndigo, 15);
            AttachControlImageDisposal(btnFriendsTop);
            AttachEdgeResizeRedirector(btnFriendsTop);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            int galBtnW = Math.Max(104, MeasurePillBtnWidth(galText, 8.2F, 18, true, 15));
            var btnGallery = MakeBtn(galText, w - frBtnW - 4 - 6 - galBtnW, 3, galBtnW, 36, 18, clrSoftGreenBg, accentText, clrSoftGreenBorder, 1, 8.2F);
            btnGallery.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGallery.HoverState.FillColor = clrCardBg;
            btnGallery.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnGallery, UiIcon.Photo, accentText, 15);
            AttachControlImageDisposal(btnGallery);
            AttachEdgeResizeRedirector(btnGallery);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var lblTitle = MakeLabel("Bảng tin", 4, 5, 12.2F, FontStyle.Bold, clrTextMain, Math.Max(70, btnGallery.Left - 10));
            SetControlFont(lblTitle, FontBold(12.2F));
            lblTitle.Height = TextRenderer.MeasureText("Ág", lblTitle.Font).Height + 4;
            lblTitle.Top = Math.Max(2, (42 - lblTitle.Height) / 2);
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            void LayoutTopBar()
            {
                if (topBar.IsDisposed) return;
                int tw = topBar.Width;
                int btnGap = tw < 440 ? 6 : 8;
                btnFriendsTop.SetBounds(Math.Max(0, tw - 4 - frBtnW), 3, frBtnW, 36);
                btnGallery.SetBounds(Math.Max(0, btnFriendsTop.Left - btnGap - galBtnW), 3, galBtnW, 36);
                lblTitle.SetBounds(4, lblTitle.Top, Math.Max(60, btnGallery.Left - 12), lblTitle.Height);
            }

            topBar.Controls.Add(lblTitle);
            topBar.Controls.Add(btnGallery);
            topBar.Controls.Add(btnFriendsTop);
            topBar.Resize += (s, e) => LayoutTopBar();
            LayoutTopBar();
            col.Controls.Add(topBar);
            stackItems.Add((topBar, 8, LayoutTopBar));

            var storyStrip = new StoryAvatarStrip(this, myFriends, friendSuggestions.Count, OpenFriendsDrawer)
            {
                Location = new Point(0, 64),
                Size = new Size(w, 96),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            col.Controls.Add(storyStrip);
            stackItems.Add((storyStrip, 12, null));

            LocketPost latestMine = posts.FirstOrDefault(p => p.IsMine);
            if (latestMine != null)
            {
                var hero = CreateLocketPostCard(latestMine, true, w, feedLikeTip, out Action updateHero);
                col.Controls.Add(hero);
                stackItems.Add((hero, 14, updateHero));
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
                    accentText,
                    () => EnsureTabVisible("CAMERA"));
                col.Controls.Add(emptyMine);
                stackItems.Add((emptyMine, 12, null));
            }

            Panel secHeader = new Panel
            {
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
            stackItems.Add((secHeader, 6, LayoutSecDivider));

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
                col.Controls.Add(noFriendPostBox);
                stackItems.Add((noFriendPostBox, 12, null));
            }
            else
            {
                foreach (var fp in friendPosts)
                {
                    var card = CreateLocketPostCard(fp, false, w, feedLikeTip, out Action updateCard);
                    col.Controls.Add(card);
                    stackItems.Add((card, 14, updateCard));
                }
            }

            bool relayoutingFeed = false;
            void RelayoutFeedStack()
            {
                if (relayoutingFeed || col.IsDisposed || page.IsDisposed) return;
                int curW = col.Width;
                if (curW < 100) return;

                relayoutingFeed = true;
                try
                {
                    col.SuspendLayout();
                    try
                    {
                        int curY = 14;
                        foreach (var item in stackItems)
                        {
                            if (item.Ctrl == null || item.Ctrl.IsDisposed) continue;
                            if (item.Ctrl.Width != curW)
                            {
                                item.Ctrl.Width = curW;
                            }
                            else
                            {
                                item.UpdateLayout?.Invoke();
                            }
                            if (item.Ctrl.Left != 0 || item.Ctrl.Top != curY)
                            {
                                item.Ctrl.Location = new Point(0, curY);
                            }
                            curY += item.Ctrl.Height + item.BottomGap;
                        }

                        int targetColH = curY + 20;
                        if (col.Height != targetColH)
                        {
                            col.Height = targetColH;
                        }
                    }
                    finally
                    {
                        col.ResumeLayout(false);
                    }
                    (page as AndroidOverlayScrollPanel)?.SyncLayout();
                }
                finally
                {
                    relayoutingFeed = false;
                }
            }

            col.Resize += (s, e) => RelayoutFeedStack();
            RelayoutFeedStack();
            return page;
        }

        private TaggedDebtItem FindMatchingSplitDebt(LocketPost post)
        {
            if (post == null || taggedDebts == null || taggedDebts.Count == 0) return null;

            if (post.PostId > 0)
            {
                TaggedDebtItem unpaidByPost = taggedDebts.FirstOrDefault(d => d != null && d.PostId == post.PostId && !d.IsConfirmedPaid);
                if (unpaidByPost != null) return unpaidByPost;

                TaggedDebtItem anyByPost = taggedDebts.FirstOrDefault(d => d != null && d.PostId == post.PostId);
                if (anyByPost != null) return anyByPost;
            }

            string note = (post.NoteTop ?? "").Trim();
            if (!string.IsNullOrEmpty(note))
            {
                TaggedDebtItem unpaidByTitle = taggedDebts.FirstOrDefault(d =>
                    d != null &&
                    !d.IsConfirmedPaid &&
                    (post.PostId <= 0 || d.PostId <= 0 || d.PostId == post.PostId) &&
                    string.Equals((d.Title ?? "").Trim(), note, StringComparison.OrdinalIgnoreCase) &&
                    (post.Amount <= 0 || d.TotalAmount == post.Amount));
                if (unpaidByTitle != null) return unpaidByTitle;

                TaggedDebtItem anyByTitle = taggedDebts.FirstOrDefault(d =>
                    d != null &&
                    (post.PostId <= 0 || d.PostId <= 0 || d.PostId == post.PostId) &&
                    string.Equals((d.Title ?? "").Trim(), note, StringComparison.OrdinalIgnoreCase) &&
                    (post.Amount <= 0 || d.TotalAmount == post.Amount));
                if (anyByTitle != null) return anyByTitle;
            }

            if (post.Amount > 0)
            {
                TaggedDebtItem unpaidByAmount = taggedDebts.FirstOrDefault(d =>
                    d != null &&
                    !d.IsConfirmedPaid &&
                    (post.PostId <= 0 || d.PostId <= 0 || d.PostId == post.PostId) &&
                    d.TotalAmount == post.Amount &&
                    (string.IsNullOrEmpty(post.Category) || string.Equals(d.Category, post.Category, StringComparison.OrdinalIgnoreCase)));
                if (unpaidByAmount != null) return unpaidByAmount;
            }

            return null;
        }

        private ElevatedCardPanel CreateLocketPostCard(LocketPost post, bool isMyTopCard, int cardWidth, ToolTip likeTip, out Action relayoutCard)
        {
            Color accentText = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
            var card = new ElevatedCardPanel(this)
            {
                Size = new Size(cardWidth, 496),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CornerRadius = 28,
                ElevationLevel = 2,
                CardFillColor = clrCardBg,
                CardBorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                CardBorderThickness = 1,
                BackColor = clrAppBg,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            Rectangle initInner = card.InnerCardBounds;
            FriendUser matchedFriend = FindAuthorFriend(post);
            bool isAuthorOnline = matchedFriend != null && matchedFriend.IsOnline;

            Control avatarHost = BuildMiniAuthorAvatar(post, matchedFriend, isMyTopCard, isAuthorOnline);
            avatarHost.Location = new Point(initInner.X + 12, initInner.Y + 12);

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
                    Location = new Point(Math.Max(initInner.X + 140, initInner.Right - 14 - timeW), initInner.Y + 14),
                    Size = new Size(timeW, timeH),
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
                    ForeColor = isMyTopCard ? accentText : clrTextMuted,
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
            int authorLeft = initInner.X + 66;
            int initAuthorW = timePill != null
                ? Math.Max(80, timePill.Left - authorLeft - 8)
                : Math.Max(120, initInner.Right - authorLeft - 14);
            var lblAuthor = MakeLabel(authorName, authorLeft, initInner.Y + 11, 10F, FontStyle.Bold, clrTextMain, initAuthorW);

            string subText = BuildPostHeaderSubtitle(post, isMyTopCard);
            int subY = timePill != null
                ? Math.Max(lblAuthor.Bottom + 1, timePill.Bottom + 2)
                : lblAuthor.Bottom - 1;
            var lblSub = MakeLabel(
                subText,
                authorLeft,
                subY,
                8.6F,
                post.HasTag ? FontStyle.Bold : FontStyle.Regular,
                post.HasTag ? accentText : clrTextMuted,
                Math.Max(120, initInner.Right - authorLeft - 14));

            int initCanvasW = Math.Max(180, initInner.Width - 24);
            int initCanvasH = Math.Clamp(initCanvasW, 260, 380);
            int initPhotoY = Math.Max(initInner.Y + 66, lblSub.Bottom + 6);
            var photoCanvas = new SocialPhotoCanvas(this, post, isMyTopCard, compactThumbnail: false)
            {
                Location = new Point(initInner.X + 12, initPhotoY),
                Size = new Size(initCanvasW, initCanvasH)
            };

            // Quick Action Footer Bar (Height = 42)
            var footer = new FeedBufferedPanel
            {
                Name = "pnlCardFooter",
                Location = new Point(initInner.X + 12, photoCanvas.Bottom + 8),
                Size = new Size(initCanvasW, 42),
                BackColor = Color.Transparent,
                Cursor = Cursors.Default
            };
            AttachEdgeResizeRedirector(footer);

            bool initLiked = IsPostLiked(post.PostId);
            var btnLike = MakeBtn(
                initLiked ? "Đã thích" : "Thích",
                0,
                4,
                Math.Max(84, MeasurePillBtnWidth(initLiked ? "Đã thích" : "Thích", 8.2F, 17, true, 15)),
                34,
                17,
                clrInputBg,
                clrIndigo,
                clrBorderLight,
                1,
                8.2F);
            btnLike.Name = "btnLike";
            AttachEdgeResizeRedirector(btnLike);

            Color likedBorderClr = isDarkMode ? Color.FromArgb(159, 18, 57) : Color.FromArgb(253, 164, 175);
            void ApplyLikeVisual(bool liked)
            {
                btnLike.Text = liked ? "Đã thích" : "Thích";
                btnLike.FillColor = liked ? clrSoftRedBg : clrInputBg;
                btnLike.ForeColor = liked ? clrRedDanger : clrIndigo;
                btnLike.BorderColor = liked ? likedBorderClr : clrBorderLight;
                btnLike.HoverState.FillColor = clrSoftRedBg;
                btnLike.HoverState.ForeColor = clrRedDanger;
                btnLike.HoverState.BorderColor = clrRedDanger;
                AttachBtnIcon(btnLike, liked ? UiIcon.HeartFilled : UiIcon.Heart, liked ? clrRedDanger : clrIndigo, 15);
            }

            ApplyLikeVisual(initLiked);
            AttachControlImageDisposal(btnLike);
            likeTip?.SetToolTip(btnLike, "Lưu yêu thích trên máy này");

            Guna2Button CreateFooterActionBtn(string label, int minW, Action onClick)
            {
                int btnW = Math.Max(minW, MeasurePillBtnWidth(label, 8.2F, 17, true, 14));
                var btn = MakeBtn(label, 0, 4, btnW, 34, 17, clrSoftGreenBg, accentText, clrSoftGreenBorder, 1, 8.2F);
                btn.Name = "btnAction";
                btn.HoverState.FillColor = clrCardBg;
                btn.HoverState.ForeColor = accentText;
                btn.HoverState.BorderColor = clrPrimaryGreen;
                Image oldImg = btn.Image;
                btn.Image = CreateVectorIcon(UiIcon.ArrowRight, accentText, 14);
                btn.ImageSize = new Size(14, 14);
                btn.ImageAlign = HorizontalAlignment.Right;
                btn.ImageOffset = new Point(4, 0);
                btn.TextAlign = HorizontalAlignment.Left;
                btn.TextOffset = new Point(4, 0);
                oldImg?.Dispose();
                AttachControlImageDisposal(btn);
                AttachEdgeResizeRedirector(btn);
                btn.Click += (s, e) => onClick();
                return btn;
            }

            Control rightFooterCtrl;
            int rightDesiredW;
            if (post.HasTag)
            {
                var btnAction = CreateFooterActionBtn("Xem chia bill", 128, () =>
                {
                    TaggedDebtItem matched = FindMatchingSplitDebt(post);
                    if (matched != null)
                    {
                        selectedSplitId = matched.SplitId;
                        if (!matched.IsConfirmedPaid)
                        {
                            isUnpaidDropdownOpen = true;
                        }
                    }
                    bool wasExpenseActive = IsTabActive("EXPENSE");
                    EnsureTabVisible("EXPENSE");
                    if (wasExpenseActive)
                    {
                        RefreshPaneContent("EXPENSE");
                    }
                });
                rightDesiredW = btnAction.Width;
                rightFooterCtrl = btnAction;
            }
            else if (isMyTopCard || post.IsMine)
            {
                var btnAction = CreateFooterActionBtn("Mở Kho ảnh", 122, () => OpenMyGalleryScreen());
                rightDesiredW = btnAction.Width;
                rightFooterCtrl = btnAction;
            }
            else
            {
                const string momentText = "Khoảnh khắc";
                using Font chipFont = FontMedium(7.8F);
                int chipTextW = TextRenderer.MeasureText(momentText, chipFont, new Size(200, 32), TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                rightDesiredW = Math.Max(92, chipTextW + 24);
                var chipMoment = new Guna2Panel
                {
                    Name = "chipMoment",
                    Size = new Size(rightDesiredW, 30),
                    BorderRadius = 15,
                    FillColor = clrInputBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };
                AttachEdgeResizeRedirector(chipMoment);
                var lblMoment = new Label
                {
                    Text = momentText,
                    Dock = DockStyle.Fill,
                    Font = FontMedium(7.8F),
                    ForeColor = clrTextMuted,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoEllipsis = true,
                    UseMnemonic = false
                };
                chipMoment.Controls.Add(lblMoment);
                rightFooterCtrl = chipMoment;
            }

            void LayoutFooter()
            {
                if (footer.IsDisposed) return;
                int fw = footer.Width;
                int desiredLikeW = Math.Max(84, MeasurePillBtnWidth(btnLike.Text, 8.2F, 17, true, 15));
                const int gap = 8;
                int likeW = desiredLikeW;
                int rightW = rightDesiredW;

                if (likeW + gap + rightW > fw)
                {
                    int avail = Math.Max(80, fw - gap);
                    likeW = Math.Min(likeW, Math.Max(72, avail / 2 - 4));
                    rightW = Math.Max(68, avail - likeW);
                }

                btnLike.SetBounds(0, (42 - 34) / 2, likeW, 34);
                int rightH = rightFooterCtrl.Height;
                rightFooterCtrl.SetBounds(Math.Max(btnLike.Right + 6, fw - rightW), (42 - rightH) / 2, rightW, rightH);
            }

            btnLike.Click += (s, e) =>
            {
                bool nowLiked = TogglePostLiked(post.PostId);
                ApplyLikeVisual(nowLiked);
                LayoutFooter();
            };

            footer.Controls.Add(btnLike);
            footer.Controls.Add(rightFooterCtrl);

            bool layingOutCard = false;
            void LayoutCardBody()
            {
                if (layingOutCard || card.IsDisposed) return;
                layingOutCard = true;
                try
                {
                    Rectangle r = card.InnerCardBounds;
                    avatarHost.Location = new Point(r.X + 12, r.Y + 12);

                    int aLeft = r.X + 66;
                    if (timePill != null && !timePill.IsDisposed)
                    {
                        timePill.Location = new Point(Math.Max(r.X + 140, r.Right - 14 - timePill.Width), r.Y + 14);
                        lblAuthor.SetBounds(aLeft, r.Y + 11, Math.Max(80, timePill.Left - aLeft - 8), lblAuthor.Height);
                    }
                    else
                    {
                        lblAuthor.SetBounds(aLeft, r.Y + 11, Math.Max(80, r.Right - aLeft - 14), lblAuthor.Height);
                    }

                    int subYNow = timePill != null && !timePill.IsDisposed
                        ? Math.Max(lblAuthor.Bottom + 1, timePill.Bottom + 2)
                        : lblAuthor.Bottom - 1;
                    bool overlapsTimePillY = timePill != null && !timePill.IsDisposed &&
                        subYNow < timePill.Bottom && (subYNow + lblSub.Height) > timePill.Top;
                    int subW = overlapsTimePillY
                        ? Math.Max(80, timePill.Left - aLeft - 8)
                        : Math.Max(80, r.Right - aLeft - 14);
                    lblSub.SetBounds(aLeft, subYNow, subW, lblSub.Height);

                    int photoYNow = Math.Max(r.Y + 66, lblSub.Bottom + 6);
                    int canvasW = Math.Max(180, r.Width - 24);
                    int canvasH = Math.Clamp(canvasW, 260, 380);
                    photoCanvas.SetBounds(r.X + 12, photoYNow, canvasW, canvasH);

                    footer.SetBounds(r.X + 12, photoCanvas.Bottom + 8, canvasW, 42);
                    LayoutFooter();

                    int desiredCardH = footer.Bottom + 8 + 6;
                    if (card.Height != desiredCardH)
                    {
                        card.Height = desiredCardH;
                    }
                }
                finally
                {
                    layingOutCard = false;
                }
            }

            card.Controls.Add(avatarHost);
            card.Controls.Add(lblAuthor);
            card.Controls.Add(lblSub);
            card.Controls.Add(photoCanvas);
            card.Controls.Add(footer);

            card.Resize += (s, e) => LayoutCardBody();
            LayoutCardBody();
            relayoutCard = LayoutCardBody;

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

            string authorNoAt = author.TrimStart('@');
            return myFriends.FirstOrDefault(f =>
            {
                if (f == null) return false;
                string name = (f.Name ?? "").Trim();
                if (!string.IsNullOrEmpty(name) && string.Equals(name, author, StringComparison.OrdinalIgnoreCase))
                    return true;

                string rawHandle = (f.Handle ?? "").Trim().TrimStart('@');
                if (string.IsNullOrEmpty(rawHandle))
                    return false;

                if (string.Equals(rawHandle, authorNoAt, StringComparison.OrdinalIgnoreCase))
                    return true;

                string atToken = "@" + rawHandle;
                return author
                    .Split(new[] { ' ', '\t', '(', ')', ',', ';', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(tok => string.Equals(tok, atToken, StringComparison.OrdinalIgnoreCase));
            });
        }

        private Control BuildMiniAuthorAvatar(LocketPost post, FriendUser matchedFriend, bool isMyTopCard, bool isAuthorOnline)
        {
            string author = (post?.Author ?? "").Trim();
            string initial = GetAvatarInitial(author);

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
                    uint tintIdx = unchecked((uint)matchedFriend.UserId) % (uint)FeedAvatarTints.Length;
                    Color tint = FeedAvatarTints[tintIdx];
                    innerFill = isAuthorOnline ? tint : BlendControlColor(tint, theme.TextMuted, 0.6f);
                }
                else
                {
                    uint hash = unchecked((uint)author.GetHashCode()) % (uint)FeedAvatarTints.Length;
                    Color tint = FeedAvatarTints[hash];
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

        private ElevatedCardPanel CreateEmptyHeroCard(
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
            var card = new ElevatedCardPanel(this)
            {
                Size = new Size(width, 156),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CornerRadius = 26,
                ElevationLevel = 2,
                CardFillColor = clrCardBg,
                CardBorderColor = borderColor,
                CardBorderThickness = 1,
                BackColor = clrAppBg,
                Cursor = Cursors.Hand
            };

            Rectangle initInner = card.InnerCardBounds;
            Control badge;
            if (useGradientBadge)
            {
                var gradBadge = new Guna2GradientPanel
                {
                    Location = new Point(initInner.X + (initInner.Width - 52) / 2, initInner.Y + 18),
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
                AttachControlImageDisposal(icoPic);
                gradBadge.Controls.Add(icoPic);
                badge = gradBadge;
            }
            else
            {
                Color accentText = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
                var softBadge = MakeSoftIconBadge(icon, initInner.X + (initInner.Width - 46) / 2, initInner.Y + 18, 46, 15, clrSoftGreenBg, accentText);
                softBadge.Cursor = Cursors.Hand;
                badge = softBadge;
            }

            int textStartY = badge.Bottom + 10;
            int initTextW = Math.Max(140, initInner.Width - 32);
            var lbl1 = MakeLabel(title, initInner.X + 16, textStartY, 10.2F, FontStyle.Bold, titleColor, initTextW);
            lbl1.TextAlign = ContentAlignment.MiddleCenter;
            lbl1.Cursor = Cursors.Hand;

            var lbl2 = MakeLabel(subtitle, initInner.X + 16, lbl1.Bottom + 4, 8.8F, FontStyle.Regular, clrTextMuted, initTextW, lines: 2);
            lbl2.TextAlign = ContentAlignment.MiddleCenter;
            lbl2.Cursor = Cursors.Hand;

            card.Height = lbl2.Bottom + 16 + 6;

            void LayoutEmptyHero()
            {
                if (card.IsDisposed) return;
                Rectangle r = card.InnerCardBounds;
                badge.Left = r.X + (r.Width - badge.Width) / 2;
                int txtW = Math.Max(140, r.Width - 32);
                lbl1.SetBounds(r.X + 16, lbl1.Top, txtW, lbl1.Height);
                lbl2.SetBounds(r.X + 16, lbl2.Top, txtW, lbl2.Height);
            }

            card.Resize += (s, e) => LayoutEmptyHero();
            LayoutEmptyHero();

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
            Color accentText = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 14;

            var myPosts = posts.Where(p => p.IsMine).ToList();

            Panel galTopBar = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            AttachEdgeResizeRedirector(galTopBar);

            int backW = Math.Max(188, MeasurePillBtnWidth("Quay lại Bảng tin", 8.6F, 19, true, 15));
            var btnBack = MakeBtn("Quay lại Bảng tin", 4, 2, backW, 38, 19, clrCardBg, accentText, clrSoftGreenBorder, 1, 8.6F);
            btnBack.HoverState.FillColor = clrSoftGreenBg;
            btnBack.HoverState.BorderColor = clrPrimaryGreen;
            AttachBtnIcon(btnBack, UiIcon.ChevronLeft, accentText, 15);
            AttachControlImageDisposal(btnBack);
            AttachEdgeResizeRedirector(btnBack);
            btnBack.Click += (s, e) =>
            {
                isFeedShowingGallery = false;
                RefreshPaneContent("FEED");
            };
            galTopBar.Controls.Add(btnBack);

            string countText = $"{myPosts.Count} khoảnh khắc";
            int countW = Math.Max(104, MeasurePillBtnWidth(countText, 8.2F, 17));
            var countChip = new Guna2Panel
            {
                Location = new Point(Math.Max(4, w - 4 - countW), 4),
                Size = new Size(countW, 34),
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
                ForeColor = accentText,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = true,
                UseMnemonic = false
            };
            countChip.Controls.Add(lblCount);
            galTopBar.Controls.Add(countChip);
            col.Controls.Add(galTopBar);

            var lblTitle = MakeLabel($"Kho ảnh của {currentFullName}", 4, 0, 12F, FontStyle.Bold, clrTextMain, Math.Max(140, w - 8));
            SetControlFont(lblTitle, FontBold(12F));
            lblTitle.Height = TextRenderer.MeasureText("Ág", lblTitle.Font).Height + 4;
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            col.Controls.Add(lblTitle);

            Control bodyControl;
            int bodyBottomMargin;
            Action layoutGalleryGridAction = null;

            if (myPosts.Count == 0)
            {
                var emptyGalCard = CreateEmptyHeroCard(
                    w,
                    useGradientBadge: true,
                    UiIcon.Photo,
                    "Chưa có tấm ảnh nào trong kho lưu trữ",
                    "Chạm vào đây để mở tab Chụp ảnh và lưu khoảnh khắc đầu tiên",
                    clrSoftGreenBorder,
                    accentText,
                    () => EnsureTabVisible("CAMERA"));
                emptyGalCard.Location = new Point(0, 0);
                col.Controls.Add(emptyGalCard);
                bodyControl = emptyGalCard;
                bodyBottomMargin = 16;
            }
            else
            {
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

                var grid = new FeedBufferedPanel
                {
                    Location = new Point(0, 0),
                    Size = new Size(w, 260),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = Color.Transparent
                };
                AttachEdgeResizeRedirector(grid);

                var tileUpdaters = new List<(ElevatedCardPanel Cell, Action<int> LayoutTile)>();
                int initTileW = Math.Max(140, (w - 12) / 2);

                for (int i = 0; i < myPosts.Count; i++)
                {
                    var item = myPosts[i];
                    var cell = new ElevatedCardPanel(this)
                    {
                        Size = new Size(initTileW, 260),
                        CornerRadius = 22,
                        ElevationLevel = 2,
                        CardFillColor = clrCardBg,
                        CardBorderColor = clrBorderLight,
                        CardBorderThickness = 1,
                        BackColor = clrAppBg
                    };

                    var miniCanvas = new SocialPhotoCanvas(this, item, isMyTopCard: false, compactThumbnail: true);

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
                    }

                    var l1 = MakeLabel(
                        item.HasTag ? GetCategoryShortClean(item.Category) : "Ảnh Pocket",
                        14,
                        0,
                        8.8F,
                        FontStyle.Bold,
                        item.HasTag ? accentText : clrIndigo,
                        Math.Max(80, initTileW - 28));

                    string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0)
                        ? $"Tag: {string.Join(", ", item.TaggedFriends)}"
                        : "Cá nhân";
                    string l2Text = item.HasTag
                        ? $"{item.NoteTop} ({tagTxt})"
                        : (string.IsNullOrWhiteSpace(item.CaptionInside) ? "Khoảnh khắc Pocket" : item.CaptionInside.Trim());
                    var l2 = MakeLabel(l2Text, 14, 0, 8.4F, FontStyle.Regular, clrTextMain, Math.Max(80, initTileW - 28), lines: 2);

                    void LayoutTile(int targetThumbSide)
                    {
                        if (cell.IsDisposed) return;
                        Rectangle inner = cell.InnerCardBounds;
                        int side = Math.Clamp(targetThumbSide, 128, Math.Max(128, inner.Width - 20));
                        int thumbX = inner.X + (inner.Width - side) / 2;
                        miniCanvas.SetBounds(thumbX, inner.Y + 10, side, side);

                        int line1Y = miniCanvas.Bottom + 8;
                        int textLeft = inner.X + 10;
                        int textRight = inner.Right - 10;
                        if (lblItemTime != null && !lblItemTime.IsDisposed)
                        {
                            int timeW = lblItemTime.PreferredWidth;
                            int timeLeft = Math.Max(textLeft + 52, textRight - timeW);
                            lblItemTime.Location = new Point(timeLeft, line1Y + Math.Max(0, (l1H - timeH) / 2));
                            l1.SetBounds(textLeft, line1Y, Math.Max(48, lblItemTime.Left - textLeft - 4), l1H);
                        }
                        else
                        {
                            l1.SetBounds(textLeft, line1Y, Math.Max(60, textRight - textLeft), l1H);
                        }

                        l2.SetBounds(textLeft, line1Y + l1H + 2, Math.Max(60, textRight - textLeft), l2H);
                    }

                    cell.Controls.Add(miniCanvas);
                    cell.Controls.Add(l1);
                    if (lblItemTime != null) cell.Controls.Add(lblItemTime);
                    cell.Controls.Add(l2);
                    grid.Controls.Add(cell);
                    tileUpdaters.Add((cell, LayoutTile));
                }

                void LayoutGalleryGrid()
                {
                    if (grid.IsDisposed || col.IsDisposed) return;
                    int gw = Math.Max(290, col.Width);
                    if (grid.Width != gw)
                    {
                        grid.Width = gw;
                    }

                    const int colGap = 10;
                    const int rowGap = 10;
                    int tileW = Math.Max(140, (gw - colGap) / 2);
                    int tileInnerW = Math.Max(132, tileW - 8);
                    int thumbSide = Math.Clamp(tileInnerW - 20, 128, 360);
                    int cellInnerH = 10 + thumbSide + 8 + l1H + 2 + l2H + 10;
                    int cellH = cellInnerH + 8;

                    int rows = (tileUpdaters.Count + 1) / 2;
                    for (int i = 0; i < tileUpdaters.Count; i++)
                    {
                        int cIdx = i % 2;
                        int rIdx = i / 2;
                        int tx = cIdx == 0 ? 0 : Math.Max(tileW + colGap, gw - tileW);
                        int ty = rIdx * (cellH + rowGap);
                        var (cell, layoutTile) = tileUpdaters[i];
                        if (cell.IsDisposed) continue;
                        cell.SetBounds(tx, ty, tileW, cellH);
                        layoutTile(thumbSide);
                    }

                    grid.Height = rows * cellH + Math.Max(0, rows - 1) * rowGap;
                }

                col.Controls.Add(grid);
                bodyControl = grid;
                bodyBottomMargin = 20;
                layoutGalleryGridAction = LayoutGalleryGrid;
            }

            bool layingOutGallery = false;
            void LayoutGalleryTopBar()
            {
                if (layingOutGallery || galTopBar.IsDisposed || col.IsDisposed || page.IsDisposed) return;
                layingOutGallery = true;
                try
                {
                    int cwTotal = col.Width;
                    if (cwTotal < 100) return;

                    if (galTopBar.Width != cwTotal)
                    {
                        galTopBar.Width = cwTotal;
                    }

                    int innerLeft = 4;
                    int innerRight = cwTotal - 4;
                    int availW = Math.Max(80, innerRight - innerLeft);
                    const int gap = 8;
                    if (backW + gap + countW <= availW)
                    {
                        galTopBar.Height = 42;
                        btnBack.SetBounds(innerLeft, 2, backW, 38);
                        countChip.SetBounds(innerRight - countW, 4, countW, 34);
                    }
                    else
                    {
                        int bw = Math.Min(backW, availW);
                        int cw = Math.Min(countW, availW);
                        galTopBar.Height = 82;
                        btnBack.SetBounds(innerLeft, 2, bw, 38);
                        countChip.SetBounds(Math.Max(innerLeft, innerRight - cw), 44, cw, 34);
                    }

                    lblTitle.SetBounds(4, galTopBar.Bottom + 6, Math.Max(140, cwTotal - 8), lblTitle.Height);
                    bodyControl.Top = lblTitle.Bottom + 10;
                    if (bodyControl.Width != cwTotal)
                    {
                        bodyControl.Width = cwTotal;
                    }
                    layoutGalleryGridAction?.Invoke();

                    int targetColH = bodyControl.Bottom + bodyBottomMargin;
                    if (col.Height != targetColH)
                    {
                        col.Height = targetColH;
                    }
                    (page as AndroidOverlayScrollPanel)?.SyncLayout();
                }
                finally
                {
                    layingOutGallery = false;
                }
            }

            galTopBar.Resize += (s, e) => LayoutGalleryTopBar();
            col.Resize += (s, e) => LayoutGalleryTopBar();
            LayoutGalleryTopBar();
            return page;
        }
    }
}
