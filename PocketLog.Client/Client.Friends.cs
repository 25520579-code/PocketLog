using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        private void SetupFriendsDrawer()
        {
            pnlDrawerBackdrop = new Panel
            {
                Location = new Point(1, 56),
                Size = new Size(this.ClientSize.Width - 2, this.ClientSize.Height - 124),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(215, 224, 234),
                Visible = false
            };
            pnlDrawerBackdrop.Click += (s, e) => CloseFriendsDrawer();

            pnlFriendsDrawer = new Guna2Panel
            {
                Location = new Point(pnlDrawerBackdrop.Width, 0),
                Size = new Size(360, pnlDrawerBackdrop.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            pnlDrawerBackdrop.Controls.Add(pnlFriendsDrawer);
            this.Controls.Add(pnlDrawerBackdrop);

            pnlDrawerBackdrop.Resize += (s, e) =>
            {
                int drawerW = Math.Clamp((int)(pnlDrawerBackdrop.Width * 0.84f), 336, 460);
                pnlFriendsDrawer.Width = drawerW;
                pnlFriendsDrawer.Height = pnlDrawerBackdrop.Height;
                if (pnlDrawerBackdrop.Visible)
                {
                    drawerTargetX = pnlDrawerBackdrop.Width - drawerW;
                    pnlFriendsDrawer.Left = drawerTargetX;
                    RenderFriendsDrawerContent("");
                }
            };

            drawerTimer = new System.Windows.Forms.Timer { Interval = 12 };
            drawerTimer.Tick += (s, e) =>
            {
                int cur = pnlFriendsDrawer.Left;
                if (cur > drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Max(drawerTargetX, cur - Math.Max(28, (cur - drawerTargetX) / 2));
                }
                else if (cur < drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Min(drawerTargetX, cur + Math.Max(28, (drawerTargetX - cur) / 2));
                    if (pnlFriendsDrawer.Left >= pnlDrawerBackdrop.Width)
                    {
                        pnlDrawerBackdrop.Visible = false;
                        drawerTimer.Stop();
                    }
                }
                else drawerTimer.Stop();
            };
        }

        private void OpenFriendsDrawer()
        {
            friendDrawerFeedback = "";
            if (currentUserId > 0)
            {
                _ = SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
            }

            pnlDrawerBackdrop.Bounds = pnlMainContainer.Bounds;
            int drawerW = Math.Clamp((int)(pnlDrawerBackdrop.Width * 0.84f), 336, 460);
            pnlFriendsDrawer.Size = new Size(drawerW, pnlDrawerBackdrop.Height);

            RenderFriendsDrawerContent("");
            pnlDrawerBackdrop.Visible = true;
            pnlDrawerBackdrop.BringToFront();
            pnlFriendsDrawer.Left = pnlDrawerBackdrop.Width;
            drawerTargetX = pnlDrawerBackdrop.Width - drawerW;
            drawerTimer.Start();
        }

        private void CloseFriendsDrawer()
        {
            drawerTargetX = pnlDrawerBackdrop.Width;
            drawerTimer.Start();
        }

        private void RenderFriendsDrawerContent(string keyword)
        {
            pnlFriendsDrawer.Controls.Clear();
            int dw = pnlFriendsDrawer.Width;

            pnlFriendsDrawer.Controls.Add(MakeSoftIconBadge(UiIcon.Users, 16, 14, 32, 10, clrSoftGreenBg, clrPrimaryGreen));
            var lblHead = MakeLabel("Bạn bè của bạn", 56, 17, 11F, FontStyle.Bold, clrTextMain);
            lblHead.Font = FontBold(11F);
            pnlFriendsDrawer.Controls.Add(lblHead);

            var btnClose = MakeBtn("", dw - 48, 13, 34, 32, 11, clrAppBg, clrTextMain, Color.Transparent, 0);
            btnClose.Image = CreateVectorIcon(UiIcon.Close, clrIndigo, 15);
            btnClose.ImageSize = new Size(15, 15);
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            int searchW = dw - 134;
            var txtSearch = MakeTextBox(keyword, "Tìm bạn hoặc nhập username...", 16, 56, searchW, 38, clrCardBg);

            var btnAddNew = MakeBtn("+ Kết bạn", 16 + searchW + 8, 56, 94, 38, 12, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 8.4F);

            int listTopY = 104;
            if (!string.IsNullOrEmpty(friendDrawerFeedback))
            {
                Label lblFeedback = MakeLabel(
                    friendDrawerFeedback,
                    16, 100, 8.2F, FontStyle.Bold,
                    friendDrawerFeedbackSuccess ? clrPrimaryGreen : clrRedDanger,
                    dw - 32);
                pnlFriendsDrawer.Controls.Add(lblFeedback);
                listTopY = 128;
            }

            var listScroll = new SmoothScrollPanel
            {
                Location = new Point(14, listTopY),
                Size = new Size(dw - 26, pnlFriendsDrawer.Height - listTopY - 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true
            };
            listScroll.MouseEnter += (s, e) => listScroll.Focus();

            int rowW = dw - 44;
            Action<string> refreshList = (kw) =>
            {
                listScroll.Controls.Clear();
                string q = (kw ?? "").Trim().ToLower();
                int y = 4;

                var filteredFriends = myFriends
                    .Where(f => string.IsNullOrEmpty(q) || f.Name.ToLower().Contains(q) || f.Handle.ToLower().Contains(q))
                    .ToList();

                listScroll.Controls.Add(MakeLabel($"Đã kết nối ({filteredFriends.Count})", 4, y, 8.8F, FontStyle.Bold, clrPrimaryGreen));
                y += 26;

                if (filteredFriends.Count == 0)
                {
                    listScroll.Controls.Add(MakeLabel("Chưa có bạn bè phù hợp. Bạn có thể chọn nhanh bên dưới.", 4, y, 8.2F, FontStyle.Regular, clrTextMuted, rowW));
                    y += 34;
                }
                else
                {
                    foreach (var fUser in filteredFriends)
                    {
                        var card = BuildFriendItemRow(fUser, rowW, true);
                        card.Location = new Point(0, y);
                        listScroll.Controls.Add(card);
                        y += 62;
                    }
                }

                y += 10;
                var filteredSuggestions = friendSuggestions
                    .Where(s => string.IsNullOrEmpty(q) || s.Name.ToLower().Contains(q) || s.Handle.ToLower().Contains(q))
                    .ToList();

                listScroll.Controls.Add(MakeLabel($"Gợi ý kết bạn trên hệ thống ({filteredSuggestions.Count})", 4, y, 8.8F, FontStyle.Bold, clrIndigo));
                y += 26;

                if (filteredSuggestions.Count == 0)
                {
                    listScroll.Controls.Add(MakeLabel("Không còn người dùng mới nào để gợi ý.", 4, y, 8.2F, FontStyle.Regular, clrTextMuted, rowW));
                    y += 32;
                }
                else
                {
                    foreach (var sUser in filteredSuggestions)
                    {
                        var card = BuildFriendItemRow(sUser, rowW, false);
                        card.Location = new Point(0, y);
                        listScroll.Controls.Add(card);
                        y += 62;
                    }
                }

                listScroll.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            };

            txtSearch.TextChanged += (s, e) => refreshList(txtSearch.Text);
            btnAddNew.Click += async (s, e) =>
            {
                string raw = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(raw)) return;
                await SendPacketSafeAsync(TcpPacket.Create(PacketType.ADD_FRIEND_REQ, new AddFriendRequestDto
                {
                    RequesterId = currentUserId,
                    TargetKeyword = raw
                }));
                txtSearch.Text = "";
            };

            refreshList(keyword);
            pnlFriendsDrawer.Controls.AddRange(new Control[] { txtSearch, btnAddNew, listScroll });
        }

        private Guna2Panel BuildFriendItemRow(FriendUser u, int rowWidth, bool isAlreadyFriend)
        {
            Guna2Panel row = new Guna2Panel
            {
                Size = new Size(rowWidth, 56),
                BorderRadius = 14,
                FillColor = clrInputBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            Guna2CircleButton avt = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(u.Name) ? "U" : u.Name.Substring(0, 1).ToUpper(),
                Size = new Size(36, 36),
                Location = new Point(10, 10),
                FillColor = isAlreadyFriend ? clrPrimaryGreen : Color.FromArgb(100, 116, 139),
                ForeColor = Color.White,
                Font = FontMedium(9.2F),
                UseTransparentBackground = true,
                BackColor = Color.Transparent,
                Enabled = false
            };

            string stateLabel = u.IsOnline ? "● Đang hoạt động" : "● Ngoại tuyến";
            Color stateColor = u.IsOnline ? Color.FromArgb(16, 185, 129) : clrTextMuted;

            row.Controls.Add(avt);
            row.Controls.Add(MakeLabel(u.Name, 54, 8, 9F, FontStyle.Bold, clrTextMain, rowWidth - 156));
            row.Controls.Add(MakeLabel($"{u.Handle}  •  {stateLabel}", 54, 29, 7.9F, FontStyle.Regular, stateColor, rowWidth - 156));

            if (isAlreadyFriend)
            {
                bool confirmRemove = false;
                var btnRemove = MakeBtn("Xóa bạn", rowWidth - 94, 13, 84, 30, 15, clrAppBg, clrIndigo, clrBorderLight, 1, 8F);
                btnRemove.HoverState.FillColor = clrSoftRedBg;
                btnRemove.HoverState.ForeColor = clrRedDanger;
                btnRemove.HoverState.BorderColor = clrRedDanger;
                btnRemove.Click += async (s, e) =>
                {
                    if (!confirmRemove)
                    {
                        confirmRemove = true;
                        btnRemove.Text = "Chắc xóa?";
                        btnRemove.FillColor = clrSoftRedBg;
                        btnRemove.ForeColor = clrRedDanger;
                        btnRemove.BorderColor = clrRedDanger;
                        return;
                    }
                    btnRemove.Enabled = false;
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.REMOVE_FRIEND_REQ, new AddFriendRequestDto
                    {
                        RequesterId = currentUserId,
                        TargetKeyword = u.Handle.TrimStart('@')
                    }));
                };
                row.Controls.Add(btnRemove);
            }
            else
            {
                var btnQuickAdd = MakeBtn("+ Kết bạn", rowWidth - 94, 13, 84, 30, 15, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 8F);
                btnQuickAdd.Click += async (s, e) =>
                {
                    btnQuickAdd.Enabled = false;
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.ADD_FRIEND_REQ, new AddFriendRequestDto
                    {
                        RequesterId = currentUserId,
                        TargetKeyword = u.Handle.TrimStart('@')
                    }));
                };
                row.Controls.Add(btnQuickAdd);
            }

            return row;
        }
    }
}