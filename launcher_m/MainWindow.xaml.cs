using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;
using launcher_m.Core;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;

namespace launcher_m
{
    public partial class MainWindow : FluentWindow
    {
        public object _editingInstance;

        private bool _suppressNextNavAnimation = true;
        public MainWindow()
        {
            InitializeComponent();
            UpdateStatus();

            this.Loaded += (s, e) =>
            {
                RootNavigation.Navigate(typeof(HomeView));
                Dispatcher.BeginInvoke(new Action(() =>
                {
                try
                {
                    _ = Activator.CreateInstance(typeof(BuilderView));
                    _ = Activator.CreateInstance(typeof(AccountsView));
                    _ = Activator.CreateInstance(typeof(SettingsView));
                }
                catch { }

                    _suppressNextNavAnimation = false;

                    HideNavigationBackButton();

                    EventHandler? handler = null;
                    handler = (ss, ee) =>
                    {
                        try { MoveSelectionLine(); } catch { }
                        NavContainer.LayoutUpdated -= handler;
                    };
                    NavContainer.LayoutUpdated += handler;
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            };
        }

        public void UpdateStatus()
        {
            var activeAccId = ConfigManager.Data.Settings.ActiveAccountId;
            var activeAcc = ConfigManager.Data.Accounts.FirstOrDefault(a => a.Id == activeAccId);

            TxtActiveUsername.Text = activeAcc != null ? activeAcc.Username :
                (Application.Current.TryFindResource("Loc_CreateAccount") as string ?? "Створіть акаунт");

            var activeInstId = ConfigManager.Data.Settings.ActiveInstanceId;
            var activeInst = ConfigManager.Data.Instances.FirstOrDefault(i => i.Id == activeInstId);

            if (activeInst != null)
            {
                TxtActiveInstance.Text = $"{activeInst.Name} ({activeInst.GameVersion} - {activeInst.LoaderType})";

                if (System.Enum.TryParse(typeof(Wpf.Ui.Controls.SymbolRegular), activeInst.IconSymbol, out var symbol))
                {
                    IconSelectedInstance.Symbol = (Wpf.Ui.Controls.SymbolRegular)symbol;
                }
            }
            else
            {
                TxtActiveInstance.Text = Application.Current.TryFindResource("Loc_SelectInstance") as string ?? "Виберіть збірку";
                IconSelectedInstance.Symbol = Wpf.Ui.Controls.SymbolRegular.Box24;
            }
        }

        public async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            var activeAccId = ConfigManager.Data.Settings.ActiveAccountId;
            var activeAcc = ConfigManager.Data.Accounts.FirstOrDefault(a => a.Id == activeAccId);

            var activeInstId = ConfigManager.Data.Settings.ActiveInstanceId;
            var activeInst = ConfigManager.Data.Instances.FirstOrDefault(i => i.Id == activeInstId);

            if (activeAcc == null || activeInst == null)
            {
                ShowAlert(Application.Current.TryFindResource("Loc_SelectAccountInstanceWarning") as string ?? "Будь ласка, виберіть акаунт і збірку перед запуском!");
                return;
            }

            BtnPlay.IsEnabled = false;
            LaunchProgressPanel.Visibility = Visibility.Visible;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string instanceDir = Path.Combine(appData, ".FDlauncher", "Instances", activeInst.Name);
                var path = new MinecraftPath(instanceDir);

                var launcher = new MinecraftLauncher(path);

                string downloadText = Application.Current.TryFindResource("Loc_DownloadingStatus") as string ?? "Завантаження:";

                launcher.FileProgressChanged += (s, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtLaunchStatus.Text = $"{downloadText} {args.Name}";
                        PbLaunchProgress.Maximum = args.TotalTasks;
                        PbLaunchProgress.Value = args.ProgressedTasks;
                    });
                };

                MSession session;
                if (activeAcc.IsOffline)
                {
                    session = MSession.CreateOfflineSession(activeAcc.Username);
                }
                else
                {
                    session = new MSession(activeAcc.Username, activeAcc.AccessToken, activeAcc.UUID);
                }

                string targetVersion = activeInst.GameVersion;

                if (activeInst.LoaderType == "Forge")
                {
                    var forge = new ForgeInstaller(launcher);
                    var versions = await forge.GetForgeVersions(activeInst.GameVersion);
                    var best = versions.First();

                    string forgeText = Application.Current.TryFindResource("Loc_InstallingForgeStatus") as string ?? "Встановлення Forge";
                    TxtLaunchStatus.Text = $"{forgeText} {best.ForgeVersionName}...";
                    targetVersion = await forge.Install(activeInst.GameVersion, best.ForgeVersionName);
                }
                else if (activeInst.LoaderType == "Fabric")
                {
                    TxtLaunchStatus.Text = Application.Current.TryFindResource("Loc_PreparingFabricStatus") as string ?? "Підготовка Fabric...";
                    targetVersion = await InstallFabricManually(activeInst.GameVersion, path.BasePath);
                }

                var settings = ConfigManager.Data.Settings;
                var jvmArgs = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrWhiteSpace(settings.JvmArguments))
                {
                    jvmArgs.AddRange(settings.JvmArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }

                var launchOptions = new MLaunchOption
                {
                    Session = session,
                    MaximumRamMb = settings.MaxRamMb,
                    ExtraJvmArguments = jvmArgs.Select(arg => new MArgument(arg)).ToArray(),
                    FullScreen = settings.FullScreen,
                    ScreenWidth = settings.WindowWidth > 0 ? settings.WindowWidth : 854,
                    ScreenHeight = settings.WindowHeight > 0 ? settings.WindowHeight : 480
                };

                var process = await launcher.CreateProcessAsync(targetVersion, launchOptions);
                TxtLaunchStatus.Text = Application.Current.TryFindResource("Loc_GameStarting") as string ?? "Гра запускається!";
                process.Start();

                await Task.Delay(3000);
                LaunchProgressPanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                string errText = Application.Current.TryFindResource("Loc_CriticalLaunchError") as string ?? "Критична помилка запуску:";
                ShowAlert($"{errText}\n{ex.Message}");
                LaunchProgressPanel.Visibility = Visibility.Collapsed;
            }
            finally
            {
                BtnPlay.IsEnabled = true;
            }
        }

