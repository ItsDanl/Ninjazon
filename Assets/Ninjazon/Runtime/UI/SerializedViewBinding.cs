using System;
using UnityEngine;

namespace Ninjazon.Runtime.UI
{
    [Serializable]
    public sealed class SerializedViewBinding
    {
        [SerializeField] private string id;
        [SerializeField] private CanvasGroup view;
        [SerializeField] private GameObject firstSelection;

        public string Id => id;
        public CanvasGroup View => view;
        public GameObject FirstSelection => firstSelection;

        public SerializedViewBinding(string id, CanvasGroup view, GameObject firstSelection)
        {
            this.id = id;
            this.view = view;
            this.firstSelection = firstSelection;
        }
    }
}
