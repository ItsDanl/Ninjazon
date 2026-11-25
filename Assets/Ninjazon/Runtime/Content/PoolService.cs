using System.Collections.Generic;
using UnityEngine;

namespace Ninjazon.Runtime.Content
{
    public sealed class PoolService : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> available = new();
        private readonly Dictionary<GameObject, GameObject> origins = new();

        public GameObject Get(GameObject prefab, Transform parent = null)
        {
            if (prefab == null) return null;
            if (!available.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                available[prefab] = queue;
            }
            var instance = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab);
            origins[instance] = prefab;
            instance.transform.SetParent(parent, false);
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null || !origins.TryGetValue(instance, out var prefab))
            {
                if (instance != null) Destroy(instance);
                return;
            }
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            available[prefab].Enqueue(instance);
        }
    }
}

