// World Political Map - Globe Edition for Unity - Main Script
// Created by Ramiro Oliva (Kronnect)
// Don't modify this script - changes could be lost if you upgrade to a more recent version of WPM

//#define VR_EYE_RAY_CAST_SUPPORT  	 // Uncomment this line to support VREyeRayCast script - note that you must have already imported this script from Unity VR Samples
//#define VR_GOOGLE				  	 // Uncomment this line to support Google VR SDK (pointer and controller touch)
//#define VR_SAMSUNG_GEAR_CONTROLLER // Uncomment this line to support old Samsung Gear VR SDK (laser pointer)
//#define VR_OCULUS               // Uncomment this line to support Oculus VR or GearVR controller using latest OVRInput manager

//#define TRACE_CTL				   // Used by us to debug/trace some events
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using WPM.Poly2Tri;
using WPM.PolygonTools;
using TMPro;
using UnityEngine.EventSystems;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if VR_EYE_RAY_CAST_SUPPORT
using VRStandardAssets.Utils;
#endif
#if VR_GOOGLE
using GVR;
#endif

namespace WPM {
    [Serializable]
    [ExecuteInEditMode]
    public partial class WorldMapGlobe : MonoBehaviour {

        public const float MAP_PRECISION = 5000000f;
        const string WPM_OVERLAY_NAME_OLD = "WPMOverlay";
        const string WPM_OVERLAY_NAME = "WorldMapGlobe Overlay";
        const float MIN_FIELD_OF_VIEW = 10.0f;
        const float MAX_FIELD_OF_VIEW = 85.0f;
        const float MIN_ZOOM_DISTANCE = 0.05f;
        // 0.58f;
        const float EARTH_RADIUS_KM = 6371f;
        const int SMOOTH_STRAIGHTEN_ON_POLES = -1;
        const string SPHERE_OVERLAY_LAYER_NAME = "SphereOverlayLayer";
        const string OVERLAY_MARKER_LAYER_NAME = "OverlayMarkers";
        const string MAPPER_CAM = "MapperCam";
        const string SPHERE_BACK_FACES = "WorldMapGlobeBackFaces";
        const string SURFACE_GAMEOBJECT = "Surf";
        readonly char[] SPLIT_SEP_PIPE = { '|' };
        readonly char[] SPLIT_SEP_DOLLAR = { '$' };
        readonly char[] SPLIT_SEP_SEMICOLON = { ';' };
        readonly char[] SPLIT_SEP_ASTERISK = { '*' };

        enum OVERLAP_CLASS {
            OUTSIDE = -1,
            PARTLY_OVERLAP = 0,
            INSIDE = 1
        }

        public bool isDirty;
        // internal variable used to confirm changes in custom inspector - don't change its value


        #region Internal variables

        // resources
        Material outlineMatThinOpaque, outlineMatThickOpaque, outlineMatCurrent;
        Material sphereOverlayMatDefault;
        Material fontMaterial;

        // gameObjects
        GameObject _surfacesLayer;

        GameObject surfacesLayer {
            get {
                if (_surfacesLayer == null)
                    CreateSurfacesLayer();
                return _surfacesLayer;
            }
        }

        GameObject markersLayer, overlayMarkersLayer;

        // caché and gameObject lifetime control
        Dictionary<int, GameObject> surfaces;
        int countryProvincesDrawnIndex;
        Dictionary<Color, Material> markerMatOtherCache;
        Dictionary<Color, Material> markerMatLinesCache;
        List<TriangulationPoint> steinerPoints;
        List<Vector2> tmpPoints;
        readonly StringBuilder sb = new StringBuilder(100);

        // FlyTo functionality
        Quaternion flyToStartQuaternion, flyToEndQuaternion;
        Quaternion flyToCameraStartRotation, flyToCameraEndRotation;
        bool flyToActive, flyToComplete;
        float flyToStartTime, flyToDuration;
        Vector3 flyToCameraStartPosition, flyToCameraEndPosition, flyToCameraStartUpVector;
        bool flyToCameraDistanceEnabled;
        Vector3 flyToGlobeStartPosition, flyToEndDestination;
        float flyToCameraDistanceStart, flyToCameraDistanceEnd;
        float flyToBounceIntensity;
        NAVIGATION_MODE flyToMode;
        bool zoomToActive, zoomComplete;
        float zoomToStartTime, zoomToDuration, zoomToStartDistance, zoomToEndDistance;

        // UI interaction variables
        int mapUnityLayer;
        Vector3 mouseDragStart, dragDirection, mouseDragStartCursorLocation;
        bool mouseStartedDragging, hasDragged;
        float mouseStartedDraggingTime;
        float dragDampingStart;
        float wheelDampingStart, wheelAccel, zoomDistance;
        Vector3 lastRestyleEarthNormalsScaleCheck;
        bool mouseIsOverUIElement;
        int simulatedMouseButtonClick = -1, simulatedMouseButtonPressed = -1, simulatedMouseButtonRelease = -1;
        bool leftMouseButtonClick, rightMouseButtonClick, leftMouseButtonRelease, rightMouseButtonRelease;
        bool leftMouseButtonPressed, rightMouseButtonPressed;
        bool leftMouseButtonWasClicked, rightMouseButtonWasClicked;
        float mouseDownTime;
        float lastCameraDistanceSqr, lastCameraFoV;
        bool pinchZooming;
        float lastAtmosDistanceSqr;
        Vector3 lastAtmosGlobePosition;
        Vector3 lastCameraRotationDiff;
        Vector3 lastAtmosGlobeScale;
        Vector3 _cursorLastLocation;
        Vector3 sphereCurrentHitPos;
        bool _mouseIsOver, _mouseEnterGlobeThisFrame;
        Vector3 zoomDir;

#if VR_GOOGLE
        Transform GVR_Reticle;
        bool GVR_TouchStarted;
#endif

        bool gestureAborted;
#if VR_SAMSUNG_GEAR_CONTROLLER
        LineRenderer SVR_Laser;
#endif

        bool touchPadTouchStays = false;
        bool touchPadTouchStart = false;
#if VR_OCULUS
        OVRManager OVR_Manager;
#endif
        float lastTimeCheckVRPointers;
        float oldPinchRotationAngle = 999;
        float dragAngle;
        Vector3 dragAxis;

        // Overlay (Labels, tickers, ...)
        Font labelsFont;
        Material labelsShadowMaterial;
        Vector3 lastSunDirection;
        public bool requestMapperCamShot;
        public int currentDecoratorCount;
        Renderer backFacesRenderer;
        Material backFacesRendererMat;

        TMP_FontAsset labelsFontTMPro;
        Material labelsFontTMProMaterial;

#if VR_EYE_RAY_CAST_SUPPORT
								VREyeRaycaster _VREyeRayCaster;
								VREyeRaycaster VRCameraEyeRayCaster {
								get {
				if (_VREyeRayCaster==null) {
				_VREyeRayCaster = transform.GetComponent<VREyeRaycaster>();
			}
			return _VREyeRayCaster;
		}
		}
#endif

        #endregion

        #region System initialization

        public void Init () {
            // Load materials
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": init");
#endif

            // Setup references & layers
            mapUnityLayer = gameObject.layer;

            // Updates layer in children
            foreach (Transform t in transform) {
                t.gameObject.layer = mapUnityLayer;
            }

            ReloadFont();

            // Map materials
            frontiersMatThinOpaque = Instantiate(Resources.Load<Material>("Materials/Frontiers"));
            frontiersMatThinAlpha = Instantiate(Resources.Load<Material>("Materials/FrontiersAlpha"));
            frontiersMatThickOpaque = Instantiate(Resources.Load<Material>("Materials/FrontiersGeo"));
            frontiersMatThickAlpha = Instantiate(Resources.Load<Material>("Materials/FrontiersGeoAlpha"));
            inlandFrontiersMatOpaque = Instantiate(Resources.Load<Material>("Materials/InlandFrontiers"));
            inlandFrontiersMatAlpha = Instantiate(Resources.Load<Material>("Materials/InlandFrontiersAlpha"));
            hudMatCountry = Instantiate(Resources.Load<Material>("Materials/HudCountry"));
            hudMatProvince = Instantiate(Resources.Load<Material>("Materials/HudProvince"));
            hudMatProvince.renderQueue++;
            hudMatBlinker = Instantiate(Resources.Load<Material>("Materials/HudBlinker"));

            ReloadCityPrefabs();
            provincesMatOpaque = Instantiate(Resources.Load<Material>("Materials/Provinces"));
            provincesMatAlpha = Instantiate(Resources.Load<Material>("Materials/ProvincesAlpha"));
            outlineMatThinOpaque = Instantiate(Resources.Load<Material>("Materials/Outline"));
            outlineMatThinOpaque.name = "Outline";
            outlineMatThickOpaque = Instantiate(Resources.Load<Material>("Materials/OutlineThick"));
            outlineMatThickOpaque.name = "Outline";
            countryColoredMat = Instantiate(Resources.Load<Material>("Materials/CountryColorizedRegion"));
            countryColoredAlphaMat = Instantiate(Resources.Load<Material>("Materials/CountryColorizedTranspRegion"));
            countryTexturizedMat = Instantiate(Resources.Load<Material>("Materials/CountryTexturizedRegion"));
            provinceColoredMat = Instantiate(Resources.Load<Material>("Materials/ProvinceColorizedRegion"));
            provinceColoredAlphaMat = Instantiate(Resources.Load<Material>("Materials/ProvinceColorizedTranspRegion"));
            provinceTexturizedMat = Instantiate(Resources.Load<Material>("Materials/ProvinceTexturizedRegion"));
            cursorMat = Instantiate(Resources.Load<Material>("Materials/Cursor"));
            gridMatOverlay = Instantiate(Resources.Load<Material>("Materials/GridOverlay"));
            gridMatMasked = Instantiate(Resources.Load<Material>("Materials/GridMasked"));
            tropicMatOverlay = Instantiate(gridMatOverlay);
            tropicMatMasked = Instantiate(gridMatMasked);
            markerMatOther = Instantiate(Resources.Load<Material>("Materials/Marker"));
            markerMatLine = Instantiate(Resources.Load<Material>("Materials/MarkerLine"));
            mountPointSpot = Resources.Load<GameObject>("Prefabs/MountPointSpot");
            mountPointsMat = Instantiate(Resources.Load<Material>("Materials/Mount Points"));
            earthGlowMat = Instantiate(Resources.Load<Material>("Materials/EarthGlow"));
            earthGlowScatterMat = Instantiate(Resources.Load<Material>("Materials/EarthGlow2"));

            countryColoredMatCache = new Dictionary<Color, Material>();
            provinceColoredMatCache = new Dictionary<Color, Material>();
            markerMatOtherCache = new Dictionary<Color, Material>();
            markerMatLinesCache = new Dictionary<Color, Material>();

