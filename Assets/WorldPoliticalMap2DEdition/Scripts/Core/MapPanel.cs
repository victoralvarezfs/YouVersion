using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


namespace WPMF {

    public class MapPanel : RawImage, IPointerEnterHandler, IPointerExitHandler {

        public WorldMap2D map;

#if UNITY_EDITOR
        [MenuItem("GameObject/UI/Map Panel")]
        static void CreateMapPanelCommandMenu(MenuCommand menuCommand) {
            GameObject parent = menuCommand.context as GameObject;
            if (parent == null) {
                parent = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = parent.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Create an event systems if needed
                if (EventSystem.current == null) {
                    new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                }
            } else if (parent.GetComponent<RectTransform>() == null) {
                EditorUtility.DisplayDialog("Requires Canvas UI", "Map Panel must be added to canvas or UI element.", "Ok");
                return;
            }

            GameObject panelGO = new GameObject("Map Panel");
            panelGO.AddComponent<CanvasRenderer>();

            MapPanel mapPanel = panelGO.AddComponent<MapPanel>();
            mapPanel.material = Instantiate<Material>(mapPanel.material);
            mapPanel.material.name = "MapPanel Material";

            panelGO.transform.SetParent(parent.transform, false);

            GameObjectUtility.SetParentAndAlign(panelGO, parent);
            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(panelGO, "Create " + panelGO.name);
            Selection.activeObject = panelGO;

            WorldMap2D map = WorldMap2D.instance;
            if (map == null) {
                // Create a map
                GameObject mapGO = Instantiate(Resources.Load<GameObject>("WPMF/Prefabs/WorldMap2D"));
                mapGO.name = "World Map 2D Edition";
                map = WorldMap2D.instance;
            }
            if (map != null) {
                if (map.transform.position.x < 500) {
                    map.transform.position += new Vector3(500, 500, -500); // keep normal map out of camera
                }
                mapPanel.map = map;
//                map.renderViewportResolution = 1;
                WorldMap2D.instance.renderViewport = panelGO;
            }
        }
#endif

        void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData) {
            if (map == null)
                return;
            map.respectOtherUI = false;
            map.OnMouseEnter();
        }

        void IPointerExitHandler.OnPointerExit(PointerEventData eventData) {
            if (map == null)
                return;
            map.OnMouseExit();
        }
    }
}