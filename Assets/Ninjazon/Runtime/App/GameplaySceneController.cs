using Ninjazon.Runtime.Gameplay;
using UnityEngine;

namespace Ninjazon.Runtime.App
{
    public sealed class GameplaySceneController : MonoBehaviour
    {
        [SerializeField] private GameplayController controller;

        private void Start()
        {
            if (controller == null)
            {
                Debug.LogError("Gameplay scene entry requires a serialized controller.", this);
                return;
            }

            controller.Initialize();
        }
    }
}
