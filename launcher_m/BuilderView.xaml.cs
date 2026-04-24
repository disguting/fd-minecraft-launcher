
using launcher_m.Core;
using launcher_m.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace launcher_m
{
    public partial class BuilderView : UserControl
    {
        public ObservableCollection<GameInstance> InstancesList { get; set; } = new();
        private GameInstance? _instancePendingDeletion;
        private static readonly HttpClient _httpClient = new HttpClient();

        private ObservableCollection<ModSearchResult> _modSearchResults = new ObservableCollection<ModSearchResult>();
        private ObservableCollection<LocalMod> _localMods = new ObservableCollection<LocalMod>();

        private launcher_m.Models.GameInstance? _editingInstance;



        public BuilderView()
        {
            InitializeComponent();

            ListModSearchResults.ItemsSource = _modSearchResults;
            ListLocalMods.ItemsSource = _localMods;

            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "LauncherM_StudentProject/1.0 (contact_my_discord)");
            }

            this.Loaded += BuilderView_Loaded;
        }

        private async void BuilderView_Loaded(object sender, RoutedEventArgs e)
        {

            await Task.Delay(50);

            LoadInstances();
        }

        private void LoadInstances()
        {
            InstancesList = new ObservableCollection<GameInstance>(ConfigManager.Data.Instances);
            ListInstances.ItemsSource = InstancesList;

            var activeId = ConfigManager.Data.Settings.ActiveInstanceId;
            ListInstances.SelectedItem = InstancesList.FirstOrDefault(i => i.Id == activeId);
        }

        private void ListInstances_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListInstances.SelectedItem is GameInstance selectedInstance)
            {
                ConfigManager.Data.Settings.ActiveInstanceId = selectedInstance.Id;
                ConfigManager.Save();

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.UpdateStatus();
                }
            }
        }

        private async void BtnShowAddDialog_Click(object sender, RoutedEventArgs e)
        {
            txtVersionName.Text = "";
            listIcons.SelectedIndex = 0;
            cbLoaderType.SelectedIndex = 0;

            DialogOverlay.Visibility = Visibility.Visible;
            await LoadMinecraftVersionsAsync();
        }

        private async Task LoadMinecraftVersionsAsync()
        {
            string loadingText = Application.Current.TryFindResource("Loc_Loading") as string ?? "Завантаження...";

            if (cbGameVersion.Items.Count > 0 && cbGameVersion.Items[0].ToString() != loadingText)
                return;

            cbGameVersion.Items.Clear();
            cbGameVersion.Text = Application.Current.TryFindResource("Loc_FetchingList") as string ?? "Отримання списку...";
            cbGameVersion.IsEnabled = false;
            bool showedFromCache = false;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string basePath = Path.Combine(appData, ".FDlauncher");
                string cachePath = Path.Combine(basePath, "versions_cache.json");

                if (File.Exists(cachePath))
                {
                    try
                    {
                        var cached = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(cachePath));
                        if (cached != null && cached.Count > 0)
                        {
                            cbGameVersion.ItemsSource = cached;
                            cbGameVersion.IsEnabled = true;
                            if (cbGameVersion.Items.Count > 0)
                                cbGameVersion.SelectedIndex = 0;
                            showedFromCache = true;
                        }
                    }
                    catch { /* ignore cache read errors and continue to fetch */ }
                }
            }
            catch { }

            try
            {
                string json = await _httpClient.GetStringAsync("https://launchermeta.mojang.com/mc/game/version_manifest.json");

                var versionsToAdd = await Task.Run(() =>
                {
                    var list = new List<string>();
                    using var doc = JsonDocument.Parse(json);
                    var versions = doc.RootElement.GetProperty("versions");
                    var s = ConfigManager.Data.Settings;

                    foreach (var version in versions.EnumerateArray())
                    {
                        string? type = version.GetProperty("type").GetString();
                        string? id = version.GetProperty("id").GetString();

                        bool shouldAdd = false;
                        if (type == "release") shouldAdd = true;
                        else if (type == "snapshot" && s.ShowSnapshots) shouldAdd = true;
                        else if ((type == "old_alpha" || type == "old_beta") && s.ShowAlphaBeta) shouldAdd = true;

                        if (shouldAdd && id != null) list.Add(id);
                    }

                    return list;
                });
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    cbGameVersion.ItemsSource = versionsToAdd;
                    cbGameVersion.IsEnabled = true;
                    if (cbGameVersion.Items.Count > 0)
                        cbGameVersion.SelectedIndex = 0;

                    if (!showedFromCache)
                    {
                        cbGameVersion.Visibility = System.Windows.Visibility.Visible;
                    }
                }));

                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string basePath = Path.Combine(appData, ".FDlauncher");
                    if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);
                    string cachePath = Path.Combine(basePath, "versions_cache.json");
                    File.WriteAllText(cachePath, JsonSerializer.Serialize(versionsToAdd));
                }
                catch { }
            }
            catch
            {
                cbGameVersion.Items.Clear();
                cbGameVersion.Items.Add("1.20.4");
                cbGameVersion.Items.Add("1.20.1");
                cbGameVersion.Items.Add("1.19.4");
                cbGameVersion.Items.Add("1.16.5");
                cbGameVersion.Items.Add("1.12.2");
                cbGameVersion.IsEnabled = true;
                cbGameVersion.SelectedIndex = 0;
            }
        }

        private void BtnCloseDialog_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveVersion_Click(object sender, RoutedEventArgs e)
        {
            string name = txtVersionName.Text.Trim();
            if (string.IsNullOrEmpty(name))
                name = Application.Current.TryFindResource("Loc_NewInstance") as string ?? "Нова збірка";

            string selectedIcon = "Box24";
            if (listIcons.SelectedItem is ListBoxItem item && item.Content is SymbolIcon icon)
            {
                selectedIcon = icon.Symbol.ToString();
            }

            string loaderType = "Vanilla";
            if (cbLoaderType.SelectedItem is ComboBoxItem cbItem)
            {
                loaderType = cbItem.Content?.ToString() ?? "Vanilla";
            }

            string version = "1.20.1";
            if (cbGameVersion.SelectedItem != null)
            {
                version = cbGameVersion.SelectedItem.ToString() ?? "1.20.1";    
            }
            else if (!string.IsNullOrEmpty(cbGameVersion.Text))
            {
                version = cbGameVersion.Text.Trim();
            }

            var newInstance = new GameInstance
            {
                Name = name,
                IconSymbol = selectedIcon,
                GameVersion = version,
                LoaderType = loaderType
            };

            InstancesList.Add(newInstance);
            ConfigManager.Data.Instances.Add(newInstance);

            ConfigManager.Data.Settings.ActiveInstanceId = newInstance.Id;
            ConfigManager.Save();

            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateStatus();
            }

            DialogOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnShowEditDialog_Click(object sender, RoutedEventArgs e)
        {
            var selectedInst = ListInstances.SelectedItem as GameInstance;
            if (selectedInst == null)
            {
                ShowAlert(Application.Current.TryFindResource("Loc_SelectInstanceFirst") as string ?? "Будь ласка, спочатку виберіть збірку зі списку ліворуч.");
                return;
            }

            _editingInstance = selectedInst;
            txtEditName.Text = selectedInst.Name;
            for (int i = 0; i < listEditIcons.Items.Count; i++)
            {
                if (listEditIcons.Items[i] is ListBoxItem item && item.Content is SymbolIcon icon)
                {
                    if (icon.Symbol.ToString() == selectedInst.IconSymbol)
                    {
                        listEditIcons.SelectedIndex = i;
                        break;
                    }
                }
            }

            LoadLocalModsList(selectedInst);

            EditDialogOverlay.Visibility = Visibility.Visible;
            EditTabControl.SelectedIndex = 0;

            _modSearchResults.Clear();
            txtSearchMod.Text = "";
            ListResPackSearchResults.ItemsSource = null;
            ListShaderSearchResults.ItemsSource = null;
            await PerformModSearchAsync();

        }

        private void BtnCloseEditDialog_Click(object sender, RoutedEventArgs e)
        {
            EditDialogOverlay.Visibility = Visibility.Collapsed;
        }
        
        private void BtnSaveEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedInst = ListInstances.SelectedItem as GameInstance;
            if (selectedInst == null) return;

            string newName = txtEditName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                ShowAlert(Application.Current.TryFindResource("Loc_InstanceNameEmpty") as string ?? "Назва збірки не може бути порожньою!");
                return;
            }

            if (selectedInst.Name != newName)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string oldPath = Path.Combine(appData, ".FDlauncher", "Instances", selectedInst.Name);
                string newPath = Path.Combine(appData, ".FDlauncher", "Instances", newName);

                try
                {
                    if (Directory.Exists(oldPath))
                    {
                        if (newName.Equals(selectedInst.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            string tempPath = oldPath + "_temp";
                            Directory.Move(oldPath, tempPath);
                            Directory.Move(tempPath, newPath);
                        }
                        else
                        {
                            if (Directory.Exists(newPath))
                            {
                                ShowAlert(Application.Current.TryFindResource("Loc_InstanceAlreadyExists") as string ?? "Збірка з такою назвою вже існує!");
                                return;
                            }
                            Directory.Move(oldPath, newPath);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    ShowAlert(Application.Current.TryFindResource("Loc_AccessDeniedExplorer") as string ?? "Помилка доступу!\nЗакрийте цю папку в 'Провіднику' Windows, щоб лаунчер міг її перейменувати.");
                    return;
                }
                catch (IOException)
                {
                    ShowAlert(Application.Current.TryFindResource("Loc_FolderInUse") as string ?? "Папка зайнята іншою програмою (або файл всередині відкритий). Закрийте всі вікна і спробуйте ще раз.");
                    return;
                }
                catch (Exception ex)
                {
                    string errFmt = Application.Current.TryFindResource("Loc_RenameFailed") as string ?? "Не вдалося перейменувати папку:\n{0}";
                    ShowAlert(string.Format(errFmt, ex.Message));
                    return;
                }
            }

            selectedInst.Name = newName;
            if (listEditIcons.SelectedItem is ListBoxItem item && item.Content is SymbolIcon icon)
            {
                selectedInst.IconSymbol = icon.Symbol.ToString();
            }

            ConfigManager.Save();
            ListInstances.Items.Refresh();

            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateStatus();
            }

            EditDialogOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnDeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (ListInstances.SelectedItem is GameInstance selectedInstance)
            {
                _instancePendingDeletion = selectedInstance;
                string formatMsg = Application.Current.TryFindResource("Loc_ConfirmDeleteInstanceSpecific") as string ?? "Ви дійсно хочете назавжди видалити збірку '{0}'? Всі моди та світи будуть стерті з диска.";
                TxtDeleteMessage.Text = string.Format(formatMsg, selectedInstance.Name);
                DeleteConfirmOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                ShowAlert(Application.Current.TryFindResource("Loc_SelectInstanceFirst") as string ?? "Будь ласка, спочатку виберіть збірку зі списку ліворуч.");
            }
        }

        private void BtnConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_instancePendingDeletion != null)
            {
                try
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string instanceDir = Path.Combine(appData, ".FDlauncher", "Instances", _instancePendingDeletion.Name);

                    if (Directory.Exists(instanceDir))
                    {
                        Directory.Delete(instanceDir, true);
                    }
                }
                catch
                {
                    DeleteConfirmOverlay.Visibility = Visibility.Collapsed;
                    ShowAlert(Application.Current.TryFindResource("Loc_DeleteFailedRunning") as string ?? "Не вдалося видалити файли. Переконайтеся, що ця версія гри зараз не запущена.");
                    return;
                }

                InstancesList.Remove(_instancePendingDeletion);

                var instInDb = ConfigManager.Data.Instances.FirstOrDefault(i => i.Id == _instancePendingDeletion.Id);
                if (instInDb != null)
                {
                    ConfigManager.Data.Instances.Remove(instInDb);
                }

                if (ConfigManager.Data.Settings.ActiveInstanceId == _instancePendingDeletion.Id)
                {
                    ConfigManager.Data.Settings.ActiveInstanceId = string.Empty;
                }

                ConfigManager.Save();

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.UpdateStatus();
                }

                _instancePendingDeletion = null;
            }

            DeleteConfirmOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnCancelDelete_Click(object sender, RoutedEventArgs e)
        {
            _instancePendingDeletion = null;
            DeleteConfirmOverlay.Visibility = Visibility.Collapsed;
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

        private bool IsModInstalled(string projectId)
        {
            return _localMods.Any(m => m.FileName.Contains($"[{projectId}]"));
        }

        private async void BtnSearchMod_Click(object sender, RoutedEventArgs e)
        {
            await PerformModSearchAsync();
        }

        private async void TxtSearchMod_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformModSearchAsync();
            }
        }

        private async void CbModSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditDialogOverlay != null && EditDialogOverlay.Visibility == Visibility.Visible)
            {
                await PerformModSearchAsync();
            }
        }

        private async Task PerformModSearchAsync()
        {
            string query = txtSearchMod.Text.Trim();
            var selectedInst = _editingInstance ?? ListInstances.SelectedItem as GameInstance;
            if (selectedInst == null) return;

            string loader = selectedInst.LoaderType.ToLower();
            if (loader == "vanilla")
            {
                txtSearchStatus.Text = Application.Current.TryFindResource("Loc_VanillaNoMods") as string ?? "Ванільна версія не підтримує моди.";
                return;
            }

            string sortIndex = "downloads";
            if (cbModSort.SelectedItem is ComboBoxItem cbItem && cbItem.Tag != null)
            {
                sortIndex = cbItem.Tag.ToString() ?? "downloads";
            }

            SearchProgressRing.Visibility = Visibility.Visible;
            ListModSearchResults.Visibility = Visibility.Collapsed;

            string popText = Application.Current.TryFindResource("Loc_LoadingPopularMods") as string ?? "Завантаження популярних модів...";
            string searchTxt = Application.Current.TryFindResource("Loc_SearchingMods") as string ?? "Шукаємо моди...";
            txtSearchStatus.Text = string.IsNullOrEmpty(query) ? popText : searchTxt;
            txtSearchStatus.Visibility = Visibility.Visible;
            _modSearchResults.Clear();

            try
            {
                string facets = $"[[\"categories:{loader}\"],[\"versions:{selectedInst.GameVersion}\"]]";
                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&facets={Uri.EscapeDataString(facets)}&index={sortIndex}&limit=50";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<ModrinthSearchResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (searchResult != null && searchResult.Hits != null && searchResult.Hits.Count > 0)
                {
                    foreach (var hit in searchResult.Hits)
                    {
                        _modSearchResults.Add(new ModSearchResult
                        {
                            ProjectId = hit.ProjectId,
                            Title = hit.Title,
                            Description = hit.Description,
                            Author = hit.Author,
                            IconUrl = hit.IconUrl,
                            IsInstalled = IsModInstalled(hit.ProjectId)
                        });
                    }
                    ListModSearchResults.Visibility = Visibility.Visible;
                    txtSearchStatus.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtSearchStatus.Text = Application.Current.TryFindResource("Loc_NothingFound") as string ?? "Нічого не знайдено :(";
                }
            }
            catch
            {
                txtSearchStatus.Text = Application.Current.TryFindResource("Loc_ConnectionError") as string ?? "Помилка з'єднання з сервером.";
            }
            finally
            {
                SearchProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnDownloadMod_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;

            string projectId = btn!.Tag?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(projectId)) return;

            var selectedInst = ListInstances.SelectedItem as GameInstance;
            if (selectedInst == null) return;

            string loader = selectedInst.LoaderType.ToLower();
            string version = selectedInst.GameVersion;

            btn.Content = Application.Current.TryFindResource("Loc_Downloading") as string ?? "Завантаження...";
            btn.IsEnabled = false;

            try
            {
                string url = $"https://api.modrinth.com/v2/project/{projectId}/version?loaders=[\"{loader}\"]&game_versions=[\"{version}\"]";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var versionsArray = doc.RootElement.EnumerateArray();

                if (!versionsArray.Any())
                {
                    ShowAlert(Application.Current.TryFindResource("Loc_ModNoFiles") as string ?? "Цей мод не має файлів для вашої версії гри чи лоадера.");
                    return;
                }

                var latestVersion = versionsArray.First();
                var files = latestVersion.GetProperty("files");
                var primaryFile = files[0];

                string downloadUrl = primaryFile.GetProperty("url").GetString() ?? "";
                string originalFileName = primaryFile.GetProperty("filename").GetString() ?? "";

                string newFileName = $"[{projectId}] {originalFileName}";

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string modsDir = Path.Combine(appData, ".FDlauncher", "Instances", selectedInst.Name, "mods");

                if (!Directory.Exists(modsDir))
                {
                    Directory.CreateDirectory(modsDir);
                }

                byte[] fileBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(Path.Combine(modsDir, newFileName), fileBytes);

                LoadLocalModsList(selectedInst);

                var searchItem = _modSearchResults.FirstOrDefault(m => m.ProjectId == projectId);
                if (searchItem != null)
                {
                    searchItem.IsInstalled = true;
                }
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current.TryFindResource("Loc_DownloadError") as string ?? "Помилка завантаження:\n{0}";
                ShowAlert(string.Format(errFmt, ex.Message));
            }
            finally
            {
                btn.ClearValue(System.Windows.Controls.Button.ContentProperty);
                btn.ClearValue(System.Windows.Controls.Button.IsEnabledProperty);
            }
        }

        private void LoadLocalModsList(GameInstance inst)
        {
            _localMods.Clear();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string modsDir = Path.Combine(appData, ".FDlauncher", "Instances", inst.Name, "mods");

            if (Directory.Exists(modsDir))
            {
                var files = Directory.GetFiles(modsDir, "*.jar");
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    _localMods.Add(new LocalMod
                    {
                        FileName = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        FileSize = (fileInfo.Length / 1024 / 1024.0).ToString("0.00") + " MB"
                    });
                }
            }
            string fmt = Application.Current.TryFindResource("Loc_InstalledCountSpecific") as string ?? "Встановлено: {0}";
            txtInstalledModsCount.Text = string.Format(fmt, _localMods.Count);
        }

        private void BtnDeleteLocalMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag != null)
            {
                string path = btn.Tag?.ToString() ?? string.Empty;
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);

                        var modToRemove = _localMods.FirstOrDefault(m => m.FullPath == path);
                        if (modToRemove != null)
                        {
                            _localMods.Remove(modToRemove);
                        }

                        string fmt = Application.Current.TryFindResource("Loc_InstalledCountSpecific") as string ?? "Встановлено: {0}";
                        txtInstalledModsCount.Text = string.Format(fmt, _localMods.Count);

                        foreach (var searchItem in _modSearchResults)
                        {
                            if (path.Contains($"[{searchItem.ProjectId}]"))
                            {
                                searchItem.IsInstalled = false;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    string errFmt = Application.Current.TryFindResource("Loc_ModDeleteFailed") as string ?? "Не вдалося видалити мод. Можливо гра зараз запущена.\n{0}";
                    ShowAlert(string.Format(errFmt, ex.Message));
                }
            }
        }

        private async void EditTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != EditTabControl) return;

            var selectedTab = EditTabControl.SelectedItem as System.Windows.Controls.TabItem;
            if (selectedTab == null || _editingInstance == null) return;

            string tabHeader = selectedTab.Header?.ToString() ?? "";

            if (tabHeader == (Application.Current.TryFindResource("Loc_TabAddMods") as string))
            {
                if (_modSearchResults.Count == 0)
                {
                    txtSearchMod.Text = "";
                    await PerformModSearchAsync();
                }
            }
            else if (tabHeader == (Application.Current.TryFindResource("Loc_TabManageMods") as string))
            {
                LoadLocalModsList(_editingInstance);
            }

            else if (tabHeader == (Application.Current.TryFindResource("Loc_TabResourcepacks") as string))
            {
                RefreshLocalAddonsList("resourcepacks", ListLocalResPacks);
                if (ListResPackSearchResults.ItemsSource == null)
                {
                    txtSearchResPack.Text = "";
                    await SearchAddon("resourcepack", "", txtResPackSearchStatus, ListResPackSearchResults);
                }
            }
            else if (tabHeader == (Application.Current.TryFindResource("Loc_TabShaders") as string))
            {
                RefreshLocalAddonsList("shaderpacks", ListLocalShaders);

                if (ListShaderSearchResults.ItemsSource == null)
                {
                    txtSearchShader.Text = "";
                    await SearchAddon("shader", "", txtShaderSearchStatus, ListShaderSearchResults);
                }
            }
        }

        private void BtnShowModpacksDialog_Click(object sender, RoutedEventArgs e)
        {
            ModpacksDialogOverlay.Visibility = Visibility.Visible;
            txtSearchModpack.Text = "";
            _ = PerformModpackSearchAsync();
        }

        private void BtnCloseModpacksDialog_Click(object sender, RoutedEventArgs e)
        {
            ModpacksDialogOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnSearchModpack_Click(object sender, RoutedEventArgs e)
        {
            await PerformModpackSearchAsync();

        }

        private async void TxtSearchModpack_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformModpackSearchAsync();
            }
        }

        private async void CbModpackSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModpacksDialogOverlay != null && ModpacksDialogOverlay.Visibility == Visibility.Visible)
            {
                await PerformModpackSearchAsync();
            }
        }

        private async Task PerformModpackSearchAsync()
        {
            string query = txtSearchModpack.Text.Trim();

            string sortIndex = "downloads";
            if (cbModpackSort.SelectedItem is ComboBoxItem cbItem && cbItem.Tag != null)
            {
                sortIndex = cbItem.Tag.ToString() ?? "";
            }

            ModpackSearchProgress.Visibility = Visibility.Visible;
            ListModpackSearchResults.Visibility = Visibility.Collapsed;

            string popText = Application.Current.TryFindResource("Loc_LoadingPopularModpacks") as string ?? "Завантаження популярних збірок...";
            string searchTxt = Application.Current.TryFindResource("Loc_SearchingModpacks") as string ?? "Шукаємо збірки...";
            txtModpackSearchStatus.Text = string.IsNullOrEmpty(query) ? popText : searchTxt;
            txtModpackSearchStatus.Visibility = Visibility.Visible;

            _modSearchResults.Clear();
            ListModpackSearchResults.ItemsSource = _modSearchResults;

            try
            {
                string facets = "[[\"project_type:modpack\"]]";
                string url = $"https://api.modrinth.com/v2/search?query={Uri.EscapeDataString(query)}&facets={Uri.EscapeDataString(facets)}&index={sortIndex}&limit=50";

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<ModrinthSearchResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (searchResult != null && searchResult.Hits != null && searchResult.Hits.Count > 0)
                {
                    foreach (var hit in searchResult.Hits)
                    {
                        _modSearchResults.Add(new ModSearchResult
                        {
                            ProjectId = hit.ProjectId,
                            Title = hit.Title,
                            Description = hit.Description,
                            Author = hit.Author,
                            IconUrl = hit.IconUrl
                        });
                    }
                    ListModpackSearchResults.Visibility = Visibility.Visible;
                    txtModpackSearchStatus.Visibility = Visibility.Collapsed;
                }
                else
                {
                    txtModpackSearchStatus.Text = Application.Current.TryFindResource("Loc_NothingFound") as string ?? "Нічого не знайдено :(";
                }
            }
            catch
            {
                txtModpackSearchStatus.Text = Application.Current.TryFindResource("Loc_ConnectionError") as string ?? "Помилка з'єднання з сервером.";
            }
            finally
            {
                ModpackSearchProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnInstallModpack_Click(object sender, RoutedEventArgs e)
        {

            var btn = (sender as System.Windows.Controls.Button)!;
            string projectId = btn.Tag?.ToString() ?? "";
            if (string.IsNullOrEmpty(projectId)) return;

            btn.Content = Application.Current.TryFindResource("Loc_AnalyzingModpack") as string ?? "Аналіз збірки...";
            btn.IsEnabled = false;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string tempDir = Path.Combine(appData, ".FDlauncher", "Temp", projectId);

            try
            {
                string url = $"https://api.modrinth.com/v2/project/{projectId}/version";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                var versionsArray = doc.RootElement.EnumerateArray();
                var latestVersion = versionsArray.First();

                string downloadUrl = latestVersion.GetProperty("files")[0].GetProperty("url").GetString() ?? "";

                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                string mrPackPath = Path.Combine(tempDir, "modpack.mrpack");

                btn.Content = Application.Current.TryFindResource("Loc_DownloadingArchive") as string ?? "Завантаження архіву...";
                byte[] packBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(mrPackPath, packBytes);

                btn.Content = Application.Current.TryFindResource("Loc_Extracting") as string ?? "Розпакування...";
                string extractDir = Path.Combine(tempDir, "extracted");
                ZipFile.ExtractToDirectory(mrPackPath, extractDir);

                string indexPath = Path.Combine(extractDir, "modrinth.index.json");
                string indexJson = await File.ReadAllTextAsync(indexPath);
                var index = JsonSerializer.Deserialize<MrPackIndex>(indexJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (index == null || index.Dependencies == null)
                {
                    ShowAlert("File index is corrupted.");
                    return;
                }

                string gameVer = index.Dependencies.ContainsKey("minecraft") ? index.Dependencies["minecraft"] : "1.20.1";

                string loaderType = "Vanilla";
                if (index.Dependencies.ContainsKey("fabric-loader")) loaderType = "Fabric";
                else if (index.Dependencies.ContainsKey("forge")) loaderType = "Forge";
                else if (index.Dependencies.ContainsKey("neoforge")) loaderType = "NeoForge";

                string newInstanceName = Application.Current.TryFindResource("Loc_NewInstance") as string ?? "Нова збірка";

                var searchItem = _modSearchResults.FirstOrDefault(m => m.ProjectId == projectId);
                if (searchItem != null && !string.IsNullOrWhiteSpace(searchItem.Title))
                {
                    newInstanceName = searchItem.Title;
                }
                var newInstance = new GameInstance
                {
                    Name = newInstanceName,
                    IconSymbol = "Box24",
                    GameVersion = gameVer,
                    LoaderType = loaderType
                };

                if (InstancesList.Any(i => i.Name == newInstanceName))
                {
                    newInstance.Name += $" ({DateTime.Now.Second})";
                }

                InstancesList.Add(newInstance);
                ConfigManager.Data.Instances.Add(newInstance);
                ConfigManager.Save();

                string instanceDir = Path.Combine(appData, ".FDlauncher", "Instances", newInstance.Name);
                Directory.CreateDirectory(instanceDir);

                string overridesDir = Path.Combine(extractDir, "overrides");
                if (Directory.Exists(overridesDir))
                {
                    CopyDirectory(overridesDir, instanceDir);
                }

                int totalMods = index.Files.Count;
                int currentMod = 0;
                string modsFmt = Application.Current.TryFindResource("Loc_ModsProgress") as string ?? "Моди: {0} / {1}";

                foreach (var fileInfo in index.Files)
                {
                    currentMod++;
                    btn.Content = string.Format(modsFmt, currentMod, totalMods);

                    string modDownloadUrl = fileInfo.Downloads[0];
                    string targetFilePath = Path.Combine(instanceDir, fileInfo.Path);

                    string? targetFileDir = Path.GetDirectoryName(targetFilePath);
                    if (!string.IsNullOrEmpty(targetFileDir))
                    {
                        if (!Directory.Exists(targetFileDir))
                            Directory.CreateDirectory(targetFileDir);
                    }

                    try
                    {
                        byte[] modBytes = await _httpClient.GetByteArrayAsync(modDownloadUrl);
                        await File.WriteAllBytesAsync(targetFilePath, modBytes);
                    }
                    catch
                    {
                    }
                }

                btn.Content = Application.Current.TryFindResource("Loc_Done") as string ?? "Готово!";
                string successFmt = Application.Current.TryFindResource("Loc_ModpackInstallSuccess") as string ?? "Збірку успішно встановлено!\nВерсія: {0} ({1})\nМодів завантажено: {2}";
                ShowAlert(string.Format(successFmt, gameVer, loaderType, totalMods));

                ModpacksDialogOverlay.Visibility = Visibility.Collapsed;
                ListInstances.SelectedItem = newInstance;
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current.TryFindResource("Loc_ModpackInstallError") as string ?? "Помилка встановлення збірки:\n{0}";
                ShowAlert(string.Format(errFmt, ex.Message));
                btn.Content = Application.Current.TryFindResource("Loc_ErrorFallback") as string ?? "Помилка";
                btn.IsEnabled = true;
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
                btn.ClearValue(System.Windows.Controls.Button.ContentProperty);
                btn.ClearValue(System.Windows.Controls.Button.IsEnabledProperty);
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string destDir = Path.Combine(targetDir, Path.GetFileName(directory));
                CopyDirectory(directory, destDir);
            }
        }
        private async void BtnSearchResPack_Click(object sender, RoutedEventArgs e)
        {
            await SearchAddon("resourcepack", txtSearchResPack.Text, txtResPackSearchStatus, ListResPackSearchResults);
        }

        private void TxtSearchResPack_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) BtnSearchResPack_Click(sender, e);
        }

        private async void BtnDownloadResPack_Click(object sender, RoutedEventArgs e)
        {
            await DownloadAddon(sender, "resourcepack", "resourcepacks");
        }

        private void BtnDeleteLocalResPack_Click(object sender, RoutedEventArgs e)
        {
            DeleteLocalAddon(sender, "resourcepacks", ListLocalResPacks);
        }

        private async void BtnSearchShader_Click(object sender, RoutedEventArgs e)
        {
            await SearchAddon("shader", txtSearchShader.Text, txtShaderSearchStatus, ListShaderSearchResults);
        }

        private void TxtSearchShader_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) BtnSearchShader_Click(sender, e);
        }

        private async void BtnDownloadShader_Click(object sender, RoutedEventArgs e)
        {
            await DownloadAddon(sender, "shader", "shaderpacks");
        }

        private void BtnDeleteLocalShader_Click(object sender, RoutedEventArgs e)
        {
            DeleteLocalAddon(sender, "shaderpacks", ListLocalShaders);
        }

        private async System.Threading.Tasks.Task SearchAddon(string type, string query, System.Windows.Controls.TextBlock statusText, Wpf.Ui.Controls.ListView resultsList)
        {
            if (_editingInstance == null) return;

            statusText.Visibility = Visibility.Visible;

            string currentVer = _editingInstance.GameVersion ?? "???";
            statusText.Text = $"Пошук для версії: {currentVer}...";

            resultsList.Visibility = Visibility.Collapsed;

            try
            {
                var service = new launcher_m.Core.ModrinthService();

                var results = await service.SearchAddons(query, type, currentVer);

                resultsList.ItemsSource = results;
                resultsList.Visibility = Visibility.Visible;
                statusText.Visibility = Visibility.Collapsed;

                if (results == null || results.Count == 0)
                {
                    statusText.Visibility = Visibility.Visible;
                    statusText.Text = $"Нічого не знайдено (Версія: {currentVer})";
                }
            }
            catch (Exception ex)
            {
                statusText.Visibility = Visibility.Collapsed;
                ShowAlert($"Помилка зв'язку: {ex.Message}");
            }
        }


        private async System.Threading.Tasks.Task DownloadAddon(object sender, string type, string folderName)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string projectId)
            {
                if (_editingInstance == null) return;

                btn.IsEnabled = false;
                var oldContent = btn.Content;
                btn.Content = "Качаю...";

                try
                {
                    var service = new launcher_m.Core.ModrinthService();
                    string rawVersion = _editingInstance.GameVersion ?? "1.20.1";
                    string cleanVersion = System.Text.RegularExpressions.Regex.Match(rawVersion, @"\d+\.\d+(\.\d+)?").Value;

                    var downloadInfo = await service.GetDownloadInfo(projectId, cleanVersion);

                    if (downloadInfo != null)
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string destPath = System.IO.Path.Combine(appData, ".FDlauncher", "Instances", _editingInstance.Name, folderName);

                        if (!System.IO.Directory.Exists(destPath)) System.IO.Directory.CreateDirectory(destPath);

                        string filePath = System.IO.Path.Combine(destPath, downloadInfo.Value.FileName);

                        using (var client = new System.Net.Http.HttpClient())
                        {
                            var data = await client.GetByteArrayAsync(downloadInfo.Value.Url);
                            await System.IO.File.WriteAllBytesAsync(filePath, data);
                        }

                        RefreshLocalAddonsList(folderName, folderName == "resourcepacks" ? ListLocalResPacks : ListLocalShaders);
                    }
                    else
                    {
                        ShowAlert("Файл не знайдено для версії " + cleanVersion);
                    }
                }
                catch (Exception ex)
                {
                    ShowAlert($"Помилка завантаження: {ex.Message}");
                }
                finally
                {
                    btn.IsEnabled = true;
                    btn.Content = oldContent;
                }
            }
        }

        private void DeleteLocalAddon(object sender, string folderName, Wpf.Ui.Controls.ListView targetList)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is string fullPath)
            {
                try
                {
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                    RefreshLocalAddonsList(folderName, targetList);
                }
                catch { }
            }
        }

        public void RefreshLocalAddonsList(string folderName, Wpf.Ui.Controls.ListView targetList)
        {
            if (_editingInstance == null) return;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = System.IO.Path.Combine(appData, ".FDlauncher", "Instances", _editingInstance.Name, folderName);

            if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

            var files = System.IO.Directory.GetFiles(path)
                .Select(f => new { FileName = System.IO.Path.GetFileName(f), FullPath = f })
                .ToList();

            targetList.ItemsSource = files;
        }

        public class ModSearchResult : INotifyPropertyChanged
        {
            public string ProjectId { get; set; } = null!;
            public string Title { get; set; } = null!;
            public string Description { get; set; } = null!;
            public string Author { get; set; } = null!;
            public string? IconUrl { get; set; }

            private bool _isInstalled;
            public bool IsInstalled
            {
                get => _isInstalled;
                set { _isInstalled = value; OnPropertyChanged(nameof(IsInstalled)); }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class ModrinthSearchResponse
        {
            [JsonPropertyName("hits")]
            public List<ModrinthHit> Hits { get; set; } = new();
        }

        public class ModrinthHit
        {
            [JsonPropertyName("project_id")]
            public string ProjectId { get; set; } = null!;
            [JsonPropertyName("title")]
            public string Title { get; set; } = null!;
            [JsonPropertyName("description")]
            public string Description { get; set; } = null!;
            [JsonPropertyName("author")]
            public string Author { get; set; } = null!;
            [JsonPropertyName("icon_url")]
            public string? IconUrl { get; set; }
        }

        public class LocalMod
        {
            public string FileName { get; set; } = null!;
            public string FullPath { get; set; } = null!;
            public string FileSize { get; set; } = null!;
        }

        public class MrPackIndex
        {
            [JsonPropertyName("dependencies")]
            public Dictionary<string, string> Dependencies { get; set; } = new();

            [JsonPropertyName("files")]
            public List<MrPackFile> Files { get; set; } = new();
        }

        public class MrPackFile
        {
            [JsonPropertyName("path")]
            public required string Path { get; set; }

            [JsonPropertyName("downloads")]
            public List<string> Downloads { get; set; } = new();
        }
    }
}