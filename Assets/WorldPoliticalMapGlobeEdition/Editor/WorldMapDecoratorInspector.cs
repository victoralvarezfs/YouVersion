using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

namespace WPM {
	[CustomEditor (typeof(WorldMapDecorator))]
	public class WorldMapDecoratorInspector : Editor {
	
		WorldMapDecorator _decorator;
		string[] groupNames, countryNames;
		CountryDecorator decorator;
		CountryDecoratorGroupInfo decoratorGroup;
		int lastCountryCount;
		Vector3 oldCameraPos;
		bool zoomState;

		WorldMapGlobe _map { get { return _decorator.map; } }

		void OnEnable () {
			_decorator = (WorldMapDecorator)target;
			groupNames = new string[WorldMapDecorator.NUM_GROUPS];
			ReloadGroupNames ();
			ReloadCountryNames ();
		}

		void OnDisable () {
			if (zoomState)
				ToggleZoomState ();
		}

		public override void OnInspectorGUI () {
			if (_decorator == null)
				return;

			bool requestChanges = false;

			EditorGUILayout.Separator ();
			EditorGUILayout.BeginVertical ();

			EditorGUILayout.BeginHorizontal ();
			int oldGroup = _decorator.GUIGroupIndex;
			_decorator.GUIGroupIndex = EditorGUILayout.Popup ("Group", _decorator.GUIGroupIndex, groupNames);
			if (_decorator.GUIGroupIndex != oldGroup || decoratorGroup == null) {
				decoratorGroup = _decorator.GetDecoratorGroup (_decorator.GUIGroupIndex, true);
			}

			if (GUILayout.Button ("Clear", GUILayout.Width (60))) {
				_decorator.ClearDecoratorGroup (_decorator.GUIGroupIndex);
				ReloadGroupNames ();
				ReloadCountryNames ();
			}

			EditorGUILayout.EndHorizontal ();
		
			decoratorGroup.active = EditorGUILayout.Toggle ("Enabled", decoratorGroup.active);

			EditorGUILayout.Separator ();

			// country selector
			if (lastCountryCount != _map.countries.Length) {
				ReloadCountryNames ();
			}
			int selection = EditorGUILayout.Popup ("Country", _decorator.GUICountryIndex, countryNames);
			if (selection != _decorator.GUICountryIndex) {
				_decorator.GUICountryName = "";
				_decorator.GUICountryIndex = selection;
				FlyToCountry ();
			}

			bool prevc = _decorator.groupByContinent;
			EditorGUI.indentLevel++;
			_decorator.groupByContinent = EditorGUILayout.Toggle ("Grouped", _decorator.groupByContinent);
			if (_decorator.groupByContinent != prevc) {
				ReloadCountryNames ();
			}
			EditorGUI.indentLevel--;

			// type of decoration
			if (_decorator.GUICountryName.Length > 0) {
				EditorGUILayout.BeginHorizontal ();
				GUILayout.Label ("", GUILayout.Width (EditorGUIUtility.labelWidth));
				if (GUILayout.Button ("Toggle Zoom")) {
					ToggleZoomState ();
				}
				if (GUILayout.Button ("Fly To")) {
					FlyToCountry ();
				}
				EditorGUILayout.EndHorizontal ();

				CountryDecorator existingDecorator = _decorator.GetCountryDecorator (_decorator.GUIGroupIndex, _decorator.GUICountryName);
				if (existingDecorator != null) {
					decorator = existingDecorator;
				} else if (decorator == null || !decorator.countryName.Equals (_decorator.GUICountryName)) {
					decorator = new CountryDecorator (_decorator.GUICountryName);
				}

				bool prevHidden = decorator.hidden;
				decorator.hidden = EditorGUILayout.Toggle ("Hidden", decorator.hidden);
				if (prevHidden != decorator.hidden)
					requestChanges = true;
				
				if (!decorator.hidden) {

					bool prevLabelVisible = decorator.labelVisible;
					decorator.labelVisible = EditorGUILayout.Toggle ("Label Visible", decorator.labelVisible);
					if (prevLabelVisible != decorator.labelVisible)
						requestChanges = true;

					if (decorator.labelVisible) {
						EditorGUI.indentLevel++;
						string prevLabel = decorator.customLabel;
						decorator.customLabel = EditorGUILayout.TextField ("Text", decorator.customLabel);
						if (!prevLabel.Equals (decorator.customLabel))
							requestChanges = true;

						if (_map.countryLabelsTextEngine == TEXT_ENGINE.TextMeshStandard) {
							Font prevFont = decorator.labelFontOverride;
							decorator.labelFontOverride = (Font)EditorGUILayout.ObjectField("Font", decorator.labelFontOverride, typeof(Font), false);
							if (decorator.labelFontOverride != prevFont)
								requestChanges = true;
						} else {
							TMP_FontAsset prevFontTMP = decorator.labelFontTMProOverride;
							decorator.labelFontTMProOverride = (TMP_FontAsset)EditorGUILayout.ObjectField("Font (TextMesh Pro)", decorator.labelFontTMProOverride, typeof(TMP_FontAsset), false);
							if (decorator.labelFontTMProOverride != prevFontTMP)
								requestChanges = true;

							Material prevFontTMPMaterial = decorator.labelFontTMProMaterialOverride;
							decorator.labelFontTMProMaterialOverride = (Material)EditorGUILayout.ObjectField("Font Material (TextMesh Pro)", decorator.labelFontTMProMaterialOverride, typeof(TMP_FontAsset), false);
							if (decorator.labelFontTMProMaterialOverride != prevFontTMPMaterial)
								requestChanges = true;
						}

						decorator.labelOverridesSize = EditorGUILayout.Toggle ("Custom Size", decorator.labelOverridesSize);
						if (decorator.labelOverridesSize) {
							EditorGUI.indentLevel++;
							float prevSize = decorator.labelSize;
							decorator.labelSize = EditorGUILayout.FloatField ("Size", decorator.labelSize);
							if (prevSize != decorator.labelSize) {
								requestChanges = true;
							}
							EditorGUI.indentLevel--;
						}

						decorator.labelOverridesColor = EditorGUILayout.Toggle ("Custom Color", decorator.labelOverridesColor);
						if (decorator.labelOverridesColor) {
							EditorGUI.indentLevel++;
							Color prevColor = decorator.labelColor;
							decorator.labelColor = WPMEditorStyles.HDRColorPicker("Color", decorator.labelColor);
							if (prevColor != decorator.labelColor)
								requestChanges = true;
							EditorGUI.indentLevel--;
						}

						Vector2 prevLabelOffset = decorator.labelOffset;
						decorator.labelOffset = EditorGUILayout.Vector2Field ("Offset", decorator.labelOffset);
						if (prevLabelOffset != decorator.labelOffset)
							requestChanges = true;
				
						float prevLabelRotation = decorator.labelRotation;
						decorator.labelRotation = EditorGUILayout.Slider ("Rotation", decorator.labelRotation, 0, 359);
						if (prevLabelRotation != decorator.labelRotation)
							requestChanges = true;
						EditorGUI.indentLevel--;
					}

					bool prevColorized = decorator.isColorized;
					decorator.isColorized = EditorGUILayout.Toggle ("Colorized", decorator.isColorized);
					if (decorator.isColorized != prevColorized) {
						requestChanges = true;
					}
					if (decorator.isColorized) {
						EditorGUI.indentLevel++;
						Color prevColor = decorator.fillColor;
						decorator.fillColor = WPMEditorStyles.HDRColorPicker("Fill Color", decorator.fillColor);
						if (prevColor != decorator.fillColor)
							requestChanges = true;

						Texture2D prevTexture = decorator.texture;
						decorator.texture = (Texture2D)EditorGUILayout.ObjectField ("Texture", decorator.texture, typeof(Texture2D), false);
						if (decorator.texture != prevTexture)
							requestChanges = true;

						if (decorator.texture != null) {
							EditorGUI.indentLevel++;
							Vector2 prevVector = decorator.textureScale;
							decorator.textureScale = EditorGUILayout.Vector2Field ("Scale", decorator.textureScale);
							if (prevVector != decorator.textureScale)
								requestChanges = true;

							prevVector = decorator.textureOffset;
							decorator.textureOffset = EditorGUILayout.Vector2Field ("Offset", decorator.textureOffset);
							if (prevVector != decorator.textureOffset)
								requestChanges = true;

							float prevFloat = decorator.textureRotation;
							decorator.textureRotation = EditorGUILayout.Slider ("Rotation", decorator.textureRotation, 0, 360);
							if (prevFloat != decorator.textureRotation) {
								requestChanges = true;
							}
							EditorGUI.indentLevel--;
						}
						EditorGUI.indentLevel--;
					}

				}
				EditorGUILayout.BeginHorizontal ();
				if (decorator.isNew) {
					if (GUILayout.Button ("Assign")) {
						_decorator.SetCountryDecorator (_decorator.GUIGroupIndex, _decorator.GUICountryName, decorator);
						ReloadGroupNames ();
						ReloadCountryNames ();
					}
				} else if (GUILayout.Button ("Remove")) {
					decorator = null;
					_decorator.RemoveCountryDecorator (_decorator.GUIGroupIndex, _decorator.GUICountryName);
					ReloadGroupNames ();
					ReloadCountryNames ();
				}
				EditorGUILayout.EndHorizontal ();

				if (!decoratorGroup.active) {
					DrawWarningLabel ("Enable the decoration group to activate changes");
				}
			}


			EditorGUILayout.EndVertical ();

			if (requestChanges) {
				_decorator.ForceUpdateDecorators ();
				SceneView.RepaintAll ();
				EditorUtility.SetDirty (_map);
				if (!Application.isPlaying) {
					UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene ());
				}
			}

		}

					

