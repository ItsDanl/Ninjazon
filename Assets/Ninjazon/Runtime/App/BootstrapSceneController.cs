using UnityEngine;

namespace Ninjazon.Runtime.App
{
    /// <summary>
    /// Entry point of the game. The Bootstrap scene contains only this component;
    /// GameBoot builds the camera, world and UI in code (see Runtime/Game).
    /// The old MainMenu/Gameplay scenes are kept in the project for reference.
    /// </summary>
    public sealed class BootstrapSceneController : MonoBehaviour
    {
        private void Awake() => Ninjazon.Game.GameBoot.Launch();
    }
}