            // Destroy obsolete labels layer -> now replaced with overlay feature
            GameObject o = GameObject.Find("WPMLabels");
            if (o != null) {
                DestroyImmediate(o);
            }
            Transform tlabel = transform.Find("LabelsLayer");
            if (tlabel != null) {
                DestroyImmediate(tlabel.gameObject);
            }
            // End destroy obsolete.

            InitGridSystem();

            ReloadData();

            lastRestyleEarthNormalsScaleCheck = transform.lossyScale;

            if (gameObject.activeInHierarchy) {
                StartCoroutine(CheckGPS());
            }

        }

        void ReloadCityPrefabs () {
            if (_citySpot == null) {
                _citySpot = Resources.Load<GameObject>("Prefabs/CitySpot");
            }
            if (_citySpotCapitalRegion == null) {
                _citySpotCapitalRegion = Resources.Load<GameObject>("Prefabs/CityCapitalRegionSpot");
            }
            if (_citySpotCapitalCountry == null) {
                _citySpotCapitalCountry = Resources.Load<GameObject>("Prefabs/CityCapitalCountrySpot");
            }

            citiesNormalMat = GetCityMaterial(_citySpot, "Materials/Cities");
            citiesRegionCapitalMat = GetCityMaterial(_citySpotCapitalRegion, "Materials/CitiesCapitalRegion");
            citiesCountryCapitalMat = GetCityMaterial(_citySpotCapitalCountry, "Materials/CitiesCapitalCountry");
        }

        Material GetCityMaterial (GameObject go, string defaultMaterialName) {
            Renderer r = go.GetComponentInChildren<Renderer>();
            Material mat = null;
            if (r != null) {
                mat = r.sharedMaterial;
            }
            if (mat == null) {
                mat = Resources.Load(defaultMaterialName) as Material;
                if (mat == null) {
                    Debug.LogWarning("Cannot load city prefab or material");
                    return null;
                }
            }

            string matName = mat.name;
            mat = Instantiate(mat);
            mat.name = matName;
            return mat;
        }

        /// <summary>
        /// Reloads the data of frontiers and cities from datafiles and redraws the map.
        /// </summary>
        public void ReloadData () {

            // read baked data
            ReadCountriesGeoData();

            if (_showProvinces) {
                ReadProvincesGeoData();
            } else {
                _provinces = null;
            }
            if (_showCities) {
                ReadCitiesGeoData();
            } else {
                _cities = null;
            }
            ReloadMountPointsData();

            // Redraw frontiers and cities -- destroy layers if they already exists
            Redraw();
        }

        void GetPointFromPackedString (string s, out float x, out float y) {
            int d = 1;
            float v = 0;
            y = 0;
            for (int k = s.Length - 1; k >= 0; k--) {
                char ch = s[k];
                if (ch >= '0' && ch <= '9') {
                    v += (ch - '0') * d;
                    d *= 10;
                } else if (ch == '.') {
                    v = v / d;
                    d = 1;
                } else if (ch == '-') {
                    v = -v;
                } else if (ch == ',') {
                    y = v / MAP_PRECISION;
                    v = 0;
                    d = 1;
                }
            }
            x = v / MAP_PRECISION;
        }

        void GetPointFromPackedString (string s, int start, int length, out float x, out float y) {
            int d = 1;
            float v = 0;
            y = 0;
            for (int k = start + length - 1; k >= start; k--) {
                char ch = s[k];
                if (ch >= '0' && ch <= '9') {
                    v += (ch - '0') * d;
                    d *= 10;
                } else if (ch == '.') {
                    v = v / d;
                    d = 1;
                } else if (ch == '-') {
                    v = -v;
                } else if (ch == ',') {
                    y = v / MAP_PRECISION;
                    v = 0;
                    d = 1;
                }
            }
            x = v / MAP_PRECISION;
        }

        #endregion

        #region Game loop events

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainInit () {
            WorldMapGlobe[] maps = Misc.FindObjectsOfType<WorldMapGlobe>(true);
            foreach (WorldMapGlobe map in maps) {
                map.Dispose();
            }
        }

        void Dispose () {
            OnDestroy();
        }




        void OnEnable () {

            VRCheck.Init();

#if UNITY_EDITOR
            // skip double initialization when entering playmode
            if (!Application.isPlaying && UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif

#if UNITY_EDITOR
            PrefabInstanceStatus prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(gameObject);
            if (prefabInstanceStatus != PrefabInstanceStatus.NotAPrefab) {
                EditorApplication.delayCall += () => {
                    if (this == null || gameObject == null) return;
                    PrefabUtility.UnpackPrefabInstance(gameObject.transform.root.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    OnEnableDelayed();
                    RestyleEarth();
                };
                return;
            }
#endif
            radius = transform.lossyScale.y * 0.5f;
            OnEnableDelayed();
        }

        void OnEnableDelayed () {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": enable wpm");
#endif

#if VR_GOOGLE || VR_SAMSUNG_GEAR_CONTROLLER || VR_OCULUS
            _VREnabled = true;
#endif

            if (input == null) {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                input = new NewInputSystem();
#else
                input = new DefaultInputSystem();
#endif
            }
            input.Init();

            if ((int)_earthStyle == 20) {   // migration to new property
                _earthStyle = EARTH_STYLE.Alternate1;
                _showTiles = true;
            }

            if (_overlayLayerIndex == 0) {
                _overlayLayerIndex = gameObject.layer;
            }

            // Check backfaces
            Transform tBackFaces = transform.Find(SPHERE_BACK_FACES);
            if (tBackFaces == null) {
                GameObject backFaces = Instantiate(Resources.Load<GameObject>("Prefabs/WorldMapGlobeBackFaces"));
                backFaces.name = SPHERE_BACK_FACES;
                backFaces.hideFlags = HideFlags.DontSave;
                backFaces.transform.SetParent(transform, false);
                backFaces.transform.localPosition = Misc.Vector3zero;
                tBackFaces = backFaces.transform;

            }
            backFacesRenderer = tBackFaces.GetComponent<Renderer>();
            backFacesRendererMat = backFacesRenderer.sharedMaterial;

            if (_countries == null) {
                Init();
            }

            // Check material
            if (earthRenderer.sharedMaterial == null) {
                RestyleEarth();
            }

            UpdateMoon();
            UpdateSkybox();

            if (hudMatCountry != null && hudMatCountry.color != _fillColor) {
                hudMatCountry.color = _fillColor;
            }
            if (hudMatProvince != null && hudMatProvince.color != _provincesFillColor) {
                hudMatProvince.color = _provincesFillColor;
            }
            if (citiesNormalMat.color != _citiesColor) {
                citiesNormalMat.color = _citiesColor;
            }
            if (citiesRegionCapitalMat.color != _citiesRegionCapitalColor) {
                citiesRegionCapitalMat.color = _citiesRegionCapitalColor;
            }
            if (citiesCountryCapitalMat.color != _citiesCountryCapitalColor) {
                citiesCountryCapitalMat.color = _citiesCountryCapitalColor;
            }
            UpdateOutlineMatProperties();
            if (cursorMat.color != _cursorColor) {
                cursorMat.color = _cursorColor;
            }
            if (gridMatOverlay.color != _gridColor) {
                gridMatOverlay.color = _gridColor;
            }
            if (gridMatMasked.color != _gridColor) {
                gridMatMasked.color = _gridColor;
            }
            if (tropicMatOverlay.color != _tropicLinesColor) {
                tropicMatOverlay.color = _tropicLinesColor;
            }
            if (tropicMatMasked.color != _tropicLinesColor) {
                tropicMatMasked.color = _tropicLinesColor;
            }

            CheckCameraPivot();

            OrbitStart();

            Camera cam = mainCamera;
            if (cam != null) {
                if (_allowUserZoom) {
                    float minClipPlane = Mathf.Abs(transform.lossyScale.x / 100f);
                    if (cam.nearClipPlane > minClipPlane) {
                        cam.nearClipPlane = minClipPlane;
                    }
                }
                lastCameraDistanceSqr = (pivotTransform.position - transform.position).sqrMagnitude;
            }

            if (!enableOrbit && cam != null && ((_allowUserRotation && _navigationMode == NAVIGATION_MODE.CAMERA_ROTATES) || (_allowUserZoom && _zoomMode == ZOOM_MODE.CAMERA_MOVES))) {
                pivotTransform.LookAt(transform.position);
            }
        }

        void Start () {
            RegisterVRPointers();
        }

        void RegisterVRPointers () {
            if (Time.time - lastTimeCheckVRPointers < 1f)
                return;
            lastTimeCheckVRPointers = Time.time;

#if VR_GOOGLE
												GameObject obj = GameObject.Find ("GvrControllerPointer");
												if (obj != null) {
																Transform t = obj.transform.Find ("Laser");
																if (t != null) {
																				GVR_Reticle = t.Find ("Reticle");
																}
												}
#elif VR_SAMSUNG_GEAR_CONTROLLER
			if (SVR_Laser == null) {
				OVRGearVrController[] cc = FindObjectsOfType<OVRGearVrController> ();
				for (int k=0; k<cc.Length; k++) {
					{
						if (cc [k].m_model.activeSelf) {
							Transform t = cc [k].transform.Find ("Model/Laser");
						
							if (t != null) {
								SVR_Laser = t.gameObject.GetComponent<LineRenderer> ();            
							}
						}
					}
				}
			}
#elif VR_OCULUS
            if (OVR_Manager == null) {
                OVR_Manager = FindObjectOfType<OVRManager>();
            }
#endif
        }

        void OnDestroy () {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": destroy wpm");
#endif
            DestroyOverlay();
            DestroySurfaces();
            DestroyTiles();
            DestroyFogOfWarLayer();
            DestroyMaterialCaches();

            _cursorLastLocation = lastAtmosGlobeScale = lastCameraRotationDiff = lastAtmosGlobePosition = lastRestyleEarthNormalsScaleCheck = Misc.Vector3zero;
            mouseIsOverUIElement = false;
            lastAtmosDistanceSqr = lastCameraDistanceSqr = lastCameraFoV = 0;
            _countries = null;
        }

        void Reset () {
#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": reset");
#endif
            Redraw();
        }

