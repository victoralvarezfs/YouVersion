﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace WPM {
	/// <summary>
	/// Mount Point scaler (similar to City Scaler). Checks the mount point icons' size is always appropiate
	/// </summary>
	public class MountPointScaler : MonoBehaviour {

		const int MOUNTPOINT_SIZE_ON_SCREEN = 10;
		Vector3 lastCamPos, lastPos;
		float lastIconSize;
		float lastCustomSize;

		[NonSerialized]
		public WorldMapGlobe map;

		void Start () {
			ScaleMountPoints ();
		}
	
		// Update is called once per frame
		void Update () {
			if (map != null && lastPos == transform.position && lastCamPos == map.mainCamera.transform.position && lastIconSize == map.cityIconSize)
				return;
			ScaleMountPoints ();
		}

		public void ScaleMountPoints () {
			if (map == null)
				return;
			Camera cam = map.mainCamera;
			if (cam==null || cam.pixelWidth == 0)
				return;
			lastPos = transform.position;
			lastCamPos = cam.transform.position;
			lastIconSize = map.cityIconSize;
			float oldFV = cam.fieldOfView;
			if (!VRCheck.isVrRunning) {
				cam.fieldOfView = 60.0f;
			}
			Vector3 refPos = transform.position;
			if (map.earthInvertedMode)
				refPos += Vector3.forward; // otherwise, transform.position = 0 in inverted mode
			Vector3 a = cam.WorldToScreenPoint (refPos);
			Vector3 b = new Vector3 (a.x, a.y + MOUNTPOINT_SIZE_ON_SCREEN * map.cityIconSize, a.z);
			Vector3 aa = cam.ScreenToWorldPoint (a);
			Vector3 bb = cam.ScreenToWorldPoint (b);
			if (!VRCheck.isVrRunning) {
				cam.fieldOfView = oldFV;
			}
			float scale = (aa - bb).magnitude / map.transform.localScale.x;
			scale = Mathf.Clamp (scale, 0.00001f, 0.005f);
			Vector3 newScale = new Vector3 (scale, scale, 1.0f);

			foreach (Transform t in transform)
				t.localScale = newScale;
		}

		public void ScaleMountPoints (float customSize) {
			customSize = Mathf.Clamp (customSize, 0, 0.005f);
			if (customSize == lastCustomSize)
				return;
			lastCustomSize = customSize;
			Vector3 newScale = new Vector3 (customSize, customSize, 1);
			foreach (Transform t in transform)
				t.localScale = newScale;
		}
	}

}