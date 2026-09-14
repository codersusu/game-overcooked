using UnityEngine;
namespace BaraKitchen {
    // Only appearance changes: the item's root, identity and world transform persist.
    public sealed class VisualState : MonoBehaviour {
        public string itemType;
        public GameObject[] states;
        public string[] stateNames;
        [Min(0)] public int currentState;
        public void SetState(int index) {
            if (states == null || index < 0 || index >= states.Length) return;
            currentState = index;
            for (int i = 0; i < states.Length; i++) if (states[i]) states[i].SetActive(i == index);
        }
        void OnValidate() { SetState(currentState); }
    }
}
