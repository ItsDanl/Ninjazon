using System.Collections.Generic;

namespace Ninjazon.Runtime.Navigation
{
    public sealed class ScreenStack
    {
        private readonly List<string> entries = new();

        public int Count => entries.Count;
        public string Current => entries.Count == 0 ? null : entries[^1];

        public void Push(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || Current == id)
            {
                return;
            }

            entries.Add(id);
        }

        public string Pop()
        {
            if (entries.Count == 0)
            {
                return null;
            }

            var index = entries.Count - 1;
            var value = entries[index];
            entries.RemoveAt(index);
            return value;
        }

        public void Clear() => entries.Clear();
    }
}

