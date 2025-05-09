using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using DjmaxRandomSelectorV.Models;
using Dmrsv.RandomSelector;
using Dmrsv.RandomSelector.Enums;

namespace DjmaxRandomSelectorV
{
    public class TrackDB
    {
        private const string AllTrackFilePath = @"DMRSV3_Data\AllTrackList.json";
        private const string TrackTitleFilePath = @"DMRSV3_Data\TrackTitle.{0}.json";

        private readonly IFileManager _fileManager;
        private Dictionary<string, string> _trackTitleMappings;

        private string[] _basicCategories;
        private LinkDiscItem[] _linkDisc;

        public IReadOnlyList<Track> AllTrack { get; private set; }

        public IEnumerable<Track> Playable => AllTrack.Where(t => t.IsPlayable);
        
        public TrackDB(IFileManager fileManager)
        {
            _fileManager = fileManager;
        }

        public void Initialize(Dmrsv3AppData appdata)
        {
            _basicCategories = appdata.BasicCategories;
            _linkDisc = appdata.LinkDisc;
        }

        private void LoadTrackTitleMapping(string language)
        {
            if (language == "ko_KR") return;

            try
            {
                var mappingPath = string.Format(TrackTitleFilePath, language);
                _trackTitleMappings = _fileManager.Import<Dictionary<string, string>>(mappingPath);
            }
            catch (Exception)
            {
                // If mapping file doesn't exist or is invalid, use Korean titles
                _trackTitleMappings = new Dictionary<string, string>();
            }
        }

        public void ImportDB()
        {
            var db = _fileManager.Import<List<VArchiveDBTrack>>(AllTrackFilePath);
            AllTrack = db.ConvertAll(x =>
            {
                var info = new MusicInfo()
                {
                    Id = x.Title,
                    Title = x.Name,
                    Composer = x.Composer,
                    Category = x.DlcCode
                };
                return new Track()
                {
                    Info = info,
                    Patterns = x.Patterns
                                .SelectMany(bt => bt.Value, (bt, df) => new Pattern()
                                {
                                    Info = info,
                                    Button = bt.Key.AsButtonTunes(),
                                    Difficulty = df.Key.AsDifficulty(),
                                    Level = df.Value.Level
                                })
                                .OrderBy(p => p.PatternId)
                                .ToArray()
                };
            }).AsReadOnly();
        }

        public void ApplyLanguage(string language)
        {
            if (language == "ko_KR") return;
            
            LoadTrackTitleMapping(language);
            AllTrack = AllTrack.Select(track =>
            {
                var info = track.Info with { Title = GetLocalizedTitle(track.Info.Title) };
                return track with { Info = info };
            }).ToList().AsReadOnly();
        }

        private string GetLocalizedTitle(string koreanTitle)
        {
            if (_trackTitleMappings.TryGetValue(koreanTitle, out var localizedTitle))
            {
                return localizedTitle;
            }

            return koreanTitle;
        }

        public void SetPlayable(IEnumerable<string> ownedDlcs)
        {
            var categories = ownedDlcs.Concat(_basicCategories);
            var exclusions = _linkDisc.Where(x => !x.RequiredDlc.Any(dlcs => dlcs.All(dlc => ownedDlcs.Contains(dlc))))
                                      .Select(x => x.Id);
            AllTrack = AllTrack
                .Select(track => track with { IsPlayable = categories.Contains(track.Category) && !exclusions.Contains(track.Id) })
                .ToList()
                .AsReadOnly();
        }

        public record VArchiveDBTrack
        {
            public int Title { get; init; }
            public string Name { get; init; }
            public string Composer { get; init; }
            public string DlcCode { get; init; }
            public string Dlc { get; init; }
            public Dictionary<string, Dictionary<string, VArchiveDBPattern>> Patterns { get; init; }
        }

        public record VArchiveDBPattern
        {
            public int Level { get; init; }
            public double Floor { get; init; }
            public int Rating { get; init; }
        }
    }
}
