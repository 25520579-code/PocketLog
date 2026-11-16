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
        private Control BuildExpenseScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalIOwe = unpaidList.Where(d => d.DebtorId == currentUserId).Sum(d => d.MyShare);
            int totalOwedToMe = unpaidList.Where(d => d.CreditorId == currentUserId).Sum(d => d.MyShare);

            TaggedDebtItem currentDebt = taggedDebts.FirstOrDefault(d => d.SplitId == selectedSplitId)
                                         ?? unpaidList.FirstOrDefault()
                                         ?? taggedDebts.FirstOrDefault();

            Guna2Panel pnlBanner = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(380, 48),
                BorderRadius = 14,
                FillColor = Color.FromArgb(30, 41, 59),
                UseTransparentBackground = true
            };
            pnlBanner.Controls.Add(MakeLabel($"Bạn nợ: {totalIOwe:N0}đ   •   Bạn bè nợ bạn: {totalOwedToMe:N0}đ", 16, 15, 8.8F, FontStyle.Bold, Color.White, 348));
            root.Controls.Add(pnlBanner);
            y += 58;

            Guna2Panel cardActiveDebt = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(380, 220),
                BorderRadius = 18,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            if (currentDebt != null)
            {
                bool isDebtor = (currentDebt.DebtorId == currentUserId);
                string roleDesc = isDebtor
                    ? $"Bạn nợ {currentDebt.CreditorName}"
                    : $"{currentDebt.DebtorName} nợ bạn";

                cardActiveDebt.Controls.Add(MakeLabel($"Khoản chia: {currentDebt.Title}", 14, 10, 9.2F, FontStyle.Bold, clrTextMain, 350));
                cardActiveDebt.Controls.Add(MakeLabel($"{roleDesc} • Phần chia: {currentDebt.MyShare:N0}đ", 14, 30, 8.2F, FontStyle.Bold, isDebtor ? clrRedDanger : clrPrimaryGreen, 350));

                var badgeState = MakeBtn(
                    currentDebt.IsConfirmedPaid ? "✓ Đã hoàn thành" : (currentDebt.IsSentProof ? "⏳ Chờ xác nhận" : "Chưa thanh toán"),
                    14, 52, 136, 26, 8,
                    currentDebt.IsConfirmedPaid ? clrSoftGreenBg : clrSoftIndigo,
                    currentDebt.IsConfirmedPaid ? clrPrimaryGreen : clrIndigo,
                    Color.Transparent, 0, 7.6F);
                cardActiveDebt.Controls.Add(badgeState);

                Label lblNote = MakeLabel(currentDebt.StatusNote, 156, 56, 7.6F, FontStyle.Italic, clrTextMuted, 214);
                cardActiveDebt.Controls.Add(lblNote);

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(14, 86),
                    Size = new Size(138, 118),
                    BorderRadius = 12,
                    FillColor = clrAppBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                Guna2PictureBox picProof = new Guna2PictureBox
                {
                    Location = new Point(2, 2),
                    Size = new Size(134, 114),
                    BorderRadius = 10,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = currentDebt.ProofImage,
                    Visible = currentDebt.ProofImage != null,
                    UseTransparentBackground = true
                };
                Label lblNoImg = new Label
                {
                    Text = "🧾\nẢnh bill CK\nđính kèm",
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                    ForeColor = clrTextMuted,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Visible = currentDebt.ProofImage == null
                };
                pnlBillImg.Controls.Add(lblNoImg);
                pnlBillImg.Controls.Add(picProof);
                cardActiveDebt.Controls.Add(pnlBillImg);

                var btnUploadProof = MakeBtn("📤 Gửi ảnh bill CK", 162, 86, 204, 52, 12, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.2F);
                var btnFriendVerify = MakeBtn("✓ Xác nhận hoàn thành", 162, 148, 204, 56, 12, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 8.2F);

                if (currentDebt.IsConfirmedPaid)
                {
                    btnUploadProof.Enabled = false;
                    btnUploadProof.Text = "✓ Đã đính kèm bill";
                    btnUploadProof.FillColor = clrAppBg;
                    btnUploadProof.ForeColor = clrTextMuted;

                    btnFriendVerify.Enabled = false;
                    btnFriendVerify.Text = "✓ Đã tất toán xong";
                    btnFriendVerify.FillColor = clrSoftGreenBg;
                    btnFriendVerify.ForeColor = clrPrimaryGreen;
                }
                else if (isDebtor)
                {
                    btnUploadProof.Enabled = true;
                    btnUploadProof.Text = currentDebt.IsSentProof ? "🔄 Đổi ảnh bill CK khác" : "📤 Gửi ảnh bill CK";

                    btnFriendVerify.Enabled = false;
                    btnFriendVerify.Text = $"⏳ Chờ {currentDebt.CreditorName} duyệt";
                    btnFriendVerify.FillColor = clrAppBg;
                    btnFriendVerify.ForeColor = clrTextMuted;
                }
                else
                {
                    btnUploadProof.Enabled = false;
                    btnUploadProof.Text = currentDebt.IsSentProof ? $"🧾 {currentDebt.DebtorName} đã gửi bill" : $"⏳ Chờ {currentDebt.DebtorName} gửi bill";
                    btnUploadProof.FillColor = clrAppBg;
                    btnUploadProof.ForeColor = clrTextMuted;

                    btnFriendVerify.Enabled = true;
                    btnFriendVerify.Text = "✓ Xác nhận đã trả";
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
            }
            else
            {
                cardActiveDebt.Controls.Add(MakeLabel("Chưa có khoản chia tiền nào được gắn tag.", 46, 94, 9.2F, FontStyle.Bold, clrTextMuted));
            }
            root.Controls.Add(cardActiveDebt);
            y += 230;

            string arrow = isUnpaidDropdownOpen ? "▲" : "▼";
            var btnToggleUnpaid = MakeBtn(
                $"{arrow} Các khoản chi gắn tag chưa tất toán ({unpaidList.Count})",
                20, y, 380, 40, 12, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.2F);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                ActivateMenu(btnTabExpense, BuildExpenseScreen());
            };
            root.Controls.Add(btnToggleUnpaid);
            y += 48;

            if (isUnpaidDropdownOpen)
            {
                if (unpaidList.Count == 0)
                {
                    Guna2Panel emptyBox = new Guna2Panel
                    {
                        Location = new Point(20, y),
                        Size = new Size(380, 48),
                        BorderRadius = 12,
                        FillColor = clrSoftGreenBg,
                        BorderColor = clrSoftGreenBorder,
                        BorderThickness = 1,
                        UseTransparentBackground = true
                    };
                    emptyBox.Controls.Add(MakeLabel("🎉 Tất cả các khoản chia tiền đều đã hoàn tất!", 14, 14, 8.5F, FontStyle.Bold, clrPrimaryGreen));
                    root.Controls.Add(emptyBox);
                    y += 56;
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
                            Location = new Point(20, y),
                            Size = new Size(380, 72),
                            BorderRadius = 14,
                            FillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                            BorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                            BorderThickness = isSelected ? 2 : 1,
                            UseTransparentBackground = true
                        };

                        itemRow.Controls.Add(MakeLabel($"{debt.Category} • {debt.Title}", 12, 8, 8.5F, FontStyle.Bold, clrTextMain, 250));
                        itemRow.Controls.Add(MakeLabel($"{roleTag} • {debt.MyShare:N0}đ", 12, 28, 8F, FontStyle.Bold, rowIsDebtor ? clrRedDanger : clrPrimaryGreen, 250));
                        itemRow.Controls.Add(MakeLabel($"{debt.Time} • {(debt.IsSentProof ? "Đã gửi bill" : "Chưa gửi bill")}", 12, 48, 7.5F, FontStyle.Regular, clrTextMuted, 250));

                        var btnSelectDebt = MakeBtn(
                            isSelected ? "Đang chọn" : (rowIsDebtor ? "Trả ngay ›" : "Kiểm tra ›"),
                            276, 18, 92, 34, 10,
                            isSelected ? clrPrimaryGreen : clrSoftIndigo,
                            isSelected ? Color.White : clrIndigo,
                            isSelected ? clrPrimaryGreen : clrBorderLight, 1, 7.8F);

                        btnSelectDebt.Click += (s, e) =>
                        {
                            selectedSplitId = debt.SplitId;
                            ActivateMenu(btnTabExpense, BuildExpenseScreen());
                        };

                        itemRow.Controls.Add(btnSelectDebt);
                        root.Controls.Add(itemRow);
                        y += 78;
                    }
                }
            }

            y += 6;
            root.Controls.Add(MakeLabel("Chi tiêu của bạn theo danh mục Tag:", 20, y, 9.5F, FontStyle.Bold, clrTextMain));
            y += 28;

            foreach (var cat in standardCategories)
            {
                string catName = cat.Key;
                int spent = posts.Where(p => p.IsMine && p.HasTag && p.Category == catName).Sum(p => p.Amount);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;

                Guna2Panel catCard = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(380, 92),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1,
                    UseTransparentBackground = true
                };

                catCard.Controls.Add(MakeLabel(catName, 14, 10, 8.8F, FontStyle.Bold, clrTextMain, 214));
                catCard.Controls.Add(MakeLabel($"Đã tiêu: {spent:N0}đ / {limit:N0}đ", 14, 34, 8F, FontStyle.Bold, spent > limit ? clrRedDanger : clrPrimaryGreen, 214));

                int pct = limit > 0 ? Math.Min(100, (int)((long)spent * 100 / limit)) : 100;
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(14, 62),
                    Size = new Size(214, 12),
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = clrSoftIndigo,
                    ProgressColor = spent > limit ? clrRedDanger : clrPrimaryGreen,
                    ProgressColor2 = spent > limit ? Color.OrangeRed : clrSoftGreenBorder,
                    UseTransparentBackground = true
                };

                Guna2TextBox txtLim = new Guna2TextBox
                {
                    Text = limit.ToString(),
                    Location = new Point(242, 12),
                    Size = new Size(124, 30),
                    BorderRadius = 8,
                    FillColor = clrAppBg,
                    BorderColor = clrBorderLight,
                    Font = new Font("Segoe UI", 8.5F)
                };

                var btnSetLim = MakeBtn("Đặt hạn mức", 242, 48, 124, 30, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F);
                btnSetLim.Click += (s, e) =>
                {
                    if (int.TryParse(txtLim.Text.Replace(".", "").Replace(",", ""), out int nLim) && nLim > 0)
                    {
                        categoryBudgets[catName] = nLim;
                        ActivateMenu(btnTabExpense, BuildExpenseScreen());
                    }
                };

                catCard.Controls.AddRange(new Control[] { prg, txtLim, btnSetLim });
                root.Controls.Add(catCard);
                y += 100;
            }

            int grandTotal = posts.Where(p => p.IsMine && p.HasTag).Sum(p => p.Amount);

            Guna2Panel cardTotal = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(380, 136),
                BorderRadius = 18,
                FillColor = clrSoftGreenBg,
                BorderColor = clrPrimaryGreen,
                BorderThickness = 2,
                UseTransparentBackground = true
            };

            cardTotal.Controls.Add(MakeLabel("TỔNG CHI TIÊU CÁ NHÂN TRONG THÁNG", 14, 10, 8.8F, FontStyle.Bold, clrPrimaryGreen, 350));
            cardTotal.Controls.Add(MakeLabel($"{grandTotal:N0}đ  /  Hạn mức: {totalBudgetLimit:N0}đ", 14, 32, 9.8F, FontStyle.Bold, grandTotal > totalBudgetLimit ? clrRedDanger : clrTextMain, 350));

            int totalPct = totalBudgetLimit > 0 ? Math.Min(100, (int)((long)grandTotal * 100 / totalBudgetLimit)) : 100;
            Guna2ProgressBar prgTotal = new Guna2ProgressBar
            {
                Location = new Point(14, 60),
                Size = new Size(352, 14),
                BorderRadius = 7,
                Value = totalPct,
                FillColor = Color.White,
                ProgressColor = grandTotal > totalBudgetLimit ? clrRedDanger : clrPrimaryGreen,
                ProgressColor2 = grandTotal > totalBudgetLimit ? Color.OrangeRed : clrIndigo,
                UseTransparentBackground = true
            };

            Guna2TextBox txtTotalIn = new Guna2TextBox
            {
                Text = totalBudgetLimit.ToString(),
                Location = new Point(14, 84),
                Size = new Size(214, 36),
                BorderRadius = 10,
                FillColor = Color.White,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var btnSetTotal = MakeBtn("Lưu tổng hạn mức", 236, 84, 130, 36, 10, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 8F);
            btnSetTotal.Click += async (s, e) =>
            {
                if (int.TryParse(txtTotalIn.Text.Replace(".", "").Replace(",", ""), out int nTot) && nTot > 0)
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

            cardTotal.Controls.AddRange(new Control[] { prgTotal, txtTotalIn, btnSetTotal });
            root.Controls.Add(cardTotal);
            y += 152;

            root.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            return root;
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
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }
    }
}