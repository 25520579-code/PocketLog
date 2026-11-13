using Guna.UI2.WinForms;

namespace PocketLog.Client
{
    public partial class Client
    {
        // ====================================================================
        // MÀN HÌNH BẢNG TIN (NEWFEED)
        // ====================================================================
        private Control BuildNewFeedScreen() => BuildFeedScreen();

        private Control BuildFeedScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            root.Controls.Add(MakeLabel("Ảnh mới nhất", 20, y + 4, 10.5F, FontStyle.Bold, clrTextMain));

            var btnGallery = MakeBtn("Kho ảnh ›", 150, y, 90, 28, 14, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var btnFriendsTop = MakeBtn($"👥 Bạn bè ({myFriends.Count}) ›", 246, y, 148, 28, 14, clrSoftIndigo, clrIndigo, clrIndigo, 1, 7.8F);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            root.Controls.Add(btnGallery);
            root.Controls.Add(btnFriendsTop);
            y += 36;

            LocketPost? latestMine = posts.FirstOrDefault(p => p.IsMine);
            if (latestMine != null)
            {
                var hero = CreateLocketPostCard(latestMine, true);
                hero.Location = new Point(20, y);
                root.Controls.Add(hero);
                y += hero.Height + 16;
            }
            else
            {
                Guna2Panel emptyMine = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(374, 96),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1,
                    Cursor = Cursors.Hand
                };
                emptyMine.Controls.Add(MakeLabel("📷 Bạn chưa đăng tấm ảnh nào", 82, 24, 9.5F, FontStyle.Bold, clrPrimaryGreen));
                emptyMine.Controls.Add(MakeLabel("Bấm sang tab 'Chụp ảnh' để chụp bằng Camera!", 52, 50, 8.2F, FontStyle.Regular, clrTextMuted));
                emptyMine.Click += (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
                root.Controls.Add(emptyMine);
                y += 110;
            }

            Guna2Panel divider = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 34),
                BorderRadius = 10,
                FillColor = clrSoftIndigo,
                BorderColor = Color.FromArgb(180, 188, 240),
                BorderThickness = 1
            };
            divider.Controls.Add(new Label
            {
                Text = "⬇ Lướt xuống • Bảng tin của bạn bè",
                Font = new Font("Segoe UI", 8.8F, FontStyle.Bold),
                ForeColor = clrIndigo,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            root.Controls.Add(divider);
            y += 44;

            var friendPosts = posts.Where(p => !p.IsMine).ToList();
            if (friendPosts.Count == 0)
            {
                Guna2Panel noFriendPostBox = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(374, 92),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    Cursor = Cursors.Hand
                };
                noFriendPostBox.Controls.Add(MakeLabel("🔒 Chưa có bài đăng nào từ bạn bè", 74, 20, 9.5F, FontStyle.Bold, clrIndigo));
                noFriendPostBox.Controls.Add(MakeLabel("Bấm vào '👥 Bạn bè' ở góc trên để kết bạn mới thấy bài nhau!", 26, 48, 8F, FontStyle.Regular, clrTextMuted));
                noFriendPostBox.Click += (s, e) => OpenFriendsDrawer();
                root.Controls.Add(noFriendPostBox);
                y += 108;
            }
            else
            {
                foreach (var fp in friendPosts)
                {
                    var card = CreateLocketPostCard(fp, false);
                    card.Location = new Point(20, y);
                    root.Controls.Add(card);
                    y += card.Height + 16;
                }
            }

            root.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            return root;
        }

