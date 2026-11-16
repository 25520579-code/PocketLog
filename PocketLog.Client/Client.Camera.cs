using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        private VideoCaptureDevice webcamDevice;
        private bool isWebcamStreaming = false;
        private Bitmap latestWebcamFrame;
        private readonly object frameLock = new object();
        private int selectedCameraIndex = 0;

        // Hàm xử lý dán ảnh (Ctrl + V) được gọi từ mọi nơi trên màn hình Camera
        private Action pasteImageToCameraAction;

        private bool StartWebcam(Guna2PictureBox targetPictureBox, Label lblStatusOverlay)
        {
            StopWebcam();
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    lblStatusOverlay.Text = "📷 Không tìm thấy Webcam trên máy\n(Nhấn Ctrl+V để dán ảnh hoặc Chọn ảnh từ máy)";
                    lblStatusOverlay.Visible = true;
                    return false;
                }

                // Ưu tiên chọn Camera thật nếu máy có cài Camera ảo (OBS / Virtual Cam)
                if (selectedCameraIndex >= videoDevices.Count)
                {
                    selectedCameraIndex = 0;
                }

                webcamDevice = new VideoCaptureDevice(videoDevices[selectedCameraIndex].MonikerString);

                // Chọn độ phân giải vừa phải (ưu tiên 640x480 hoặc 1280x720) để khởi động nhanh và mượt
                if (webcamDevice.VideoCapabilities != null && webcamDevice.VideoCapabilities.Length > 0)
                {
                    var preferredRes = webcamDevice.VideoCapabilities
                        .OrderBy(c => Math.Abs(c.FrameSize.Width - 640))
                        .FirstOrDefault();
                    if (preferredRes != null)
                    {
                        webcamDevice.VideoResolution = preferredRes;
                    }
                }

                // Bật sẵn Visible cho PictureBox để Windows Forms cấp phát Handle hiển thị ngay
                targetPictureBox.Visible = true;

                webcamDevice.NewFrame += (sender, eventArgs) =>
                {
                    try
                    {
                        if (!isWebcamStreaming || eventArgs.Frame == null) return;

                        using Bitmap raw = (Bitmap)eventArgs.Frame.Clone();
                        raw.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        Bitmap frameForStorage = (Bitmap)raw.Clone();
                        Bitmap frameForUi = (Bitmap)raw.Clone();

                        lock (frameLock)
                        {
                            if (latestWebcamFrame != null) latestWebcamFrame.Dispose();
                            latestWebcamFrame = frameForStorage;
                        }

                        // Gọi BeginInvoke qua Form chính (this) để không bao giờ bị kẹt IsHandleCreated
                        if (this.IsHandleCreated && !this.IsDisposed)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                if (isWebcamStreaming && !targetPictureBox.IsDisposed)
                                {
                                    Image oldImg = targetPictureBox.Image;
                                    targetPictureBox.Image = frameForUi;
                                    targetPictureBox.Visible = true;
                                    lblStatusOverlay.Visible = false;
                                    if (oldImg != null) oldImg.Dispose();
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
                lblStatusOverlay.Text = "⏳ Đang kết nối ống kính Camera...";
                lblStatusOverlay.Visible = true;
                return true;
            }
            catch (Exception ex)
            {
                lblStatusOverlay.Text = "⚠ Không mở được Camera: " + ex.Message;
                lblStatusOverlay.Visible = true;
                return false;
            }
        }

        private Bitmap CaptureSnapshotFromWebcam()
        {
            Bitmap snap = null;
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
                if (latestWebcamFrame != null) latestWebcamFrame.Dispose();
                latestWebcamFrame = null;
            }
        }

        // Lấy ảnh từ Clipboard (hỗ trợ cả ảnh chụp màn hình Win+Shift+S, copy từ Zalo/Web và copy file trong Explorer)
        private Image TryGetImageFromClipboard()
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    Image clipImg = Clipboard.GetImage();
                    if (clipImg != null) return new Bitmap(clipImg);
                }

                if (Clipboard.ContainsFileDropList())
                {
                    StringCollection files = Clipboard.GetFileDropList();
                    foreach (string file in files)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if ((ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp") && File.Exists(file))
                        {
                            using var temp = Image.FromFile(file);
                            return new Bitmap(temp);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private Control BuildCameraScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();

            string selectedCategory = "🍜 Đồ ăn/Thức uống";
            HashSet<string> taggedFriendNames = new HashSet<string>();
            Image uploadedPhoto = null;

            var badgeLiveTop = MakeBtn("🍜 Ăn uống — Bún bò Huế • 45,000đ (Cá nhân)", 20, 8, 380, 30, 12, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);

            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(20, 44),
                Size = new Size(380, 210),
                BorderRadius = 26,
                FillColor = Color.FromArgb(30, 41, 59),
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                UseTransparentBackground = true,
                Cursor = Cursors.Hand
            };

            Guna2PictureBox picPreview = new Guna2PictureBox
            {
                Location = new Point(3, 3),
                Size = new Size(374, 204),
                BorderRadius = 24,
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                Visible = true,
                Cursor = Cursors.Hand
            };

            Label lblPrompt = new Label
            {
                Text = "📷 Đang khởi động Camera...\n(Bạn cũng có thể nhấn Ctrl + V để dán ảnh)",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Size = new Size(340, 60),
                Location = new Point(20, 74),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var badgeCamState = MakeBtn("● LIVE • Bấm khung để chụp / Ctrl+V dán ảnh", 14, 12, 278, 26, 13, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 1, 7.5F);
            var btnSwitchCam = MakeBtn("🔄 Đổi Cam", 300, 12, 66, 26, 13, clrIndigo, Color.White, clrBorderLight, 1, 7.2F);

            pnlViewfinder.Controls.AddRange(new Control[] { picPreview, lblPrompt, badgeCamState, btnSwitchCam });
            lblPrompt.BringToFront();
            badgeCamState.BringToFront();
            btnSwitchCam.BringToFront();

            // Hàm áp dụng ảnh đã chọn hoặc dán từ Clipboard lên khung ngắm
            Action<Image, string> applyCustomImageToViewfinder = (img, statusText) =>
            {
                if (img == null) return;
                StopWebcam();
                if (uploadedPhoto != null) uploadedPhoto.Dispose();
                uploadedPhoto = new Bitmap(img);

                Image oldPreview = picPreview.Image;
                picPreview.Image = new Bitmap(uploadedPhoto);
                picPreview.Visible = true;
                lblPrompt.Visible = false;
                if (oldPreview != null) oldPreview.Dispose();

                badgeCamState.Text = statusText;
                badgeCamState.FillColor = clrPrimaryGreen;
            };

            // Định nghĩa hành động Paste ảnh (Ctrl + V hoặc nút Dán ảnh)
            pasteImageToCameraAction = () =>
            {
                Image pastedImg = TryGetImageFromClipboard();
                if (pastedImg != null)
                {
                    applyCustomImageToViewfinder(pastedImg, "📋 Đã dán ảnh từ Clipboard! (Bấm khung để bật lại Cam)");
                    pastedImg.Dispose();
                }
                else
                {
                    badgeCamState.Text = "⚠ Trong Clipboard chưa có ảnh để dán!";
                    badgeCamState.FillColor = clrRedDanger;
                }
            };

            // Menu chuột phải trực tiếp trên khung ảnh để Dán ảnh nhanh
            ContextMenuStrip ctxMenu = new ContextMenuStrip();
            var menuPaste = ctxMenu.Items.Add("📋 Dán ảnh từ Clipboard (Ctrl + V)");
            menuPaste.Click += (s, e) => pasteImageToCameraAction();
            var menuRestartCam = ctxMenu.Items.Add("📷 Bật lại Camera trực tiếp");
            menuRestartCam.Click += (s, e) =>
            {
                uploadedPhoto = null;
                StartWebcam(picPreview, lblPrompt);
                badgeCamState.Text = "● LIVE • Bấm khung để chụp / Ctrl+V dán ảnh";
                badgeCamState.FillColor = clrPrimaryGreen;
            };
            pnlViewfinder.ContextMenuStrip = ctxMenu;
            picPreview.ContextMenuStrip = ctxMenu;

            bool started = StartWebcam(picPreview, lblPrompt);
            if (!started)
            {
                badgeCamState.Text = "📷 Chưa có Webcam • Nhấn Ctrl+V để dán ảnh";
                badgeCamState.FillColor = clrIndigo;
            }

            btnSwitchCam.Click += (s, e) =>
            {
                var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (devices.Count > 1)
                {
                    selectedCameraIndex = (selectedCameraIndex + 1) % devices.Count;
                }
                uploadedPhoto = null;
                if (StartWebcam(picPreview, lblPrompt))
                {
                    badgeCamState.Text = $"● Đang dùng Cam #{selectedCameraIndex + 1} • Bấm khung để chụp";
                    badgeCamState.FillColor = clrPrimaryGreen;
                }
            };

            EventHandler clickViewfinderToSnap = (s, e) =>
            {
                if (isWebcamStreaming)
                {
                    Bitmap snap = CaptureSnapshotFromWebcam();
                    if (snap != null)
                    {
                        if (uploadedPhoto != null) uploadedPhoto.Dispose();
                        uploadedPhoto = snap;
                        picPreview.Image = (Bitmap)snap.Clone();
                        picPreview.Visible = true;
                        lblPrompt.Visible = false;
                        badgeCamState.Text = "📸 Đã chụp! (Bấm khung để chụp lại)";
                        badgeCamState.FillColor = clrIndigo;
                    }
                    else
                    {
                        // Nếu Webcam chưa kịp trả frame mà người dùng bấm vào khung, kiểm tra xem Clipboard có ảnh không
                        lblPrompt.Text = "⚠ Camera chưa trả hình (hoặc đang bị app khác chiếm).\nBạn có thể nhấn Ctrl+V để dán ảnh ngay!";
                        lblPrompt.Visible = true;
                    }
                }
                else
                {
                    uploadedPhoto = null;
                    if (StartWebcam(picPreview, lblPrompt))
                    {
                        badgeCamState.Text = "● LIVE • Bấm khung để chụp / Ctrl+V dán ảnh";
                        badgeCamState.FillColor = clrPrimaryGreen;
                    }
                }
            };

            pnlViewfinder.Click += clickViewfinderToSnap;
            picPreview.Click += clickViewfinderToSnap;
            lblPrompt.Click += clickViewfinderToSnap;
            badgeCamState.Click += clickViewfinderToSnap;

            EventHandler pickImgFromFile = (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        using var fileImg = Image.FromFile(ofd.FileName);
                        applyCustomImageToViewfinder(fileImg, "🖼️ Đã chọn ảnh từ máy (Bấm khung để bật lại Cam)");
                    }
                }
            };

            root.Controls.Add(MakeLabel("Danh mục chi tiêu:", 20, 262, 8.8F, FontStyle.Bold, clrTextMain));
            // Mở rộng nút Không gắn tag lên 142px để hiển thị trọn vẹn chữ
            var btnNoTag = MakeBtn("✕ Không gắn tag", 258, 258, 142, 26, 10, clrCardBg, clrIndigo, clrBorderLight, 1, 7.6F);

            List<Guna2Button> catButtons = new List<Guna2Button>();

            Guna2Panel pnlNoteCard = new Guna2Panel
            {
                Location = new Point(20, 364),
                Size = new Size(380, 224),
                BorderRadius = 18,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            Guna2TextBox txtNoteTop = new Guna2TextBox
            {
                Text = "Bún bò Huế",
                PlaceholderText = "Tên món / Khoản chi...",
                Location = new Point(14, 12),
                Size = new Size(226, 34),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            Guna2TextBox txtAmountNum = new Guna2TextBox
            {
                Text = "45000",
                PlaceholderText = "Số tiền (đ)",
                Location = new Point(248, 12),
                Size = new Size(118, 34),
                BorderRadius = 10,
                FillColor = clrSoftGreenBg,
                BorderColor = clrSoftGreenBorder,
                ForeColor = clrPrimaryGreen,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold)
            };

            Label lblTagFriends = MakeLabel("👥 Gắn tag bạn bè chia hóa đơn (Bỏ trống = Cá nhân):", 14, 52, 8F, FontStyle.Bold, clrIndigo);

            FlowLayoutPanel flowFriends = new FlowLayoutPanel
            {
                Location = new Point(14, 72),
                Size = new Size(352, 48),
                AutoScroll = true,
                WrapContents = true,
                BackColor = clrAppBg
            };

            Guna2TextBox txtCaption = new Guna2TextBox
            {
                Text = "Khoảnh khắc cùng đồng đội ✨",
                PlaceholderText = "Nhập lời nhắn hiển thị trên ảnh...",
                Location = new Point(14, 128),
                Size = new Size(352, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            // Bộ 3 nút tiện ích dưới cùng: Chọn file ảnh | Dán ảnh (Ctrl+V) | + Thêm bạn
            var btnChooseFile = MakeBtn("🖼️ Chọn ảnh", 14, 172, 114, 38, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);
            btnChooseFile.Click += pickImgFromFile;

            var btnPasteImg = MakeBtn("📋 Dán ảnh (Ctrl+V)", 134, 172, 132, 38, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F);
            btnPasteImg.Click += (s, e) => pasteImageToCameraAction();

            var btnAddMoreFriend = MakeBtn("+ Thêm bạn", 272, 172, 94, 38, 10, clrSoftIndigo, clrIndigo, clrBorderLight, 1, 8F);
            btnAddMoreFriend.Click += (s, e) => OpenFriendsDrawer();

            Action updatePreview = () =>
            {
                if (string.IsNullOrEmpty(selectedCategory))
                {
                    badgeLiveTop.Text = "📷 Ảnh khoảnh khắc Locket (Không tính chi tiêu)";
                    badgeLiveTop.FillColor = clrSoftIndigo;
                    badgeLiveTop.ForeColor = clrIndigo;
                    badgeLiveTop.BorderColor = clrBorderLight;
                    txtNoteTop.Enabled = false;
                    txtAmountNum.Enabled = false;
                    flowFriends.Enabled = false;
                }
                else
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out int amt);
                    if (amt <= 0) amt = 45000;
                    string shortCat = standardCategories.FirstOrDefault(c => c.Key == selectedCategory).ShortLabel ?? selectedCategory;
                    string splitInfo = taggedFriendNames.Count == 0
                        ? "Cá nhân"
                        : $"Chia {taggedFriendNames.Count + 1} người";

                    badgeLiveTop.Text = $"{shortCat} — {txtNoteTop.Text} • {amt:N0}đ ({splitInfo})";
                    badgeLiveTop.FillColor = clrSoftGreenBg;
                    badgeLiveTop.ForeColor = clrPrimaryGreen;
                    badgeLiveTop.BorderColor = clrSoftGreenBorder;
                    txtNoteTop.Enabled = true;
                    txtAmountNum.Enabled = true;
                    flowFriends.Enabled = true;
                }
            };

            if (myFriends.Count == 0)
            {
                var btnHintAddFriend = MakeBtn("+ Chưa có bạn bè • Bấm để kết bạn", 4, 6, 330, 32, 10, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);
                btnHintAddFriend.Click += (s, e) => OpenFriendsDrawer();
                flowFriends.Controls.Add(btnHintAddFriend);
            }
            else
            {
                foreach (var f in myFriends)
                {
                    int dynamicWidth = Math.Max(98, f.Name.Length * 8 + 30);
                    var fBtn = MakeBtn($"+ {f.Name}", 0, 0, dynamicWidth, 30, 12, clrCardBg, clrTextMain, clrBorderLight, 1, 7.8F);
                    fBtn.Margin = new Padding(3, 4, 4, 4);
                    fBtn.Click += (s, e) =>
                    {
                        if (taggedFriendNames.Contains(f.Name))
                        {
                            taggedFriendNames.Remove(f.Name);
                            fBtn.Text = $"+ {f.Name}";
                            fBtn.FillColor = clrCardBg;
                            fBtn.ForeColor = clrTextMain;
                            fBtn.BorderColor = clrBorderLight;
                        }
                        else
                        {
                            taggedFriendNames.Add(f.Name);
                            fBtn.Text = $"✓ {f.Name}";
                            fBtn.FillColor = clrPrimaryGreen;
                            fBtn.ForeColor = Color.White;
                            fBtn.BorderColor = clrPrimaryGreen;
                        }
                        updatePreview();
                    };
                    flowFriends.Controls.Add(fBtn);
                }
            }

            for (int i = 0; i < standardCategories.Length; i++)
            {
                var cat = standardCategories[i];
                bool isFirst = (i == 0);
                var btnCat = MakeBtn(cat.ShortLabel, 20 + (i % 3) * 129, 290 + (i / 3) * 35, 122, 30, 10,
                    isFirst ? clrPrimaryGreen : clrCardBg,
                    isFirst ? Color.White : clrTextMain,
                    isFirst ? clrPrimaryGreen : clrBorderLight, 1, 8F);

                btnCat.Click += (s, e) =>
                {
                    if (selectedCategory == cat.Key)
                    {
                        selectedCategory = null;
                        btnCat.FillColor = clrCardBg;
                        btnCat.ForeColor = clrTextMain;
                        btnCat.BorderColor = clrBorderLight;
                    }
                    else
                    {
                        foreach (var b in catButtons)
                        {
                            b.FillColor = clrCardBg;
                            b.ForeColor = clrTextMain;
                            b.BorderColor = clrBorderLight;
                        }
                        btnCat.FillColor = clrPrimaryGreen;
                        btnCat.ForeColor = Color.White;
                        btnCat.BorderColor = clrPrimaryGreen;
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
                }
                updatePreview();
            };

            txtNoteTop.TextChanged += (s, e) => updatePreview();
            txtAmountNum.TextChanged += (s, e) => updatePreview();

            pnlNoteCard.Controls.AddRange(new Control[]
            {
                txtNoteTop, txtAmountNum, lblTagFriends, flowFriends,
                txtCaption, btnChooseFile, btnPasteImg, btnAddMoreFriend
            });

            var btnPostPhoto = MakeBtn("🚀 Đăng lên Bảng tin Locket", 20, 598, 380, 46, 16, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 10F);
            btnPostPhoto.Click += async (s, e) =>
            {
                if (isWebcamStreaming && uploadedPhoto == null)
                {
                    uploadedPhoto = CaptureSnapshotFromWebcam();
                }
                else
                {
                    StopWebcam();
                }

                btnPostPhoto.Enabled = false;
                btnPostPhoto.Text = "⏳ Đang đồng bộ lên Server...";

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
                    Category = hasTag ? selectedCategory : "",
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