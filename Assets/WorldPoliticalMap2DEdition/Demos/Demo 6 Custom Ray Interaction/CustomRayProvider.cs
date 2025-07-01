using UnityEngine;

namespace WPMF.Demos {
    public class CustomRayProvider : MonoBehaviour {

        void Start() {
            WorldMap2D map = WorldMap2D.instance;
            map.pointerSource = PointerSource.Custom;
            map.pointerRayProvider = MyCustomRayProvider;
        }

        Ray MyCustomRayProvider() {
            // replace with your own logic to provide a ray to the map 2d interaction system
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            return ray;
        }
    }

}