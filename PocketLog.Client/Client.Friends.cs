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
        private string currentFriendsSearchKeyword = "";

        private int ComputeDrawerWidth(int backdropWidth)
        {
            int maxAllowed = Math.Max(260, backdropWidth - 20);
            int target = (int)(backdropWidth * 0.86f);
            return Math.Clamp(target, Math.Min(350, maxAllowed), Math.Min(520, maxAllowed));
        }

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
                int drawerW = ComputeDrawerWidth(pnlDrawerBackdrop.Width);
                pnlFriendsDrawer.Width = drawerW;
                pnlFriendsDrawer.Height = pnlDrawerBackdrop.Height;
                if (pnlDrawerBackdrop.Visible)
                {
                    drawerTargetX = pnlDrawerBackdrop.Width - drawerW;
                    pnlFriendsDrawer.Left = drawerTargetX;
                    RenderFriendsDrawerContent(currentFriendsSearchKeyword);
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
            currentFriendsSearchKeyword = "";
            if (currentUserId > 0)
            {
                _ = SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
            }

            pnlDrawerBackdrop.Bounds = pnlMainContainer.Bounds;
            int drawerW = ComputeDrawerWidth(pnlDrawerBackdrop.Width);
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
            currentFriendsSearchKeyword = keyword ?? "";
            var oldCtrls = pnlFriendsDrawer.Controls.Cast<Control>().ToList();
            pnlFriendsDrawer.Controls.Clear();
            foreach (var oc in oldCtrls) { try { oc.Dispose(); } catch { } }
            int dw = pnlFriendsDrawer.Width;

            pnlFriendsDrawer.Controls.Add(MakeSoftIconBadge(UiIcon.Users, 16, 14, 32, 10, clrSoftGreenBg, clrPrimaryGreen));

            var btnClose = MakeBtn("", dw - 48, 13, 34, 32, 11, clrAppBg, clrTextMain, Color.Transparent, 0);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Image = CreateVectorIcon(UiIcon.Close, clrIndigo, 15);
            btnClose.ImageSize = new Size(15, 15);
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            var lblHead = MakeLabel("Bạn bè của bạn", 56, 17, 11F, FontStyle.Bold, clrTextMain, Math.Max(100, btnClose.Left - 64));
            lblHead.Font = FontBold(11F);
            lblHead.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlFriendsDrawer.Controls.Add(lblHead);

            int addBtnW = Math.Max(118, MeasurePillBtnWidth("+ Kết bạn", 8.8F, 14));
            int searchW = Math.Max(100, dw - 16 - addBtnW - 8 - 16);
            var txtSearch = MakeTextBox(keyword, "Tìm bạn hoặc username...", 16, 56, searchW, 42, clrCardBg);
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var btnAddNew = MakeBtn("+ Kết bạn", 16 + searchW + 8, 56, addBtnW, 42, 14, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 8.8F);
            btnAddNew.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            int listTopY = 106;
            if (!string.IsNullOrEmpty(friendDrawerFeedback))
            {
                Label lblFeedback = MakeLabel(
                    friendDrawerFeedback,
                    16, 102, 8.4F, FontStyle.Bold,
                    friendDrawerFeedbackSuccess ? clrPrimaryGreen : clrRedDanger,
                    dw - 32);
                lblFeedback.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                pnlFriendsDrawer.Controls.Add(lblFeedback);
                listTopY = 132;
            }

            var listScroll = new SmoothScrollPanel
            {
                Location = new Point(14, listTopY),
                Size = new Size(dw - 26, pnlFriendsDrawer.Height - listTopY - 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true
            };
            listScroll.MouseEnter += (s, e) => listScroll.Focus();
            AttachEdgeResizeRedirector(listScroll);

            int rowW = Math.Max(220, dw - 44);
            Action<string> refreshList = (kw) =>
            {
                currentFriendsSearchKeyword = kw ?? "";
                var prevRows = listScroll.Controls.Cast<Control>().ToList();
                listScroll.Controls.Clear();
                foreach (var pr in prevRows) { try { pr.Dispose(); } catch { } }
                string q = (kw ?? "").Trim().ToLower();
                int y = 4;

                var filteredFriends = myFriends
                    .Where(f => string.IsNullOrEmpty(q) || f.Name.ToLower().Contains(q) || f.Handle.ToLower().Contains(q))
                    .ToList();

                listScroll.Controls.Add(MakeLabel($"Đã kết nối ({filteredFriends.Count})", 4, y, 8.8F, FontStyle.Bold, clrPrimaryGreen, rowW));
                y += 28;

                if (filteredFriends.Count == 0)
                {
                    listScroll.Controls.Add(MakeLabel("Chưa có bạn bè phù hợp. Bạn có thể chọn nhanh bên dưới.", 4, y, 8.2F, FontStyle.Regular, clrTextMuted, rowW, lines: 2));
                    y += 44;
                }
                else
                {
                    foreach (var fUser in filteredFriends)
                    {
                        var card = BuildFriendItemRow(fUser, rowW, true);
                        card.Location = new Point(0, y);
                        listScroll.Controls.Add(card);
                        y += 76;
                    }
                }

                y += 10;
                var filteredSuggestions = friendSuggestions
                    .Where(s => string.IsNullOrEmpty(q) || s.Name.ToLower().Contains(q) || s.Handle.ToLower().Contains(q))
                    .ToList();

                listScroll.Controls.Add(MakeLabel($"Gợi ý kết bạn trên hệ thống ({filteredSuggestions.Count})", 4, y, 8.8F, FontStyle.Bold, clrIndigo, rowW));
                y += 28;

                if (filteredSuggestions.Count == 0)
                {
                    listScroll.Controls.Add(MakeLabel("Không còn người dùng mới nào để gợi ý.", 4, y, 8.2F, FontStyle.Regular, clrTextMuted, rowW));
                    y += 34;
                }
                else
                {
                    foreach (var sUser in filteredSuggestions)
                    {
                        var card = BuildFriendItemRow(sUser, rowW, false);
                        card.Location = new Point(0, y);
                        listScroll.Controls.Add(card);
                        y += 76;
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
                Size = new Size(rowWidth, 68),
                BorderRadius = 16,
                FillColor = clrInputBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            Guna2CircleButton avt = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(u.Name) ? "U" : u.Name.Substring(0, 1).ToUpper(),
                Size = new Size(40, 40),
                Location = new Point(12, 14),
                FillColor = isAlreadyFriend ? clrPrimaryGreen : Color.FromArgb(100, 116, 139),
                ForeColor = Color.White,
                Font = FontMedium(10F),
                UseTransparentBackground = true,
                BackColor = Color.Transparent,
                Enabled = false
            };

            int actionBtnW = Math.Max(118, Math.Max(
                MeasurePillBtnWidth("+ Kết bạn", 8.5F, 17),
                MeasurePillBtnWidth("Chắc xóa?", 8.5F, 17)));
            int textMaxW = Math.Max(72, rowWidth - 60 - actionBtnW - 22);

            string stateLabel = u.IsOnline ? "● Đang hoạt động" : "● Ngoại tuyến";
            Color stateColor = u.IsOnline ? Color.FromArgb(16, 185, 129) : clrTextMuted;

            var lblName = MakeLabel(u.Name, 60, 10, 9.6F, FontStyle.Bold, clrTextMain, textMaxW);
            lblName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var lblSub = MakeLabel($"{u.Handle}  •  {stateLabel}", 60, 38, 8.4F, FontStyle.Regular, stateColor, textMaxW);
            lblSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            row.Controls.Add(avt);
            row.Controls.Add(lblName);
            row.Controls.Add(lblSub);

            if (isAlreadyFriend)
            {
                bool confirmRemove = false;
                var btnRemove = MakeBtn("Xóa bạn", rowWidth - actionBtnW - 10, 16, actionBtnW, 36, 17, clrAppBg, clrIndigo, clrBorderLight, 1, 8.5F);
                btnRemove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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
                var btnQuickAdd = MakeBtn("+ Kết bạn", rowWidth - actionBtnW - 10, 16, actionBtnW, 36, 17, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 8.5F);
                btnQuickAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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