using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            int target = (int)(backdropWidth * 0.90f);
            return Math.Clamp(target, Math.Min(360, maxAllowed), Math.Min(520, maxAllowed));
        }

        private void SetupFriendsDrawer()
        {
            pnlDrawerBackdrop = new Panel
            {
                Location = new Point(1, 56),
                Size = new Size(this.ClientSize.Width - 2, this.ClientSize.Height - 124),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = CurrentTheme.DrawerBackdrop,
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

            pnlDrawerBackdrop.BackColor = CurrentTheme.DrawerBackdrop;
            pnlDrawerBackdrop.Bounds = pnlMainContainer.Bounds;
            int drawerW = ComputeDrawerWidth(pnlDrawerBackdrop.Width);
            pnlFriendsDrawer.FillColor = clrCardBg;
            pnlFriendsDrawer.BorderColor = clrBorderLight;
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

            pnlFriendsDrawer.FillColor = clrCardBg;
            pnlFriendsDrawer.BorderColor = clrBorderLight;
            pnlFriendsDrawer.Controls.Add(MakeSoftIconBadge(UiIcon.Users, 16, 14, 34, 11, clrSoftGreenBg, clrPrimaryGreen));

            var btnClose = MakeBtn("", dw - 48, 13, 34, 34, 12, clrInputBg, clrTextMain, clrBorderLight, 1);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Image = CreateVectorIcon(UiIcon.Close, clrIndigo, 15);
            btnClose.ImageSize = new Size(15, 15);
            btnClose.HoverState.FillColor = clrSoftRedBg;
            btnClose.HoverState.BorderColor = clrRedDanger;
            btnClose.Disposed += (s, e) => { try { btnClose.Image?.Dispose(); } catch { } };
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            var lblHead = MakeLabel("Bạn bè của bạn", 58, 18, 11F, FontStyle.Bold, clrTextMain, Math.Max(100, btnClose.Left - 66));
            lblHead.Font = FontBold(11F);
            lblHead.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pnlFriendsDrawer.Controls.Add(lblHead);

            int addBtnW = Math.Max(118, MeasurePillBtnWidth("+ Kết bạn", 8.8F, 14));
            int searchW = Math.Max(100, dw - 16 - addBtnW - 8 - 16);
            var txtSearch = MakeTextBox(keyword, "Tìm bạn hoặc username...", 16, 58, searchW, 42, clrCardBg);
            txtSearch.BorderRadius = 14;
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var btnAddNew = MakeBtn("+ Kết bạn", 16 + searchW + 8, 58, addBtnW, 42, 14, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 8.8F);
            btnAddNew.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            int listTopY = 108;
            if (!string.IsNullOrEmpty(friendDrawerFeedback))
            {
                Label lblFeedback = MakeLabel(
                    friendDrawerFeedback,
                    16, 104, 8.4F, FontStyle.Bold,
                    friendDrawerFeedbackSuccess ? CurrentTheme.Success : clrRedDanger,
                    dw - 32);
                lblFeedback.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                pnlFriendsDrawer.Controls.Add(lblFeedback);
                listTopY = 134;
            }

            var listScroll = new SmoothScrollPanel
            {
                Location = new Point(12, listTopY),
                Size = new Size(dw - 20, pnlFriendsDrawer.Height - listTopY - 12),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                BackColor = clrCardBg
            };
            listScroll.MouseEnter += (s, e) => listScroll.Focus();
            AttachEdgeResizeRedirector(listScroll);

            int rowW = Math.Max(220, dw - 43);
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
                        y += 78;
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
                        y += 78;
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
            AppPalette theme = CurrentTheme;
            Guna2Panel row = new Guna2Panel
            {
                Size = new Size(rowWidth, 70),
                BorderRadius = 18,
                FillColor = clrInputBg,
                BorderColor = isAlreadyFriend ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            string initial = string.IsNullOrEmpty(u.Name) ? "U" : u.Name.Substring(0, 1).ToUpper();
            var avtHost = new Panel
            {
                Size = new Size(46, 46),
                Location = new Point(8, 12),
                BackColor = Color.Transparent
            };
            avtHost.Paint += (s, e) =>
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                var ringRect = new RectangleF(2f, 2f, 40f, 40f);
                if (isAlreadyFriend)
                {
                    if (u.IsOnline)
                    {
                        using var ringBrush = new LinearGradientBrush(ringRect, theme.AccentPrimary, theme.AccentSecondary, 45f);
                        using var ringPen = new Pen(ringBrush, 2.2f);
                        g.DrawEllipse(ringPen, ringRect);
                    }
                    else
                    {
                        using var ringPen = new Pen(theme.SoftAccentBorder, 1.8f);
                        g.DrawEllipse(ringPen, ringRect);
                    }
                }
                else
                {
                    using var ringPen = new Pen(theme.BorderLight, 1.4f);
                    g.DrawEllipse(ringPen, ringRect);
                }

                var innerRect = new RectangleF(5.5f, 5.5f, 33f, 33f);
                Color avatarFill = isAlreadyFriend ? theme.AccentPrimary : Color.FromArgb(100, 116, 139);
                using (var avtBrush = new SolidBrush(avatarFill))
                {
                    g.FillEllipse(avtBrush, innerRect);
                }

                using (Font initFont = FontBold(10F))
                {
                    TextRenderer.DrawText(
                        g,
                        initial,
                        initFont,
                        Rectangle.Round(innerRect),
                        Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                if (u.IsOnline)
                {
                    using var haloBrush = new SolidBrush(theme.InputBg);
                    using var dotBrush = new SolidBrush(theme.Success);
                    g.FillEllipse(haloBrush, 30f, 30f, 13f, 13f);
                    g.FillEllipse(dotBrush, 32f, 32f, 9f, 9f);
                }
            };

            int actionBtnW = Math.Max(98, Math.Max(
                MeasurePillBtnWidth("+ Kết bạn", 8.2F, 14),
                MeasurePillBtnWidth("Chắc xóa?", 8.2F, 14)));
            int textMaxW = Math.Max(70, rowWidth - 58 - actionBtnW - 12);

            string stateLabel = u.IsOnline ? "Đang hoạt động" : "Ngoại tuyến";
            Color stateColor = u.IsOnline ? theme.Success : clrTextMuted;

            var lblName = MakeLabel(u.Name, 58, 11, 9.6F, FontStyle.Bold, clrTextMain, textMaxW);
            lblName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var lblSub = MakeLabel($"{u.Handle} • {stateLabel}", 58, 39, 8.2F, FontStyle.Regular, stateColor, textMaxW);
            lblSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            row.Controls.Add(avtHost);
            row.Controls.Add(lblName);
            row.Controls.Add(lblSub);

            if (isAlreadyFriend)
            {
                bool confirmRemove = false;
                var btnRemove = MakeBtn("Xóa bạn", rowWidth - actionBtnW - 8, 17, actionBtnW, 36, 14, clrCardBg, clrIndigo, clrBorderLight, 1, 8.2F);
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
                var btnQuickAdd = MakeBtn("+ Kết bạn", rowWidth - actionBtnW - 8, 17, actionBtnW, 36, 14, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 8.2F);
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