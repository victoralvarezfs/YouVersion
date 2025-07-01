using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WPMF {

    /// <summary>
    /// Mount Point scaler (similar to City Scaler). Checks the mount point icons' size is always appropiate
    /// </summary>
    public class MountPointScaler : MonoBehaviour {

        const float MOUNTPOINT_SIZE_ON_SCREEN = 10.0f;
        Vector3 lastCamPos, lastPos;
        float lastIconSize;
        float lastCustomSize;
        float lastOrtographicSize;

        [NonSerialized]
        public WorldMap2D map;

        void Start() {
            ScaleMountPoints();
        }

        // Update is called once per frame
        void Update() {
            if (lastPos != transform.position || lastCamPos != map.currentCamera.transform.position || lastIconSize != map.cityIconSize ||
                map.currentCamera.orthographic && map.currentCamera.orthographicSize != lastOrtographicSize) {
                ScaleMountPoints();
            }
        }

        public void ScaleMountPoints() {
            if (map == null) return;
            Camera cam = map.currentCamera;
            if (cam == null) return;

            map.GetLocalHitFromScreenPos(Input.mousePosition, out lastPos);
            lastPos = transform.TransformPoint(lastPos);
            lastCamPos = cam.transform.position; // Camera.main.transform.position;
            lastIconSize = map.cityIconSize;
            lastOrtographicSize = cam.orthographicSize;

            Vector3 a = map.currentCamera.WorldToScreenPoint(transform.position);
            Vector3 b = new Vector3(a.x, a.y + MOUNTPOINT_SIZE_ON_SCREEN, a.z);
            if (cam.pixelWidth == 0) return; // Camera pending setup
            Vector3 aa = cam.ScreenToWorldPoint(a);
            Vector3 bb = cam.ScreenToWorldPoint(b);
            float scale = (aa - bb).magnitude * map.cityIconSize;
            if (cam.orthographic) {
                scale /= 1 + (cam.orthographicSize * cam.orthographicSize) * (0.1f / map.transform.localScale.x);
            } else {

                scale /= 1 + (lastCamPos - lastPos).sqrMagnitude * (0.1f / map.transform.localScale.x);
            }
            Vector3 newScale = new Vector3(scale / WorldMap2D.mapWidth, scale / WorldMap2D.mapHeight, 1.0f);
            newScale *= 2.0f;
            foreach (Transform t in transform)
                t.localScale = newScale;
        }

        public void ScaleMountPoints(float customSize) {
            if (customSize == lastCustomSize) return;
            lastCustomSize = customSize;
            Vector3 newScale = new Vector3(customSize / WorldMap2D.mapWidth, customSize / WorldMap2D.mapHeight, 1);
            newScale *= 2.0f;
            foreach (Transform t in transform)
                t.localScale = newScale;
        }
    }

}