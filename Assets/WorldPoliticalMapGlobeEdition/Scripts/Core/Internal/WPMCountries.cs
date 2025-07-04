// World Political Map - Globe Edition for Unity - Main Script
// Created by Ramiro Oliva (Kronnect)
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WPM

//#define PAINT_MODE
//#define TRACE_CTL
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using WPM.ClipperLib;
using WPM.Poly2Tri;
using TMPro;
using System.Collections;
using System.IO;

namespace WPM {
    public partial class WorldMapGlobe : MonoBehaviour {

        #region Internal variables

        Country[] _countries;
        const string FRONTIERS_LAYER = "Frontiers";
        const string COUNTRY_ATTRIB_DEFAULT_FILENAME = "countriesAttrib";
        const string TEXT_ROOT = "TextRoot";

        // resources
        Material countryColoredMat, countryColoredAlphaMat, countryTexturizedMat;
        Material frontiersMatThinOpaque, frontiersMatThinAlpha, frontiersMatThickOpaque, frontiersMatThickAlpha, frontiersMatCurrent;
        Material inlandFrontiersMatOpaque, inlandFrontiersMatAlpha, inlandFrontiersMatCurrent;
        Material hudMatCountry;
        Material hudMatBlinker;
        
        // gameObjects
        GameObject countryRegionHighlightedObj;
        GameObject frontiersLayer, inlandFrontiersLayer;

        // cache
        Dictionary<Color, Material> countryColoredMatCache;
        readonly List<Country> highlightedCountries = new List<Country>();

        class FrontierSegment {
            public Vector3 p0, p1;
            public int repetitions = 0, countryIndex;
            public Region region;

            public FrontierSegment (Vector3 p0, Vector3 p1, int countryIndex, Region region) {
                this.p0 = p0;
                this.p1 = p1;
                this.countryIndex = countryIndex;
                this.region = region;
            }
        }

        // caché and gameObject lifetime control
        Vector3[][] frontiers;
        int[][] frontiersIndices;
        List<Vector3> frontiersPoints;
        Dictionary<int, FrontierSegment> frontiersCacheHit;
        List<Vector3> interiorFrontiersPoints;
        Vector3[][] inlandFrontiers;
        int[][] inlandFrontiersIndices;
        List<Vector3> inlandFrontiersPoints;

        /// <summary>
        /// Country look up dictionary. Used internally for fast searching of country names.
        /// </summary>
        Dictionary<string, int> _countryLookup;
        int lastCountryLookupCount = -1;

        Dictionary<string, int> countryLookup {
            get {
                CheckCountryArraysAreUpToDate();
                return _countryLookup;
            }
        }

        List<int> _countriesOrderedBySize;

        List<int> countriesOrderedBySize {
            get {
                CheckCountryArraysAreUpToDate();
                return _countriesOrderedBySize;
            }
        }

        int fadeCountryIndex;

        void CheckCountryArraysAreUpToDate () {
            if (_countries != null && _countries.Length > 0 && _countries[0] != null) {
                int countryCount = _countries.Length;
                if (_countryLookup != null && countryCount == lastCountryLookupCount)
                    return;
                if (_countryLookup == null) {
                    _countryLookup = new Dictionary<string, int>();
                } else {
                    _countryLookup.Clear();
                }
                if (_countriesOrderedBySize == null) {
                    _countriesOrderedBySize = new List<int>(countryCount);
                } else {
                    _countriesOrderedBySize.Clear();
                }
                for (int k = 0; k < countryCount; k++) {
                    _countryLookup[_countries[k].name] = k;
                    _countriesOrderedBySize.Add(k);
                }
                // Sort countries based on size
                _countriesOrderedBySize.Sort((int cIndex1, int cIndex2) => {
                    Country c1 = _countries[cIndex1];
                    Region r1 = c1.mainRegion;
                    Country c2 = _countries[cIndex2];
                    Region r2 = c2.mainRegion;
                    float r1Area = r1 != null ? r1.rect2DArea : 0;
                    float r2Area = r2 != null ? r2.rect2DArea : 0;
                    if (r1Area < r2Area) {
                        return -1;
                    } else if (r1Area > r2Area) {
                        return 1;
                    } else {
                        return 0;
                    }
                });
            } else {
                _countryLookup = new Dictionary<string, int>();
                _countriesOrderedBySize = new List<int>();
            }
            lastCountryLookupCount = _countryLookup.Count;
        }


        #endregion

        #region System initialization

        void ReadCountriesGeoData () {
            string frontiersFileName = _geodataResourcesPath + (_frontiersDetail == FRONTIERS_DETAIL.Low ? "/countries110" : "/countries10");

            TextAsset ta = null;
            if (_geodataFormat == GEODATA_FORMAT.BinaryFormat) {
                // try to load in binary
                ta = Resources.Load<TextAsset>(frontiersFileName + "_bin");
                if (ta != null) {
                    SetCountriesGeoDataBinary(ta.bytes);
                } else {
                    Debug.LogWarning("Geodata file is set to binary but the file can't be found or read, trying packed string format.");
                }
            }

            if (ta == null) {
                // load legacy packed string format
                ta = Resources.Load<TextAsset>(frontiersFileName);
                if (ta != null) {
                    SetCountriesGeoData(ta.text);
                } else {
                    Debug.LogWarning("Can't load countries packed string format file.");
                }
            }
            if (ta != null) {
                Resources.UnloadAsset(ta);
                ReloadCountryAttributes();
            }
        }

        void ReloadCountryAttributes () {
            TextAsset ta = Resources.Load<TextAsset>(_geodataResourcesPath + "/" + _countryAttributeFile);
            if (ta == null)
                return;
            SetCountriesAttributes(ta.text);
            Resources.UnloadAsset(ta);
        }

        void DestroyCountrySurfaces () {
            if (_countries == null) return;
            for (int k = 0; k < _countries.Length; k++) {
                Country c = _countries[k];
                c.DestroySurfaces();
            }
        }

        /// <summary>
        /// Loads country geodata information. Use GetCountryGeoData method to get the current country geodata.
        /// </summary>
        public void SetCountriesGeoData (string s) {
            DestroyCountrySurfaces();
            lastCountryLookupCount = -1;

            string[] countryList = s.Split(SPLIT_SEP_PIPE, StringSplitOptions.RemoveEmptyEntries);
            int countryCount = countryList.Length;
            List<Country> newCountries = new List<Country>(countryCount);
            Vector2 min, max;
            Vector2 latlonCenter = new Vector2();

            for (int k = 0; k < countryCount; k++) {

                string[] countryInfo = countryList[k].Split(SPLIT_SEP_DOLLAR, StringSplitOptions.None);
                int countryInfoLength = countryInfo.Length;

                string name = countryInfo[0];

                string continent = countryInfo[1];

                Country country = new Country(name, continent);

                string[] regions;
                int regionCount;
                if (countryInfoLength < 3) {
                    regions = null;
                    regionCount = 0;
                } else {
                    regions = countryInfo[2].Split(SPLIT_SEP_ASTERISK, StringSplitOptions.RemoveEmptyEntries);
                    regionCount = regions.Length;
                }
                country.regions = new List<Region>(regionCount);

                float maxVol = 0;
                Vector2 minCountry = new Vector2(1000, 1000);
                Vector2 maxCountry = -minCountry;

                for (int r = 0; r < regionCount; r++) {
                    string[] coordinates = regions[r].Split(SPLIT_SEP_SEMICOLON, StringSplitOptions.RemoveEmptyEntries);
                    int coorCount = coordinates.Length;
                    if (coorCount < 3)
                        continue;
                    min.x = min.y = 1000;
                    max.x = max.y = -1000;
                    Region countryRegion = new Region(country, country.regions.Count);
                    Vector2[] latlon = new Vector2[coorCount];
                    for (int c = 0; c < coorCount; c++) {
                        float x, y;
                        GetPointFromPackedString(coordinates[c], out x, out y);
                        if (x < min.x)
                            min.x = x;
                        if (x > max.x)
                            max.x = x;
                        if (y < min.y)
                            min.y = y;
                        if (y > max.y)
                            max.y = y;
                        latlon[c].x = x;
                        latlon[c].y = y;
                    }

                    countryRegion.latlon = latlon;
                    float dlon = max.y - min.y;
                    if (dlon > 180 && min.y > -180 && max.y < 180) { // fix wrapped countries rect2D around 180 longitude line
                        max.y += 360;
                    }
                    FastVector.Average(ref min, ref max, ref latlonCenter);
                    countryRegion.latlonCenter = latlonCenter;

                    // Calculate country bounding rect
                    if (min.x < minCountry.x)
                        minCountry.x = min.x;
                    if (min.y < minCountry.y)
                        minCountry.y = min.y;
                    if (max.x > maxCountry.x)
                        maxCountry.x = max.x;
                    if (max.y > maxCountry.y)
                        maxCountry.y = max.y;
                    countryRegion.latlonRect2D = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
                    countryRegion.rect2DArea = countryRegion.latlonRect2D.width * countryRegion.latlonRect2D.height;
                    float vol = FastVector.SqrDistance(ref max, ref min);
                    if (vol > maxVol) {
                        maxVol = vol;
                        country.mainRegionIndex = country.regions.Count;
                        country.latlonCenter = countryRegion.latlonCenter;
                    }
                    country.regions.Add(countryRegion);
                }
                // hidden
                if (countryInfoLength >= 4) {
                    country.hidden = "1".Equals(countryInfo[3]);
                }
                // fip 10 4
                if (countryInfoLength >= 5) {
                    country.fips10_4 = countryInfo[4];
                }
                // iso A2
                if (countryInfoLength >= 6) {
                    country.iso_a2 = countryInfo[5];
                }
                // iso A3
                if (countryInfoLength >= 7) {
                    country.iso_a3 = countryInfo[6];
                }
                // iso N3
                if (countryInfoLength >= 8) {
                    country.iso_n3 = countryInfo[7];
                }
                // label visibility
                if (countryInfoLength >= 9) {
                    country.labelVisible = "1".Equals(countryInfo[8]);
                }
                country.regionsRect2D = new Rect(minCountry.x, minCountry.y, Math.Abs(maxCountry.x - minCountry.x), Mathf.Abs(maxCountry.y - minCountry.y));
                newCountries.Add(country);
            }

            newCountries.Sort((Country c1, Country c2) => {
                return c1.mainRegionArea.CompareTo(c2.mainRegionArea);
            });
            countries = newCountries.ToArray();


            OptimizeFrontiers();
        }


