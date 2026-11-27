using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace PocketLog.Client
{
    public partial class Client
    {
        private static Color BlendControlColor(Color a, Color b, float t)
        {
            float clamped = Math.Clamp(t, 0f, 1f);
            int alpha = (int)Math.Round(a.A + (b.A - a.A) * clamped);
            int r = (int)Math.Round(a.R + (b.R - a.R) * clamped);
            int g = (int)Math.Round(a.G + (b.G - a.G) * clamped);
            int bl = (int)Math.Round(a.B + (b.B - a.B) * clamped);
            return Color.FromArgb(alpha, r, g, bl);
        }

        public class GradientPillButton : Guna2Button
        {
            private bool isGradientActive;
            private Color gradientStart = Color.FromArgb(79, 70, 229);
            private Color gradientEnd = Color.FromArgb(14, 165, 233);
            private int badgeCount;
            private Color badgeFillColor = Color.FromArgb(244, 63, 94);
            private Color badgeHaloColor = Color.Empty;
            private bool isHovered;
            private bool isPressed;

            public bool IsGradientActive
            {
                get => isGradientActive;
                set
                {
                    if (isGradientActive != value)
                    {
                        isGradientActive = value;
                        Invalidate();
                    }
                }
            }

            public Color GradientStart
            {
                get => gradientStart;
                set
                {
                    if (gradientStart != value)
                    {
                        gradientStart = value;
                        Invalidate();
                    }
                }
            }

            public Color GradientEnd
            {
                get => gradientEnd;
                set
                {
                    if (gradientEnd != value)
                    {
                        gradientEnd = value;
                        Invalidate();
                    }
                }
            }

            public int BadgeCount
            {
                get => badgeCount;
                set
                {
                    int safe = Math.Max(0, value);
                    if (badgeCount != safe)
                    {
                        badgeCount = safe;
                        Invalidate();
                    }
                }
            }

            public Color BadgeFillColor
            {
                get => badgeFillColor;
                set
                {
                    if (badgeFillColor != value)
                    {
                        badgeFillColor = value;
                        Invalidate();
                    }
                }
            }

            public Color BadgeHaloColor
            {
                get => badgeHaloColor;
                set
                {
                    if (badgeHaloColor != value)
                    {
                        badgeHaloColor = value;
                        Invalidate();
                    }
                }
            }

            public GradientPillButton()
            {
                DoubleBuffered = true;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                isHovered = true;
                if (isGradientActive) Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                isHovered = false;
                isPressed = false;
                if (isGradientActive) Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs mevent)
            {
                base.OnMouseDown(mevent);
                if (mevent.Button == MouseButtons.Left)
                {
                    isPressed = true;
                    if (isGradientActive) Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs mevent)
            {
                base.OnMouseUp(mevent);
                isPressed = false;
                if (isGradientActive) Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (!isGradientActive)
                {
                    base.OnPaint(e);
                    if (badgeCount > 0) DrawCounterBadge(e.Graphics);
                    return;
                }

                if (Width <= 2 || Height <= 2) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                Color c1 = gradientStart.A == 0 ? Color.FromArgb(79, 70, 229) : gradientStart;
                Color c2 = gradientEnd.A == 0 ? Color.FromArgb(14, 165, 233) : gradientEnd;
                if (!Enabled)
                {
                    c1 = BlendControlColor(c1, Color.Gray, 0.45f);
                    c2 = BlendControlColor(c2, Color.Gray, 0.45f);
                }
                else if (isPressed)
                {
                    c1 = BlendControlColor(c1, Color.Black, 0.12f);
                    c2 = BlendControlColor(c2, Color.Black, 0.12f);
                }
                else if (isHovered)
                {
                    c1 = BlendControlColor(c1, Color.White, 0.10f);
                    c2 = BlendControlColor(c2, Color.White, 0.10f);
                }

                float radius = Math.Max(2f, Math.Min(BorderRadius, Math.Min(Width, Height) / 2f));
                var pillRect = new RectangleF(0.5f, 0.5f, Math.Max(2f, Width - 1f), Math.Max(2f, Height - 1f));
                using (GraphicsPath path = CreateRoundedPath(pillRect.X, pillRect.Y, pillRect.Width, pillRect.Height, radius))
                using (var brush = new LinearGradientBrush(pillRect, c1, c2, 35f))
                {
                    brush.WrapMode = WrapMode.TileFlipXY;
                    g.FillPath(brush, path);
                }

                DrawGradientContent(g);

                if (badgeCount > 0)
                {
                    DrawCounterBadge(g);
                }
            }

            private void DrawGradientContent(Graphics g)
            {
                string label = Text ?? "";
                bool hasImg = Image != null;
                bool hasTxt = !string.IsNullOrEmpty(label);
                int imgW = hasImg ? (ImageSize.Width > 0 ? ImageSize.Width : Image.Width) : 0;
                int imgH = hasImg ? (ImageSize.Height > 0 ? ImageSize.Height : Image.Height) : 0;

                if (hasImg && hasTxt)
                {
                    const int gap = 7;
                    int availW = Math.Max(16, Width - 12);
                    int maxTxtW = Math.Max(8, availW - imgW - gap);
                    Size txtSz = TextRenderer.MeasureText(
                        g,
                        label,
                        Font,
                        new Size(maxTxtW, Height),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    int drawTxtW = Math.Min(txtSz.Width, maxTxtW);
                    int totalW = imgW + gap + drawTxtW;
                    int startX = Math.Max(6, (Width - totalW) / 2);

                    g.DrawImage(Image, new Rectangle(startX, (Height - imgH) / 2, imgW, imgH));
                    TextRenderer.DrawText(
                        g,
                        label,
                        Font,
                        new Rectangle(startX + imgW + gap, 0, drawTxtW + 2, Height),
                        ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                }
                else if (hasTxt)
                {
                    TextRenderer.DrawText(
                        g,
                        label,
                        Font,
                        new Rectangle(6, 0, Math.Max(1, Width - 12), Height),
                        ForeColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                }
                else if (hasImg)
                {
                    g.DrawImage(Image, new Rectangle((Width - imgW) / 2, (Height - imgH) / 2, imgW, imgH));
                }
            }

            private void DrawCounterBadge(Graphics g)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                string text = badgeCount > 9 ? "9+" : badgeCount.ToString();
                using Font badgeFont = new Font("Segoe UI", 7.2F, FontStyle.Bold);
                Size measured = TextRenderer.MeasureText(
                    g,
                    text,
                    badgeFont,
                    new Size(64, 32),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

                int bw = Math.Max(text.Length > 1 ? 22 : 18, measured.Width + 8);
                int bh = Math.Max(18, measured.Height + 4);
                int bx = Math.Max(0, Width - bw - 6);
                int by = 4;

                Color fill = badgeFillColor.A == 0 ? Color.FromArgb(244, 63, 94) : badgeFillColor;
                Color halo = badgeHaloColor != Color.Empty
                    ? badgeHaloColor
                    : (isGradientActive ? BlendControlColor(gradientStart, gradientEnd, 0.55f) : Color.White);

                using (GraphicsPath path = CreateRoundedPath(bx, by, bw, bh, bh / 2f))
                using (var fillBrush = new SolidBrush(fill))
                using (var haloPen = new Pen(halo, 1.5f))
                {
                    g.FillPath(fillBrush, path);
                    g.DrawPath(haloPen, path);
                }

                TextRenderer.DrawText(
                    g,
                    text,
                    badgeFont,
                    new Rectangle(bx, by, bw, bh),
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        public class SocialPhotoCanvas : Control
        {
            private readonly Client owner;
            private readonly LocketPost post;
            private readonly bool isMyTopCard;
            private readonly bool compactThumbnail;
            private Bitmap cachedCoverBmp;
            private int cachedW;
            private int cachedH;
            private bool isHovered;

            public SocialPhotoCanvas(Client owner, LocketPost post, bool isMyTopCard = false, bool compactThumbnail = false)
            {
                this.owner = owner;
                this.post = post;
                this.isMyTopCard = isMyTopCard;
                this.compactThumbnail = compactThumbnail;

                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default;
                owner?.AttachEdgeResizeRedirector(this);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                if (isMyTopCard)
                {
                    isHovered = true;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (isHovered)
                {
                    isHovered = false;
                    Invalidate();
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    cachedCoverBmp?.Dispose();
                    cachedCoverBmp = null;
                }
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                int w = ClientSize.Width;
                int h = ClientSize.Height;
                if (w < 12 || h < 12) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                AppPalette p = owner?.CurrentTheme ?? LightPalette;
                float radius = compactThumbnail ? 16f : 26f;
                var cardRect = new RectangleF(0.5f, 0.5f, Math.Max(2f, w - 1f), Math.Max(2f, h - 1f));

                using GraphicsPath cardPath = CreateRoundedPath(cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height, radius);
                GraphicsState savedState = g.Save();
                g.SetClip(cardPath);

                PaintPhotoOrGradientBackground(g, w, h, p);

                if (!compactThumbnail)
                {
                    PaintBottomScrim(g, w, h, p);
                    PaintTopPills(g, w, p);
                    PaintBottomCaptionPill(g, w, h);
                }

                g.Restore(savedState);

                if (isMyTopCard && isHovered)
                {
                    using GraphicsPath hoverPath = CreateRoundedPath(1f, 1f, Math.Max(2f, w - 2f), Math.Max(2f, h - 2f), Math.Max(2f, radius - 0.5f));
                    using var hoverPen = new Pen(p.AccentPrimary, 2.2f);
                    g.DrawPath(hoverPen, hoverPath);
                }
                else
                {
                    Color borderClr = owner != null && owner.isDarkMode
                        ? Color.FromArgb(48, 255, 255, 255)
                        : Color.FromArgb(26, 15, 23, 42);
                    using var borderPen = new Pen(borderClr, 1f);
                    g.DrawPath(borderPen, cardPath);
                }
            }

            private void PaintPhotoOrGradientBackground(Graphics g, int w, int h, AppPalette p)
            {
                if (post?.CustomImage != null && owner != null)
                {
                    if (cachedCoverBmp == null || Math.Abs(w - cachedW) >= 6 || Math.Abs(h - cachedH) >= 6)
                    {
                        Bitmap next = owner.FitImageCover(post.CustomImage, w, h);
                        if (next != null)
                        {
                            cachedCoverBmp?.Dispose();
                            cachedCoverBmp = next;
                            cachedW = w;
                            cachedH = h;
                        }
                    }

                    if (cachedCoverBmp != null)
                    {
                        g.DrawImage(cachedCoverBmp, 0, 0, w, h);
                        return;
                    }
                }

                Color baseClr = (post != null && post.PhotoBg.A > 0) ? post.PhotoBg : p.HeroGradStart;
                Color topClr = BlendControlColor(baseClr, Color.White, 0.14f);
                Color botClr = BlendControlColor(baseClr, Color.FromArgb(15, 23, 42), 0.34f);

                using (var bgBrush = new LinearGradientBrush(new RectangleF(0, 0, w, h), topClr, botClr, 135f))
                {
                    bgBrush.WrapMode = WrapMode.TileFlipXY;
                    g.FillRectangle(bgBrush, 0, 0, w, h);
                }

                float minSide = Math.Min(w, h);
                using (var b1 = new SolidBrush(Color.FromArgb(24, 255, 255, 255)))
                using (var b2 = new SolidBrush(Color.FromArgb(16, 255, 255, 255)))
                using (var b3 = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                {
                    float d1 = minSide * 0.56f;
                    float d2 = minSide * 0.38f;
                    float d3 = minSide * 0.30f;
                    g.FillEllipse(b1, w - d1 * 0.65f, -d1 * 0.25f, d1, d1);
                    g.FillEllipse(b2, -d2 * 0.28f, h * 0.34f, d2, d2);
                    g.FillEllipse(b3, w * 0.55f, h * 0.62f, d3, d3);
                }

                int boxSz = compactThumbnail ? 44 : 64;
                float boxRadius = compactThumbnail ? 14f : 22f;
                float bx = (w - boxSz) / 2f;
                float by = compactThumbnail ? (h - boxSz) / 2f : Math.Max(46f, (h - boxSz) / 2f - 22f);

                using (GraphicsPath badgePath = CreateRoundedPath(bx, by, boxSz, boxSz, boxRadius))
                using (var fill = new SolidBrush(Color.FromArgb(56, 255, 255, 255)))
                using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f))
                {
                    g.FillPath(fill, badgePath);
                    g.DrawPath(pen, badgePath);
                }

                if (owner != null)
                {
                    int icoSz = compactThumbnail ? 22 : 30;
                    using Bitmap ico = owner.CreateVectorIcon(owner.GetCategoryIcon(post?.Category), Color.White, icoSz);
                    g.DrawImage(ico, bx + (boxSz - icoSz) / 2f, by + (boxSz - icoSz) / 2f, icoSz, icoSz);
                }
            }

            private static void PaintBottomScrim(Graphics g, int w, int h, AppPalette p)
            {
                float scrimStartY = h * 0.56f;
                if (h - scrimStartY < 4f) return;

                Color topScrim = p.ScrimTop.A == 0 ? Color.FromArgb(0, 15, 23, 42) : p.ScrimTop;
                Color botScrim = p.ScrimBottom.A > 0 ? p.ScrimBottom : Color.FromArgb(185, 15, 23, 42);
                using var scrim = new LinearGradientBrush(
                    new PointF(0f, scrimStartY),
                    new PointF(0f, h + 1f),
                    topScrim,
                    botScrim);
                scrim.WrapMode = WrapMode.TileFlipXY;
                g.FillRectangle(scrim, 0f, scrimStartY, w, h - scrimStartY + 1f);
            }

            private void PaintTopPills(Graphics g, int w, AppPalette p)
            {
                const int padEdge = 12;
                const int pillGap = 8;
                using Font pillFont = owner != null ? owner.FontMedium(8.2F) : new Font("Segoe UI", 8.2F, FontStyle.Bold);
                using var fmt = new StringFormat(StringFormat.GenericTypographic)
                {
                    FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };

                int rightPillW = 0;
                int tagCount = post?.TaggedFriends?.Count ?? 0;
                if (tagCount > 0)
                {
                    string tagLabel;
                    if (tagCount == 1)
                    {
                        string name = (post.TaggedFriends[0] ?? "").Trim();
                        if (string.IsNullOrEmpty(name)) name = "bạn";
                        tagLabel = name.StartsWith("@", StringComparison.Ordinal) ? name : "@" + name;
                    }
                    else
                    {
                        tagLabel = $"Tag +{tagCount} bạn";
                    }

                    const int rightIcoPx = 12;
                    const int rightOverhead = 10 + rightIcoPx + 5 + 10 + 4;
                    SizeF tagSz = g.MeasureString(tagLabel, pillFont, 400, fmt);
                    int maxRightW = Math.Max(88, (int)(w * 0.44f));
                    rightPillW = Math.Clamp((int)Math.Ceiling(tagSz.Width) + rightOverhead, 72, maxRightW);
                    int pillH = Math.Max(26, (int)Math.Ceiling(tagSz.Height) + 10);
                    float rx = w - padEdge - rightPillW;
                    DrawFrostedPill(g, rx, padEdge, rightPillW, pillH, UiIcon.Users, rightIcoPx, tagLabel, pillFont, fmt);
                }

                string catLabel;
                if (post != null && (post.HasTag || post.Amount > 0 || !string.IsNullOrWhiteSpace(post.Category)))
                {
                    string catShort = owner != null ? owner.GetCategoryShortClean(post.Category) : "Chi tiêu";
                    if (post.Amount > 0)
                    {
                        string amtText = post.Amount >= 1000 && post.Amount % 1000 == 0
                            ? $"{post.Amount / 1000:N0}k"
                            : $"{post.Amount:N0}đ";
                        catLabel = $"{catShort} • {amtText}";
                    }
                    else
                    {
                        catLabel = catShort;
                    }
                }
                else
                {
                    catLabel = "Khoảnh khắc Pocket";
                }

                const int leftIcoPx = 13;
                const int leftOverhead = 10 + leftIcoPx + 5 + 10 + 4;
                int availLeftW = rightPillW > 0
                    ? Math.Max(88, w - padEdge * 2 - rightPillW - pillGap)
                    : Math.Max(100, w - padEdge * 2);

                SizeF catSz = g.MeasureString(catLabel, pillFont, availLeftW, fmt);
                int leftPillW = Math.Clamp((int)Math.Ceiling(catSz.Width) + leftOverhead, 76, availLeftW);
                int leftPillH = Math.Max(26, (int)Math.Ceiling(catSz.Height) + 10);
                UiIcon catIcon = owner != null ? owner.GetCategoryIcon(post?.Category) : UiIcon.Camera;
                DrawFrostedPill(g, padEdge, padEdge, leftPillW, leftPillH, catIcon, leftIcoPx, catLabel, pillFont, fmt);
            }

            private void DrawFrostedPill(
                Graphics g,
                float x,
                float y,
                float w,
                float h,
                UiIcon icon,
                int iconPx,
                string text,
                Font font,
                StringFormat fmt)
            {
                using GraphicsPath path = CreateRoundedPath(x, y, w, h, h / 2f);
                using (var fill = new SolidBrush(Color.FromArgb(168, 15, 23, 42)))
                using (var border = new Pen(Color.FromArgb(75, 255, 255, 255), 1f))
                {
                    g.FillPath(fill, path);
                    g.DrawPath(border, path);
                }

                float textX = x + 10f;
                if (owner != null)
                {
                    using Bitmap ico = owner.CreateVectorIcon(icon, Color.White, iconPx);
                    g.DrawImage(ico, x + 10f, y + (h - iconPx) / 2f, iconPx, iconPx);
                    textX = x + 10f + iconPx + 5f;
                }

                var textRect = new RectangleF(textX, y, Math.Max(12f, (x + w) - textX - 9f), h);
                g.DrawString(text, font, Brushes.White, textRect, fmt);
            }

            private void PaintBottomCaptionPill(Graphics g, int w, int h)
            {
                string cap = string.IsNullOrWhiteSpace(post?.CaptionInside) ? "Khoảnh khắc" : post.CaptionInside.Trim();
                using Font capFont = owner != null ? owner.FontMedium(9.3F) : new Font("Segoe UI", 9.3F, FontStyle.Bold);

                int maxPillW = Math.Max(110, w - 28);
                const int horizPad = 30;
                int maxTextW = Math.Max(60, maxPillW - horizPad);

                using var singleFmt = new StringFormat(StringFormat.GenericTypographic)
                {
                    FormatFlags = StringFormatFlags.NoWrap,
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                using var wrapFmt = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisWord
                };

                SizeF singleSz = g.MeasureString(cap, capFont, 2400, singleFmt);
                int pillW;
                int pillH;
                int textBoxH;
                float pillRadius;
                bool isSingle = singleSz.Width <= maxTextW;

                if (isSingle)
                {
                    pillW = Math.Clamp((int)Math.Ceiling(singleSz.Width) + horizPad, 110, maxPillW);
                    textBoxH = Math.Max(20, (int)Math.Ceiling(capFont.GetHeight(g)) + 2);
                    pillH = Math.Max(38, textBoxH + 16);
                    pillRadius = pillH / 2f;
                }
                else
                {
                    float lineStep = capFont.GetHeight(g);
                    int twoLineCapH = (int)Math.Ceiling(lineStep * 2f) + 2;
                    SizeF wrapSz = g.MeasureString(cap, capFont, new SizeF(maxTextW, twoLineCapH), wrapFmt);
                    textBoxH = Math.Min((int)Math.Ceiling(wrapSz.Height) + 2, twoLineCapH);
                    pillW = Math.Clamp((int)Math.Ceiling(wrapSz.Width) + horizPad, 128, maxPillW);
                    pillH = Math.Max(50, textBoxH + 16);
                    pillRadius = 18f;
                }

                float px = (w - pillW) / 2f;
                float py = Math.Max(48f, h - pillH - 14f);

                using (GraphicsPath pillPath = CreateRoundedPath(px, py, pillW, pillH, pillRadius))
                using (var fill = new SolidBrush(Color.FromArgb(172, 18, 24, 38)))
                using (var border = new Pen(Color.FromArgb(85, 255, 255, 255), 1f))
                {
                    g.FillPath(fill, pillPath);
                    g.DrawPath(border, pillPath);
                }

                float boxY = py + (pillH - textBoxH) / 2f;
                var capRect = new RectangleF(px + 12f, boxY, Math.Max(20f, pillW - 24f), textBoxH);
                g.DrawString(cap, capFont, Brushes.White, capRect, isSingle ? singleFmt : wrapFmt);
            }
        }

        public class StoryAvatarStrip : Control
        {
            private const int SlotWidth = 72;
            private const int PadX = 10;

            private static readonly Color[] AvatarTints =
            {
                Color.FromArgb(79, 70, 229),
                Color.FromArgb(14, 165, 233),
                Color.FromArgb(236, 72, 153),
                Color.FromArgb(245, 158, 11),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(139, 92, 246)
            };

            private readonly Client owner;
            private readonly List<FriendUser> sortedFriends;
            private readonly int suggestionsCount;
            private readonly Action onOpenFriendsDrawer;
            private int scrollOffsetX;
            private int hoveredSlot = -1;
            private int dragStartX = -1;
            private int dragStartScroll;
            private bool dragged;

            public IReadOnlyList<FriendUser> SortedFriends => sortedFriends;
            public int ScrollOffsetX => scrollOffsetX;

            public StoryAvatarStrip(Client owner, List<FriendUser> friends, int suggestionsCount, Action onOpenFriendsDrawer)
            {
                this.owner = owner;
                this.suggestionsCount = Math.Max(0, suggestionsCount);
                this.onOpenFriendsDrawer = onOpenFriendsDrawer;

                sortedFriends = (friends ?? new List<FriendUser>())
                    .Select((f, idx) => (Friend: f, Index: idx))
                    .Where(x => x.Friend != null)
                    .OrderByDescending(x => x.Friend.IsOnline)
                    .ThenBy(x => x.Index)
                    .Select(x => x.Friend)
                    .ToList();

                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Height = 96;
                Cursor = Cursors.Hand;
                owner?.AttachEdgeResizeRedirector(this);
            }

            private int MaxScrollOffset
            {
                get
                {
                    int totalW = PadX * 2 + (1 + sortedFriends.Count) * SlotWidth;
                    return Math.Max(0, totalW - Width);
                }
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                scrollOffsetX = Math.Clamp(scrollOffsetX, 0, MaxScrollOffset);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                int max = MaxScrollOffset;
                if (max <= 0) return;
                scrollOffsetX = Math.Clamp(scrollOffsetX - Math.Sign(e.Delta) * 48, 0, max);
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    dragStartX = e.X;
                    dragStartScroll = scrollOffsetX;
                    dragged = false;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (dragStartX >= 0 && e.Button == MouseButtons.Left && MaxScrollOffset > 0)
                {
                    int dx = e.X - dragStartX;
                    if (Math.Abs(dx) > 5)
                    {
                        dragged = true;
                        scrollOffsetX = Math.Clamp(dragStartScroll - dx, 0, MaxScrollOffset);
                        Invalidate();
                        return;
                    }
                }

                int relX = e.X + scrollOffsetX - PadX;
                int slot = relX >= 0 ? relX / SlotWidth : -1;
                int totalSlots = 1 + sortedFriends.Count;
                if (slot >= totalSlots) slot = -1;
                if (hoveredSlot != slot)
                {
                    hoveredSlot = slot;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                if (hoveredSlot != -1)
                {
                    hoveredSlot = -1;
                    Invalidate();
                }
            }

            protected override void OnClick(EventArgs e)
            {
                base.OnClick(e);
                if (!dragged)
                {
                    onOpenFriendsDrawer?.Invoke();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                dragStartX = -1;
                dragged = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (Width < 20 || Height < 20) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                AppPalette p = owner?.CurrentTheme ?? LightPalette;
                var bgRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
                using (GraphicsPath bgPath = CreateRoundedPath(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height, 20f))
                using (var bgBrush = new SolidBrush(p.CardBg))
                using (var borderPen = new Pen(p.BorderLight, 1f))
                {
                    g.FillPath(bgBrush, bgPath);
                    g.DrawPath(borderPen, bgPath);
                }

                GraphicsState state = g.Save();
                using (GraphicsPath innerClip = CreateRoundedPath(4f, 2f, Math.Max(4f, Width - 8f), Math.Max(4f, Height - 4f), 17f))
                {
                    g.SetClip(innerClip);

                    scrollOffsetX = Math.Clamp(scrollOffsetX, 0, MaxScrollOffset);
                    int totalSlots = 1 + sortedFriends.Count;

                    using Font initialFont = owner != null ? owner.FontBold(12.5F) : new Font("Segoe UI", 12.5F, FontStyle.Bold);
                    using Font nameOnlineFont = owner != null ? owner.FontMedium(7.9F) : new Font("Segoe UI", 7.9F, FontStyle.Bold);
                    using Font nameMutedFont = owner != null ? owner.FontRegular(7.9F) : new Font("Segoe UI", 7.9F, FontStyle.Regular);
                    using Font badgeFont = owner != null ? owner.FontBold(6.8F) : new Font("Segoe UI", 6.8F, FontStyle.Bold);
                    using var nameFmt = new StringFormat(StringFormatFlags.NoWrap)
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Near,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    using var centerFmt = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    for (int i = 0; i < totalSlots; i++)
                    {
                        float sx = PadX + i * SlotWidth - scrollOffsetX;
                        if (sx + SlotWidth < 0 || sx > Width) continue;

                        if (hoveredSlot == i)
                        {
                            using GraphicsPath hPath = CreateRoundedPath(sx + 2f, 4f, SlotWidth - 4f, Height - 8f, 14f);
                            using var hBrush = new SolidBrush(p.SoftAccentBg);
                            g.FillPath(hBrush, hPath);
                        }

                        if (i == 0)
                        {
                            PaintAddFriendSlot(g, sx, p, nameOnlineFont, badgeFont, nameFmt, centerFmt);
                        }
                        else
                        {
                            FriendUser friend = sortedFriends[i - 1];
                            PaintFriendSlot(g, sx, friend, p, initialFont, nameOnlineFont, nameMutedFont, nameFmt, centerFmt);
                        }
                    }
                }
                g.Restore(state);
            }

            private void PaintAddFriendSlot(
                Graphics g,
                float sx,
                AppPalette p,
                Font labelFont,
                Font badgeFont,
                StringFormat nameFmt,
                StringFormat centerFmt)
            {
                var circleRect = new RectangleF(sx + 12f, 10f, 48f, 48f);
                using (var fill = new SolidBrush(p.SoftAccentBg))
                using (var dashedPen = new Pen(p.AccentPrimary, 2f) { DashStyle = DashStyle.Dash })
                {
                    g.FillEllipse(fill, circleRect);
                    g.DrawEllipse(dashedPen, circleRect);
                }

                if (owner != null)
                {
                    using Bitmap ico = owner.CreateVectorIcon(UiIcon.UserPlus, p.AccentPrimary, 20);
                    g.DrawImage(ico, circleRect.X + 14f, circleRect.Y + 14f, 20f, 20f);
                }
                else
                {
                    using var plusBrush = new SolidBrush(p.AccentPrimary);
                    g.DrawString("+", labelFont, plusBrush, circleRect, centerFmt);
                }

                if (suggestionsCount > 0)
                {
                    string countText = suggestionsCount > 9 ? "9+" : suggestionsCount.ToString();
                    float bw = countText.Length > 1 ? 20f : 16f;
                    float bh = 16f;
                    float bx = circleRect.Right - bw + 3f;
                    float by = circleRect.Top - 2f;

                    using GraphicsPath dotPath = CreateRoundedPath(bx, by, bw, bh, bh / 2f);
                    using (var dotBrush = new SolidBrush(p.Danger))
                    using (var haloPen = new Pen(p.CardBg, 1.8f))
                    {
                        g.FillPath(dotBrush, dotPath);
                        g.DrawPath(haloPen, dotPath);
                    }
                    g.DrawString(countText, badgeFont, Brushes.White, new RectangleF(bx, by, bw, bh), centerFmt);
                }

                using var textBrush = new SolidBrush(p.AccentPrimary);
                g.DrawString("+ Kết bạn", labelFont, textBrush, new RectangleF(sx + 2f, 65f, SlotWidth - 4f, 22f), nameFmt);
            }

            private void PaintFriendSlot(
                Graphics g,
                float sx,
                FriendUser friend,
                AppPalette p,
                Font initialFont,
                Font nameOnlineFont,
                Font nameMutedFont,
                StringFormat nameFmt,
                StringFormat centerFmt)
            {
                var ringRect = new RectangleF(sx + 8.5f, 6.5f, 55f, 55f);
                var avatarRect = new RectangleF(sx + 12f, 10f, 48f, 48f);

                if (friend.IsOnline)
                {
                    using var ringBrush = new LinearGradientBrush(ringRect, p.StoryRingStart, p.StoryRingEnd, 45f);
                    ringBrush.InterpolationColors = new ColorBlend(3)
                    {
                        Colors = new[] { p.StoryRingStart, p.StoryRingMid, p.StoryRingEnd },
                        Positions = new[] { 0f, 0.5f, 1f }
                    };
                    using var ringPen = new Pen(ringBrush, 2.8f);
                    g.DrawEllipse(ringPen, ringRect);
                }
                else
                {
                    Color mutedRing = owner != null && owner.isDarkMode
                        ? Color.FromArgb(95, p.TextMuted)
                        : Color.FromArgb(180, p.TextMuted);
                    using var mutedPen = new Pen(mutedRing, 1.8f);
                    g.DrawEllipse(mutedPen, ringRect);
                }

                Color tint = AvatarTints[Math.Abs(friend.UserId) % AvatarTints.Length];
                Color avatarFill = friend.IsOnline ? tint : BlendControlColor(tint, p.TextMuted, 0.65f);
                using (var avatarBrush = new SolidBrush(avatarFill))
                {
                    g.FillEllipse(avatarBrush, avatarRect);
                }

                string cleanName = (friend.Name ?? "").Trim();
                string initial = string.IsNullOrEmpty(cleanName) ? "U" : cleanName.Substring(0, 1).ToUpper();
                g.DrawString(initial, initialFont, Brushes.White, avatarRect, centerFmt);

                if (friend.IsOnline)
                {
                    var dotRect = new RectangleF(avatarRect.Right - 11f, avatarRect.Bottom - 11f, 12f, 12f);
                    var haloRect = new RectangleF(dotRect.X - 2f, dotRect.Y - 2f, dotRect.Width + 4f, dotRect.Height + 4f);
                    using var haloBrush = new SolidBrush(p.CardBg);
                    using var dotBrush = new SolidBrush(p.Success);
                    g.FillEllipse(haloBrush, haloRect);
                    g.FillEllipse(dotBrush, dotRect);
                }

                string displayName = string.IsNullOrEmpty(cleanName) ? "Bạn bè" : cleanName;
                using var nameBrush = new SolidBrush(friend.IsOnline ? p.TextMain : p.TextMuted);
                g.DrawString(
                    displayName,
                    friend.IsOnline ? nameOnlineFont : nameMutedFont,
                    nameBrush,
                    new RectangleF(sx + 2f, 65f, SlotWidth - 4f, 22f),
                    nameFmt);
            }
        }

        public class DoubleRingShutterButton : Control
        {
            private readonly Client owner;
            private readonly System.Windows.Forms.Timer animTimer;
            private bool isCapturedMode;
            private bool isHovered;
            private bool isPressed;
            private float discScale = 1.0f;
            private float targetScale = 1.0f;

            public bool IsCapturedMode
            {
                get => isCapturedMode;
                set
                {
                    if (isCapturedMode != value)
                    {
                        isCapturedMode = value;
                        Invalidate();
                    }
                }
            }

            public DoubleRingShutterButton() : this(null)
            {
            }

            public DoubleRingShutterButton(Client owner)
            {
                this.owner = owner;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);
                DoubleBuffered = true;
                BackColor = Color.Transparent;
                Size = new Size(58, 58);
                Cursor = Cursors.Hand;

                animTimer = new System.Windows.Forms.Timer { Interval = 16 };
                animTimer.Tick += OnAnimationTick;
                owner?.AttachEdgeResizeRedirector(this);
            }

            private void OnAnimationTick(object sender, EventArgs e)
            {
                float diff = targetScale - discScale;
                if (Math.Abs(diff) <= 0.01f)
                {
                    discScale = targetScale;
                    animTimer.Stop();
                }
                else
                {
                    discScale += diff * 0.4f;
                }
                Invalidate();
            }

            private void UpdateTargetScale()
            {
                targetScale = isPressed ? 0.80f : (isHovered ? 0.92f : 1.0f);
                discScale += (targetScale - discScale) * 0.55f;
                animTimer.Start();
                Invalidate();
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                base.OnMouseEnter(e);
                isHovered = true;
                UpdateTargetScale();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                isHovered = false;
                isPressed = false;
                UpdateTargetScale();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button == MouseButtons.Left)
                {
                    isPressed = true;
                    UpdateTargetScale();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                isPressed = false;
                UpdateTargetScale();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    animTimer.Stop();
                    animTimer.Dispose();
                }
                base.Dispose(disposing);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                float side = Math.Min(ClientSize.Width, ClientSize.Height);
                if (side < 16f) return;

                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                AppPalette p = owner?.CurrentTheme ?? LightPalette;
                float cx = ClientSize.Width / 2f;
                float cy = ClientSize.Height / 2f;
                float outerDia = Math.Max(20f, side - 6f);
                var ringRect = new RectangleF(cx - outerDia / 2f, cy - outerDia / 2f, outerDia, outerDia);

                using (var shadowPen = new Pen(Color.FromArgb(42, 0, 0, 0), 4.5f))
                {
                    g.DrawEllipse(shadowPen, ringRect);
                }

                if (isCapturedMode)
                {
                    using var gradBrush = new LinearGradientBrush(ringRect, p.AccentPrimary, p.AccentSecondary, 45f);
                    using var ringPen = new Pen(gradBrush, 3f);
                    g.DrawEllipse(ringPen, ringRect);
                }
                else
                {
                    using var ringPen = new Pen(Color.White, 3f);
                    g.DrawEllipse(ringPen, ringRect);
                }

                float baseInnerDia = outerDia * (40f / 52f);
                float innerDia = baseInnerDia * discScale;
                var discRect = new RectangleF(cx - innerDia / 2f, cy - innerDia / 2f, innerDia, innerDia);

                Color discColor = isCapturedMode
                    ? BlendControlColor(Color.White, p.SoftAccentBg, isPressed ? 0.35f : 0.12f)
                    : (isPressed
                        ? Color.FromArgb(224, 230, 240)
                        : (isHovered ? Color.FromArgb(245, 248, 255) : Color.White));

                using (var discBrush = new SolidBrush(discColor))
                {
                    g.FillEllipse(discBrush, discRect);
                }

                if (isCapturedMode)
                {
                    int icoSz = Math.Max(12, (int)Math.Round(innerDia * 0.42f));
                    if (owner != null)
                    {
                        using Bitmap ico = owner.CreateVectorIcon(UiIcon.SwitchCam, p.AccentPrimary, icoSz);
                        g.DrawImage(ico, cx - icoSz / 2f, cy - icoSz / 2f, icoSz, icoSz);
                    }
                    else
                    {
                        float r = icoSz * 0.42f;
                        using var retakePen = new Pen(p.AccentPrimary, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                        g.DrawArc(retakePen, cx - r, cy - r, r * 2f, r * 2f, 20f, 290f);
                    }
                }
            }
        }
    }
}
