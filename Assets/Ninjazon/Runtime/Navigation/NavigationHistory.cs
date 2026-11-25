namespace Ninjazon.Runtime.Navigation
{
    public sealed class NavigationHistory
    {
        private readonly ScreenStack screens = new();
        private readonly ScreenStack modals = new();

        public string CurrentScreen => screens.Current;
        public string CurrentModal => modals.Current;
        public int ScreenCount => screens.Count;
        public int ModalCount => modals.Count;

        public void PushScreen(string id) => screens.Push(id);
        public void ShowModal(string id) => modals.Push(id);

        public string Back()
        {
            if (modals.Count > 0)
            {
                return modals.Pop();
            }

            return screens.Count > 1 ? screens.Pop() : null;
        }

        public void Clear()
        {
            screens.Clear();
            modals.Clear();
        }
    }
}

