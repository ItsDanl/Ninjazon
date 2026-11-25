namespace Ninjazon.Runtime.Gameplay
{
    public sealed class TutorialSequence
    {
        private static readonly string[] Actions = { "Switch", "Jump", "Slide", "Chip", "Jammer" };
        private int index;

        public bool IsComplete => index >= Actions.Length;
        public string CurrentAction => IsComplete ? null : Actions[index];

        public bool Accept(string action)
        {
            if (IsComplete || action != CurrentAction) return false;
            index++;
            return true;
        }
    }
}

