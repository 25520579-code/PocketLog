using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Drawing2D;
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

        private sealed class CameraBottomDockPanel : Panel
        {
            private readonly Guna2PictureBox previewBox;
            private readonly Color fallbackBg;

            public CameraBottomDockPanel(Client owner, Guna2PictureBox previewBox, Color fallbackBg)
            {
                this.previewBox = previewBox;
                this.fallbackBg = fallbackBg;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                owner?.AttachEdgeResizeRedirector(this);
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                int w = ClientSize.Width;
                int h = ClientSize.Height;
                if (w < 4 || h < 4) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using GraphicsPath clipPath = CreateBottomRoundedPath(w, h, 21f);
                GraphicsState saved = g.Save();
                g.SetClip(clipPath);

                if (previewBox != null && !previewBox.IsDisposed && previewBox.Visible && previewBox.Image != null)
                {
                    int pw = Math.Max(1, previewBox.Width);
                    int ph = Math.Max(1, previewBox.Height);
                    g.DrawImage(previewBox.Image, new Rectangle(0, -(ph - h), pw, ph));
                }
                else
                {
                    using var baseBrush = new SolidBrush(fallbackBg);
                    g.FillRectangle(baseBrush, 0, 0, w, h);
                }

                using (var glassBrush = new LinearGradientBrush(
                    new RectangleF(0f, 0f, w, h),
                    Color.FromArgb(148, 11, 14, 20),
                    Color.FromArgb(228, 11, 14, 20),
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(glassBrush, 0, 0, w, h);
                }

                using (var topLinePen = new Pen(Color.FromArgb(54, 255, 255, 255), 1f))
                {
                    g.DrawLine(topLinePen, 0f, 0.5f, w, 0.5f);
                }

                g.Restore(saved);
            }

            private static GraphicsPath CreateBottomRoundedPath(float w, float h, float r)
            {
                float d = Math.Max(2f, Math.Min(r * 2f, Math.Min(w, h)));
                var path = new GraphicsPath();
                path.AddLine(0f, 0f, w, 0f);
                path.AddLine(w, 0f, w, h - d);
                path.AddArc(w - d, h - d, d, d, 0f, 90f);
                path.AddArc(0f, h - d, d, d, 90f, 90f);
                path.CloseFigure();
                return path;
            }
        }

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
                                    foreach (Control child in targetPictureBox.Controls)
                                    {
                                        if (child is CameraBottomDockPanel)
                                        {
                                            child.Invalidate();
                                            break;
                                        }
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
                    using Image clipImg = Clipboard.GetImage();
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
            AppPalette theme = CurrentTheme;
            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 8;

            string selectedCategory = "🍜 Đồ ăn/Thức uống";
            HashSet<string> taggedFriendNames = new HashSet<string>();
            Image uploadedPhoto = null;

            Color accentInk = isDarkMode ? clrTextMain : clrPrimaryGreen;
            Color accentIconInk = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;

            var badgeLiveTop = MakeBtn("Ăn uống (Cá nhân)", 0, y, w, 34, 17, clrSoftGreenBg, accentInk, clrSoftGreenBorder, 1, 8.8F);
            badgeLiveTop.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            badgeLiveTop.Cursor = Cursors.Default;
            badgeLiveTop.TabStop = false;
            badgeLiveTop.HoverState.FillColor = clrSoftGreenBg;
            badgeLiveTop.HoverState.BorderColor = clrSoftGreenBorder;
            AttachBtnIcon(badgeLiveTop, UiIcon.Food, accentIconInk, 16);
            col.Controls.Add(badgeLiveTop);
            y += 42;

            Color viewfinderBg = Color.FromArgb(15, 23, 42);
            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 390),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = viewfinderBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                Padding = new Padding(3),
                UseTransparentBackground = true,
                Cursor = Cursors.Hand
            };
            AttachEdgeResizeRedirector(pnlViewfinder);

            Guna2PictureBox picPreview = new Guna2PictureBox
            {
                Dock = DockStyle.Fill,
                BorderRadius = 21,
                SizeMode = PictureBoxSizeMode.Normal,
                FillColor = viewfinderBg,
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                Visible = true,
                Cursor = Cursors.Hand
            };
            AttachEdgeResizeRedirector(picPreview);

            const int dockHeight = 72;
            var pnlCameraDock = new CameraBottomDockPanel(this, picPreview, viewfinderBg)
            {
                Dock = DockStyle.Bottom,
                Height = dockHeight
            };

            var btnChooseFile = MakeBtn("Chọn ảnh", 12, 18, 118, 36, 18, Color.FromArgb(34, 44, 66), Color.White, Color.FromArgb(94, 114, 148), 1, 8.5F);
            btnChooseFile.HoverState.FillColor = theme.AccentPrimary;
            btnChooseFile.HoverState.BorderColor = theme.AccentPrimary;
            AttachBtnIcon(btnChooseFile, UiIcon.Photo, Color.White, 15);
            AttachEdgeResizeRedirector(btnChooseFile);

            var shutterBtn = new DoubleRingShutterButton(this);

            var btnPasteImg = MakeBtn("Dán ảnh", 240, 18, 114, 36, 18, Color.FromArgb(34, 44, 66), Color.White, Color.FromArgb(94, 114, 148), 1, 8.5F);
            btnPasteImg.HoverState.FillColor = theme.AccentPrimary;
            btnPasteImg.HoverState.BorderColor = theme.AccentPrimary;
            AttachBtnIcon(btnPasteImg, UiIcon.Clipboard, Color.White, 15);
            AttachEdgeResizeRedirector(btnPasteImg);
            uiToolTip.SetToolTip(btnPasteImg, "Dán ảnh từ Clipboard (Phím tắt: Ctrl + V)");

            int desiredChooseW = Math.Max(108, MeasurePillBtnWidth("Chọn ảnh", 8.5F, 18, true, 15));
            int desiredPasteW = Math.Max(104, MeasurePillBtnWidth("Dán ảnh", 8.5F, 18, true, 15));

            void LayoutCameraDock()
            {
                if (pnlCameraDock.IsDisposed) return;
                int dw = pnlCameraDock.ClientSize.Width;
                int dh = pnlCameraDock.ClientSize.Height;
                if (dw < 120 || dh < 40) return;

                int shutterX = (dw - shutterBtn.Width) / 2;
                int shutterY = (dh - shutterBtn.Height) / 2;
                shutterBtn.Location = new Point(shutterX, shutterY);

                int maxSideW = Math.Max(88, shutterX - 22);
                int chooseW = Math.Min(desiredChooseW, maxSideW);
                int pasteW = Math.Min(desiredPasteW, maxSideW);
                int pillY = (dh - 36) / 2;

                btnChooseFile.SetBounds(12, pillY, chooseW, 36);
                btnPasteImg.SetBounds(dw - 12 - pasteW, pillY, pasteW, 36);
            }

            pnlCameraDock.Controls.AddRange(new Control[] { btnChooseFile, shutterBtn, btnPasteImg });
            pnlCameraDock.Resize += (s, e) => LayoutCameraDock();
            LayoutCameraDock();

            Label lblPrompt = new Label
            {
                Text = "Đang khởi động Camera...\nBạn có thể nhấn Ctrl + V để dán ảnh nhanh",
                Font = FontMedium(9.8F),
                ForeColor = Color.FromArgb(226, 232, 240),
                UseMnemonic = false,
                Size = new Size(Math.Max(200, w - 42), 88),
                Location = new Point(15, 134),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            AttachEdgeResizeRedirector(lblPrompt);

            int swCamW = Math.Max(122, MeasurePillBtnWidth("Đổi cam", 8.6F, 17, true, 15));
            var btnSwitchCam = MakeBtn("Đổi cam", Math.Max(140, w - 6 - swCamW - 11), 11, swCamW, 34, 17, Color.FromArgb(30, 41, 59), Color.White, Color.FromArgb(100, 116, 139), 1, 8.6F);
            btnSwitchCam.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSwitchCam.HoverState.FillColor = Color.FromArgb(51, 65, 85);
            btnSwitchCam.HoverState.BorderColor = Color.FromArgb(148, 163, 184);
            AttachBtnIcon(btnSwitchCam, UiIcon.SwitchCam, Color.White, 15);
            AttachEdgeResizeRedirector(btnSwitchCam);

            int initCamStateW = Math.Max(136, MeasurePillBtnWidth("Cam trước", 8.6F, 17));
            var badgeCamState = MakeBtn("Cam trước", 11, 11, initCamStateW, 34, 17, theme.AccentPrimary, Color.White, Color.Transparent, 0, 8.6F);
            AttachEdgeResizeRedirector(badgeCamState);

            Action<string, Color> setCamStateBadge = (txt, bg) =>
            {
                badgeCamState.Text = txt;
                badgeCamState.FillColor = bg;
                badgeCamState.HoverState.FillColor = bg;
                int maxBadgeW = Math.Max(126, picPreview.Width - swCamW - 36);
                badgeCamState.Width = Math.Clamp(MeasurePillBtnWidth(txt, 8.6F, 17), 126, maxBadgeW);
            };

            string GetActiveCameraBadgeText() =>
                selectedCameraIndex == 0 ? "Cam trước" : (selectedCameraIndex == 1 ? "Cam sau" : $"Cam #{selectedCameraIndex + 1}");

            Color noCamBadgeColor = Color.FromArgb(51, 65, 85);

            void LayoutViewfinderOverlays()
            {
                if (pnlViewfinder.IsDisposed || picPreview.IsDisposed) return;
                int pw = picPreview.ClientSize.Width;
                int ph = picPreview.ClientSize.Height;
                if (pw <= 40 || ph <= 120) return;
                btnSwitchCam.Left = Math.Max(120, pw - swCamW - 11);
                int freeTop = 52;
                int freeBottom = Math.Max(freeTop + 60, ph - dockHeight - 8);
                lblPrompt.Size = new Size(Math.Max(180, pw - 30), Math.Min(92, freeBottom - freeTop));
                lblPrompt.Location = new Point(15, freeTop + (freeBottom - freeTop - lblPrompt.Height) / 2);
                setCamStateBadge(badgeCamState.Text, badgeCamState.FillColor);
            }

            picPreview.Controls.AddRange(new Control[] { lblPrompt, pnlCameraDock, badgeCamState, btnSwitchCam });
            lblPrompt.BringToFront();
            pnlCameraDock.BringToFront();
            badgeCamState.BringToFront();
            btnSwitchCam.BringToFront();
            pnlViewfinder.Controls.Add(picPreview);
            LayoutViewfinderOverlays();
            col.Controls.Add(pnlViewfinder);
            y += 400;

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
                shutterBtn.IsCapturedMode = true;

                pnlCameraDock.Invalidate();
                oldPreview?.Dispose();

                setCamStateBadge(statusText, theme.AccentPrimary);
            };

            Action restartLiveWebcam = () =>
            {
                uploadedPhoto?.Dispose();
                uploadedPhoto = null;
                Image oldPreview = picPreview.Image;
                picPreview.Image = null;
                picPreview.Visible = true;
                oldPreview?.Dispose();

                shutterBtn.IsCapturedMode = false;
                pnlCameraDock.Invalidate();

                if (StartWebcam(picPreview, lblPrompt))
                {
                    setCamStateBadge(GetActiveCameraBadgeText(), theme.AccentPrimary);
                }
                else
                {
                    setCamStateBadge("Chưa có Cam", noCamBadgeColor);
                }
            };

            pnlViewfinder.Resize += (s, e) =>
            {
                LayoutViewfinderOverlays();
                if (!isWebcamStreaming && uploadedPhoto != null && picPreview.Width > 20 && picPreview.Height > 20)
                {
                    Image old = picPreview.Image;
                    picPreview.Image = FitImageCover(uploadedPhoto, picPreview.Width, picPreview.Height);
                    old?.Dispose();
                    pnlCameraDock.Invalidate();
                }
            };

            Action localPasteAction = () =>
            {
                Image pastedImg = TryGetImageFromClipboard();
                if (pastedImg != null)
                {
                    applyCustomImageToViewfinder(pastedImg, "Đã dán ảnh");
                    pastedImg.Dispose();
                }
                else
                {
                    setCamStateBadge("Chưa có ảnh!", theme.Danger);
                }
            };
            pasteImageToCameraAction = localPasteAction;

            ContextMenuStrip ctxMenu = new ContextMenuStrip();
            var menuPaste = ctxMenu.Items.Add("Dán ảnh từ Clipboard (Ctrl + V)");
            menuPaste.Click += (s, e) => localPasteAction();
            var menuRestartCam = ctxMenu.Items.Add("Bật lại Camera trực tiếp");
            menuRestartCam.Click += (s, e) => restartLiveWebcam();
            pnlViewfinder.ContextMenuStrip = ctxMenu;
            picPreview.ContextMenuStrip = ctxMenu;

            bool started = StartWebcam(picPreview, lblPrompt);
            if (!started)
            {
                setCamStateBadge("Chưa có Cam", noCamBadgeColor);
            }
            else
            {
                setCamStateBadge(GetActiveCameraBadgeText(), theme.AccentPrimary);
            }

            btnSwitchCam.Click += (s, e) =>
            {
                var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (devices.Count > 1)
                {
                    selectedCameraIndex = (selectedCameraIndex + 1) % devices.Count;
                }
                restartLiveWebcam();
            };

            EventHandler clickCaptureOrRetake = (s, e) =>
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
                        shutterBtn.IsCapturedMode = true;
                        pnlCameraDock.BringToFront();
                        badgeCamState.BringToFront();
                        btnSwitchCam.BringToFront();
                        pnlCameraDock.Invalidate();
                        setCamStateBadge("Đã chụp", theme.AccentPrimary);
                    }
                    else
                    {
                        lblPrompt.Text = "Camera chưa trả hình hoặc đang bận.\nBạn có thể nhấn Ctrl + V để dán ảnh ngay!";
                        lblPrompt.Visible = true;
                        shutterBtn.IsCapturedMode = false;
                        setCamStateBadge("Chưa có Cam", noCamBadgeColor);
                    }
                }
                else
                {
                    restartLiveWebcam();
                }
            };

            shutterBtn.Click += clickCaptureOrRetake;
            pnlViewfinder.Click += clickCaptureOrRetake;
            picPreview.Click += clickCaptureOrRetake;
            lblPrompt.Click += clickCaptureOrRetake;
            badgeCamState.Click += clickCaptureOrRetake;

            EventHandler pickImgFromFile = (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        using var fileImg = Image.FromFile(ofd.FileName);
                        applyCustomImageToViewfinder(fileImg, "Đã chọn ảnh");
                    }
                }
            };
            btnChooseFile.Click += pickImgFromFile;
            btnPasteImg.Click += (s, e) => localPasteAction();

            // Hàng tiêu đề Danh mục chi tiêu + nút Không gắn tag
            Panel catHeader = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            int noTagW = Math.Max(158, MeasurePillBtnWidth("Không gắn tag", 8.6F, 17, true, 14));
            var btnNoTag = MakeBtn("Không gắn tag", w - noTagW, 0, noTagW, 34, 17, clrCardBg, clrIndigo, clrBorderLight, 1, 8.6F);
            btnNoTag.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            AttachBtnIcon(btnNoTag, UiIcon.Close, clrIndigo, 14);

            var lblCatHeader = MakeLabel("Danh mục chi tiêu", 2, 4, 10.2F, FontStyle.Bold, clrTextMain, Math.Max(100, btnNoTag.Left - 12));
            lblCatHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblCatHeader.Top = Math.Max(2, (catHeader.Height - lblCatHeader.Height) / 2);

            void LayoutCatHeader()
            {
                if (catHeader.IsDisposed) return;
                btnNoTag.Left = Math.Max(100, catHeader.Width - noTagW);
                lblCatHeader.Width = Math.Max(90, btnNoTag.Left - 12);
            }

            catHeader.Controls.Add(lblCatHeader);
            catHeader.Controls.Add(btnNoTag);
            catHeader.Resize += (s, e) => LayoutCatHeader();
            LayoutCatHeader();
            col.Controls.Add(catHeader);
            y += 38;

            // Lưới 3 cột x 2 hàng cân đối cho 6 danh mục
            var catGrid = new BufferedTableLayout
            {
                Location = new Point(0, y),
                Size = new Size(w, 86),
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

            // Thẻ nhập thông tin chi tiêu & gắn tag bạn bè (thu gọn, bỏ rowTools trùng lặp)
            const int noteCardHeight = 218;
            Guna2Panel pnlNoteCard = new Guna2Panel
            {
                Location = new Point(0, y + 94),
                Size = new Size(w, noteCardHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            var rowItemAmt = new BufferedTableLayout
            {
                Location = new Point(14, 12),
                Size = new Size(w - 28, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            rowItemAmt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
            rowItemAmt.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
            rowItemAmt.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtNoteTop = MakeTextBox("", "Thêm tên món ăn", 0, 0, 180, 40, clrCardBg);
            txtNoteTop.Dock = DockStyle.Fill;
            txtNoteTop.Margin = new Padding(2, 0, 6, 0);

            var txtAmountNum = MakeTextBox("", "Thêm số tiền", 0, 0, 100, 40, clrCardBg, true);
            txtAmountNum.Dock = DockStyle.Fill;
            txtAmountNum.Margin = new Padding(2, 0, 2, 0);
            txtAmountNum.FillColor = clrSoftGreenBg;
            txtAmountNum.BorderColor = clrSoftGreenBorder;
            txtAmountNum.ForeColor = isDarkMode ? clrAccentSecondary : clrPrimaryGreen;
            txtAmountNum.FocusedState.FillColor = clrSoftGreenBg;

            rowItemAmt.Controls.Add(txtNoteTop, 0, 0);
            rowItemAmt.Controls.Add(txtAmountNum, 1, 0);

            Panel tagFriendsHeader = new Panel
            {
                Location = new Point(14, 56),
                Size = new Size(w - 28, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            int addFrQuickW = Math.Max(104, MeasurePillBtnWidth("+ Thêm bạn", 8.1F, 13));
            var btnAddFriendQuick = MakeBtn(
                "+ Thêm bạn",
                Math.Max(0, tagFriendsHeader.Width - addFrQuickW),
                0,
                addFrQuickW,
                26,
                13,
                clrSoftGreenBg,
                isDarkMode ? clrAccentSecondary : clrPrimaryGreen,
                clrSoftGreenBorder,
                1,
                8.1F);
            btnAddFriendQuick.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddFriendQuick.HoverState.FillColor = clrPrimaryGreen;
            btnAddFriendQuick.HoverState.ForeColor = Color.White;
            btnAddFriendQuick.HoverState.BorderColor = clrPrimaryGreen;
            btnAddFriendQuick.Click += (s, e) => OpenFriendsDrawer();

            Label lblTagFriends = MakeLabel(
                "Gắn tag bạn bè (Bỏ trống = Cá nhân)",
                2,
                3,
                8.6F,
                FontStyle.Bold,
                clrIndigo,
                Math.Max(100, btnAddFriendQuick.Left - 8));
            lblTagFriends.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblTagFriends.Top = Math.Max(1, (tagFriendsHeader.Height - lblTagFriends.Height) / 2);

            void LayoutTagFriendsHeader()
            {
                if (tagFriendsHeader.IsDisposed) return;
                int thw = tagFriendsHeader.Width;
                int bw = Math.Min(addFrQuickW, Math.Max(88, thw / 2));
                btnAddFriendQuick.SetBounds(thw - bw, 0, bw, 26);
                lblTagFriends.Width = Math.Max(80, btnAddFriendQuick.Left - 8);
            }

            tagFriendsHeader.Controls.Add(lblTagFriends);
            tagFriendsHeader.Controls.Add(btnAddFriendQuick);
            tagFriendsHeader.Resize += (s, e) => LayoutTagFriendsHeader();
            LayoutTagFriendsHeader();

            Guna2Panel pnlFriendBox = new Guna2Panel
            {
                Location = new Point(14, 86),
                Size = new Size(w - 28, 74),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 14,
                FillColor = clrInputBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Padding = new Padding(5, 3, 5, 3),
                UseTransparentBackground = true
            };

            FlowLayoutPanel flowFriends = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                BackColor = clrInputBg,
                Padding = new Padding(3, 3, 3, 3)
            };
            pnlFriendBox.Controls.Add(flowFriends);

            var txtCaption = MakeTextBox("", "Thêm một tin nhắn", 14, 167, w - 28, 40, clrCardBg);
            txtCaption.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            void UpdateNotePlaceholderForCategory(string category)
            {
                if (category == null)
                    txtNoteTop.PlaceholderText = "Thêm tên khoản chi";
                else if (category.Contains("Đồ ăn") || category.Contains("Ăn uống"))
                    txtNoteTop.PlaceholderText = "Thêm tên món ăn";
                else if (category.Contains("Xăng"))
                    txtNoteTop.PlaceholderText = "Thêm tên trạm xăng";
                else if (category.Contains("Quần áo"))
                    txtNoteTop.PlaceholderText = "Thêm tên món đồ";
                else if (category.Contains("Tiền trọ"))
                    txtNoteTop.PlaceholderText = "Thêm địa chỉ / phòng trọ";
                else if (category.Contains("Mua sắm"))
                    txtNoteTop.PlaceholderText = "Thêm tên món đồ";
                else if (category.Contains("Học tập"))
                    txtNoteTop.PlaceholderText = "Thêm tên khoản học tập";
                else
                    txtNoteTop.PlaceholderText = "Thêm tên khoản chi";
            }

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

                bool noTagActive = string.IsNullOrEmpty(selectedCategory);
                btnNoTag.FillColor = noTagActive ? clrSoftGreenBg : clrCardBg;
                btnNoTag.ForeColor = noTagActive ? accentInk : clrIndigo;
                btnNoTag.BorderColor = noTagActive ? clrSoftGreenBorder : clrBorderLight;
                AttachBtnIcon(btnNoTag, UiIcon.Close, noTagActive ? accentIconInk : clrIndigo, 14);
            };

            string lastPreviewIconKey = null;
            Action updatePreview = () =>
            {
                if (string.IsNullOrEmpty(selectedCategory))
                {
                    badgeLiveTop.Text = "Ảnh khoảnh khắc Pocket (Không tính chi tiêu)";
                    badgeLiveTop.FillColor = clrInputBg;
                    badgeLiveTop.ForeColor = clrIndigo;
                    badgeLiveTop.BorderColor = clrBorderLight;
                    badgeLiveTop.HoverState.FillColor = clrInputBg;
                    badgeLiveTop.HoverState.BorderColor = clrBorderLight;
                    if (lastPreviewIconKey != "__NONE__")
                    {
                        AttachBtnIcon(badgeLiveTop, UiIcon.Camera, clrIndigo, 15);
                        lastPreviewIconKey = "__NONE__";
                    }
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
                    badgeLiveTop.ForeColor = accentInk;
                    badgeLiveTop.BorderColor = clrSoftGreenBorder;
                    badgeLiveTop.HoverState.FillColor = clrSoftGreenBg;
                    badgeLiveTop.HoverState.BorderColor = clrSoftGreenBorder;
                    if (lastPreviewIconKey != selectedCategory)
                    {
                        AttachBtnIcon(badgeLiveTop, GetCategoryIcon(selectedCategory), accentIconInk, 15);
                        lastPreviewIconKey = selectedCategory;
                    }
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

                int maxChipW = Math.Max(140, (flowFriends.ClientSize.Width > 40 ? flowFriends.ClientSize.Width : pnlFriendBox.Width - 16) - 28);

                if (myFriends.Count == 0)
                {
                    string hintTxt = "+ Chưa có bạn bè • Bấm để kết bạn";
                    int hintW = Math.Clamp(MeasurePillBtnWidth(hintTxt, 8.2F, 14), 200, maxChipW);
                    var btnHintAddFriend = MakeBtn(hintTxt, 4, 3, hintW, 28, 14, clrCardBg, accentInk, clrSoftGreenBorder, 1, 8.2F);
                    btnHintAddFriend.Margin = new Padding(3, 2, 3, 2);
                    btnHintAddFriend.Click += (s, e) => OpenFriendsDrawer();
                    flowFriends.Controls.Add(btnHintAddFriend);
                }
                else
                {
                    foreach (var f in myFriends)
                    {
                        bool alreadyTagged = taggedFriendNames.Contains(f.Name);
                        int chipW = Math.Clamp(MeasurePillBtnWidth($"✓ {f.Name}", 8.2F, 14), 94, maxChipW);
                        var fBtn = MakeBtn(
                            alreadyTagged ? $"✓ {f.Name}" : $"+ {f.Name}",
                            0, 0, chipW, 28, 14,
                            alreadyTagged ? clrPrimaryGreen : clrCardBg,
                            alreadyTagged ? Color.White : clrTextMain,
                            alreadyTagged ? clrPrimaryGreen : clrBorderLight,
                            1, 8.2F);
                        fBtn.Margin = new Padding(3, 2, 3, 2);
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
                    UpdateNotePlaceholderForCategory(selectedCategory);
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
                UpdateNotePlaceholderForCategory(null);
            };

            txtNoteTop.TextChanged += (s, e) => updatePreview();
            txtAmountNum.TextChanged += (s, e) => updatePreview();
            updatePreview();

            pnlNoteCard.Controls.AddRange(new Control[]
            {
                rowItemAmt, tagFriendsHeader, pnlFriendBox, txtCaption
            });

            col.Controls.Add(catGrid);
            col.Controls.Add(pnlNoteCard);
            y += 94 + noteCardHeight + 12;

            var btnPostPhoto = new Guna2GradientButton
            {
                Text = "Đăng lên Bảng tin Pocket",
                Location = new Point(0, y),
                Size = new Size(w, 52),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 26,
                FillColor = theme.AccentPrimary,
                FillColor2 = theme.AccentSecondary,
                GradientMode = LinearGradientMode.ForwardDiagonal,
                ForeColor = Color.White,
                Font = FontBold(10.8F),
                BackColor = Color.Transparent,
                UseTransparentBackground = true,
                AutoRoundedCorners = false,
                Cursor = Cursors.Hand
            };
            btnPostPhoto.DisabledState.FillColor = clrInputBg;
            btnPostPhoto.DisabledState.FillColor2 = clrInputBg;
            btnPostPhoto.DisabledState.ForeColor = clrTextMuted;
            btnPostPhoto.Image = CreateVectorIcon(UiIcon.Send, Color.White, 18);
            btnPostPhoto.ImageSize = new Size(18, 18);
            btnPostPhoto.ImageAlign = HorizontalAlignment.Center;
            btnPostPhoto.ImageOffset = new Point(-2, 0);
            btnPostPhoto.TextOffset = new Point(2, 0);
            AttachEdgeResizeRedirector(btnPostPhoto);

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
            col.Height = btnPostPhoto.Bottom + 8;

            Func<CameraDraftState> captureCameraDraft = () => new CameraDraftState
            {
                UploadedPhoto = uploadedPhoto != null ? new Bitmap(uploadedPhoto) : null,
                SelectedCategory = selectedCategory,
                TaggedFriends = taggedFriendNames.ToList(),
                NoteText = txtNoteTop.Text,
                AmountText = txtAmountNum.Text,
                CaptionText = txtCaption.Text
            };

            Action<CameraDraftState> restoreCameraDraft = draft =>
            {
                if (draft == null || page.IsDisposed) return;

                selectedCategory = draft.SelectedCategory;
                taggedFriendNames.Clear();
                if (draft.TaggedFriends != null)
                {
                    foreach (string name in draft.TaggedFriends)
                    {
                        if (!string.IsNullOrEmpty(name)) taggedFriendNames.Add(name);
                    }
                }

                UpdateNotePlaceholderForCategory(selectedCategory);
                txtNoteTop.Text = draft.NoteText ?? "";
                txtAmountNum.Text = draft.AmountText ?? "";
                txtCaption.Text = draft.CaptionText ?? "";

                refreshCategoryStyles();
                populateFriendChips();

                if (draft.UploadedPhoto != null)
                {
                    applyCustomImageToViewfinder(draft.UploadedPhoto, "Đã chụp");
                }
            };

            cameraDraftCapture = captureCameraDraft;
            cameraDraftRestore = restoreCameraDraft;

            page.Disposed += (s, e) =>
            {
                if (cameraDraftCapture == captureCameraDraft) cameraDraftCapture = null;
                if (cameraDraftRestore == restoreCameraDraft) cameraDraftRestore = null;
                if (pasteImageToCameraAction == localPasteAction) pasteImageToCameraAction = null;

                StopWebcam();

                try { uploadedPhoto?.Dispose(); } catch { }
                uploadedPhoto = null;

                try
                {
                    Image previewImg = picPreview.Image;
                    picPreview.Image = null;
                    previewImg?.Dispose();
                }
                catch { }

                try { ctxMenu.Dispose(); } catch { }

                static void DisposeButtonImage(Func<Image> getImg, Action<Image> setImg)
                {
                    try
                    {
                        Image img = getImg();
                        setImg(null);
                        img?.Dispose();
                    }
                    catch { }
                }

                foreach (var btn in new Guna2Button[] { badgeLiveTop, btnSwitchCam, btnNoTag, btnChooseFile, btnPasteImg })
                    DisposeButtonImage(() => btn.Image, img => btn.Image = img);

                DisposeButtonImage(() => btnPostPhoto.Image, img => btnPostPhoto.Image = img);

                foreach (var item in catButtons)
                    DisposeButtonImage(() => item.Btn.Image, img => item.Btn.Image = img);
            };

            AttachCameraScrollWidthGuard(page, col);
            return page;
        }

        private void AttachCameraScrollWidthGuard(Panel page, Panel col)
        {
            void SyncScrollWidth()
            {
                if (page.IsDisposed || col.IsDisposed) return;
                int availW = page.ClientSize.Width;
                if (availW <= 0) return;

                bool needsVScroll = col.Height > page.ClientSize.Height;
                int vBarReserve = (needsVScroll && !page.VerticalScroll.Visible)
                    ? Math.Max(SystemInformation.VerticalScrollBarWidth, (int)Math.Ceiling(18.0 * page.DeviceDpi / 96.0))
                    : 0;
                int effectiveW = Math.Max(260, availW - vBarReserve);
                int sidePad = effectiveW < 450 ? 10 : 16;
                int targetW = Math.Clamp(effectiveW - sidePad * 2, 260, MaxColWidth);
                int targetX = Math.Max(sidePad, (effectiveW - targetW) / 2);

                if (col.Left != targetX || col.Width != targetW)
                {
                    col.Location = new Point(targetX, page.AutoScrollPosition.Y);
                    col.Width = targetW;
                }

                if (page.HorizontalScroll.Visible && col.Right <= page.ClientSize.Width && page.IsHandleCreated)
                {
                    page.BeginInvoke(new Action(() =>
                    {
                        if (!page.IsDisposed && !col.IsDisposed && page.HorizontalScroll.Visible && col.Right <= page.ClientSize.Width)
                        {
                            page.HorizontalScroll.Value = 0;
                            page.PerformLayout();
                        }
                    }));
                }
            }

            page.Resize += (s, e) => SyncScrollWidth();
            page.Layout += (s, e) => SyncScrollWidth();
            page.HandleCreated += (s, e) => SyncScrollWidth();
            SyncScrollWidth();
        }
    }
}
