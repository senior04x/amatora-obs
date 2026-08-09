using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AmatoraObsWpf
{
    public class App : System.Windows.Application
    {
        [STAThread]
        public static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls;
            App app = new App();
            app.Run(new MainWindow());
        }
    }

    public class MainWindow : Window
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_F11 = 0x7A;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void TriggerF11Hotkey()
        {
            try
            {
                keybd_event(VK_F11, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(50);
                keybd_event(VK_F11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }

        // UI Views
        private Grid mainGrid;
        private Grid loginView;
        private Grid mainAppView;

        // Login Controls
        private System.Windows.Controls.TextBox txtLoginUsername;
        private PasswordBox txtLoginPassword;
        private System.Windows.Controls.TextBox txtLoginPasswordVisible;
        private System.Windows.Controls.Button btnLoginTogglePassword;
        private System.Windows.Controls.Button btnPerformLogin;
        private TextBlock txtLoginError;
        private bool isLoginPasswordVisible = false;

        // Navigation Buttons
        private System.Windows.Controls.Button btnTabObs;
        private System.Windows.Controls.Button btnTabSettings;

        // Views
        private Border viewObsAutomation;
        private Border viewAppSettings;

        // Header Controls
        private TextBlock txtHeaderOrgBadge;
        private TextBlock txtHeaderFieldBadge;
        private Border badgeObsStatus;
        private TextBlock txtObsStatusBadgeText;
        private System.Windows.Controls.Button btnLogout;

        // Status Panel Controls
        private TextBlock txtMainFieldTitle;
        private TextBlock txtEngineStatusSub;
        private System.Windows.Controls.Button btnTestReplay;
        private System.Windows.Controls.Button btnCheckObsConnection;
        private System.Windows.Controls.Button btnCleanFolder;

        // Settings Controls
        private System.Windows.Controls.TextBox txtOrgId;
        private System.Windows.Controls.TextBox txtObsIp;
        private System.Windows.Controls.TextBox txtObsPort;
        private PasswordBox txtObsPassword;
        private System.Windows.Controls.TextBox txtObsPasswordVisible;
        private System.Windows.Controls.Button btnTogglePassword;
        private bool isPasswordVisible = false;
        private System.Windows.Controls.TextBox txtObsSceneName;
        private System.Windows.Controls.TextBox txtReplayDuration;
        private System.Windows.Controls.TextBox txtFolder;
        private System.Windows.Controls.TextBox txtFieldId;
        private System.Windows.Controls.Button btnSaveConfig;

        // Activity Feed
        private StackPanel pnlActivityFeed;
        private System.Windows.Controls.ScrollViewer scrollActivityFeed;

        // System Tray & App Icon
        private NotifyIcon trayIcon;

        // Config & Runtime State
        private string configFilePath;
        private string safeUsername = "";
        private string safeOrgId = "";
        private string safeOrgName = "";
        private string safeObsIp = "127.0.0.1";
        private string safeObsPort = "4455";
        private string safeObsPassword = "";
        private string safeObsSceneName = "ReplayBuffer";
        private string safeReplayDurationSec = "18";
        private string safeFolder = @"C:\Replays";
        private string safeFieldId = "1";

        private bool isLoggedIn = false;
        private bool isServiceRunning = true;
        private bool isReplayRunning = false;
        
        private long lastProcessedGoalTimestamp = 0;
        private string lastProcessedEventId = "";
        private long lastProcessedFinishTimestamp = 0;

        private HttpClient httpClient;

        private const string SupabaseUrl = "https://xzzyhfyazwohdqqbjiiy.supabase.co";
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inh6enloZnlhendvaGRxcWJqaWl5Iiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc4MzEwMzU1MSwiZXhwIjoyMDk4Njc5NTUxfQ.Z_qdzR5mYepOEyW57WXl9fb1v5FV4xEYDP-LvihiU6I";

        public MainWindow()
        {
            Title = "AMATORA OBS Replay Engine (v3.4.0 Universal)";
            Width = 1100;
            Height = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 15, 26));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string amatoraFolder = System.IO.Path.Combine(appData, "AMATORA");
            if (!Directory.Exists(amatoraFolder))
            {
                Directory.CreateDirectory(amatoraFolder);
            }
            configFilePath = System.IO.Path.Combine(appData, "AmatoraObsConfig.ini");

            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Add("apikey", SupabaseKey);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseKey);

            LoadSavedConfig();
            SetWindowIcon();
            BuildUI();
            SetupSystemTrayIcon();

            // Handle Minimize to Tray
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;

            if (isLoggedIn)
            {
                ShowMainAppView();
            }
            else
            {
                ShowLoginView();
            }
        }

        private void BuildUI()
        {
            mainGrid = new Grid();
            Content = mainGrid;

            BuildLoginView();

            mainAppView = new Grid();
            mainAppView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });
            mainAppView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.Children.Add(mainAppView);

            BuildHeaderView();
            BuildContentView();
            UpdateAllFieldLabels();
        }

        private void BuildLoginView()
        {
            loginView = new Grid
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 15, 26))
            };
            mainGrid.Children.Add(loginView);

            Border card = new Border
            {
                Width = 440,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 22, 37)),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 65)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(30)
            };
            loginView.Children.Add(card);

            StackPanel sp = new StackPanel();
            card.Child = sp;

            // Brand Header
            TextBlock txtLogo = new TextBlock
            {
                Text = "⚡ AMATORA ENGINE",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            sp.Children.Add(txtLogo);

            TextBlock txtSub = new TextBlock
            {
                Text = "Tashkilot hisobiga kirish (Admin Login)",
                FontSize = 13,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 140, 170)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 25)
            };
            sp.Children.Add(txtSub);

            // Error Msg
            txtLoginError = new TextBlock
            {
                Text = "",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                Visibility = Visibility.Collapsed
            };
            sp.Children.Add(txtLoginError);

            // Username Label & Input
            sp.Children.Add(CreateFormLabel("🔑 Tashkilot Logini / Email:"));
            txtLoginUsername = CreateFormInput(safeUsername);
            sp.Children.Add(txtLoginUsername);

            // Password Label & Input
            sp.Children.Add(CreateFormLabel("🔒 Parol:"));
            Grid pwdGrid = new Grid();
            pwdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pwdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtLoginPassword = new PasswordBox
            {
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 20)
            };
            Grid.SetColumn(txtLoginPassword, 0);
            pwdGrid.Children.Add(txtLoginPassword);

            txtLoginPasswordVisible = new System.Windows.Controls.TextBox
            {
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 20),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(txtLoginPasswordVisible, 0);
            pwdGrid.Children.Add(txtLoginPasswordVisible);

            btnLoginTogglePassword = new System.Windows.Controls.Button
            {
                Content = "👁️",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(10, 6, 10, 6),
                Height = 38,
                Margin = new Thickness(0, 0, 0, 20),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 70)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnLoginTogglePassword.Click += (s, e) =>
            {
                if (isLoginPasswordVisible)
                {
                    txtLoginPassword.Password = txtLoginPasswordVisible.Text;
                    txtLoginPassword.Visibility = Visibility.Visible;
                    txtLoginPasswordVisible.Visibility = Visibility.Collapsed;
                    btnLoginTogglePassword.Content = "👁️";
                    isLoginPasswordVisible = false;
                }
                else
                {
                    txtLoginPasswordVisible.Text = txtLoginPassword.Password;
                    txtLoginPasswordVisible.Visibility = Visibility.Visible;
                    txtLoginPassword.Visibility = Visibility.Collapsed;
                    btnLoginTogglePassword.Content = "🙈";
                    isLoginPasswordVisible = true;
                }
            };
            Grid.SetColumn(btnLoginTogglePassword, 1);
            pwdGrid.Children.Add(btnLoginTogglePassword);

            sp.Children.Add(pwdGrid);

            // Login Button
            btnPerformLogin = new System.Windows.Controls.Button
            {
                Content = "🔐 TASHKILOTGA KIRISH",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Black,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Padding = new Thickness(20, 12, 20, 12),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnPerformLogin.Click += async (s, e) => await HandleUserLoginAsync();
            sp.Children.Add(btnPerformLogin);
        }

        private async Task HandleUserLoginAsync()
        {
            string username = txtLoginUsername.Text.Trim();
            string pwd = isLoginPasswordVisible ? txtLoginPasswordVisible.Text : txtLoginPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(pwd))
            {
                txtLoginError.Text = "Login va parolni kiriting!";
                txtLoginError.Visibility = Visibility.Visible;
                return;
            }

            btnPerformLogin.IsEnabled = false;
            btnPerformLogin.Content = "⏳ TEKSHIRILMOQDA...";
            txtLoginError.Visibility = Visibility.Collapsed;

            try
            {
                bool success = await PerformSupabaseLoginAsync(username, pwd);
                if (success)
                {
                    isLoggedIn = true;
                    safeUsername = username;
                    SaveUserConfigValues();
                    
                    // Reset login button state for next time
                    btnPerformLogin.IsEnabled = true;
                    btnPerformLogin.Content = "🔐 TASHKILOTGA KIRISH";

                    ShowMainAppView();
                }
                else
                {
                    txtLoginError.Text = "❌ Login yoki parol xato kiritildi!";
                    txtLoginError.Visibility = Visibility.Visible;
                    btnPerformLogin.IsEnabled = true;
                    btnPerformLogin.Content = "🔐 TASHKILOTGA KIRISH";
                }
            }
            catch
            {
                btnPerformLogin.IsEnabled = true;
                btnPerformLogin.Content = "🔐 TASHKILOTGA KIRISH";
            }
        }

        private async Task<bool> PerformSupabaseLoginAsync(string username, string password)
        {
            try
            {
                // Reset active org values
                safeOrgId = "";
                safeOrgName = "";

                string emailValue = username.Contains("@") ? username : (username.Trim() + "@hfl.uz");
                string authUrl = SupabaseUrl + "/auth/v1/token?grant_type=password";
                string payload = "{\"email\":\"" + emailValue + "\",\"password\":\"" + password + "\"}";

                using (StringContent content = new StringContent(payload, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage res = await httpClient.PostAsync(authUrl, content);
                    if (res.IsSuccessStatusCode)
                    {
                        string body = await res.Content.ReadAsStringAsync();
                        string userId = ExtractJsonField(body, "id");
                        
                        // Check if organization_id exists inside user_metadata
                        string metaOrgId = ExtractJsonField(body, "organization_id");
                        if (!string.IsNullOrEmpty(metaOrgId))
                        {
                            safeOrgId = metaOrgId;
                        }

                        await FetchOrganizationDetailsForUserAsync(userId);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private async Task FetchOrganizationDetailsForUserAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(safeOrgId))
                {
                    string adminUrl = SupabaseUrl + "/rest/v1/admin_users?id=eq." + userId + "&select=organization_id,role";
                    HttpResponseMessage res = await httpClient.GetAsync(adminUrl);
                    if (res.IsSuccessStatusCode)
                    {
                        string body = await res.Content.ReadAsStringAsync();
                        string orgIdStr = ExtractJsonField(body, "organization_id");
                        if (!string.IsNullOrEmpty(orgIdStr))
                        {
                            safeOrgId = orgIdStr;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(safeOrgId))
                {
                    string orgUrl = SupabaseUrl + "/rest/v1/organizations?id=eq." + safeOrgId + "&select=id,name,slug";
                    HttpResponseMessage orgRes = await httpClient.GetAsync(orgUrl);
                    if (orgRes.IsSuccessStatusCode)
                    {
                        string orgBody = await orgRes.Content.ReadAsStringAsync();
                        string nameStr = ExtractJsonField(orgBody, "name");
                        if (!string.IsNullOrEmpty(nameStr))
                        {
                            safeOrgName = nameStr;
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(safeOrgName))
            {
                safeOrgName = string.IsNullOrEmpty(safeOrgId) ? "AMATORA LEAGUE" : ("Tashkilot #" + safeOrgId);
            }
        }

        private void ShowLoginView()
        {
            loginView.Visibility = Visibility.Visible;
            mainAppView.Visibility = Visibility.Collapsed;
        }

        private void ShowMainAppView()
        {
            loginView.Visibility = Visibility.Collapsed;
            mainAppView.Visibility = Visibility.Visible;

            UpdateAllFieldLabels();
            InitializeAndStartPollingAsync();
        }

        private void UserLogout()
        {
            // Ask confirmation modal (Ha / Yo'q)
            MessageBoxResult confirm = System.Windows.MessageBox.Show(
                "Tizimdan chiqishni tasdiqlaysizmi?",
                "Chiqishni Tasdiqlash",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            isLoggedIn = false;
            safeUsername = "";
            safeOrgId = "";
            safeOrgName = "";

            // Clear login input textboxes completely
            if (txtLoginUsername != null) txtLoginUsername.Text = "";
            if (txtLoginPassword != null) txtLoginPassword.Password = "";
            if (txtLoginPasswordVisible != null) txtLoginPasswordVisible.Text = "";
            if (txtLoginError != null) txtLoginError.Visibility = Visibility.Collapsed;

            // Reset Login Button state
            if (btnPerformLogin != null)
            {
                btnPerformLogin.IsEnabled = true;
                btnPerformLogin.Content = "🔐 TASHKILOTGA KIRISH";
            }

            SaveUserConfigValues();
            ShowLoginView();
        }

        private void ExitApplication()
        {
            try
            {
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }
            }
            catch { }
            System.Windows.Application.Current.Shutdown();
        }

        private async void InitializeAndStartPollingAsync()
        {
            await FetchAndLockInitialSignalStateAsync();
            CheckObsWebSocketConnectionAsync();
            StartRemotePollingLoop();
        }

        private async Task FetchAndLockInitialSignalStateAsync()
        {
            try
            {
                // Lock latest timestamp from DB on startup so historical goals don't re-trigger on app launch
                string goalSignalName = !string.IsNullOrWhiteSpace(safeOrgId) ? ("REMOTE_GOAL_" + safeOrgId + "_FIELD_" + safeFieldId) : ("REMOTE_GOAL_FIELD_" + safeFieldId);
                string goalUrl = SupabaseUrl + "/rest/v1/sponsors?name=eq." + goalSignalName + "&select=id,logo_url";
                HttpResponseMessage resGoal = await httpClient.GetAsync(goalUrl);
                if (resGoal.IsSuccessStatusCode)
                {
                    string body = await resGoal.Content.ReadAsStringAsync();
                    long ts = ExtractJsonLongField(body, "timestamp");
                    string evId = ExtractJsonField(body, "event_id");
                    if (ts > 0)
                    {
                        lastProcessedGoalTimestamp = ts;
                        lastProcessedEventId = evId;
                    }
                }

                // Ensure OBS is explicitly set to MainScene on launch
                await EnsureObsMainSceneOnLaunchAsync();
            }
            catch { }
        }

        private async Task EnsureObsMainSceneOnLaunchAsync()
        {
            try
            {
                int port = 4455;
                int.TryParse(safeObsPort, out port);
                string wsUriStr = "ws://" + safeObsIp + ":" + port;

                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    CancellationTokenSource cts = new CancellationTokenSource(3000);
                    await ws.ConnectAsync(new Uri(wsUriStr), cts.Token);
                    if (ws.State == WebSocketState.Open)
                    {
                        byte[] recvBuf = new byte[4096];
                        WebSocketReceiveResult recvResult = await ws.ReceiveAsync(new ArraySegment<byte>(recvBuf), cts.Token);
                        string helloJson = Encoding.UTF8.GetString(recvBuf, 0, recvResult.Count);

                        string identifyPayload = "{\"op\":1,\"d\":{\"rpcVersion\":1}}";

                        if (helloJson.Contains("authentication") && helloJson.Contains("challenge") && helloJson.Contains("salt"))
                        {
                            string challenge = ExtractJsonField(helloJson, "challenge");
                            string salt = ExtractJsonField(helloJson, "salt");

                            if (!string.IsNullOrEmpty(challenge) && !string.IsNullOrEmpty(salt))
                            {
                                string pwd = isPasswordVisible ? txtObsPasswordVisible.Text : txtObsPassword.Password;
                                if (string.IsNullOrEmpty(pwd)) pwd = safeObsPassword;

                                string authHash = CalculateObsAuthHash(pwd, salt, challenge);
                                identifyPayload = "{\"op\":1,\"d\":{\"rpcVersion\":1,\"authentication\":\"" + authHash + "\"}}";
                            }
                        }

                        await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, identifyPayload);
                        await Task.Delay(200);

                        // Force OBS to MainScene on launch
                        string returnMainReq = "{\"op\":6,\"d\":{\"requestType\":\"SetCurrentProgramScene\",\"requestData\":{\"sceneName\":\"MainScene\"},\"requestId\":\"launch_main\"}}";
                        await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, returnMainReq);

                        AddActivityFeedCard("📺 MAIN SCENE", "Dastur ishga tushdi: OBS sahnasi Asosiy Efir (MainScene) holatiga o'rnatildi.", "#00F2FE");
                    }
                }
            }
            catch { }
        }

        private void SetWindowIcon()
        {
            try
            {
                System.Drawing.Icon exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (exeIcon != null)
                {
                    Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        exeIcon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions()
                    );
                }
            }
            catch { }
        }

        private void SetupSystemTrayIcon()
        {
            try
            {
                trayIcon = new NotifyIcon();
                System.Drawing.Icon exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                trayIcon.Icon = exeIcon ?? System.Drawing.SystemIcons.Application;
                trayIcon.Text = "AMATORA Engine (" + (string.IsNullOrEmpty(safeOrgName) ? "Tashkilot" : safeOrgName) + ")";
                trayIcon.Visible = true;

                System.Windows.Forms.ContextMenu strip = new System.Windows.Forms.ContextMenu();
                strip.MenuItems.Add("⚡ Ochish", (s, e) => {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                });
                strip.MenuItems.Add("🧹 Replays Papkasini Tozalash", (s, e) => CleanReplaysFolder());
                strip.MenuItems.Add("❌ Dasturdan Chiqish", (s, e) => ExitApplication());

                trayIcon.ContextMenu = strip;
                trayIcon.DoubleClick += (s, e) => {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                };
            }
            catch { }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(2000, "AMATORA Engine", "Dastur fonda ishlashda davom etmoqda (" + safeOrgName + ")", ToolTipIcon.Info);
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }

        private void LoadSavedConfig()
        {
            if (File.Exists(configFilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(configFilePath);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("IsLoggedIn=")) isLoggedIn = line.Substring(11).Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                        else if (line.StartsWith("Username=")) safeUsername = line.Substring(9).Trim();
                        else if (line.StartsWith("OrgId=")) safeOrgId = line.Substring(6).Trim();
                        else if (line.StartsWith("OrgName=")) safeOrgName = line.Substring(8).Trim();
                        else if (line.StartsWith("IP=")) safeObsIp = line.Substring(3).Trim();
                        else if (line.StartsWith("Port=")) safeObsPort = line.Substring(5).Trim();
                        else if (line.StartsWith("Password=")) safeObsPassword = line.Substring(9).Trim();
                        else if (line.StartsWith("Scene=")) safeObsSceneName = line.Substring(6).Trim();
                        else if (line.StartsWith("Duration=")) safeReplayDurationSec = line.Substring(9).Trim();
                        else if (line.StartsWith("Folder=")) safeFolder = line.Substring(7).Trim();
                        else if (line.StartsWith("FieldId=")) safeFieldId = line.Substring(8).Trim();
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(safeFieldId)) safeFieldId = "1";
            if (string.IsNullOrEmpty(safeReplayDurationSec)) safeReplayDurationSec = "18";
        }

        private void SaveUserConfigValues()
        {
            try
            {
                string pwdToSave = isPasswordVisible ? txtObsPasswordVisible.Text : txtObsPassword.Password;

                if (txtOrgId != null) safeOrgId = string.IsNullOrWhiteSpace(txtOrgId.Text) ? safeOrgId : txtOrgId.Text.Trim();
                if (txtObsIp != null) safeObsIp = string.IsNullOrWhiteSpace(txtObsIp.Text) ? "127.0.0.1" : txtObsIp.Text.Trim();
                if (txtObsPort != null) safeObsPort = string.IsNullOrWhiteSpace(txtObsPort.Text) ? "4455" : txtObsPort.Text.Trim();
                if (txtObsPassword != null) safeObsPassword = pwdToSave;
                if (txtObsSceneName != null) safeObsSceneName = string.IsNullOrWhiteSpace(txtObsSceneName.Text) ? "ReplayBuffer" : txtObsSceneName.Text.Trim();
                if (txtReplayDuration != null) safeReplayDurationSec = string.IsNullOrWhiteSpace(txtReplayDuration.Text) ? "18" : txtReplayDuration.Text.Trim();
                if (txtFolder != null) safeFolder = string.IsNullOrWhiteSpace(txtFolder.Text) ? @"C:\Replays" : txtFolder.Text.Trim();
                if (txtFieldId != null) safeFieldId = string.IsNullOrWhiteSpace(txtFieldId.Text) ? "1" : txtFieldId.Text.Trim();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("IsLoggedIn=" + isLoggedIn);
                sb.AppendLine("Username=" + safeUsername);
                sb.AppendLine("OrgId=" + safeOrgId);
                sb.AppendLine("OrgName=" + safeOrgName);
                sb.AppendLine("IP=" + safeObsIp);
                sb.AppendLine("Port=" + safeObsPort);
                sb.AppendLine("Password=" + safeObsPassword);
                sb.AppendLine("Scene=" + safeObsSceneName);
                sb.AppendLine("Duration=" + safeReplayDurationSec);
                sb.AppendLine("Folder=" + safeFolder);
                sb.AppendLine("FieldId=" + safeFieldId);

                File.WriteAllText(configFilePath, sb.ToString());

                // Update UI Labels & Tray Tooltip
                UpdateAllFieldLabels();
            }
            catch { }
        }

        private void UpdateAllFieldLabels()
        {
            string displayOrg = string.IsNullOrEmpty(safeOrgName) ? (string.IsNullOrEmpty(safeOrgId) ? "AMATORA LEAGUE" : ("TASHKILOT #" + safeOrgId)) : safeOrgName.ToUpper();
            
            if (txtHeaderOrgBadge != null) txtHeaderOrgBadge.Text = "🏢 " + displayOrg;
            if (txtHeaderFieldBadge != null) txtHeaderFieldBadge.Text = "MAYDON #" + safeFieldId;
            if (txtMainFieldTitle != null) txtMainFieldTitle.Text = displayOrg + " — MONITOR (MAYDON #" + safeFieldId + ")";
            if (txtEngineStatusSub != null) txtEngineStatusSub.Text = "AMATORA OBS Engine — " + displayOrg + " [Maydon #" + safeFieldId + "] faol!";
            if (trayIcon != null) trayIcon.Text = "AMATORA Engine (" + displayOrg + " - Maydon #" + safeFieldId + ")";
        }

        private void BuildHeaderView()
        {
            Border headerBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 22, 37)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 65)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(headerBorder, 0);
            mainAppView.Children.Add(headerBorder);

            Grid headerGrid = new Grid();
            headerGrid.Margin = new Thickness(20, 0, 20, 0);
            headerBorder.Child = headerGrid;

            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Brand Logo & Field Badge
            StackPanel logoPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock txtLogo = new TextBlock
            {
                Text = "⚡ AMATORA",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoPanel.Children.Add(txtLogo);

            txtHeaderOrgBadge = new TextBlock
            {
                Text = "🏢 TASHKILOT",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 242, 254)),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoPanel.Children.Add(txtHeaderOrgBadge);

            txtHeaderFieldBadge = new TextBlock
            {
                Text = "MAYDON #" + safeFieldId,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 200, 0)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 200, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoPanel.Children.Add(txtHeaderFieldBadge);
            Grid.SetColumn(logoPanel, 0);
            headerGrid.Children.Add(logoPanel);

            // Nav Tabs
            StackPanel navPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            btnTabObs = CreateNavButton("🎥 OBS AUTOMATION", true);
            btnTabSettings = CreateNavButton("⚙️ SOZLAMALAR", false);

            btnTabObs.Click += (s, e) => SwitchTab(0);
            btnTabSettings.Click += (s, e) => SwitchTab(1);

            navPanel.Children.Add(btnTabObs);
            navPanel.Children.Add(btnTabSettings);

            Grid.SetColumn(navPanel, 1);
            headerGrid.Children.Add(navPanel);

            // Right Panel (OBS Badge & Logout)
            StackPanel rightPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            badgeObsStatus = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 68, 68)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            txtObsStatusBadgeText = new TextBlock
            {
                Text = "🔴 OBS: ULANMAGAN",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68))
            };
            badgeObsStatus.Child = txtObsStatusBadgeText;
            rightPanel.Children.Add(badgeObsStatus);

            btnLogout = new System.Windows.Controls.Button
            {
                Content = "🚪 CHIQISH",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 255, 68, 68)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnLogout.Click += (s, e) => UserLogout();
            rightPanel.Children.Add(btnLogout);

            Grid.SetColumn(rightPanel, 2);
            headerGrid.Children.Add(rightPanel);
        }

        private System.Windows.Controls.Button CreateNavButton(string text, bool isActive)
        {
            System.Windows.Controls.Button btn = new System.Windows.Controls.Button
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isActive ? System.Windows.Media.Color.FromRgb(0, 242, 254) : System.Windows.Media.Color.FromRgb(160, 160, 190)),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(10, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new Thickness(12, 8, 12, 8)
            };
            return btn;
        }

        private void BuildContentView()
        {
            Grid contentGrid = new Grid();
            Grid.SetRow(contentGrid, 1);
            mainAppView.Children.Add(contentGrid);

            viewObsAutomation = BuildObsAutomationTabView();
            viewAppSettings = BuildSettingsTabView();

            viewAppSettings.Visibility = Visibility.Collapsed;

            contentGrid.Children.Add(viewObsAutomation);
            contentGrid.Children.Add(viewAppSettings);
        }

        private Border BuildObsAutomationTabView()
        {
            Border b = new Border { Margin = new Thickness(20) };
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            b.Child = g;

            // Top Info & Test Action Controls
            Grid topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            txtMainFieldTitle = new TextBlock
            {
                Text = "FIELD MONITOR (MAYDON #" + safeFieldId + ")",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White
            };
            txtEngineStatusSub = new TextBlock
            {
                Text = "AMATORA OBS Engine — Maydon #" + safeFieldId + " faol!",
                FontSize = 13,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(140, 140, 170)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            headerStack.Children.Add(txtMainFieldTitle);
            headerStack.Children.Add(txtEngineStatusSub);
            Grid.SetColumn(headerStack, 0);
            topGrid.Children.Add(headerStack);

            // Action Buttons Panel (TEST REPLAY & CLEAN FOLDER & RECONNECT)
            StackPanel btnPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 15) };

            btnCheckObsConnection = new System.Windows.Controls.Button
            {
                Content = "🔄 ULANISHNI TEKSHIRISH",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 35, 55)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCheckObsConnection.Click += (s, e) => CheckObsWebSocketConnectionAsync();
            btnPanel.Children.Add(btnCheckObsConnection);

            btnCleanFolder = new System.Windows.Controls.Button
            {
                Content = "🧹 PAPKANI TOZALASH",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 25, 35)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCleanFolder.Click += (s, e) => CleanReplaysFolder();
            btnPanel.Children.Add(btnCleanFolder);

            btnTestReplay = new System.Windows.Controls.Button
            {
                Content = "🎬 TEST REPLAY BUFFER",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Black,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Padding = new Thickness(16, 9, 16, 9),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnTestReplay.Click += async (s, e) => await ExecuteFullGoalReplayWorkflowAsync("", "", safeOrgId);
            btnPanel.Children.Add(btnTestReplay);

            Grid.SetColumn(btnPanel, 1);
            topGrid.Children.Add(btnPanel);

            Grid.SetRow(topGrid, 0);
            g.Children.Add(topGrid);

            // Activity Log Container
            Border feedBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 22, 37)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 65)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };
            Grid.SetRow(feedBorder, 1);
            g.Children.Add(feedBorder);

            scrollActivityFeed = new System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
            pnlActivityFeed = new StackPanel();
            scrollActivityFeed.Content = pnlActivityFeed;
            feedBorder.Child = scrollActivityFeed;

            AddActivityFeedCard("🚀 AMATORA AUTH", "Dasturga avtorizatsiyadan muvaffaqiyatli o'tildi! Tashkilot: " + safeOrgName, "#00F2FE");

            return b;
        }

        private Border BuildSettingsTabView()
        {
            Border b = new Border { Margin = new Thickness(20) };
            System.Windows.Controls.ScrollViewer sv = new System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
            b.Child = sv;

            StackPanel container = new StackPanel { MaxWidth = 650, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
            sv.Content = container;

            TextBlock title = new TextBlock
            {
                Text = "⚙️ OBS WEBSOCKET VA MAYDON SOZLAMALARI",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 20)
            };
            container.Children.Add(title);

            // Org ID
            container.Children.Add(CreateFormLabel("🏢 TASHKILOT ID / SLUG:"));
            txtOrgId = CreateFormInput(safeOrgId);
            container.Children.Add(txtOrgId);

            // Field ID
            container.Children.Add(CreateFormLabel("⚽ MAYDON RAQAMI / ID (1 yoki 2):"));
            txtFieldId = CreateFormInput(safeFieldId);
            container.Children.Add(txtFieldId);

            // OBS IP
            container.Children.Add(CreateFormLabel("🌐 OBS Server IP (Default: 127.0.0.1):"));
            txtObsIp = CreateFormInput(safeObsIp);
            container.Children.Add(txtObsIp);

            // OBS Port
            container.Children.Add(CreateFormLabel("🔌 OBS WebSocket Port (Default: 4455 yoki 4456):"));
            txtObsPort = CreateFormInput(safeObsPort);
            container.Children.Add(txtObsPort);

            // OBS Password with Eye Toggle Button
            container.Children.Add(CreateFormLabel("🔑 OBS WebSocket Paroli:"));
            Grid pwdGrid = new Grid();
            pwdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pwdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            txtObsPassword = new PasswordBox
            {
                Password = safeObsPassword,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 15)
            };
            Grid.SetColumn(txtObsPassword, 0);
            pwdGrid.Children.Add(txtObsPassword);

            txtObsPasswordVisible = new System.Windows.Controls.TextBox
            {
                Text = safeObsPassword,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 15),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(txtObsPasswordVisible, 0);
            pwdGrid.Children.Add(txtObsPasswordVisible);

            btnTogglePassword = new System.Windows.Controls.Button
            {
                Content = "👁️ KO'Z",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(12, 6, 12, 6),
                Height = 38,
                Margin = new Thickness(0, 0, 0, 15),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 70)),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnTogglePassword.Click += TogglePasswordVisibility;
            Grid.SetColumn(btnTogglePassword, 1);
            pwdGrid.Children.Add(btnTogglePassword);

            container.Children.Add(pwdGrid);

            // Replay Scene Name
            container.Children.Add(CreateFormLabel("🎬 OBS Replay Sahna Nomi (Default: ReplayBuffer):"));
            txtObsSceneName = CreateFormInput(safeObsSceneName);
            container.Children.Add(txtObsSceneName);

            // Replay Duration (Seconds)
            container.Children.Add(CreateFormLabel("⏱️ Replay Efir Davomiyligi (Sekund, Masalan: 18 yoki 20):"));
            txtReplayDuration = CreateFormInput(safeReplayDurationSec);
            container.Children.Add(txtReplayDuration);

            // Replay Folder
            container.Children.Add(CreateFormLabel("📁 OBS Replays Papkasi (Video Directory):"));
            txtFolder = CreateFormInput(safeFolder);
            container.Children.Add(txtFolder);

            // Save Button
            btnSaveConfig = new System.Windows.Controls.Button
            {
                Content = "💾 BARCHA SOZLAMALARNI SAQLASH",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.Black,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254)),
                Padding = new Thickness(20, 12, 20, 12),
                Margin = new Thickness(0, 20, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSaveConfig.Click += (s, e) =>
            {
                SaveUserConfigValues();
                System.Windows.MessageBox.Show("✅ SOZLAMALAR MUVAFFAQIYATLI SAQLANDI!", "AMATORA OBS", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            container.Children.Add(btnSaveConfig);

            return b;
        }

        private void TogglePasswordVisibility(object sender, RoutedEventArgs e)
        {
            if (isPasswordVisible)
            {
                txtObsPassword.Password = txtObsPasswordVisible.Text;
                txtObsPassword.Visibility = Visibility.Visible;
                txtObsPasswordVisible.Visibility = Visibility.Collapsed;
                btnTogglePassword.Content = "👁️ KO'Z";
                isPasswordVisible = false;
            }
            else
            {
                txtObsPasswordVisible.Text = txtObsPassword.Password;
                txtObsPasswordVisible.Visibility = Visibility.Visible;
                txtObsPassword.Visibility = Visibility.Collapsed;
                btnTogglePassword.Content = "🙈 YASHIR";
                isPasswordVisible = true;
            }
        }

        private TextBlock CreateFormLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 210)),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private System.Windows.Controls.TextBox CreateFormInput(string defaultText)
        {
            return new System.Windows.Controls.TextBox
            {
                Text = defaultText,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 50)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 15)
            };
        }

        private void SwitchTab(int tabIndex)
        {
            viewObsAutomation.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            viewAppSettings.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

            btnTabObs.Foreground = new SolidColorBrush(tabIndex == 0 ? System.Windows.Media.Color.FromRgb(0, 242, 254) : System.Windows.Media.Color.FromRgb(160, 160, 190));
            btnTabSettings.Foreground = new SolidColorBrush(tabIndex == 1 ? System.Windows.Media.Color.FromRgb(0, 242, 254) : System.Windows.Media.Color.FromRgb(160, 160, 190));
        }

        private void AddActivityFeedCard(string tag, string message, string hexColor)
        {
            Dispatcher.Invoke(() =>
            {
                Border card = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 45)),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(12)
                };

                StackPanel sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                TextBlock txtTag = new TextBlock
                {
                    Text = tag,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)),
                    Margin = new Thickness(0, 0, 12, 0)
                };
                TextBlock txtMsg = new TextBlock
                {
                    Text = message,
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                };
                sp.Children.Add(txtTag);
                sp.Children.Add(txtMsg);
                card.Child = sp;

                pnlActivityFeed.Children.Insert(0, card);
            });
        }

        private async void CheckObsWebSocketConnectionAsync()
        {
            bool connected = await TestObsWebSocketHandshakeAsync();
            UpdateObsStatusUI(connected);
        }

        private async Task<bool> TestObsWebSocketHandshakeAsync()
        {
            try
            {
                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    int port = 4455;
                    int.TryParse(safeObsPort, out port);

                    Uri wsUri = new Uri("ws://" + safeObsIp + ":" + port);
                    CancellationTokenSource cts = new CancellationTokenSource(3000);

                    await ws.ConnectAsync(wsUri, cts.Token);
                    if (ws.State == WebSocketState.Open)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private void UpdateObsStatusUI(bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                if (connected)
                {
                    badgeObsStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 242, 254));
                    badgeObsStatus.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254));
                    txtObsStatusBadgeText.Text = "🟢 OBS: ULANDI (" + safeObsPort + ")";
                    txtObsStatusBadgeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 242, 254));
                }
                else
                {
                    badgeObsStatus.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 68, 68));
                    badgeObsStatus.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
                    txtObsStatusBadgeText.Text = "🔴 OBS: ULANMAGAN";
                    txtObsStatusBadgeText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 68, 68));
                }
            });
        }

        private string CalculateObsAuthHash(string password, string salt, string challenge)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] secretBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt));
                string secretBase64 = Convert.ToBase64String(secretBytes);
                byte[] authBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretBase64 + challenge));
                return Convert.ToBase64String(authBytes);
            }
        }

        private async Task SendObsWebSocketCommandPayloadAsync(ClientWebSocket ws, CancellationToken ct, string jsonPayload)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(jsonPayload);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        private async Task ExecuteFullGoalReplayWorkflowAsync(string matchId, string eventId, string orgId)
        {
            // LOCK: Prevent concurrent execution during replay playback
            if (isReplayRunning) return;
            isReplayRunning = true;

            int port = 4455;
            int.TryParse(safeObsPort, out port);
            string wsUriStr = "ws://" + safeObsIp + ":" + port;

            int durationSec = 18;
            int.TryParse(safeReplayDurationSec, out durationSec);
            if (durationSec <= 0) durationSec = 18;

            try
            {
                AddActivityFeedCard("⚽ WORKFLOW", "Gol Replay avtomatizatsiyasi boshlandi! (Maydon #" + safeFieldId + ")", "#00F2FE");

                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    CancellationTokenSource cts = new CancellationTokenSource(35000);
                    await ws.ConnectAsync(new Uri(wsUriStr), cts.Token);

                    if (ws.State == WebSocketState.Open)
                    {
                        UpdateObsStatusUI(true);

                        // Read op:0 (Hello)
                        byte[] recvBuf = new byte[4096];
                        WebSocketReceiveResult recvResult = await ws.ReceiveAsync(new ArraySegment<byte>(recvBuf), cts.Token);
                        string helloJson = Encoding.UTF8.GetString(recvBuf, 0, recvResult.Count);

                        string identifyPayload = "{\"op\":1,\"d\":{\"rpcVersion\":1}}";

                        if (helloJson.Contains("authentication") && helloJson.Contains("challenge") && helloJson.Contains("salt"))
                        {
                            string challenge = ExtractJsonField(helloJson, "challenge");
                            string salt = ExtractJsonField(helloJson, "salt");

                            if (!string.IsNullOrEmpty(challenge) && !string.IsNullOrEmpty(salt))
                            {
                                string pwd = isPasswordVisible ? txtObsPasswordVisible.Text : txtObsPassword.Password;
                                if (string.IsNullOrEmpty(pwd)) pwd = safeObsPassword;

                                string authHash = CalculateObsAuthHash(pwd, salt, challenge);
                                identifyPayload = "{\"op\":1,\"d\":{\"rpcVersion\":1,\"authentication\":\"" + authHash + "\"}}";
                            }
                        }

                        // Send op:1 (Identify)
                        await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, identifyPayload);
                        await Task.Delay(200);

                        // STEP 1: Send SaveReplayBuffer request to OBS (both built-in & source-record plugin)
                        AddActivityFeedCard("🎬 OBS SAVE", "SaveReplayBuffer yuborildi. Replay Buffer saqlanmoqda...", "#00F2FE");
                        string saveReq = "{\"op\":6,\"d\":{\"requestType\":\"SaveReplayBuffer\",\"requestId\":\"save_rb_id\"}}";
                        await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, saveReq);

                        // Trigger Source Record plugin vendor request for clean camera feed
                        try
                        {
                            string srReq1 = "{\"op\":6,\"d\":{\"requestType\":\"CallVendorRequest\",\"requestData\":{\"vendorName\":\"source-record\",\"requestType\":\"save\"},\"requestId\":\"sr_req_1\"}}";
                            string srReq2 = "{\"op\":6,\"d\":{\"requestType\":\"CallVendorRequest\",\"requestData\":{\"vendorName\":\"source-record\",\"requestType\":\"save_replay_buffer\"},\"requestId\":\"sr_req_2\"}}";
                            await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, srReq1);
                            await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, srReq2);
                        }
                        catch { }

                        // STEP 2: Wait 3 seconds for file to be written to C:\Replays
                        AddActivityFeedCard("⏳ DELAY (3s)", "Videoni papkaga yozilishi kutilmoqda (3 soniya)...", "#FFC800");
                        await Task.Delay(3000);

                        // STEP 3: Switch OBS Program Scene to ReplayBuffer (or ReplayScene)
                        AddActivityFeedCard("🎥 SCENE SWITCH", "OBS Saqlangan Scene-ga (" + safeObsSceneName + ") o'tkazildi!", "#00F2FE");
                        string switchSceneReq = "{\"op\":6,\"d\":{\"requestType\":\"SetCurrentProgramScene\",\"requestData\":{\"sceneName\":\"" + safeObsSceneName + "\"},\"requestId\":\"switch_replay\"}}";
                        await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, switchSceneReq);

                        // STEP 4: Wait exact replay duration (e.g. 18s) for replay video playback
                        AddActivityFeedCard("📺 REPLAY EFIR", "Replay kadr " + durationSec + " soniya jonli efirga uzatilmoqda...", "#00F2FE");
                        await Task.Delay(durationSec * 1000);

                        // STEP 5: Switch OBS Program Scene INSTANTLY back to MainScene
                        AddActivityFeedCard("📺 MAIN SCENE", durationSec + " soniya tugadi. OBS Asosiy Efir (MainScene)-ga DARHOL qaytarildi!", "#00F2FE");
                        string returnMainReq = "{\"op\":6,\"d\":{\"requestType\":\"SetCurrentProgramScene\",\"requestData\":{\"sceneName\":\"MainScene\"},\"requestId\":\"return_main\"}}";
                        await SendObsWebSocketCommandPayloadAsync(ws, cts.Token, returnMainReq);

                        // STEP 6: Find latest video file in C:\Replays and Upload to Supabase Storage with org_id isolation
                        FileInfo latestVideo = GetLatestReplayFile(safeFolder);
                        if (latestVideo != null && latestVideo.Exists)
                        {
                            AddActivityFeedCard("☁️ UPLOAD", "Replay video Supabase Storage-ga yuklanmoqda (" + (latestVideo.Length / 1024 / 1024) + " MB)...", "#FF007F");
                            string targetOrg = string.IsNullOrEmpty(orgId) ? (string.IsNullOrEmpty(safeOrgId) ? "default" : safeOrgId) : orgId;
                            string publicUrl = await UploadVideoToSupabaseStorageAsync(latestVideo.FullName, targetOrg, matchId);

                            if (!string.IsNullOrEmpty(publicUrl))
                            {
                                AddActivityFeedCard("✅ SUPABASE", "Replay video bulutga yuklandi! (" + targetOrg + ")", "#00F2FE");

                                // STEP 7: Link replay URL to unique match_event UUID in Supabase
                                if (!string.IsNullOrEmpty(eventId))
                                {
                                    await LinkReplayUrlToMatchEventAsync(eventId, publicUrl);
                                    AddActivityFeedCard("📱 AMATORA APP", "Replay video amatora-app ilovasiga real-time biriktirildi! ⚽🔥", "#00F2FE");
                                }
                            }
                        }
                        else
                        {
                            AddActivityFeedCard("⚠️ VIDEO TOPILMADI", "Replay papkasida (" + safeFolder + ") video fayl topilmadi.", "#FFC800");
                        }
                    }
                    else
                    {
                        UpdateObsStatusUI(false);
                        AddActivityFeedCard("🔴 OBS ULANMAGAN", "OBS WebSocket-ga ulana olmadi. Port: " + safeObsPort, "#FF4444");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateObsStatusUI(false);
                AddActivityFeedCard("⚠️ WORKFLOW XATOSI", "Replay workflow xatosi: " + ex.Message, "#FFC800");
            }
            finally
            {
                isReplayRunning = false;
            }
        }

        private void CleanReplaysFolder()
        {
            try
            {
                if (Directory.Exists(safeFolder))
                {
                    DirectoryInfo dir = new DirectoryInfo(safeFolder);
                    FileInfo[] files = dir.GetFiles("*.*", SearchOption.TopDirectoryOnly);
                    int count = 0;

                    foreach (FileInfo file in files)
                    {
                        try
                        {
                            file.Delete();
                            count++;
                        }
                        catch { }
                    }

                    AddActivityFeedCard("🧹 PAPKA TOZALANDI", "O'yin yakunlandi! " + safeFolder + " papkasidagi " + count + " ta eski replay videolari tozalandi.", "#00F2FE");
                }
            }
            catch (Exception ex)
            {
                AddActivityFeedCard("⚠️ TOZALASH XATOSI", "Papkani tozalashda xatolik: " + ex.Message, "#FFC800");
            }
        }

        private FileInfo GetLatestReplayFile(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    DirectoryInfo dir = new DirectoryInfo(folderPath);
                    FileInfo[] files = dir.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => f.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                                    f.Extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase) ||
                                    f.Extension.Equals(".mov", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.LastWriteTime)
                        .ToArray();

                    if (files.Length > 0)
                    {
                        // If CleanReplay_ file exists, prioritize it over standard Replay_ files
                        FileInfo cleanFile = files.FirstOrDefault(f => f.Name.StartsWith("CleanReplay", StringComparison.OrdinalIgnoreCase));
                        if (cleanFile != null)
                        {
                            return cleanFile;
                        }
                        return files[0];
                    }
                }
            }
            catch { }
            return null;
        }

        private async Task<string> UploadVideoToSupabaseStorageAsync(string localFilePath, string orgId, string matchId)
        {
            try
            {
                byte[] fileBytes = File.ReadAllBytes(localFilePath);
                string pathPrefix = (string.IsNullOrEmpty(orgId) ? "default" : orgId) + "/";
                if (!string.IsNullOrEmpty(matchId)) pathPrefix += matchId + "/";
                
                string fileName = pathPrefix + "replay_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4";
                string uploadUrl = SupabaseUrl + "/storage/v1/object/replays/" + fileName;

                using (ByteArrayContent content = new ByteArrayContent(fileBytes))
                {
                    content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                    content.Headers.Add("x-upsert", "true");

                    HttpResponseMessage res = await httpClient.PostAsync(uploadUrl, content);
                    if (res.IsSuccessStatusCode)
                    {
                        return SupabaseUrl + "/storage/v1/object/public/replays/" + fileName;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Upload Exception: " + ex.Message);
            }
            return "";
        }

        private async Task LinkReplayUrlToMatchEventAsync(string eventId, string videoUrl)
        {
            try
            {
                string patchUrl = SupabaseUrl + "/rest/v1/match_events?id=eq." + eventId;
                string payloadStr = "{\"replay_video_url\":\"" + videoUrl + "\"}";
                
                using (StringContent content = new StringContent(payloadStr, Encoding.UTF8, "application/json"))
                {
                    HttpRequestMessage req = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl);
                    req.Content = content;
                    req.Headers.Add("Prefer", "return=minimal");

                    await httpClient.SendAsync(req);
                }
            }
            catch { }
        }

        private string ExtractJsonField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return "";
            try
            {
                // Unescape escaped JSON strings if present e.g. \"timestamp\": -> "timestamp":
                string clean = json.Replace("\\\"", "\"").Replace("\\\\", "\\");

                int keyIdx = clean.IndexOf("\"" + fieldName + "\"");
                if (keyIdx == -1) return "";

                int colonIdx = clean.IndexOf(":", keyIdx);
                if (colonIdx == -1) return "";

                int start = colonIdx + 1;
                while (start < clean.Length && (clean[start] == ' ' || clean[start] == '\t' || clean[start] == '\r' || clean[start] == '\n'))
                {
                    start++;
                }

                if (start >= clean.Length) return "";

                if (clean[start] == '"')
                {
                    start++;
                    int end = clean.IndexOf("\"", start);
                    if (end != -1)
                    {
                        return clean.Substring(start, end - start);
                    }
                }
                else
                {
                    int end = start;
                    while (end < clean.Length && clean[end] != ',' && clean[end] != '}' && clean[end] != ']' && clean[end] != ' ' && clean[end] != '\r' && clean[end] != '\n')
                    {
                        end++;
                    }
                    return clean.Substring(start, end - start).Trim();
                }
            }
            catch { }
            return "";
        }

        private long ExtractJsonLongField(string json, string fieldName)
        {
            try
            {
                string val = ExtractJsonField(json, fieldName);
                long parsed = 0;
                if (long.TryParse(val, out parsed))
                {
                    return parsed;
                }
            }
            catch { }
            return 0;
        }

        private void StartRemotePollingLoop()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (isServiceRunning && isLoggedIn && !isReplayRunning)
                        {
                            await PollSupabaseRemoteFieldSignal();
                        }
                    }
                    catch { }

                    await Task.Delay(2500);
                }
            });
        }

        private async Task PollSupabaseRemoteFieldSignal()
        {
            if (isReplayRunning) return;

            // 1. Poll Goal Signal (Check both General and Scoped signal rows)
            string generalGoalSignal = "REMOTE_GOAL_FIELD_" + safeFieldId;
            string scopedGoalSignal = !string.IsNullOrWhiteSpace(safeOrgId) ? ("REMOTE_GOAL_" + safeOrgId + "_FIELD_" + safeFieldId) : "";

            string[] goalSignalsToPoll = string.IsNullOrEmpty(scopedGoalSignal) 
                ? new string[] { generalGoalSignal } 
                : new string[] { scopedGoalSignal, generalGoalSignal };

            foreach (string goalSignalName in goalSignalsToPoll)
            {
                if (isReplayRunning) return;

                string goalUrl = SupabaseUrl + "/rest/v1/sponsors?name=eq." + goalSignalName + "&select=id,name,logo_url";
                HttpResponseMessage goalRes = await httpClient.GetAsync(goalUrl);

                if (goalRes.IsSuccessStatusCode)
                {
                    string body = await goalRes.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(body) && (body.Contains("timestamp") || body.Contains("event_id")))
                    {
                        long timestamp = ExtractJsonLongField(body, "timestamp");
                        string eventId = ExtractJsonField(body, "event_id");
                        string signalOrgId = ExtractJsonField(body, "org_id");

                        // Trigger if timestamp > lastProcessedGoalTimestamp OR (eventId is non-empty and eventId != lastProcessedEventId)
                        bool isNewSignal = (timestamp > 0 && timestamp > lastProcessedGoalTimestamp) || 
                                           (!string.IsNullOrEmpty(eventId) && eventId != lastProcessedEventId);

                        if (isNewSignal)
                        {
                            // Org match check
                            bool isOrgMatch = string.IsNullOrWhiteSpace(safeOrgId) || 
                                             safeOrgId == "default" || 
                                             string.IsNullOrWhiteSpace(signalOrgId) || 
                                             signalOrgId.Equals(safeOrgId, StringComparison.OrdinalIgnoreCase);

                            if (isOrgMatch)
                            {
                                if (timestamp > 0) lastProcessedGoalTimestamp = timestamp;
                                if (!string.IsNullOrEmpty(eventId)) lastProcessedEventId = eventId;

                                AddActivityFeedCard("⚽ GOL SIGNAL TUTILDI", "Org ID: " + (string.IsNullOrEmpty(signalOrgId) ? safeOrgId : signalOrgId) + " | Maydon #" + safeFieldId, "#00F2FE");
                                await ExecuteFullGoalReplayWorkflowAsync(matchId: ExtractJsonField(body, "match_id"), eventId: eventId, orgId: signalOrgId);
                                break;
                            }
                        }
                    }
                }
            }

            // 2. Poll Finish Match Signal to Clean Replays Folder
            string generalFinishSignal = "REMOTE_FINISH_MATCH_FIELD_" + safeFieldId;
            string scopedFinishSignal = !string.IsNullOrWhiteSpace(safeOrgId) ? ("REMOTE_FINISH_MATCH_" + safeOrgId + "_FIELD_" + safeFieldId) : "";

            string[] finishSignalsToPoll = string.IsNullOrEmpty(scopedFinishSignal) ? new string[] { generalFinishSignal } : new string[] { scopedFinishSignal, generalFinishSignal };

            foreach (string finishSignalName in finishSignalsToPoll)
            {
                string finishUrl = SupabaseUrl + "/rest/v1/sponsors?name=eq." + finishSignalName + "&select=id,name,logo_url";
                HttpResponseMessage finishRes = await httpClient.GetAsync(finishUrl);

                if (finishRes.IsSuccessStatusCode)
                {
                    string body = await finishRes.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(body) && body.Contains("timestamp"))
                    {
                        long timestamp = ExtractJsonLongField(body, "timestamp");
                        string signalOrgId = ExtractJsonField(body, "org_id");

                        if (timestamp > 0 && timestamp > lastProcessedFinishTimestamp)
                        {
                            bool isOrgMatch = string.IsNullOrWhiteSpace(safeOrgId) || 
                                             safeOrgId == "default" || 
                                             string.IsNullOrWhiteSpace(signalOrgId) || 
                                             signalOrgId.Equals(safeOrgId, StringComparison.OrdinalIgnoreCase);

                            if (isOrgMatch)
                            {
                                lastProcessedFinishTimestamp = timestamp;
                                CleanReplaysFolder();
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
