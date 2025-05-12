using Caliburn.Micro;
using DjmaxRandomSelectorV.Messages;
using DjmaxRandomSelectorV.Models;
using Dmrsv.RandomSelector;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace DjmaxRandomSelectorV.ViewModels
{
    public class SettingViewModel : Screen
    {
        private const string ConfigPath = @"DMRSV3_Data\Config.json";
        private readonly IEventAggregator _eventAggregator;
        private readonly IFileManager _fileManager;
        private readonly SettingMessage _message;
        private readonly List<Category> _categories;
        private readonly List<string> _gameLanguages;
        private int _currentLanguageIndex;

        public ICommand PreviousLanguageCommand { get; }
        public ICommand NextLanguageCommand { get; }

        public bool IsPlaylist
        {
            get { return _message.FilterType == FilterType.Playlist; }
            set
            {
                _message.FilterType = value ? FilterType.Playlist : FilterType.Query;
                NotifyOfPropertyChange();
            }
        }
        public int InputDelay
        {
            get { return _message.InputInterval; }
            set
            {
                _message.InputInterval = value;
                NotifyOfPropertyChange();
            }
        }
        public bool SavesRecents
        {
            get { return _message.SavesExclusion; }
            set
            {
                _message.SavesExclusion = value;
                NotifyOfPropertyChange();
            }
        }
        public string GameLanguage
        {
            get { return _message.GameLanguage; }
            set
            {
                _message.GameLanguage = value;
                NotifyOfPropertyChange();
            }
        }
        public BindableCollection<ListUpdater> CategoryUpdaters { get; }

        public SettingViewModel(IEventAggregator eventAggregator, IFileManager fileManager)
        {
            _eventAggregator = eventAggregator;
            _fileManager = fileManager;

            var config = IoC.Get<Dmrsv3Configuration>();
            _message = new SettingMessage()
            {
                FilterType = config.FilterType,
                InputInterval = config.InputDelay,
                SavesExclusion = config.SavesRecents,
                OwnedDlcs = config.OwnedDlcs.ConvertAll(x => x),
                GameLanguage = config.GameLanguage
            };

            _categories = IoC.Get<CategoryContainer>().GetCategories();
            _categories.RemoveAll(x => string.IsNullOrEmpty(x.SteamId) && x.Type != 3); //TODO: use enum
            var updaters = _categories.ConvertAll(x => new ListUpdater(x.Name, x.Id, _message.OwnedDlcs));
            CategoryUpdaters = new BindableCollection<ListUpdater>(updaters);

            _gameLanguages = IoC.Get<List<string>>();
            _currentLanguageIndex = _gameLanguages.IndexOf(_message.GameLanguage);

            PreviousLanguageCommand = new RelayCommand(OnPreviousLanguageClick);
            NextLanguageCommand = new RelayCommand(OnNextLanguageClick);
        }

        private void OnPreviousLanguageClick()
        {
            _currentLanguageIndex = (_currentLanguageIndex - 1 + _gameLanguages.Count) % _gameLanguages.Count;
            GameLanguage = _gameLanguages[_currentLanguageIndex];
        }

        private void OnNextLanguageClick()
        {
            _currentLanguageIndex = (_currentLanguageIndex + 1) % _gameLanguages.Count;
            GameLanguage = _gameLanguages[_currentLanguageIndex];
        }

        public void DetectDlcs()
        {
            Dictionary<string, string> dlcCodes = _categories.Where(x => x.SteamId is not null).ToDictionary(x => x.SteamId, x => x.Id);

            var ownedDlcs = _message.OwnedDlcs;
            ownedDlcs.Clear();

            string steamKeyName = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam";
            string steamPath = Registry.GetValue(steamKeyName, "InstallPath", null).ToString();
            
            var libraryPath = new DirectoryInfo($"{steamPath}\\appcache\\librarycache");

            foreach (DirectoryInfo dir in libraryPath.GetDirectories())
            {
                string dlc = dlcCodes.GetValueOrDefault(dir.Name, null);
                if (!string.IsNullOrEmpty(dlc))
                {
                    ownedDlcs.Add(dlc);
                }
            }

            CategoryUpdaters.Refresh();
            MessageBox.Show($"{ownedDlcs.Count} DLCs are detected.",
                "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void Apply()
        {
            var config = IoC.Get<Dmrsv3Configuration>();
            config.FilterType = _message.FilterType;
            config.InputDelay = _message.InputInterval;
            config.SavesRecents = _message.SavesExclusion;
            config.OwnedDlcs = _message.OwnedDlcs.ConvertAll(x => x);
            config.GameLanguage = _message.GameLanguage;

            _fileManager.Export(config, ConfigPath);
            _eventAggregator.PublishOnUIThreadAsync(_message);
            TryCloseAsync(true);
        }

        public void Cancel()
        {
            TryCloseAsync(false);
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly System.Action _execute;
        private readonly System.Func<bool> _canExecute;

        public RelayCommand(System.Action execute, System.Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
