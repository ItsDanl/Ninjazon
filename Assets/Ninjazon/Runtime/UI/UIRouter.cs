using System;
using System.Collections.Generic;
using Ninjazon.Runtime.Navigation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ninjazon.Runtime.UI
{
    public interface IBackHandler
    {
        bool TryHandleBack();
    }

    public sealed class UIRouter : MonoBehaviour, IBackHandler
    {
        private readonly Dictionary<string, CanvasGroup> screens = new();
        private readonly Dictionary<string, CanvasGroup> modals = new();
        private readonly Dictionary<string, GameObject> selections = new();
        private readonly NavigationHistory history = new();

        public Func<bool> RootFallback { get; set; }
        public string CurrentScreen => history.CurrentScreen;
        public string CurrentModal => history.CurrentModal;

        public void Configure(SerializedViewBinding[] screens, SerializedViewBinding[] modals)
        {
            ValidateBindings(screens, modals);

            foreach (var view in this.screens.Values) SetVisible(view, false);
            foreach (var view in this.modals.Values) SetVisible(view, false);
            this.screens.Clear();
            this.modals.Clear();
            selections.Clear();
            history.Clear();

            RegisterBindings(screens, RegisterScreen);
            RegisterBindings(modals, RegisterModal);
        }

        public void RegisterScreen(string id, CanvasGroup view, GameObject firstSelection = null)
        {
            screens[id] = view;
            selections[id] = firstSelection;
            SetVisible(view, false);
        }

        public void RegisterModal(string id, CanvasGroup view, GameObject firstSelection = null)
        {
            modals[id] = view;
            selections[id] = firstSelection;
            SetVisible(view, false);
        }

        public void PushScreen(string id)
        {
            if (!screens.TryGetValue(id, out var view) || history.CurrentScreen == id) return;
            if (history.CurrentScreen != null && screens.TryGetValue(history.CurrentScreen, out var current)) SetVisible(current, false);
            history.PushScreen(id);
            SetVisible(view, true);
            Select(id);
        }

        public bool PopScreen()
        {
            var closed = history.Back();
            if (closed == null || !screens.TryGetValue(closed, out var view)) return false;
            SetVisible(view, false);
            if (history.CurrentScreen != null && screens.TryGetValue(history.CurrentScreen, out var previous)) SetVisible(previous, true);
            Select(history.CurrentScreen);
            return true;
        }

        public void ShowModal(string id)
        {
            if (!modals.TryGetValue(id, out var view) || history.CurrentModal == id) return;
            history.ShowModal(id);
            SetVisible(view, true);
            Select(id);
        }

        public bool CloseModal()
        {
            if (history.CurrentModal == null) return false;
            var closed = history.Back();
            if (closed == null || !modals.TryGetValue(closed, out var view)) return false;
            SetVisible(view, false);
            Select(history.CurrentModal ?? history.CurrentScreen);
            return true;
        }

        public bool TryHandleBack()
        {
            if (CloseModal()) return true;
            if (PopScreen()) return true;
            return RootFallback?.Invoke() ?? false;
        }

        private static void RegisterBindings(
            SerializedViewBinding[] bindings,
            Action<string, CanvasGroup, GameObject> register)
        {
            if (bindings == null) return;

            foreach (var binding in bindings)
            {
                register(binding.Id, binding.View, binding.FirstSelection);
            }
        }

        private static void ValidateBindings(params SerializedViewBinding[][] bindingGroups)
        {
            var ids = new HashSet<string>();

            foreach (var bindings in bindingGroups)
            {
                if (bindings == null) continue;

                foreach (var binding in bindings)
                {
                    if (binding == null || string.IsNullOrWhiteSpace(binding.Id))
                    {
                        throw new ArgumentException("Serialized view bindings require a non-blank id.");
                    }

                    if (binding.View == null)
                    {
                        throw new ArgumentException($"Serialized view binding '{binding.Id}' requires a view.");
                    }

                    if (!ids.Add(binding.Id))
                    {
                        throw new ArgumentException($"Serialized view binding id '{binding.Id}' is duplicated.");
                    }
                }
            }
        }

        private void Select(string id)
        {
            if (id != null && selections.TryGetValue(id, out var selected) && selected != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selected);
            }
        }

        private static void SetVisible(CanvasGroup view, bool visible)
        {
            view.alpha = visible ? 1f : 0f;
            view.interactable = visible;
            view.blocksRaycasts = visible;
            view.gameObject.SetActive(visible);
        }
    }
}
