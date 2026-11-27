using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PocketLog.Client
{
    public partial class Client
    {
        protected sealed class AppPalette
        {
            public Color AppBg { get; init; }
            public Color CardBg { get; init; }
            public Color InputBg { get; init; }
            public Color FloatingNavBg { get; init; }
            public Color BorderLight { get; init; }
            public Color BorderWindow { get; init; }
            public Color AccentPrimary { get; init; }
            public Color AccentSecondary { get; init; }
            public Color SoftAccentBg { get; init; }
            public Color SoftAccentBorder { get; init; }
            public Color TextMain { get; init; }
            public Color TextMuted { get; init; }
            public Color TextSecondary { get; init; }
            public Color Danger { get; init; }
            public Color SoftDangerBg { get; init; }
            public Color Success { get; init; }
            public Color StoryRingStart { get; init; }
            public Color StoryRingMid { get; init; }
            public Color StoryRingEnd { get; init; }
            public Color WalletGradStart { get; init; }
            public Color WalletGradEnd { get; init; }
            public Color HeroGradStart { get; init; }
            public Color HeroGradMid { get; init; }
            public Color HeroGradEnd { get; init; }
            public Color DrawerBackdrop { get; init; }
            public Color ScrimTop { get; init; }
            public Color ScrimBottom { get; init; }
        }

        internal sealed class CameraDraftState : IDisposable
        {
            public Image UploadedPhoto { get; set; }
            public string SelectedCategory { get; set; }
            public List<string> TaggedFriends { get; set; } = new List<string>();
            public string NoteText { get; set; } = "";
            public string AmountText { get; set; } = "";
            public string CaptionText { get; set; } = "";

            public void Dispose()
            {
                try { UploadedPhoto?.Dispose(); } catch { }
                UploadedPhoto = null;
            }
        }

        private static readonly AppPalette LightPalette = new AppPalette
        {
            AppBg = Color.FromArgb(244, 246, 251),
            CardBg = Color.FromArgb(255, 255, 255),
            InputBg = Color.FromArgb(238, 242, 249),
            FloatingNavBg = Color.FromArgb(255, 255, 255),
            BorderLight = Color.FromArgb(226, 232, 244),
            BorderWindow = Color.FromArgb(214, 223, 238),
            AccentPrimary = Color.FromArgb(79, 70, 229),
            AccentSecondary = Color.FromArgb(14, 165, 233),
            SoftAccentBg = Color.FromArgb(238, 242, 255),
            SoftAccentBorder = Color.FromArgb(199, 210, 254),
            TextMain = Color.FromArgb(15, 23, 42),
            TextMuted = Color.FromArgb(100, 116, 139),
            TextSecondary = Color.FromArgb(51, 65, 85),
            Danger = Color.FromArgb(244, 63, 94),
            SoftDangerBg = Color.FromArgb(255, 228, 235),
            Success = Color.FromArgb(16, 185, 129),
            StoryRingStart = Color.FromArgb(245, 158, 11),
            StoryRingMid = Color.FromArgb(236, 72, 153),
            StoryRingEnd = Color.FromArgb(99, 102, 241),
            WalletGradStart = Color.FromArgb(49, 46, 129),
            WalletGradEnd = Color.FromArgb(67, 56, 202),
            HeroGradStart = Color.FromArgb(79, 70, 229),
            HeroGradMid = Color.FromArgb(124, 58, 237),
            HeroGradEnd = Color.FromArgb(14, 165, 233),
            DrawerBackdrop = Color.FromArgb(215, 224, 234),
            ScrimTop = Color.FromArgb(0, 15, 23, 42),
            ScrimBottom = Color.FromArgb(185, 15, 23, 42)
        };

        private static readonly AppPalette DarkPalette = new AppPalette
        {
            AppBg = Color.FromArgb(11, 14, 20),
            CardBg = Color.FromArgb(22, 27, 38),
            InputBg = Color.FromArgb(30, 37, 52),
            FloatingNavBg = Color.FromArgb(26, 32, 46),
            BorderLight = Color.FromArgb(42, 51, 70),
            BorderWindow = Color.FromArgb(40, 49, 68),
            AccentPrimary = Color.FromArgb(99, 102, 241),
            AccentSecondary = Color.FromArgb(56, 189, 248),
            SoftAccentBg = Color.FromArgb(35, 43, 69),
            SoftAccentBorder = Color.FromArgb(79, 70, 229),
            TextMain = Color.FromArgb(241, 245, 249),
            TextMuted = Color.FromArgb(148, 163, 184),
            TextSecondary = Color.FromArgb(203, 213, 225),
            Danger = Color.FromArgb(251, 113, 133),
            SoftDangerBg = Color.FromArgb(62, 24, 38),
            Success = Color.FromArgb(52, 211, 153),
            StoryRingStart = Color.FromArgb(251, 191, 36),
            StoryRingMid = Color.FromArgb(244, 114, 182),
            StoryRingEnd = Color.FromArgb(129, 140, 248),
            WalletGradStart = Color.FromArgb(30, 27, 75),
            WalletGradEnd = Color.FromArgb(55, 48, 163),
            HeroGradStart = Color.FromArgb(55, 48, 163),
            HeroGradMid = Color.FromArgb(109, 40, 217),
            HeroGradEnd = Color.FromArgb(2, 132, 199),
            DrawerBackdrop = Color.FromArgb(6, 8, 12),
            ScrimTop = Color.FromArgb(0, 11, 14, 20),
            ScrimBottom = Color.FromArgb(190, 11, 14, 20)
        };

        protected bool isDarkMode;
        protected AppPalette CurrentTheme { get; private set; } = LightPalette;

        // Hooks populated by Auth (Task 3) & Camera (Task 5) screens to preserve unsaved inputs across theme switches
        private Func<(string Ip, string Port, string User, string Pass, bool ShowPass, bool IsRegister, string Budget)> authDraftCapture;
        private Action<(string Ip, string Port, string User, string Pass, bool ShowPass, bool IsRegister, string Budget)> authDraftRestore;
        private Func<CameraDraftState> cameraDraftCapture;
        private Action<CameraDraftState> cameraDraftRestore;

        private static string GetThemePreferencePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PocketLog");
            return Path.Combine(dir, "theme_mode.txt");
        }

        protected void LoadThemePreference()
        {
            try
            {
                string path = GetThemePreferencePath();
                if (File.Exists(path))
                {
                    string mode = File.ReadAllText(path).Trim();
                    isDarkMode = mode.Equals("dark", StringComparison.OrdinalIgnoreCase);
                    return;
                }
            }
            catch { }
            isDarkMode = false;
        }

        protected void SaveThemePreference(bool dark)
        {
            isDarkMode = dark;
            try
            {
                string path = GetThemePreferencePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, dark ? "dark" : "light");
            }
            catch { }
        }

        protected void ApplyThemeColors()
        {
            AppPalette p = isDarkMode ? DarkPalette : LightPalette;
            CurrentTheme = p;

            clrAppBg = p.AppBg;
            clrCardBg = p.CardBg;
            clrInputBg = p.InputBg;
            clrPrimaryGreen = p.AccentPrimary;
            clrAccentSecondary = p.AccentSecondary;
            clrSoftGreenBg = p.SoftAccentBg;
            clrSoftGreenBorder = p.SoftAccentBorder;
            clrIndigo = p.TextSecondary;
            clrSoftIndigo = p.BorderLight;
            clrRedDanger = p.Danger;
            clrSoftRedBg = p.SoftDangerBg;
            clrBorderLight = p.BorderLight;
            clrTextMain = p.TextMain;
            clrTextMuted = p.TextMuted;
        }

        protected int GetUnpaidSplitCount()
        {
            return taggedDebts.Count(d => !d.IsConfirmedPaid);
        }

        protected void ApplyShellTheme()
        {
            AppPalette p = CurrentTheme ?? LightPalette;
            this.BackColor = p.BorderWindow;

            if (pnlMainContainer != null && !pnlMainContainer.IsDisposed)
            {
                pnlMainContainer.FillColor = clrAppBg;
                pnlMainContainer.BackColor = clrAppBg;
            }

            if (pnlHeader != null && !pnlHeader.IsDisposed)
            {
                pnlHeader.FillColor = clrCardBg;
            }
            if (pnlHeaderDivider != null && !pnlHeaderDivider.IsDisposed)
            {
                pnlHeaderDivider.BackColor = clrBorderLight;
            }

            if (pnlLogoBadge != null && !pnlLogoBadge.IsDisposed)
            {
                pnlLogoBadge.FillColor = p.AccentPrimary;
                pnlLogoBadge.FillColor2 = p.AccentSecondary;
            }
            if (picLogoIcon != null && !picLogoIcon.IsDisposed)
            {
                Image oldLogo = picLogoIcon.Image;
                picLogoIcon.Image = CreateVectorIcon(UiIcon.Brand, Color.White, 18);
                oldLogo?.Dispose();
            }
            if (lblBrand != null && !lblBrand.IsDisposed)
            {
                lblBrand.ForeColor = clrTextMain;
            }

            if (pnlHeaderUserChip != null && !pnlHeaderUserChip.IsDisposed)
            {
                pnlHeaderUserChip.FillColor = clrSoftGreenBg;
            }
            if (avtHeaderUser != null && !avtHeaderUser.IsDisposed)
            {
                avtHeaderUser.FillColor = clrPrimaryGreen;
                avtHeaderUser.ForeColor = clrPrimaryGreen;
                avtHeaderUser.HoverState.FillColor = clrPrimaryGreen;
                avtHeaderUser.DisabledState.FillColor = clrPrimaryGreen;
                avtHeaderUser.DisabledState.ForeColor = clrPrimaryGreen;
                avtHeaderUser.Invalidate();
            }
            if (lblHeaderUser != null && !lblHeaderUser.IsDisposed)
            {
                lblHeaderUser.ForeColor = clrPrimaryGreen;
            }

            if (btnLogoutTop != null && !btnLogoutTop.IsDisposed)
            {
                btnLogoutTop.FillColor = clrAppBg;
                btnLogoutTop.ForeColor = clrIndigo;
                btnLogoutTop.HoverState.FillColor = clrSoftRedBg;
                btnLogoutTop.HoverState.ForeColor = clrRedDanger;
                btnLogoutTop.DisabledState.FillColor = clrAppBg;
                btnLogoutTop.DisabledState.ForeColor = clrTextMuted;
                btnLogoutTop.DisabledState.BorderColor = clrBorderLight;
            }

            if (btnThemeToggle != null && !btnThemeToggle.IsDisposed)
            {
                btnThemeToggle.FillColor = clrAppBg;
                btnThemeToggle.BorderColor = clrBorderLight;
                btnThemeToggle.BorderThickness = 1;
                btnThemeToggle.HoverState.FillColor = clrSoftGreenBg;
                btnThemeToggle.HoverState.BorderColor = clrSoftGreenBorder;
                btnThemeToggle.DisabledState.FillColor = clrAppBg;
                btnThemeToggle.DisabledState.BorderColor = clrBorderLight;

                Image oldThemeImg = btnThemeToggle.Image;
                Color icoClr = isDarkMode ? Color.FromArgb(251, 191, 36) : clrIndigo;
                btnThemeToggle.Image = CreateVectorIcon(isDarkMode ? UiIcon.Sun : UiIcon.Moon, icoClr, 17);
                btnThemeToggle.ImageSize = new Size(17, 17);
                btnThemeToggle.ImageAlign = HorizontalAlignment.Center;
                btnThemeToggle.ImageOffset = Point.Empty;
                btnThemeToggle.TextOffset = Point.Empty;
                oldThemeImg?.Dispose();

                uiToolTip.SetToolTip(
                    btnThemeToggle,
                    isDarkMode ? "Chuyển sang giao diện Sáng (Daylight)" : "Chuyển sang giao diện Tối (Midnight)");
            }

            if (btnWinMin != null && !btnWinMin.IsDisposed)
            {
                btnWinMin.FillColor = clrAppBg;
                btnWinMin.IconColor = clrTextMain;
                btnWinMin.HoverState.FillColor = clrBorderLight;
                btnWinMin.HoverState.IconColor = clrTextMain;
            }
            if (btnWinMax != null && !btnWinMax.IsDisposed)
            {
                btnWinMax.FillColor = clrAppBg;
                btnWinMax.IconColor = clrTextMain;
                btnWinMax.HoverState.FillColor = clrBorderLight;
                btnWinMax.HoverState.IconColor = clrTextMain;
            }
            if (btnWinClose != null && !btnWinClose.IsDisposed)
            {
                btnWinClose.FillColor = clrAppBg;
                btnWinClose.IconColor = clrTextMain;
                btnWinClose.HoverState.FillColor = clrRedDanger;
                btnWinClose.HoverState.IconColor = Color.White;
            }

            if (pnlBottomNav != null && !pnlBottomNav.IsDisposed)
            {
                pnlBottomNav.FillColor = clrAppBg;
                pnlBottomNav.BackColor = clrAppBg;
                pnlBottomNav.Invalidate();
            }
            UpdateBottomTabIcons();

            if (pnlDrawerBackdrop != null && !pnlDrawerBackdrop.IsDisposed)
            {
                pnlDrawerBackdrop.BackColor = p.DrawerBackdrop;
            }
            if (pnlFriendsDrawer != null && !pnlFriendsDrawer.IsDisposed)
            {
                pnlFriendsDrawer.FillColor = clrCardBg;
                pnlFriendsDrawer.BorderColor = clrBorderLight;
            }
            if (pnlDrawerBackdrop != null && !pnlDrawerBackdrop.IsDisposed && pnlDrawerBackdrop.Visible)
            {
                RenderFriendsDrawerContent(currentFriendsSearchKeyword);
            }
        }

        protected void ToggleThemeMode()
        {
            SaveThemePreference(!isDarkMode);
            ApplyThemeColors();
            ApplyShellTheme();

            if (activeTabName == "AUTH" && activeTabs.Count == 0)
            {
                var authDraft = authDraftCapture?.Invoke();
                ShowAuthScreen();
                if (authDraft.HasValue)
                {
                    authDraftRestore?.Invoke(authDraft.Value);
                }
                return;
            }

            CameraDraftState camDraft = null;
            if (IsTabActive("CAMERA"))
            {
                camDraft = cameraDraftCapture?.Invoke();
            }

            try
            {
                foreach (string tabKey in activeTabs.ToList())
                {
                    if (paneHosts.TryGetValue(tabKey, out Panel host) && host != null && !host.IsDisposed)
                    {
                        host.BackColor = clrAppBg;
                    }
                    RefreshPaneContent(tabKey);
                }

                if (pnlMainContainer != null && !pnlMainContainer.IsDisposed)
                {
                    foreach (Control ctrl in pnlMainContainer.Controls)
                    {
                        if (ctrl is TableLayoutPanel splitGrid)
                        {
                            splitGrid.BackColor = clrAppBg;
                            foreach (Control child in splitGrid.Controls)
                            {
                                if (!paneHosts.ContainsValue(child as Panel))
                                {
                                    child.BackColor = clrBorderLight;
                                }
                            }
                        }
                    }
                }

                if (camDraft != null && IsTabActive("CAMERA"))
                {
                    cameraDraftRestore?.Invoke(camDraft);
                }
            }
            finally
            {
                camDraft?.Dispose();
            }
        }
    }
}
