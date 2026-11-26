using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace PocketLog.Client
{
    public partial class Client
    {
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

            int frBtnW = Math.Max(140, MeasurePillBtnWidth($"Bạn bè ({myFriends.Count})", 8.8F, 19, true, 16));
            var btnFriendsTop = MakeBtn($"Bạn bè ({myFriends.Count})", w - frBtnW, 2, frBtnW, 38, 19, clrCardBg, clrIndigo, clrBorderLight, 1, 8.8F);
            btnFriendsTop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnFriendsTop, UiIcon.Users, clrIndigo, 16);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            int galBtnW = Math.Max(124, MeasurePillBtnWidth("Kho ảnh", 8.8F, 19, true, 16));
            var btnGallery = MakeBtn("Kho ảnh", w - frBtnW - 8 - galBtnW, 2, galBtnW, 38, 19, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.8F);
            btnGallery.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnGallery, UiIcon.Photo, clrPrimaryGreen, 16);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var lblTitle = MakeLabel("Bảng tin", 2, 5, 13F, FontStyle.Bold, clrTextMain, Math.Max(80, btnGallery.Left - 10));
            lblTitle.Font = FontBold(13F);
            lblTitle.Height = TextRenderer.MeasureText("Ág", lblTitle.Font).Height + 4;
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            topBar.Controls.Add(lblTitle);

            topBar.Controls.Add(btnGallery);
            topBar.Controls.Add(btnFriendsTop);
            col.Controls.Add(topBar);
            y += 52;

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
                Guna2Panel emptyMine = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 120),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 22,
                    FillColor = clrCardBg,
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                var camBadge = MakeSoftIconBadge(UiIcon.Camera, (w - 42) / 2, 14, 42, 14, clrSoftGreenBg, clrPrimaryGreen);
                var lblEmpty1 = new Label
                {
                    Text = "Bạn chưa đăng khoảnh khắc nào",
                    Location = new Point(12, 60),
                    Size = new Size(w - 24, 24),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontMedium(10.2F),
                    ForeColor = clrPrimaryGreen,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                var lblEmpty2 = new Label
                {
                    Text = "Chạm vào đây để mở Chụp ảnh và chia sẻ ngay",
                    Location = new Point(12, 86),
                    Size = new Size(w - 24, 22),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontRegular(8.8F),
                    ForeColor = clrTextMuted,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                emptyMine.Resize += (s, e) => camBadge.Left = (emptyMine.Width - camBadge.Width) / 2;

                EventHandler goCam = (s, e) => EnsureTabVisible("CAMERA");
                emptyMine.Click += goCam;
                lblEmpty1.Click += goCam;
                lblEmpty2.Click += goCam;

                emptyMine.Controls.AddRange(new Control[] { camBadge, lblEmpty1, lblEmpty2 });
                col.Controls.Add(emptyMine);
                y += 134;
            }

            // Tiêu đề phân tách bạn bè nhẹ nhàng kiểu Android Feed
            Panel secHeader = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            var lblSec = MakeLabel("Khoảnh khắc từ bạn bè", 4, 5, 10F, FontStyle.Bold, clrIndigo);
            secHeader.Controls.Add(lblSec);

            int lineStartX = lblSec.Right + 12;
            var lineSec = new Panel
            {
                Location = new Point(lineStartX, lblSec.Top + lblSec.Height / 2),
                Size = new Size(Math.Max(16, w - lineStartX - 4), 1),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = clrBorderLight
            };
            secHeader.Resize += (s, e) =>
            {
                int lx = lblSec.Right + 12;
                lineSec.SetBounds(lx, lblSec.Top + lblSec.Height / 2, Math.Max(16, secHeader.Width - lx - 4), 1);
            };
            secHeader.Controls.Add(lineSec);
            col.Controls.Add(secHeader);
            y += 40;

            var friendPosts = posts.Where(p => !p.IsMine).ToList();
            if (friendPosts.Count == 0)
            {
                Guna2Panel noFriendPostBox = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 116),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 22,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                var usrBadge = MakeSoftIconBadge(UiIcon.Users, (w - 40) / 2, 14, 40, 14, clrAppBg, clrIndigo);
                var lblF1 = new Label
                {
                    Text = "Chưa có bài đăng nào từ bạn bè",
                    Location = new Point(12, 58),
                    Size = new Size(w - 24, 24),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontMedium(10F),
                    ForeColor = clrTextMain,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                var lblF2 = new Label
                {
                    Text = "Chạm vào đây để kết nối thêm bạn bè mới",
                    Location = new Point(12, 84),
                    Size = new Size(w - 24, 22),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontRegular(8.6F),
                    ForeColor = clrTextMuted,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                noFriendPostBox.Resize += (s, e) => usrBadge.Left = (noFriendPostBox.Width - usrBadge.Width) / 2;

                EventHandler openFr = (s, e) => OpenFriendsDrawer();
                noFriendPostBox.Click += openFr;
                lblF1.Click += openFr;
                lblF2.Click += openFr;

                noFriendPostBox.Controls.AddRange(new Control[] { usrBadge, lblF1, lblF2 });
                col.Controls.Add(noFriendPostBox);
                y += 130;
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
            return page;
        }

        private Guna2Panel CreateLocketPostCard(LocketPost post, bool isMyTopCard, int cardWidth)
        {
            Guna2Panel card = new Guna2Panel
            {
                Size = new Size(cardWidth, 476),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = clrCardBg,
                BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            Guna2CircleButton avatar = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(post.Author) ? "U" : post.Author.Substring(0, 1).ToUpper(),
                Size = new Size(42, 42),
                Location = new Point(14, 14),
                FillColor = isMyTopCard ? clrPrimaryGreen : Color.FromArgb(100, 116, 139),
                ForeColor = Color.White,
                Font = FontMedium(10.5F),
                UseTransparentBackground = true,
                BackColor = Color.Transparent,
                Enabled = false
            };

            string tagFriendsStr = (post.TaggedFriends != null && post.TaggedFriends.Count > 0)
                ? $"Tag: {string.Join(", ", post.TaggedFriends)}"
                : "Cá nhân";

            string cleanCat = GetCategoryShortClean(post.Category);
            string topText = post.HasTag
                ? $"{cleanCat}  •  {post.NoteTop} ({tagFriendsStr})"
                : "Ảnh khoảnh khắc Pocket";

            var lblAuthor = MakeLabel($"{post.Author}   •   {post.Time}", 64, 10, 10F, FontStyle.Bold, clrTextMain, cardWidth - 80);
            lblAuthor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var lblSub = MakeLabel(topText, 64, 37, 8.8F, post.HasTag ? FontStyle.Bold : FontStyle.Regular, post.HasTag ? clrPrimaryGreen : clrTextMuted, cardWidth - 80);
            lblSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            card.Controls.Add(avatar);
            card.Controls.Add(lblAuthor);
            card.Controls.Add(lblSub);

            int frameW = cardWidth - 24;
            int frameH = 390;
            Guna2Panel photoFrame = new Guna2Panel
            {
                Location = new Point(12, 70),
                Size = new Size(frameW, frameH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 20,
                FillColor = post.CustomImage != null ? clrAppBg : post.PhotoBg,
                BorderThickness = 0,
                UseTransparentBackground = true,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            string capText = string.IsNullOrWhiteSpace(post.CaptionInside) ? "Khoảnh khắc" : post.CaptionInside.Trim();
            int measuredCapW = MeasurePillBtnWidth(capText, 9.5F, 21);
            int maxInitPillW = Math.Max(120, frameW - 24);
            int initPillW = Math.Clamp(measuredCapW, 120, maxInitPillW);
            bool initTwoLines = measuredCapW > maxInitPillW;
            int initPillH = initTwoLines ? 58 : 42;

            var pillCaption = MakeBtn(
                capText,
                (frameW - initPillW) / 2,
                frameH - initPillH - 14,
                initPillW, initPillH, initTwoLines ? 18 : 21,
                Color.FromArgb(24, 33, 47), Color.White, Color.Transparent, 0, 9.5F);
            if (isMyTopCard) pillCaption.Click += (s, e) => OpenMyGalleryScreen();

            Action<int> syncCaptionPill = (pw) =>
            {
                int maxW = Math.Max(120, pw - 24);
                int newW = Math.Clamp(measuredCapW, 120, maxW);
                bool multi = measuredCapW > maxW;
                int newH = multi ? 58 : 42;
                pillCaption.BorderRadius = multi ? 18 : 21;
                pillCaption.SetBounds((pw - newW) / 2, photoFrame.Height - newH - 14, newW, newH);
            };

            if (post.CustomImage != null)
            {
                Guna2PictureBox pic = new Guna2PictureBox
                {
                    Dock = DockStyle.Fill,
                    BorderRadius = 20,
                    Image = FitImageCover(post.CustomImage, frameW, frameH),
                    SizeMode = PictureBoxSizeMode.Normal,
                    UseTransparentBackground = true,
                    BackColor = Color.Transparent,
                    Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
                };

                int lastRenderedW = frameW;
                photoFrame.Resize += (s, e) =>
                {
                    int pw = photoFrame.Width;
                    if (pw > 20 && Math.Abs(pw - lastRenderedW) >= 8)
                    {
                        lastRenderedW = pw;
                        Image prev = pic.Image;
                        pic.Image = FitImageCover(post.CustomImage, pw, photoFrame.Height);
                        prev?.Dispose();
                    }
                    syncCaptionPill(pw);
                };

                pic.Disposed += (s, e) => { pic.Image?.Dispose(); };
                if (isMyTopCard) pic.Click += (s, e) => OpenMyGalleryScreen();

                photoFrame.Controls.Add(pic);
                photoFrame.Controls.Add(pillCaption);
                pillCaption.BringToFront();
            }
            else
            {
                var centerIco = MakeSoftIconBadge(GetCategoryIcon(post.Category), (frameW - 64) / 2, (frameH - 90) / 2, 64, 22, Color.FromArgb(60, 255, 255, 255), Color.White);
                photoFrame.Resize += (s, e) =>
                {
                    int pw = photoFrame.Width;
                    centerIco.Left = (pw - centerIco.Width) / 2;
                    syncCaptionPill(pw);
                };
                if (isMyTopCard) centerIco.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(centerIco);
                photoFrame.Controls.Add(pillCaption);
                pillCaption.BringToFront();
            }

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

            int backW = Math.Max(188, MeasurePillBtnWidth("Quay lại Bảng tin", 8.6F, 19, true, 15));
            var btnBack = MakeBtn("Quay lại Bảng tin", 0, y, backW, 38, 19, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.6F);
            AttachBtnIcon(btnBack, UiIcon.ChevronLeft, clrPrimaryGreen, 15);
            btnBack.Click += (s, e) =>
            {
                isFeedShowingGallery = false;
                RefreshPaneContent("FEED");
            };
            col.Controls.Add(btnBack);
            y += 48;

            var lblTitle = MakeLabel($"Kho ảnh của {currentFullName}", 2, y, 11.5F, FontStyle.Bold, clrTextMain, w - 8);
            lblTitle.Font = FontBold(11.5F);
            lblTitle.Height = TextRenderer.MeasureText("Ág", lblTitle.Font).Height + 4;
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            col.Controls.Add(lblTitle);
            y += 36;

            var myPosts = posts.Where(p => p.IsMine).ToList();
            if (myPosts.Count == 0)
            {
                var lblEmptyGal = MakeLabel("Chưa có tấm ảnh nào trong kho lưu trữ của bạn.", 4, y + 8, 9F, FontStyle.Regular, clrTextMuted, w - 8);
                lblEmptyGal.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                col.Controls.Add(lblEmptyGal);
                y += 48;
            }
            else
            {
                int rows = (myPosts.Count + 1) / 2;
                int rowHeight = 222;
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
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));

                for (int i = 0; i < myPosts.Count; i++)
                {
                    var item = myPosts[i];
                    Guna2Panel cell = new Guna2Panel
                    {
                        Size = new Size(200, rowHeight - 12),
                        Dock = DockStyle.Fill,
                        Margin = new Padding(i % 2 == 0 ? 0 : 6, 4, i % 2 == 0 ? 6 : 0, 8),
                        BorderRadius = 18,
                        FillColor = clrCardBg,
                        BorderColor = clrBorderLight,
                        BorderThickness = 1,
                        UseTransparentBackground = true
                    };

                    Guna2Panel mini = new Guna2Panel
                    {
                        Location = new Point(8, 8),
                        Size = new Size(184, 128),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        BorderRadius = 14,
                        FillColor = item.PhotoBg,
                        UseTransparentBackground = true
                    };

                    if (item.CustomImage != null)
                    {
                        var miniPic = new Guna2PictureBox
                        {
                            Dock = DockStyle.Fill,
                            BorderRadius = 14,
                            Image = FitImageCover(item.CustomImage, 220, 140),
                            SizeMode = PictureBoxSizeMode.Zoom,
                            UseTransparentBackground = true
                        };
                        miniPic.Disposed += (s, e) => miniPic.Image?.Dispose();
                        mini.Controls.Add(miniPic);
                    }
                    else
                    {
                        var ico = MakeSoftIconBadge(GetCategoryIcon(item.Category), 70, 42, 44, 14, Color.FromArgb(60, 255, 255, 255), Color.White);
                        mini.Resize += (s, e) => ico.Left = (mini.Width - ico.Width) / 2;
                        mini.Controls.Add(ico);
                    }

                    string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0)
                        ? $"Tag: {string.Join(", ", item.TaggedFriends)}"
                        : "Cá nhân";

                    var l1 = MakeLabel(item.HasTag ? GetCategoryShortClean(item.Category) : "Ảnh Pocket", 10, 142, 8.8F, FontStyle.Bold, item.HasTag ? clrPrimaryGreen : clrTextMuted, 180);
                    l1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                    var l2 = MakeLabel(item.HasTag ? $"{item.NoteTop} ({tagTxt})" : item.CaptionInside, 10, 169, 8.4F, FontStyle.Regular, clrTextMain, 180);
                    l2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                    cell.Controls.AddRange(new Control[] { mini, l1, l2 });
                    grid.Controls.Add(cell, i % 2, i / 2);
                }

                col.Controls.Add(grid);
                y += rows * rowHeight;
            }

            col.Height = y + 20;
            return page;
        }
    }
}