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
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            root.Controls.Add(MakeLabel("Bảng tin Locket", 20, y + 4, 10.5F, FontStyle.Bold, clrTextMain));

            var btnGallery = MakeBtn("Kho ảnh ›", 158, y, 88, 30, 14, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var btnFriendsTop = MakeBtn($"👥 Bạn bè ({myFriends.Count}) ›", 252, y, 148, 30, 14, clrSoftIndigo, clrIndigo, clrBorderLight, 1, 8F);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            root.Controls.Add(btnGallery);
            root.Controls.Add(btnFriendsTop);
            y += 40;

            LocketPost latestMine = posts.FirstOrDefault(p => p.IsMine);
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
                    Size = new Size(380, 96),
                    BorderRadius = 18,
                    FillColor = clrCardBg,
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                emptyMine.Controls.Add(MakeLabel("📷 Bạn chưa đăng tấm ảnh nào", 82, 24, 9.5F, FontStyle.Bold, clrPrimaryGreen));
                emptyMine.Controls.Add(MakeLabel("Bấm sang tab 'Chụp ảnh' để chia sẻ khoảnh khắc!", 48, 50, 8.2F, FontStyle.Regular, clrTextMuted));
                emptyMine.Click += (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
                root.Controls.Add(emptyMine);
                y += 110;
            }

            Guna2Panel divider = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(380, 34),
                BorderRadius = 12,
                FillColor = clrSoftIndigo,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            divider.Controls.Add(new Label
            {
                Text = "⬇ Lịch sử khoảnh khắc của bạn bè",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = clrIndigo,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            root.Controls.Add(divider);
            y += 46;

            var friendPosts = posts.Where(p => !p.IsMine).ToList();
            if (friendPosts.Count == 0)
            {
                Guna2Panel noFriendPostBox = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(380, 92),
                    BorderRadius = 18,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true,
                    Cursor = Cursors.Hand
                };
                noFriendPostBox.Controls.Add(MakeLabel("🔒 Chưa có bài đăng nào từ bạn bè", 74, 22, 9.2F, FontStyle.Bold, clrIndigo));
                noFriendPostBox.Controls.Add(MakeLabel("Bấm vào '👥 Bạn bè' ở góc trên để thêm bạn mới!", 44, 48, 8F, FontStyle.Regular, clrTextMuted));
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
                Size = new Size(380, 360),
                BorderRadius = 24,
                FillColor = clrCardBg,
                BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = isMyTopCard ? 2 : 1,
                UseTransparentBackground = true,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            Guna2CircleButton avatar = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(post.Author) ? "U" : post.Author.Substring(0, 1).ToUpper(),
                Size = new Size(36, 36),
                Location = new Point(14, 12),
                FillColor = isMyTopCard ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                UseTransparentBackground = true,
                BackColor = Color.Transparent
            };

            string tagFriendsStr = (post.TaggedFriends != null && post.TaggedFriends.Count > 0)
                ? $"Tag: {string.Join(", ", post.TaggedFriends)}"
                : "Cá nhân";

            string topText = post.HasTag
                ? $"{post.Category} • {post.NoteTop} ({tagFriendsStr})"
                : "📷 Ảnh khoảnh khắc Locket";

            card.Controls.Add(avatar);
            card.Controls.Add(MakeLabel($"{post.Author} • {post.Time}", 58, 10, 9F, FontStyle.Bold, clrTextMain, 306));
            card.Controls.Add(MakeLabel(topText, 58, 30, 7.8F, FontStyle.Bold, post.HasTag ? clrPrimaryGreen : clrTextMuted, 306));

            Guna2Panel photoFrame = new Guna2Panel
            {
                Location = new Point(14, 56),
                Size = new Size(352, 288),
                BorderRadius = 22,
                FillColor = post.CustomImage != null ? clrCardBg : post.PhotoBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            var pillCaption = MakeBtn(post.CaptionInside, 26, 228, 300, 42, 18, Color.FromArgb(30, 41, 59), Color.White, clrSoftGreenBorder, 1, 8.2F);
            if (isMyTopCard) pillCaption.Click += (s, e) => OpenMyGalleryScreen();

            if (post.CustomImage != null)
            {
                Guna2PictureBox pic = new Guna2PictureBox
                {
                    Location = new Point(2, 2),
                    Size = new Size(348, 284),
                    BorderRadius = 20,
                    Image = post.CustomImage,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    UseTransparentBackground = true,
                    BackColor = Color.Transparent
                };
                if (isMyTopCard) pic.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(pic);
                photoFrame.Controls.Add(pillCaption);
                pillCaption.BringToFront();
            }
            else
            {
                Label art = new Label
                {
                    Text = isMyTopCard ? $"{post.Emoji}\n(Bấm vào khung để mở Kho ảnh)" : post.Emoji,
                    Font = new Font("Segoe UI Emoji", isMyTopCard ? 12F : 38F, FontStyle.Bold),
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
            galleryRoot.Controls.Add(MakeLabel($"Kho ảnh của {currentFullName}", 20, 58, 11.5F, FontStyle.Bold, clrTextMain));

            var myPosts = posts.Where(p => p.IsMine).ToList();
            for (int i = 0; i < myPosts.Count; i++)
            {
                var item = myPosts[i];
                int col = i % 2, row = i / 2;
                Guna2Panel cell = new Guna2Panel
                {
                    Location = new Point(20 + col * 194, 92 + row * 214),
                    Size = new Size(184, 200),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                Guna2Panel mini = new Guna2Panel
                {
                    Location = new Point(8, 8),
                    Size = new Size(168, 122),
                    BorderRadius = 12,
                    FillColor = item.PhotoBg,
                    UseTransparentBackground = true
                };
                if (item.CustomImage != null)
                {
                    mini.Controls.Add(new Guna2PictureBox
                    {
                        Location = new Point(0, 0),
                        Size = new Size(168, 122),
                        BorderRadius = 12,
                        Image = item.CustomImage,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        UseTransparentBackground = true
                    });
                }
                else
                {
                    mini.Controls.Add(new Label
                    {
                        Text = item.Emoji,
                        Font = new Font("Segoe UI Emoji", 24F),
                        ForeColor = Color.White,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.Transparent
                    });
                }

                string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0) ? $"Tag: {string.Join(", ", item.TaggedFriends)}" : "Cá nhân";
                cell.Controls.Add(mini);
                cell.Controls.Add(MakeLabel(item.HasTag ? item.Category : "📷 Ảnh Locket", 10, 136, 8F, FontStyle.Bold, item.HasTag ? clrPrimaryGreen : clrTextMuted, 164));
                cell.Controls.Add(MakeLabel(item.HasTag ? $"{item.NoteTop} ({tagTxt})" : item.CaptionInside, 10, 156, 7.6F, FontStyle.Regular, clrTextMain, 164));
                galleryRoot.Controls.Add(cell);
            }

            pnlMainContainer.Controls.Clear();
            galleryRoot.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(galleryRoot);
        }
    }
}