using UnityEngine;
using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace WPM {

    public enum TILE_SERVER {
        OpenStreeMap = 10,
        OpenStreeMapDE = 11,
        OpenStreeMapHiking = 12,
        StamenToner = 20,
        StamenWaterColor = 21,
        StamenTerrain = 22,
        CartoLightAll = 40,
        CartoDarkAll = 41,
        CartoNoLabels = 42,
        CartoOnlyLabels = 43,
        CartoDarkNoLabels = 44,
        CartoDarkOnlyLabels = 45,
        WikiMediaAtlas = 50,
        ThunderForestLandscape = 60,
        OpenTopoMap = 70,
        GoogleMapsSatellite = 80,
        GoogleMapsSatelliteNoLabels = 81,
        GoogleMapsRelief = 82,
        Sputnik = 100,
        XWeather = 110,
        MapsForFree = 120,
        USGSSattelite = 130,
        ESRITopo = 140,
        ESRIStreets = 141,
        ESRISatellite = 142,
        ESRINationalGeoStyle = 143,
        MapBoxSatellite = 150,
        MapBoxTerrain = 151,
        MapBoxTerrainRGB = 152,
        MapBoxCountries = 153,
        MapBoxIncidents = 154,
        MapBoxStreets = 155,
        MapBoxTraffic = 156,
        MapTilerSatellite = 160,
        MapTilerBasic = 161,
        MapTilerDataViz = 162,
        Custom = 999
    }

    public static class TileServerExtensions {

        public static bool IsMapBox(this TILE_SERVER server) {
            return (int)server >= 150 && (int)server < 160;
        }

        public static bool IsXweather(this TILE_SERVER server) {
            return server == TILE_SERVER.XWeather;
        }

        public static bool IsMapTiler(this TILE_SERVER server) {
            return (int)server >= 160 && (int)server < 170;
        }
    }

    public delegate string TileURLRequestEvent(string url, TILE_SERVER server, int zoomLevel, int x, int y);

    public partial class WorldMapGlobe : MonoBehaviour {

        public event TileURLRequestEvent OnTileURLRequest;

        public static string[] tileServerNames = new string[] {
            "Open Street Map",
            "Open Street Map (DE)",
            "Open Street Map (Hiking)",
            "Stamen Terrain",
            "Stamen Toner",
            "Stamen WaterColor",
            "Carto Light All",
            "Carto Dark All",
            "Carto (No Labels)",
            "Carto (Only Labels)",
            "Carto Dark (No Labels)",
            "Carto Dark (Only Labels)",
            "WikiMedia Atlas",
            "ThunderForest Landscape",
            "OpenTopoMap",
            "Google Maps Satellite",
            "Google Maps Satellite No Labels",
            "Google Maps Relief",
            "Sputnik",
            "Xweather",
            "Maps-For-Free",
            "USGS Satellite",
            "ESRI Topo",
            "ESRI Streets",
            "ESRI Satellite",
            "ESRI National Geo Style",
            "MapBox Satellite",
            "MapBox Terrain",
            "MapBox Terrain (RGB-encoded dem)",
            "MapBox Countries",
            "MapBox Incidents",
            "MapBox Streets",
            "MapBox Traffic",
            "MapTiler Satellite",
            "MapTiler Basic",
            "MapTiler DataViz",
            "Custom"
        };

        public static int[] tileServerValues = new int[] {
            (int)TILE_SERVER.OpenStreeMap,
            (int)TILE_SERVER.OpenStreeMapDE,
            (int)TILE_SERVER.OpenStreeMapHiking,
            (int)TILE_SERVER.StamenTerrain,
            (int)TILE_SERVER.StamenToner,
            (int)TILE_SERVER.StamenWaterColor,
            (int)TILE_SERVER.CartoLightAll,
            (int)TILE_SERVER.CartoDarkAll,
            (int)TILE_SERVER.CartoNoLabels,
            (int)TILE_SERVER.CartoOnlyLabels,
            (int)TILE_SERVER.CartoDarkNoLabels,
            (int)TILE_SERVER.CartoDarkOnlyLabels,
            (int)TILE_SERVER.WikiMediaAtlas,
            (int)TILE_SERVER.ThunderForestLandscape,
            (int)TILE_SERVER.OpenTopoMap,
            (int)TILE_SERVER.GoogleMapsSatellite,
            (int)TILE_SERVER.GoogleMapsSatelliteNoLabels,
            (int)TILE_SERVER.GoogleMapsRelief,
            (int)TILE_SERVER.Sputnik,
            (int)TILE_SERVER.XWeather,
            (int)TILE_SERVER.MapsForFree,
            (int)TILE_SERVER.USGSSattelite,
            (int)TILE_SERVER.ESRITopo,
            (int)TILE_SERVER.ESRIStreets,
            (int)TILE_SERVER.ESRISatellite,
            (int)TILE_SERVER.ESRINationalGeoStyle,
            (int)TILE_SERVER.MapBoxSatellite,
        (int)TILE_SERVER.MapBoxTerrain,
            (int)TILE_SERVER.MapBoxTerrainRGB,
            (int)TILE_SERVER.MapBoxCountries,
        (int)TILE_SERVER.MapBoxIncidents,
        (int)TILE_SERVER.MapBoxStreets,
        (int)TILE_SERVER.MapBoxTraffic,
        (int)TILE_SERVER.MapTilerSatellite,
        (int)TILE_SERVER.MapTilerBasic,
        (int)TILE_SERVER.MapTilerDataViz,
            (int)TILE_SERVER.Custom
        };


        public string GetTileServerCopyrightNotice(TILE_SERVER server) {

            string copyright;

            switch (server) {
                case TILE_SERVER.OpenStreeMap:
                case TILE_SERVER.OpenStreeMapDE:
                case TILE_SERVER.OpenStreeMapHiking:
                    copyright = "Map tiles © OpenStreetMap www.osm.org/copyright";
                    break;
                case TILE_SERVER.StamenToner:
                case TILE_SERVER.StamenTerrain:
                case TILE_SERVER.StamenWaterColor:
                    copyright = "Map tiles by Stamen Design, under CC BY 3.0. Data by OpenStreetMap, under ODbL.";
                    break;
                case TILE_SERVER.CartoLightAll:
                case TILE_SERVER.CartoDarkAll:
                case TILE_SERVER.CartoNoLabels:
                case TILE_SERVER.CartoOnlyLabels:
                case TILE_SERVER.CartoDarkNoLabels:
                case TILE_SERVER.CartoDarkOnlyLabels:
                    copyright = "Map tiles by Carto, under CC BY 3.0. Data by OpenStreetMap, under ODbL.";
                    break;
                case TILE_SERVER.WikiMediaAtlas:
                    copyright = "Map tiles © WikiMedia/Mapnik, Data © www.osm.org/copyright";
                    break;
                case TILE_SERVER.ThunderForestLandscape:
                    copyright = "Map tiles © www.thunderforest.com, Data © www.osm.org/copyright";
                    break;
                case TILE_SERVER.OpenTopoMap:
                    copyright = "Map tiles © OpenTopoMap, Data © www.osm.org/copyright";
                    break;
                case TILE_SERVER.GoogleMapsSatellite:
                case TILE_SERVER.GoogleMapsSatelliteNoLabels:
                case TILE_SERVER.GoogleMapsRelief:
                    copyright = "Map tiles © Google";
                    break;
                case TILE_SERVER.MapBoxSatellite:
                case TILE_SERVER.MapBoxTerrain:
                case TILE_SERVER.MapBoxTerrainRGB:
                case TILE_SERVER.MapBoxCountries:
                case TILE_SERVER.MapBoxIncidents:
                case TILE_SERVER.MapBoxStreets:
                case TILE_SERVER.MapBoxTraffic:
                    copyright = "Map tiles © MapBox";
                    break;
                case TILE_SERVER.MapTilerSatellite:
                case TILE_SERVER.MapTilerBasic:
                case TILE_SERVER.MapTilerDataViz:
                    copyright = "Map tiles @ MapTiler @ OpenStreetMap contributors";
                    break;
                case TILE_SERVER.Sputnik:
                    copyright = "Map tiles © Sputnik, Data © www.osm.org/copyright";
                    break;
                case TILE_SERVER.XWeather:
                    copyright = "Map tiles © Xweather, www.xweather.com";
                    break;
                case TILE_SERVER.MapsForFree:
                    copyright = "Map tiles © OpenStreetMap contributors";
                    break;
                case TILE_SERVER.USGSSattelite:
                    copyright = "Map tiles © USGS, www.usgs.gov";
                    break;
                case TILE_SERVER.ESRITopo:
                case TILE_SERVER.ESRIStreets:
                case TILE_SERVER.ESRISatellite:
                case TILE_SERVER.ESRINationalGeoStyle:
                    copyright = "Map tiles © ESRI";
                    break;
                case TILE_SERVER.Custom:
                    copyright = "";
                    break;
                default:
                    Debug.LogError("Tile server not defined: " + tileServer.ToString());
                    copyright = "";
                    break;
            }

            return copyright;
        }

        static string[] subservers = new string[] { "a", "b", "c" };

        public string GetTileURL(TILE_SERVER server, TileInfo ti, bool forceHTTPS) {

            string url;
            subserverSeq++;
            if (subserverSeq > 100000)
                subserverSeq = 0;

            switch (_tileServer) {
                case TILE_SERVER.OpenStreeMap:
                    url = "http://" + subservers[subserverSeq % 3] + ".tile.openstreetmap.org/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.OpenStreeMapDE:
                    url = "http://" + subservers[subserverSeq % 3] + ".tile.openstreetmap.de/tiles/osmde/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.OpenStreeMapHiking:
                    url = "http://" + subservers[subserverSeq % 3] + ".tiles.wmflabs.org/hikebike/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.StamenToner:
                    url = "http://tile.stamen.com/toner/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.StamenTerrain:
                    url = "http://tile.stamen.com/terrain/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.StamenWaterColor:
                    url = "http://tile.stamen.com/watercolor/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.CartoLightAll:
                    url = "http://" + subservers[subserverSeq % 3] + ".basemaps.cartocdn.com/light_all/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.CartoDarkAll:
                    url = "http://" + subservers[subserverSeq % 3] + ".basemaps.cartocdn.com/dark_all/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.CartoNoLabels:
                    url = "http://" + subservers[subserverSeq % 3] + ".basemaps.cartocdn.com/light_nolabels/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.CartoOnlyLabels:
                    url = "http://" + subservers[subserverSeq % 3] + ".basemaps.cartocdn.com/light_only_labels/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.CartoDarkNoLabels:
                    url = "http://" + subservers[subserverSeq % 3] + ".basemaps.cartocdn.com/dark_nolabels/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.CartoDarkOnlyLabels:
                    url = "http://" + subservers[subserverSeq % 3] + ".basemaps.cartocdn.com/dark_only_labels/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.WikiMediaAtlas:
                    url = "http://" + subservers[subserverSeq % 3] + ".tiles.wmflabs.org/bw-mapnik/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.ThunderForestLandscape:
                    url = "http://" + subservers[subserverSeq % 3] + ".tile.thunderforest.com/landscape/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.OpenTopoMap:
                    url = "http://" + subservers[subserverSeq % 3] + ".tile.opentopomap.org/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.GoogleMapsSatellite:
                    url = "http://mt" + (subserverSeq % 4) + ".googleapis.com/vt/lyrs=y&hl=en&z=" + ti.zoomLevel + "&x=" + ti.x + "&y=" + ti.y;
                    break;
                case TILE_SERVER.GoogleMapsSatelliteNoLabels:
                    url = "http://khm" + (subserverSeq % 4) + ".googleapis.com/kh?v=946&hl=en&z=" + ti.zoomLevel + "&x=" + ti.x + "&y=" + ti.y;
                    break;
                case TILE_SERVER.GoogleMapsRelief:
                    url = "http://mt" + (subserverSeq % 4) + ".googleapis.com/vt/lyrs=t@131,r@216000000&src=app&hl=en&z=" + ti.zoomLevel + "&x=" + ti.x + "&y=" + ti.y;
                    break;
                case TILE_SERVER.MapBoxSatellite:
                    url = "https://api.mapbox.com/v4/mapbox.satellite/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapBoxCountries:
                    url = "https://api.mapbox.com/v4/mapbox.country-boundaries-v1/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapBoxTraffic:
                    url = "https://api.mapbox.com/v4/mapbox.mapbox-traffic-v1/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapBoxIncidents:
                    url = "https://api.mapbox.com/v4/mapbox.mapbox-incidents-v1/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapBoxStreets:
                    url = "https://api.mapbox.com/v4/mapbox.mapbox-streets-v8/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapBoxTerrain:
                    url = "https://api.mapbox.com/v4/mapbox.mapbox-terrain-v2/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapBoxTerrainRGB:
                    url = "https://api.mapbox.com/v4/mapbox.terrain-rgb/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?access_token=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapTilerSatellite:
                    url = "https://api.maptiler.com/tiles/satellite-v2/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".jpg?key=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapTilerBasic:
                    url = "https://api.maptiler.com/maps/basic-v2/256/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?key=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapTilerDataViz:
                    url = "https://api.maptiler.com/maps/dataviz/256/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png?key=" + _tileServerAPIKey;
                    break;
                case TILE_SERVER.MapsForFree:
                    url = "https://maps-for-free.com/layer/relief/z" + ti.zoomLevel + "/row" + ti.y + "/" + ti.zoomLevel + "_" + ti.x + "-" + ti.y + ".jpg";
                    break;
                case TILE_SERVER.USGSSattelite:
                    url = "https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer/tile/" + ti.zoomLevel + "/" + ti.y + "/" + ti.x;
                    break;
                case TILE_SERVER.Sputnik:
                    url = "http://" + subservers[subserverSeq % 3] + ".tiles.maps.sputnik.ru/tiles/kmt2/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + ".png";
                    break;
                case TILE_SERVER.XWeather:
                    //https://maps[server].aerisapi.com/[client_id]_[client_key]/[type]/[zoom]/[x]/[y]/[offset].png
                    //url = "http://maps" + ((subserverSeq % 4) + 1).ToString() + ".aerisapi.com/" + _tileServerClientId + "_" + _tileServerAPIKey + "/" + _tileServerLayerTypes + "/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + "/" + _tileServerTimeOffset + ".png";
                    url = "http://maps" + ((subserverSeq % 4) + 1).ToString() + ".api.xweather.com/" + _tileServerClientId + "_" + _tileServerAPIKey + "/" + _tileServerLayerTypes + "/" + ti.zoomLevel + "/" + ti.x + "/" + ti.y + "/" + _tileServerTimeOffset + ".png";
                    break;
                case TILE_SERVER.ESRITopo:
                    url = "https://services.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/" + ti.zoomLevel + "/" + ti.y + "/" + ti.x + ".jpg";
                    break;
                case TILE_SERVER.ESRIStreets:
                    url = "http://services.arcgisonline.com/ArcGIS/rest/services/World_Street_Map/MapServer/tile/" + ti.zoomLevel + "/" + ti.y + "/" + ti.x;
                    break;
                case TILE_SERVER.ESRISatellite:
                    url = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/" + ti.zoomLevel + "/" + ti.y + "/" + ti.x + ".jpg";
                    break;
                case TILE_SERVER.ESRINationalGeoStyle:
                    url = "http://services.arcgisonline.com/ArcGIS/rest/services/NatGeo_World_Map/MapServer/tile/" + ti.zoomLevel + "/" + ti.y + "/" + ti.x;
                    break;
                case TILE_SERVER.Custom:
                    StringBuilder sb = new StringBuilder(_tileServerCustomUrl);
                    sb.Replace("$n$", subservers[subserverSeq % 3]);
                    sb.Replace("$N$", subservers[subserverSeq % 3]);
                    sb.Replace("$X$", ti.x.ToString());
                    sb.Replace("$x$", ti.x.ToString());
                    sb.Replace("$Y$", ti.y.ToString());
                    sb.Replace("$y$", ti.y.ToString());
                    sb.Replace("$Z$", ti.zoomLevel.ToString());
                    sb.Replace("$z$", ti.zoomLevel.ToString());
                    url = sb.ToString();
                    break;
                default:
                    Debug.LogError("Tile server not defined: " + tileServer.ToString());
                    url = "";
                    break;
            }

            if (!server.IsMapBox() && !server.IsXweather() && !server.IsMapTiler() && server != TILE_SERVER.Custom && !string.IsNullOrEmpty(_tileServerAPIKey)) {
                url += "?" + _tileServerAPIKey;
            }

            if (forceHTTPS) {
                url = url.Replace("http://", "https://");
            }
#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
            else {
                if (UnityEditor.PlayerSettings.insecureHttpOption != UnityEditor.InsecureHttpOption.AlwaysAllowed) {
                    Debug.LogWarning("Enable non-secure connections in Player Settings or try enabling 'Use Secure Connection' option in World Map Globe inspector.");
                }
            }
#endif

            if (OnTileURLRequest != null) {
                url = OnTileURLRequest(url, server, ti.zoomLevel, ti.x, ti.y);
            }

            return url;

        }

    }

}