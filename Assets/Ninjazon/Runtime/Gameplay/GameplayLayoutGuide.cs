using UnityEngine;

namespace Ninjazon.Runtime.Gameplay
{
    [ExecuteAlways]
    public sealed class GameplayLayoutGuide : MonoBehaviour
    {
        [SerializeField] private Transform playerAnchor;
        [SerializeField] private Transform entitySpawnAnchor;
        [SerializeField] private Transform[] laneAnchors;

        private void OnDrawGizmos()
        {
            if (playerAnchor == null || entitySpawnAnchor == null || laneAnchors == null) return;
            var left = playerAnchor.position.x - 1.5f;
            var right = entitySpawnAnchor.position.x + 1.5f;
            var colors = new[]
            {
                new Color(0.1f, 0.75f, 1f, 0.8f),
                new Color(1f, 0.15f, 0.7f, 0.8f),
                new Color(1f, 0.72f, 0.1f, 0.8f)
            };
            for (var i = 0; i < laneAnchors.Length; i++)
            {
                if (laneAnchors[i] == null) continue;
                Gizmos.color = colors[Mathf.Min(i, colors.Length - 1)];
                var y = laneAnchors[i].position.y;
                Gizmos.DrawLine(new Vector3(left, y, 0f), new Vector3(right, y, 0f));
                Gizmos.DrawWireSphere(laneAnchors[i].position, 0.18f);
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(playerAnchor.position, new Vector3(0.45f, 0.75f, 0.1f));
            Gizmos.color = new Color(1f, 0.72f, 0.1f, 1f);
            Gizmos.DrawWireCube(entitySpawnAnchor.position, new Vector3(0.45f, 6.8f, 0.1f));
        }
    }
}