        public void SetCountriesGeoDataBinary (byte[] data) {
            if (data == null || data.Length < 4) return;

            DestroyCountrySurfaces();
            lastCountryLookupCount = -1;

            MemoryStream ms = new MemoryStream(data);
            BinaryReader br = new BinaryReader(ms, Encoding.UTF8);

            int countryCount = br.ReadUInt16();
            List<Country> newCountries = new List<Country>(countryCount);
            Vector2 min = Misc.Vector2one * 10;
            Vector2 max = -min;
            Vector2 latlonCenter = new Vector2();

            for (int k = 0; k < countryCount; k++) {
                string name = br.ReadString();
                string continent = br.ReadString();
                Country country = new Country(name, continent);
                country.regions = new List<Region>();
                float maxVol = 0;
                Vector2 minCountry = new Vector2(10, 10);
                Vector2 maxCountry = -minCountry;

                int regionsCount = br.ReadUInt16();
                for (int r = 0; r < regionsCount; r++) {
                    uint coorCount = br.ReadUInt32();
                    min.x = min.y = 1000;
                    max.x = max.y = -1000;
                    Region countryRegion = new Region(country, country.regions.Count);
                    Vector2[] newPoints = new Vector2[coorCount];
                    for (int c = 0; c < coorCount; c++) {
                        float x = br.ReadSingle();
                        float y = br.ReadSingle();
                        if (x < min.x)
                            min.x = x;
                        if (x > max.x)
                            max.x = x;
                        if (y < min.y)
                            min.y = y;
                        if (y > max.y)
                            max.y = y;
                        newPoints[c].x = x;
                        newPoints[c].y = y;
                    }

                    countryRegion.latlon = newPoints;
                    float dlon = max.y - min.y;
                    if (dlon > 180 && min.y > -180 && max.y < 180) { // fix wrapped countries rect2D around 180 longitude line
                        max.y += 360;
                    }
                    FastVector.Average(ref min, ref max, ref latlonCenter);
                    countryRegion.latlonCenter = latlonCenter;

                    countryRegion.sanitized = true;

                    // Calculate country bounding rect
                    if (min.x < minCountry.x)
                        minCountry.x = min.x;
                    if (min.y < minCountry.y)
                        minCountry.y = min.y;
                    if (max.x > maxCountry.x)
                        maxCountry.x = max.x;
                    if (max.y > maxCountry.y)
                        maxCountry.y = max.y;
                    countryRegion.latlonRect2D = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
                    countryRegion.rect2DArea = countryRegion.latlonRect2D.width * countryRegion.latlonRect2D.height;
                    float vol = FastVector.SqrDistance(ref max, ref min);
                    if (vol > maxVol) {
                        maxVol = vol;
                        country.mainRegionIndex = country.regions.Count;
                        country.latlonCenter = countryRegion.latlonCenter;
                    }
                    country.regions.Add(countryRegion);
                }
                country.hidden = br.ReadBoolean();
                country.fips10_4 = br.ReadString();
                country.iso_a2 = br.ReadString();
                country.iso_a3 = br.ReadString();
                country.iso_n3 = br.ReadString();
                country.labelVisible = br.ReadBoolean();
                country.regionsRect2D = new Rect(minCountry.x, minCountry.y, Math.Abs(maxCountry.x - minCountry.x), Mathf.Abs(maxCountry.y - minCountry.y));
                newCountries.Add(country);
            }

            newCountries.Sort((Country c1, Country c2) => {
                return c1.mainRegionArea.CompareTo(c2.mainRegionArea);
            });
            countries = newCountries.ToArray();

            OptimizeFrontiers();
        }


        /// <summary>
        /// Used internally by the Map Editor. It will recalculate de boundaries and optimize frontiers based on new data of countries array
        /// </summary>
        public bool RefreshCountryDefinition (int countryIndex, List<Region> filterRegions = null) {
            lastCountryLookupCount = -1;
            if (countryIndex < 0 || countryIndex >= countries.Length)
                return false;

            Country country = countries[countryIndex];
            RefreshCountryGeometry(country);

            OptimizeFrontiers(filterRegions);
            DrawFrontiers();

            if (_showInlandFrontiers) {
                DrawInlandFrontiers();
            }
            return true;
        }


        Rect EncapsulateRect (Rect prev, Rect r) {
            // check if they're crossing the antimeridian
            Rect minRect, maxRect;
            if (r.yMin < prev.yMin) {
                minRect = r;
                maxRect = prev;
            } else {
                minRect = prev;
                maxRect = r;
            }
            float dy = maxRect.yMin - minRect.yMin;
            if (minRect.y < -90 && dy > 180) {
                return new Rect(Mathf.Min(minRect.xMin, maxRect.xMin), maxRect.yMin, Mathf.Max(minRect.xMax, maxRect.xMax), 360 - maxRect.yMin + minRect.yMax);
            } else {
                return new Rect(Mathf.Min(minRect.xMin, maxRect.xMin), minRect.yMin, Mathf.Max(minRect.xMax, maxRect.xMax), maxRect.yMax - minRect.yMin);
            }
        }


        public void RefreshCountryGeometry (Country country) {
            if (country.regions == null)
                return;
            float maxVol = 0;
            int regionCount = country.regions.Count;
            for (int r = 0; r < regionCount; r++) {
                Region countryRegion = country.regions[r];
                countryRegion.regionIndex = r;
                if (countryRegion.latlon == null) continue;
                int coorCount = countryRegion.latlon.Length;
                Vector2 min = Misc.Vector2one * 1000;
                Vector2 max = -min;
                for (int c = 0; c < coorCount; c++) {
                    float x = countryRegion.latlon[c].x;
                    float y = countryRegion.latlon[c].y;
                    if (x < min.x)
                        min.x = x;
                    if (x > max.x)
                        max.x = x;
                    if (y < min.y)
                        min.y = y;
                    if (y > max.y)
                        max.y = y;
                }
                float dlon = max.y - min.y;
                if (dlon > 180 && min.y > -180 && max.y < 180) { // fix wrapped countries rect2D around 180 longitude line
                    max.y -= 360;
                }
                Vector3 normRegionCenter = (min + max) * 0.5f;
                countryRegion.latlonCenter = normRegionCenter;

                // Calculate bounding rect
                countryRegion.latlonRect2D = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
                countryRegion.rect2DArea = countryRegion.latlonRect2D.width * countryRegion.latlonRect2D.height;
                float vol = FastVector.SqrDistance(ref min, ref max);
                if (vol > maxVol) {
                    maxVol = vol;
                    country.mainRegionIndex = r;
                    country.latlonCenter = countryRegion.latlonCenter;
                }
                country.regionsRect2D = r == 0 ? countryRegion.latlonRect2D : EncapsulateRect(country.regionsRect2D, countryRegion.latlonRect2D);
                countryRegion.surfaceGameObjectIsDirty = true;
            }
        }

        /// <summary>
        /// Regenerates frontiers mesh for all countries
        /// </summary>
        public void OptimizeFrontiers () {
            OptimizeFrontiers(null);
        }

