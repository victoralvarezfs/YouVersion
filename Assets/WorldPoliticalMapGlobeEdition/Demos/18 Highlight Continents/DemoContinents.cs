using System.Collections.Generic;
using UnityEngine;

namespace WPM {

    public class DemoContinents : MonoBehaviour {

        public WorldMapGlobe map;

        Dictionary<string, List<Country>> continents = new Dictionary<string, List<Country>>();

        void Start () {

            // Group countries by continent
            foreach (Country country in map.countries) {
                if (continents.ContainsKey(country.continent)) {
                    continents[country.continent].Add(country);
                } else {
                    continents[country.continent] = new List<Country> { country };
                }
            }

            map.OnCountryEnter += OnCountryEnter;
            map.OnCountryExit += OnCountryExit;
        }

        void OnCountryEnter (int countryIndex, int regionIndex) {
            string continent = map.countries[countryIndex].continent;
            if (continents.TryGetValue(continent, out List<Country> continentCountries)) {
                foreach (Country country in continentCountries) {
                    int cindex = map.GetCountryIndex(country.name);
                    map.ToggleCountrySurface(cindex, visible: true, Color.green);
                }
            }
        }

        void OnCountryExit (int countryIndex, int regionIndex) {
            string continent = map.countries[countryIndex].continent;
            if (continents.TryGetValue(continent, out List<Country> continentCountries)) {
                foreach (Country country in continentCountries) {
                    int cindex = map.GetCountryIndex(country.name);
                    map.ToggleCountrySurface(cindex, visible: false);
                }
            }
        }

    }

}