        private async Task<string> InstallFabricManually(string mcVersion, string basePath)
        {
            using (var client = new HttpClient())
            {
                string loadersUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}";
                string loadersJson = await client.GetStringAsync(loadersUrl);

                using var doc = JsonDocument.Parse(loadersJson);
                var root = doc.RootElement;
                if (root.GetArrayLength() == 0)
                {
                    string loaderErrText = Application.Current.TryFindResource("Loc_FabricLoaderNotFound") as string ?? "Не знайдено лоадер Fabric для версії";
                    throw new Exception($"{loaderErrText} {mcVersion}!");
                }

                string loaderVersion = root[0].GetProperty("loader").GetProperty("version").GetString() ?? "";
                string profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
                string profileJson = await client.GetStringAsync(profileUrl);

                string versionName = $"fabric-loader-{loaderVersion}-{mcVersion}";
                string versionDir = Path.Combine(basePath, "versions", versionName);

                Directory.CreateDirectory(versionDir);
                File.WriteAllText(Path.Combine(versionDir, $"{versionName}.json"), profileJson);

                return versionName;
            }
        }

        private void ShowAlert(string message)
        {
            TxtAlertMessage.Text = message;
            AlertOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseAlert_Click(object sender, RoutedEventArgs e)
        {
            AlertOverlay.Visibility = Visibility.Collapsed;
        }

        private FrameworkElement _currentSelectedTab;

        private void RootNavigation_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (RootNavigation.SelectedItem is Wpf.Ui.Controls.NavigationViewItem selectedItem)
            {
                _currentSelectedTab = selectedItem;
                MoveSelectionLine();
                if (!_suppressNextNavAnimation)
                    AnimateNavigationContent();
            }
        }

        private void AnimateNavigationContent()
        {
            try
            {
                ContentPresenter? contentPresenter = FindVisualChild<ContentPresenter>(RootNavigation);

                FrameworkElement target = contentPresenter as FrameworkElement ?? RootNavigation as FrameworkElement;
                if (target == null) return;

                if (!(target.RenderTransform is TranslateTransform tt))
                {
                    tt = new TranslateTransform(0, 8);
                    target.RenderTransform = tt;
                }

                tt.Y = 8;
                target.Opacity = 0;

                var duration = TimeSpan.FromMilliseconds(320);

                var fadeIn = new DoubleAnimation(0, 1, duration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                var slideUp = new DoubleAnimation(8, 0, duration)
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { fadeIn.Freeze(); } catch { }
                    try { slideUp.Freeze(); } catch { }

                    target.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                    tt.BeginAnimation(TranslateTransform.YProperty, slideUp);
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            catch { }
        }

        private void HideNavigationBackButton()
        {
            try
            {
                var backToggle = FindVisualChild<ToggleButton>(RootNavigation);
                if (backToggle != null)
                {
                    backToggle.Visibility = Visibility.Collapsed;
                    return;
                }
                var backBtn = FindVisualChild<System.Windows.Controls.Button>(RootNavigation);
                if (backBtn != null)
                {
                    backBtn.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) return typed;

                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }

            return null;
        }

        private void RootNavigation_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MoveSelectionLine();
        }

        private void MoveSelectionLine()
        {
            if (_currentSelectedTab == null) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var transform = _currentSelectedTab.TransformToAncestor(NavContainer);
                    var position = transform.Transform(new Point(0, 0));

                    double targetX = position.X + (_currentSelectedTab.ActualWidth / 2) - 15;

                    if (AnimatedSelectionLine.Opacity == 0)
                    {
                        SelectionLineTransform.X = targetX;
                        AnimatedSelectionLine.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
                        return;
                    }

                    var moveAnim = new DoubleAnimation
                    {
                        To = targetX,
                        Duration = TimeSpan.FromMilliseconds(250),
                        EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                    };

                    moveAnim.Freeze();
                    SelectionLineTransform.BeginAnimation(TranslateTransform.XProperty, moveAnim);
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        public void OpenEditPage(object instance)
        {
            this._editingInstance = instance;
            RootNavigation.Navigate(typeof(BuilderView));
        }

    }
}