        void Update () {

            radius = transform.lossyScale.x * 0.5f;

            CheckOverlay();

            Camera cam = mainCamera;
            if (cam == null || input == null)
                return;

            SyncTimeOfDay();

            PerformAutoRotation();

            CheckMouseOver();

            GetButtonState();

            CheckCursorVisibility();

            CheckPointerOverUI();

            // Handle interaction mode
            if ((mouseStartedDragging || !mouseIsOverUIElement) && Application.isPlaying) {
                if (_earthInvertedMode) {
                    CheckUserInteractionInvertedMode();
                } else {
                    CheckUserInteractionNormalMode();
                }
            }

            PerformOrbit(cam);

            // Has moved?
            Vector3 cameraRotationDiff = (cam.transform.eulerAngles - transform.eulerAngles);
            float cameraDistanceSqr = FastVector.SqrDistanceByValue(pivotTransform.position, transform.position);
            // Fades country labels and updates borders
            if (cameraRotationDiff != lastCameraRotationDiff || cameraDistanceSqr != lastCameraDistanceSqr || cam.fieldOfView != lastCameraFoV) {

                lastCameraDistanceSqr = cameraDistanceSqr;
                lastCameraRotationDiff = cameraRotationDiff;
                lastCameraFoV = cam.fieldOfView;
                shouldCheckTiles = true;
                resortLoadQueue = true;

                if (_showCountryNames && (_countryLabelsEnableAutomaticFade || _labelsFaceToCamera)) {
                    FadeCountryLabels();
                }

                // Check maximum screen area size for highlighted country
                if (_countryHighlightMaxScreenAreaSize < 1f && _countryRegionHighlighted != null && countryRegionHighlightedObj != null && countryRegionHighlightedObj.activeSelf) {
                    if (!CheckGlobeDistanceForHighlight(_countryRegionHighlighted, _countryLabelsAutoFadeMaxHeight)) {
                        countryRegionHighlightedObj.SetActive(false);
                    }
                }

                // Check maximum screen area size for highlighted country
                if (_provinceHighlightMaxScreenAreaSize < 1f && _provinceRegionHighlighted != null && provinceRegionHighlightedObj != null && provinceRegionHighlightedObj.activeSelf) {
                    if (!CheckGlobeDistanceForHighlight(_provinceRegionHighlighted, _provinceHighlightMaxScreenAreaSize)) {
                        provinceRegionHighlightedObj.SetActive(false);
                    }
                }
            }

            if (!mouseIsOverUIElement && Application.isPlaying) {
                CheckEventsOverMap();
            }

        }


        void CheckMouseOver () {
            // Check if it's really outside of sphere
            _mouseEnterGlobeThisFrame = false;
            if (GetGlobeIntersection(out sphereCurrentHitPos)) {
                if (!_mouseIsOver) {
                    _mouseEnterGlobeThisFrame = true;
                }
                _mouseIsOver = true;
            } else {
                if (_mouseIsOver) {
                    if (!leftMouseButtonPressed && !rightMouseButtonPressed) {
                        mouseStartedDragging = false;
                        if (_enableCountryHighlight) {
                            HideContinentHighlight();
                            HideCountryRegionHighlight();
                        }
                        HideHighlightedCell();
                    }
                }
                _mouseIsOver = false;
            }
        }

        void CheckEventsOverMap () {
            // Verify if mouse enter a country boundary - we only check if mouse is inside the sphere of world
            if (mouseIsOver || _VREnabled) {
#if !UNITY_WEBGL
                if (Application.isMobilePlatform || TouchScreenKeyboard.isSupported) {
                    if (_VREnabled || leftMouseButtonClick || rightMouseButtonClick) {
                        CheckMousePos();
                    } else if (leftMouseButtonPressed) {
                        UpdateCursorLocation();
                    }
                } else
#endif
                {
                    CheckMousePos();
                }

                // Remember the last element clicked & trigger events
                bool isClick = dragDampingStart == 0 && (leftMouseButtonClick || rightMouseButtonClick);
                bool isRelease = leftMouseButtonRelease || rightMouseButtonRelease;
                bool fullClick = dragDampingStart == 0 && isRelease && (Time.time - mouseDownTime < 0.5f || simulatedMouseButtonClick == 0);
                if (isClick || isRelease) {
                    int buttonIndex = leftMouseButtonClick || leftMouseButtonPressed || leftMouseButtonRelease ? 0 : 1;
                    _countryLastClicked = _countryHighlightedIndex;
                    _countryRegionLastClicked = _countryRegionHighlightedIndex;
                    if (_countryLastClicked >= 0) {
                        if (isClick && !fullClick && OnCountryPointerDown != null) {
                            OnCountryPointerDown(_countryLastClicked, _countryRegionLastClicked, buttonIndex);
                        } else if (isRelease) {
                            if (OnCountryPointerUp != null) {
                                OnCountryPointerUp(_countryLastClicked, _countryRegionLastClicked, buttonIndex);
                            }
                            if (fullClick && OnCountryClick != null) {
                                OnCountryClick(_countryLastClicked, _countryRegionLastClicked, buttonIndex);
                            }
                        }
                    }
                    _provinceLastClicked = _provinceHighlightedIndex;
                    _provinceRegionLastClicked = _provinceRegionHighlightedIndex;
                    if (_provinceLastClicked >= 0) {
                        if (isClick && !fullClick && OnProvincePointerDown != null) {
                            OnProvincePointerDown(_provinceLastClicked, _provinceRegionLastClicked, buttonIndex);
                        } else if (isRelease) {
                            if (OnProvincePointerUp != null) {
                                OnProvincePointerUp(_provinceLastClicked, _provinceRegionLastClicked, buttonIndex);
                            }
                            if (fullClick && OnProvinceClick != null) {
                                OnProvinceClick(_provinceLastClicked, _provinceRegionLastClicked, buttonIndex);
                            }
                        }
                    }
                    _cityLastClicked = _cityHighlightedIndex;
                    if (_cityLastClicked >= 0) {
                        if (isClick && !fullClick && OnCityPointerDown != null) {
                            OnCityPointerDown(_cityLastClicked, buttonIndex);
                        } else if (isRelease) {
                            if (OnCityPointerUp != null) {
                                OnCityPointerUp(_cityLastClicked, buttonIndex);
                            }
                            if (fullClick && OnCityClick != null) {
                                OnCityClick(_cityLastClicked, buttonIndex);
                            }
                        }
                    }
                    _continentLastClicked = _continentHighlighted;
                    if (!string.IsNullOrEmpty(_continentLastClicked)) {
                        if (isClick && !fullClick && OnContinentPointerDown != null) {
                            OnContinentPointerDown(_continentLastClicked, buttonIndex);
                        } else if (isRelease) {
                            if (OnContinentPointerUp != null) {
                                OnContinentPointerUp(_continentLastClicked, buttonIndex);
                            }
                            if (fullClick && OnContinentClick != null) {
                                OnContinentClick(_continentLastClicked, buttonIndex);
                            }
                        }
                    }
                    if (isClick && OnMouseDown != null) {
                        OnMouseDown(_cursorLocation, buttonIndex);
                    }
                    if (fullClick && OnClick != null && !CheckDragThreshold(mouseDragStart, input.mousePosition, 10)) {
                        OnClick(_cursorLocation, buttonIndex);
                    }
                }
                if (leftMouseButtonPressed && _cursorLastLocation != _cursorLocation && OnDrag != null) {
                    OnDrag(_cursorLocation);
                }
                _cursorLastLocation = _cursorLocation;
            }

            if (leftMouseButtonRelease && OnMouseRelease != null)
                OnMouseRelease(_cursorLocation, 0);
            if (rightMouseButtonRelease && OnMouseRelease != null)
                OnMouseRelease(_cursorLocation, 1);

            // Reset simulated click
            simulatedMouseButtonClick = -1;
            simulatedMouseButtonPressed = -1;
            simulatedMouseButtonRelease = -1;
        }

        readonly List<RaycastResult> raycastAllResults = new List<RaycastResult>();
        private bool RaycastWithMask () {
            PointerEventData eventDataCurrentPosition = new PointerEventData(EventSystem.current);
            eventDataCurrentPosition.position = input.mousePosition;
            EventSystem.current.RaycastAll(eventDataCurrentPosition, raycastAllResults);
            int hitCount = raycastAllResults.Count;
            for (int k = 0; k < hitCount; k++) {
                var hit = raycastAllResults[k];
                if ((_blockingMask & (1 << hit.gameObject.layer)) != 0) return true;
            }
            return false;
        }

        readonly HashSet<int> currentIgnoredFingerIDs = new HashSet<int>();
        void CheckPointerOverUI () {

            // Check whether the points is on an UI element, then cancels
            if (_respectOtherUI) {
#if VR_EYE_RAY_CAST_SUPPORT
				if (VRCameraEyeRayCaster!=null && VRCameraEyeRayCaster.CurrentInteractible != null) {
					if (!mouseIsOverUIElement) {
						mouseIsOverUIElement = true;
						HideCountryRegionHighlight();
					}
					CheckTilt();
					return;
				}
#endif
                if (EventSystem.current != null) {
                    if (_blockingMask > 0) {
                        mouseIsOverUIElement = RaycastWithMask();
                        return;
                    } else {
                        if (input.touchSupported && input.touchCount > 0) {
                            for (int i = 0; i < input.touchCount; i++) {
                                Touch currTouch = input.GetTouch(i);
                                if (currTouch.phase == TouchPhase.Began && EventSystem.current.IsPointerOverGameObject(currTouch.fingerId)) {
                                    mouseIsOverUIElement = true;
                                    currentIgnoredFingerIDs.Add(currTouch.fingerId);
                                } else {
                                    mouseIsOverUIElement = currentIgnoredFingerIDs.Contains(currTouch.fingerId);
                                    if (currTouch.phase == TouchPhase.Ended || currTouch.phase == TouchPhase.Canceled) {
                                        currentIgnoredFingerIDs.Remove(currTouch.fingerId);
                                    }
                                }
                            }
                            return;
                        } else if (EventSystem.current.IsPointerOverGameObject(-1)) {
                            mouseIsOverUIElement = true;
                            return;
                        }
                    }
                }
            }
            mouseIsOverUIElement = false;
        }

        void SyncTimeOfDay () {
            if (_sun == null) return;
            if (_syncTimeOfDay) {
                SetTimeOfDay(DateTime.Now);
            }
            _earthScenicLightDirection = -_sun.forward;
        }

        void GetButtonState () {

            // Check mouse buttons state

            // LEFT CLICK

            leftMouseButtonClick = simulatedMouseButtonClick == 0;
#if VR_OCULUS
                touchPadTouchStart = false;
                if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)) {
                    leftMouseButtonClick = true;
                    touchPadTouchStart = touchPadTouchStays = false;
                }
                if (OVRInput.GetDown(OVRInput.Touch.PrimaryTouchpad)) {
                    touchPadTouchStart = touchPadTouchStays = true;
                    leftMouseButtonPressed = false;
                }
            if (OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick) != Misc.Vector2zero) {
                touchPadTouchStays = true;
                hasDragged = true;
                mouseStartedDragging = true;
            }
#elif VR_GOOGLE
                                                                if (GvrController.TouchDown) {
                                                                                GVR_TouchStarted = true;
                                                                                leftMouseButtonClick = true;
                                                                                mouseDownTime = Time.time;
                                                                }
