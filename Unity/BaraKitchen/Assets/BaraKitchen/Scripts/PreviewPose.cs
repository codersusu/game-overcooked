using UnityEngine;
namespace BaraKitchen {
    /// <summary>Art-review rig only. Replace approximate weights and poses before gameplay animation.</summary>
    public sealed class PreviewPose : MonoBehaviour {
        public Transform head, leftArm, rightArm, leftForearm, rightForearm, leftLeg, rightLeg, leftFoot, rightFoot;
        public bool seated, walking, animate;
        public void ApplyPose(float time = 0) {
            float sway = animate ? Mathf.Sin(time * 5) : 0;
            if(head) head.localRotation = Quaternion.Euler(seated ? 9 : 0, animate ? sway * 2 : 0, 0);
            if(leftArm) leftArm.localRotation = Quaternion.Euler(seated ? -42 : walking ? sway * 13 : 0, 0, seated ? -10 : 0);
            if(rightArm) rightArm.localRotation = Quaternion.Euler(seated ? -48 + sway * 8 : walking ? -sway * 13 : 0, 0, seated ? 10 : 0);
            if(leftForearm) leftForearm.localRotation = Quaternion.Euler(seated ? -24 : 0,0,0);
            if(rightForearm) rightForearm.localRotation = Quaternion.Euler(seated ? -32 - sway * 8 : 0,0,0);
            if(leftLeg) leftLeg.localRotation = Quaternion.Euler(seated ? -65 : walking ? -sway * 20 : 0,0,0);
            if(rightLeg) rightLeg.localRotation = Quaternion.Euler(seated ? -65 : walking ? sway * 20 : 0,0,0);
            if(leftFoot) leftFoot.localRotation = Quaternion.Euler(seated ? 25 : 0,0,0);
            if(rightFoot) rightFoot.localRotation = Quaternion.Euler(seated ? 25 : 0,0,0);
        }
        void LateUpdate() { if(animate) ApplyPose(Time.time); }
    }
}
