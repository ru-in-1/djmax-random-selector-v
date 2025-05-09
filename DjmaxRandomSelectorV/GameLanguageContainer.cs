using DjmaxRandomSelectorV.Models;
using System.Collections.Generic;

namespace DjmaxRandomSelectorV
{
    public class GameLanguageContainer
    {
        private List<GameLanguage> _gameLanguages;

        public List<GameLanguage> GetGameLanguages()
        {
            return _gameLanguages.ConvertAll(x => x);
        }

        public void SetGameLanguages(Dmrsv3AppData appData)
        {
            _gameLanguages = new List<GameLanguage>(appData.GameLanguages);
        }
    }
} 