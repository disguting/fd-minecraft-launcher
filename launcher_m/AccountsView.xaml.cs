using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using launcher_m.Core;
using launcher_m.Models;

namespace launcher_m
{
    public partial class AccountsView : UserControl
    {
        public ObservableCollection<AccountProfile> AccountsList { get; set; } = new();

        private AccountProfile? _accountPendingDeletion;

        public AccountsView()
        {
            InitializeComponent();
            LoadAccounts();
        }

        private void LoadAccounts()
        {
            AccountsList = new ObservableCollection<AccountProfile>(ConfigManager.Data.Accounts);
            ListAccounts.ItemsSource = AccountsList;

            var activeId = ConfigManager.Data.Settings.ActiveAccountId;
            ListAccounts.SelectedItem = AccountsList.FirstOrDefault(a => a.Id == activeId);
        }

        private void ListAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListAccounts.SelectedItem is AccountProfile selectedAccount)
            {
                ConfigManager.Data.Settings.ActiveAccountId = selectedAccount.Id;
                ConfigManager.Save();

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.UpdateStatus();
                }
            }
        }

        private void BtnShowOfflineDialog_Click(object sender, RoutedEventArgs e)
        {
            txtOfflineUsername.Text = "";
            OfflineDialogOverlay.Visibility = Visibility.Visible;
        }

        private void BtnCloseOfflineDialog_Click(object sender, RoutedEventArgs e)
        {
            OfflineDialogOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnSaveOffline_Click(object sender, RoutedEventArgs e)
        {
            string nickname = txtOfflineUsername.Text.Trim();

            if (string.IsNullOrEmpty(nickname))
            {
                string alertMsg = Application.Current.TryFindResource("Loc_AlertEmptyNickname") as string ?? "Будь ласка, введіть нікнейм!";
                ShowAlert(alertMsg);
                return;
            }

            var newAccount = new AccountProfile
            {
                Username = nickname,
                IsOffline = true,
                LastLoginType = "Offline"
            };

            AccountsList.Add(newAccount);
            ConfigManager.Data.Accounts.Add(newAccount);
            ConfigManager.Data.Settings.ActiveAccountId = newAccount.Id;
            ConfigManager.Save();

            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                mainWin.UpdateStatus();
            }

            OfflineDialogOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnAddMicrosoft_Click(object sender, RoutedEventArgs e)
        {
            BtnAddMicrosoft.IsEnabled = false;

            try
            {
                ShowAlert(Application.Current.TryFindResource("Loc_AuthOpeningBrowser") as string ?? "Відкриваємо браузер...");

                var authService = new MicrosoftAuthService();
                var session = await authService.LoginInteractive();

                if (session == null) return;

                var existingAcc = ConfigManager.Data.Accounts.FirstOrDefault(a => a.UUID == session.UUID);

                if (existingAcc != null)
                {
                    existingAcc.Username = session!.Username!;
                    existingAcc.AccessToken = session!.AccessToken!;
                    existingAcc.IsOffline = false;
                    existingAcc.LastLoginType = "Microsoft";

                    ConfigManager.Data.Settings.ActiveAccountId = existingAcc.Id;
                }
                else
                {

                    var newAccount = new AccountProfile
                    {
                        Username = session!.Username!,
                        UUID = session!.UUID!,
                        AccessToken = session!.AccessToken!, 
                        IsOffline = false,
                        LastLoginType = "Microsoft"
                    };

                    AccountsList.Add(newAccount);
                    ConfigManager.Data.Accounts.Add(newAccount);
                    ConfigManager.Data.Settings.ActiveAccountId = newAccount.Id;
                }

                ConfigManager.Save();

                LoadAccounts();

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.UpdateStatus();
                }

                AlertOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                string errFmt = Application.Current.TryFindResource("Loc_AuthFailed") as string ?? "Помилка: {0}";
                ShowAlert(string.Format(errFmt, ex.Message));
            }
            finally
            {
                BtnAddMicrosoft.IsEnabled = true;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListAccounts.SelectedItem is AccountProfile selectedAccount)
            {
                _accountPendingDeletion = selectedAccount;
                string formatMsg = Application.Current.TryFindResource("Loc_ConfirmDeleteAccountSpecific") as string ?? "Видалити акаунт '{0}'?";
                TxtDeleteMessage.Text = string.Format(formatMsg, selectedAccount.Username);
                DeleteConfirmOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                ShowAlert(Application.Current.TryFindResource("Loc_AlertSelectAccountFirst") as string ?? "Виберіть акаунт!");
            }
        }

        private void BtnConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_accountPendingDeletion != null)
            {
                AccountsList.Remove(_accountPendingDeletion);
                var accInDb = ConfigManager.Data.Accounts.FirstOrDefault(a => a.Id == _accountPendingDeletion.Id);
                if (accInDb != null) ConfigManager.Data.Accounts.Remove(accInDb);

                if (ConfigManager.Data.Settings.ActiveAccountId == _accountPendingDeletion.Id)
                    ConfigManager.Data.Settings.ActiveAccountId = string.Empty;

                ConfigManager.Save();
                if (Application.Current.MainWindow is MainWindow mainWin) mainWin.UpdateStatus();
                _accountPendingDeletion = null;
            }
            DeleteConfirmOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnCancelDelete_Click(object sender, RoutedEventArgs e)
        {
            _accountPendingDeletion = null;
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
    }
}