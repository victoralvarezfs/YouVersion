// World Political Map - 2D Edition for Unity - Main Script
// Copyright 2015-2022 Kronnect
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WPM

//#define TRACE_CTL

using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WPMF {

    public partial class WorldMap2D : MonoBehaviour {

        #region Render viewport

        public enum ViewportMode {
            None,
            Viewport3D,
            MapPanel
        }


        ViewportMode viewportMode;
        RectTransform renderViewportUIPanel;

        public bool renderViewportEnabled { get { return _renderViewport != gameObject; } }

        void SetupViewport() {

            if (!gameObject.activeInHierarchy) {
                _currentCamera = _mainCamera;
                _renderViewport = gameObject;
                return;
            }
            if (_renderViewport == null)
                _renderViewport = gameObject;

            if (_renderViewport == gameObject) {
                DestroyOverlayRT();
                if (this.overlayLayer != null) {
                    DestroyMapperCam();
                }
                _currentCamera = _mainCamera;
                viewportMode = ViewportMode.None;
                return;
            }

            // Is it a Map Panel?
            if (_renderViewport.GetComponent<MapPanel>() != null) {
                viewportMode = ViewportMode.MapPanel;
                RectTransform rt = _renderViewport.GetComponent<RectTransform>();
                renderViewportUIPanel = rt;
            } else {
                viewportMode = ViewportMode.Viewport3D;
            }

            // Setup Render texture
            int imageWidth, imageHeight;
            imageWidth = 2048;
            imageHeight = 1024;
            if (overlayRT != null && (overlayRT.width != imageWidth || overlayRT.height != imageHeight || overlayRT.filterMode != _renderViewportFilterMode)) {
                DestroyOverlayRT();
            }

            GameObject overlayLayer = GetOverlayLayer(true);
            if (overlayRT == null) {
                overlayRT = new RenderTexture(imageWidth, imageHeight, 0);
                overlayRT.filterMode = _renderViewportFilterMode; // FilterMode.Trilinear; -> trilinear causes blurry issues with NGUI
                overlayRT.anisoLevel = 0;
                overlayRT.useMipMap = (_renderViewportFilterMode == FilterMode.Trilinear);
            }

            // Camera
            mapperCamName = MAPPER_CAM_PREFIX + "_" + gameObject.name;
            GameObject camObj = GameObject.Find(mapperCamName);
            if (camObj == null) {
                camObj = new GameObject(mapperCamName, typeof(Camera));
                camObj.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                camObj.layer = overlayLayer.layer;
            }
            mapperCam = camObj.GetComponent<Camera>();
            mapperCam.cullingMask = 1 << camObj.layer;
            mapperCam.clearFlags = CameraClearFlags.SolidColor;
            mapperCam.backgroundColor = new Color(0, 0, 0, 0);
            mapperCam.targetTexture = overlayRT;
            mapperCam.nearClipPlane = 0.01f;
            mapperCam.farClipPlane = 1000;
            mapperCam.enabled = true;
            mapperCam.targetTexture = overlayRT;
            if (_currentCamera != mapperCam) {
                _currentCamera = mapperCam;
                if (transform.position.x < 5000) {
                    transform.position = new Vector3(5000, 1000, 5000); // moves the main gameobject away: note: can't use 10000,10000,10000 for precision problems
                }
                _currentCamera.transform.position = transform.position + Misc.Vector3back * 86.5f; // default camera position for a standard height of 100
                CenterMap();
            }
            // Assigns render texture to current material and recreates the camera
            // Specific support depending on viewport type
            switch (viewportMode) {
                case ViewportMode.MapPanel:
                    MapPanel mapPanel = _renderViewport.GetComponent<MapPanel>();
                    if (mapPanel != null) {
                        Material viewportMat = mapPanel.material;
                        if (viewportMat != null) {
                            Shader shader = Shader.Find("World Political Map 2D/UI Viewport");
                            viewportMat.shader = shader;
                            viewportMat.mainTexture = overlayRT;
                            mapPanel.SetMaterialDirty();
                        }
                    }
                    break;
                default:
                    Renderer rvRenderer = _renderViewport.GetComponent<Renderer>();
                    if (rvRenderer.sharedMaterial == null) {
                        rvRenderer.sharedMaterial = Resources.Load<Material>("WPMF/Materials/ViewportMaterial");
                    }
                    Material rvMat = Instantiate(rvRenderer.sharedMaterial);
                    rvMat.mainTexture = overlayRT;
                    rvRenderer.sharedMaterial = rvMat;
                    // Assign mouse interaction proxy on viewport
                    PointerTrigger pt = _renderViewport.GetComponent<PointerTrigger>() ?? _renderViewport.AddComponent<PointerTrigger>();
                    pt.map = this;


                    // Position the viewport if overlay mode is enabled
                    if (_renderViewportScreenOverlay) {
                        _renderViewport.transform.SetParent(_mainCamera.transform, false);
                        _renderViewport.transform.localRotation = Quaternion.Euler(0, 0, 0);
                        float dist = (_mainCamera.nearClipPlane + 0.1f);
                        Vector3 bl = _mainCamera.ViewportToWorldPoint(new Vector3(_renderViewportScreenRect.xMin, _renderViewportScreenRect.yMin, dist));
                        Vector3 tr = _mainCamera.ViewportToWorldPoint(new Vector3(_renderViewportScreenRect.xMax, _renderViewportScreenRect.yMax, dist));
                        _renderViewport.transform.localScale = new Vector3(tr.x - bl.x, tr.y - bl.y, 1);
                        Vector3 vc = _mainCamera.transform.InverseTransformPoint((bl + tr) * 0.5f);
                        _renderViewport.transform.localPosition = vc;
                    } else {
                        if (_renderViewport.transform.parent == _mainCamera.transform) {
                            _renderViewport.transform.SetParent(null, true);
                        }
                    }
                    break;
            }

            CheckViewportScale();
        }


        void DestroyMapperCam() {
            if (mapperCam != null) {
                DestroyImmediate(mapperCam.gameObject);
                mapperCam = null;
                if (!string.IsNullOrEmpty(mapperCamName)) {
                    GameObject o = GameObject.Find(mapperCamName);
                    if (o != null)
                        DestroyImmediate(o);
                }
            }
        }


        /// <summary>
        /// Ensure the proportions of the main map fit the aspect ratio of the render viewport
        /// </summary>
        void CheckViewportScale() {
            if (viewportMode == ViewportMode.None || _renderViewport == null || _renderViewport == gameObject || mapperCam == null)
                return;

            Vector3 scale = new Vector3(transform.localScale.y * 2f, transform.localScale.y, 1f);
            if (transform.localScale != scale) {
                if (scale.x != 0 && scale.y != 0) {
                    transform.localScale = scale;
                }
            }

            float aspect = 1f;
            switch (viewportMode) {
                case ViewportMode.MapPanel: {
                        Rect wsRect = GetWorldRect(renderViewportUIPanel);
                        if (wsRect.size.x != 0 && wsRect.size.y != 0) {
                            aspect = wsRect.size.x / wsRect.size.y;
                        }
                    }
                    break;
                default: {
                        Vector3 rvScale = _renderViewport.transform.localScale;
                        if (rvScale.x != 0 && rvScale.y != 0) {
                            aspect = rvScale.x / rvScale.y;
                        }
                    }
                    break;
            }
            mapperCam.aspect = aspect;

            FitViewportToUIPanel();
        }

        Vector3[] wc;
        Vector3 panelUIOldPosition;
        Vector2 panelUIOldSize;


        void FitViewportToUIPanel() {

            if (viewportMode != ViewportMode.Viewport3D || renderViewportUIPanel == null)
                return;

            if (Application.isPlaying && panelUIOldPosition == renderViewportUIPanel.position && panelUIOldSize == renderViewportUIPanel.sizeDelta) {
                return;
            }

            // Check if positions are different
            Camera cam = mainCamera;
            if (cam == null) return;

            Rect rect = GetWorldRect(renderViewportUIPanel);
            float zDistance = cam.farClipPlane - 10f;
            Vector3 bl = new Vector3(rect.xMin, rect.yMax, zDistance);
            Vector3 tr = new Vector3(rect.xMax, rect.yMin, zDistance);
            Vector3 br = new Vector3(rect.xMax, rect.yMax, zDistance);
            bl = cam.ScreenToWorldPoint(bl);
            br = cam.ScreenToWorldPoint(br);
            tr = cam.ScreenToWorldPoint(tr);

            Transform t = _renderViewport.transform;

            Vector3 pos = (bl + tr) * 0.5f;
            float width = Vector3.Distance(bl, br);
            float height = Vector3.Distance(br, tr);

            t.position = pos;
            t.localScale = new Vector3(width, height, 1f);
            t.forward = cam.transform.forward;

            if (!flyToActive && panelUIOldSize.x == 0) {
                CenterMap();
            }

#if UNITY_EDITOR
            if (Application.isPlaying && panelUIOldSize != renderViewportUIPanel.sizeDelta) {
                SetupViewport();
            }
#endif

            panelUIOldPosition = renderViewportUIPanel.position;
            panelUIOldSize = renderViewportUIPanel.sizeDelta;

        }

        Rect GetWorldRect(RectTransform rt) {
            if (rt == null) return Rect.zero;
            if (wc == null || wc.Length < 4) wc = new Vector3[4];
            rt.GetWorldCorners(wc);
            return new Rect(wc[0].x, wc[0].y, wc[2].x - wc[0].x, wc[2].y - wc[0].y);
        }

        #endregion

    }

}