#else
            leftMouseButtonClick = leftMouseButtonClick || input.GetMouseButtonDown(0) || input.GetButtonDown("Fire1");
#endif
            // LEFT PRESSED
            leftMouseButtonPressed = leftMouseButtonClick || simulatedMouseButtonPressed == 0;

#if VR_OCULUS
                if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger)) {
                    leftMouseButtonPressed = true;
                }
#elif VR_GOOGLE
                if (GVR_TouchStarted) {
                                            leftMouseButtonPressed = true;
                }
#else
            leftMouseButtonPressed = leftMouseButtonPressed || input.GetMouseButton(0);
#endif

            // LEFT RELEASED
            leftMouseButtonRelease = simulatedMouseButtonRelease == 0;
#if VR_OCULUS
                if (OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger)) {
                    leftMouseButtonRelease = true;
                }
                if (OVRInput.GetUp(OVRInput.Touch.PrimaryTouchpad)) {
                    touchPadTouchStays = false;
                }
#elif VR_GOOGLE
                                                                if (GvrController.TouchUp) {
                                                                                GVR_TouchStarted = false;
                                                                                leftMouseButtonRelease = true;
                                                                }
#else
            leftMouseButtonRelease = leftMouseButtonRelease || simulatedMouseButtonClick == 0 || input.GetMouseButtonUp(0) || input.GetButtonUp("Fire1");
#endif

            if (leftMouseButtonClick) {
                mouseDownTime = Time.time;
            }

            rightMouseButtonClick = input.GetMouseButtonDown(1) || simulatedMouseButtonClick == 1;

#if VR_OCULUS
                if (OVRInput.GetDown(OVRInput.Button.PrimaryTouchpad)) {
                    rightMouseButtonClick = true;
                    touchPadTouchStays = touchPadTouchStart = false;
                }
