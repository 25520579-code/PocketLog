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
                Size = new Size(w, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            var lblTitle = MakeLabel("Bảng tin", 2, 4, 12F, FontStyle.Bold, clrTextMain);
            lblTitle.Font = FontBold(12F);
            topBar.Controls.Add(lblTitle);

            var btnFriendsTop = MakeBtn($"Bạn bè ({myFriends.Count})", w - 122, 2, 122, 32, 16, clrCardBg, clrIndigo, clrBorderLight, 1, 8.3F);
            btnFriendsTop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnFriendsTop, UiIcon.Users, clrIndigo, 15);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            var btnGallery = MakeBtn("Kho ảnh", w - 230, 2, 102, 32, 16, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.3F);
            btnGallery.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnGallery, UiIcon.Photo, clrPrimaryGreen, 15);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            topBar.Controls.Add(btnGallery);
            topBar.Controls.Add(btnFriendsTop);
            col.Controls.Add(topBar);
            y += 46;

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
                    Size = new Size(w, 110),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 20,
                    FillColor = clrCardBg,
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                var camBadge = MakeSoftIconBadge(UiIcon.Camera, (w - 38) / 2, 12, 38, 13, clrSoftGreenBg, clrPrimaryGreen);
                var lblEmpty1 = new Label
                {
                    Text = "Bạn chưa đăng khoảnh khắc nào",
                    Location = new Point(12, 56),
                    Size = new Size(w - 24, 22),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontMedium(9.5F),
                    ForeColor = clrPrimaryGreen,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                var lblEmpty2 = new Label
                {
                    Text = "Chạm vào đây để mở Chụp ảnh và chia sẻ ngay",
                    Location = new Point(12, 78),
                    Size = new Size(w - 24, 20),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontRegular(8.3F),
                    ForeColor = clrTextMuted,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                emptyMine.Resize += (s, e) => camBadge.Left = (emptyMine.Width - camBadge.Width) / 2;

                EventHandler goCam = (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
                emptyMine.Click += goCam;
                lblEmpty1.Click += goCam;
                lblEmpty2.Click += goCam;

                emptyMine.Controls.AddRange(new Control[] { camBadge, lblEmpty1, lblEmpty2 });
                col.Controls.Add(emptyMine);
                y += 124;
            }

            // Tiêu đề phân tách bạn bè nhẹ nhàng kiểu Android Feed
            Panel secHeader = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };
            var lblSec = MakeLabel("Khoảnh khắc từ bạn bè", 4, 5, 9.2F, FontStyle.Bold, clrIndigo);
            var lineSec = new Panel
            {
                Location = new Point(162, 15),
                Size = new Size(w - 166, 1),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = clrBorderLight
            };
            secHeader.Controls.Add(lblSec);
            secHeader.Controls.Add(lineSec);
            col.Controls.Add(secHeader);
            y += 36;

            var friendPosts = posts.Where(p => !p.IsMine).ToList();
            if (friendPosts.Count == 0)
            {
                Guna2Panel noFriendPostBox = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 104),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 20,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                var usrBadge = MakeSoftIconBadge(UiIcon.Users, (w - 36) / 2, 12, 36, 12, clrAppBg, clrIndigo);
                var lblF1 = new Label
                {
                    Text = "Chưa có bài đăng nào từ bạn bè",
                    Location = new Point(12, 54),
                    Size = new Size(w - 24, 22),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontMedium(9.2F),
                    ForeColor = clrTextMain,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                var lblF2 = new Label
                {
                    Text = "Chạm vào đây để kết nối thêm bạn bè mới",
                    Location = new Point(12, 76),
                    Size = new Size(w - 24, 20),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = FontRegular(8.2F),
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
                y += 118;
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
                Size = new Size(cardWidth, 384),
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
                Size = new Size(38, 38),
                Location = new Point(14, 12),
                FillColor = isMyTopCard ? clrPrimaryGreen : Color.FromArgb(100, 116, 139),
                ForeColor = Color.White,
                Font = FontMedium(9.5F),
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
                : "Ảnh khoảnh khắc Locket";

            var lblAuthor = MakeLabel($"{post.Author}   •   {post.Time}", 60, 10, 9.2F, FontStyle.Bold, clrTextMain, cardWidth - 76);
            lblAuthor.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var lblSub = MakeLabel(topText, 60, 31, 8.2F, post.HasTag ? FontStyle.Bold : FontStyle.Regular, post.HasTag ? clrPrimaryGreen : clrTextMuted, cardWidth - 76);
            lblSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            card.Controls.Add(avatar);
            card.Controls.Add(lblAuthor);
            card.Controls.Add(lblSub);

            int frameW = cardWidth - 24;
            int frameH = 310;
            Guna2Panel photoFrame = new Guna2Panel
            {
                Location = new Point(12, 60),
                Size = new Size(frameW, frameH),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 20,
                FillColor = post.CustomImage != null ? clrAppBg : post.PhotoBg,
                BorderThickness = 0,
                UseTransparentBackground = true,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            string capText = string.IsNullOrWhiteSpace(post.CaptionInside) ? "Khoảnh khắc" : post.CaptionInside.Trim();
            int measuredCapW = TextRenderer.MeasureText(capText, FontMedium(8.8F)).Width + 38;
            int initPillW = Math.Clamp(measuredCapW, 108, frameW - 32);

            var pillCaption = MakeBtn(
                capText,
                (frameW - initPillW) / 2,
                frameH - 50,
                initPillW, 36, 18,
                Color.FromArgb(24, 33, 47), Color.White, Color.Transparent, 0, 8.8F);
            if (isMyTopCard) pillCaption.Click += (s, e) => OpenMyGalleryScreen();

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
                    int newPillW = Math.Clamp(measuredCapW, 108, Math.Max(110, pw - 32));
                    pillCaption.Width = newPillW;
                    pillCaption.Left = (pw - newPillW) / 2;
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
                    int newPillW = Math.Clamp(measuredCapW, 108, Math.Max(110, pw - 32));
                    pillCaption.Width = newPillW;
                    pillCaption.Left = (pw - newPillW) / 2;
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
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 14;

            var btnBack = MakeBtn("Quay lại Bảng tin", 0, y, 152, 34, 17, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.6F);
            AttachBtnIcon(btnBack, UiIcon.ChevronLeft, clrPrimaryGreen, 15);
            btnBack.Click += (s, e) => ActivateMenu(btnTabFeed, BuildFeedScreen());
            col.Controls.Add(btnBack);
            y += 46;

            var lblTitle = MakeLabel($"Kho ảnh của {currentFullName}", 2, y, 11.5F, FontStyle.Bold, clrTextMain, w - 8);
            lblTitle.Font = FontBold(11.5F);
            lblTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            col.Controls.Add(lblTitle);
            y += 34;

            var myPosts = posts.Where(p => p.IsMine).ToList();
            if (myPosts.Count == 0)
            {
                col.Controls.Add(MakeLabel("Chưa có tấm ảnh nào trong kho lưu trữ của bạn.", 4, y + 8, 9F, FontStyle.Regular, clrTextMuted, w - 8));
                y += 48;
            }
            else
            {
                int rows = (myPosts.Count + 1) / 2;
                int rowHeight = 212;
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
                        Size = new Size(168, 128),
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
                        var ico = MakeSoftIconBadge(GetCategoryIcon(item.Category), 62, 42, 44, 14, Color.FromArgb(60, 255, 255, 255), Color.White);
                        mini.Resize += (s, e) => ico.Left = (mini.Width - ico.Width) / 2;
                        mini.Controls.Add(ico);
                    }

                    string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0)
                        ? $"Tag: {string.Join(", ", item.TaggedFriends)}"
                        : "Cá nhân";

                    var l1 = MakeLabel(item.HasTag ? GetCategoryShortClean(item.Category) : "Ảnh Locket", 10, 142, 8.3F, FontStyle.Bold, item.HasTag ? clrPrimaryGreen : clrTextMuted, 164);
                    l1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                    var l2 = MakeLabel(item.HasTag ? $"{item.NoteTop} ({tagTxt})" : item.CaptionInside, 10, 164, 7.9F, FontStyle.Regular, clrTextMain, 164);
                    l2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                    cell.Controls.AddRange(new Control[] { mini, l1, l2 });
                    grid.Controls.Add(cell, i % 2, i / 2);
                }

                col.Controls.Add(grid);
                y += rows * rowHeight;
            }

            col.Height = y + 20;
            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(page);
        }
    }
}