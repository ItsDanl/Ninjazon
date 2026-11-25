using Ninjazon.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ninjazon.Runtime.Gameplay
{
    public sealed class GameplayView : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private CanvasGroup hudScreen;
        [SerializeField] private CanvasGroup pauseScreen;
        [SerializeField] private CanvasGroup optionsScreen;
        [SerializeField] private CanvasGroup resultsScreen;
        [SerializeField] private CanvasGroup confirmModal;
        [SerializeField] private CanvasGroup tutorialModal;

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI creditsText;
        [SerializeField] private TextMeshProUGUI speedText;
        [SerializeField] private RectTransform alertFill;
        [SerializeField] private TextMeshProUGUI alertText;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button jumpButton;
        [SerializeField] private Button slideButton;

        [Header("Pause")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button pauseOptionsButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Options")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle controlHintsToggle;
        [SerializeField] private Button resetTutorialButton;
        [SerializeField] private Button optionsBackButton;

        [Header("Results and modals")]
        [SerializeField] private TextMeshProUGUI resultsText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button resultsMainMenuButton;
        [SerializeField] private TextMeshProUGUI confirmText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI tutorialText;

        [Header("Router")]
        [SerializeField] private SerializedViewBinding[] screenBindings;
        [SerializeField] private SerializedViewBinding[] modalBindings;

        public CanvasGroup HudScreen => hudScreen;
        public CanvasGroup PauseScreen => pauseScreen;
        public CanvasGroup OptionsScreen => optionsScreen;
        public CanvasGroup ResultsScreen => resultsScreen;
        public CanvasGroup ConfirmModal => confirmModal;
        public CanvasGroup TutorialModal => tutorialModal;
        public TextMeshProUGUI ScoreText => scoreText;
        public TextMeshProUGUI CreditsText => creditsText;
        public TextMeshProUGUI SpeedText => speedText;
        public RectTransform AlertFill => alertFill;
        public TextMeshProUGUI AlertText => alertText;
        public Button PauseButton => pauseButton;
        public Button JumpButton => jumpButton;
        public Button SlideButton => slideButton;
        public Button ResumeButton => resumeButton;
        public Button RestartButton => restartButton;
        public Button PauseOptionsButton => pauseOptionsButton;
        public Button MainMenuButton => mainMenuButton;
        public Slider MusicSlider => musicSlider;
        public Slider SfxSlider => sfxSlider;
        public Toggle VibrationToggle => vibrationToggle;
        public Toggle ScreenShakeToggle => screenShakeToggle;
        public Toggle ControlHintsToggle => controlHintsToggle;
        public Button ResetTutorialButton => resetTutorialButton;
        public Button OptionsBackButton => optionsBackButton;
        public TextMeshProUGUI ResultsText => resultsText;
        public Button RetryButton => retryButton;
        public Button ResultsMainMenuButton => resultsMainMenuButton;
        public TextMeshProUGUI ConfirmText => confirmText;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;
        public TextMeshProUGUI TutorialText => tutorialText;
        public SerializedViewBinding[] ScreenBindings => screenBindings;
        public SerializedViewBinding[] ModalBindings => modalBindings;

        public bool ValidateReferences(out string error)
        {
            if (hudScreen == null) return Missing(nameof(HudScreen), out error);
            if (pauseScreen == null) return Missing(nameof(PauseScreen), out error);
            if (optionsScreen == null) return Missing(nameof(OptionsScreen), out error);
            if (resultsScreen == null) return Missing(nameof(ResultsScreen), out error);
            if (confirmModal == null) return Missing(nameof(ConfirmModal), out error);
            if (tutorialModal == null) return Missing(nameof(TutorialModal), out error);
            if (scoreText == null) return Missing(nameof(ScoreText), out error);
            if (creditsText == null) return Missing(nameof(CreditsText), out error);
            if (speedText == null) return Missing(nameof(SpeedText), out error);
            if (alertFill == null) return Missing(nameof(AlertFill), out error);
            if (alertText == null) return Missing(nameof(AlertText), out error);
            if (pauseButton == null) return Missing(nameof(PauseButton), out error);
            if (jumpButton == null) return Missing(nameof(JumpButton), out error);
            if (slideButton == null) return Missing(nameof(SlideButton), out error);
            if (resumeButton == null) return Missing(nameof(ResumeButton), out error);
            if (restartButton == null) return Missing(nameof(RestartButton), out error);
            if (pauseOptionsButton == null) return Missing(nameof(PauseOptionsButton), out error);
            if (mainMenuButton == null) return Missing(nameof(MainMenuButton), out error);
            if (musicSlider == null) return Missing(nameof(MusicSlider), out error);
            if (sfxSlider == null) return Missing(nameof(SfxSlider), out error);
            if (vibrationToggle == null) return Missing(nameof(VibrationToggle), out error);
            if (screenShakeToggle == null) return Missing(nameof(ScreenShakeToggle), out error);
            if (controlHintsToggle == null) return Missing(nameof(ControlHintsToggle), out error);
            if (resetTutorialButton == null) return Missing(nameof(ResetTutorialButton), out error);
            if (optionsBackButton == null) return Missing(nameof(OptionsBackButton), out error);
            if (resultsText == null) return Missing(nameof(ResultsText), out error);
            if (retryButton == null) return Missing(nameof(RetryButton), out error);
            if (resultsMainMenuButton == null) return Missing(nameof(ResultsMainMenuButton), out error);
            if (confirmText == null) return Missing(nameof(ConfirmText), out error);
            if (confirmButton == null) return Missing(nameof(ConfirmButton), out error);
            if (cancelButton == null) return Missing(nameof(CancelButton), out error);
            if (tutorialText == null) return Missing(nameof(TutorialText), out error);
            if (!HasBinding(screenBindings, "HUD", hudScreen, out error)) return false;
            if (!HasBinding(screenBindings, "Pause", pauseScreen, out error)) return false;
            if (!HasBinding(screenBindings, "Options", optionsScreen, out error)) return false;
            if (!HasBinding(screenBindings, "Results", resultsScreen, out error)) return false;
            if (!HasBinding(modalBindings, "Confirm", confirmModal, out error)) return false;
            if (!HasBinding(modalBindings, "Tutorial", tutorialModal, out error)) return false;

            error = null;
            return true;
        }

        private static bool Missing(string referenceName, out string error)
        {
            error = $"Gameplay view requires {referenceName}.";
            return false;
        }

        private static bool HasBinding(
            SerializedViewBinding[] bindings,
            string id,
            CanvasGroup expectedView,
            out string error)
        {
            if (bindings != null)
            {
                foreach (var binding in bindings)
                {
                    if (binding?.Id != id) continue;
                    if (binding.View != expectedView)
                    {
                        error = $"Gameplay view binding '{id}' must reference its matching view.";
                        return false;
                    }

                    if (binding.FirstSelection == null)
                    {
                        error = $"Gameplay view binding '{id}' requires a first selection.";
                        return false;
                    }

                    error = null;
                    return true;
                }
            }

            error = $"Gameplay view requires binding '{id}'.";
            return false;
        }
    }
}
