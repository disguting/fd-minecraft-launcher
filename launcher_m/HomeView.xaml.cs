using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using launcher_m.Core;
using launcher_m.Models;

namespace launcher_m
{
    public partial class HomeView : UserControl
    {
        public ObservableCollection<GameInstance> Instances { get; set; } = new();

        public HomeView()
        {
            InitializeComponent();
            this.Loaded += async (s, e) => LoadInstances();
        }

        public void LoadInstances()
        {
            Instances = new ObservableCollection<GameInstance>(ConfigManager.Data.Instances);
            ListInstancesHome.ItemsSource = Instances;

            var activeId = ConfigManager.Data.Settings.ActiveInstanceId;
            var selected = Instances.FirstOrDefault(i => i.Id == activeId);

            if (selected != null)
                ListInstancesHome.SelectedItem = selected;
            else
                ListInstancesHome.SelectedItem = null;
        }

        private async void Card_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is GameInstance selected)
            {
                ListInstancesHome.SelectedItem = selected;
                ConfigManager.Data.Settings.ActiveInstanceId = selected.Id;

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.UpdateStatus();
                }

                await Task.Run(() => ConfigManager.Save());
            }
        }

        private void BtnSupportArmy_Click(object sender, RoutedEventArgs e) => OpenLink("https://u24.gov.ua/uk/donate");
        private void BtnSupportAuthor_Click(object sender, RoutedEventArgs e) => OpenLink("https://send.monobank.ua/jar/2iJkdH7vYZ");
        private void BtnReportBug_Click(object sender, RoutedEventArgs e) => OpenLink("https://t.me/+sD_T3k0IMMA3YzEy");

        private void OpenLink(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void BtnMoreOptions_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is FrameworkElement button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void MenuEditInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem &&
                menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu &&
                contextMenu.PlacementTarget is FrameworkElement button)
            {
                var selectedInstance = button.DataContext;

                if (selectedInstance != null)
                {
                    var mainWindow = System.Windows.Window.GetWindow(this) as MainWindow;

                    if (mainWindow != null)
                    {
                        mainWindow.OpenEditPage(selectedInstance);
                    }
                }
            }
        }
        private void MenuMoveUp_Click(object sender, RoutedEventArgs e)
        {
            MoveInstance(sender, -1);
        }

        private void MenuMoveDown_Click(object sender, RoutedEventArgs e)
        {
            MoveInstance(sender, 1);
        }

        private void MoveInstance(object sender, int direction)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem &&
                menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu &&
                contextMenu.PlacementTarget is FrameworkElement button)
            {
                var selectedInstance = button.DataContext as launcher_m.Models.GameInstance;

                if (selectedInstance != null)
                {
                    var uiList = ListInstancesHome.ItemsSource as System.Collections.IList;
                    if (uiList == null) return;

                    int currentIndex = uiList.IndexOf(selectedInstance);
                    int newIndex = currentIndex + direction;

                    if (currentIndex >= 0 && newIndex >= 0 && newIndex < uiList.Count)
                    {
                        if (uiList is System.Collections.ObjectModel.ObservableCollection<launcher_m.Models.GameInstance> obsList)
                        {
                            obsList.Move(currentIndex, newIndex);
                        }
                        else
                        {
                            uiList.RemoveAt(currentIndex);
                            uiList.Insert(newIndex, selectedInstance);
                            ListInstancesHome.Items.Refresh();
                        }

                        var configList = launcher_m.Core.ConfigManager.Data.Instances;
                        configList.Clear();
                        foreach (launcher_m.Models.GameInstance item in uiList)
                        {
                            configList.Add(item);
                        }

                        launcher_m.Core.ConfigManager.Save();
                    }
                }
            }
        }
    }
}