#endif
            rightMouseButtonPressed = rightMouseButtonClick || input.GetMouseButton(1) || simulatedMouseButtonPressed == 1 || input.touchCount == 2;
            rightMouseButtonRelease = input.GetMouseButtonUp(1) || simulatedMouseButtonRelease == 1 || simulatedMouseButtonClick == 1;

            // ensure that click sequence was captured from start: 1) click event 2) pressed event (can last several frames) 3) release event.
            // we don't want to react to a release event without having captured the click event first
            if (leftMouseButtonClick) leftMouseButtonWasClicked = true;
            if (rightMouseButtonClick) rightMouseButtonWasClicked = true;
            if (!leftMouseButtonWasClicked) { leftMouseButtonPressed = leftMouseButtonRelease = false; }
            ;
            if (!rightMouseButtonWasClicked) { rightMouseButtonPressed = rightMouseButtonRelease = false; }
            ;
            if (leftMouseButtonRelease) { leftMouseButtonWasClicked = false; }
            if (rightMouseButtonRelease) { rightMouseButtonWasClicked = false; }
        }


        void PerformAutoRotation () {
            if (!Application.isPlaying) return;

            // Check if navigateTo... has been called and in this case rotate the globe until the country is centered
            if (flyToActive) {
                NavigateToDestination();
            } else {
                // subtle/slow continuous rotation
                if (!constraintPositionEnabled) {
                    if (_autoRotationSpeed != 0) {
                        transform.Rotate(Misc.Vector3up, -_autoRotationSpeed * Time.deltaTime * 60f);
                    }
                    if (_cameraAutoRotationSpeed != 0) {
                        mainCamera.transform.RotateAround(transform.position, transform.up, -_cameraAutoRotationSpeed * Time.deltaTime * 60f);
                    }
                }
            }
            if (zoomToActive) {
                ZoomToDestination();
            }
        }


        void LateUpdate () {

            // Check mapper cam
            if (requestMapperCamShot) {
                if (mapperCam == null || (overlayRT != null && !overlayRT.IsCreated())) {
                    DestroyOverlay();
                    Redraw();
                }
                if (mapperCam != null) {
                    mapperCam.Render();
                }
                requestMapperCamShot = false;
            }

            // Updates atmosphere if Sun light has changed direction
            if (lastAtmosDistanceSqr != lastCameraDistanceSqr || _earthScenicLightDirection != lastSunDirection || !Application.isPlaying || lastAtmosGlobePosition != transform.position || lastAtmosGlobeScale != transform.lossyScale) {
                lastSunDirection = _earthScenicLightDirection;
                lastAtmosDistanceSqr = lastCameraDistanceSqr;
                lastAtmosGlobePosition = transform.position;
                lastAtmosGlobeScale = transform.lossyScale;
                DrawAtmosphere();
            }
            if (_showTiles) {
                LateUpdateTiles();
            }

            if (_showHexagonalGrid) {
                UpdateHexagonalGrid();
            }

            if (!leftMouseButtonPressed && !rightMouseButtonPressed && !touchPadTouchStays) {
                mouseStartedDragging = false;
                hasDragged = false;
            }

            if (_frontiersThicknessMode == FRONTIERS_THICKNESS.Custom) {
                Shader.SetGlobalMatrix(ShaderParams.GlobalObject2WorldMatrix, transform.localToWorldMatrix.inverse);
            }
            if (_skyboxStyle == SKYBOX_STYLE.DualSkybox) {
                Material mat = RenderSettings.skybox;
                if (mat != null) {
                    // Counter camera rotation
                    Quaternion q = Quaternion.Inverse(Quaternion.LookRotation(mainCamera.transform.forward, mainCamera.transform.up));
                    // Apply rotation around camera x-axis
                    q = Quaternion.Euler(90 - pitch, 0, 0) * q;
                    // Apply rotation around camera y-axis
                    q = Quaternion.Euler(0, yaw, 0) * q;
                    Matrix4x4 m = Matrix4x4.Rotate(q);
                    mat.SetMatrix(ShaderParams.CameraRot, m);

                    // Set transition
                    float altitude = Vector3.Distance(transform.position, pivotTransform.position) - radius;
                    float range = _skyboxEnvironmentTransitionAltitudeMax - _skyboxEnvironmentTransitionAltitudeMin;
                    range = Mathf.Max(range, 1f);
                    float t = Mathf.Clamp01((altitude - _skyboxEnvironmentTransitionAltitudeMin) / range);
                    mat.SetFloat(ShaderParams.SpaceToGround, t);
                }
            }
        }

        #endregion

        #region Drawing stuff

        /// <summary>
        /// Clears and Repaints the Globe's features (frontiers, cities, provinces, grid, ...)
        /// </summary>
        public void Redraw (bool rebuildFrontiers = false) {
            if (!gameObject.activeInHierarchy)
                return;

            if (countries == null)
                OnEnable();

#if TRACE_CTL
			Debug.Log ("CTL " + DateTime.Now + ": Redraw");
#endif

            DestroyMapLabels();

            // Initialize surface cache
            bool forceBuildSurfaces = rebuildFrontiers || surfaces == null;
            if (forceBuildSurfaces) {
                InitSurfacesCache();
            }

            HideProvinces();

            RestyleEarth(); // Apply texture to Earth

            if (rebuildFrontiers) {
                OptimizeFrontiers();
            }

            DrawFrontiers();    // Redraw country frontiers

            DrawInlandFrontiers(); // Redraw inland frontiers

            DrawAllProvinceBorders(true); // Redraw province borders

            DrawCities();       // Redraw cities layer

            DrawMountPoints();  // Redraw mount points (only in Editor time)

            DrawCursor();       // Draw cursor lines

            DrawGrid();     // Draw longitude & latitude lines

            DrawAtmosphere();

            DrawMapLabels();

            DrawSurfaces(forceBuildSurfaces);

            DrawFogOfWar();

        }

        void InitSurfacesCache () {
            if (surfaces != null) {
                List<GameObject> cached = new List<GameObject>(surfaces.Values);
                int cachedCount = cached.Count;
                for (int k = 0; k < cachedCount; k++) {
                    if (cached[k] != null) {
                        DestroyImmediate(cached[k]);
                    }
                }
                surfaces.Clear();
            } else {
                surfaces = new Dictionary<int, GameObject>();
            }
            _surfacesCount = 0;
            DestroySurfacesLayer();
        }

        void CreateSurfacesLayer () {
            Transform t = transform.Find("Surfaces");
            if (t != null) {
                DestroyImmediate(t.gameObject);
                for (int k = 0; k < countries.Length; k++) {
                    int regionsCount = countries[k].regions.Count;
                    for (int r = 0; r < regionsCount; r++)
                        countries[k].regions[r].customMaterial = null;
                }
            }
            _surfacesLayer = new GameObject("Surfaces");
            _surfacesLayer.layer = gameObject.layer;
            _surfacesLayer.transform.SetParent(transform, false);
            _surfacesLayer.transform.localScale = _earthInvertedMode ? Misc.Vector3one * 0.995f : Misc.Vector3one;
        }

        void DestroySurfacesLayer () {
            if (_surfacesLayer != null) {
                DestroyImmediate(_surfacesLayer);
            }
        }

        /// <summary>
        /// Redraws country and provinces surfaces
        /// </summary>
        void DrawSurfaces (bool forceRebuild = false) {
            // Check country regions
            int countryCount = countries.Length;
            for (int k = 0; k < countryCount; k++) {
                Country country = countries[k];
                if (country.regions == null) continue;
                int regionCount = country.regions.Count;
                for (int r = 0; r < regionCount; r++) {
                    Region region = country.regions[r];
                    // Check if region was colored/textured and its surface was destroyed
                    if (region.customMaterial == null) continue;
                    if (region.surfaceGameObject != null && !region.surfaceGameObjectIsDirty && !forceRebuild) continue;
                    ToggleCountryRegionSurface(k, r, true, region.customColor, region.customTexture, region.customTextureScale, region.customTextureOffset, region.customTextureRotation, region.customOutline, region.customOutlineColor);
                }
            }

            // Check province regions
            if (_provinces != null) {
                int provinceCount = _provinces.Length;
                for (int k = 0; k < provinceCount; k++) {
                    Province province = provinces[k];
                    if (province.regions == null) continue;
                    for (int r = 0; r < province.regions.Count; r++) {
                        Region region = province.regions[r];
                        // Check if region was colored/textured and its surface was destroyed
                        if (region.customMaterial == null) continue;
                        if (region.surfaceGameObject != null && !region.surfaceGameObjectIsDirty && !forceRebuild) continue;
                        ToggleProvinceRegionSurface(k, r, true, region.customColor, region.customTexture, region.customTextureScale, region.customTextureOffset, region.customTextureRotation, false);
                    }
                }
            }

            UpdateSurfaceCount();
        }

        void DestroyMaterialCaches () {
            DestroyMaterialDict(markerMatOtherCache);
            DestroyMaterialDict(markerMatLinesCache);
        }

        void DestroyMaterialDict (Dictionary<Color, Material> dict) {
            if (dict == null) return;
            foreach (Material mat in dict.Values) {
                if (mat != null) DestroyImmediate(mat);
            }
            dict.Clear();
        }


        Material GetColoredMarkerOtherMaterial (Color color) {
            if (markerMatOtherCache.TryGetValue(color, out Material mat) && mat != null) {
                return mat;
            } else {
                Material customMat;
                customMat = Instantiate(markerMatOther);
                customMat.name = markerMatOther.name;
                markerMatOtherCache[color] = customMat;
                customMat.color = color;
                return customMat;
            }
        }

        Material GetColoredMarkerLineMaterial (Color color) {
            if (markerMatLinesCache.TryGetValue(color, out Material mat) && mat != null) {
                return mat;
            } else {
                Material customMat;
                customMat = Instantiate(markerMatLine);
                customMat.name = markerMatLine.name;
                markerMatLinesCache[color] = customMat;
                customMat.color = color;
                return customMat;
            }
        }

        void ApplyMaterialToSurface (GameObject obj, Material sharedMaterial) {
            if (obj != null) {
                Renderer[] rr = obj.GetComponentsInChildren<Renderer>(true);
                for (int k = 0; k < rr.Length; k++) {
                    Renderer r = rr[k];
                    if (r != null && r.name.Equals(SURFACE_GAMEOBJECT)) {
                        r.sharedMaterial = sharedMaterial;
                    }
                }
            }
        }

        void ToggleGlobalVisibility (bool visible) {
            Renderer[] rr = transform.GetComponentsInChildren<MeshRenderer>();
            for (int k = 0; k < rr.Length; k++) {
                rr[k].enabled = visible;
            }
            if (overlayLayer != null) {
                Transform billboard = overlayLayer.transform.Find("Billboard");
                if (billboard != null) {
                    Renderer billboardRenderer = billboard.GetComponent<Renderer>();
                    if (billboardRenderer != null)
                        billboardRenderer.enabled = false;
                }
            }
            enabled = visible;
        }

        #endregion

        #region Internal functions

        float ApplyDragThreshold (float value, float threshold) {
            if (threshold > 0) {
                if (value < 0) {
                    value += threshold;
                    if (value > 0)
                        value = 0;
                } else {
                    value -= threshold;
                    if (value < 0)
                        value = 0;
                }
            }
            return value;
        }

        /// <summary>
        /// Returns true if drag is detected based on displacement threshold
        /// </summary>
        bool CheckDragThreshold (Vector3 v1, Vector3 v2, float threshold) {
            if (threshold <= 0f)
                return true;

            float dx = v1.x - v2.x;
            if (dx <= -threshold || dx >= threshold) {
                return true;
            }
            float dy = v1.y - v2.y;
            if (dy <= -threshold || dy >= threshold) {
                return true;
            }
            return false;
        }

        float LerpCameraDistance (float t) {
            float distance = Mathf.Lerp(flyToCameraDistanceStart, flyToCameraDistanceEnd, t);
            if (flyToBounceIntensity > 0) {
                distance *= 1f + Mathf.Sin(t * Mathf.PI) * flyToBounceIntensity;
            }
            return distance;
        }


        Quaternion GetCameraStraightLookRotation () {
            Vector3 camVec = transform.position - pivotTransform.position;
            if (Mathf.Abs(Vector3.Dot(transform.up, camVec.normalized)) > 0.96f) {   // avoid going crazy around poles
                return pivotTransform.rotation;
            }

            Quaternion old = pivotTransform.rotation;
            pivotTransform.LookAt(transform.position);
            Vector3 camUp = Vector3.ProjectOnPlane(transform.up, camVec);
            float angle = SignedAngleBetween(pivotTransform.up, camUp, camVec);
            pivotTransform.Rotate(camVec, angle, Space.World);
            Quaternion q = pivotTransform.rotation;
            pivotTransform.rotation = old;
            return q;
        }

        float GetFrustumDistance (Camera cam) {
            if (cam == null) return 1;
            // Gets the max distance from the map considering the perspective projection
            float fv = cam.fieldOfView;
            float radAngle = fv * Mathf.Deg2Rad * 0.5f;
            float cang = Mathf.PI * 0.5f - radAngle;
            float sphereY = radius * Mathf.Sin(cang);
            float sphereX = radius * Mathf.Cos(cang);
            float frustumDistance = sphereY / Mathf.Tan(radAngle) + sphereX;
            return frustumDistance;
        }

        /// <summary>
        /// Returns optimum distance between camera and a region maxWidth
        /// </summary>
        float GetFrustumZoomLevel (float width, float height) {
            Camera cam = mainCamera;
            if (cam == null)
                return 1;
            if (cam.orthographic)
                return 1;

            float fv = cam.fieldOfView;
            float radAngle = fv * Mathf.Deg2Rad * 0.5f;
            float aspect = cam.aspect;
            float frustumDistanceH = height * 0.5f / Mathf.Tan(radAngle);
            float frustumDistanceW = (width / aspect) * 0.5f / Mathf.Tan(radAngle);
            float frustumDistance = radius + Mathf.Max(frustumDistanceH, frustumDistanceW);

            float cang = Mathf.PI * 0.5f - radAngle;
            float sphereY = radius * Mathf.Sin(cang);
            float sphereX = radius * Mathf.Cos(cang);
            float frustumDistanceSphere = sphereY / Mathf.Tan(radAngle) + sphereX;
            float minRadius = GetCameraMinDistance(cam);
            float zoomLevel = (frustumDistance - minRadius) / (frustumDistanceSphere - minRadius);

            return zoomLevel;

        }

        public virtual bool IsPointerOnScreenEdges () {
            float edgeLeft = Screen.width * _dragOnScreenEdgesMarginPercentage;
            float edgeRight = Screen.width * (1f - _dragOnScreenEdgesMarginPercentage);
            float edgeBottom = Screen.height * _dragOnScreenEdgesMarginPercentage;
            float edgeTop = Screen.height * (1f - _dragOnScreenEdgesMarginPercentage);
            Vector3 mousePos = input.mousePosition;
            if (mousePos.x < 0 || mousePos.x > Screen.width || mousePos.y < 0 || mousePos.y > Screen.height) return false;
            return (mousePos.x < edgeLeft || mousePos.x > edgeRight || mousePos.y < edgeBottom || mousePos.y > edgeTop);
        }

        protected virtual void CheckUserInteractionNormalMode () {

            Camera cam = mainCamera;

            // cancel current gesture if time exceeded max gesture time
            if (mouseStartedDragging && _dragMaxDuration > 0 && (leftMouseButtonPressed || rightMouseButtonPressed) && Time.time > mouseStartedDraggingTime + dragMaxDuration) {
                gestureAborted = true;
            }

            // if mouse/finger is over map, implement drag and rotation of the world
            bool canOrbit = (rightMouseButtonPressed && _rightButtonDragBehaviour == DRAG_BEHAVIOUR.CameraOrbit && !leftMouseButtonClick);
            // If touch released while was zooming, reset state
            if (pinchZooming && input.touchCount == 0) {
                mouseStartedDragging = false;
                dragDampingStart = 0;
                pinchZooming = false;
            }

            if (!pinchZooming && (_mouseIsOver || hasDragged || rightMouseButtonPressed)) {
                // Use left mouse button and drag to rotate the world
                if (_allowUserRotation) {
                    bool capturePosition = leftMouseButtonClick || touchPadTouchStart || rightMouseButtonClick;
                    if (capturePosition) {
#if VR_GOOGLE
						mouseDragStart = GvrController.TouchPos;
#elif VR_OCULUS
                        if (_dragConstantSpeed) {
                            mouseDragStart = input.mousePosition;
                        } else {
                            mouseDragStart = OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad);
                        }

#else
                        mouseDragStart = input.mousePosition;
                        UpdateCursorLocation(); // _cursorLocation has not been set yet so we call CheckMousePos before any interaction
#endif

                        mouseDragStartCursorLocation = _cursorLocation;
                        mouseStartedDragging = _rightButtonDragBehaviour != DRAG_BEHAVIOUR.Drag || (_rightButtonDragBehaviour == DRAG_BEHAVIOUR.Drag && rightMouseButtonClick);
                        mouseStartedDraggingTime = Time.time;
                        hasDragged = false;
                        dragDampingStart = 0;
                        gestureAborted = false;
                    } else if (!lockPan && mouseStartedDragging && (leftMouseButtonPressed || (_rightButtonDragBehaviour == DRAG_BEHAVIOUR.Drag && rightMouseButtonPressed) || touchPadTouchStays) && input.touchCount < 2) {
                        if (_dragConstantSpeed) {
                            dragAngle = 0;
                            if (_mouseIsOver) {
                                if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.X_AXIS_ONLY) {
                                    mouseDragStartCursorLocation.y = 0;
                                    _cursorLocation.y = 0;
                                } else if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.Y_AXIS_ONLY) {
                                    mouseDragStartCursorLocation.x = 0;
                                    _cursorLocation.x = 0;
                                }
                                if (CheckDragThreshold(mouseDragStart, input.mousePosition, _mouseDragThreshold)) {
                                    if (_mouseEnterGlobeThisFrame) {
                                        mouseDragStartCursorLocation = _cursorLocation;
                                    }
                                    if (input.mousePosition != mouseDragStart) {
                                        dragAngle = FastVector.AngleBetweenNormalizedVectors(mouseDragStartCursorLocation, _cursorLocation);
                                        if (dragAngle != 0) {
                                            hasDragged = true;
                                            if (_navigationMode == NAVIGATION_MODE.EARTH_ROTATES) {
                                                dragAxis = Vector3.Cross(mouseDragStartCursorLocation, _cursorLocation);
                                                transform.Rotate(dragAxis, dragAngle);
                                            } else {
                                                dragAxis = Vector3.Cross(transform.TransformVector(mouseDragStartCursorLocation), transform.TransformVector(_cursorLocation));
                                                RotateAround(pivotTransform, transform.position, dragAxis, -dragAngle);
                                            }
                                            mouseDragStart = input.mousePosition;
                                        }
                                    }
                                }
                            }
                        } else if (!gestureAborted) {
                            float distFactor = Mathf.Min((GetCameraDistance() - radius) / radius, 1f);
#if VR_GOOGLE
																												dragDirection = (mouseDragStart - (Vector3)GvrController.TouchPos);
																												dragDirection.y *= -1.0f;
																												dragDirection *= distFactor * _mouseDragSensitivity * Time.deltaTime * 60f;
#elif VR_OCULUS
                            dragDirection = (mouseDragStart - (Vector3)OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad));
                            if (dragDirection == Misc.Vector3zero) {
                                dragDirection = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
                            }
                            if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.X_AXIS_ONLY) {
                                dragDirection.y = 0;
                            } else if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.Y_AXIS_ONLY) {
                                dragDirection.x = 0;
                            }
                            dragDirection.x = ApplyDragThreshold(dragDirection.x, _mouseDragThreshold);
                            dragDirection.y = ApplyDragThreshold(dragDirection.y, _mouseDragThreshold);
                            dragDirection *= distFactor * _mouseDragSensitivity * Time.deltaTime * 60f;