        /// <summary>
        /// Generates frontiers mesh for specific regions.
        /// </summary>
        void OptimizeFrontiers (List<Region> filterRegions) {

            if (frontiersPoints == null) {
                frontiersPoints = new List<Vector3>(200000);
            } else {
                frontiersPoints.Clear();
            }
            if (frontiersCacheHit == null) {
                frontiersCacheHit = new Dictionary<int, FrontierSegment>(200000);
            } else {
                frontiersCacheHit.Clear();
            }
            for (int k = 0; k < countries.Length; k++) {
                Country country = countries[k];
                if (country.regions == null)
                    continue;
                int regionCount = country.regions.Count;
                for (int r = 0; r < regionCount; r++) {
                    Region region = country.regions[r];
                    if (filterRegions == null || filterRegions.Contains(region)) {
                        region.entity = country;
                        region.regionIndex = r;
                        region.neighbours.Clear();
                    }
                }
            }

            const float t1 = 50; // was 600	// was 500 but failed making Norway neighbour of South Sudan
            int[] roff = {
                -1000001,
                -1000000,
                -999999,
                -1,
                0,
                1,
                999999,
                1000000,
                1000001
            };

            for (int k = 0; k < countries.Length; k++) {
                Country country = countries[k];
                if (country.hidden || country.regions == null)
                    continue;
                int lastRegion = country.regions.Count;
                for (int r = 0; r < lastRegion; r++) {
                    Region region = country.regions[r];
                    if (filterRegions == null || filterRegions.Contains(region)) {
                        Vector2[] latlon = region.latlon;
                        int max = latlon.Length - 1;
                        for (int i = 0; i <= max; i++) {
                            Vector2 p0, p1;
                            if (i < max) {
                                p0 = latlon[i];
                                p1 = latlon[i + 1];
                            } else {
                                p0 = latlon[i];
                                p1 = latlon[0];
                            }
                            bool isNew = true;
                            int hc = (int)(p0.x * t1) + (int)(p1.x * t1) + 1000000 * ((int)(p0.y * t1) + (int)(p1.y * t1));
                            for (int h = 0; h < 9; h++) { // 9 = roff.Length
                                int hc1 = hc + roff[h];
                                if (frontiersCacheHit.TryGetValue(hc1, out FrontierSegment fs)) {
                                    if (fs.countryIndex != k || fs.region.regionIndex != r) {
                                        isNew = false;
                                        fs.repetitions++;
                                        Region neighbour = fs.region;
                                        if (neighbour != region) {
                                            if (!region.neighbours.Contains(neighbour)) {
                                                region.neighbours.Add(neighbour);
                                                neighbour.neighbours.Add(region);
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                            if (isNew) {
                                // Add frontier segment
                                Vector3[] spherePoints = region.spherePoints;
                                Vector3 v0, v1;
                                if (i < max) {
                                    v0 = spherePoints[i];
                                    v1 = spherePoints[i + 1];
                                } else {
                                    v0 = spherePoints[i];
                                    v1 = spherePoints[0];
                                }
                                FrontierSegment ifs = new FrontierSegment(v0, v1, k, region);
                                frontiersCacheHit[hc] = ifs;
                                frontiersPoints.Add(v0);
                                frontiersPoints.Add(v1);
                            }
                        }
                    }
                }
            }

            // Prepare frontiers mesh data
            if (_showCoastalFrontiers) {
                int frontiersPointCount = frontiersPoints.Count;
                int meshGroups = (frontiersPointCount / 65000) + 1;
                int meshIndex = -1;
                frontiersIndices = new int[meshGroups][];
                frontiers = new Vector3[meshGroups][];
                for (int k = 0; k < frontiersPointCount; k += 65000) {
                    int max = Mathf.Min(frontiersPointCount - k, 65000);
                    frontiers[++meshIndex] = new Vector3[max];
                    frontiersIndices[meshIndex] = new int[max];
                    for (int j = k; j < k + max; j++) {
                        frontiers[meshIndex][j - k] = frontiersPoints[j];
                        frontiersIndices[meshIndex][j - k] = j - k;
                    }
                }
            } else {
                if (interiorFrontiersPoints == null) {
                    interiorFrontiersPoints = new List<Vector3>(200000);
                } else {
                    interiorFrontiersPoints.Clear();
                }
                List<FrontierSegment> fs = new List<FrontierSegment>(frontiersCacheHit.Values);
                int fsCount = fs.Count;
                for (int k = 0; k < fsCount; k++) {
                    if (fs[k].repetitions > 0) {
                        interiorFrontiersPoints.Add(fs[k].p0);
                        interiorFrontiersPoints.Add(fs[k].p1);
                    }
                }

                int intFrontiersPointCount = interiorFrontiersPoints.Count;
                int meshGroups = (intFrontiersPointCount / 65000) + 1;
                int meshIndex = -1;
                frontiersIndices = new int[meshGroups][];
                frontiers = new Vector3[meshGroups][];
                for (int k = 0; k < intFrontiersPointCount; k += 65000) {
                    int max = Mathf.Min(intFrontiersPointCount - k, 65000);
                    frontiers[++meshIndex] = new Vector3[max];
                    frontiersIndices[meshIndex] = new int[max];
                    for (int j = k; j < k + max; j++) {
                        frontiers[meshIndex][j - k] = interiorFrontiersPoints[j];
                        frontiersIndices[meshIndex][j - k] = j - k;
                    }
                }
            }

            // Prepare inland frontiers mesh data
            if (_showInlandFrontiers) {
                if (inlandFrontiersPoints == null) {
                    inlandFrontiersPoints = new List<Vector3>(200000);
                } else {
                    inlandFrontiersPoints.Clear();
                }
                List<FrontierSegment> fs = new List<FrontierSegment>(frontiersCacheHit.Values);
                int fsCount = fs.Count;
                for (int k = 0; k < fsCount; k++) {
                    if (fs[k].repetitions == 0) {
                        if (_countries.Length != 177 || fs[k].countryIndex != 95 || _frontiersDetail != FRONTIERS_DETAIL.High) { // Avoid Lesotho - special case
                            inlandFrontiersPoints.Add(fs[k].p0);
                            inlandFrontiersPoints.Add(fs[k].p1);
                        }
                    }
                }

                int inlandFrontierPointCount = inlandFrontiersPoints.Count;
                int meshGroups = (inlandFrontierPointCount / 65000) + 1;
                int meshIndex = -1;
                inlandFrontiersIndices = new int[meshGroups][];
                inlandFrontiers = new Vector3[meshGroups][];
                for (int k = 0; k < inlandFrontierPointCount; k += 65000) {
                    int max = Mathf.Min(inlandFrontierPointCount - k, 65000);
                    inlandFrontiers[++meshIndex] = new Vector3[max];
                    inlandFrontiersIndices[meshIndex] = new int[max];
                    for (int j = k; j < k + max; j++) {
                        inlandFrontiers[meshIndex][j - k] = inlandFrontiersPoints[j];
                        inlandFrontiersIndices[meshIndex][j - k] = j - k;
                    }
                }
            }
        }


        #endregion

        #region IO stuff

        /// <summary>
        /// Returns the file name corresponding to the current country data file (countries10, countries110)
        /// </summary>
        public string GetCountriesGeoDataFileName () {
            return frontiersDetail == FRONTIERS_DETAIL.Low ? "countries110.txt" : "countries10.txt";
        }

        /// <summary>
        /// Returns the file name corresponding to the current country data file (countries10, countries110)
        /// </summary>
        public string GetCountryGeoDataBinaryFileName () {
            return frontiersDetail == FRONTIERS_DETAIL.Low ? "countries110_bin.txt" : "countries10_bin.txt";
        }


        public string GetCountriesGeoData () {
            if (countries == null) return null;
            StringBuilder sb = new StringBuilder();
            for (int k = 0; k < countries.Length; k++) {
                Country country = countries[k];
                if (country.regions.Count < 1)
                    continue;
                if (k > 0)
                    sb.Append("|");
                sb.Append(country.name);
                sb.Append("$");
                sb.Append(country.continent);
                sb.Append("$");
                for (int r = 0; r < country.regions.Count; r++) {
                    if (r > 0)
                        sb.Append("*");
                    Region region = country.regions[r];
                    for (int p = 0; p < region.latlon.Length; p++) {
                        if (p > 0) {
                            sb.Append(";");
                        }
                        int x = (int)(region.latlon[p].x * MAP_PRECISION);
                        int y = (int)(region.latlon[p].y * MAP_PRECISION);
                        sb.Append(x.ToString(Misc.InvariantCulture));
                        sb.Append(",");
                        sb.Append(y.ToString(Misc.InvariantCulture));

                    }
                }
                sb.Append("$");
                sb.Append((country.hidden ? "1" : "0"));
                sb.Append("$");
                sb.Append(country.fips10_4);
                sb.Append("$");
                sb.Append(country.iso_a2);
                sb.Append("$");
                sb.Append(country.iso_a3);
                sb.Append("$");
                sb.Append(country.iso_n3);
                sb.Append("$");
                sb.Append(country.labelVisible ? "1" : "0");
            }
            return sb.ToString();
        }


        /// <summary>
        /// Exports the geographic data in binary format.
        /// </summary>
        public byte[] GetCountriesGeoDataBinary () {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8);
            int countriesCount = countries.Length;
            bw.Write((ushort)countriesCount);
            for (int k = 0; k < countriesCount; k++) {
                Country country = _countries[k];
                bw.Write(country.name);
                bw.Write(country.continent);
                int regionsCount = country.regions.Count;
                bw.Write((ushort)regionsCount);
                for (int r = 0; r < regionsCount; r++) {
                    Region region = country.regions[r];
                    int pointsCount = region.latlon.Length;
                    bw.Write((uint)pointsCount);
                    for (int p = 0; p < pointsCount; p++) {
                        bw.Write(region.latlon[p].x);
                        bw.Write(region.latlon[p].y);
                    }
                }
                bw.Write(country.hidden);
                bw.Write(country.fips10_4);
                bw.Write(country.iso_a2);
                bw.Write(country.iso_a3);
                bw.Write(country.iso_n3);
                bw.Write(country.labelVisible);
            }
            bw.Flush();
            return ms.ToArray();
        }

        #endregion

        #region Drawing stuff


        int GetCacheIndexForCountryRegion (int countryIndex, int regionIndex) {
            return countryIndex * 1000 + regionIndex;
        }

        Material GetCountryColoredTexturedMaterial (Color color, Texture2D texture) {
            return GetCountryColoredTexturedMaterial(color, texture, true);
        }

        Material GetCountryColoredTexturedMaterial (Color color, Texture2D texture, bool autoChooseTransparentMaterial) {
            Material mat;
            if (texture == null && countryColoredMatCache.TryGetValue(color, out mat)) {
                return mat;
            } else {
                int zwriteValue = (_showWorld || !_showBackSide) ? 0 : 1;
                Material customMat;
                if (texture != null) {
                    customMat = Instantiate(countryTexturizedMat);
                    customMat.renderQueue = zwriteValue == 1 ? RENDER_QUEUE_TRANSPARENT - 17 : RENDER_QUEUE_OPAQUE - 17;
                    customMat.name = countryTexturizedMat.name;
                    customMat.mainTexture = texture;
                } else {
                    if (color.a < 1.0f || !autoChooseTransparentMaterial) {
                        customMat = Instantiate(countryColoredAlphaMat);
                        customMat.renderQueue = zwriteValue == 1 ? RENDER_QUEUE_TRANSPARENT - 17 : RENDER_QUEUE_OPAQUE - 17;
                    } else {
                        customMat = Instantiate(countryColoredMat);
                        customMat.SetInt("_ZWrite", zwriteValue);
                    }
                    customMat.name = countryColoredMat.name;
                    countryColoredMatCache[color] = customMat;
                }
                customMat.color = color;
                return customMat;
            }
        }

        void UpdateOutlineMatProperties () {
            if (_frontiersThicknessMode == FRONTIERS_THICKNESS.Custom) {
                outlineMatCurrent = outlineMatThickOpaque;
            } else {
                outlineMatCurrent = outlineMatThinOpaque;
            }

            if (outlineMatCurrent == null)
                return;
            outlineMatCurrent.color = _outlineColor;
            outlineMatCurrent.SetFloat(ShaderParams.OutlineThickness, _frontiersThickness);
        }

        void UpdateFrontiersMat () {
            if (frontiersMatCurrent == null)
                return;
            // Different alpha?
            if (_frontiersColor.a != frontiersMatCurrent.color.a) {
                DrawFrontiers();
            } else {
                UpdateFrontiersMatProperties();
            }
        }

        void UpdateFrontiersMatProperties () {
            if (frontiersMatCurrent == null)
                return;
            frontiersMatCurrent.color = _frontiersColor;
            frontiersMatCurrent.SetFloat(ShaderParams.OutlineThickness, _frontiersThickness);
        }

        void UpdateInlandFrontiersMat () {
            if (inlandFrontiersMatCurrent == null)
                return;
            // Different alpha?
            if (_inlandFrontiersColor.a != inlandFrontiersMatCurrent.color.a) {
                DrawInlandFrontiers();
            } else if (inlandFrontiersMatCurrent.color != _inlandFrontiersColor) {
                inlandFrontiersMatCurrent.color = _inlandFrontiersColor;
            }
        }

        void DrawFrontiers () {

            if (!gameObject.activeInHierarchy || frontiers == null)
                return;

            // Create frontiers layer
            Transform t = transform.Find(FRONTIERS_LAYER);
            if (t != null) {
                DestroyImmediate(t.gameObject);
            }
            frontiersLayer = new GameObject(FRONTIERS_LAYER);
            frontiersLayer.layer = gameObject.layer;
            frontiersLayer.transform.SetParent(transform, false);
            frontiersLayer.transform.localPosition = Misc.Vector3zero;
            frontiersLayer.transform.localRotation = Misc.QuaternionZero; //Quaternion.Euler (Misc.Vector3zero);
            frontiersLayer.transform.localScale = _earthInvertedMode ? Misc.Vector3one * 0.995f : Misc.Vector3one * 1.0002f;

            // Choose a frontiers mat
            if (_frontiersColor.a < 1f) {
                if (_frontiersThicknessMode == FRONTIERS_THICKNESS.Custom) {
                    frontiersMatCurrent = frontiersMatThickAlpha;
                } else {
                    frontiersMatCurrent = frontiersMatThinAlpha;
                }
            } else {
                if (_frontiersThicknessMode == FRONTIERS_THICKNESS.Custom) {
                    frontiersMatCurrent = frontiersMatThickOpaque;
                } else {
                    frontiersMatCurrent = frontiersMatThinOpaque;
                }
            }
            UpdateFrontiersMatProperties();

            for (int k = 0; k < frontiers.Length; k++) {
                GameObject flayer = new GameObject("flayer");
                flayer.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                flayer.layer = gameObject.layer;
                flayer.transform.SetParent(frontiersLayer.transform, false);
                flayer.transform.localPosition = Misc.Vector3zero;
                flayer.transform.localRotation = Misc.QuaternionZero;

                Mesh mesh = new Mesh();
                mesh.vertices = frontiers[k];
                mesh.SetIndices(frontiersIndices[k], MeshTopology.Lines, 0);
                mesh.RecalculateBounds();

                MeshFilter mf = flayer.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = flayer.AddComponent<MeshRenderer>();
                mr.receiveShadows = false;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.sharedMaterial = frontiersMatCurrent;
            }

            // Toggle frontiers visibility layer according to settings
            frontiersLayer.SetActive(_showFrontiers);

        }

        void DrawInlandFrontiers () {
            if (!gameObject.activeInHierarchy)
                return;

            Transform t = transform.Find("InlandFrontiers");
            if (t != null)
                DestroyImmediate(t.gameObject);

            if (!_showInlandFrontiers)
                return;

            // Create frontiers layer
            inlandFrontiersLayer = new GameObject("InlandFrontiers");
            inlandFrontiersLayer.layer = gameObject.layer;
            inlandFrontiersLayer.transform.SetParent(transform, false);
            inlandFrontiersLayer.transform.localPosition = Misc.Vector3zero;
            inlandFrontiersLayer.transform.localRotation = Misc.QuaternionZero; //Quaternion.Euler (Misc.Vector3zero);
            inlandFrontiersLayer.transform.localScale = _earthInvertedMode ? Misc.Vector3one * 0.995f : Misc.Vector3one;

            // Choose an inland frontiers mat
            if (_inlandFrontiersColor.a < 1f) {
                inlandFrontiersMatCurrent = inlandFrontiersMatAlpha;
            } else {
                inlandFrontiersMatCurrent = inlandFrontiersMatOpaque;
            }
            inlandFrontiersMatCurrent.color = _inlandFrontiersColor;

            for (int k = 0; k < inlandFrontiers.Length; k++) {
                GameObject flayer = new GameObject("flayer");
                flayer.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                flayer.layer = gameObject.layer;
                flayer.transform.SetParent(inlandFrontiersLayer.transform, false);
                flayer.transform.localPosition = Misc.Vector3zero;
                flayer.transform.localRotation = Misc.QuaternionZero; //Quaternion.Euler (Misc.Vector3zero);

                Mesh mesh = new Mesh();
                mesh.vertices = inlandFrontiers[k];
                mesh.SetIndices(inlandFrontiersIndices[k], MeshTopology.Lines, 0);
                mesh.RecalculateBounds();

                MeshFilter mf = flayer.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                MeshRenderer mr = flayer.AddComponent<MeshRenderer>();
                mr.receiveShadows = false;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.sharedMaterial = inlandFrontiersMatCurrent;
            }

            // Toggle frontiers visibility layer according to settings
            inlandFrontiersLayer.SetActive(_showInlandFrontiers);
        }

        #endregion

        #region Map Labels


        void ReloadFont () {
            if (_countryLabelsTextEngine == TEXT_ENGINE.TextMeshStandard) {
                if (_countryLabelsFont != null && _countryLabelsFont.dynamic) {
                    Debug.LogError("Dynamic font (" + _countryLabelsFont.name + ") is not supported - please select the font and choose other option in the Import settings. Also set Font Size to 160.");
                    _countryLabelsFont = null;
                }
                if (_countryLabelsFont == null) {
                    labelsFont = Instantiate(Resources.Load<Font>("Font/Lato"));
                } else {
                    labelsFont = Instantiate(_countryLabelsFont);
                }

                Material fontMaterial = Instantiate(Resources.Load<Material>("Materials/Font")); // this material is linked to a shader that has into account zbuffer
                if (labelsFont.material != null) {
                    fontMaterial.mainTexture = labelsFont.material.mainTexture;
                }
                labelsFont.material = fontMaterial;
                labelsShadowMaterial = Instantiate(fontMaterial);
                labelsShadowMaterial.renderQueue--;

            } else {
                if (_countryLabelsFontTMPro == null) {
                    _countryLabelsFontTMPro = Resources.Load<TMP_FontAsset>("Font/TextMeshPro/Lato SDF");
                    if (_countryLabelsFontTMPro == null) {
                        Debug.LogWarning("Please assign an SDF Font to World Map Globe inspector. You can create SDF Fonts using TextMesh Pro Font Asset creator tool.");
                    }
                }
#if UNITY_2023_1_OR_NEWER
                labelsFontTMPro = (TMP_FontAsset)_countryLabelsFontTMPro;
#else
                labelsFontTMPro = Instantiate(_countryLabelsFontTMPro);
#endif
                labelsFontTMProMaterial = _countryLabelsFontTMProMaterial;
                if (labelsFontTMProMaterial == null) {
                    labelsFontTMProMaterial = labelsFontTMPro.material;
                }
            }
        }


        /// <summary>
        /// Forces redraw of all labels.
        /// </summary>
        public void RedrawMapLabels () {
            DestroyMapLabels();
            DrawMapLabels();
        }

        /// <summary>
        /// Draws the map labels. Note that it will update cached textmesh objects if labels are already drawn.
        /// </summary>
        void DrawMapLabels () {

            if (!_showCountryNames)
                return;

            GameObject textRoot = null;

            if (_countryLabelsTextEngine == TEXT_ENGINE.TextMeshPro) {
                if (labelsFontTMPro == null) {
                    ReloadFont();
                    if (labelsFontTMPro == null) return;
                }
            } else {
                // Set colors for standard font
                if (labelsFont == null) {
                    ReloadFont();
                    if (labelsFont == null) return;
                }
                labelsFont.material.color = _countryLabelsColor;
                labelsShadowMaterial.color = _countryLabelsShadowColor;
            }

            // Create texts
            GameObject overlay = GetOverlayLayer(true, _labelsRenderMethod == LABELS_RENDER_METHOD.Blended);
            Transform t = transform.Find(TEXT_ROOT);
            if (t == null) {
                t = overlay.transform.Find(TEXT_ROOT);
            }
            if (t == null) {
                textRoot = new GameObject(TEXT_ROOT);
                textRoot.layer = overlay.layer;
            } else {
                textRoot = t.gameObject;
                textRoot.transform.SetParent(null);
            }
            textRoot.transform.localPosition = new Vector3(0, 0, -0.001f);
            textRoot.transform.rotation = Misc.QuaternionZero;
            textRoot.transform.localScale = Misc.Vector3one;

            List<MeshRect> meshRects = new List<MeshRect>();
            for (int countryIndex = 0; countryIndex < countries.Length; countryIndex++) {
                Country country = countries[countryIndex];
                if (country.hidden || !country.labelVisible)
                    continue;

                Vector2 center = Conversion.GetBillboardPointFromLatLon(country.latlonCenter) + country.labelOffset;

                // add caption
                Region region = country.regions[country.mainRegionIndex];

                // Special countries adjustements
                if (_frontiersDetail == FRONTIERS_DETAIL.Low && countries.Length == 177) {
                    switch (countryIndex) {
                        case 175: // Russia
                            center.y++;
                            center.x -= 8;
                            break;
                        case 176: // Antartica
                            if (_labelsRenderMethod == LABELS_RENDER_METHOD.Blended) {
                                center.y += 8f;
                            }
                            break;
                        case 171: // Greenland
                            center.y -= 2f;
                            break;
                        case 172: // Brazil
                            center.y += 4f;
                            center.x += 1.0f;
                            break;
                        case 168: // India
                            center.x -= 2f;
                            break;
                        case 170: // USA
                            center.x -= 1f;
                            break;
                        case 174: // Canada
                            center.x -= 3f;
                            break;
                        case 173: // China
                            center.x -= 1f;
                            center.y -= 2f;
                            break;
                    }
                } else if (_frontiersDetail == FRONTIERS_DETAIL.High && countries.Length == 243) {
                    switch (countryIndex) {
                        case 242: // Antartica
                            center.y += 6f;
                            break;
                        case 238: // Brazil
                            center.y += 4f;
                            break;
                    }
                }

                // Adjusts country name length
                string countryName = country.customLabel != null ? country.customLabel : country.name.ToUpper();
                bool introducedCarriageReturn = false;
                if (countryName.Length > 15) {
                    int spaceIndex = countryName.IndexOf(' ', countryName.Length / 2);
                    if (spaceIndex >= 0) {
                        countryName = countryName.Substring(0, spaceIndex) + "\n" + countryName.Substring(spaceIndex + 1);
                        introducedCarriageReturn = true;
                    }
                }

                GameObject textObj;
                TextMesh tm = null;
                TextMeshPro tmPro = null;
                Renderer textRenderer;

                if (_countryLabelsTextEngine == TEXT_ENGINE.TextMeshPro) {

                    if (country.labelTextMeshPro == null) {
                        Color labelColor = country.labelColorOverride ? country.labelColor : _countryLabelsColor;
                        TMP_FontAsset customFont = country.labelFontTMProOverride;
                        if (customFont == null) {
                            customFont = labelsFontTMPro;
                        }
                        Material customFontMaterial = country.labelFontTMProMaterialOverride;
                        if (customFontMaterial == null) {
                            customFontMaterial = labelsFontTMProMaterial;
                        }
                        if (customFont == null || customFontMaterial == null || customFontMaterial.shader == null) continue;
                        tmPro = Drawing.CreateTextPro(countryName, null, overlay.layer, center, customFont, customFontMaterial, _countryLabelsEnableAutomaticFade, labelColor, _showLabelsShadow, _countryLabelsShadowOffset, _countryLabelsShadowColor);
                        textObj = tmPro.gameObject;
                        country.labelTextGameObject = textObj;
                        country.labelTextMeshPro = tmPro;
                        textRenderer = textObj.GetComponent<Renderer>();
                        country.labelRenderer = textRenderer;
                        country.labelMeshWidth = tmPro.preferredWidth;
                        country.labelMeshHeight = tmPro.preferredHeight;
                        country.labelMeshCenter = center;
                        textObj.transform.SetParent(textRoot.transform, false);
                        textObj.transform.localPosition = center;
                        textObj.layer = textRoot.gameObject.layer;
                    } else {
                        tmPro = country.labelTextMeshPro;
                        textObj = country.labelTextGameObject;
                        textObj.transform.localPosition = center;
                        textRenderer = textObj.GetComponent<Renderer>();

                        Material fontMat = tmPro.fontSharedMaterial;
                        if (_showLabelsShadow) {
                            fontMat.SetColor("_UnderlayColor", _countryLabelsShadowColor);
                            fontMat.SetFloat("_UnderlayOffsetX", _countryLabelsShadowOffset);
                            fontMat.SetFloat("_UnderlayOffsetY", -_countryLabelsShadowOffset);
                            fontMat.EnableKeyword("UNDERLAY_ON");
                        } else {
                            fontMat.DisableKeyword("UNDERLAY_ON");
                        }
                    }
                } else {

                    if (country.labelTextMesh == null) {
                        Color labelColor = country.labelColorOverride ? country.labelColor : _countryLabelsColor;
                        Font customFont = country.labelFontOverride;
                        if (customFont == null) {
                            customFont = labelsFont;
                        }
                        Material customLabelShadowMaterial = country.labelFontShadowMaterial ?? labelsShadowMaterial;
                        tm = Drawing.CreateText(countryName, null, overlay.layer, center, customFont, labelColor, _showLabelsShadow, customLabelShadowMaterial, _countryLabelsShadowColor, TextAnchor.MiddleCenter, _countryLabelsShadowOffset);

                        textObj = tm.gameObject;
                        country.labelTextGameObject = textObj;
                        country.labelTextMesh = tm;
                        textRenderer = textObj.GetComponent<Renderer>();
                        country.labelRenderer = textRenderer;
                        Bounds bounds = textRenderer.bounds;
                        country.labelMeshWidth = bounds.size.x;
                        country.labelMeshHeight = bounds.size.y;
                        country.labelMeshCenter = center;
                        textObj.transform.SetParent(textRoot.transform, false);
                        textObj.transform.localPosition = center;
                        textObj.layer = textRoot.gameObject.layer;
                        if (_showLabelsShadow) {
                            country.labelShadowTextMesh = textObj.transform.Find("shadow").GetComponent<TextMesh>();
                            country.labelShadowTextMesh.gameObject.layer = textObj.layer;
                            country.labelShadowRenderer = country.labelShadowTextMesh.GetComponent<Renderer>();
                        }
                        if (_countryLabelsEnableAutomaticFade) {
                            // Label fade works by changing text material alpha - instantiate material to allow different colors
                            Material clonedTextMaterial = Instantiate(textRenderer.sharedMaterial);
                            textRenderer.sharedMaterial = clonedTextMaterial;
                            // Also its shadow
                            if (showLabelsShadow) {
                                Renderer textShadowRenderer = country.labelShadowTextMesh.GetComponent<Renderer>();
                                clonedTextMaterial = Instantiate(textShadowRenderer.sharedMaterial);
                                textShadowRenderer.sharedMaterial = clonedTextMaterial;
                            }
                        }
                    } else {
                        tm = country.labelTextMesh;
                        textObj = country.labelTextGameObject;
                        textObj.transform.localPosition = center;
                        textRenderer = textObj.GetComponent<Renderer>();
                    }
                }

                if (_labelsRenderMethod == LABELS_RENDER_METHOD.Blended && country.regionsRect2D.yMax >= 181) {
                    // draws label on the other side
                    Debug.LogWarning("Country " + country.name + " exceeds 180 degree longitude. Label maybe partially displayed. Switch label render mode to world space.");
                }

                float meshWidth = country.labelMeshWidth;
                float meshHeight = country.labelMeshHeight;

                // adjusts caption
                Rect rect = region.rect2Dbillboard;
                float absoluteHeight;
                if (country.labelRotation > 0) {
                    textObj.transform.localRotation = Quaternion.Euler(0, 0, country.labelRotation);
                    absoluteHeight = Mathf.Min(rect.height * _countryLabelsSize, rect.width);
                } else if (_countryLabelsOrientation == COUNTRY_LABELS_ORIENTATION.Automatic && rect.height > rect.width * 1.45f) {
                    float angle;
                    if (rect.height > rect.width * 1.5f) {
                        angle = 90;
                    } else {
                        angle = Mathf.Atan2(rect.height, rect.width) * Mathf.Rad2Deg;
                    }
                    textObj.transform.localRotation = Quaternion.Euler(0, 0, angle);
                    absoluteHeight = Mathf.Min(rect.width * _countryLabelsSize, rect.height);
                } else {
                    absoluteHeight = Mathf.Min(rect.height * _countryLabelsSize, rect.width);
                }

                // adjusts scale to fit width in rect
                float scale;
                if (country.labelFontSizeOverride) {
                    scale = country.labelFontSize;
                } else {
                    float adjustedMeshHeight = introducedCarriageReturn ? meshHeight * 0.5f : meshHeight;
                    scale = absoluteHeight / adjustedMeshHeight;
                    float desiredWidth = meshWidth * scale;
                    if (desiredWidth > rect.width) {
                        scale = rect.width / meshWidth;
                    }
                    if (adjustedMeshHeight * scale < _countryLabelsAbsoluteMinimumSize) {
                        scale = _countryLabelsAbsoluteMinimumSize / adjustedMeshHeight;
                    }
                }

                // stretchs out the caption
                float displayedMeshWidth = meshWidth * scale;
                float displayedMeshHeight = meshHeight * scale;
                string wideName;
                int times = Mathf.FloorToInt(rect.width * 0.45f / (meshWidth * scale));
                if (times > 10)
                    times = 10;
                if (times > 0 && _labelsRenderMethod == LABELS_RENDER_METHOD.Blended) {
                    sb.Length = 0;
                    string spaces = new string(' ', times * 2);
                    for (int c = 0; c < countryName.Length; c++) {
                        sb.Append(countryName[c]);
                        if (c < countryName.Length - 1) {
                            sb.Append(spaces);
                        }
                    }
                    wideName = sb.ToString();
                } else {
                    wideName = countryName;
                }

                if (countryName.Length != wideName.Length) {
                    if (_countryLabelsTextEngine == TEXT_ENGINE.TextMeshStandard) {
                        tm.text = wideName;
                        country.labelMeshWidth = textRenderer.bounds.size.x;
                        country.labelMeshHeight = textRenderer.bounds.size.y;
                        if (_showLabelsShadow) {
                            textObj.transform.Find("shadow").GetComponent<TextMesh>().text = wideName;
                        }
                    } else {
                        tmPro.text = wideName;
                        country.labelMeshWidth = tmPro.preferredWidth;
                        country.labelMeshHeight = tmPro.preferredHeight;
                    }
                    // bounds has changed
                    displayedMeshWidth = country.labelMeshWidth * scale;
                    displayedMeshHeight = country.labelMeshHeight * scale;
                }

                // apply scale
                textObj.transform.localScale = new Vector3(scale, scale, 1);

                // Cache position of the label rect in the sphere, used for fader.
                Vector2 minMaxPlaneY = new Vector2(textObj.transform.localPosition.y - displayedMeshHeight * 0.5f, textObj.transform.localPosition.y + displayedMeshHeight * 0.5f);   // country.regions[country.mainRegionIndex].minMaxLat;
                Vector2 labelLatLonTop = Conversion.GetLatLonFromBillboard(new Vector2(textObj.transform.localPosition.x, minMaxPlaneY.y));
                Vector2 labelLatLonBottom = Conversion.GetLatLonFromBillboard(new Vector2(textObj.transform.localPosition.x, minMaxPlaneY.x));
                country.labelSphereEdgeTop = Conversion.GetSpherePointFromLatLon(labelLatLonTop);
                country.labelSphereEdgeBottom = Conversion.GetSpherePointFromLatLon(labelLatLonBottom);

                // Save mesh rect for overlapping checking
                if (country.labelOffset == Misc.Vector2zero) {
                    MeshRect mr = new MeshRect(countryIndex, new Rect(center.x - displayedMeshWidth * 0.5f, center.y - displayedMeshHeight * 0.5f, displayedMeshWidth, displayedMeshHeight));
                    meshRects.Add(mr);
                }
            }

            // Simple-fast overlapping checking
            int cont = 0;
            bool needsResort = true;

            while (needsResort && ++cont < 10) {
                meshRects.Sort(OverlapComparer);

                for (int c = 1; c < meshRects.Count; c++) {
                    Rect thisMeshRect = meshRects[c].rect;
                    for (int prevc = c - 1; prevc >= 0; prevc--) {
                        Rect otherMeshRect = meshRects[prevc].rect;
                        if (thisMeshRect.Overlaps(otherMeshRect)) {
                            needsResort = true;
                            int thisCountryIndex = meshRects[c].countryIndex;
                            Country country = countries[thisCountryIndex];
                            GameObject thisLabel = country.labelTextGameObject;

                            // displaces this label
                            float offsety = (thisMeshRect.yMax - otherMeshRect.yMin);
                            offsety = Mathf.Min(country.regions[country.mainRegionIndex].rect2Dbillboard.height * 0.35f, offsety);
                            float maxH = country.regions[country.mainRegionIndex].rect2Dbillboard.height;
                            float centerY = country.regions[country.mainRegionIndex].sphereCenter.y;
                            if (Mathf.Abs(country.labelMeshCenter.y + offsety - centerY * 100) > maxH * 100) {
                                offsety = 0;
                            }
                            thisLabel.transform.localPosition = new Vector3(country.labelMeshCenter.x, country.labelMeshCenter.y - offsety, thisLabel.transform.localPosition.z);
                            thisMeshRect = new Rect(thisLabel.transform.localPosition.x - thisMeshRect.width * 0.5f,
                                thisLabel.transform.localPosition.y - thisMeshRect.height * 0.5f,
                                thisMeshRect.width, thisMeshRect.height);
                            meshRects[c].rect = thisMeshRect;
                        }
                    }
                }
            }

            if (_labelsRenderMethod == LABELS_RENDER_METHOD.WorldSpace) {
                textRoot.transform.SetParent(transform, true);
            } else {
                textRoot.transform.SetParent(overlay.transform, false);
            }
            textRoot.transform.localPosition = Misc.Vector3zero;
            textRoot.transform.localRotation = Misc.QuaternionZero;

            // Reposition labels in world space?
            if (_labelsRenderMethod == LABELS_RENDER_METHOD.WorldSpace) {
                textRoot.transform.position = transform.position;
                // Get current mesh position in billboard
                for (int c = 0; c < countries.Length; c++) {
                    GameObject labelTextGO = countries[c].labelTextGameObject;
                    if (labelTextGO == null) continue;
                    Transform tmLabel = labelTextGO.transform;
                    if (tmLabel == null) continue;
                    Vector2 labelPos = tmLabel.localPosition;
                    Vector2 latlon = Conversion.GetLatLonFromBillboard(labelPos);
                    Vector3 spherePos = Conversion.GetSpherePointFromLatLon(latlon);
                    Vector3 wsLabelPos = transform.TransformPoint(spherePos * (_earthInvertedMode ? 0.985f - _labelsElevation : 1.001f + _labelsElevation));
                    tmLabel.position = wsLabelPos;
                    float az = tmLabel.localRotation.eulerAngles.z;

                    // Fixed: negative scale breaks dynamic batching
                    Vector3 lookAtPos = transform.TransformPoint(spherePos * 0.5f);
                    tmLabel.LookAt(lookAtPos, transform.up);
                    tmLabel.Rotate(lookAtPos - wsLabelPos, -az, Space.World);
                    tmLabel.localScale = new Vector3(tmLabel.localScale.x * transform.localScale.x / 100f, tmLabel.localScale.y * transform.localScale.y / 100f, tmLabel.localScale.z);
                }
            }

            if (_countryLabelsEnableAutomaticFade)
                FadeCountryLabels();

            requestMapperCamShot = true;
        }


        int OverlapComparer (MeshRect r1, MeshRect r2) {
            return (r2.rect.center.y).CompareTo(r1.rect.center.y);
        }

        class MeshRect {
            public int countryIndex;
            public Rect rect;

            public MeshRect (int countryIndex, Rect rect) {
                this.countryIndex = countryIndex;
                this.rect = rect;
            }
        }

        void DestroyMapLabels () {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": destroy labels");
#endif
            if (countries != null) {
                for (int k = 0; k < countries.Length; k++) {
                    if (countries[k].labelTextGameObject != null) {
                        if (countries[k].labelTextMeshPro != null) {
                            DestroyImmediate(countries[k].labelTextMeshPro);
                            countries[k].labelTextMeshPro = null;
                        }
                        DestroyImmediate(countries[k].labelTextGameObject);
                        countries[k].labelTextGameObject = null;
                        countries[k].labelTextMesh = null;
                    }
                }
            }
            // Security check: if there're still gameObjects under TextRoot, also delete it
            Transform t = transform.Find(TEXT_ROOT);
            if (t == null && overlayLayer != null) {
                t = overlayLayer.transform.Find(TEXT_ROOT);
            }
            if (t != null && t.childCount > 0) {
                DestroyImmediate(t.gameObject);
            }

            if (_labelsRenderMethod == LABELS_RENDER_METHOD.Blended)
                requestMapperCamShot = true;
        }

        void FadeCountryLabels () {
            if (!_showCountryNames || _countries == null)
                return;
            int maxIterations = _countries.Length;
            for (int k = 0, iterations = 0; k < _countryLabelsFadePerFrame; k++, iterations++) {
                fadeCountryIndex++;
                if (fadeCountryIndex >= _countries.Length)
                    fadeCountryIndex = 0;
                Country country = _countries[fadeCountryIndex];
                if (!country.labelVisible)
                    continue;
                bool didFade;
                if (_countryLabelsTextEngine == TEXT_ENGINE.TextMeshStandard) {
                    didFade = FadeCountryLabelTextMesh(country);
                } else {
                    didFade = FadeCountryLabelTextMeshPro(country);
                }

                if (!didFade) {
                    // if no changes, try with next but don't consume a slot
                    if (iterations < maxIterations)
                        k--;
                } else if (_labelsRenderMethod == LABELS_RENDER_METHOD.Blended) {
                    requestMapperCamShot = true;
                }
            }
        }

        /// <summary>
        /// Automatically fades in/out country labels based on their screen size
        /// </summary>
        /// <returns><c>true</c>, if country label was faded, <c>false</c> otherwise.</returns>
        bool FadeCountryLabelTextMesh (Country country) {
            Camera cam = mainCamera;
            bool changes = false;
            float maxAlpha = _countryLabelsColor.a;
            float maxAlphaShadow = _countryLabelsShadowColor.a;
            float invertedSign = _earthInvertedMode ? -1f : 1f;
            TextMesh tm = country.labelTextMesh;
            if (tm != null) {
                // Fade label
                float ad = 1f;
                if (Application.isPlaying) {
                    if (_countryLabelsEnableAutomaticFade && !_earthInvertedMode) {
                        // If country label is not visible (facing beyond camera's direction), fade out completely
                        if (Vector3.Dot(cam.transform.forward, transform.TransformDirection(country.mainRegion.sphereCenter).normalized) > 0.1f) {
                            ad = 0;
                        } else {
                            Vector2 lc0 = cam.WorldToScreenPoint(transform.TransformPoint(country.labelSphereEdgeBottom));
                            Vector2 lc1 = cam.WorldToScreenPoint(transform.TransformPoint(country.labelSphereEdgeTop));
                            float screenHeight = Vector2.Distance(lc0, lc1) / cam.pixelHeight;
                            if (screenHeight < _countryLabelsAutoFadeMinHeight) {
                                ad = Mathf.Lerp(1.0f, 0, (_countryLabelsAutoFadeMinHeight - screenHeight) / _countryLabelsAutoFadeMinHeightFallOff);
                            } else if (screenHeight > _countryLabelsAutoFadeMaxHeight) {
                                ad = Mathf.Lerp(1.0f, 0, (screenHeight - _countryLabelsAutoFadeMaxHeight) / _countryLabelsAutoFadeMaxHeightFallOff);
                            }
                        }
                    }
                }
                bool enableRendering = ad > 0;
                Renderer mr = country.labelRenderer;
                if (mr.enabled != enableRendering) {
                    changes = true;
                    mr.enabled = enableRendering;
                    if (country.labelShadowRenderer != null) {
                        country.labelShadowRenderer.enabled = enableRendering;
                    }
                }
                if (enableRendering) {
                    float newAlpha = ad * maxAlpha;
                    if (mr.sharedMaterial.color.a != newAlpha) {
                        changes = true;
                        if (Application.isPlaying) {
                            mr.sharedMaterial.color = new Color(tm.color.r, tm.color.g, tm.color.b, newAlpha);
                        } else {
                            tm.color = new Color(tm.color.r, tm.color.g, tm.color.b, newAlpha);
                        }
                    }
                    // Fade label shadow
                    if (country.labelShadowRenderer != null) {
                        newAlpha = ad * maxAlphaShadow;
                        Color tmShadowColor = country.labelShadowTextMesh.color;
                        if (country.labelShadowRenderer.sharedMaterial.color.a != newAlpha) {
                            changes = true;
                            if (Application.isPlaying) {
                                country.labelShadowRenderer.sharedMaterial.color = new Color(tmShadowColor.r, tmShadowColor.g, tmShadowColor.b, newAlpha);
                            } else {
                                country.labelShadowTextMesh.color = new Color(tmShadowColor.r, tmShadowColor.g, tmShadowColor.b, newAlpha);
                            }
                        }
                    }
                }

                // Orient to camera
                if (_labelsFaceToCamera) {
                    Vector3 currentScale = tm.transform.localScale;
                    float d = Vector3.Dot(tm.transform.up, cam.transform.up) * Mathf.Sign(currentScale.x);
                    Vector3 newScale = currentScale;
                    if (_labelsRenderMethod == LABELS_RENDER_METHOD.Blended) {
                        if (d < 0)
                            newScale = new Vector3(-currentScale.x, -currentScale.y, currentScale.z);
                    } else {
                        if (d * invertedSign < 0)
                            newScale = new Vector3(-currentScale.x, -currentScale.y, currentScale.z);
                    }
                    if (newScale != currentScale) {
                        tm.transform.localScale = newScale;
                        changes = true;
                    }
                }
            }
            return changes;
        }


        /// <summary>
        /// Automatically fades in/out country labels based on their screen size
        /// </summary>
        /// <returns><c>true</c>, if country label was faded, <c>false</c> otherwise.</returns>
        bool FadeCountryLabelTextMeshPro (Country country) {
            Camera cam = mainCamera;
            bool changes = false;
            float maxAlpha = _countryLabelsColor.a;
            float invertedSign = _earthInvertedMode ? -1f : 1f;
            TextMeshPro tmPro = country.labelTextMeshPro;
            if (tmPro != null) {
                // Fade label
                float ad = 1f;
                if (Application.isPlaying) {
                    if (_countryLabelsEnableAutomaticFade && !_earthInvertedMode) {
                        // If country label is not visible (facing beyond camera's direction), fade out completely
                        if (Vector3.Dot(cam.transform.forward, transform.TransformDirection(country.mainRegion.sphereCenter).normalized) > 0.1f) {
                            ad = 0;
                        } else {
                            Vector2 lc0 = cam.WorldToScreenPoint(transform.TransformPoint(country.labelSphereEdgeBottom));
                            Vector2 lc1 = cam.WorldToScreenPoint(transform.TransformPoint(country.labelSphereEdgeTop));
                            float screenHeight = Vector2.Distance(lc0, lc1) / cam.pixelHeight;
                            if (screenHeight < _countryLabelsAutoFadeMinHeight) {
                                ad = Mathf.Lerp(1.0f, 0, (_countryLabelsAutoFadeMinHeight - screenHeight) / _countryLabelsAutoFadeMinHeightFallOff);
                            } else if (screenHeight > _countryLabelsAutoFadeMaxHeight) {
                                ad = Mathf.Lerp(1.0f, 0, (screenHeight - _countryLabelsAutoFadeMaxHeight) / _countryLabelsAutoFadeMaxHeightFallOff);
                            }
                        }
                    }
                }
                bool enableRendering = ad > 0;
                if (tmPro.enabled != enableRendering) {
                    changes = true;
                    tmPro.enabled = enableRendering;
                }
                if (enableRendering) {
                    float newAlpha = ad * maxAlpha;
                    if (tmPro.color.a != newAlpha) {
                        changes = true;
                        tmPro.color = new Color(tmPro.color.r, tmPro.color.g, tmPro.color.b, newAlpha);
                    }
                }

                // Orient to camera
                if (_labelsFaceToCamera) {
                    Vector3 currentScale = tmPro.transform.localScale;
                    float d = Vector3.Dot(tmPro.transform.up, cam.transform.up) * Mathf.Sign(currentScale.x);
                    Vector3 newScale = currentScale;
                    if (_labelsRenderMethod == LABELS_RENDER_METHOD.Blended) {
                        if (d < 0)
                            newScale = new Vector3(-currentScale.x, -currentScale.y, currentScale.z);
                    } else {
                        if (d * invertedSign < 0)
                            newScale = new Vector3(-currentScale.x, -currentScale.y, currentScale.z);
                    }
                    if (newScale != currentScale) {
                        tmPro.transform.localScale = newScale;
                        changes = true;
                    }
                }

            }
            return changes;
        }

        #endregion

        #region Country highlighting

        bool GetCountryUnderMouse (Vector3 spherePoint, out int countryIndex, out int regionIndex) {
            if (!GetCountryUnderMouseInt(spherePoint, out countryIndex, out regionIndex)) {
                // Fallback: search by nearest city
                int ci = GetCityNearPointFast(spherePoint);
                if (ci >= 0) {
                    City city = cities[ci];
                    countryIndex = city.countryIndex;
                    regionIndex = GetCityCountryRegionIndex(city);
                }
            }
            return countryIndex >= 0 && regionIndex >= 0;
        }

        bool GetCountryUnderMouseInt (Vector3 spherePoint, out int countryIndex, out int regionIndex) {
            Vector2 latLon;
            Conversion.GetLatLonFromSpherePoint(spherePoint, out latLon);
            float maxArea = float.MaxValue;
            countryIndex = regionIndex = -1;

            // Check if current country is still under mouse
            if (_countryHighlightedIndex >= 0 && _countryHighlightedIndex < countries.Length && _countryHighlighted != null) {
                if (_countryHighlighted.regions != null && _countryRegionHighlightedIndex >= 0 && _countryRegionHighlightedIndex < _countryHighlighted.regions.Count) {
                    Region region = _countryHighlighted.regions[_countryRegionHighlightedIndex];
                    if (region.Contains(latLon)) {
                        maxArea = _countryHighlighted.mainRegionArea; // don't check for bigger countries since this is at least still highlighted - just check if any other smaller country inside this one can be highlighted
                        countryIndex = _countryHighlightedIndex;
                        regionIndex = _countryRegionHighlightedIndex;
                    }
                }
            }

            // Check countries with smaller area
            int cc = countriesOrderedBySize.Count;

            for (int c = 0; c < cc; c++) {
                Country country = _countries[_countriesOrderedBySize[c]];
                if (country.regions == null) continue;
                if (country.mainRegionArea >= maxArea) {
                    break;
                }
                if (country.hidden || !country.Contains(latLon, out int testRegionIndex))
                    continue;
                countryIndex = GetCountryIndex(country);
                regionIndex = testRegionIndex;
                return true;
            }

            return regionIndex >= 0;
        }

        /// <summary>
        /// Disables all country regions highlights. This doesn't remove custom materials.
        /// </summary>
        public void HideCountryRegionHighlights (bool destroyCachedSurfaces) {
            HideCountryRegionHighlight();
            if (countries == null)
                return;
            for (int c = 0; c < countries.Length; c++) {
                Country country = countries[c];
                if (country == null || country.regions == null)
                    continue;
                int crCount = country.regions.Count;
                for (int cr = 0; cr < crCount; cr++) {
                    Region region = country.regions[cr];
                    int cacheIndex = GetCacheIndexForCountryRegion(c, cr);
                    GameObject surf;
                    if (surfaces.TryGetValue(cacheIndex, out surf)) {
                        if (surf == null) {
                            surfaces.Remove(cacheIndex);
                        } else {
                            if (destroyCachedSurfaces) {
                                surfaces.Remove(cacheIndex);
                                DestroyImmediate(surf);
                            } else {
                                if (region.customMaterial == null) {
                                    surf.SetActive(false);
                                } else {
                                    if (region.customOutline) {
                                        ToggleCountryRegionSurface(c, cr, true, region.customMaterial.color, (Texture2D)region.customMaterial.mainTexture, region.customTextureScale, region.customTextureOffset, region.customTextureRotation, true, region.customOutlineColor);
                                    } else {
                                        ApplyMaterialToSurface(surf, region.customMaterial);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        void HideCountryRegionHighlight () {
            HideProvinceRegionHighlight();
            HideCityHighlight();
            if (_countryRegionHighlightedIndex >= 0 && _countryRegionHighlightedIndex >= 0) {
                if (_countryHighlighted != null) {
                    int rCount = _countryHighlighted.regions.Count;
                    for (int k = 0; k < rCount; k++) {
                        HideCountryRegionHighlightSingle(_countryHighlightedIndex, k);
                    }
                }
                countryRegionHighlightedObj = null;
            }

            // hide highlighted continent
            int hc = highlightedCountries.Count;
            for (int k = 0; k < hc; k++) {
                Country country = highlightedCountries[k];
                int cindex = GetCountryIndex(country);
                int rCount = country.regions.Count;
                for (int r = 0; r < rCount; r++) {
                    HideCountryRegionHighlightSingle(cindex, r);
                }
            }
            highlightedCountries.Clear();

            if (OnCountryExit != null && _countryHighlightedIndex >= 0) {
                OnCountryExit(_countryHighlightedIndex, _countryRegionHighlightedIndex);
            }

            hudMatCountry.mainTexture = null;
            _countryHighlighted = null;
            _countryHighlightedIndex = -1;
            _countryRegionHighlighted = null;
            _countryRegionHighlightedIndex = -1;
        }

        void HideCountryRegionHighlightSingle (int countryIndex, int regionIndex) {
            int cacheIndex = GetCacheIndexForCountryRegion(countryIndex, regionIndex);
            Country countryHighlighted = _countries[countryIndex];
            Region region = countryHighlighted.regions[regionIndex];
            GameObject surf;
            surfaces.TryGetValue(cacheIndex, out surf);
            if (surf == null) {
                surfaces.Remove(cacheIndex);
            } else {
                Material mat = region.customMaterial;
                if (mat != null) {
                    surf.GetComponent<Renderer>().sharedMaterial = mat;
                } else {
                    surf.SetActive(false);
                }
                if (region.customOutline) {
                    DrawRegionOutline(region, surf, false, region.customOutlineColor);
                } else {
                    ToggleRegionOutline(region, false);
                }
            }
        }

        public GameObject HighlightCountry (int countryIndex, bool refreshGeometry, bool drawOutline, Color outlineColor, bool includeContinent = false) {
            return HighlightCountryRegion(countryIndex, 0, refreshGeometry, drawOutline, outlineColor, true, includeContinent);
        }

        public GameObject HighlightCountryRegion (int countryIndex, int regionIndex, bool refreshGeometry, bool drawOutline, Color outlineColor) {
            return HighlightCountryRegion(countryIndex, regionIndex, refreshGeometry, drawOutline, outlineColor, _highlightAllCountryRegions, _enableContinentHighlight);
        }


        public GameObject HighlightCountryRegion (int countryIndex, int regionIndex, bool refreshGeometry, bool drawOutline, Color outlineColor, bool includeAllCountryRegions, bool includeContinent) {
#if PAINT_MODE
			ToggleCountrySurface(countryIndex, true, Color.white);
			return null; 
#else

            if (countryIndex < 0 || countryIndex >= countries.Length || regionIndex < 0 || regionIndex >= countries[countryIndex].regions.Count)
                return null;

            if (countryRegionHighlightedObj != null) {
                if (countryIndex == _countryHighlightedIndex && regionIndex == _countryRegionHighlightedIndex && !refreshGeometry)
                    return countryRegionHighlightedObj;
                HideCountryRegionHighlight();
            }

            Country country = _countries[countryIndex];

            if (_enableCountryHighlight) {

                Shader.SetGlobalVector(ShaderParams.CountryHighlightData, new Vector4(Time.timeSinceLevelLoad - 0.01f, _countryHighlightFadeDuration + 0.001f, 0, 0));
                countryRegionHighlightedObj = HighlightCountryRegionSingle(countryIndex, regionIndex, refreshGeometry, drawOutline, outlineColor);
                if (includeAllCountryRegions) {
                    int rCount = country.regions.Count;
                    for (int r = 0; r < rCount; r++) {
                        if (r != regionIndex) {
                            HighlightCountryRegionSingle(countryIndex, r, refreshGeometry, drawOutline, outlineColor);
                        }
                    }
                }

                if (includeContinent) {
                    int countriesLength = countries.Length;
                    for (int k = 0; k < countriesLength; k++) {
                        if (k == countryIndex) continue;
                        Country otherCountry = _countries[k];
                        if (country.continent.Equals(otherCountry.continent)) {
                            highlightedCountries.Add(otherCountry);
                            if (includeAllCountryRegions) {
                                int rCount = otherCountry.regions.Count;
                                for (int r = 0; r < rCount; r++) {
                                    HighlightCountryRegionSingle(k, r, false, false, Color.black);
                                }
                            } else {
                                int otherRegionIndex = otherCountry.mainRegionIndex;
                                HighlightCountryRegionSingle(k, otherRegionIndex, false, false, Color.black);
                            }
                        }
                    }
                }

                if (currentDecoratorCount > 0) {
                    decorator.ForceUpdateDecorators();
                }
            }

            _countryHighlightedIndex = countryIndex;
            _countryRegionHighlighted = country.regions[regionIndex];
            _countryRegionHighlightedIndex = regionIndex;
            _countryHighlighted = countries[countryIndex];
            _continentHighlighted = country.continent;

            return countryRegionHighlightedObj;
#endif
        }

        GameObject HighlightCountryRegionSingle (int countryIndex, int regionIndex, bool refreshGeometry, bool drawOutline, Color outlineColor) {
            int cacheIndex = GetCacheIndexForCountryRegion(countryIndex, regionIndex);
            bool existsInCache = surfaces.TryGetValue(cacheIndex, out GameObject surf);
            if (refreshGeometry && existsInCache) {
                surfaces.Remove(cacheIndex);
                DestroyImmediate(surf);
                existsInCache = false;
            }

            bool doHighlight = true;
            if (_countryHighlightMaxScreenAreaSize < 1f) {
                // Check screen area size
                Region region = countries[countryIndex].regions[regionIndex];
                doHighlight = CheckGlobeDistanceForHighlight(region, _countryHighlightMaxScreenAreaSize);
            }
            if (doHighlight) {
                if (existsInCache) {
                    surf = surfaces[cacheIndex];
                    if (surf == null) {
                        surfaces.Remove(cacheIndex);
                    } else {
                        if (!surf.activeSelf) {
                            surf.SetActive(true);
                        }
                        if (fillColor.a > 0) {
                            Renderer r = surf.GetComponent<Renderer>();
                            Material surfMat = r.sharedMaterial;
                            if (surfMat != hudMatCountry) {
                                if (surfMat.mainTexture != null) {
                                    hudMatCountry.mainTexture = surfMat.mainTexture;
                                } else {
                                    hudMatCountry.mainTexture = null;
                                }
                                r.sharedMaterial = hudMatCountry;
                            }
                        }
                        if (drawOutline) {
                            DrawRegionOutline(_countries[countryIndex].regions[regionIndex], surf, true, outlineColor);
                        }
                        return surf;
                    }
                }
                surf = GenerateCountryRegionSurface(countryIndex, regionIndex, hudMatCountry, Misc.Vector2one, Misc.Vector2zero, 0, drawOutline, outlineColor, true);
            }
            return surf;
        }

        bool CheckGlobeDistanceForHighlight (Region region, float threshold) {
            if (_showTiles && _currentZoomLevel > _tileMaxZoomLevelFrontiers) return false;
            Camera cam = mainCamera;
            Vector3 regionTR = Conversion.GetSpherePointFromLatLon(region.latlonRect2D.max);
            Vector3 regionBL = Conversion.GetSpherePointFromLatLon(region.latlonRect2D.min);
            Vector2 scrTR = cam.WorldToViewportPoint(transform.TransformPoint(regionTR));
            Vector2 scrBL = cam.WorldToViewportPoint(transform.TransformPoint(regionBL));
            Rect scrRect = new Rect(scrBL.x, scrTR.y, Math.Abs(scrTR.x - scrBL.x), Mathf.Abs(scrTR.y - scrBL.y));
            float highlightedArea = Mathf.Clamp01(scrRect.width * scrRect.height);
            return highlightedArea < threshold;
        }

        GameObject GenerateCountryRegionSurface (int countryIndex, int regionIndex, Material material, bool drawOutline, Color outlineColor, bool temporary) {
            return GenerateCountryRegionSurface(countryIndex, regionIndex, material, Misc.Vector2one, Misc.Vector2zero, 0, drawOutline, outlineColor, temporary);
        }

        void CountrySubstractProvinceEnclaves (int countryIndex, Region region, Poly2Tri.Polygon poly) {
            List<Region> negativeRegions = new List<Region>();
            for (int op = 0; op < _countries.Length; op++) {
                if (op == countryIndex)
                    continue;
                Country opCountry = _countries[op];
                if (opCountry.provinces == null)
                    continue;
                if (opCountry.regionsRect2D.Overlaps(region.latlonRect2D, true)) {
                    int provCount = opCountry.provinces.Length;
                    for (int p = 0; p < provCount; p++) {
                        Province oProv = opCountry.provinces[p];
                        if (oProv.regions == null)
                            ReadProvincePackedString(oProv);
                        Region oProvRegion = oProv.regions[oProv.mainRegionIndex];
                        if (region.Contains(oProvRegion)) { // just check main region of province for speed purposes
                            negativeRegions.Add(oProvRegion);
                        }
                    }
                }
            }
            // Collapse negative regions in big holes
            for (int nr = 0; nr < negativeRegions.Count - 1; nr++) {
                for (int nr2 = nr + 1; nr2 < negativeRegions.Count; nr2++) {
                    if (negativeRegions[nr].Intersects(negativeRegions[nr2])) {
                        Clipper clipper = new Clipper();
                        clipper.AddPath(negativeRegions[nr], PolyType.ptSubject);
                        clipper.AddPath(negativeRegions[nr2], PolyType.ptClip);
                        clipper.Execute(ClipType.ctUnion, negativeRegions[nr]);
                        negativeRegions.RemoveAt(nr2);
                        nr = -1;
                        break;
                    }
                }
            }

            // Substract holes
            for (int r = 0; r < negativeRegions.Count; r++) {
                Poly2Tri.Polygon polyHole = new Poly2Tri.Polygon(negativeRegions[r].latlon);
                poly.AddHole(polyHole);
            }
        }

        void CountrySubstractCountryEnclaves (int countryIndex, Region region, Poly2Tri.Polygon poly) {
            List<Region> negativeRegions = new List<Region>();
            int ccount = countriesOrderedBySize.Count;
            for (int ops = 0; ops < ccount; ops++) {
                int op = _countriesOrderedBySize[ops];
                if (op == countryIndex)
                    continue;
                Country opCountry = _countries[op];
                Region opCountryRegion = opCountry.regions[opCountry.mainRegionIndex];
                if (opCountryRegion.latlonRect2D.Overlaps(region.latlonRect2D, true)) {
                    if (region.Contains(opCountryRegion)) { // just check main region of province for speed purposes
                        negativeRegions.Add(opCountryRegion);
                    }
                }
            }
            // Collapse negative regions in big holes
            for (int nr = 0; nr < negativeRegions.Count - 1; nr++) {
                for (int nr2 = nr + 1; nr2 < negativeRegions.Count; nr2++) {
                    if (negativeRegions[nr].Intersects(negativeRegions[nr2])) {
                        Clipper clipper = new Clipper();
                        clipper.AddPath(negativeRegions[nr], PolyType.ptSubject);
                        clipper.AddPath(negativeRegions[nr2], PolyType.ptClip);
                        clipper.Execute(ClipType.ctUnion, negativeRegions[nr]);
                        negativeRegions.RemoveAt(nr2);
                        nr = -1;
                        break;
                    }
                }
            }

            // Substract holes
            for (int r = 0; r < negativeRegions.Count; r++) {
                Poly2Tri.Polygon polyHole = new Poly2Tri.Polygon(negativeRegions[r].latlon);
                poly.AddHole(polyHole);
            }
        }

        GameObject GenerateCountryRegionSurface (int countryIndex, int regionIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool drawOutline, Color outlineColor, bool temporary) {
            if (countryIndex < 0 || countryIndex >= countries.Length)
                return null;
            Country country = countries[countryIndex];
            int crCount = country.regions.Count;
            if (regionIndex < 0 || regionIndex >= crCount)
                return null;

            Region region = country.regions[regionIndex];
            if (region.latlon.Length < 3) {
                return null;
            }

            // Generate surface mesh
            int cacheIndex = GetCacheIndexForCountryRegion(countryIndex, regionIndex);
            GameObject surf = GenerateCountryRegionSurfaceOne(surfacesLayer.transform, SURFACE_GAMEOBJECT, countryIndex, regionIndex, material, textureScale, textureOffset, textureRotation, drawOutline, outlineColor, temporary);
            surfaces[cacheIndex] = surf;
            return surf;
        }


        GameObject GenerateCountryRegionSurfaceOne (Transform parent, string name, int countryIndex, int regionIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool drawOutline, Color outlineColor, bool temporary) {
            Country country = countries[countryIndex];
            Region region = country.regions[regionIndex];

            if (!temporary) {
                region.customColor = material.color;
                region.customTexture = material.mainTexture as Texture2D;
                region.customMaterial = material;
                region.customTextureOffset = textureOffset;
                region.customTextureRotation = textureRotation;
                region.customTextureScale = textureScale;
                region.customOutline = drawOutline;
                region.customOutlineColor = outlineColor;
                UpdateSurfaceCount();
            }

            if (region.latlon.Length < 3) {
                return null;
            }
            Poly2Tri.Polygon poly = new Poly2Tri.Polygon(region.latlon);

            // Extracts enclaves from main region
            if (_enableCountryEnclaves && regionIndex == country.mainRegionIndex) {
                // Remove negative provinces
                if (_showProvinces) {
                    CountrySubstractProvinceEnclaves(countryIndex, region, poly);
                } else {
                    CountrySubstractCountryEnclaves(countryIndex, region, poly);
                }
            }

            float step = _frontiersDetail == FRONTIERS_DETAIL.High ? 2f : 5f;
            if (steinerPoints == null) {
                steinerPoints = new List<TriangulationPoint>(1000);
            } else {
                steinerPoints.Clear();
            }
            float x0 = region.latlonRect2D.min.x + step / 2f;
            float x1 = region.latlonRect2D.max.x - step / 2f;
            float y0 = region.latlonRect2D.min.y + step / 2f;
            float y1 = region.latlonRect2D.max.y - step / 2f;
            for (float x = x0; x < x1; x += step) {
                for (float y = y0; y < y1; y += step) {
                    float xp = x;
                    float yp = y;
                    if (region.Contains(xp, yp)) {
                        steinerPoints.Add(new TriangulationPoint(xp, yp));
                    }
                }
            }

            if (steinerPoints.Count > 0) {
                poly.AddSteinerPoints(steinerPoints);
            }

            P2T.Triangulate(poly);

            int flip1, flip2;
            if (_earthInvertedMode) {
                flip1 = 2;
                flip2 = 1;
            } else {
                flip1 = 1;
                flip2 = 2;
            }
            int triCount = poly.Triangles.Count;
            Vector3[] revisedSurfPoints = new Vector3[triCount * 3];
            for (int k = 0; k < triCount; k++) {
                DelaunayTriangle dt = poly.Triangles[k];
                revisedSurfPoints[k * 3] = Conversion.GetSpherePointFromLatLon(dt.Points[0].X, dt.Points[0].Y);
                revisedSurfPoints[k * 3 + flip1] = Conversion.GetSpherePointFromLatLon(dt.Points[1].X, dt.Points[1].Y);
                revisedSurfPoints[k * 3 + flip2] = Conversion.GetSpherePointFromLatLon(dt.Points[2].X, dt.Points[2].Y);
            }
            int revIndex = revisedSurfPoints.Length - 1;

            // Generate surface mesh
            GameObject surf = Drawing.CreateSurface(name, revisedSurfPoints, revIndex, material, region.rect2Dbillboard, textureScale, textureOffset, textureRotation);
            surf.transform.SetParent(parent, false);
            surf.layer = parent.gameObject.layer;
            surf.transform.localPosition = Misc.Vector3zero;
            surf.transform.localRotation = Misc.QuaternionZero;
            if (_earthInvertedMode) {
                surf.transform.localScale = Misc.Vector3one * 0.998f;
            }
            if (region.surfaceGameObject != null) {
                DestroyImmediate(region.surfaceGameObject);
            }
            region.surfaceGameObject = surf;
            region.surfaceGameObjectIsDirty = false;

            // draw outline
            if (drawOutline) {
                DrawRegionOutline(region, surf, temporary, outlineColor);
            }

            return surf;
        }


        public void DestroyAllCountriesOutline () {
            if (_surfacesLayer == null)
                return;
            MeshRenderer[] rr = _surfacesLayer.GetComponentsInChildren<MeshRenderer>(true);
            for (int k = 0; k < rr.Length; k++) {
                if (rr[k].gameObject.name.Equals(OUTLINE_GAMEOBJECT_NAME)) {
                    DestroyImmediate(rr[k].gameObject);
                }
            }
        }

        #endregion

        #region Country manipulation

        bool ValidCountryIndex (int countryIndex) {
            return countryIndex >= 0 && countries != null && countryIndex < _countries.Length;
        }

        bool ValidCountryRegionIndex (int countryIndex, int regionIndex) {
            return countryIndex >= 0 && countries != null && countryIndex < _countries.Length && regionIndex >= 0 && _countries[countryIndex].regions != null && regionIndex < _countries[countryIndex].regions.Count;
        }

        /// <summary>
        /// Deletes the country. Optionally also delete its dependencies (provinces, cities, mountpoints).
        /// This internal method does not refresh cachés.
        /// </summary>
        bool internal_CountryDelete (int countryIndex, bool deleteDependencies) {
            if (countryIndex < 0 || countryIndex >= countries.Length)
                return false;

            // Update dependencies
            if (deleteDependencies) {
                List<Province> newProvinces = new List<Province>(provinces.Length);
                int k;
                for (k = 0; k < provinces.Length; k++) {
                    if (provinces[k].countryIndex != countryIndex)
                        newProvinces.Add(provinces[k]);
                }
                provinces = newProvinces.ToArray();
                lastProvinceLookupCount = -1;

                k = -1;
                while (++k < cities.Count) {
                    if (cities[k].countryIndex == countryIndex) {
                        cities.RemoveAt(k);
                        k--;
                    }
                }
                lastCityLookupCount = -1;

                k = -1;
                while (++k < mountPoints.Count) {
                    if (mountPoints[k].countryIndex == countryIndex) {
                        mountPoints.RemoveAt(k);
                        k--;
                    }
                }
            }

            // Updates provinces reference to country
            for (int k = 0; k < provinces.Length; k++) {
                if (provinces[k].countryIndex > countryIndex)
                    provinces[k].countryIndex--;
            }

            // Updates country index in cities
            int cityCount = cities.Count;
            for (int k = 0; k < cityCount; k++) {
                if (cities[k].countryIndex > countryIndex) {
                    cities[k].countryIndex--;
                }
            }
            // Updates country index in mount points
            if (mountPoints != null) {
                for (int k = 0; k < mountPoints.Count; k++) {
                    if (mountPoints[k].countryIndex > countryIndex) {
                        mountPoints[k].countryIndex--;
                    }
                }
            }

            // Excludes country from new array
            List<Country> newCountries = new List<Country>(countries.Length);
            for (int k = 0; k < countries.Length; k++) {
                if (k != countryIndex)
                    newCountries.Add(countries[k]);
            }
            countries = newCountries.ToArray();
            return true;
        }


        #endregion


    }

}