		void ReloadGroupNames () {
			for (int k = 0; k < groupNames.Length; k++) {
				int dc = _decorator.GetCountryDecoratorCount (k);
				if (dc > 0) {
					groupNames [k] = k.ToString () + " (" + dc + " decorators)";
				} else {
					groupNames [k] = k.ToString ();
				}
			}
		}

		void ReloadCountryNames () {
			if (_map == null || _map.countries == null)
				lastCountryCount = -1;
			else
				lastCountryCount = _map.countries.Length;
			_decorator.GUICountryIndex = -1;
			List<string> all = new List<string> ();
			all.AddRange (_decorator.GetDecoratedCountries (_decorator.GUIGroupIndex, true));
			// recover GUI country index selection
			if (_decorator.GUICountryName.Length > 0) {
				for (int k = 0; k < all.Count; k++) {
					if (all [k].StartsWith (_decorator.GUICountryName)) {
						_decorator.GUICountryIndex = k;
						break; 
					}
				}
			}
			if (all.Count > 0)
				all.Add ("---");
			all.AddRange (_map.GetCountryNames (_decorator.groupByContinent));
			countryNames = all.ToArray ();
		}

		void DrawWarningLabel (string s) {
			GUIStyle warningLabelStyle = new GUIStyle (GUI.skin.label);
			warningLabelStyle.normal.textColor = new Color (0.31f, 0.38f, 0.56f);
			GUILayout.Label (s, warningLabelStyle);
		}

		void ToggleZoomState () {
			zoomState = !zoomState;
			Transform camTransform = _map.pivotTransform;
			if (zoomState) {
				oldCameraPos = camTransform.position;
				camTransform.position = _map.transform.position + (camTransform.position - _map.transform.position) * _map.transform.localScale.z * 0.6f;
			} else {
				camTransform.position = oldCameraPos;
			}
		}

		void FlyToCountry () {
			string[] s = countryNames [_decorator.GUICountryIndex].Split (new char[] {
				'(',
				')'
			}, System.StringSplitOptions.RemoveEmptyEntries);
			if (s.Length >= 2) {
				_decorator.GUICountryName = s [0].Trim ();
				int countryIndex = int.Parse (s [1]);
				if (countryIndex >= 0) {
					if (Application.isPlaying) {
						_map.FlyToCountry (countryIndex, 2.0f);
						_map.BlinkCountry (countryIndex, Color.black, Color.green, 2.2f, 0.2f);
					} else {
						_map.FlyToCountry (countryIndex, 0);
					}
				}
			}
		}
	}

}