#else
                            dragDirection = mouseDragStart - input.mousePosition;
                            if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.X_AXIS_ONLY) {
                                dragDirection.y = 0;
                            } else {
                                dragDirection.y = ApplyDragThreshold(dragDirection.y, _mouseDragThreshold);
                            }
                            if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.Y_AXIS_ONLY) {
                                dragDirection.x = 0;
                            } else {
                                dragDirection.x = ApplyDragThreshold(dragDirection.x, _mouseDragThreshold);
                            }
                            dragDirection *= 0.01f * distFactor * dragSensibility * Time.deltaTime * 60f;
#endif
                            if (dragDirection.x != 0 || dragDirection.y != 0) {
                                hasDragged = true;
                                if (_navigationMode == NAVIGATION_MODE.EARTH_ROTATES) {
                                    transform.Rotate(cam.transform.up, dragDirection.x, Space.World);
                                    Vector3 axisY = Vector3.Cross(transform.position - cam.transform.position, cam.transform.up);
                                    transform.Rotate(axisY, dragDirection.y, Space.World);
                                } else {
                                    if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.X_AXIS_ONLY) {
                                        pivotTransform.RotateAround(transform.position, cam.transform.up, -dragDirection.x);
                                    } else {
                                        RotateAround(pivotTransform, transform.position, cam.transform.up, -dragDirection.x);
                                        RotateAround(pivotTransform, transform.position, cam.transform.right, dragDirection.y);
                                    }
                                }
                                dragDampingStart = Time.time;
                            }
                        }
                        StopAnyNavigation();
                    } else if (mouseStartedDragging && canOrbit) {
                        Vector3 orbitDirection = mouseDragStart - input.mousePosition;

                        orbitDirection.y = ApplyDragThreshold(orbitDirection.y, _mouseDragThreshold);
                        orbitDirection.x = ApplyDragThreshold(orbitDirection.x, _mouseDragThreshold);

                        if (orbitDirection.x != 0 || orbitDirection.y != 0) {
                            hasDragged = true;
                            float direction = _orbitInvertDragDirection ? -1 : 1;
                            if (!lockYaw) {
                                yaw += direction * orbitDirection.x * dragSensibility * 0.01f * Time.deltaTime * 60f;
                            }
                            if (!lockPitch) {
                                pitch += direction * orbitDirection.y * dragSensibility * 0.01f * Time.deltaTime * 60f;
                            }
                        }
                        StopAnyNavigation();
                    }

                    // Use right mouse button and drag to spin the world around z-axis
                    if (!flyToActive) {
                        if (input.touchCount < 2) {
                            oldPinchRotationAngle = 999;
                            if (rightMouseButtonPressed) {
                                if (_allowUserRotation && _rightButtonDragBehaviour == DRAG_BEHAVIOUR.Rotate && Time.time - mouseStartedDraggingTime > 0.5f) {
                                    float rotAngle = _rightClickRotatingClockwise || input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift) ? -2f : 2f;
                                    RotateCameraAroundAxis(rotAngle);
                                    hasDragged = true;
                                }
                            } else if (rightMouseButtonRelease && _centerOnRightClick && !hasDragged) {
                                FlyToLocation(_cursorLocation);
                            }
                        }

                    }
                }
            }

            // auto drag on screen edges
            if (_dragOnScreenEdges && !leftMouseButtonPressed && !flyToActive) {
                PerformDragOnScreenEdges();
            }

            // click and thrown (only in constant drag speed)
            if (dragAngle > 0.001f && (!_mouseIsOver || !leftMouseButtonPressed)) {
                if (mouseStartedDragging) {
                    mouseStartedDragging = false;
                    dragDampingStart = Time.time;
                }
                PerformClickAndThrow();
            }

            // Check rotation keys
            if (_allowUserKeys && _allowUserRotation) {
                CheckRotationKeys(cam);
            }

            // Perform drag damping
            if (dragDampingStart > 0 && (gestureAborted || (!leftMouseButtonPressed && !rightMouseButtonPressed))) {
                PerformDragDamping(cam);
            }

            // Check angle constraint
            if (constraintPositionEnabled) {
                CheckAngleConstraint();
            }

            // Check latitude constraint
            if (constraintLatitudeEnabled) {
                CheckLatitudeConstraint();
            }


            // Use mouse wheel to zoom in and out
            if (_allowUserZoom) {
                float impulse = 0;
                // Support for zoom on pinch on mobile
                if (pinchZooming || input.touchCount == 2) {
                    if (!leftMouseButtonClick) {
                        impulse = CheckTwoFingerZoomGestures(cam);
                    }
                } else {
                    // mouse wheel support
                    if (_mouseIsOver || wheelAccel != 0) {
                        float wheel = input.GetMouseScrollWheel();
#if VR_OCULUS
                        if (wheel == 0) {
                            if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger)) {
                                wheel = -OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick).y;
                            }
                        }
