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
        private void BtnSupportAuthor_Click(object sender, RoutedEventArgs e) => OpenLink("https://donatello.to/launcher_minecraft");
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
    }
}