using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ninjazon.Runtime.UI
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup mainScreen;
        [SerializeField] private CanvasGroup optionsScreen;
        [SerializeField] private CanvasGroup creditsScreen;
        [SerializeField] private CanvasGroup quitConfirmation;
        [SerializeField] private Button playButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button optionsBackButton;
        [SerializeField] private Button creditsBackButton;
        [SerializeField] private Button confirmQuitButton;
        [SerializeField] private Button cancelQuitButton;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle controlHintsToggle;
        [SerializeField] private Button resetTutorialButton;
        [SerializeField] private TextMeshProUGUI recordText;
        [SerializeField] private TextMeshProUGUI creditsBodyText;
        [SerializeField] private SerializedViewBinding[] screenBindings;
        [SerializeField] private SerializedViewBinding[] modalBindings;

        public CanvasGroup MainScreen => mainScreen;
        public CanvasGroup OptionsScreen => optionsScreen;
        public CanvasGroup CreditsScreen => creditsScreen;
        public CanvasGroup QuitConfirmation => quitConfirmation;
        public Button PlayButton => playButton;
        public Button OptionsButton => optionsButton;
        public Button CreditsButton => creditsButton;
        public Button QuitButton => quitButton;
        public Button OptionsBackButton => optionsBackButton;
        public Button CreditsBackButton => creditsBackButton;
        public Button ConfirmQuitButton => confirmQuitButton;
        public Button CancelQuitButton => cancelQuitButton;
        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public Toggle VibrationToggle => vibrationToggle;
        public Toggle ScreenShakeToggle => screenShakeToggle;
        public Toggle ControlHintsToggle => controlHintsToggle;
        public Button ResetTutorialButton => resetTutorialButton;
        public TextMeshProUGUI RecordText => recordText;
        public TextMeshProUGUI CreditsBodyText => creditsBodyText;
        public SerializedViewBinding[] ScreenBindings => screenBindings;
        public SerializedViewBinding[] ModalBindings => modalBindings;

        public bool ValidateReferences(out string error)
        {
            if (playButton == null) return Missing(nameof(PlayButton), out error);
            if (optionsButton == null) return Missing(nameof(OptionsButton), out error);
            if (creditsButton == null) return Missing(nameof(CreditsButton), out error);
            if (quitButton == null) return Missing(nameof(QuitButton), out error);
            if (optionsBackButton == null) return Missing(nameof(OptionsBackButton), out error);
            if (creditsBackButton == null) return Missing(nameof(CreditsBackButton), out error);
            if (confirmQuitButton == null) return Missing(nameof(ConfirmQuitButton), out error);
            if (cancelQuitButton == null) return Missing(nameof(CancelQuitButton), out error);
            if (musicSlider == null) return Missing(nameof(MusicSlider), out error);
            if (sfxSlider == null) return Missing(nameof(SfxSlider), out error);
            if (vibrationToggle == null) return Missing(nameof(VibrationToggle), out error);
            if (screenShakeToggle == null) return Missing(nameof(ScreenShakeToggle), out error);
            if (controlHintsToggle == null) return Missing(nameof(ControlHintsToggle), out error);
            if (resetTutorialButton == null) return Missing(nameof(ResetTutorialButton), out error);
            if (recordText == null) return Missing(nameof(RecordText), out error);
            if (creditsBodyText == null) return Missing(nameof(CreditsBodyText), out error);
            if (mainScreen == null) return Missing(nameof(MainScreen), out error);
            if (optionsScreen == null) return Missing(nameof(OptionsScreen), out error);
            if (creditsScreen == null) return Missing(nameof(CreditsScreen), out error);
            if (quitConfirmation == null) return Missing(nameof(QuitConfirmation), out error);
            if (!HasBinding(screenBindings, "Main", mainScreen, out error)) return false;
            if (!HasBinding(screenBindings, "Options", optionsScreen, out error)) return false;
            if (!HasBinding(screenBindings, "Credits", creditsScreen, out error)) return false;
            if (!HasBinding(modalBindings, "Quit", quitConfirmation, out error)) return false;

            error = null;
            return true;
        }

        private static bool Missing(string referenceName, out string error)
        {
            error = $"Main menu view requires {referenceName}.";
            return false;
        }

        private static bool HasBinding(
            SerializedViewBinding[] bindings,
            string id,
            CanvasGroup expectedView,
            out string error)
        {
            if (bindings == null || bindings.Length == 0)
            {
                error = $"Main menu view requires {id} in ScreenBindings or ModalBindings.";
                return false;
            }

            foreach (var binding in bindings)
            {
                if (binding?.Id != id) continue;
                if (binding.View != expectedView)
                {
                    error = $"Main menu view binding '{id}' must reference its matching view.";
                    return false;
                }

                if (binding.FirstSelection == null)
                {
                    error = $"Main menu view binding '{id}' requires a first selection.";
                    return false;
                }

                error = null;
                return true;
            }

            error = $"Main menu view requires {id} in ScreenBindings or ModalBindings.";
            return false;
        }
    }
}
