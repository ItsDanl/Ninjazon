using Ninjazon.Runtime.UI;
using UnityEngine;

namespace Ninjazon.Runtime.App
{
    public sealed class MainMenuSceneController : MonoBehaviour
    {
        [SerializeField] private MainMenuController controller;

        private void Start()
        {
            if (controller == null)
            {
                Debug.LogError("Main menu scene entry requires a serialized controller.", this);
                return;
            }

            controller.Initialize();
        }
    }
}