#endif
                        impulse = wheel * (_invertZoomDirection ? -1 : 1);
                    }
                }

                if (impulse != 0) {
                    StopAnyNavigation();
                    wheelDampingStart = Time.time;
                    if (wheelAccel * impulse < 0) {
                        wheelAccel = 0; // change direction
                    } else {
                        wheelAccel += impulse;
                        if (_zoomAtMousePosition) {
                            Ray ray = cam.ScreenPointToRay(input.mousePosition);
                            zoomDir = ray.direction;
                        } else {
                            zoomDir = pivotTransform.forward;
                        }
                    }
                }
            }

            zoomDistance = 0;
            if (wheelDampingStart > 0) {
                float t = (Time.time - wheelDampingStart) / (_zoomDamping + 0.0001f);
                if (t < 0) {
                    t = 0;
                    wheelDampingStart = 0;
                } else if (t > 1f) {
                    t = 1f;
                }
                if (t < 1f) {
                    t = 1f - t;
                    zoomDistance = t * Mathf.Clamp(wheelAccel, -0.1f, 0.1f);
                    float distFactor = Mathf.Min((Vector3.Distance(pivotTransform.position, transform.position) - radius) / radius, 1f);
                    zoomDistance *= distFactor;
                    if (_zoomConstantSpeed) {
                        wheelAccel = 0;
                    }
                } else {
                    wheelAccel = 0;
                    wheelDampingStart = 0;
                }
            }

            // Performs zoom in / out
            if (zoomDistance != 0) {
                PerformZoomInOut(zoomDistance);
            }

            // Ensure camera is within min/max range
            CheckCamMinMaxDistance(cam);

            // Ensure camera looks straight (up or aligned with horizon depending on tilt)
            KeepCameraStraight();

            // Adjust grid visibility
            AdjustsHexagonalGridMaterial();
        }

        protected virtual void CheckUserInteractionInvertedMode () {
            Camera cam = mainCamera;

            // if mouse/finger is over map, implement drag and rotation of the world
            if (mouseIsOver) {
                // Use left mouse button and drag to rotate the world
                if (_allowUserRotation) {
                    if (leftMouseButtonClick) {
#if VR_GOOGLE
						mouseDragStart = GvrController.TouchPos;
#elif VR_OCULUS
                        mouseDragStart = OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad);
#else
                        mouseDragStart = input.mousePosition;
                        UpdateCursorLocation(); // _cursorLocation has not been set yet so we call CheckMousePos before any interaction
#endif
                        mouseDragStartCursorLocation = _cursorLocation;
                        mouseStartedDragging = true;
                        hasDragged = false;
                    } else if (mouseStartedDragging && leftMouseButtonPressed && input.touchCount < 2) {
                        if (_dragConstantSpeed) {
                            dragAngle = 0;
                            if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.X_AXIS_ONLY) {
                                mouseDragStartCursorLocation.y = 0;
                                _cursorLocation.y = 0;
                            } else if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.Y_AXIS_ONLY) {
                                mouseDragStartCursorLocation.x = 0;
                                _cursorLocation.x = 0;
                            }
                            if (CheckDragThreshold(mouseDragStart, input.mousePosition, _mouseDragThreshold)) {
                                dragAngle = FastVector.AngleBetweenNormalizedVectors(mouseDragStartCursorLocation, _cursorLocation);
                                if (dragAngle != 0) {
                                    hasDragged = true;
                                    if (_navigationMode == NAVIGATION_MODE.EARTH_ROTATES) {
                                        Vector3 v1 = transform.TransformPoint(mouseDragStartCursorLocation) - transform.position;
                                        Vector3 v2 = sphereCurrentHitPos - transform.position;
                                        dragAxis = Vector3.Cross(v1, v2);
                                        transform.Rotate(dragAxis, dragAngle, Space.World);
                                    } else {
                                        Vector3 v1 = transform.TransformPoint(mouseDragStartCursorLocation) - transform.position;
                                        Vector3 v2 = sphereCurrentHitPos - transform.position;
                                        dragAxis = Vector3.Cross(v1, v2);
                                        cam.transform.Rotate(dragAxis, -dragAngle);
                                    }
                                    mouseDragStart = input.mousePosition;
                                }
                            }
                        } else {
                            Vector3 referencePos = transform.position + cam.transform.forward * lastRestyleEarthNormalsScaleCheck.z * 0.5f;
                            float distFactor = Vector3.Distance(cam.transform.position, referencePos);
#if VR_GOOGLE
							dragDirection = (mouseDragStart - (Vector3)GvrController.TouchPos);
							dragDirection.y *= -1.0f;
																												dragDirection *= distFactor * _mouseDragSensitivity * Time.deltaTime * 60f;
#elif VR_OCULUS
                            dragDirection = (mouseDragStart - (Vector3)OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad));
                            if (dragDirection == Misc.Vector3zero) {
                                dragDirection = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
                            }
                            dragDirection.y *= -1.0f;
                            dragDirection *= distFactor * _mouseDragSensitivity * Time.deltaTime * 60f;
#else
                            dragDirection = (input.mousePosition - mouseDragStart);
                            dragDirection.x = ApplyDragThreshold(dragDirection.x, _mouseDragThreshold);
                            dragDirection.y = ApplyDragThreshold(dragDirection.y, _mouseDragThreshold);
                            dragDirection *= 0.015f * distFactor * dragSensibility * Time.deltaTime * 60f;
#endif
                            if (_rotationAxisAllowed == WPM.ROTATION_AXIS_ALLOWED.X_AXIS_ONLY)
                                dragDirection.y = 0;
                            else if (_rotationAxisAllowed == ROTATION_AXIS_ALLOWED.Y_AXIS_ONLY)
                                dragDirection.x = 0;


                            if (dragDirection.x != 0 && dragDirection.y != 0) {
                                dragDirection *= Time.deltaTime * 60f;
                                hasDragged = true;
                                if (_navigationMode == NAVIGATION_MODE.EARTH_ROTATES) {
                                    transform.Rotate(Misc.Vector3up, dragDirection.x, Space.World);
                                    Vector3 axisY = Vector3.Cross(referencePos - cam.transform.position, Misc.Vector3up);
                                    transform.Rotate(axisY, dragDirection.y, Space.World);
                                } else {
                                    dragDirection.x *= -1f;
                                    cam.transform.Rotate(dragDirection.y, dragDirection.x, 0, Space.Self);
                                }
                                dragDampingStart = Time.time;
                            }
                        }
                        StopAnyNavigation();
                    } else {
                        if (mouseStartedDragging) {
                            mouseStartedDragging = false;
                            hasDragged = false;
                        }
                    }

                    // Use right mouse button and drag to spin the world around z-axis
                    if (rightMouseButtonPressed && input.touchCount < 2 && !flyToActive) {
                        if (_showProvinces && _provinceHighlightedIndex >= 0 && _centerOnRightClick && rightMouseButtonClick) {
                            FlyToProvince(_provinceHighlightedIndex, 0.8f);
                        } else if (_countryHighlightedIndex >= 0 && rightMouseButtonClick && _centerOnRightClick) {
                            FlyToCountry(_countryHighlightedIndex, 0.8f);
                        } else {
                            Vector3 axis = (transform.position - cam.transform.position).normalized;
                            float rotAngle = _rightClickRotatingClockwise || input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift) ? -2f : 2f;
                            transform.Rotate(axis, rotAngle * Time.deltaTime * 60f, Space.World);
                        }
                    }
                }
            }

            // Check special keys
            if (_allowUserKeys && _allowUserRotation) {
                bool pressed = false;
                dragDirection = Misc.Vector3zero;
                if (input.GetKey(KeyCode.W)) {
                    dragDirection += Misc.Vector3down;
                    pressed = true;
                }
                if (input.GetKey(KeyCode.S)) {
                    dragDirection += Misc.Vector3up;
                    pressed = true;
                }
                if (input.GetKey(KeyCode.A)) {
                    dragDirection += Misc.Vector3right;
                    pressed = true;
                }
                if (input.GetKey(KeyCode.D)) {
                    dragDirection += Misc.Vector3left;
                    pressed = true;
                }
                if (pressed) {
                    Vector3 referencePos = transform.position + cam.transform.forward * lastRestyleEarthNormalsScaleCheck.z * 0.5f;
                    dragDirection *= Vector3.Distance(cam.transform.position, referencePos) * dragSensibility;
                    transform.Rotate(Misc.Vector3up, dragDirection.x, Space.World);
                    Vector3 axisY = Vector3.Cross(referencePos - cam.transform.position, Misc.Vector3up);
                    transform.Rotate(axisY, dragDirection.y, Space.World);
                    dragDampingStart = Time.time;
                }
            }

            if (dragDampingStart > 0) {
                float t = 1f - (Time.time - dragDampingStart) / _dragDampingDuration;
                if (t >= 0 && t <= 1f) {
                    if (_navigationMode == NAVIGATION_MODE.EARTH_ROTATES) {
                        transform.Rotate(Misc.Vector3up, dragDirection.x * t, Space.World);
                        Vector3 axisY = Vector3.Cross(transform.position - cam.transform.position, Misc.Vector3up);
                        transform.Rotate(axisY, dragDirection.y * t, Space.World);
                    } else {
                        cam.transform.Rotate(dragDirection.y * t, dragDirection.x * t, 0, Space.Self);
                    }
                } else {
                    dragDampingStart = 0;
                }
            }

            // Check contraint
            if (constraintPositionEnabled && mouseStartedDragging) {
                // Check constraint around position
                if (mouseIsOver) {
                    Vector3 camPos = cam.transform.position;
                    Vector3 contraintWPos = transform.TransformPoint(constraintPosition) - camPos;
                    Vector3 hitPos;
                    GetGlobeIntersectionCameraDirection(cam, out hitPos);
                    Vector3 hitPoint = hitPos - camPos;
                    Vector3 axis = Vector3.Cross(contraintWPos, hitPoint);
                    float angleDiff = SignedAngleBetween(contraintWPos, hitPoint, axis);
                    if (Mathf.Abs(angleDiff) > constraintPositionAngle + 0.0001f) {
                        if (angleDiff > 0) {
                            angleDiff = constraintPositionAngle - angleDiff;
                        } else {
                            angleDiff = angleDiff - constraintPositionAngle;
                        }
                        if (_navigationMode == NAVIGATION_MODE.CAMERA_ROTATES) {
                            Vector3 prevUp = cam.transform.up;
                            cam.transform.Rotate(axis, angleDiff, Space.World);
                            cam.transform.LookAt(camPos + cam.transform.forward, prevUp); // keep straight
                        } else {
                            axis.z = 0;
                            transform.Rotate(axis, -angleDiff, Space.World);
                        }
                        dragDampingStart = 0;

                        UpdateCursorLocation();
                        mouseDragStartCursorLocation = _cursorLocation;

                    }
                }
            }

            // Use mouse wheel to zoom in and out
            if (_allowUserZoom) {
                float impulse = 0;

                if (mouseIsOver || wheelAccel != 0) {
                    float wheel = input.GetMouseScrollWheel();
                    impulse = wheel * (_invertZoomDirection ? -1 : 1);
                }

                // Support for pinch on mobile
                if (input.touchSupported && input.touchCount == 2) {
                    // Store both touches.
                    Touch touchZero = input.GetTouch(0);
                    Touch touchOne = input.GetTouch(1);

                    // Find the position in the previous frame of each touch.
                    Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                    Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                    // Find the magnitude of the vector (the distance) between the touches in each frame.
                    float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                    float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                    // Find the difference in the distances between each frame.
                    float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                    // Pass the delta to the wheel accel
                    impulse = deltaMagnitudeDiff;
                }

                if (impulse != 0) {
                    wheelAccel += impulse;
                }
            }

            if (wheelAccel != 0) {
                wheelAccel = Mathf.Clamp(wheelAccel, -0.1f, 0.1f);
                if (wheelAccel >= 0.01f || wheelAccel <= -0.01f) {
                    cam.fieldOfView = Mathf.Clamp(cam.fieldOfView + (90.0f * cam.fieldOfView / MAX_FIELD_OF_VIEW) * wheelAccel * zoomSpeed * Time.deltaTime * 60f, MIN_FIELD_OF_VIEW, MAX_FIELD_OF_VIEW);
                    if (_zoomConstantSpeed) {
                        wheelAccel = 0;
                    } else {
                        wheelAccel *= _zoomDamping;
                    }
                } else {
                    wheelAccel = 0;
                }
            }

            if (_keepStraight && !flyToActive) {
                StraightenGlobe(SMOOTH_STRAIGHTEN_ON_POLES, true);
            }
        }

        void UpdateSurfaceCount () {
            if (_surfacesLayer != null)
                _surfacesCount = (_surfacesLayer.GetComponentsInChildren<Transform>().Length - 1) / 2;
            else
                _surfacesCount = 0;
        }


        #endregion

        #region Highlighting

        public int layerMask { get { return 1 << mapUnityLayer; } }

        bool GetRay (out Ray ray) {

            if (OnRaycast != null) {
                ray = OnRaycast();
                return true;
            }

            Camera cam = mainCamera;

#if VR_GOOGLE
																if (GVR_Reticle != null && GVR_Reticle.gameObject.activeInHierarchy) {
																				Vector3 screenPoint = cam.WorldToScreenPoint (GVR_Reticle.position);
																				ray = cam.ScreenPointToRay (screenPoint);
																} else {
					RegisterVRPointers();
																				ray = new Ray (cam.transform.position, GvrController.Orientation * Vector3.forward);
																}
#elif VR_SAMSUNG_GEAR_CONTROLLER
				if (SVR_Laser != null && SVR_Laser.gameObject.activeInHierarchy) {
							ray = new Ray(SVR_Laser.transform.position, SVR_Laser.transform.forward);
				} else {
					RegisterVRPointers();
					ray = new Ray (cam.transform.position, cam.transform.forward);
				}
#elif VR_OCULUS
            if (OVR_Manager != null && OVR_Manager.gameObject.activeInHierarchy) {
                Vector3 controllerPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
                Vector3 controllerDir = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch) * Vector3.forward;
                ray = new Ray(controllerPos, controllerDir);
            } else {
                ray = new Ray(cam.transform.position, cam.transform.forward);
            }
#else
            if (_VREnabled) {
                ray = new Ray(cam.transform.position, cam.transform.forward);
            } else {
                Vector3 mousePos;
                if (input.touchCount == 2) {
                    mousePos = (input.touches[0].position + input.touches[1].position) * 0.5f;
                } else {
                    mousePos = input.mousePosition;
                    if (mainCamera != null) {
                        Rect rect = _mainCamera.pixelRect;
                        if (Display.RelativeMouseAt(mousePos).z != mainCamera.targetDisplay || mousePos.x < rect.xMin || mousePos.x > rect.xMax || mousePos.y < rect.yMin || mousePos.y > rect.yMax) {
                            ray = new Ray();
                            return false;
                        }
                    }
                }
                ray = cam.ScreenPointToRay(mousePos);
            }
