using UnityEngine.SceneManagement;

namespace Ninjazon.Runtime.App
{
    public static class SceneFlow
    {
        public static void MainMenu() => SceneManager.LoadScene("MainMenu");

        public static void Gameplay() => SceneManager.LoadScene("Gameplay");
    }
}
