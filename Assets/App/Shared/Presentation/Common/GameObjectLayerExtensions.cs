using UnityEngine;

namespace FloorBreaker.Shared.Presentation.Common
{
    public static class GameObjectLayerExtensions
    {
        public static void SetLayerRecursive(this GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
