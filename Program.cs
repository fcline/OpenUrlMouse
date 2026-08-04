using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OpenUrlHotkey
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }

    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem autorunMenuItem;
        private ToolStripMenuItem presetsSubMenu;
        private ToolStripMenuItem langSubMenu;
        private GlobalKeyboardHook keyboardHook;
        private string configPath;
        private AppConfig config;

        private const string AppName = "OpenUrlHotkey";
        private const string RunRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public TrayApplicationContext()
        {
            LoadConfig();
            InitTrayMenu();
            InitKeyboardHook();
            TrimMemory();

            ShowStartBalloon();
        }

        private void ShowStartBalloon()
        {
            if (config.Language == "en")
            {
                trayIcon.ShowBalloonTip(3000, "OpenUrlHotkey Started",
                    string.Format("Hotkey: {0}\nURL: {1}", config.HotkeyDisplay, config.Url), ToolTipIcon.Info);
            }
            else
            {
                trayIcon.ShowBalloonTip(3000, "OpenUrlHotkey запущен",
                    string.Format("Горячие клавиши: {0}\nСайт: {1}", config.HotkeyDisplay, config.Url), ToolTipIcon.Info);
            }
        }

        private void LoadConfig()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, AppName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            configPath = Path.Combine(folder, "config.json");

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    config = SimpleJson.Deserialize(json);
                    if (config == null) config = new AppConfig();
                }
                catch
                {
                    config = new AppConfig();
                }
            }
            else
            {
                config = new AppConfig();
                SaveConfig();
            }

            // Ensure valid language (if empty or invalid, auto-detect Windows OS UI language)
            if (string.IsNullOrEmpty(config.Language))
            {
                config.Language = AppConfig.DetectSystemLanguage();
                SaveConfig();
            }

            bool regAutorun = IsAutorunEnabledInRegistry();
            if (config.Autorun != regAutorun)
            {
                SetAutorunRegistry(config.Autorun);
            }
        }

        private void SaveConfig()
        {
            try
            {
                string json = SimpleJson.Serialize(config);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving config: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitTrayMenu()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            BuildTrayMenu();

            trayIcon = new NotifyIcon
            {
                Icon = LoadAppIcon(),
                ContextMenuStrip = trayMenu,
                Text = string.Format("OpenUrlHotkey ({0})", config.HotkeyDisplay),
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => OpenTargetUrl();
        }

        private Icon LoadAppIcon()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidatePaths = new string[]
                {
                    Path.Combine(baseDir, @"assets\icons\512.png"),
                    Path.Combine(baseDir, @"assets\icons\24.png"),
                    Path.Combine(baseDir, @"assets\icons\icon.png"),
                    @"assets\icons\512.png",
                    @"assets\icons\24.png",
                    @"assets\icons\icon.png"
                };

                string iconPath = null;
                foreach (string p in candidatePaths)
                {
                    if (File.Exists(p))
                    {
                        iconPath = p;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(iconPath))
                {
                    using (Image img = Image.FromFile(iconPath))
                    using (Bitmap bmp = new Bitmap(32, 32))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(img, 0, 0, 32, 32);
                        IntPtr hIcon = bmp.GetHicon();
                        return Icon.FromHandle(hIcon);
                    }
                }
            }
            catch
            {
            }

            return CreateFallbackIcon();
        }

        private Icon CreateFallbackIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (Brush b = new LinearGradientBrush(new Rectangle(0, 0, 32, 32),
                    Color.FromArgb(0, 122, 255), Color.FromArgb(0, 80, 200), 45f))
                {
                    g.FillEllipse(b, 2, 2, 28, 28);
                }

                using (Pen p = new Pen(Color.White, 2f))
                {
                    g.DrawEllipse(p, 6, 6, 20, 20);
                    g.DrawLine(p, 6, 16, 26, 16);
                    g.DrawArc(p, 10, 6, 12, 20, -90, 180);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        private void BuildTrayMenu()
        {
            if (trayMenu == null) return;
            trayMenu.Items.Clear();

            bool isEn = config.Language == "en";

            // Title
            var titleItem = new ToolStripMenuItem(string.Format("OpenUrlHotkey ({0})", config.HotkeyDisplay));
            titleItem.Enabled = false;
            titleItem.Font = new Font(trayMenu.Font.FontFamily, 9.5f, FontStyle.Bold);
            titleItem.ForeColor = Color.DarkSlateBlue;
            trayMenu.Items.Add(titleItem);
            trayMenu.Items.Add(new ToolStripSeparator());

            // Open Site
            string openText = isEn ? "🌐 Open Website" : "🌐 Открыть сайт";
            var openItem = new ToolStripMenuItem(openText, null, (s, e) => OpenTargetUrl());
            openItem.Font = new Font(trayMenu.Font.FontFamily, 9.5f, FontStyle.Bold);
            trayMenu.Items.Add(openItem);
            trayMenu.Items.Add(new ToolStripSeparator());

            // Change URL
            string urlText = isEn ? "🔗 Change Website URL..." : "🔗 Изменить URL...";
            var urlItem = new ToolStripMenuItem(urlText, null, (s, e) => ChangeUrlDialog());
            trayMenu.Items.Add(urlItem);

            // Record Hotkey
            string recordText = isEn ? "🎯 Record New Hotkey..." : "🎯 Записать новый хоткей...";
            var recordItem = new ToolStripMenuItem(recordText, null, (s, e) => RecordHotkeyFromTray());
            trayMenu.Items.Add(recordItem);

            // Presets Submenu
            string presetsText = isEn ? "⌨️ Quick Presets" : "⌨️ Быстрые варианты";
            presetsSubMenu = new ToolStripMenuItem(presetsText);
            BuildPresetSubMenu();
            trayMenu.Items.Add(presetsSubMenu);

            trayMenu.Items.Add(new ToolStripSeparator());

            // Language Submenu
            string langText = isEn ? "🌍 Language / Язык" : "🌍 Язык / Language";
            langSubMenu = new ToolStripMenuItem(langText);
            BuildLanguageSubMenu();
            trayMenu.Items.Add(langSubMenu);

            // Autorun
            string autoText = isEn ? "🚀 Run at Windows Startup" : "🚀 Автозапуск с Windows";
            autorunMenuItem = new ToolStripMenuItem(autoText, null, (s, e) => ToggleAutorunFromMenu());
            autorunMenuItem.Checked = config.Autorun;
            trayMenu.Items.Add(autorunMenuItem);

            trayMenu.Items.Add(new ToolStripSeparator());

            // Exit
            string exitText = isEn ? "❌ Exit" : "❌ Выход";
            var exitItem = new ToolStripMenuItem(exitText, null, (s, e) => ExitApp());
            trayMenu.Items.Add(exitItem);

            if (trayIcon != null)
            {
                string tip = string.Format("OpenUrlHotkey ({0})", config.HotkeyDisplay);
                if (tip.Length > 63) tip = tip.Substring(0, 60) + "...";
                trayIcon.Text = tip;
            }
        }

        private void BuildPresetSubMenu()
        {
            if (presetsSubMenu == null) return;
            presetsSubMenu.DropDownItems.Clear();

            AddPresetItem("RightAlt + RightControl", new int[] { 0xA5, 0xA3 });
            AddPresetItem("LeftCtrl + LeftAlt", new int[] { 0xA2, 0xA4 });
            AddPresetItem("Ctrl + Shift + O", new int[] { 0xA2, 0xA0, 0x4F });
            AddPresetItem("Alt + Space", new int[] { 0xA4, 0x20 });
            AddPresetItem("F12", new int[] { 0x7B });
        }

        private void AddPresetItem(string display, int[] vkCodes)
        {
            var item = new ToolStripMenuItem(display, null, (s, e) => ApplyHotkey(display, vkCodes));
            if (config.HotkeyDisplay == display)
            {
                item.Checked = true;
            }
            presetsSubMenu.DropDownItems.Add(item);
        }

        private void BuildLanguageSubMenu()
        {
            if (langSubMenu == null) return;
            langSubMenu.DropDownItems.Clear();

            var ruItem = new ToolStripMenuItem("🇷🇺 Русский", null, (s, e) => SwitchLanguage("ru"));
            ruItem.Checked = (config.Language == "ru");

            var enItem = new ToolStripMenuItem("🇬🇧 English", null, (s, e) => SwitchLanguage("en"));
            enItem.Checked = (config.Language == "en");

            langSubMenu.DropDownItems.Add(ruItem);
            langSubMenu.DropDownItems.Add(enItem);
        }

        private void SwitchLanguage(string lang)
        {
            config.Language = lang;
            SaveConfig();
            BuildTrayMenu();

            string title = lang == "en" ? "Language Switched" : "Язык изменен";
            string msg = lang == "en" ? "Interface language set to English" : "Интерфейс переключен на русский язык";
            trayIcon.ShowBalloonTip(2000, title, msg, ToolTipIcon.Info);
        }

        private void ApplyHotkey(string display, int[] vkCodes)
        {
            config.HotkeyDisplay = display;
            config.HotkeyVkCodes = vkCodes;
            SaveConfig();

            InitKeyboardHook();
            BuildTrayMenu();

            bool isEn = config.Language == "en";
            string title = isEn ? "Hotkey Changed" : "Хоткей изменен";
            string msg = isEn ? string.Format("New hotkey: {0}", display) : string.Format("Новые горячие клавиши: {0}", display);
            trayIcon.ShowBalloonTip(2000, title, msg, ToolTipIcon.Info);
        }

        private void RecordHotkeyFromTray()
        {
            using (HotkeyRecorderForm recorder = new HotkeyRecorderForm(config.Language))
            {
                if (recorder.ShowDialog() == DialogResult.OK)
                {
                    if (recorder.RecordedVkCodes != null && recorder.RecordedVkCodes.Length > 0)
                    {
                        ApplyHotkey(recorder.RecordedDisplay, recorder.RecordedVkCodes);
                    }
                }
            }
            TrimMemory();
        }

        private void ChangeUrlDialog()
        {
            bool isEn = config.Language == "en";
            using (Form prompt = new Form())
            {
                prompt.Width = 460;
                prompt.Height = 180;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = isEn ? "Configure Website URL" : "Настройка URL сайта";
                prompt.StartPosition = FormStartPosition.CenterScreen;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.TopMost = true;

                string promptText = isEn ? "Enter the website URL to open via hotkey:" : "Введите URL сайта для открытия по горячим клавишам:";
                Label label = new Label() { Left = 20, Top = 20, Text = promptText, Width = 400 };
                TextBox textBox = new TextBox() { Left = 20, Top = 45, Width = 400, Text = config.Url };

                string saveText = isEn ? "Save" : "Сохранить";
                string cancelText = isEn ? "Cancel" : "Отмена";

                Button confirmation = new Button() { Text = saveText, Left = 210, Width = 100, Top = 85, DialogResult = DialogResult.OK };
                Button cancel = new Button() { Text = cancelText, Left = 320, Width = 100, Top = 85, DialogResult = DialogResult.Cancel };

                prompt.Controls.Add(label);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(cancel);
                prompt.AcceptButton = confirmation;
                prompt.CancelButton = cancel;

                if (prompt.ShowDialog() == DialogResult.OK)
                {
                    string newUrl = textBox.Text.Trim();
                    if (!string.IsNullOrEmpty(newUrl))
                    {
                        config.Url = newUrl;
                        SaveConfig();
                        string title = isEn ? "URL Saved" : "URL сохранен";
                        string msg = isEn ? string.Format("New URL: {0}", config.Url) : string.Format("Новый URL: {0}", config.Url);
                        trayIcon.ShowBalloonTip(2000, title, msg, ToolTipIcon.Info);
                    }
                }
            }
            TrimMemory();
        }

        private void InitKeyboardHook()
        {
            if (keyboardHook != null)
            {
                keyboardHook.Stop();
            }

            keyboardHook = new GlobalKeyboardHook(config.HotkeyVkCodes);
            keyboardHook.HotKeyPressed += () =>
            {
                OpenTargetUrl();
            };
            keyboardHook.Start();
        }

        private void OpenTargetUrl()
        {
            try
            {
                string url = config.Url;
                if (string.IsNullOrEmpty(url) || url.Trim().Length == 0)
                {
                    url = "https://www.google.com";
                }
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                string title = config.Language == "en" ? "Error Opening Site" : "Ошибка открытия сайта";
                trayIcon.ShowBalloonTip(3000, title, ex.Message, ToolTipIcon.Error);
            }
        }

        private bool IsAutorunEnabledInRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key == null) return false;
                    object val = key.GetValue(AppName);
                    return val != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void SetAutorunRegistry(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = Application.ExecutablePath;
                        key.SetValue(AppName, string.Format("\"{0}\"", exePath));
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error setting autorun: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleAutorunFromMenu()
        {
            bool newState = !config.Autorun;
            config.Autorun = newState;
            SaveConfig();
            SetAutorunRegistry(newState);
            autorunMenuItem.Checked = newState;

            bool isEn = config.Language == "en";
            string title = isEn ? "Startup Option" : "Автозапуск";
            string msg = isEn ? (newState ? "Startup enabled" : "Startup disabled")
                              : (newState ? "Автозапуск включен" : "Автозапуск отключен");
            trayIcon.ShowBalloonTip(2000, title, msg, ToolTipIcon.Info);
        }

        private void ExitApp()
        {
            if (keyboardHook != null)
            {
                keyboardHook.Stop();
            }
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        public static void TrimMemory()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
        }
    }

    public class AppConfig
    {
        public string Url { get; set; }
        public string HotkeyDisplay { get; set; }
        public int[] HotkeyVkCodes { get; set; }
        public bool Autorun { get; set; }
        public string Language { get; set; }

        public AppConfig()
        {
            Url = "https://www.google.com";
            HotkeyDisplay = "RightAlt + RightControl";
            HotkeyVkCodes = new int[] { 0xA5, 0xA3 }; // VK_RMENU, VK_RCONTROL
            Autorun = false;
            Language = DetectSystemLanguage();
        }

        public static string DetectSystemLanguage()
        {
            try
            {
                string uiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                if (uiLang == "ru")
                {
                    return "ru";
                }
            }
            catch
            {
            }
            return "en";
        }
    }

    public class HotkeyRecorderForm : Form
    {
        public string RecordedDisplay { get; private set; }
        public int[] RecordedVkCodes { get; private set; }

        private Label lblInstruction;
        private Label lblCurrentPressed;
        private Button btnOk;
        private Button btnCancel;

        private List<int> currentVkList = new List<int>();

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private IntPtr hookId = IntPtr.Zero;
        private LowLevelKeyboardProc proc;

        public HotkeyRecorderForm(string lang)
        {
            InitializeUI(lang == "en");
            proc = HookCallback;
            hookId = SetHook(proc);
        }

        private void InitializeUI(bool isEn)
        {
            this.Text = isEn ? "Record Hotkey" : "Запись горячих клавиш";
            this.Size = new Size(440, 230);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            string instrText = isEn ? "Press your desired key combination on keyboard (e.g., RightAlt + RightControl):"
                                    : "Зажмите желаемые клавиши на клавиатуре (например, RightAlt + RightControl):";
            lblInstruction = new Label()
            {
                Left = 20, Top = 18, Width = 380,
                Text = instrText,
                TextAlign = ContentAlignment.TopLeft
            };

            string pressText = isEn ? "Press keys..." : "Нажмите клавиши...";
            lblCurrentPressed = new Label()
            {
                Left = 20, Top = 60, Width = 380, Height = 45,
                Text = pressText,
                Font = new Font(this.Font.FontFamily, 12f, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.WhiteSmoke
            };

            string saveText = isEn ? "Save" : "Сохранить";
            string cancelText = isEn ? "Cancel" : "Отмена";

            btnOk = new Button() { Text = saveText, Left = 200, Top = 130, Width = 95, Height = 34, DialogResult = DialogResult.OK, Enabled = false };
            btnCancel = new Button() { Text = cancelText, Left = 305, Top = 130, Width = 95, Height = 34, DialogResult = DialogResult.Cancel };

            this.Controls.Add(lblInstruction);
            this.Controls.Add(lblCurrentPressed);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.FormClosing += (s, e) =>
            {
                if (hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(hookId);
                    hookId = IntPtr.Zero;
                }
            };
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (!currentVkList.Contains(vkCode))
                    {
                        currentVkList.Add(vkCode);
                        UpdateDisplay();
                    }
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        private void UpdateDisplay()
        {
            if (currentVkList.Count == 0) return;

            List<string> names = new List<string>();
            foreach (int vk in currentVkList)
            {
                names.Add(GetVkName(vk));
            }

            RecordedDisplay = string.Join(" + ", names.ToArray());
            RecordedVkCodes = currentVkList.ToArray();

            this.Invoke(new MethodInvoker(delegate
            {
                lblCurrentPressed.Text = RecordedDisplay;
                btnOk.Enabled = currentVkList.Count > 0;
            }));
        }

        public static string GetVkName(int vkCode)
        {
            switch (vkCode)
            {
                case 0xA5: return "RightAlt";
                case 0xA4: return "LeftAlt";
                case 0xA3: return "RightControl";
                case 0xA2: return "LeftControl";
                case 0xA1: return "RightShift";
                case 0xA0: return "LeftShift";
                case 0x5B: return "LeftWin";
                case 0x5C: return "RightWin";
                case 0x20: return "Space";
                case 0x0D: return "Enter";
                case 0x09: return "Tab";
                case 0x1B: return "Escape";
                default:
                    Keys k = (Keys)vkCode;
                    return k.ToString();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    public class GlobalKeyboardHook
    {
        public event Action HotKeyPressed;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private LowLevelKeyboardProc proc;
        private IntPtr hookId = IntPtr.Zero;

        private int[] targetVkCodes;
        private HashSet<int> currentlyPressedKeys = new HashSet<int>();
        private bool triggered = false;

        public GlobalKeyboardHook(int[] vkCodes)
        {
            targetVkCodes = vkCodes ?? new int[0];
            proc = HookCallback;
        }

        public void Start()
        {
            hookId = SetHook(proc);
        }

        public void Stop()
        {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && targetVkCodes != null && targetVkCodes.Length > 0)
            {
                int msg = wParam.ToInt32();
                int vkCode = Marshal.ReadInt32(lParam);

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    currentlyPressedKeys.Add(vkCode);

                    bool allTargetPressed = true;
                    foreach (int reqVk in targetVkCodes)
                    {
                        if (!currentlyPressedKeys.Contains(reqVk))
                        {
                            allTargetPressed = false;
                            break;
                        }
                    }

                    if (allTargetPressed)
                    {
                        if (!triggered)
                        {
                            triggered = true;
                            System.Threading.ThreadPool.QueueUserWorkItem(delegate
                            {
                                if (HotKeyPressed != null)
                                {
                                    HotKeyPressed();
                                }
                            });
                        }
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    currentlyPressedKeys.Remove(vkCode);

                    bool allTargetPressed = true;
                    foreach (int reqVk in targetVkCodes)
                    {
                        if (!currentlyPressedKeys.Contains(reqVk))
                        {
                            allTargetPressed = false;
                            break;
                        }
                    }

                    if (!allTargetPressed)
                    {
                        triggered = false;
                    }
                }
            }

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    public static class SimpleJson
    {
        public static string Serialize(AppConfig cfg)
        {
            if (cfg == null) cfg = new AppConfig();
            string vkJson = "[]";
            if (cfg.HotkeyVkCodes != null && cfg.HotkeyVkCodes.Length > 0)
            {
                List<string> items = new List<string>();
                for (int i = 0; i < cfg.HotkeyVkCodes.Length; i++)
                {
                    items.Add(cfg.HotkeyVkCodes[i].ToString());
                }
                vkJson = "[" + string.Join(",", items.ToArray()) + "]";
            }
            return string.Format(
                "{{\n  \"Url\": \"{0}\",\n  \"HotkeyDisplay\": \"{1}\",\n  \"HotkeyVkCodes\": {2},\n  \"Autorun\": {3},\n  \"Language\": \"{4}\"\n}}",
                EscapeJson(cfg.Url),
                EscapeJson(cfg.HotkeyDisplay),
                vkJson,
                cfg.Autorun ? "true" : "false",
                EscapeJson(cfg.Language ?? AppConfig.DetectSystemLanguage())
            );
        }

        public static AppConfig Deserialize(string json)
        {
            AppConfig cfg = new AppConfig();
            if (string.IsNullOrEmpty(json)) return cfg;

            Match mUrl = Regex.Match(json, @"""Url""\s*:\s*""([^""]*)""");
            if (mUrl.Success) cfg.Url = UnescapeJson(mUrl.Groups[1].Value);

            Match mHk = Regex.Match(json, @"""HotkeyDisplay""\s*:\s*""([^""]*)""");
            if (mHk.Success) cfg.HotkeyDisplay = UnescapeJson(mHk.Groups[1].Value);

            Match mVk = Regex.Match(json, @"""HotkeyVkCodes""\s*:\s*\[([^\]]*)\]");
            if (mVk.Success)
            {
                string raw = mVk.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    string[] parts = raw.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    List<int> vks = new List<int>();
                    foreach (string p in parts)
                    {
                        int v;
                        if (int.TryParse(p.Trim(), out v)) vks.Add(v);
                    }
                    if (vks.Count > 0) cfg.HotkeyVkCodes = vks.ToArray();
                }
            }

            Match mAuto = Regex.Match(json, @"""Autorun""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
            if (mAuto.Success) cfg.Autorun = bool.Parse(mAuto.Groups[1].Value);

            Match mLang = Regex.Match(json, @"""Language""\s*:\s*""([^""]*)""");
            if (mLang.Success) cfg.Language = UnescapeJson(mLang.Groups[1].Value);

            return cfg;
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string UnescapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
