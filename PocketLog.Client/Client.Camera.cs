using System.Drawing.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        // ====================================================================
        // BỘ ĐIỀU KHIỂN CAMERA LAPTOP / PC (LIVE STREAM & SNAPSHOT)
        // ====================================================================
        private bool StartWebcam(PictureBox targetPictureBox, Label lblStatusOverlay, Control captionOverlay)
        {
            StopWebcam();
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    lblStatusOverlay.Text = "⚠ Không tìm thấy Camera trên máy!\n(Bạn vẫn có thể bấm nút Chọn ảnh ở dưới)";
                    lblStatusOverlay.Visible = true;
                    return false;
                }

                webcamDevice = new VideoCaptureDevice(videoDevices[0].MonikerString);
                webcamDevice.NewFrame += (sender, eventArgs) =>
                {
                    try
                    {
                        using Bitmap raw = (Bitmap)eventArgs.Frame.Clone();
                        // Lật ngang khung hình như gương giống camera Locket trên điện thoại
                        raw.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        Bitmap frameForStorage = (Bitmap)raw.Clone();
                        Bitmap frameForUi = (Bitmap)raw.Clone();

                        lock (frameLock)
                        {
                            latestWebcamFrame?.Dispose();
                            latestWebcamFrame = frameForStorage;
                        }

                        if (targetPictureBox.IsHandleCreated && !targetPictureBox.IsDisposed)
                        {
                            targetPictureBox.BeginInvoke(new Action(() =>
                            {
                                if (isWebcamStreaming && !targetPictureBox.IsDisposed)
                                {
                                    Image? oldImg = targetPictureBox.Image;
                                    targetPictureBox.Image = frameForUi;
                                    targetPictureBox.Visible = true;
                                    if (captionOverlay.Parent != targetPictureBox)
                                    {
                                        targetPictureBox.Controls.Add(captionOverlay);
                                    }
                                    oldImg?.Dispose();
                                }
                                else
                                {
                                    frameForUi.Dispose();
                                }
                            }));
                        }
                        else
                        {
                            frameForUi.Dispose();
                        }
                    }
                    catch { }
                };

                isWebcamStreaming = true;
                webcamDevice.Start();
                lblStatusOverlay.Visible = false;
                return true;
            }
            catch (Exception ex)
            {
                lblStatusOverlay.Text = "⚠ Không thể mở Camera: " + ex.Message;
                lblStatusOverlay.Visible = true;
                return false;
            }
        }

        private Bitmap? CaptureSnapshotFromWebcam()
        {
            Bitmap? snap = null;
            lock (frameLock)
            {
                if (latestWebcamFrame != null)
                {
                    snap = (Bitmap)latestWebcamFrame.Clone();
                }
            }
            StopWebcam();
            return snap;
        }

        private void StopWebcam()
        {
            isWebcamStreaming = false;
            try
            {
                if (webcamDevice != null)
                {
                    if (webcamDevice.IsRunning)
                    {
                        webcamDevice.SignalToStop();
                    }
                    webcamDevice = null;
                }
            }
            catch { }

            lock (frameLock)
            {
                latestWebcamFrame?.Dispose();
                latestWebcamFrame = null;
            }
        }

        // ====================================================================
        // MÀN HÌNH 2: CHỤP ẢNH BẰNG WEBCAM LAPTOP/PC (BẤM VÀO KHUNG LÀ CHỤP)
        // ====================================================================
        private Control BuildCameraScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();

            string? selectedCategory = "🍜 Đồ ăn/Thức uống";
            HashSet<string> taggedFriendNames = new HashSet<string>();
            Image? uploadedPhoto = null;

            var badgeLiveTop = MakeBtn("🍜 Đồ ăn/Thức uống — Bún bò Huế • 45.000đ (🔒 Cá nhân)", 20, 8, 374, 30, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F);

            // Khung ngắm Camera Locket
            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(20, 44),
                Size = new Size(374, 214),
                BorderRadius = 22,
                FillColor = Color.FromArgb(36, 48, 44),
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                Cursor = Cursors.Hand
            };

            PictureBox picPreview = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                Visible = false,
                Cursor = Cursors.Hand
            };

            Label lblPrompt = new Label
            {
                Text = "📷 Đang khởi động Camera Laptop...",
                Font = new Font("Segoe UI", 9.8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(215, 230, 224),
                Size = new Size(340, 65),
                Location = new Point(17, 62),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            // Nhãn hướng dẫn trạng thái Camera góc trên bên trái khung ngắm
            var badgeCamState = MakeBtn("🟢 LIVE CAMERA • Bấm vào khung để chụp", 14, 10, 260, 26, 12,
                Color.FromArgb(24, 92, 68), Color.White, clrSoftGreenBorder, 1, 7.6F);

            var pillLiveCaption = MakeBtn("Bữa trưa ngon bổ rẻ cùng đồng đội ✨", 32, 160, 310, 40, 18,
                Color.FromArgb(28, 34, 31), Color.White, Color.FromArgb(180, 210, 195), 1, 8.2F);

            pnlViewfinder.Controls.AddRange(new Control[] { lblPrompt, picPreview, badgeCamState, pillLiveCaption });
            badgeCamState.BringToFront();
            pillLiveCaption.BringToFront();

            // Bật Webcam tự động ngay khi mở màn hình Chụp ảnh
            bool started = StartWebcam(picPreview, lblPrompt, pillLiveCaption);
            if (started)
            {
                picPreview.Controls.Add(badgeCamState);
                picPreview.Controls.Add(pillLiveCaption);
            }
            else
            {
                badgeCamState.Text = "⚠ Không có Webcam • Bấm để thử lại";
                badgeCamState.FillColor = clrRedDanger;
            }

            // SỰ KIỆN CHÍNH: Bấm vào khung ảnh -> Nếu đang Live thì CHỤP ẢNH; Nếu đã chụp thì MỞ LẠI CAMERA để chụp lại
            EventHandler clickViewfinderToSnap = (s, e) =>
            {
                if (isWebcamStreaming)
                {
                    Bitmap? snap = CaptureSnapshotFromWebcam();
                    if (snap != null)
                    {
                        uploadedPhoto?.Dispose();
                        uploadedPhoto = snap;
                        picPreview.Image = (Bitmap)snap.Clone();
                        picPreview.Visible = true;
                        lblPrompt.Visible = false;

                        badgeCamState.Text = "📸 ĐÃ CHỤP ẢNH! (Bấm vào khung để chụp lại)";
                        badgeCamState.FillColor = clrIndigo;
                    }
                }
                else
                {
                    // Mở lại Webcam để chụp tấm mới
                    uploadedPhoto = null;
                    if (StartWebcam(picPreview, lblPrompt, pillLiveCaption))
                    {
                        picPreview.Controls.Add(badgeCamState);
                        picPreview.Controls.Add(pillLiveCaption);
                        badgeCamState.Text = "🟢 LIVE CAMERA • Bấm vào khung để chụp";
                        badgeCamState.FillColor = Color.FromArgb(24, 92, 68);
                    }
                }
            };

            pnlViewfinder.Click += clickViewfinderToSnap;
            picPreview.Click += clickViewfinderToSnap;
            lblPrompt.Click += clickViewfinderToSnap;
            badgeCamState.Click += clickViewfinderToSnap;

            // SỰ KIỆN NÚT CŨ BÊN DƯỚI: Chọn file ảnh có sẵn từ máy tính
            EventHandler pickImgFromFile = (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        StopWebcam();
                        uploadedPhoto?.Dispose();
                        uploadedPhoto = Image.FromFile(ofd.FileName);
                        picPreview.Image = (Image)uploadedPhoto.Clone();
                        picPreview.Visible = true;
                        lblPrompt.Visible = false;

                        picPreview.Controls.Add(badgeCamState);
                        picPreview.Controls.Add(pillLiveCaption);
                        badgeCamState.Text = "🖼️ Ảnh từ máy (Bấm vào khung để bật lại Camera)";
                        badgeCamState.FillColor = clrPrimaryGreen;
                    }
                }
            };

            root.Controls.Add(MakeLabel("Mục chi tiêu (Bấm lại để bỏ tag):", 20, 266, 8.5F, FontStyle.Bold, clrTextMain));
            var btnNoTag = MakeBtn("🚫 Bỏ gắn tag", 272, 262, 122, 26, 8, clrCardBg, clrRedDanger, clrRedDanger, 1, 7.6F);

            List<Guna2Button> catButtons = new List<Guna2Button>();

            Guna2Panel pnlNoteCard = new Guna2Panel
            {
                Location = new Point(20, 368),
                Size = new Size(374, 226),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            Guna2TextBox txtNoteTop = new Guna2TextBox
            {
                Text = "Bún bò Huế",
                PlaceholderText = "Ghi chú món...",
                Location = new Point(12, 10),
                Size = new Size(230, 34),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            Guna2TextBox txtAmountNum = new Guna2TextBox
            {
                Text = "45000",
                PlaceholderText = "Số tiền (đ)",
                Location = new Point(250, 10),
                Size = new Size(112, 34),
                BorderRadius = 10,
                BorderColor = clrSoftGreenBorder,
                ForeColor = clrPrimaryGreen,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold)
            };

            Label lblTagFriends = MakeLabel("👥 Tag bạn bè chia tiền cùng (Không chọn = Cá nhân):", 12, 48, 8F, FontStyle.Bold, clrIndigo);

            // Sửa FlowLayoutPanel cho phép tự động co giãn độ rộng nút tên bạn bè không bị khuất chữ
            FlowLayoutPanel flowFriends = new FlowLayoutPanel
            {
                Location = new Point(10, 68),
                Size = new Size(354, 52),
                AutoScroll = true,
                WrapContents = true,
                BackColor = Color.Transparent
            };

            Guna2TextBox txtCaption = new Guna2TextBox
            {
                Text = "Bữa trưa ngon bổ rẻ cùng đồng đội ✨",
                PlaceholderText = "Caption lọt trong ảnh (giữa phía dưới)...",
                Location = new Point(12, 126),
                Size = new Size(350, 36),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnChooseFile = MakeBtn("🖼️ Chọn ảnh từ máy tính", 12, 170, 220, 36, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);
            btnChooseFile.Click += pickImgFromFile;

            var btnAddMoreFriend = MakeBtn("+ Thêm bạn", 240, 170, 122, 36, 10, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8F);
            btnAddMoreFriend.Click += (s, e) => OpenFriendsDrawer();

            Action updatePreview = () =>
            {
                if (string.IsNullOrEmpty(selectedCategory))
                {
                    badgeLiveTop.Text = "📷 Ảnh khoảnh khắc (Không gắn tag chi tiêu)";
                    badgeLiveTop.FillColor = Color.FromArgb(235, 238, 236);
                    badgeLiveTop.ForeColor = clrTextMuted;
                    txtNoteTop.Enabled = false;
                    txtAmountNum.Enabled = false;
                    flowFriends.Enabled = false;
                }
                else
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out int amt);
                    if (amt <= 0) amt = 45000;
                    string splitInfo = taggedFriendNames.Count == 0
                        ? "🔒 Cá nhân"
                        : $"👥 Tag {string.Join(", ", taggedFriendNames)} ({amt / (taggedFriendNames.Count + 1):N0}đ/người)";

                    badgeLiveTop.Text = $"{selectedCategory} — {txtNoteTop.Text} • {amt:N0}đ ({splitInfo})";
                    badgeLiveTop.FillColor = clrSoftGreenBg;
                    badgeLiveTop.ForeColor = clrPrimaryGreen;
                    txtNoteTop.Enabled = true;
                    txtAmountNum.Enabled = true;
                    flowFriends.Enabled = true;
                }
            };

            if (myFriends.Count == 0)
            {
                var btnHintAddFriend = MakeBtn("👥 Chưa có bạn bè -> Bấm vào đây để kết bạn & tag", 0, 0, 336, 30, 12, clrSoftIndigo, clrIndigo, clrIndigo, 1, 7.8F);
                btnHintAddFriend.Click += (s, e) => OpenFriendsDrawer();
                flowFriends.Controls.Add(btnHintAddFriend);
            }
            else
            {
                foreach (var f in myFriends)
                {
                    int dynamicWidth = Math.Max(105, f.Name.Length * 9 + 34);
                    var fBtn = MakeBtn($"+ {f.Name}", 0, 0, dynamicWidth, 28, 14, clrCardBg, clrTextMain, clrBorderLight, 1, 7.8F);
                    fBtn.Margin = new Padding(2, 2, 6, 4);
                    fBtn.Click += (s, e) =>
                    {
                        if (taggedFriendNames.Contains(f.Name))
                        {
                            taggedFriendNames.Remove(f.Name);
                            fBtn.Text = $"+ {f.Name}";
                            fBtn.FillColor = clrCardBg;
                            fBtn.ForeColor = clrTextMain;
                            fBtn.BorderColor = clrBorderLight;
                            fBtn.BorderThickness = 1;
                        }
                        else
                        {
                            taggedFriendNames.Add(f.Name);
                            fBtn.Text = $"✓ {f.Name}";
                            fBtn.FillColor = clrSoftIndigo;
                            fBtn.ForeColor = clrIndigo;
                            fBtn.BorderColor = clrIndigo;
                            fBtn.BorderThickness = 2;
                        }
                        updatePreview();
                    };
                    flowFriends.Controls.Add(fBtn);
                }
            }

            // Hiển thị nhãn ngắn gọn trên 6 nút danh mục (🍜 Ăn uống, 📚 Học tập...) để không bị mất chữ
            for (int i = 0; i < standardCategories.Length; i++)
            {
                var cat = standardCategories[i];
                var btnCat = MakeBtn(cat.ShortLabel, 20 + (i % 3) * 127, 292 + (i / 3) * 36, 120, 32, 10,
                    i == 0 ? clrSoftIndigo : clrCardBg, i == 0 ? clrIndigo : clrTextMain, i == 0 ? clrIndigo : clrBorderLight, i == 0 ? 2 : 1, 8F);

                btnCat.Click += (s, e) =>
                {
                    if (selectedCategory == cat.Key)
                    {
                        selectedCategory = null;
                        btnCat.FillColor = clrCardBg;
                        btnCat.ForeColor = clrTextMain;
                        btnCat.BorderColor = clrBorderLight;
                        btnCat.BorderThickness = 1;
                    }
                    else
                    {
                        foreach (var b in catButtons)
                        {
                            b.FillColor = clrCardBg;
                            b.ForeColor = clrTextMain;
                            b.BorderColor = clrBorderLight;
                            b.BorderThickness = 1;
                        }
                        btnCat.FillColor = clrSoftIndigo;
                        btnCat.ForeColor = clrIndigo;
                        btnCat.BorderColor = clrIndigo;
                        btnCat.BorderThickness = 2;
                        selectedCategory = cat.Key;
                    }
                    updatePreview();
                };
                catButtons.Add(btnCat);
                root.Controls.Add(btnCat);
            }

            btnNoTag.Click += (s, e) =>
            {
                selectedCategory = null;
                foreach (var b in catButtons)
                {
                    b.FillColor = clrCardBg;
                    b.ForeColor = clrTextMain;
                    b.BorderColor = clrBorderLight;
                    b.BorderThickness = 1;
                }
                updatePreview();
            };

            txtNoteTop.TextChanged += (s, e) => updatePreview();
            txtAmountNum.TextChanged += (s, e) => updatePreview();
            txtCaption.TextChanged += (s, e) => pillLiveCaption.Text = txtCaption.Text;

            pnlNoteCard.Controls.AddRange(new Control[] { txtNoteTop, txtAmountNum, lblTagFriends, flowFriends, txtCaption, btnChooseFile, btnAddMoreFriend });

            var btnPostPhoto = MakeBtn("Đăng ảnh", 20, 602, 374, 48, 16, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 2, 10.5F);
            btnPostPhoto.Click += async (s, e) =>
            {
                // Nếu người dùng bấm "Đăng ảnh" ngay khi Camera vẫn đang Live thì tự động chụp luôn khung hình hiện tại
                if (isWebcamStreaming && uploadedPhoto == null)
                {
                    uploadedPhoto = CaptureSnapshotFromWebcam();
                }
                else
                {
                    StopWebcam();
                }

                btnPostPhoto.Enabled = false;
                btnPostPhoto.Text = "⏳ Đang gửi lên Server...";

                bool hasTag = !string.IsNullOrEmpty(selectedCategory);
                int parsedAmt = 0;
                if (hasTag)
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out parsedAmt);
                    if (parsedAmt <= 0) parsedAmt = 45000;
                }

                string cleanItemName = string.IsNullOrWhiteSpace(txtNoteTop.Text) ? "Khoản chi mới" : txtNoteTop.Text.Trim();
                string formattedNoteTop = hasTag ? $"{cleanItemName} • {parsedAmt:N0}đ" : "";

                var postDto = new PostDto
                {
                    AuthorId = currentUserId,
                    AuthorName = currentFullName,
                    HasTag = hasTag,
                    TaggedFriends = hasTag ? taggedFriendNames.ToList() : new List<string>(),
                    Category = hasTag ? selectedCategory! : "",
                    NoteTop = formattedNoteTop,
                    CaptionInside = string.IsNullOrWhiteSpace(txtCaption.Text) ? "Khoảnh khắc mới ✨" : txtCaption.Text.Trim(),
                    Amount = hasTag ? parsedAmt : 0,
                    ImageBase64 = ImageToBase64(uploadedPhoto)
                };

                await SendPacketSafeAsync(TcpPacket.Create(PacketType.CREATE_POST_REQ, postDto));

                activeTabName = "FEED";
                ActivateMenu(btnTabFeed, BuildFeedScreen());
            };

            root.Controls.AddRange(new Control[] { badgeLiveTop, pnlViewfinder, btnNoTag, pnlNoteCard, btnPostPhoto });
            return root;
        }
    }
}
