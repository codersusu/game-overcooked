using UnityEngine;
namespace BaraKitchen {
    public sealed class SceneAnchor : MonoBehaviour {
        public string purpose;
        public Transform approach;
        public Transform dishPosition;
        void OnDrawGizmosSelected() {
            Gizmos.color = new Color(.2f,.7f,.65f);
            Gizmos.DrawWireSphere(transform.position,.09f);
            Gizmos.DrawRay(transform.position, transform.forward * .3f);
            if(approach) Gizmos.DrawLine(approach.position, transform.position);
        }
    }
}
