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
        // ====================================================================
        // NGĂN KÉO BẠN BÈ & DANH BẠ
        // ====================================================================
        private void SetupFriendsDrawer()
        {
            pnlDrawerBackdrop = new Panel
            {
                Location = new Point(0, 52),
                Size = new Size(420, 694),
                BackColor = Color.FromArgb(222, 230, 226),
                Visible = false
            };
            pnlDrawerBackdrop.Click += (s, e) => CloseFriendsDrawer();

            pnlFriendsDrawer = new Guna2Panel
            {
                Location = new Point(420, 0),
                Size = new Size(344, 694),
                FillColor = clrCardBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2
            };
            pnlDrawerBackdrop.Controls.Add(pnlFriendsDrawer);
            this.Controls.Add(pnlDrawerBackdrop);

            drawerTimer = new System.Windows.Forms.Timer { Interval = 12 };
            drawerTimer.Tick += (s, e) =>
            {
                int cur = pnlFriendsDrawer.Left;
                if (cur > drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Max(drawerTargetX, cur - Math.Max(20, (cur - drawerTargetX) / 2));
                }
                else if (cur < drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Min(drawerTargetX, cur + Math.Max(20, (drawerTargetX - cur) / 2));
                    if (pnlFriendsDrawer.Left >= 420)
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

            RenderFriendsDrawerContent("");
            pnlDrawerBackdrop.Visible = true;
            pnlDrawerBackdrop.BringToFront();
            pnlFriendsDrawer.Left = 420;
            drawerTargetX = 76;
            drawerTimer.Start();
        }

        private void CloseFriendsDrawer()
        {
            drawerTargetX = 420;
            drawerTimer.Start();
        }

        private void RenderFriendsDrawerContent(string keyword)
        {
            pnlFriendsDrawer.Controls.Clear();
            pnlFriendsDrawer.Controls.Add(MakeLabel("👥 Kết bạn & Danh bạ CSDL", 16, 16, 11F, FontStyle.Bold, clrTextMain));

            var btnClose = MakeBtn("✕", 294, 12, 34, 32, 10, clrAppBg, clrTextMain, clrBorderLight);
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            Guna2TextBox txtSearch = new Guna2TextBox
            {
                Text = keyword,
                PlaceholderText = "🔍 Tìm hoặc nhập tên tài khoản...",
                Location = new Point(16, 52),
                Size = new Size(216, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnAddNew = MakeBtn("+ Kết bạn", 238, 52, 90, 36, 10, clrPrimaryGreen, Color.White, clrSoftGreenBorder);

            Panel listScroll = new Panel
            {
                Location = new Point(14, 96),
                Size = new Size(318, 580),
                AutoScroll = true
            };
            listScroll.MouseEnter += (s, e) => listScroll.Focus();

            Action<string> refreshList = (kw) =>
            {
                listScroll.Controls.Clear();
                string q = (kw ?? "").Trim().ToLower();
                int y = 4;

                var filteredFriends = myFriends
                    .Where(f => string.IsNullOrEmpty(q) || f.Name.ToLower().Contains(q) || f.Handle.ToLower().Contains(q))
                    .ToList();

                listScroll.Controls.Add(MakeLabel($"✓ Bạn bè đã kết nối ({filteredFriends.Count}) - Thấy bài nhau", 2, y, 8.2F, FontStyle.Bold, clrPrimaryGreen));
                y += 22;

                if (filteredFriends.Count == 0)
                {
                    listScroll.Controls.Add(MakeLabel("Chưa có bạn bè. Bấm '+ Kết bạn' ở danh sách dưới!", 4, y, 7.8F, FontStyle.Italic, clrTextMuted));
                    y += 26;
                }
                else
                {
                    foreach (var fUser in filteredFriends)
                    {
                        var card = BuildFriendItemRow(fUser, true, null);
                        card.Location = new Point(0, y);
                        listScroll.Controls.Add(card);
                        y += 56;
                    }
                }

                y += 10;
                var sugs = friendSuggestions
                    .Where(u => string.IsNullOrEmpty(q) || u.Name.ToLower().Contains(q) || u.Handle.ToLower().Contains(q))
                    .ToList();

                listScroll.Controls.Add(MakeLabel($"🌐 Tất cả tài khoản khác trong Database ({sugs.Count})", 2, y, 8.2F, FontStyle.Bold, clrIndigo));
                y += 22;

                foreach (var sUser in sugs)
                {
                    var card = BuildFriendItemRow(sUser, false, async () =>
                    {
                        await SendPacketSafeAsync(TcpPacket.Create(PacketType.ADD_FRIEND_REQ, new AddFriendRequestDto
                        {
                            RequesterId = currentUserId,
                            TargetKeyword = sUser.Name
                        }));
                    });
                    card.Location = new Point(0, y);
                    listScroll.Controls.Add(card);
                    y += 56;
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

        private Guna2Panel BuildFriendItemRow(FriendUser u, bool isFriend, Action? onAdd)
        {
            Guna2Panel row = new Guna2Panel
            {
                Size = new Size(296, 50),
                BorderRadius = 12,
                FillColor = isFriend ? clrAppBg : clrSoftIndigo,
                BorderColor = isFriend ? clrBorderLight : Color.FromArgb(180, 188, 240),
                BorderThickness = 1
            };

            Guna2CircleButton avt = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(u.Name) ? "U" : u.Name.Substring(0, 1).ToUpper(),
                Size = new Size(32, 32),
                Location = new Point(8, 9),
                FillColor = isFriend ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            row.Controls.Add(avt);
            row.Controls.Add(MakeLabel(u.Name, 46, 7, 8.8F, FontStyle.Bold, clrTextMain));
            row.Controls.Add(MakeLabel($"{u.Handle} • {(u.IsOnline ? "🟢 Online" : "⚪ Offline")}", 46, 26, 7.6F, FontStyle.Regular, u.IsOnline ? clrPrimaryGreen : clrTextMuted));

            if (!isFriend && onAdd != null)
            {
                var btn = MakeBtn("+ Kết bạn", 204, 10, 84, 30, 8, clrIndigo, Color.White, clrIndigo, 1, 7.6F);
                btn.Click += (s, e) => onAdd();
                row.Controls.Add(btn);
            }
            else
            {
                row.Controls.Add(MakeBtn("✓ Bạn bè", 208, 11, 80, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.6F));
            }
            return row;
        }
    }
}
