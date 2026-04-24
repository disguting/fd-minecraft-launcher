using launcher_m.Core;
using launcher_m.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;

namespace launcher_m
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = ConfigManager.Data.Settings;
            slRam.Value = s.MaxRamMb;

            ToggleSnapshots.IsChecked = s.ShowSnapshots;
            ToggleOldVersions.IsChecked = s.ShowAlphaBeta;

            TxtJvmArgs.Text = s.JvmArguments;

            foreach (ComboBoxItem item in cbLanguage.Items)
            {
                if (item.Tag?.ToString() == s.Language) { cbLanguage.SelectedItem = item; break; }
            }
        }

        private async void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var s = ConfigManager.Data.Settings;

            s.MaxRamMb = (int)slRam.Value;
            s.ShowSnapshots = ToggleSnapshots.IsChecked ?? false;
            s.ShowAlphaBeta = ToggleOldVersions.IsChecked ?? false;
            s.JvmArguments = TxtJvmArgs.Text.Trim();

            if (cbLanguage.SelectedItem is ComboBoxItem langItem)
            {
                string newLang = langItem.Tag.ToString() ?? "uk-UA";
                ((App)Application.Current).SetLanguage(newLang);
            }

            ConfigManager.Save();

            TxtSaveStatus.Visibility = Visibility.Visible;
            await Task.Delay(2000);
            TxtSaveStatus.Visibility = Visibility.Collapsed;
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string rootDir = Path.Combine(appData, ".FDlauncher");

                if (!Directory.Exists(rootDir))
                {
                    Directory.CreateDirectory(rootDir);
                }

                Process.Start("explorer.exe", rootDir);
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current.TryFindResource("Loc_ErrorOpenFolder") as string ?? "Не вдалося відкрити папку: {0}";
                MessageBox.Show(string.Format(errFmt, ex.Message));
            }
        }

        private async void BtnClearTemp_Click(object sender, RoutedEventArgs e)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string tempDir = Path.Combine(appData, ".FDlauncher", "Temp");

            try
            {
                if (Directory.Exists(tempDir))
                {
                    await Task.Run(() => Directory.Delete(tempDir, true));
                    Directory.CreateDirectory(tempDir);

                    if (sender is Wpf.Ui.Controls.Button btn)
                    {
                        string originalContent = btn.Content?.ToString() ?? "";
                        btn.Content = Application.Current.TryFindResource("Loc_Cleared") as string ?? "Очищено!";
                        await Task.Delay(2000);
                        btn.Content = originalContent;
                    }
                }
                else
                {
                    if (sender is Wpf.Ui.Controls.Button btn)
                    {
                        btn.Content = Application.Current.TryFindResource("Loc_AlreadyClean") as string ?? "Вже чисто";
                        await Task.Delay(2000);
                        btn.Content = Application.Current.TryFindResource("Loc_ClearTempFolder") as string ?? "Очистити папку Temp";
                    }
                }
            }
            catch (IOException)
            {
                string inUseMsg = Application.Current.TryFindResource("Loc_ErrorTempInUse") as string ?? "Деякі файли в Temp зараз використовуються. Закрийте всі процеси гри та спробуйте ще раз.";
                MessageBox.Show(inUseMsg);
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current.TryFindResource("Loc_ErrorClearTemp") as string ?? "Помилка при очищенні: {0}";
                MessageBox.Show(string.Format(errFmt, ex.Message));
            }
        }
    }
}