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

namespace WPMF {

    public enum VIEWPORT_QUALITY {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public partial class WorldMap2D : MonoBehaviour {

        #region Public properties

        static WorldMap2D _instance;

        /// <summary>
        /// Instance of the world map. Use this property to access World Map functionality.
        /// </summary>
        public static WorldMap2D instance {
            get {
                if (_instance == null) {
#if UNITY_EDITOR
					WorldMap2D[] candidates = Misc.FindObjectsOfType<WorldMap2D> ();
					for (int k = 0; k < candidates.Length; k++) {
						WorldMap2D c = candidates [k];
							if (UnityEditor.EditorUtility.IsPersistent (c.gameObject))
								continue;	// exclude prefabs
						_instance = c;
						break;
					}
#else
                    _instance = Misc.FindObjectOfType<WorldMap2D>();
#endif
                    if (_instance == null) {
                        Debug.LogWarning("'WorldMap2D' GameObject could not be found in the scene. Make sure it's created with this name before using any map functionality.");
                    }
                }
                return _instance;
            }
        }

        public static bool instanceExists {
            get {
                if (_instance == null) {
                    GameObject obj = GameObject.Find("WorldMap2D");
                    if (obj == null) {
                        return false;
                    }
                    _instance = obj.GetComponent<WorldMap2D>();
                    if (_instance == null)
                        return false;
                }
                return true;
            }
        }

        [SerializeField]
        Camera _mainCamera;

        public Camera mainCamera {
            get { return _mainCamera; }
            set {
                if (_mainCamera != value) {
                    _mainCamera = value;
                    isDirty = true;
                    SetupViewport();
                    CenterMap();
                }
            }
        }


        public ViewportMode renderViewportMode => viewportMode;

        [SerializeField]
        Transform _sun;

        public Transform sun {
            get { return _sun; }
            set {
                if (_sun != value) {
                    _sun = value;
                    isDirty = true;
                }
            }
        }

        /// <summary>
        /// Target gameobject to display de map (optional)
        /// </summary>
        [SerializeField]
        GameObject _renderViewport;

        public GameObject renderViewport {
            get {
                return _renderViewport;
            }
            set {
                if (value != _renderViewport) {
                    if (value == null)
                        _renderViewport = gameObject;
                    else
                        _renderViewport = value;
                    isDirty = true;
                    SetupViewport();
                    CenterMap();
                }
            }
        }

        [SerializeField]
        Rect _renderViewportScreenRect = new Rect(0, 0, 1, 1);

        public Rect renderViewportScreenRect {
            get { return _renderViewportScreenRect; }
            set {
                if (value != _renderViewportScreenRect) {
                    _renderViewportScreenRect = value;
                    isDirty = true;
                    SetupViewport();
                }
            }
        }

        [SerializeField]
        bool _renderViewportScreenOverlay;

        public bool renderViewPortScreenOverlay {
            get { return _renderViewportScreenOverlay; }
            set {
                if (value != _renderViewportScreenOverlay) {
                    _renderViewportScreenOverlay = value;
                    isDirty = true;
                    SetupViewport();
                }
            }
        }

        [SerializeField]
        FilterMode _renderViewportFilterMode = FilterMode.Trilinear;

        public FilterMode renderViewportFilterMode {
            get { return _renderViewportFilterMode; }
            set {
                if (_renderViewportFilterMode != value) {
                    _renderViewportFilterMode = value;
                    isDirty = true;
                    SetupViewport();
                }
            }
        }


        #endregion

        #region Public API area

        /// <summary>
        /// Enables Calculator component and returns a reference to its API.
        /// </summary>
        public WorldMap2D_Calculator calc { get { return GetComponent<WorldMap2D_Calculator>() ?? gameObject.AddComponent<WorldMap2D_Calculator>(); } }

        /// <summary>
        /// Enables Ticker component and returns a reference to its API.
        /// </summary>
        public WorldMap2D_Ticker ticker { get { return GetComponent<WorldMap2D_Ticker>() ?? gameObject.AddComponent<WorldMap2D_Ticker>(); } }

        /// <summary>
        /// Enables Decorator component and returns a reference to its API.
        /// </summary>
        public WorldMap2D_Decorator decorator { get { return GetComponent<WorldMap2D_Decorator>() ?? gameObject.AddComponent<WorldMap2D_Decorator>(); } }

        /// <summary>
        /// Enables Editor component and returns a reference to its API.
        /// </summary>
        public WorldMap2D_Editor editor { get { return GetComponent<WorldMap2D_Editor>() ?? gameObject.AddComponent<WorldMap2D_Editor>(); } }

        #endregion

    }

}