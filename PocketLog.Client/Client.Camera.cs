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

        private Action pasteImageToCameraAction;
        private Action refreshCameraFriendsAction;

        private bool StartWebcam(Guna2PictureBox targetPictureBox, Label lblStatusOverlay)
        {
            StopWebcam();
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    lblStatusOverlay.Text = "Không tìm thấy Webcam trên máy\nNhấn Ctrl + V để dán ảnh hoặc bấm Chọn ảnh bên dưới";
                    lblStatusOverlay.Visible = true;
                    return false;
                }

                if (selectedCameraIndex >= videoDevices.Count)
                {
                    selectedCameraIndex = 0;
                }

                webcamDevice = new VideoCaptureDevice(videoDevices[selectedCameraIndex].MonikerString);

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

                webcamDevice.NewFrame += (sender, eventArgs) =>
                {
                    try
                    {
                        if (!isWebcamStreaming || eventArgs.Frame == null) return;

                        using Bitmap raw = (Bitmap)eventArgs.Frame.Clone();
                        raw.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        Bitmap frameForStorage = (Bitmap)raw.Clone();

                        int boxW = Math.Max(320, targetPictureBox.Width);
                        int boxH = Math.Max(180, targetPictureBox.Height);
                        Bitmap frameForUi = FitImageCover(raw, boxW, boxH) ?? (Bitmap)raw.Clone();

                        lock (frameLock)
                        {
                            latestWebcamFrame?.Dispose();
                            latestWebcamFrame = frameForStorage;
                        }

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
                lblStatusOverlay.Text = "Đang kết nối ống kính Camera...";
                lblStatusOverlay.Visible = true;
                return true;
            }
            catch (Exception ex)
            {
                lblStatusOverlay.Text = "Không mở được Camera: " + ex.Message;
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
                latestWebcamFrame?.Dispose();
                latestWebcamFrame = null;
            }
        }

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
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 10;

            string selectedCategory = "🍜 Đồ ăn/Thức uống";
            HashSet<string> taggedFriendNames = new HashSet<string>();
            Image uploadedPhoto = null;

            var badgeLiveTop = MakeBtn("Ăn uống (Cá nhân)", 0, y, w, 36, 18, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.8F);
            badgeLiveTop.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AttachBtnIcon(badgeLiveTop, UiIcon.Food, clrPrimaryGreen, 16);
            col.Controls.Add(badgeLiveTop);
            y += 44;

            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 340),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = Color.FromArgb(15, 23, 42),
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                Padding = new Padding(3),
                UseTransparentBackground = true,
                Cursor = Cursors.Hand
            };

            Guna2PictureBox picPreview = new Guna2PictureBox
            {
                Dock = DockStyle.Fill,
                BorderRadius = 21,
                SizeMode = PictureBoxSizeMode.Normal,
                FillColor = Color.FromArgb(15, 23, 42),
                BackColor = Color.FromArgb(15, 23, 42),
                UseTransparentBackground = false,
                Visible = false,
                Cursor = Cursors.Hand
            };

            Label lblPrompt = new Label
            {
                Text = "Đang khởi động Camera...\nBạn có thể nhấn Ctrl + V để dán ảnh nhanh",
                Font = FontMedium(9.8F),
                ForeColor = Color.FromArgb(226, 232, 240),
                UseMnemonic = false,
                Size = new Size(w - 32, 76),
                Location = new Point(16, 132),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            int swCamW = Math.Max(122, MeasurePillBtnWidth("Đổi cam", 8.6F, 17, true, 15));
            var btnSwitchCam = MakeBtn("Đổi cam", w - swCamW - 14, 14, swCamW, 34, 17, Color.FromArgb(51, 65, 85), Color.White, Color.FromArgb(100, 116, 139), 1, 8.6F);
            btnSwitchCam.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnSwitchCam, UiIcon.SwitchCam, Color.White, 15);

            int initCamStateW = Math.Max(136, MeasurePillBtnWidth("Cam trước", 8.6F, 17));
            var badgeCamState = MakeBtn("Cam trước", 14, 14, initCamStateW, 34, 17, clrPrimaryGreen, Color.White, Color.Transparent, 0, 8.6F);

            Action<string, Color> setCamStateBadge = (txt, bg) =>
            {
                badgeCamState.Text = txt;
                badgeCamState.FillColor = bg;
                int maxBadgeW = Math.Max(126, pnlViewfinder.Width - swCamW - 42);
                badgeCamState.Width = Math.Clamp(MeasurePillBtnWidth(txt, 8.6F, 17), 126, maxBadgeW);
            };

            pnlViewfinder.Controls.AddRange(new Control[] { picPreview, lblPrompt, badgeCamState, btnSwitchCam });
            lblPrompt.BringToFront();
            badgeCamState.BringToFront();
            btnSwitchCam.BringToFront();
            col.Controls.Add(pnlViewfinder);
            y += 352;

            Action<Image, string> applyCustomImageToViewfinder = (img, statusText) =>
            {
                if (img == null) return;
                StopWebcam();
                uploadedPhoto?.Dispose();
                uploadedPhoto = new Bitmap(img);

                Image oldPreview = picPreview.Image;
                picPreview.Image = FitImageCover(uploadedPhoto, Math.Max(320, picPreview.Width), Math.Max(180, picPreview.Height));
                picPreview.Visible = true;
                lblPrompt.Visible = false;
                badgeCamState.BringToFront();
                btnSwitchCam.BringToFront();
                oldPreview?.Dispose();

                setCamStateBadge(statusText, clrPrimaryGreen);
            };

            pnlViewfinder.Resize += (s, e) =>
            {
                setCamStateBadge(badgeCamState.Text, badgeCamState.FillColor);
                if (!isWebcamStreaming && uploadedPhoto != null && picPreview.Width > 20)
                {
                    Image old = picPreview.Image;
                    picPreview.Image = FitImageCover(uploadedPhoto, picPreview.Width, picPreview.Height);
                    old?.Dispose();
                }
            };

            pasteImageToCameraAction = () =>
            {
                Image pastedImg = TryGetImageFromClipboard();
                if (pastedImg != null)
                {
                    applyCustomImageToViewfinder(pastedImg, "Đã dán ảnh");
                    pastedImg.Dispose();
                }
                else
                {
                    setCamStateBadge("Chưa có ảnh!", clrRedDanger);
                }
            };

            ContextMenuStrip ctxMenu = new ContextMenuStrip();
            var menuPaste = ctxMenu.Items.Add("Dán ảnh từ Clipboard (Ctrl + V)");
            menuPaste.Click += (s, e) => pasteImageToCameraAction();
            var menuRestartCam = ctxMenu.Items.Add("Bật lại Camera trực tiếp");
            menuRestartCam.Click += (s, e) =>
            {
                uploadedPhoto = null;
                picPreview.Visible = false;
                StartWebcam(picPreview, lblPrompt);
                setCamStateBadge(selectedCameraIndex == 0 ? "Cam trước" : (selectedCameraIndex == 1 ? "Cam sau" : $"Cam #{selectedCameraIndex + 1}"), clrPrimaryGreen);
            };
            pnlViewfinder.ContextMenuStrip = ctxMenu;
            picPreview.ContextMenuStrip = ctxMenu;

            bool started = StartWebcam(picPreview, lblPrompt);
            if (!started)
            {
                setCamStateBadge("Chưa có Cam", clrIndigo);
            }

            btnSwitchCam.Click += (s, e) =>
            {
                var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (devices.Count > 1)
                {
                    selectedCameraIndex = (selectedCameraIndex + 1) % devices.Count;
                }
                uploadedPhoto = null;
                picPreview.Visible = false;
                if (StartWebcam(picPreview, lblPrompt))
                {
                    setCamStateBadge(selectedCameraIndex == 0 ? "Cam trước" : (selectedCameraIndex == 1 ? "Cam sau" : $"Cam #{selectedCameraIndex + 1}"), clrPrimaryGreen);
                }
            };

            EventHandler clickViewfinderToSnap = (s, e) =>
            {
                if (isWebcamStreaming)
                {
                    Bitmap snap = CaptureSnapshotFromWebcam();
                    if (snap != null)
                    {
                        uploadedPhoto?.Dispose();
                        uploadedPhoto = snap;
                        Image old = picPreview.Image;
                        picPreview.Image = FitImageCover(uploadedPhoto, Math.Max(320, picPreview.Width), Math.Max(180, picPreview.Height));
                        old?.Dispose();
                        picPreview.Visible = true;
                        lblPrompt.Visible = false;
                        badgeCamState.BringToFront();
                        btnSwitchCam.BringToFront();
                        setCamStateBadge("Đã chụp", clrIndigo);
                    }
                    else
                    {
                        lblPrompt.Text = "Camera chưa trả hình hoặc đang bận.\nBạn có thể nhấn Ctrl + V để dán ảnh ngay!";
                        lblPrompt.Visible = true;
                    }
                }
                else
                {
                    uploadedPhoto = null;
                    picPreview.Visible = false;
                    if (StartWebcam(picPreview, lblPrompt))
                    {
                        setCamStateBadge(selectedCameraIndex == 0 ? "Cam trước" : (selectedCameraIndex == 1 ? "Cam sau" : $"Cam #{selectedCameraIndex + 1}"), clrPrimaryGreen);
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
                        applyCustomImageToViewfinder(fileImg, "Đã chọn ảnh • Chạm mở Cam");
                    }
                }
            };

            // Hàng tiêu đề Danh mục chi tiêu + nút Không gắn tag
            Panel catHeader = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            int noTagW = Math.Max(172, MeasurePillBtnWidth("Không gắn tag", 8.6F, 18, true, 14));
            var btnNoTag = MakeBtn("Không gắn tag", w - noTagW, 1, noTagW, 36, 18, clrCardBg, clrIndigo, clrBorderLight, 1, 8.6F);
            btnNoTag.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnNoTag, UiIcon.Close, clrIndigo, 14);

            var lblCatHeader = MakeLabel("Danh mục chi tiêu", 2, 6, 10.2F, FontStyle.Bold, clrTextMain, Math.Max(100, btnNoTag.Left - 12));
            lblCatHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            catHeader.Controls.Add(lblCatHeader);
            catHeader.Controls.Add(btnNoTag);
            col.Controls.Add(catHeader);
            y += 42;

            // Lưới 3 cột x 2 hàng cân đối cho 6 danh mục
            var catGrid = new BufferedTableLayout
            {
                Location = new Point(0, y),
                Size = new Size(w, 92),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 3,
                RowCount = 2
            };
            catGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            catGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            catGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            catGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            catGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            var catButtons = new List<(Guna2Button Btn, string Key, UiIcon Icon)>();

            // Thẻ nhập thông tin chi tiêu & gắn tag bạn bè
            Guna2Panel pnlNoteCard = new Guna2Panel
            {
                Location = new Point(0, y + 102),
                Size = new Size(w, 274),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            var rowItemAmt = new BufferedTableLayout
            {
                Location = new Point(14, 14),
                Size = new Size(w - 28, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            rowItemAmt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
            rowItemAmt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            rowItemAmt.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtNoteTop = MakeTextBox("", "Thêm tên món ăn", 0, 0, 180, 42, clrCardBg);
            txtNoteTop.Dock = DockStyle.Fill;
            txtNoteTop.Margin = new Padding(2, 0, 6, 0);

            var txtAmountNum = MakeTextBox("", "Thêm số tiền", 0, 0, 100, 42, clrCardBg, true);
            txtAmountNum.Dock = DockStyle.Fill;
            txtAmountNum.Margin = new Padding(2, 0, 2, 0);
            txtAmountNum.FillColor = clrSoftGreenBg;
            txtAmountNum.BorderColor = clrSoftGreenBorder;
            txtAmountNum.ForeColor = clrPrimaryGreen;

            rowItemAmt.Controls.Add(txtNoteTop, 0, 0);
            rowItemAmt.Controls.Add(txtAmountNum, 1, 0);

            Label lblTagFriends = MakeLabel("Gắn tag bạn bè chia hóa đơn (Bỏ trống = Cá nhân)", 16, 63, 8.8F, FontStyle.Bold, clrIndigo, w - 32);
            lblTagFriends.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Guna2Panel pnlFriendBox = new Guna2Panel
            {
                Location = new Point(14, 88),
                Size = new Size(w - 28, 64),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 14,
                FillColor = clrInputBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Padding = new Padding(6, 4, 6, 4),
                UseTransparentBackground = true
            };

            FlowLayoutPanel flowFriends = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                BackColor = clrInputBg,
                Padding = new Padding(4, 6, 4, 4)
            };
            pnlFriendBox.Controls.Add(flowFriends);

            var txtCaption = MakeTextBox("", "Thêm một tin nhắn", 14, 160, w - 28, 42, clrCardBg);
            txtCaption.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // Lưới 3 nút tiện ích đều nhau, không bao giờ xuống dòng mất chữ
            var rowTools = new BufferedTableLayout
            {
                Location = new Point(12, 210),
                Size = new Size(w - 24, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 3,
                RowCount = 1
            };
            rowTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            rowTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
            rowTools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
            rowTools.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var btnChooseFile = MakeBtn("Chọn ảnh", 0, 0, 100, 44, 14, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.5F);
            btnChooseFile.Dock = DockStyle.Fill;
            btnChooseFile.Margin = new Padding(3, 1, 3, 1);
            AttachBtnIcon(btnChooseFile, UiIcon.Photo, clrPrimaryGreen, 15);
            btnChooseFile.Click += pickImgFromFile;

            var btnPasteImg = MakeBtn("Dán ảnh", 0, 0, 100, 44, 14, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.5F);
            btnPasteImg.Dock = DockStyle.Fill;
            btnPasteImg.Margin = new Padding(3, 1, 3, 1);
            AttachBtnIcon(btnPasteImg, UiIcon.Clipboard, clrPrimaryGreen, 15);
            uiToolTip.SetToolTip(btnPasteImg, "Dán ảnh từ Clipboard (Phím tắt: Ctrl + V)");
            btnPasteImg.Click += (s, e) => pasteImageToCameraAction();

            var btnAddMoreFriend = MakeBtn("Thêm bạn", 0, 0, 100, 44, 14, clrAppBg, clrIndigo, clrBorderLight, 1, 8.5F);
            btnAddMoreFriend.Dock = DockStyle.Fill;
            btnAddMoreFriend.Margin = new Padding(3, 1, 3, 1);
            AttachBtnIcon(btnAddMoreFriend, UiIcon.UserPlus, clrIndigo, 15);
            btnAddMoreFriend.Click += (s, e) => OpenFriendsDrawer();

            rowTools.Controls.Add(btnChooseFile, 0, 0);
            rowTools.Controls.Add(btnPasteImg, 1, 0);
            rowTools.Controls.Add(btnAddMoreFriend, 2, 0);

            Action refreshCategoryStyles = () =>
            {
                foreach (var item in catButtons)
                {
                    bool active = (item.Key == selectedCategory);
                    item.Btn.FillColor = active ? clrPrimaryGreen : clrCardBg;
                    item.Btn.ForeColor = active ? Color.White : clrTextMain;
                    item.Btn.BorderColor = active ? clrPrimaryGreen : clrBorderLight;
                    AttachBtnIcon(item.Btn, item.Icon, active ? Color.White : clrIndigo, 15);
                }
            };

            Action updatePreview = () =>
            {
                if (string.IsNullOrEmpty(selectedCategory))
                {
                    badgeLiveTop.Text = "Ảnh khoảnh khắc Pocket (Không tính chi tiêu)";
                    badgeLiveTop.FillColor = clrAppBg;
                    badgeLiveTop.ForeColor = clrIndigo;
                    badgeLiveTop.BorderColor = clrBorderLight;
                    AttachBtnIcon(badgeLiveTop, UiIcon.Camera, clrIndigo, 15);
                    txtNoteTop.Enabled = false;
                    txtAmountNum.Enabled = false;
                    flowFriends.Enabled = false;
                }
                else
                {
                    string shortCat = GetCategoryShortClean(selectedCategory);
                    string splitInfo = taggedFriendNames.Count == 0
                        ? "Cá nhân"
                        : $"Chia {taggedFriendNames.Count + 1} người";

                    string noteText = txtNoteTop.Text.Trim();
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out int amt);

                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(noteText)) parts.Add(noteText);
                    if (amt > 0) parts.Add($"{amt:N0}đ");

                    if (parts.Count > 0)
                    {
                        string detail = string.Join(" • ", parts);
                        badgeLiveTop.Text = $"{shortCat} — {detail} ({splitInfo})";
                    }
                    else
                    {
                        badgeLiveTop.Text = $"{shortCat} ({splitInfo})";
                    }

                    badgeLiveTop.FillColor = clrSoftGreenBg;
                    badgeLiveTop.ForeColor = clrPrimaryGreen;
                    badgeLiveTop.BorderColor = clrSoftGreenBorder;
                    AttachBtnIcon(badgeLiveTop, GetCategoryIcon(selectedCategory), clrPrimaryGreen, 15);
                    txtNoteTop.Enabled = true;
                    txtAmountNum.Enabled = true;
                    flowFriends.Enabled = true;
                }
            };

            Action populateFriendChips = () =>
            {
                if (flowFriends.IsDisposed) return;
                taggedFriendNames.RemoveWhere(name => !myFriends.Any(f => f.Name == name));

                var oldChips = flowFriends.Controls.Cast<Control>().ToList();
                flowFriends.Controls.Clear();
                foreach (var oc in oldChips) { try { oc.Dispose(); } catch { } }

                if (myFriends.Count == 0)
                {
                    string hintTxt = "+ Chưa có bạn bè • Bấm để kết bạn";
                    int hintW = Math.Max(280, MeasurePillBtnWidth(hintTxt, 8.2F, 16));
                    var btnHintAddFriend = MakeBtn(hintTxt, 4, 4, hintW, 32, 16, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.2F);
                    btnHintAddFriend.Margin = new Padding(4, 4, 4, 4);
                    btnHintAddFriend.Click += (s, e) => OpenFriendsDrawer();
                    flowFriends.Controls.Add(btnHintAddFriend);
                }
                else
                {
                    foreach (var f in myFriends)
                    {
                        bool alreadyTagged = taggedFriendNames.Contains(f.Name);
                        int chipW = Math.Max(98, MeasurePillBtnWidth($"✓ {f.Name}", 8.2F, 16));
                        var fBtn = MakeBtn(
                            alreadyTagged ? $"✓ {f.Name}" : $"+ {f.Name}",
                            0, 0, chipW, 32, 16,
                            alreadyTagged ? clrPrimaryGreen : clrCardBg,
                            alreadyTagged ? Color.White : clrTextMain,
                            alreadyTagged ? clrPrimaryGreen : clrBorderLight,
                            1, 8.2F);
                        fBtn.Margin = new Padding(4, 4, 4, 4);
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
                updatePreview();
            };

            refreshCameraFriendsAction = populateFriendChips;
            flowFriends.Disposed += (s, e) =>
            {
                if (refreshCameraFriendsAction == populateFriendChips)
                    refreshCameraFriendsAction = null;
            };
            populateFriendChips();

            for (int i = 0; i < standardCategories.Length; i++)
            {
                var cat = standardCategories[i];
                var ico = GetCategoryIcon(cat.Key);
                var btnCat = MakeBtn(cat.ShortLabel, 0, 0, 110, 40, 16, clrCardBg, clrTextMain, clrBorderLight, 1, 8.6F);
                btnCat.Dock = DockStyle.Fill;
                int rightMargin = (i % 3 == 2) ? 0 : 6;
                btnCat.Margin = new Padding(0, 3, rightMargin, 3);

                btnCat.Click += (s, e) =>
                {
                    selectedCategory = (selectedCategory == cat.Key) ? null : cat.Key;
                    refreshCategoryStyles();
                    updatePreview();
                    // Đổi placeholder tên theo danh mục
                    if (selectedCategory == null)
                        txtNoteTop.PlaceholderText = "Thêm tên khoản chi";
                    else if (selectedCategory.Contains("Đồ ăn") || selectedCategory.Contains("Ăn uống"))
                        txtNoteTop.PlaceholderText = "Thêm tên món ăn";
                    else if (selectedCategory.Contains("Xăng"))
                        txtNoteTop.PlaceholderText = "Thêm tên trạm xăng";
                    else if (selectedCategory.Contains("Quần áo"))
                        txtNoteTop.PlaceholderText = "Thêm tên món đồ";
                    else if (selectedCategory.Contains("Tiền trọ"))
                        txtNoteTop.PlaceholderText = "Thêm địa chỉ / phòng trọ";
                    else if (selectedCategory.Contains("Mua sắm"))
                        txtNoteTop.PlaceholderText = "Thêm tên món đồ";
                    else if (selectedCategory.Contains("Học tập"))
                        txtNoteTop.PlaceholderText = "Thêm tên khoản học tập";
                    else
                        txtNoteTop.PlaceholderText = "Thêm tên khoản chi";
                };

                catButtons.Add((btnCat, cat.Key, ico));
                catGrid.Controls.Add(btnCat, i % 3, i / 3);
            }
            refreshCategoryStyles();

            btnNoTag.Click += (s, e) =>
            {
                selectedCategory = null;
                refreshCategoryStyles();
                updatePreview();
                txtNoteTop.PlaceholderText = "Thêm tên khoản chi";
            };

            txtNoteTop.TextChanged += (s, e) => updatePreview();
            txtAmountNum.TextChanged += (s, e) => updatePreview();
            updatePreview();

            pnlNoteCard.Controls.AddRange(new Control[]
            {
                rowItemAmt, lblTagFriends, pnlFriendBox, txtCaption, rowTools
            });

            col.Controls.Add(catGrid);
            col.Controls.Add(pnlNoteCard);
            y += 102 + 274 + 16;

            var btnPostPhoto = MakeBtn("Đăng lên Bảng tin Pocket", 0, y, w, 54, 27, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 11F);
            btnPostPhoto.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AttachBtnIcon(btnPostPhoto, UiIcon.Send, Color.White, 18);

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
                btnPostPhoto.Text = "Đang đồng bộ lên Server...";

                bool hasTag = !string.IsNullOrEmpty(selectedCategory);
                int parsedAmt = 0;
                if (hasTag)
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out parsedAmt);
                }

                string cleanItemName = string.IsNullOrWhiteSpace(txtNoteTop.Text) ? "" : txtNoteTop.Text.Trim();
                string formattedNoteTop = "";
                if (hasTag)
                {
                    var parts = new List<string>();
                    if (!string.IsNullOrEmpty(cleanItemName)) parts.Add(cleanItemName);
                    if (parsedAmt > 0) parts.Add($"{parsedAmt:N0}đ");
                    formattedNoteTop = parts.Count > 0 ? string.Join(" • ", parts) : GetCategoryShortClean(selectedCategory);
                }

                var postDto = new PostDto
                {
                    AuthorId = currentUserId,
                    AuthorName = currentFullName,
                    HasTag = hasTag,
                    TaggedFriends = hasTag ? taggedFriendNames.ToList() : new List<string>(),
                    Category = hasTag ? selectedCategory : "",
                    NoteTop = formattedNoteTop,
                    CaptionInside = string.IsNullOrWhiteSpace(txtCaption.Text) ? "Khoảnh khắc mới" : txtCaption.Text.Trim(),
                    Amount = hasTag ? parsedAmt : 0,
                    ImageBase64 = ImageToBase64(uploadedPhoto)
                };

                await SendPacketSafeAsync(TcpPacket.Create(PacketType.CREATE_POST_REQ, postDto));

                if (IsSplitModeCapable && IsTabActive("FEED"))
                {
                    isFeedShowingGallery = false;
                    RefreshPaneContent("CAMERA");
                    RefreshPaneContent("FEED");
                }
                else if (IsSplitModeCapable && activeTabs.Count >= 2)
                {
                    int camIdx = activeTabs.IndexOf("CAMERA");
                    if (camIdx >= 0) activeTabs[camIdx] = "FEED";
                    isFeedShowingGallery = false;
                    RenderActiveTabLayout();
                }
                else
                {
                    OpenInitialAuthenticatedTab("FEED");
                }
            };

            col.Controls.Add(btnPostPhoto);
            col.Height = btnPostPhoto.Bottom + 16;
            return page;
        }
    }
}