using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DjmaxRandomSelectorV
{
    public class UpdateManager
    {
        private const string VersionCheckUrl = "https://raw.githubusercontent.com/ru-in-1/djmax-random-selector-v/main/DjmaxRandomSelectorV/Version3.txt";
        private const string AllTrackDownloadUrl = "https://v-archive.net/db/songs.json";
        private const string AppdataDownloadUrl = "https://raw.githubusercontent.com/ru-in-1/djmax-random-selector-v/main/DjmaxRandomSelectorV/DMRSV3_Data/appdata.json";
        private const string TrackLangDownloadUrl = "https://raw.githubusercontent.com/ru-in-1/djmax-random-selector-v/main/DjmaxRandomSelectorV/DMRSV3_Data/TrackLang.json";
        private const string AllTrackFilePath = @"DMRSV3_Data\AllTrackList.json";
        private const string AppdataFilePath = @"DMRSV3_Data\appdata.json";
        private const string TrackLangFilePath = @"DMRSV3_Data\TrackLang.json";

        private readonly VersionContainer _container;
        private readonly IFileManager _fileManager;

        public UpdateManager(Dmrsv3Configuration config, VersionContainer container, IFileManager fileManager)
        {
            _container = container;
            _fileManager = fileManager;
            Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
            _container.CurrentAppVersion = _container.LatestAppVersion = assemblyVersion;
            _container.AllTrackVersion = config.AllTrackVersion;
            _container.AppdataVersion = config.AppdataVersion;
            _container.TrackLangVersion = config.TrackLangVersion;
        }

        public async Task UpdateAsync()
        {
            string[] versions; // [ app version, appdata version, track language version, notice header, notice body ]
            try
            {
                string result = await _fileManager.RequestAsync(VersionCheckUrl);
                versions = result.Split('\n');
            }
            catch
            {
                throw new Exception("Failed to check update.");
            }
            _container.LatestAppVersion = new Version(versions[0]);

            var tasks = new List<Task<int>>();
            // update all track
            long now = long.Parse(DateTime.Now.ToString("yyMMddHHmm"));
            long past = _container.AllTrackVersion;
            if (now > past || !File.Exists(AllTrackFilePath))
            {
                Debug.WriteLine("all track update start");
                tasks.Add(DownloadAllTrackAsync());
            }
            // update appdata
            if (!File.Exists(AllTrackFilePath)
                || versions[1].CompareTo(_container.AppdataVersion) > 0)
            {
                Debug.WriteLine("appdata update start");
                tasks.Add(DownloadAppdataAsync());
            }
            // update track language
            if (!File.Exists(TrackLangFilePath)
                || versions[1].CompareTo(_container.TrackLangVersion) > 0)
            {
                Debug.WriteLine("track language update start");
                tasks.Add(DownloadTrackLangAsync());
            }

            while (tasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(tasks);
                int result = await finishedTask;
                switch (result)
                {
                    case 0:
                        _container.AllTrackVersion = now;
                        break;
                    case 1:
                        _container.AppdataVersion = versions[1];
                        break;
                    case 2:
                        _container.TrackLangVersion = versions[4];
                        break;
                }
                tasks.Remove(finishedTask);
            }
        }

        private async Task<int> DownloadAllTrackAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(AllTrackDownloadUrl);
                _fileManager.Write(result, AllTrackFilePath);
            }
            catch
            {
                return -1;
            }
            return 0;
        }

        private async Task<int> DownloadAppdataAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(AppdataDownloadUrl);
                _fileManager.Write(result, AppdataFilePath);
            }
            catch
            {
                return -1;
            }
            return 1;
        }

        private async Task<int> DownloadTrackLangAsync()
        {
            try
            {
                string result = await _fileManager.RequestAsync(TrackLangDownloadUrl);
                _fileManager.Write(result, TrackLangFilePath);
            }
            catch
            {
                return -1;
            }
            return 2;
        }
    }
}
