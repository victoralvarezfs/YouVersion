﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WPMF {
    /// <summary>
    /// City scaler. Checks the city icons' size is always appropiate
    /// </summary>
    public class CityScaler : MonoBehaviour {

        const float CITY_SIZE_ON_SCREEN = 10.0f;
        Vector3 lastCamPos, lastPos;
        float lastIconSize;
        float lastCustomSize;
        float lastOrtographicSize;

        [NonSerialized]
        public WorldMap2D map;

        void Start() {
            ScaleCities();
        }

        // Update is called once per frame
        void Update() {
            if (lastPos != transform.position || lastCamPos != map.currentCamera.transform.position || lastIconSize != map.cityIconSize ||
                map.currentCamera.orthographic && map.currentCamera.orthographicSize != lastOrtographicSize) {
                ScaleCities();
            }
        }

        public void ScaleCities() {
            // Get distance to camera
            Camera cam = map.currentCamera;
            if (cam == null) return;
            lastPos = transform.position;
            lastCamPos = cam.transform.position;
            lastIconSize = map.cityIconSize;
            lastOrtographicSize = cam.orthographicSize;

            Plane plane = new Plane(transform.forward, transform.position);
            float dist = plane.GetDistanceToPoint(lastCamPos);
            Vector3 centerPos = lastCamPos - transform.forward * dist;
            Vector3 a = cam.WorldToScreenPoint(centerPos);
            Vector3 b = new Vector3(a.x, a.y + CITY_SIZE_ON_SCREEN, a.z);
            if (cam.pixelWidth == 0) return; // Camera pending setup
            Vector3 aa = cam.ScreenToWorldPoint(a);
            Vector3 bb = cam.ScreenToWorldPoint(b);
            float scale = (aa - bb).magnitude * map.cityIconSize;
            if (cam.orthographic) {
                scale /= 1 + (cam.orthographicSize * cam.orthographicSize) * (0.1f / map.transform.localScale.x);
            } else {
                scale /= 1 + dist * dist * (0.1f / map.transform.localScale.x);
            }
            Vector3 newScale = new Vector3(scale / WorldMap2D.mapWidth, scale / WorldMap2D.mapHeight, 1.0f);
            foreach (Transform t in transform.Find("Normal Cities"))
                t.localScale = newScale;
            foreach (Transform t in transform.Find("Region Capitals"))
                t.localScale = newScale * 1.75f;
            foreach (Transform t in transform.Find("Country Capitals"))
                t.localScale = newScale * 2.0f;
        }

        public void ScaleCities(float customSize) {
            if (customSize == lastCustomSize) return;
            lastCustomSize = customSize;
            Vector3 newScale = new Vector3(customSize / WorldMap2D.mapWidth, customSize / WorldMap2D.mapHeight, 1);
            foreach (Transform t in transform.Find("Normal Cities"))
                t.localScale = newScale;
            foreach (Transform t in transform.Find("Region Capitals"))
                t.localScale = newScale * 1.75f;
            foreach (Transform t in transform.Find("Country Capitals"))
                t.localScale = newScale * 2.0f;
        }
    }

}