#endif
            return true;
        }


        public bool GetGlobeIntersection (Ray ray, out Vector3 hitPos) {

            hitPos = Misc.Vector3zero;

            Vector3 m = ray.origin - transform.position;
            float b = Vector3.Dot(m, ray.direction);
            float c = Vector3.Dot(m, m) - radius * radius;

            // Exit if r's origin outside s (c > 0) and r pointing away from s (b > 0) 
            if (c > 0f && b > 0f) return false;
            float discr = b * b - c;

            // A negative discriminant corresponds to ray missing sphere 
            if (discr < 0f) return false;

            // Ray now found to intersect sphere, compute smallest t value of intersection
            float t = -b - Mathf.Sqrt(discr);
            hitPos = ray.origin + ray.direction * t;
            return true;
        }

        public bool GetGlobeIntersection (out Vector3 hitPos) {

            Ray ray;
            if (!GetRay(out ray)) {
                hitPos = Misc.Vector3zero;
                return false;
            }
            if (_earthInvertedMode) {
                ray.origin += ray.direction * transform.lossyScale.z * 1.5f;
                ray.direction = -ray.direction;
            }

            return GetGlobeIntersection(ray, out hitPos);
        }


        public bool GetGlobeIntersectionCameraDirection (Camera cam, out Vector3 hitPos) {

            Transform t = cam.transform;
            Ray ray = new Ray(t.position, t.forward);
            if (_earthInvertedMode) {
                ray.origin += ray.direction * transform.lossyScale.z * 1.5f;
                ray.direction = -ray.direction;
            }
            return GetGlobeIntersection(ray, out hitPos);

        }


        bool UpdateCursorLocation () {

            // Keep cursor while rotating/orbiting
            if (rightMouseButtonPressed) return false;

            if (mouseIsOver) {

                if (GetGlobeIntersection(out sphereCurrentHitPos)) {
                    // Cursor follow
                    if (_cursorFollowMouse) {
                        cursorLocation = transform.InverseTransformPoint(sphereCurrentHitPos);
                    } else {
                        _cursorLocation = transform.InverseTransformPoint(sphereCurrentHitPos);
                    }
                    return true;
                }
            }
            return false;
        }


        void CheckMousePos () {

            if (UpdateCursorLocation()) {

                if (leftMouseButtonPressed && mouseStartedDragging && !_allowHighlightWhileDragging) return;

                // verify if hitPos is inside any country polygon
                if (GetCountryUnderMouse(cursorLocation, out int c, out int cr)) {
                    string continent = _countries[c].continent;
                    if (!string.Equals(continent, _continentHighlighted)) {
                        if (OnContinentEnter != null) {
                            OnContinentEnter(continent);
                        }
                    }
                    bool ignoreCountryByEvent = false;
                    if (c != _countryHighlightedIndex || (c == _countryHighlightedIndex && cr != _countryRegionHighlightedIndex)) {
                        if (OnCountryBeforeEnter != null) {
                            OnCountryBeforeEnter(c, cr, ref ignoreCountryByEvent);
                        }
                        HideCountryRegionHighlight();

                        if (!ignoreCountryByEvent) {
                            // Raise enter event
                            if (OnCountryEnter != null) {
                                OnCountryEnter(c, cr);
                            }
                            HighlightCountryRegion(c, cr, false, _showOutline, _outlineColor);
                        }
                    }
                    if (!ignoreCountryByEvent) {
                        // if show provinces is enabled, then we draw provinces borders
                        if (_countries[c].allowShowProvinces) {
                            if (_showProvinces) {
                                mDrawProvinces(_countryHighlightedIndex, false, false); // draw provinces borders if not drawn
                            }
                            if (provincesObj != null) {
                                // and now, we check if the mouse if inside a province, so highlight it
                                int p, pr;
                                if (GetProvinceUnderMouse(c, cursorLocation, out p, out pr)) {
                                    if (p != _provinceHighlightedIndex || (p == _provinceHighlightedIndex && pr != _provinceRegionHighlightedIndex)) {
                                        bool ignoreProvinceByEvent = false;
                                        if (OnProvinceBeforeEnter != null) {
                                            OnProvinceBeforeEnter(p, pr, ref ignoreProvinceByEvent);
                                        }
                                        HideProvinceRegionHighlight();

                                        if (!ignoreProvinceByEvent) {
                                            // Raise enter event
                                            if (OnProvinceEnter != null) {
                                                OnProvinceEnter(p, pr);
                                            }
                                            HighlightProvinceRegion(p, pr, false);
                                        }
                                    }
                                } else {
                                    HideProvinceRegionHighlight();
                                }
                            }
                        }
                        // if show cities is enabled, then check if mouse is over any city
                        if (_showCities) {
                            if (GetCityUnderMouse(c, cursorLocation, out int ci)) {
                                if (ci != _cityHighlightedIndex) {
                                    HideCityHighlight();

                                    // Raise enter event
                                    if (OnCityEnter != null)
                                        OnCityEnter(ci);
                                }
                                HighlightCity(ci);
                            } else if (_cityHighlightedIndex >= 0) {
                                HideCityHighlight();
                            }
                        }
                        return;
                    }
                }
            }

            // Raise exit event
            HideCountryRegionHighlight();
            HideContinentHighlight();
        }

        #endregion

        #region Geometric functions

        float SignedAngleBetween (Vector3 a, Vector3 b, Vector3 n) {
            // angle in [0,180]
            float angle = FastVector.Angle(a, b);
            float sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(a, b)));

            // angle in [-179,180]
            float signed_angle = angle * sign;

            return signed_angle;
        }

        Quaternion GetQuaternion (Vector3 point) {
            Camera cam = mainCamera;
            Quaternion oldRotation = transform.localRotation;
            Quaternion q;
            // center destination
            if (_earthInvertedMode) {
                cam.transform.LookAt(point);
                Vector3 angles = cam.transform.localRotation.eulerAngles;
                cam.transform.localRotation = Quaternion.Euler(new Vector3(angles.x, -angles.y, angles.z));
                q = Quaternion.Inverse(cam.transform.localRotation);
                cam.transform.localRotation = Misc.QuaternionZero;
            } else {
                Quaternion oldCamRot = pivotTransform.rotation;
                pivotTransform.rotation = flyToCameraEndRotation;
                Vector3 v1 = point;
                Vector3 v2 = pivotTransform.position - transform.position;
                float angle = FastVector.Angle(v1, v2);
                Vector3 axis = Vector3.Cross(v1, v2);
                transform.localRotation = Quaternion.AngleAxis(angle, axis);
                // straighten view
                Vector3 v3 = Vector3.ProjectOnPlane(transform.up, v2);
                Vector3 projCamUp = Vector3.ProjectOnPlane(pivotTransform.up, v2);
                float angle2 = SignedAngleBetween(projCamUp, v3, v2);
                transform.Rotate(v2, -angle2, Space.World);
                q = transform.localRotation;
                pivotTransform.rotation = oldCamRot;
            }
            transform.localRotation = oldRotation;
            return q;
        }

        /// <summary>
        /// Better than Transform.RotateAround
        /// </summary>
        void RotateAround (Transform transform, Vector3 center, Vector3 axis, float angle) {
            Vector3 pos = transform.position;
            Quaternion rot = Quaternion.AngleAxis(angle, axis); // get the desired rotation
            Vector3 dir = pos - center;                         // find current direction relative to center
            dir = rot * dir;                                    // rotate the direction
            transform.position = center + dir;                  // define new position
                                                                // rotate object to keep looking at the center:
            transform.rotation = rot * transform.rotation;
        }

        /// <summary>
        /// Internal usage. Checks quality of polygon points. Useful before using polygon clipping operations.
        /// Return true if there're changes.
        /// </summary>
        public bool RegionSanitize (Region region) {
            if (region == null || region.latlon == null) return false;
            bool changes = false;
            if (tmpPoints == null) {
                tmpPoints = new List<Vector2>(region.latlon);
            } else {
                tmpPoints.Clear();
            }
            // removes points which are too near from others
            Vector2[] latlon = region.latlon;
            for (int k = 0; k < latlon.Length; k++) {
                bool validPoint = true;
                for (int j = k + 1; j < latlon.Length; j++) {
                    float distance = (latlon[k].x - latlon[j].x) * (latlon[k].x - latlon[j].x) + (latlon[k].y - latlon[j].y) * (latlon[k].y - latlon[j].y);
                    if (distance < 0.00000000001f) {
                        validPoint = false;
                        changes = true;
                        break;
                    }
                }
                if (validPoint) {
                    tmpPoints.Add(latlon[k]);
                }
            }
            // remove crossing segments
            if (PolygonSanitizer.RemoveCrossingSegments(tmpPoints)) {
                changes = true;
            }
            if (changes) {
                region.latlon = tmpPoints.ToArray();
            }
            region.sanitized = true;
            return changes;
        }

        /// <summary>
        /// Checks for the sanitized flag in regions list and invoke RegionSanitize on pending regions
        /// </summary>
        /// <param name="regions">Regions.</param>
        /// <param name="forceSanitize">If set to <c>true</c> it will perform the check regardless of the internal sanitized flag.</param>
        public void RegionSanitize (List<Region> regions, bool forceSanitize) {
            if (regions == null) return;
            int regionCount = regions.Count;
            for (int k = 0; k < regionCount; k++) {
                Region region = regions[k];
                if (region.latlon == null) continue;
                if (!region.sanitized || forceSanitize) {
                    RegionSanitize(region);
                }
                if (region.latlon.Length < 3) { // remove invalid regions (<3 points)
                    regions.RemoveAt(k);
                    k--;
                    regionCount--;
                }
            }
        }


        /// <summary>
        /// Makes a region collapse with the neigbhours frontiers - needed when merging two adjacent regions.
        /// neighbourRegion is not modified. Region points are the ones that can be modified to match neighbour border.
        /// </summary>
        /// <returns><c>true</c>, if region was changed. <c>false</c> otherwise.</returns>
        bool RegionMagnet (Region region, Region neighbourRegion) {

            const float tolerance = 1e-6f;
            int pointCount = region.latlon.Length;
            bool[] usedPoints = new bool[pointCount];
            int otherPointCount = neighbourRegion.latlon.Length;
            bool[] usedOtherPoints = new bool[otherPointCount];
            bool changes = false;

            for (int i = 0; i < pointCount; i++) { // maximum iterations = pointCount ; also avoid any potential (rare) infinite loop (good practice)
                float minDist = float.MaxValue;
                int selPoint = -1;
                int selOtherPoint = -1;
                // Search nearest pair of points
                for (int p = 0; p < pointCount; p++) {
                    if (usedPoints[p])
                        continue;
                    Vector2 point0 = region.latlon[p];
                    for (int o = 0; o < otherPointCount; o++) {
                        if (usedOtherPoints[o])
                            continue;
                        Vector2 point1 = neighbourRegion.latlon[o];
                        float dx = point0.x - point1.x;
                        if (dx < 0)
                            dx = -dx;
                        if (dx < tolerance) {
                            float dy = point0.y - point1.y;
                            if (dy < 0)
                                dy = -dy;
                            if (dy < tolerance) {
                                float dist = dx < dy ? dx : dy;
                                if (dist <= 0) {
                                    // same point, ignore them now and in next iterations
                                    usedPoints[p] = true;
                                    usedOtherPoints[o] = true;
                                    selPoint = -1;
                                    break;
                                } else if (dist < minDist) {
                                    minDist = dist;
                                    selPoint = p;
                                    selOtherPoint = o;
                                }
                            }
                        }
                    }
                }
                if (selPoint >= 0) {
                    region.latlon[selPoint] = neighbourRegion.latlon[selOtherPoint];
                    region.sanitized = false;
                    usedPoints[selPoint] = true;
                    usedOtherPoints[selOtherPoint] = true;
                    changes = true;
                } else
                    break; // exit loop, no more pairs
            }
            if (changes) {
                region.UpdateSpherePointsFromLatLon();
            }
            return changes;
        }

        #endregion

    }

}