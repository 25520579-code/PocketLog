using System.Drawing.Imaging;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        // ====================================================================
        // MÀN HÌNH 3: QUẢN LÝ CHI TIÊU (PHÂN RÕ VAI TRÒ CHỦ NỢ / NGƯỜI NỢ)
        // ====================================================================
        private Control BuildExpenseScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalIOwe = unpaidList.Where(d => d.DebtorId == currentUserId).Sum(d => d.MyShare);
            int totalOwedToMe = unpaidList.Where(d => d.CreditorId == currentUserId).Sum(d => d.MyShare);

            TaggedDebtItem? currentDebt = taggedDebts.FirstOrDefault(d => d.SplitId == selectedSplitId)
                                          ?? unpaidList.FirstOrDefault()
                                          ?? taggedDebts.FirstOrDefault();

            // Banner phân tách rõ tiền mình nợ vs tiền bạn nợ mình
            Guna2Panel pnlBanner = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 46),
                BorderRadius = 12,
                FillColor = Color.FromArgb(31, 41, 79)
            };
            pnlBanner.Controls.Add(MakeLabel($"Bạn nợ: {totalIOwe:N0}đ  •  Bạn bè nợ bạn: {totalOwedToMe:N0}đ", 14, 14, 8.8F, FontStyle.Bold, Color.White));
            root.Controls.Add(pnlBanner);
            y += 54;

            Guna2Panel cardActiveDebt = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 218),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            if (currentDebt != null)
            {
                bool isDebtor = (currentDebt.DebtorId == currentUserId);
                string roleDesc = isDebtor
                    ? $"Bạn nợ {currentDebt.CreditorName}"
                    : $"{currentDebt.DebtorName} nợ bạn";

                cardActiveDebt.Controls.Add(MakeLabel($"Khoản chia: {currentDebt.Title}", 14, 10, 9.2F, FontStyle.Bold, clrTextMain));
                cardActiveDebt.Controls.Add(MakeLabel($"{roleDesc} • Phần chia: {currentDebt.MyShare:N0}đ", 14, 30, 8F, FontStyle.Bold, isDebtor ? clrRedDanger : clrIndigo));

                var badgeState = MakeBtn(
                    currentDebt.IsConfirmedPaid ? "✓ Đã hoàn thành" : (currentDebt.IsSentProof ? "⏳ Chờ xác nhận" : "Chưa thanh toán"),
                    14, 52, 136, 24, 8,
                    currentDebt.IsConfirmedPaid ? clrSoftGreenBg : clrBadgeOrangeBg,
                    currentDebt.IsConfirmedPaid ? clrPrimaryGreen : clrBadgeOrangeTxt,
                    Color.Transparent, 0, 7.6F);
                cardActiveDebt.Controls.Add(badgeState);

                Label lblNote = MakeLabel(currentDebt.StatusNote, 156, 56, 7.6F, FontStyle.Italic, clrTextMuted);
                cardActiveDebt.Controls.Add(lblNote);

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(14, 84),
                    Size = new Size(136, 118),
                    BorderRadius = 12,
                    FillColor = Color.FromArgb(238, 242, 240),
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1
                };

                PictureBox picProof = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = currentDebt.ProofImage,
                    Visible = currentDebt.ProofImage != null
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

                var btnUploadProof = MakeBtn("📤 Gửi ảnh CK của bản thân", 162, 86, 198, 50, 12, clrSoftIndigo, clrIndigo, clrIndigo, 2, 8.2F);
                var btnFriendVerify = MakeBtn("✓ Xác nhận hoàn thành", 162, 146, 198, 54, 12, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 1, 8.2F);

                // Phân quyền nút bấm theo đúng vai trò Người nợ / Chủ nợ
                if (currentDebt.IsConfirmedPaid)
                {
                    btnUploadProof.Enabled = false;
                    btnUploadProof.Text = "✓ Đã đính kèm bill";
                    btnUploadProof.FillColor = clrAppBg;
                    btnUploadProof.ForeColor = clrTextMuted;
                    btnUploadProof.BorderColor = clrBorderLight;

                    btnFriendVerify.Enabled = false;
                    btnFriendVerify.Text = "✓ Đã tất toán xong";
                    btnFriendVerify.FillColor = clrSoftGreenBg;
                    btnFriendVerify.ForeColor = clrPrimaryGreen;
                }
                else if (isDebtor)
                {
                    // Mình là người nợ: Chỉ được gửi ảnh CK, không được tự xác nhận
                    btnUploadProof.Enabled = true;
                    btnUploadProof.Text = currentDebt.IsSentProof ? "🔄 Gửi lại ảnh CK khác" : "📤 Gửi ảnh CK của bản thân";

                    btnFriendVerify.Enabled = false;
                    btnFriendVerify.Text = $"⏳ Chờ {currentDebt.CreditorName} xác nhận";
                    btnFriendVerify.FillColor = Color.FromArgb(232, 236, 234);
                    btnFriendVerify.ForeColor = clrTextMuted;
                    btnFriendVerify.BorderColor = clrBorderLight;
                }
                else
                {
                    // Mình là chủ chi: Không gửi ảnh CK, chỉ chờ bạn gửi bill rồi bấm xác nhận
                    btnUploadProof.Enabled = false;
                    btnUploadProof.Text = currentDebt.IsSentProof ? $"🧾 {currentDebt.DebtorName} đã gửi bill" : $"⏳ Chờ {currentDebt.DebtorName} gửi bill";
                    btnUploadProof.FillColor = clrAppBg;
                    btnUploadProof.ForeColor = clrTextMuted;
                    btnUploadProof.BorderColor = clrBorderLight;

                    btnFriendVerify.Enabled = true;
                    btnFriendVerify.Text = $"✓ Xác nhận {currentDebt.DebtorName} đã trả";
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
                        lblNote.Text = "Người nợ chưa gửi ảnh CK!";
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
                cardActiveDebt.Controls.Add(MakeLabel("Chưa có khoản chia tiền nào được gắn tag.", 42, 92, 9.2F, FontStyle.Bold, clrTextMuted));
            }
            root.Controls.Add(cardActiveDebt);
            y += 226;

            string arrow = isUnpaidDropdownOpen ? "▲" : "▼";
            var btnToggleUnpaid = MakeBtn(
                $"{arrow} Các khoản chi gắn tag chưa tất toán ({unpaidList.Count})",
                20, y, 374, 40, 12, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8.2F);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                ActivateMenu(btnTabExpense, BuildExpenseScreen());
            };
            root.Controls.Add(btnToggleUnpaid);
            y += 46;

            if (isUnpaidDropdownOpen)
            {
                if (unpaidList.Count == 0)
                {
                    Guna2Panel emptyBox = new Guna2Panel
                    {
                        Location = new Point(20, y),
                        Size = new Size(374, 48),
                        BorderRadius = 12,
                        FillColor = clrSoftGreenBg,
                        BorderColor = clrSoftGreenBorder,
                        BorderThickness = 1
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
                            Size = new Size(374, 68),
                            BorderRadius = 12,
                            FillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                            BorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                            BorderThickness = isSelected ? 2 : 1
                        };

                        itemRow.Controls.Add(MakeLabel($"{debt.Category} • {debt.Title}", 12, 8, 8.5F, FontStyle.Bold, clrTextMain));
                        itemRow.Controls.Add(MakeLabel($"{roleTag} • {debt.MyShare:N0}đ", 12, 28, 7.8F, FontStyle.Bold, rowIsDebtor ? clrRedDanger : clrIndigo));
                        itemRow.Controls.Add(MakeLabel($"{debt.Time} • {(debt.IsSentProof ? "Đã đính kèm bill" : "Chưa gửi bill")}", 12, 46, 7.5F, FontStyle.Regular, clrTextMuted));

                        var btnSelectDebt = MakeBtn(
                            isSelected ? "Đang chọn" : (rowIsDebtor ? "Trả ngay ›" : "Kiểm tra ›"),
                            272, 16, 90, 34, 10,
                            isSelected ? clrPrimaryGreen : clrSoftIndigo,
                            isSelected ? Color.White : clrIndigo,
                            isSelected ? clrPrimaryGreen : clrIndigo, 1, 7.8F);

                        btnSelectDebt.Click += (s, e) =>
                        {
                            selectedSplitId = debt.SplitId;
                            ActivateMenu(btnTabExpense, BuildExpenseScreen());
                        };

                        itemRow.Controls.Add(btnSelectDebt);
                        root.Controls.Add(itemRow);
                        y += 74;
                    }
                }
            }

            y += 6;
            root.Controls.Add(MakeLabel("Chi tiêu của bạn theo danh mục Tag:", 20, y, 9.8F, FontStyle.Bold, clrTextMain));
            y += 26;

            foreach (var cat in standardCategories)
            {
                string catName = cat.Key;
                int spent = posts.Where(p => p.IsMine && p.HasTag && p.Category == catName).Sum(p => p.Amount);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;

                Guna2Panel catCard = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(374, 84),
                    BorderRadius = 14,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                catCard.Controls.Add(MakeLabel(catName, 12, 10, 9F, FontStyle.Bold, clrTextMain));
                catCard.Controls.Add(MakeLabel($"Đã tiêu: {spent:N0}đ / Mức: {limit:N0}đ", 12, 32, 7.8F, FontStyle.Bold, spent > limit ? clrRedDanger : clrPrimaryGreen));

                int pct = limit > 0 ? Math.Min(100, (int)((long)spent * 100 / limit)) : 100;
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(12, 56),
                    Size = new Size(210, 12),
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = Color.FromArgb(232, 236, 234),
                    ProgressColor = spent > limit ? clrRedDanger : clrPrimaryGreen,
                    ProgressColor2 = spent > limit ? Color.OrangeRed : clrSoftGreenBorder
                };

                Guna2TextBox txtLim = new Guna2TextBox
                {
                    Text = limit.ToString(),
                    Location = new Point(234, 12),
                    Size = new Size(126, 28),
                    BorderRadius = 8,
                    BorderColor = clrBorderLight,
                    Font = new Font("Segoe UI", 8.5F)
                };

                var btnSetLim = MakeBtn("Set hạn mức", 234, 46, 126, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.6F);
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
                y += 92;
            }

            int grandTotal = posts.Where(p => p.IsMine && p.HasTag).Sum(p => p.Amount);

            Guna2Panel cardTotal = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 132),
                BorderRadius = 18,
                FillColor = clrSoftGreenBg,
                BorderColor = clrPrimaryGreen,
                BorderThickness = 2
            };

            cardTotal.Controls.Add(MakeLabel("TỔNG CHI TIÊU CÁ NHÂN TẤT CẢ CÁC MỤC", 14, 10, 8.8F, FontStyle.Bold, clrPrimaryGreen));
            cardTotal.Controls.Add(MakeLabel($"{grandTotal:N0}đ  /  Tổng hạn mức: {totalBudgetLimit:N0}đ", 14, 32, 10F, FontStyle.Bold, grandTotal > totalBudgetLimit ? clrRedDanger : clrTextMain));

            int totalPct = totalBudgetLimit > 0 ? Math.Min(100, (int)((long)grandTotal * 100 / totalBudgetLimit)) : 100;
            Guna2ProgressBar prgTotal = new Guna2ProgressBar
            {
                Location = new Point(14, 58),
                Size = new Size(346, 14),
                BorderRadius = 7,
                Value = totalPct,
                FillColor = Color.White,
                ProgressColor = grandTotal > totalBudgetLimit ? clrRedDanger : clrPrimaryGreen,
                ProgressColor2 = grandTotal > totalBudgetLimit ? Color.OrangeRed : clrIndigo
            };

            Guna2TextBox txtTotalIn = new Guna2TextBox
            {
                Text = totalBudgetLimit.ToString(),
                Location = new Point(14, 82),
                Size = new Size(210, 34),
                BorderRadius = 10,
                FillColor = Color.White,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var btnSetTotal = MakeBtn("Set tổng hạn mức", 232, 82, 128, 34, 10, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 8F);
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
            y += 148;

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

        private string ImageToBase64(Image? img)
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

        private Image? Base64ToImage(string base64)
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
