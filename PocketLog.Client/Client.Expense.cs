using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        private int CalculatePersonalSpentByCategory(string categoryName)
        {
            int myPostsShare = 0;
            foreach (var p in posts.Where(p => p.IsMine && p.HasTag && p.Category == categoryName))
            {
                int friendsShare = taggedDebts
                    .Where(d => d.PostId == p.PostId && d.CreditorId == currentUserId)
                    .Sum(d => d.MyShare);
                myPostsShare += Math.Max(0, p.Amount - friendsShare);
            }

            int taggedAsDebtorShare = taggedDebts
                .Where(d => d.DebtorId == currentUserId && d.Category == categoryName)
                .Sum(d => d.MyShare);

            return myPostsShare + taggedAsDebtorShare;
        }

        private int CalculateTotalPersonalSpent()
        {
            int myPostsShare = 0;
            foreach (var p in posts.Where(p => p.IsMine && p.HasTag))
            {
                int friendsShare = taggedDebts
                    .Where(d => d.PostId == p.PostId && d.CreditorId == currentUserId)
                    .Sum(d => d.MyShare);
                myPostsShare += Math.Max(0, p.Amount - friendsShare);
            }

            int taggedAsDebtorShare = taggedDebts
                .Where(d => d.DebtorId == currentUserId)
                .Sum(d => d.MyShare);

            return myPostsShare + taggedAsDebtorShare;
        }

        private Control BuildExpenseScreen()
        {
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 12;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalIOwe = unpaidList.Where(d => d.DebtorId == currentUserId).Sum(d => d.MyShare);
            int totalOwedToMe = unpaidList.Where(d => d.CreditorId == currentUserId).Sum(d => d.MyShare);

            TaggedDebtItem currentDebt = taggedDebts.FirstOrDefault(d => d.SplitId == selectedSplitId)
                                         ?? unpaidList.FirstOrDefault()
                                         ?? taggedDebts.FirstOrDefault();

            // Thẻ tổng quan công nợ chia 2 cột hiện đại
            Guna2Panel pnlBanner = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = Color.FromArgb(30, 41, 59),
                UseTransparentBackground = true
            };

            var bannerGrid = new BufferedTableLayout
            {
                Location = new Point(14, 10),
                Size = new Size(w - 28, 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 3,
                RowCount = 1
            };
            bannerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49.5F));
            bannerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            bannerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.5F));
            bannerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel colLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            colLeft.Controls.Add(MakeLabel("Bạn đang nợ", 6, 4, 8.8F, FontStyle.Regular, Color.FromArgb(148, 163, 184)));
            var lblOweVal = MakeLabel($"{totalIOwe:N0}đ", 6, 26, 11.5F, FontStyle.Bold, totalIOwe > 0 ? Color.FromArgb(253, 164, 175) : Color.White);
            lblOweVal.Font = FontBold(11.5F);
            colLeft.Controls.Add(lblOweVal);

            Panel dividerMid = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8), BackColor = Color.FromArgb(51, 65, 85) };

            Panel colRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            colRight.Controls.Add(MakeLabel("Bạn bè nợ bạn", 12, 4, 8.8F, FontStyle.Regular, Color.FromArgb(148, 163, 184)));
            var lblOwedVal = MakeLabel($"{totalOwedToMe:N0}đ", 12, 26, 11.5F, FontStyle.Bold, totalOwedToMe > 0 ? Color.FromArgb(125, 211, 252) : Color.White);
            lblOwedVal.Font = FontBold(11.5F);
            colRight.Controls.Add(lblOwedVal);

            bannerGrid.Controls.Add(colLeft, 0, 0);
            bannerGrid.Controls.Add(dividerMid, 1, 0);
            bannerGrid.Controls.Add(colRight, 2, 0);
            pnlBanner.Controls.Add(bannerGrid);
            col.Controls.Add(pnlBanner);
            y += 94;

            if (currentDebt != null)
            {
                Guna2Panel cardActiveDebt = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 256),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 22,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                bool isDebtor = (currentDebt.DebtorId == currentUserId);
                string roleDesc = isDebtor
                    ? $"Bạn nợ {currentDebt.CreditorName}"
                    : $"{currentDebt.DebtorName} nợ bạn";

                var lblDebtTitle = MakeLabel($"Khoản chia: {currentDebt.Title}", 16, 12, 10.5F, FontStyle.Bold, clrTextMain, w - 32);
                lblDebtTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                var lblDebtRole = MakeLabel($"{roleDesc}  •  Phần chia: {currentDebt.MyShare:N0}đ", 16, 36, 9.2F, FontStyle.Bold, isDebtor ? clrRedDanger : clrPrimaryGreen, w - 32);
                lblDebtRole.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                var badgeState = MakeBtn(
                    currentDebt.IsConfirmedPaid ? "Đã hoàn thành" : (currentDebt.IsSentProof ? "Chờ xác nhận" : "Chưa thanh toán"),
                    16, 62, 142, 28, 14,
                    currentDebt.IsConfirmedPaid ? clrSoftGreenBg : clrAppBg,
                    currentDebt.IsConfirmedPaid ? clrPrimaryGreen : clrIndigo,
                    Color.Transparent, 0, 8.2F);

                Label lblNote = MakeLabel(currentDebt.StatusNote, 168, 64, 8.2F, FontStyle.Regular, clrTextMuted, w - 184);
                lblNote.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                cardActiveDebt.Controls.AddRange(new Control[] { lblDebtTitle, lblDebtRole, badgeState, lblNote });

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(16, 98),
                    Size = new Size(150, 142),
                    BorderRadius = 14,
                    FillColor = clrInputBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                Guna2PictureBox picProof = new Guna2PictureBox
                {
                    Dock = DockStyle.Fill,
                    BorderRadius = 13,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = currentDebt.ProofImage,
                    Visible = currentDebt.ProofImage != null,
                    UseTransparentBackground = true
                };

                Panel emptyProofWrap = new Panel
                {
                    Dock = DockStyle.Fill,
                    Visible = currentDebt.ProofImage == null,
                    BackColor = Color.Transparent
                };
                emptyProofWrap.Controls.Add(MakeSoftIconBadge(UiIcon.Receipt, 56, 34, 38, 12, clrSoftGreenBg, clrPrimaryGreen));
                emptyProofWrap.Controls.Add(new Label
                {
                    Text = "Ảnh bill chuyển khoản",
                    Font = FontRegular(8F),
                    ForeColor = clrTextMuted,
                    Location = new Point(6, 80),
                    Size = new Size(138, 34),
                    TextAlign = ContentAlignment.TopCenter,
                    BackColor = Color.Transparent
                });

                pnlBillImg.Controls.Add(emptyProofWrap);
                pnlBillImg.Controls.Add(picProof);
                cardActiveDebt.Controls.Add(pnlBillImg);

                int btnRightW = w - 178 - 16;
                var btnUploadProof = MakeBtn("Gửi ảnh bill CK", 178, 98, btnRightW, 64, 16, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 9.2F);
                btnUploadProof.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                AttachBtnIcon(btnUploadProof, UiIcon.Upload, clrPrimaryGreen, 17);

                var btnFriendVerify = MakeBtn("Xác nhận hoàn thành", 178, 174, btnRightW, 66, 16, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 9.5F);
                btnFriendVerify.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                AttachBtnIcon(btnFriendVerify, UiIcon.Check, Color.White, 17);

                if (currentDebt.IsConfirmedPaid)
                {
                    btnUploadProof.Enabled = false;
                    btnUploadProof.Text = "Đã đính kèm bill";
                    btnFriendVerify.Enabled = false;
                    btnFriendVerify.Text = "Đã tất toán xong";
                }
                else if (isDebtor)
                {
                    btnUploadProof.Enabled = true;
                    btnUploadProof.Text = currentDebt.IsSentProof ? "Đổi ảnh bill khác" : "Gửi ảnh bill CK";

                    btnFriendVerify.Enabled = false;
                    btnFriendVerify.Text = $"Chờ {currentDebt.CreditorName} duyệt";
                }
                else
                {
                    btnUploadProof.Enabled = false;
                    btnUploadProof.Text = currentDebt.IsSentProof ? $"{currentDebt.DebtorName} đã gửi bill" : $"Chờ {currentDebt.DebtorName} gửi bill";

                    btnFriendVerify.Enabled = true;
                    btnFriendVerify.Text = "Xác nhận đã trả";
                }

                btnUploadProof.Click += async (s, e) =>
                {
                    using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                    {
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            using var img = Image.FromFile(ofd.FileName);
                            string b64 = ImageToBase64(img);
                            await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPLOAD_PROOF_REQ, new UploadProofRequestDto
                            {
                                SplitId = currentDebt.SplitId,
                                UserId = currentUserId,
                                ProofImageBase64 = b64
                            }));
                        }
                    }
                };

                btnFriendVerify.Click += async (s, e) =>
                {
                    if (!currentDebt.IsSentProof)
                    {
                        lblNote.Text = "Người nợ chưa gửi ảnh bill!";
                        lblNote.ForeColor = clrRedDanger;
                        return;
                    }
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.VERIFY_BILL_REQ, new VerifyBillRequestDto
                    {
                        SplitId = currentDebt.SplitId,
                        VerifierName = currentFullName
                    }));
                };

                cardActiveDebt.Controls.Add(btnUploadProof);
                cardActiveDebt.Controls.Add(btnFriendVerify);
                col.Controls.Add(cardActiveDebt);
                y += 270;
            }
            else
            {
                // Thẻ trạng thái rỗng gọn gàng thay vì khung trắng khổng lồ 220px
                Guna2Panel compactEmpty = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 84),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 18,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };
                compactEmpty.Controls.Add(MakeSoftIconBadge(UiIcon.Receipt, 16, 20, 44, 14, clrSoftGreenBg, clrPrimaryGreen));
                var lEmpty1 = MakeLabel("Chưa có khoản chia tiền nào được gắn tag", 72, 18, 9.8F, FontStyle.Bold, clrTextMain, w - 88);
                lEmpty1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                var lEmpty2 = MakeLabel("Gắn tag bạn bè lúc đăng ảnh chi tiêu để tự động chia hóa đơn", 72, 44, 8.6F, FontStyle.Regular, clrTextMuted, w - 88);
                lEmpty2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                compactEmpty.Controls.Add(lEmpty1);
                compactEmpty.Controls.Add(lEmpty2);
                col.Controls.Add(compactEmpty);
                y += 96;
            }

            var btnToggleUnpaid = MakeBtn(
                $"Các khoản chi gắn tag chưa tất toán ({unpaidList.Count})",
                0, y, w, 44, 16, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 9.2F);
            btnToggleUnpaid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AttachBtnIcon(btnToggleUnpaid, isUnpaidDropdownOpen ? UiIcon.ChevronUp : UiIcon.ChevronDown, clrPrimaryGreen, 17);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                ActivateMenu(btnTabExpense, BuildExpenseScreen());
            };
            col.Controls.Add(btnToggleUnpaid);
            y += 52;

            if (isUnpaidDropdownOpen)
            {
                if (unpaidList.Count == 0)
                {
                    Guna2Panel emptyBox = new Guna2Panel
                    {
                        Location = new Point(0, y),
                        Size = new Size(w, 52),
                        Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                        BorderRadius = 14,
                        FillColor = clrCardBg,
                        BorderColor = clrSoftGreenBorder,
                        BorderThickness = 1,
                        UseTransparentBackground = true
                    };
                    emptyBox.Controls.Add(MakeSoftIconBadge(UiIcon.Check, 14, 11, 30, 10, clrSoftGreenBg, clrPrimaryGreen));
                    emptyBox.Controls.Add(MakeLabel("Tất cả các khoản chia tiền đều đã tất toán xong!", 54, 14, 9F, FontStyle.Bold, clrPrimaryGreen, w - 70));
                    col.Controls.Add(emptyBox);
                    y += 60;
                }
                else
                {
                    foreach (var debt in unpaidList)
                    {
                        bool isSelected = currentDebt != null && debt.SplitId == currentDebt.SplitId;
                        bool rowIsDebtor = (debt.DebtorId == currentUserId);
                        string roleTag = rowIsDebtor ? $"Bạn nợ {debt.CreditorName}" : $"{debt.DebtorName} nợ bạn";

                        Guna2Panel itemRow = new Guna2Panel
                        {
                            Location = new Point(0, y),
                            Size = new Size(w, 86),
                            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                            BorderRadius = 18,
                            FillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                            BorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                            BorderThickness = 1,
                            UseTransparentBackground = true
                        };

                        int textMaxW = w - 134;
                        var r1 = MakeLabel($"{GetCategoryShortClean(debt.Category)}  •  {debt.Title}", 16, 10, 9.2F, FontStyle.Bold, clrTextMain, textMaxW);
                        r1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        var r2 = MakeLabel($"{roleTag}  •  {debt.MyShare:N0}đ", 16, 33, 8.8F, FontStyle.Bold, rowIsDebtor ? clrRedDanger : clrPrimaryGreen, textMaxW);
                        r2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                        var r3 = MakeLabel($"{debt.Time}  •  {(debt.IsSentProof ? "Đã gửi bill" : "Chưa gửi bill")}", 16, 56, 8.2F, FontStyle.Regular, clrTextMuted, textMaxW);
                        r3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                        var btnSelectDebt = MakeBtn(
                            isSelected ? "Đang chọn" : (rowIsDebtor ? "Trả ngay" : "Kiểm tra"),
                            w - 114, 24, 100, 38, 19,
                            isSelected ? clrPrimaryGreen : clrAppBg,
                            isSelected ? Color.White : clrIndigo,
                            isSelected ? clrPrimaryGreen : clrBorderLight, 1, 8.6F);
                        btnSelectDebt.Anchor = AnchorStyles.Top | AnchorStyles.Right;

                        btnSelectDebt.Click += (s, e) =>
                        {
                            selectedSplitId = debt.SplitId;
                            ActivateMenu(btnTabExpense, BuildExpenseScreen());
                        };

                        itemRow.Controls.AddRange(new Control[] { r1, r2, r3, btnSelectDebt });
                        col.Controls.Add(itemRow);
                        y += 94;
                    }
                }
            }

            y += 8;
            var lblSecCat = MakeLabel("Chi tiêu thực tế theo danh mục", 2, y, 11F, FontStyle.Bold, clrTextMain);
            lblSecCat.Font = FontBold(11F);
            col.Controls.Add(lblSecCat);
            y += 34;

            foreach (var cat in standardCategories)
            {
                string catName = cat.Key;
                int spent = CalculatePersonalSpentByCategory(catName);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;
                bool isOver = spent > limit;

                Guna2Panel catCard = new Guna2Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(w, 104),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 18,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                var iconBadge = MakeSoftIconBadge(
                    GetCategoryIcon(catName), 16, 16, 42, 14,
                    isOver ? clrSoftRedBg : clrSoftGreenBg,
                    isOver ? clrRedDanger : clrPrimaryGreen);
                catCard.Controls.Add(iconBadge);

                int leftColW = w - 68 - 146;
                var lblCatName = MakeLabel(GetCategoryCleanName(catName), 68, 14, 10F, FontStyle.Bold, clrTextMain, leftColW);
                lblCatName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                var lblSpent = MakeLabel($"Đã tiêu: {spent:N0}đ / {limit:N0}đ", 68, 40, 8.8F, FontStyle.Bold, isOver ? clrRedDanger : clrPrimaryGreen, leftColW);
                lblSpent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

                int pct = limit > 0 ? Math.Min(100, (int)((long)spent * 100 / limit)) : 100;
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(16, 76),
                    Size = new Size(w - 176, 12),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = clrAppBg,
                    ProgressColor = isOver ? clrRedDanger : clrPrimaryGreen,
                    ProgressColor2 = isOver ? Color.OrangeRed : clrSoftGreenBorder,
                    UseTransparentBackground = true
                };

                var txtLim = MakeTextBox(limit.ToString("N0"), "Hạn mức", w - 144, 14, 130, 36, clrCardBg, true);
                txtLim.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                txtLim.TextAlign = HorizontalAlignment.Right;

                var btnSetLim = MakeBtn("Lưu hạn mức", w - 144, 56, 130, 36, 12, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.6F);
                btnSetLim.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                btnSetLim.Click += async (s, e) =>
                {
                    if (int.TryParse(txtLim.Text.Replace(".", "").Replace(",", "").Trim(), out int nLim) && nLim > 0)
                    {
                        categoryBudgets[catName] = nLim;
                        await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPDATE_CATEGORY_BUDGET_REQ, new UpdateCategoryBudgetRequestDto
                        {
                            UserId = currentUserId,
                            Category = catName,
                            NewLimit = nLim
                        }));
                        ActivateMenu(btnTabExpense, BuildExpenseScreen());
                    }
                };

                catCard.Controls.AddRange(new Control[] { lblCatName, lblSpent, prg, txtLim, btnSetLim });
                col.Controls.Add(catCard);
                y += 114;
            }

            int grandTotal = CalculateTotalPersonalSpent();

            Guna2Panel cardTotal = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 156),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrSoftGreenBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            cardTotal.Controls.Add(MakeLabel("TỔNG CHI TIÊU CÁ NHÂN TRONG THÁNG", 18, 14, 9.2F, FontStyle.Bold, clrPrimaryGreen, w - 36));
            var lblGrand = MakeLabel($"{grandTotal:N0}đ   /   Hạn mức: {totalBudgetLimit:N0}đ", 18, 38, 11.2F, FontStyle.Bold, grandTotal > totalBudgetLimit ? clrRedDanger : clrTextMain, w - 36);
            lblGrand.Font = FontBold(11.2F);
            lblGrand.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cardTotal.Controls.Add(lblGrand);

            int totalPct = totalBudgetLimit > 0 ? Math.Min(100, (int)((long)grandTotal * 100 / totalBudgetLimit)) : 100;
            Guna2ProgressBar prgTotal = new Guna2ProgressBar
            {
                Location = new Point(18, 70),
                Size = new Size(w - 36, 14),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 7,
                Value = totalPct,
                FillColor = Color.White,
                ProgressColor = grandTotal > totalBudgetLimit ? clrRedDanger : clrPrimaryGreen,
                ProgressColor2 = grandTotal > totalBudgetLimit ? Color.OrangeRed : clrPrimaryGreen,
                UseTransparentBackground = true
            };

            var totalActionGrid = new BufferedTableLayout
            {
                Location = new Point(16, 96),
                Size = new Size(w - 32, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            totalActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            totalActionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            totalActionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtTotalIn = MakeTextBox(totalBudgetLimit.ToString("N0"), "Tổng hạn mức", 0, 0, 180, 42, clrSoftGreenBg, true);
            txtTotalIn.Dock = DockStyle.Fill;
            txtTotalIn.FillColor = Color.White;
            txtTotalIn.BorderColor = clrSoftGreenBorder;
            txtTotalIn.Margin = new Padding(2, 0, 6, 0);

            var btnSetTotal = MakeBtn("Lưu tổng hạn mức", 0, 0, 120, 42, 14, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 9.2F);
            btnSetTotal.Dock = DockStyle.Fill;
            btnSetTotal.Margin = new Padding(2, 0, 2, 0);
            btnSetTotal.Click += async (s, e) =>
            {
                if (int.TryParse(txtTotalIn.Text.Replace(".", "").Replace(",", "").Trim(), out int nTot) && nTot > 0)
                {
                    totalBudgetLimit = nTot;
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPDATE_BUDGET_REQ, new UpdateBudgetRequestDto
                    {
                        UserId = currentUserId,
                        NewMonthlyBudget = nTot
                    }));
                    ActivateMenu(btnTabExpense, BuildExpenseScreen());
                }
            };

            totalActionGrid.Controls.Add(txtTotalIn, 0, 0);
            totalActionGrid.Controls.Add(btnSetTotal, 1, 0);

            cardTotal.Controls.Add(prgTotal);
            cardTotal.Controls.Add(totalActionGrid);
            col.Controls.Add(cardTotal);
            y += 170;

            var btnExportCsv = MakeBtn("Xuất báo cáo chi tiêu (.CSV / Excel)", 0, y, w, 48, 16, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 9.5F);
            btnExportCsv.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AttachBtnIcon(btnExportCsv, UiIcon.Receipt, clrPrimaryGreen, 17);

            var lblExportStatus = MakeLabel("", 4, y + 48, 8F, FontStyle.Regular, clrTextMuted, w - 8);
            lblExportStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnExportCsv.Click += (s, e) =>
            {
                using SaveFileDialog sfd = new SaveFileDialog
                {
                    Title = "Lưu báo cáo chi tiêu PocketLog",
                    Filter = "CSV (Mở bằng Excel)|*.csv",
                    FileName = $"PocketLog_BaoCao_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    string CsvEsc(string val)
                    {
                        string text = (val ?? "").Replace("\r", " ").Replace("\n", " ");
                        return $"\"{text.Replace("\"", "\"\"")}\"";
                    }

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("BÁO CÁO CHI TIÊU CÁ NHÂN & CÔNG NỢ - POCKETLOG");
                    sb.AppendLine($"Người dùng,{CsvEsc(currentFullName)}");
                    sb.AppendLine($"Ngày xuất,{DateTime.Now:dd/MM/yyyy HH:mm}");
                    sb.AppendLine($"Tổng chi tiêu cá nhân,{grandTotal}");
                    sb.AppendLine($"Hạn mức tháng,{totalBudgetLimit}");
                    sb.AppendLine($"Bạn đang nợ bạn bè,{totalIOwe}");
                    sb.AppendLine($"Bạn bè đang nợ bạn,{totalOwedToMe}");
                    sb.AppendLine();

                    sb.AppendLine("1. CHI TIÊU THEO DANH MỤC");
                    sb.AppendLine("Danh mục,Đã tiêu cá nhân (VNĐ),Hạn mức (VNĐ),Tình trạng");
                    foreach (var cat in standardCategories)
                    {
                        int cSpent = CalculatePersonalSpentByCategory(cat.Key);
                        int cLimit = categoryBudgets.ContainsKey(cat.Key) ? categoryBudgets[cat.Key] : 500000;
                        string cState = cSpent > cLimit ? "Vượt hạn mức" : "Trong hạn mức";
                        sb.AppendLine($"{CsvEsc(GetCategoryCleanName(cat.Key))},{cSpent},{cLimit},{CsvEsc(cState)}");
                    }
                    sb.AppendLine();

                    sb.AppendLine("2. LỊCH SỬ BÀI ĐĂNG CHI TIÊU & CHIA TIỀN");
                    sb.AppendLine("Thời gian,Khoản chi,Danh mục,Tổng hóa đơn (VNĐ),Vai trò / Ghi chú");
                    foreach (var p in posts.Where(p => p.IsMine && p.HasTag))
                    {
                        string tagNote = (p.TaggedFriends != null && p.TaggedFriends.Count > 0)
                            ? $"Bạn trả trước (Tag: {string.Join("; ", p.TaggedFriends)})"
                            : "Chi tiêu cá nhân";
                        sb.AppendLine($"{CsvEsc(p.Time)},{CsvEsc(p.NoteTop)},{CsvEsc(GetCategoryCleanName(p.Category))},{p.Amount},{CsvEsc(tagNote)}");
                    }
                    sb.AppendLine();

                    sb.AppendLine("3. CHI TIẾT CÔNG NỢ CHIA HÓA ĐƠN");
                    sb.AppendLine("Thời gian,Khoản chia,Chủ chi,Người nợ,Phần chia (VNĐ),Trạng thái");
                    foreach (var d in taggedDebts)
                    {
                        string st = d.IsConfirmedPaid ? "Đã tất toán" : (d.IsSentProof ? "Chờ xác nhận" : "Chưa thanh toán");
                        sb.AppendLine($"{CsvEsc(d.Time)},{CsvEsc(d.Title)},{CsvEsc(d.CreditorName)},{CsvEsc(d.DebtorName)},{d.MyShare},{CsvEsc(st)}");
                    }

                    File.WriteAllText(sfd.FileName, sb.ToString(), new System.Text.UTF8Encoding(true));
                    lblExportStatus.Text = $"✓ Đã xuất file: {Path.GetFileName(sfd.FileName)}";
                    lblExportStatus.ForeColor = clrPrimaryGreen;
                }
                catch (Exception ex)
                {
                    lblExportStatus.Text = "Lỗi xuất file: " + ex.Message;
                    lblExportStatus.ForeColor = clrRedDanger;
                }
            };

            col.Controls.Add(btnExportCsv);
            col.Controls.Add(lblExportStatus);
            y += 80;

            col.Height = y;
            return page;
        }

        private string GetCategoryEmoji(string category)
        {
            foreach (var c in standardCategories)
            {
                if (c.Key == category || c.ShortLabel == category) return c.Emoji;
            }
            return "📷";
        }

        private string ImageToBase64(Image img)
        {
            if (img == null) return "";
            try
            {
                int maxSide = 600;
                int w = img.Width, h = img.Height;
                if (w > maxSide || h > maxSide)
                {
                    if (w > h) { h = (int)((double)h * maxSide / w); w = maxSide; }
                    else { w = (int)((double)w * maxSide / h); h = maxSide; }
                }
                using Bitmap resized = new Bitmap(img, new Size(Math.Max(1, w), Math.Max(1, h)));
                using MemoryStream ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Jpeg);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return "";
            }
        }

        private Image Base64ToImage(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using MemoryStream ms = new MemoryStream(bytes);
                using Image temp = Image.FromStream(ms);
                return new Bitmap(temp);
            }
            catch
            {
                return null;
            }
        }
    }
}