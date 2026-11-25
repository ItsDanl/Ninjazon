using Ninjazon.Runtime.App;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ninjazon.Runtime.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private MainMenuView view;
        [SerializeField] private UIRouter router;

        private bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            if (view == null || router == null)
            {
                Debug.LogError("Main menu controller requires a serialized view and router.", this);
                return;
            }

            if (!view.ValidateReferences(out var error))
            {
                Debug.LogError(error, view);
                return;
            }

            initialized = true;
            var app = NinjazonApp.Ensure();
            router.Configure(view.ScreenBindings, view.ModalBindings);
            router.RootFallback = OpenQuit;
            view.PlayButton.onClick.AddListener(Play);
            view.OptionsButton.onClick.AddListener(() => router.PushScreen("Options"));
            view.CreditsButton.onClick.AddListener(() => router.PushScreen("Credits"));
            view.QuitButton.onClick.AddListener(() => router.ShowModal("Quit"));
            view.OptionsBackButton.onClick.AddListener(() => router.PopScreen());
            view.CreditsBackButton.onClick.AddListener(() => router.PopScreen());
            view.ConfirmQuitButton.onClick.AddListener(Application.Quit);
            view.CancelQuitButton.onClick.AddListener(() => router.CloseModal());
            view.MusicSlider.onValueChanged.AddListener(value => SetMusicVolume(app, value));
            view.SfxSlider.onValueChanged.AddListener(value => SetSfxVolume(app, value));
            view.VibrationToggle.onValueChanged.AddListener(value => SetVibration(app, value));
            view.ScreenShakeToggle.onValueChanged.AddListener(value => SetScreenShake(app, value));
            view.ControlHintsToggle.onValueChanged.AddListener(value => SetControlHints(app, value));
            view.ResetTutorialButton.onClick.AddListener(app.ResetTutorial);
            router.PushScreen("Main");
            if (app.Data != null) RefreshFromSave(app);
        }

        private void Play()
        {
            NinjazonApp.Ensure().Audio.PlayUi();
            SceneFlow.Gameplay();
        }

        private bool OpenQuit()
        {
            router.ShowModal("Quit");
            return true;
        }

        private void RefreshFromSave(NinjazonApp app)
        {
            var data = app.Data;
            view.RecordText.text = $"BEST  {data.bestScore:N0}   //   {data.bestDistance:0} m\nCREDITS  {data.lifetimeCredits:N0}";
            view.MusicSlider.SetValueWithoutNotify(data.musicVolume);
            view.SfxSlider.SetValueWithoutNotify(data.sfxVolume);
            view.VibrationToggle.SetIsOnWithoutNotify(data.vibration);
            view.ScreenShakeToggle.SetIsOnWithoutNotify(data.screenShake);
            view.ControlHintsToggle.SetIsOnWithoutNotify(data.controlHints);
        }

        private static void SetMusicVolume(NinjazonApp app, float value)
        {
            app.Data.musicVolume = value;
            app.Commit();
        }

        private static void SetSfxVolume(NinjazonApp app, float value)
        {
            app.Data.sfxVolume = value;
            app.Commit();
        }

        private static void SetVibration(NinjazonApp app, bool value)
        {
            app.Data.vibration = value;
            app.Commit();
        }

        private static void SetScreenShake(NinjazonApp app, bool value)
        {
            app.Data.screenShake = value;
            app.Commit();
        }

        private static void SetControlHints(NinjazonApp app, bool value)
        {
            app.Data.controlHints = value;
            app.Commit();
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) router?.TryHandleBack();
        }
    }
}
