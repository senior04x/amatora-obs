using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AmatoraObsWpf
{
    public class App : Application
    {
        [STAThread]
        public static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            App app = new App();
            app.Run(new MainWindow());
        }
    }

    public class MainWindow : Window
    {
        // UI Containers
        private Grid mainGrid;
        private Grid mainAppView;

        // Navigation Buttons
        private Button btnTabObs;
        private Button btnTabTablo;
        private Button btnTabSettings;

        // Views
        private Border viewObsAutomation;
        private Border viewStadiumTablo;
        private Border viewAppSettings;

        // Header Controls
        private TextBlock txtHeaderFieldBadge;
        private Border badgeObsStatus;
        private TextBlock txtObsStatusBadgeText;

        // Status Panel Controls
        private TextBlock txtMainFieldTitle;
        private TextBlock txtEngineStatusSub;
        private Button btnTestReplay;
        private Button btnCheckObsConnection;

        // Settings Controls
        private TextBox txtObsIp;
        private TextBox txtObsPort;
        private PasswordBox txtObsPassword;
        private TextBox txtObsPasswordVisible;
        private Button btnTogglePassword;
        private bool isPasswordVisible = false;
        private TextBox txtObsSceneName;
        private TextBox txtFolder;
        private TextBox txtFieldId;
        private Button btnSaveConfig;

        // Activity Feed
        private StackPanel pnlActivityFeed;
        private ScrollViewer scrollActivityFeed;

        // Config & Runtime State
        private string configFilePath;
        private string safeObsIp = "127.0.0.1";
        private string safeObsPort = "4455";
        private string safeObsPassword = "";
        private string safeObsSceneName = "ReplayBuffer";
        private string safeFolder = @"C:\Replays";
        private string safeFieldId = "1";

        private bool isServiceRunning = true;
        private bool isObsConnected = false;
        private ClientWebSocket obsClientWebSocket;
        private string lastSeenSignalTime = "";
        private HttpClient httpClient;

        private const string SupabaseUrl = "https://xzzyhfyazwohdqqbjiiy.supabase.co";
        private const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inh6enloZnlhendvaGRxcWJqaWl5Iiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc4MzEwMzU1MSwiZXhwIjoyMDk4Njc5NTUxfQ.Z_qdzR5mYepOEyW57WXl9fb1v5FV4xEYDP-LvihiU6I";

        public MainWindow()
        {
            Title = "AMATORA OBS Replay Engine (v2.0.0)";
            Width = 1100;
            Height = 750;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(Color.FromRgb(15, 15, 26));

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string amatoraFolder = System.IO.Path.Combine(appData, "AMATORA");
            if (!Directory.Exists(amatoraFolder))
            {
                Directory.CreateDirectory(amatoraFolder);
            }
            configFilePath = System.IO.Path.Combine(appData, "AmatoraObsConfig.ini");

            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.Add("apikey", SupabaseKey);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseKey);

            LoadSavedConfig();
            BuildUI();
            
            // Check OBS Connection & Start Polling
            CheckObsWebSocketConnectionAsync();
            StartRemotePollingLoop();
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
                        if (line.StartsWith("IP=")) safeObsIp = line.Substring(3).Trim();
                        else if (line.StartsWith("Port=")) safeObsPort = line.Substring(5).Trim();
                        else if (line.StartsWith("Password=")) safeObsPassword = line.Substring(9).Trim();
                        else if (line.StartsWith("Scene=")) safeObsSceneName = line.Substring(6).Trim();
                        else if (line.StartsWith("Folder=")) safeFolder = line.Substring(7).Trim();
                        else if (line.StartsWith("FieldId=")) safeFieldId = line.Substring(8).Trim();
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(safeFieldId)) safeFieldId = "1";
        }

        private void SaveUserConfigValues()
        {
            try
            {
                string pwdToSave = isPasswordVisible ? txtObsPasswordVisible.Text : txtObsPassword.Password;

                safeObsIp = string.IsNullOrWhiteSpace(txtObsIp.Text) ? "127.0.0.1" : txtObsIp.Text.Trim();
                safeObsPort = string.IsNullOrWhiteSpace(txtObsPort.Text) ? "4455" : txtObsPort.Text.Trim();
                safeObsPassword = pwdToSave;
                safeObsSceneName = string.IsNullOrWhiteSpace(txtObsSceneName.Text) ? "ReplayBuffer" : txtObsSceneName.Text.Trim();
                safeFolder = string.IsNullOrWhiteSpace(txtFolder.Text) ? @"C:\Replays" : txtFolder.Text.Trim();
                safeFieldId = string.IsNullOrWhiteSpace(txtFieldId.Text) ? "1" : txtFieldId.Text.Trim();

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("IP=" + safeObsIp);
                sb.AppendLine("Port=" + safeObsPort);
                sb.AppendLine("Password=" + safeObsPassword);
                sb.AppendLine("Scene=" + safeObsSceneName);
                sb.AppendLine("Folder=" + safeFolder);
                sb.AppendLine("FieldId=" + safeFieldId);

                File.WriteAllText(configFilePath, sb.ToString());

                // Update UI Labels
                UpdateAllFieldLabels();

                // Re-check OBS connection with new settings
                CheckObsWebSocketConnectionAsync();

                MessageBox.Show("✅ SOZLAMALAR MUVAFFAQIYATLI SAQLANDI!\n\nMaydon raqami: " + safeFieldId + "-MAYDON\nOBS Port: " + safeObsPort, "AMATORA OBS", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Sozlamalarni saqlashda xatolik: " + ex.Message, "Xatolik", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateAllFieldLabels()
        {
            if (txtHeaderFieldBadge != null) txtHeaderFieldBadge.Text = "MAYDON #" + safeFieldId;
            if (txtMainFieldTitle != null) txtMainFieldTitle.Text = "FIELD MONITOR (MAYDON #" + safeFieldId + ")";
            if (txtEngineStatusSub != null) txtEngineStatusSub.Text = "AMATORA OBS Replay Engine (v2.0.0) — Maydon #" + safeFieldId + " faol!";
        }

        private void BuildUI()
        {
            mainGrid = new Grid();
            Content = mainGrid;

            mainAppView = new Grid();
            mainAppView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(65) });
            mainAppView.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.Children.Add(mainAppView);

            BuildHeaderView();
            BuildContentView();
            UpdateAllFieldLabels();
        }

        private void BuildHeaderView()
        {
            Border headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 37)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 65)),
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
            StackPanel logoPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock txtLogo = new TextBlock
            {
                Text = "⚡ AMATORA",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoPanel.Children.Add(txtLogo);

            txtHeaderFieldBadge = new TextBlock
            {
                Text = "MAYDON #" + safeFieldId,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 0)),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 200, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            logoPanel.Children.Add(txtHeaderFieldBadge);
            Grid.SetColumn(logoPanel, 0);
            headerGrid.Children.Add(logoPanel);

            // Nav Tabs
            StackPanel navPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            btnTabObs = CreateNavButton("🎥 OBS AUTOMATION", true);
            btnTabTablo = CreateNavButton("📊 TABLO CONTROL", false);
            btnTabSettings = CreateNavButton("⚙️ SOZLAMALAR", false);

            btnTabObs.Click += (s, e) => SwitchTab(0);
            btnTabTablo.Click += (s, e) => SwitchTab(1);
            btnTabSettings.Click += (s, e) => SwitchTab(2);

            navPanel.Children.Add(btnTabObs);
            navPanel.Children.Add(btnTabTablo);
            navPanel.Children.Add(btnTabSettings);

            Grid.SetColumn(navPanel, 1);
            headerGrid.Children.Add(navPanel);

            // OBS Status Indicator Badge (Header Right)
            badgeObsStatus = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 68, 68)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                VerticalAlignment = VerticalAlignment.Center
            };

            txtObsStatusBadgeText = new TextBlock
            {
                Text = "🔴 OBS: ULANMAGAN",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68))
            };
            badgeObsStatus.Child = txtObsStatusBadgeText;
            Grid.SetColumn(badgeObsStatus, 2);
            headerGrid.Children.Add(badgeObsStatus);
        }

        private Button CreateNavButton(string text, bool isActive)
        {
            Button btn = new Button
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(isActive ? Color.FromRgb(0, 242, 254) : Color.FromRgb(160, 160, 190)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(10, 0, 10, 0),
                Cursor = Cursors.Hand,
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
            viewStadiumTablo = BuildStadiumTabloView();
            viewAppSettings = BuildSettingsTabView();

            viewStadiumTablo.Visibility = Visibility.Collapsed;
            viewAppSettings.Visibility = Visibility.Collapsed;

            contentGrid.Children.Add(viewObsAutomation);
            contentGrid.Children.Add(viewStadiumTablo);
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
                Foreground = Brushes.White
            };
            txtEngineStatusSub = new TextBlock
            {
                Text = "AMATORA OBS Replay Engine (v2.0.0) — Maydon #" + safeFieldId + " faol!",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 170)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            headerStack.Children.Add(txtMainFieldTitle);
            headerStack.Children.Add(txtEngineStatusSub);
            Grid.SetColumn(headerStack, 0);
            topGrid.Children.Add(headerStack);

            // Action Buttons Panel (TEST REPLAY & RECONNECT)
            StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 15) };

            btnCheckObsConnection = new Button
            {
                Content = "🔄 ULANISHNI TEKSHIRISH",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Background = new SolidColorBrush(Color.FromRgb(25, 35, 55)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btnCheckObsConnection.Click += (s, e) => CheckObsWebSocketConnectionAsync();
            btnPanel.Children.Add(btnCheckObsConnection);

            btnTestReplay = new Button
            {
                Content = "🎬 TEST REPLAY BUFFER",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Padding = new Thickness(16, 9, 16, 9),
                Cursor = Cursors.Hand
            };
            btnTestReplay.Click += async (s, e) => await TriggerObsReplayBufferAsync(true);
            btnPanel.Children.Add(btnTestReplay);

            Grid.SetColumn(btnPanel, 1);
            topGrid.Children.Add(btnPanel);

            Grid.SetRow(topGrid, 0);
            g.Children.Add(topGrid);

            // Activity Log Container
            Border feedBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 37)),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 65)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15)
            };
            Grid.SetRow(feedBorder, 1);
            g.Children.Add(feedBorder);

            scrollActivityFeed = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            pnlActivityFeed = new StackPanel();
            scrollActivityFeed.Content = pnlActivityFeed;
            feedBorder.Child = scrollActivityFeed;

            AddActivityFeedCard("🚀 SYSTEM", "AMATORA Engine (v2.0.0) tushdi! Maydon #" + safeFieldId + " uchun tayyor...", "#00F2FE");

            return b;
        }

        private Border BuildSettingsTabView()
        {
            Border b = new Border { Margin = new Thickness(20) };
            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            b.Child = sv;

            StackPanel container = new StackPanel { MaxWidth = 650, HorizontalAlignment = HorizontalAlignment.Left };
            sv.Content = container;

            TextBlock title = new TextBlock
            {
                Text = "⚙️ OBS WEBSOCKET VA MAYDON SOZLAMALARI",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 20)
            };
            container.Children.Add(title);

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
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 50)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 15)
            };
            Grid.SetColumn(txtObsPassword, 0);
            pwdGrid.Children.Add(txtObsPassword);

            txtObsPasswordVisible = new TextBox
            {
                Text = safeObsPassword,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 50)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 10, 15),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(txtObsPasswordVisible, 0);
            pwdGrid.Children.Add(txtObsPasswordVisible);

            btnTogglePassword = new Button
            {
                Content = "👁️ KO'Z",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(12, 6, 12, 6),
                Height = 38,
                Margin = new Thickness(0, 0, 0, 15),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 70)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Cursor = Cursors.Hand
            };
            btnTogglePassword.Click += TogglePasswordVisibility;
            Grid.SetColumn(btnTogglePassword, 1);
            pwdGrid.Children.Add(btnTogglePassword);

            container.Children.Add(pwdGrid);

            // Replay Scene Name
            container.Children.Add(CreateFormLabel("🎬 OBS Replay Sahna Nomi (Default: ReplayBuffer):"));
            txtObsSceneName = CreateFormInput(safeObsSceneName);
            container.Children.Add(txtObsSceneName);

            // Replay Folder
            container.Children.Add(CreateFormLabel("📁 OBS Replays Papkasi (Video Directory):"));
            txtFolder = CreateFormInput(safeFolder);
            container.Children.Add(txtFolder);

            // Save Button
            btnSaveConfig = new Button
            {
                Content = "💾 BARCHA SOZLAMALARNI SAQLASH",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromRgb(0, 242, 254)),
                Padding = new Thickness(20, 12, 20, 12),
                Margin = new Thickness(0, 20, 0, 0),
                Cursor = Cursors.Hand
            };
            btnSaveConfig.Click += (s, e) => SaveUserConfigValues();
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
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 210)),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private TextBox CreateFormInput(string defaultText)
        {
            return new TextBox
            {
                Text = defaultText,
                FontSize = 14,
                Padding = new Thickness(10, 8, 10, 8),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 50)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 80)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 15)
            };
        }

        private Border BuildStadiumTabloView()
        {
            Border b = new Border { Margin = new Thickness(20) };
            TextBlock tb = new TextBlock
            {
                Text = "📊 STADION TABLOSI (HDMI Offline Boshqaruv)",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            b.Child = tb;
            return b;
        }

        private void SwitchTab(int tabIndex)
        {
            viewObsAutomation.Visibility = tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            viewStadiumTablo.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            viewAppSettings.Visibility = tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

            btnTabObs.Foreground = new SolidColorBrush(tabIndex == 0 ? Color.FromRgb(0, 242, 254) : Color.FromRgb(160, 160, 190));
            btnTabTablo.Foreground = new SolidColorBrush(tabIndex == 1 ? Color.FromRgb(0, 242, 254) : Color.FromRgb(160, 160, 190));
            btnTabSettings.Foreground = new SolidColorBrush(tabIndex == 2 ? Color.FromRgb(0, 242, 254) : Color.FromRgb(160, 160, 190));
        }

        private void AddActivityFeedCard(string tag, string message, string hexColor)
        {
            Dispatcher.Invoke(() =>
            {
                Border card = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(28, 28, 45)),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(12)
                };

                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                TextBlock txtTag = new TextBlock
                {
                    Text = tag,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)),
                    Margin = new Thickness(0, 0, 12, 0)
                };
                TextBlock txtMsg = new TextBlock
                {
                    Text = message,
                    FontSize = 13,
                    Foreground = Brushes.White,
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
            bool connected = await ConnectToObsWebSocketAsync();
            UpdateObsStatusUI(connected);
        }

        private async Task<bool> ConnectToObsWebSocketAsync()
        {
            try
            {
                if (obsClientWebSocket != null)
                {
                    try { obsClientWebSocket.Dispose(); } catch { }
                }

                obsClientWebSocket = new ClientWebSocket();
                int port = 4455;
                int.TryParse(safeObsPort, out port);

                string wsUriStr = "ws://" + safeObsIp + ":" + port;
                Uri wsUri = new Uri(wsUriStr);

                CancellationTokenSource cts = new CancellationTokenSource(3000);
                await obsClientWebSocket.ConnectAsync(wsUri, cts.Token);

                if (obsClientWebSocket.State == WebSocketState.Open)
                {
                    // Send obs-websocket Hello/Identify RPC
                    string identifyMsg = "{\"op\":1,\"d\":{\"rpcVersion\":1}}";
                    byte[] sendBytes = Encoding.UTF8.GetBytes(identifyMsg);
                    await obsClientWebSocket.SendAsync(new ArraySegment<byte>(sendBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                    isObsConnected = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("OBS Connection exception: " + ex.Message);
            }

            isObsConnected = false;
            return false;
        }

        private void UpdateObsStatusUI(bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                if (connected)
                {
                    badgeObsStatus.Background = new SolidColorBrush(Color.FromArgb(40, 0, 242, 254));
                    badgeObsStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 242, 254));
                    txtObsStatusBadgeText.Text = "🟢 OBS: ULANDI (" + safeObsPort + ")";
                    txtObsStatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0, 242, 254));
                }
                else
                {
                    badgeObsStatus.Background = new SolidColorBrush(Color.FromArgb(40, 255, 68, 68));
                    badgeObsStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                    txtObsStatusBadgeText.Text = "🔴 OBS: ULANMAGAN";
                    txtObsStatusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                }
            });
        }

        private async Task TriggerObsReplayBufferAsync(bool isManualTest = false)
        {
            if (!isObsConnected)
            {
                bool reconnected = await ConnectToObsWebSocketAsync();
                UpdateObsStatusUI(reconnected);
            }

            if (isObsConnected && obsClientWebSocket != null && obsClientWebSocket.State == WebSocketState.Open)
            {
                try
                {
                    string reqPayload = "{\"op\":6,\"d\":{\"requestType\":\"SaveReplayBuffer\",\"requestId\":\"save_replay_req\"}}";
                    byte[] bytes = Encoding.UTF8.GetBytes(reqPayload);
                    await obsClientWebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

                    if (isManualTest)
                    {
                        AddActivityFeedCard("🎬 TEST REPLAY", "TEST REPLAY TUGMASI BOSILDI! Local OBS Replay Buffer saqlandi! (Port " + safeObsPort + ")", "#00F2FE");
                    }
                    else
                    {
                        AddActivityFeedCard("⚽ GOL REPLAY", "GOL REPLAY SIGNAL KELDI! Local OBS Replay Buffer saqlandi! (Maydon #" + safeFieldId + ")", "#00F2FE");
                    }
                }
                catch (Exception ex)
                {
                    AddActivityFeedCard("⚠️ OBS WEBSOCKET XATOSI", "OBS Replay yuborishda xatolik: " + ex.Message, "#FFC800");
                }
            }
            else
            {
                AddActivityFeedCard("🔴 OBS ULANMAGAN", "OBS Studio ishga tushirilmagan yoki WebSocket porti (" + safeObsPort + ") yopiq. OBS va WebSocket sozlamasini tekshiring!", "#FF4444");
            }
        }

        private void StartRemotePollingLoop()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (isServiceRunning)
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
            string targetSignalName = "REMOTE_GOAL_FIELD_" + safeFieldId;
            string url = SupabaseUrl + "/rest/v1/sponsors?name=eq." + targetSignalName + "&select=id,name,logo_url";

            HttpResponseMessage res = await httpClient.GetAsync(url);
            if (res.IsSuccessStatusCode)
            {
                string body = await res.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(body) && body.Contains("logo_url") && body.Contains("timestamp"))
                {
                    if (body != lastSeenSignalTime)
                    {
                        lastSeenSignalTime = body;
                        await TriggerObsReplayBufferAsync(false);
                    }
                }
            }
        }
    }
}
