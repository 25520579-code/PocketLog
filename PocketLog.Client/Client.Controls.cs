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

        internal static void PaintElevatedCardSurface(
            Graphics g,
            Rectangle bounds,
            int cornerRadius,
            Color fillColor,
            Color borderColor,
            int borderThickness,
            int elevationLevel,
            bool isDarkMode)
        {
            if (g == null || bounds.Width < 4 || bounds.Height < 4) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float radius = Math.Max(2f, Math.Min(cornerRadius, Math.Min(bounds.Width, bounds.Height) / 2f));

            if (elevationLevel > 0)
            {
                int lvl = Math.Clamp(elevationLevel, 1, 3);
                float scale = lvl == 1 ? 0.65f : (lvl == 2 ? 1.0f : 1.35f);
                Color shadowBase = isDarkMode ? Color.Black : Color.FromArgb(15, 23, 42);

                int a3 = Math.Clamp((int)Math.Round((isDarkMode ? 24 : 8) * scale), 1, 95);
                int a2 = Math.Clamp((int)Math.Round((isDarkMode ? 44 : 14) * scale), 1, 130);
                int a1 = Math.Clamp((int)Math.Round((isDarkMode ? 70 : 22) * scale), 1, 165);

                using (GraphicsPath p3 = CreateRoundedPath(bounds.X - 2.5f, bounds.Y + 5f, bounds.Width + 5f, bounds.Height, radius + 2f))
                using (var b3 = new SolidBrush(Color.FromArgb(a3, shadowBase)))
                {
                    g.FillPath(b3, p3);
                }

                using (GraphicsPath p2 = CreateRoundedPath(bounds.X - 1.5f, bounds.Y + 3f, bounds.Width + 3f, bounds.Height, radius + 1f))
                using (var b2 = new SolidBrush(Color.FromArgb(a2, shadowBase)))
                {
                    g.FillPath(b2, p2);
                }

                using (GraphicsPath p1 = CreateRoundedPath(bounds.X - 0.5f, bounds.Y + 1f, bounds.Width + 1f, bounds.Height, radius + 0.5f))
                using (var b1 = new SolidBrush(Color.FromArgb(a1, shadowBase)))
                {
                    g.FillPath(b1, p1);
                }
            }

            var cardRect = new RectangleF(
                bounds.X + 0.5f,
                bounds.Y + 0.5f,
                Math.Max(2f, bounds.Width - 1f),
                Math.Max(2f, bounds.Height - 1f));

            using GraphicsPath cardPath = CreateRoundedPath(cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height, radius);
            if (fillColor.A > 0)
            {
                using var fillBrush = new SolidBrush(fillColor);
                g.FillPath(fillBrush, cardPath);
            }

            if (isDarkMode && bounds.Width >= 10 && bounds.Height >= 10)
            {
                float inset = Math.Max(1.0f, borderThickness);
                float rimW = Math.Max(2f, bounds.Width - inset * 2f);
                float rimH = Math.Max(2f, bounds.Height - inset * 2f);
                float rimRadius = Math.Max(1f, radius - inset * 0.6f);
                float fadeH = Math.Max(6f, Math.Min(bounds.Height * 0.45f, radius + 6f));

                GraphicsState saved = g.Save();
                try
                {
                    g.SetClip(cardPath, CombineMode.Intersect);
                    g.SetClip(new RectangleF(bounds.X, bounds.Y, bounds.Width, fadeH), CombineMode.Intersect);

                    using GraphicsPath rimPath = CreateRoundedPath(bounds.X + inset, bounds.Y + inset, rimW, rimH, rimRadius);
                    using var rimBrush = new LinearGradientBrush(
                        new PointF(bounds.X, bounds.Y + inset),
                        new PointF(bounds.X, bounds.Y + inset + fadeH),
                        Color.FromArgb(26, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255));
                    using var rimPen = new Pen(rimBrush, 1.2f);
                    g.DrawPath(rimPen, rimPath);
                }
                finally
                {
                    g.Restore(saved);
                }
            }

            if (borderThickness > 0 && borderColor.A > 0)
            {
                using var borderPen = new Pen(borderColor, borderThickness);
                g.DrawPath(borderPen, cardPath);
            }
        }

        public class ElevatedCardPanel : Panel
        {
            private readonly Client owner;
            private int cornerRadius = 20;
            private Color cardFillColor;
            private Color cardBorderColor;
            private int cardBorderThickness = 1;
            private int elevationLevel = 2;

            public int CornerRadius
            {
                get => cornerRadius;
                set
                {
                    int safe = Math.Max(0, value);
                    if (cornerRadius != safe)
                    {
                        cornerRadius = safe;
                        Invalidate();
                    }
                }
            }

            public Color CardFillColor
            {
                get => cardFillColor;
                set
                {
                    if (cardFillColor != value)
                    {
                        cardFillColor = value;
                        Invalidate();
                    }
                }
            }

            public Color CardBorderColor
            {
                get => cardBorderColor;
                set
                {
                    if (cardBorderColor != value)
                    {
                        cardBorderColor = value;
                        Invalidate();
                    }
                }
            }

            public int CardBorderThickness
            {
                get => cardBorderThickness;
                set
                {
                    int safe = Math.Max(0, value);
                    if (cardBorderThickness != safe)
                    {
                        cardBorderThickness = safe;
                        Invalidate();
                    }
                }
            }

            public int ElevationLevel
            {
                get => elevationLevel;
                set
                {
                    int safe = Math.Clamp(value, 0, 3);
                    if (elevationLevel != safe)
                    {
                        elevationLevel = safe;
                        Invalidate();
                    }
                }
            }

            public Rectangle InnerCardBounds => new Rectangle(
                4,
                2,
                Math.Max(4, ClientSize.Width - 8),
                Math.Max(4, ClientSize.Height - 8));

            public ElevatedCardPanel(Client owner = null)
            {
                this.owner = owner;
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                DoubleBuffered = true;
                Padding = new Padding(4, 2, 4, 6);
                BackColor = owner != null ? owner.clrAppBg : Color.White;
                cardFillColor = owner != null ? owner.clrCardBg : Color.White;
                cardBorderColor = owner != null ? owner.clrBorderLight : Color.FromArgb(226, 232, 244);
                owner?.AttachEdgeResizeRedirector(this);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Invalidate();
            }

            protected override void OnPaintBackground(PaintEventArgs e)
            {
                Color backdrop = BackColor.A == 255
                    ? BackColor
                    : (owner != null
                        ? owner.clrAppBg
                        : (Parent != null && Parent.BackColor.A == 255 ? Parent.BackColor : Color.White));

                using (var bgBrush = new SolidBrush(backdrop))
                {
                    e.Graphics.FillRectangle(bgBrush, ClientRectangle);
                }

                bool dark = owner != null
                    ? owner.isDarkMode
                    : ((cardFillColor.R * 299 + cardFillColor.G * 587 + cardFillColor.B * 114) / 1000 < 90);

                PaintElevatedCardSurface(
                    e.Graphics,
                    InnerCardBounds,
                    cornerRadius,
                    cardFillColor,
                    cardBorderColor,
                    cardBorderThickness,
                    elevationLevel,
                    dark);
            }
        }

        public class AndroidOverlayScrollPanel : SmoothScrollPanel
        {
            private const int TrackWidth = 10;
            private const int TrackPadY = 6;
            private const int MinThumbHeight = 32;
            private const int IdleFadeDelayMs = 1200;

            private sealed class OverlayScrollTrack : Control
            {
                private readonly Client owner;
                private readonly AndroidOverlayScrollPanel parentScroll;

                public OverlayScrollTrack(Client owner, AndroidOverlayScrollPanel parentScroll)
                {
                    this.owner = owner;
                    this.parentScroll = parentScroll;
                    SetStyle(
                        ControlStyles.AllPaintingInWmPaint |
                        ControlStyles.UserPaint |
                        ControlStyles.OptimizedDoubleBuffer |
                        ControlStyles.ResizeRedraw,
                        true);
                    DoubleBuffered = true;
                    Dock = DockStyle.Right;
                    Width = TrackWidth;
                    TabStop = false;
                    BackColor = parentScroll.ResolveOpaqueTrackColor();
                    owner?.AttachEdgeResizeRedirector(this);
                }

                private bool IsWindowResizeEdge(Point localPt)
                {
                    if (owner == null || owner.IsDisposed || owner.WindowState != FormWindowState.Normal)
                        return false;
                    return owner.GetResizeHitTest(PointToScreen(localPt)) != 0;
                }

                protected override void OnMouseEnter(EventArgs e)
                {
                    base.OnMouseEnter(e);
                    if (IsWindowResizeEdge(PointToClient(Cursor.Position))) return;
                    parentScroll.SetTrackHover(true);
                }

                protected override void OnMouseLeave(EventArgs e)
                {
                    base.OnMouseLeave(e);
                    parentScroll.SetTrackHover(false);
                }

                protected override void OnMouseDown(MouseEventArgs e)
                {
                    base.OnMouseDown(e);
                    if (e.Button != MouseButtons.Left) return;
                    if (IsWindowResizeEdge(e.Location)) return;
                    parentScroll.HandleTrackMouseDown(e.Y);
                }

                protected override void OnMouseMove(MouseEventArgs e)
                {
                    base.OnMouseMove(e);
                    if (!parentScroll.isDraggingThumb && IsWindowResizeEdge(e.Location))
                    {
                        Cursor = Cursors.Default;
                        parentScroll.SetTrackHover(false);
                        return;
                    }
                    parentScroll.HandleTrackMouseMove(e.Y, e.Button);
                }

                protected override void OnMouseUp(MouseEventArgs e)
                {
                    base.OnMouseUp(e);
                    parentScroll.HandleTrackMouseUp();
                }

                protected override void OnMouseCaptureChanged(EventArgs e)
                {
                    base.OnMouseCaptureChanged(e);
                    if (!Capture)
                    {
                        parentScroll.HandleTrackMouseUp();
                    }
                }

                protected override void OnPaint(PaintEventArgs e)
                {
                    parentScroll.PaintOverlayThumb(e.Graphics, ClientSize);
                }
            }

            private readonly Client owner;
            private readonly Panel contentHost;
            private readonly OverlayScrollTrack scrollTrack;
            private readonly System.Windows.Forms.Timer animTimer;
            private readonly System.Windows.Forms.Timer fadeTimer;

            private int scrollOffsetY;
            private int targetOffsetY;
            private Rectangle thumbBounds = Rectangle.Empty;
            private float thumbOpacity;
            private bool centerColumnMode;
            private int maxContentWidth = MaxColWidth;
            private bool isTrackHovered;
            private bool isDraggingThumb;
            private int dragStartMouseY;
            private int dragStartScrollY;
            private long lastActivityTick;
            private bool wasScrollable;
            private bool syncingLayout;
            private bool timersDisposed;

            private bool IsTearingDown => IsDisposed || Disposing || timersDisposed;

            public Panel ContentHost => contentHost;
            public int ScrollOffsetY => scrollOffsetY;

            public int MaxScrollOffset
            {
                get
                {
                    int vh = ClientSize.Height;
                    if (vh <= 0 || contentHost == null || contentHost.IsDisposed) return 0;
                    return Math.Max(0, contentHost.Height - vh);
                }
            }

            public bool IsScrollable => MaxScrollOffset > 0;
            public Rectangle ThumbBounds => thumbBounds;
            public float ThumbOpacity => thumbOpacity;

            public bool CenterColumnMode
            {
                get => centerColumnMode;
                set
                {
                    if (centerColumnMode != value)
                    {
                        centerColumnMode = value;
                        SyncLayout();
                    }
                }
            }

            public int MaxContentWidth
            {
                get => maxContentWidth;
                set
                {
                    int safe = Math.Max(260, value);
                    if (maxContentWidth != safe)
                    {
                        maxContentWidth = safe;
                        SyncLayout();
                    }
                }
            }

            public AndroidOverlayScrollPanel(Client owner = null)
            {
                this.owner = owner;
                base.AutoScroll = false;
                BackColor = owner != null ? owner.clrAppBg : Color.White;

                contentHost = new Panel
                {
                    Location = new Point(14, 0),
                    Size = new Size(DefaultColWidth, 100),
                    BackColor = Color.Transparent
                };

                scrollTrack = new OverlayScrollTrack(owner, this);

                Controls.Add(contentHost);
                Controls.Add(scrollTrack);
                scrollTrack.BringToFront();

                animTimer = new System.Windows.Forms.Timer { Interval = 16 };
                animTimer.Tick += OnAnimTimerTick;

                fadeTimer = new System.Windows.Forms.Timer { Interval = 40 };
                fadeTimer.Tick += OnFadeTimerTick;

                owner?.AttachEdgeResizeRedirector(this);
                owner?.AttachEdgeResizeRedirector(contentHost);

                Resize += (s, e) => SyncLayout();
                Layout += (s, e) => SyncLayout();
                HandleCreated += (s, e) =>
                {
                    SyncLayout();
                    if (!IsTearingDown && IsScrollable && thumbOpacity > 0f && !fadeTimer.Enabled && !isTrackHovered && !isDraggingThumb)
                    {
                        lastActivityTick = Environment.TickCount64;
                        fadeTimer.Start();
                    }
                };
                contentHost.SizeChanged += (s, e) =>
                {
                    if (!syncingLayout) SyncLayout();
                };
            }

            private Color ResolveOpaqueTrackColor()
            {
                if (BackColor.A == 255) return BackColor;
                if (owner != null) return owner.clrAppBg;
                if (Parent != null && Parent.BackColor.A == 255) return Parent.BackColor;
                return Color.White;
            }

            private void SyncTrackBackColor()
            {
                if (scrollTrack == null || scrollTrack.IsDisposed) return;
                Color target = ResolveOpaqueTrackColor();
                if (scrollTrack.BackColor.ToArgb() != target.ToArgb())
                {
                    scrollTrack.BackColor = target;
                }
                scrollTrack.Invalidate();
            }

            protected override void OnBackColorChanged(EventArgs e)
            {
                base.OnBackColorChanged(e);
                SyncTrackBackColor();
            }

            public void SyncTheme()
            {
                SyncTrackBackColor();
            }

            public void SyncLayout()
            {
                if (syncingLayout || IsTearingDown || contentHost == null || contentHost.IsDisposed)
                    return;

                int availW = ClientSize.Width;
                if (availW <= 0) return;

                syncingLayout = true;
                try
                {
                    int targetW;
                    int targetX;
                    if (centerColumnMode)
                    {
                        int usableW = Math.Max(260, availW - TrackWidth);
                        int sidePad = usableW < 450 ? 10 : 14;
                        int maxW = Math.Max(260, maxContentWidth);
                        targetW = Math.Clamp(usableW - sidePad * 2, 260, maxW);
                        targetX = Math.Max(sidePad, (usableW - targetW) / 2);
                    }
                    else
                    {
                        targetW = Math.Max(100, availW - 12);
                        targetX = 0;
                    }

                    if (contentHost.Width != targetW)
                    {
                        contentHost.Width = targetW;
                    }

                    int maxOff = MaxScrollOffset;
                    scrollOffsetY = Math.Clamp(scrollOffsetY, 0, maxOff);
                    targetOffsetY = Math.Clamp(targetOffsetY, 0, maxOff);

                    int targetY = -scrollOffsetY;
                    if (contentHost.Left != targetX || contentHost.Top != targetY)
                    {
                        contentHost.Location = new Point(targetX, targetY);
                    }

                    UpdateThumbMetrics();
                }
                finally
                {
                    syncingLayout = false;
                }
            }

            public void SetScrollOffset(int y, bool animate = false)
            {
                if (IsTearingDown || contentHost == null || contentHost.IsDisposed) return;

                int maxOff = MaxScrollOffset;
                int clamped = Math.Clamp(y, 0, maxOff);

                if (maxOff > 0)
                {
                    WakeScrollbar(1.0f);
                }

                if (!animate || !IsHandleCreated || Math.Abs(clamped - scrollOffsetY) <= 1)
                {
                    animTimer.Stop();
                    targetOffsetY = clamped;
                    ApplyScrollOffsetImmediate(clamped);
                    return;
                }

                targetOffsetY = clamped;
                int delta = targetOffsetY - scrollOffsetY;
                int step = (int)Math.Round(delta * 0.35f);
                if (step == 0) step = Math.Sign(delta);
                ApplyScrollOffsetImmediate(Math.Clamp(scrollOffsetY + step, 0, maxOff));
                if (scrollOffsetY != targetOffsetY)
                {
                    animTimer.Start();
                }
            }

            public void ScrollByDelta(int deltaY, bool animate = true)
            {
                if (deltaY == 0 || !IsScrollable) return;
                int baseY = animate ? targetOffsetY : scrollOffsetY;
                SetScrollOffset(baseY + deltaY, animate);
            }

            public void ScrollControlToVisible(Control target)
            {
                if (target == null || target.IsDisposed || contentHost == null || contentHost.IsDisposed || !IsScrollable)
                    return;

                int relY = 0;
                Control cur = target;
                bool insideHost = false;
                while (cur != null)
                {
                    if (ReferenceEquals(cur, contentHost))
                    {
                        insideHost = true;
                        break;
                    }
                    relY += cur.Top;
                    cur = cur.Parent;
                }
                if (!insideHost) return;

                int viewH = ClientSize.Height;
                if (viewH <= 0) return;

                const int pad = 12;
                int itemTop = relY;
                int itemBottom = relY + target.Height;

                if (itemTop < scrollOffsetY + pad)
                {
                    SetScrollOffset(itemTop - pad, animate: false);
                }
                else if (itemBottom > scrollOffsetY + viewH - pad)
                {
                    SetScrollOffset(itemBottom - viewH + pad, animate: false);
                }
            }

            private void ApplyScrollOffsetImmediate(int newOffset)
            {
                scrollOffsetY = Math.Clamp(newOffset, 0, MaxScrollOffset);
                if (contentHost != null && !contentHost.IsDisposed && contentHost.Top != -scrollOffsetY)
                {
                    contentHost.Top = -scrollOffsetY;
                }
                UpdateThumbMetrics();
            }

            private void UpdateThumbMetrics()
            {
                if (IsTearingDown || contentHost == null || contentHost.IsDisposed)
                    return;

                int maxOff = MaxScrollOffset;
                if (maxOff <= 0 || ClientSize.Height <= 0)
                {
                    thumbBounds = Rectangle.Empty;
                    wasScrollable = false;
                    thumbOpacity = 0f;
                    animTimer.Stop();
                    fadeTimer.Stop();
                    if (scrollTrack != null && !scrollTrack.IsDisposed)
                    {
                        scrollTrack.Cursor = Cursors.Default;
                        scrollTrack.Invalidate();
                    }
                    return;
                }

                if (!wasScrollable)
                {
                    wasScrollable = true;
                    WakeScrollbar(0.75f);
                }
                else if (IsHandleCreated && thumbOpacity > 0f && !fadeTimer.Enabled && !isTrackHovered && !isDraggingThumb)
                {
                    lastActivityTick = Environment.TickCount64;
                    fadeTimer.Start();
                }

                int trackH = (scrollTrack != null && !scrollTrack.IsDisposed && scrollTrack.Height > 0)
                    ? scrollTrack.Height
                    : ClientSize.Height;
                int usableTrackH = Math.Max(20, trackH - TrackPadY * 2);
                float viewRatio = (float)ClientSize.Height / Math.Max(1, contentHost.Height);
                int thumbH = Math.Clamp((int)Math.Round(usableTrackH * viewRatio), Math.Min(MinThumbHeight, usableTrackH), usableTrackH);
                float progress = (float)scrollOffsetY / maxOff;
                int thumbY = TrackPadY + (int)Math.Round((usableTrackH - thumbH) * progress);

                int thumbW = (isTrackHovered || isDraggingThumb) ? 6 : 4;
                int trackLeft = Math.Max(0, ClientSize.Width - TrackWidth);
                int localX = (TrackWidth - thumbW) / 2;
                thumbBounds = new Rectangle(trackLeft + localX, thumbY, thumbW, thumbH);

                if (scrollTrack != null && !scrollTrack.IsDisposed)
                {
                    scrollTrack.Invalidate();
                }
            }

            private void WakeScrollbar(float opacity = 1.0f)
            {
                if (IsTearingDown || !IsScrollable) return;
                thumbOpacity = Math.Max(thumbOpacity, Math.Clamp(opacity, 0f, 1f));
                lastActivityTick = Environment.TickCount64;
                if (!fadeTimer.Enabled && IsHandleCreated)
                {
                    fadeTimer.Start();
                }
                if (scrollTrack != null && !scrollTrack.IsDisposed)
                {
                    scrollTrack.Invalidate();
                }
            }

            private void OnAnimTimerTick(object sender, EventArgs e)
            {
                if (IsTearingDown || !IsScrollable)
                {
                    if (!timersDisposed) animTimer.Stop();
                    return;
                }

                int diff = targetOffsetY - scrollOffsetY;
                if (Math.Abs(diff) <= 1)
                {
                    animTimer.Stop();
                    ApplyScrollOffsetImmediate(targetOffsetY);
                    return;
                }

                int step = (int)Math.Round(diff * 0.32f);
                if (step == 0) step = Math.Sign(diff);
                ApplyScrollOffsetImmediate(scrollOffsetY + step);
            }

            private void OnFadeTimerTick(object sender, EventArgs e)
            {
                if (IsTearingDown || !IsScrollable)
                {
                    if (!timersDisposed) fadeTimer.Stop();
                    return;
                }

                if (isTrackHovered || isDraggingThumb || animTimer.Enabled)
                {
                    lastActivityTick = Environment.TickCount64;
                    if (thumbOpacity < 1.0f)
                    {
                        thumbOpacity = 1.0f;
                        scrollTrack?.Invalidate();
                    }
                    return;
                }

                if (Environment.TickCount64 - lastActivityTick < IdleFadeDelayMs)
                    return;

                thumbOpacity = Math.Max(0f, thumbOpacity - 0.08f);
                if (thumbOpacity <= 0.01f)
                {
                    thumbOpacity = 0f;
                    fadeTimer.Stop();
                }
                scrollTrack?.Invalidate();
            }

            private void SetTrackHover(bool hovered)
            {
                if (IsTearingDown || isTrackHovered == hovered) return;
                isTrackHovered = hovered;
                if (scrollTrack != null && !scrollTrack.IsDisposed)
                {
                    scrollTrack.Cursor = (hovered && IsScrollable) ? Cursors.Hand : Cursors.Default;
                }
                if (hovered && IsScrollable)
                {
                    WakeScrollbar(1.0f);
                }
                else
                {
                    lastActivityTick = Environment.TickCount64;
                }
                UpdateThumbMetrics();
            }

            private void HandleTrackMouseDown(int mouseY)
            {
                if (IsTearingDown || !IsScrollable) return;
                WakeScrollbar(1.0f);

                if (mouseY >= thumbBounds.Top - 2 && mouseY <= thumbBounds.Bottom + 2)
                {
                    isDraggingThumb = true;
                    dragStartMouseY = mouseY;
                    dragStartScrollY = scrollOffsetY;
                    animTimer.Stop();
                    if (scrollTrack != null && !scrollTrack.IsDisposed)
                    {
                        scrollTrack.Capture = true;
                    }
                    UpdateThumbMetrics();
                    return;
                }

                int trackH = Math.Max(20, (scrollTrack?.Height ?? ClientSize.Height) - TrackPadY * 2);
                int travel = Math.Max(1, trackH - thumbBounds.Height);
                float ratio = Math.Clamp((mouseY - TrackPadY - thumbBounds.Height / 2f) / travel, 0f, 1f);
                SetScrollOffset((int)Math.Round(ratio * MaxScrollOffset), animate: true);
            }

            private void HandleTrackMouseMove(int mouseY, MouseButtons buttons)
            {
                if (IsTearingDown) return;
                if (isDraggingThumb && buttons == MouseButtons.Left && IsScrollable)
                {
                    int trackH = Math.Max(20, (scrollTrack?.Height ?? ClientSize.Height) - TrackPadY * 2);
                    int travel = Math.Max(1, trackH - thumbBounds.Height);
                    int dy = mouseY - dragStartMouseY;
                    int nextOffset = dragStartScrollY + (int)Math.Round((double)dy * MaxScrollOffset / travel);
                    SetScrollOffset(nextOffset, animate: false);
                    return;
                }

                SetTrackHover(true);
            }

            private void HandleTrackMouseUp()
            {
                if (!isDraggingThumb) return;
                isDraggingThumb = false;
                if (IsTearingDown) return;
                if (scrollTrack != null && !scrollTrack.IsDisposed && scrollTrack.Capture)
                {
                    scrollTrack.Capture = false;
                }
                lastActivityTick = Environment.TickCount64;
                UpdateThumbMetrics();
            }

            private void PaintOverlayThumb(Graphics g, Size trackSize)
            {
                if (!IsScrollable || thumbOpacity <= 0.01f || thumbBounds.Height <= 0 || trackSize.Width <= 0)
                    return;

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                int thumbW = (isTrackHovered || isDraggingThumb) ? 6 : 4;
                float x = (trackSize.Width - thumbW) / 2f;
                float y = thumbBounds.Y;
                float h = thumbBounds.Height;

                bool dark = owner != null && owner.isDarkMode;
                Color baseClr = dark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
                int maxAlpha = dark
                    ? (isDraggingThumb ? 215 : (isTrackHovered ? 185 : 135))
                    : (isDraggingThumb ? 200 : (isTrackHovered ? 170 : 120));
                int alpha = Math.Clamp((int)Math.Round(maxAlpha * thumbOpacity), 0, 255);
                if (alpha <= 0) return;

                using GraphicsPath pill = CreateRoundedPath(x, y, thumbW, h, thumbW / 2f);
                using var brush = new SolidBrush(Color.FromArgb(alpha, baseClr));
                g.FillPath(brush, pill);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !timersDisposed)
                {
                    timersDisposed = true;
                    try { animTimer.Stop(); animTimer.Dispose(); } catch { }
                    try { fadeTimer.Stop(); fadeTimer.Dispose(); } catch { }
                }
                base.Dispose(disposing);
            }
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

                int innerW = Math.Max(40, w - padEdge * 2);
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
                    int maxRightW = w < 208
                        ? Math.Max(48, (innerW - pillGap) / 2)
                        : Math.Max(88, (int)(w * 0.44f));
                    int minRightW = Math.Min(72, maxRightW);
                    rightPillW = Math.Clamp((int)Math.Ceiling(tagSz.Width) + rightOverhead, minRightW, maxRightW);
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
                int rawLeftAvail = rightPillW > 0
                    ? (innerW - rightPillW - pillGap)
                    : innerW;
                int availLeftW = w < 208
                    ? Math.Max(40, rawLeftAvail)
                    : Math.Max(88, rawLeftAvail);
                int minLeftW = Math.Min(76, availLeftW);

                SizeF catSz = g.MeasureString(catLabel, pillFont, availLeftW, fmt);
                int leftPillW = Math.Clamp((int)Math.Ceiling(catSz.Width) + leftOverhead, minLeftW, availLeftW);
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
                Color accentInk = (owner != null && owner.isDarkMode) ? p.AccentSecondary : p.AccentPrimary;
                var circleRect = new RectangleF(sx + 12f, 10f, 48f, 48f);
                using (var fill = new SolidBrush(p.SoftAccentBg))
                using (var dashedPen = new Pen(accentInk, 2f) { DashStyle = DashStyle.Dash })
                {
                    g.FillEllipse(fill, circleRect);
                    g.DrawEllipse(dashedPen, circleRect);
                }

                if (owner != null)
                {
                    using Bitmap ico = owner.CreateVectorIcon(UiIcon.UserPlus, accentInk, 20);
                    g.DrawImage(ico, circleRect.X + 14f, circleRect.Y + 14f, 20f, 20f);
                }
                else
                {
                    using var plusBrush = new SolidBrush(accentInk);
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

                using var textBrush = new SolidBrush(accentInk);
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

                uint tintIdx = unchecked((uint)friend.UserId) % (uint)AvatarTints.Length;
                Color tint = AvatarTints[tintIdx];
                Color avatarFill = friend.IsOnline ? tint : BlendControlColor(tint, p.TextMuted, 0.65f);
                using (var avatarBrush = new SolidBrush(avatarFill))
                {
                    g.FillEllipse(avatarBrush, avatarRect);
                }

                string cleanName = (friend.Name ?? "").Trim();
                string initial = GetAvatarInitial(cleanName);
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