        private Guna2Panel CreateLocketPostCard(LocketPost post, bool isMyTopCard)
        {
            Guna2Panel card = new Guna2Panel
            {
                Size = new Size(374, 356),
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = isMyTopCard ? 2 : 1,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            Guna2CircleButton avatar = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(post.Author) ? "U" : post.Author.Substring(0, 1).ToUpper(),
                Size = new Size(34, 34),
                Location = new Point(14, 12),
                FillColor = isMyTopCard ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            string tagFriendsStr = (post.TaggedFriends != null && post.TaggedFriends.Count > 0)
                ? $"👥 Tag: {string.Join(", ", post.TaggedFriends)}"
                : "🔒 Cá nhân";

            string topText = post.HasTag
                ? $"{post.Category} — {post.NoteTop} ({tagFriendsStr})"
                : "📷 Ảnh khoảnh khắc (Không gắn tag)";

            card.Controls.Add(avatar);
            card.Controls.Add(MakeLabel($"{post.Author} • {post.Time}", 56, 10, 9F, FontStyle.Bold, clrTextMain));
            card.Controls.Add(MakeLabel(topText, 56, 30, 7.8F, FontStyle.Bold, post.HasTag ? clrPrimaryGreen : clrTextMuted));

            Guna2Panel photoFrame = new Guna2Panel
            {
                Location = new Point(14, 56),
                Size = new Size(346, 284),
                BorderRadius = 20,
                FillColor = post.CustomImage != null ? clrCardBg : post.PhotoBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            var pillCaption = MakeBtn(post.CaptionInside, 23, 224, 300, 44, 18, Color.FromArgb(32, 38, 35), Color.White, Color.FromArgb(180, 210, 195), 1, 8.2F);
            if (isMyTopCard) pillCaption.Click += (s, e) => OpenMyGalleryScreen();

            if (post.CustomImage != null)
            {
                PictureBox pic = new PictureBox { Dock = DockStyle.Fill, Image = post.CustomImage, SizeMode = PictureBoxSizeMode.StretchImage };
                if (isMyTopCard) pic.Click += (s, e) => OpenMyGalleryScreen();
                pic.Controls.Add(pillCaption);
                photoFrame.Controls.Add(pic);
            }
            else
            {
                Label art = new Label
                {
                    Text = isMyTopCard ? $"{post.Emoji}\n(Bấm vào ảnh để mở Kho ảnh cá nhân)" : post.Emoji,
                    Font = new Font("Segoe UI Emoji", isMyTopCard ? 12.5F : 40F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
                };
                if (isMyTopCard) art.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(art);
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
            Panel galleryRoot = new Panel { BackColor = clrAppBg, AutoScroll = true };
            var btnBack = MakeBtn("‹ Quay lại Bảng tin", 20, 14, 150, 36, 12, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.5F);
            btnBack.Click += (s, e) => ActivateMenu(btnTabFeed, BuildFeedScreen());

            galleryRoot.Controls.Add(btnBack);
            galleryRoot.Controls.Add(MakeLabel($"Kho ảnh chụp của {currentFullName}", 20, 58, 12F, FontStyle.Bold, clrTextMain));

            var myPosts = posts.Where(p => p.IsMine).ToList();
            for (int i = 0; i < myPosts.Count; i++)
            {
                var item = myPosts[i];
                int col = i % 2, row = i / 2;
                Guna2Panel cell = new Guna2Panel
                {
                    Location = new Point(20 + col * 192, 92 + row * 212),
                    Size = new Size(182, 198),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                Guna2Panel mini = new Guna2Panel { Location = new Point(8, 8), Size = new Size(166, 122), BorderRadius = 12, FillColor = item.PhotoBg };
                if (item.CustomImage != null)
                    mini.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = item.CustomImage, SizeMode = PictureBoxSizeMode.StretchImage });
                else
                    mini.Controls.Add(new Label { Text = item.Emoji, Font = new Font("Segoe UI Emoji", 24F), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });

                string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0) ? $"Tag: {string.Join(", ", item.TaggedFriends)}" : "Cá nhân";
                cell.Controls.Add(mini);
                cell.Controls.Add(MakeLabel(item.HasTag ? item.Category : "📷 Không gắn tag", 10, 136, 8F, FontStyle.Bold, item.HasTag ? clrPrimaryGreen : clrTextMuted));
                cell.Controls.Add(MakeLabel(item.HasTag ? $"{item.NoteTop}\n({tagTxt})" : item.CaptionInside, 10, 154, 7.6F, FontStyle.Regular, clrTextMain));
                galleryRoot.Controls.Add(cell);
            }

            pnlMainContainer.Controls.Clear();
            galleryRoot.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(galleryRoot);
        }
    }
}
