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
                Location = new Point(4, 60),
                Size = new Size(this.ClientSize.Width - 8, this.ClientSize.Height - 136),
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
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                UseTransparentBackground = true
            };
            pnlDrawerBackdrop.Controls.Add(pnlFriendsDrawer);
            this.Controls.Add(pnlDrawerBackdrop);

            pnlDrawerBackdrop.Resize += (s, e) =>
            {
                int drawerW = Math.Clamp((int)(pnlDrawerBackdrop.Width * 0.80f), 330, 520);
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
                    pnlFriendsDrawer.Left = Math.Max(drawerTargetX, cur - Math.Max(25, (cur - drawerTargetX) / 2));
                }
                else if (cur < drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Min(drawerTargetX, cur + Math.Max(25, (drawerTargetX - cur) / 2));
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
            if (currentUserId > 0)
            {
                _ = SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
            }

            pnlDrawerBackdrop.Bounds = pnlMainContainer.Bounds;
            int drawerW = Math.Clamp((int)(pnlDrawerBackdrop.Width * 0.80f), 330, 520);
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

            pnlFriendsDrawer.Controls.Add(MakeLabel("👥 Bạn bè của bạn", 16, 16, 11F, FontStyle.Bold, clrTextMain));

            var btnClose = MakeBtn("✕", dw - 48, 12, 34, 32, 10, clrAppBg, clrTextMain, clrBorderLight);
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            int searchW = dw - 130;
            Guna2TextBox txtSearch = new Guna2TextBox
            {
                Text = keyword,
                PlaceholderText = "Nhập đúng tên/username để kết bạn...",
                Location = new Point(16, 54),
                Size = new Size(searchW, 38),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnAddNew = MakeBtn("+ Kết bạn", 16 + searchW + 6, 54, 92, 38, 10, clrPrimaryGreen, Color.White, clrPrimaryGreen);

            Panel listScroll = new Panel
            {
                Location = new Point(14, 102),
                Size = new Size(dw - 26, pnlFriendsDrawer.Height - 114),
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

                listScroll.Controls.Add(MakeLabel($"✓ Bạn bè đã kết nối ({filteredFriends.Count})", 2, y, 8.8F, FontStyle.Bold, clrPrimaryGreen));
                y += 26;

                if (filteredFriends.Count == 0)
                {
                    listScroll.Controls.Add(MakeLabel("Chưa có bạn bè nào.\nHãy nhập tên tài khoản của bạn bè vào ô phía trên và bấm '+ Kết bạn'!", 4, y, 8.2F, FontStyle.Italic, clrTextMuted, rowW));
                    y += 48;
                }
                else
                {
                    foreach (var fUser in filteredFriends)
                    {
                        var card = BuildFriendItemRow(fUser, rowW);
                        card.Location = new Point(0, y);
                        listScroll.Controls.Add(card);
                        y += 58;
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

        private Guna2Panel BuildFriendItemRow(FriendUser u, int rowWidth)
        {
            Guna2Panel row = new Guna2Panel
            {
                Size = new Size(rowWidth, 52),
                BorderRadius = 12,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            Guna2CircleButton avt = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(u.Name) ? "U" : u.Name.Substring(0, 1).ToUpper(),
                Size = new Size(34, 34),
                Location = new Point(10, 9),
                FillColor = clrPrimaryGreen,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
                UseTransparentBackground = true,
                BackColor = Color.Transparent
            };

            row.Controls.Add(avt);
            row.Controls.Add(MakeLabel(u.Name, 52, 7, 9F, FontStyle.Bold, clrTextMain, rowWidth - 150));
            row.Controls.Add(MakeLabel($"{u.Handle} • {(u.IsOnline ? "🟢 Online" : "⚪ Offline")}", 52, 27, 7.8F, FontStyle.Regular, u.IsOnline ? clrPrimaryGreen : clrTextMuted, rowWidth - 150));
            row.Controls.Add(MakeBtn("✓ Bạn bè", rowWidth - 90, 12, 80, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F));

            return row;
        }
    }
}