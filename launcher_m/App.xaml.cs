using System.Windows;
using launcher_m.Core;
using System;
using System.Linq;
using Wpf.Ui.Appearance;
using System.Drawing;

namespace launcher_m
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigManager.Load();
            SetLanguage(ConfigManager.Data.Settings.Language);

            //Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(
            //    System.Windows.Media.Color.FromRgb(76, 175, 80)
            //);

        }

        public void SetLanguage(string localeCode)
        {
            try
            {
                var dict = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/Locales/{localeCode}.xaml")
                };

                var oldDict = Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("/Locales/"));

                if (oldDict != null)
                {
                    Resources.MergedDictionaries.Remove(oldDict);
                }

                Resources.MergedDictionaries.Add(dict);

                ConfigManager.Data.Settings.Language = localeCode;
                ConfigManager.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка завантаження мови {localeCode}: {ex.Message}");
            }
        }
    }
}