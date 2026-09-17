using UnityEngine;

namespace Jointure
{
    [AddComponentMenu("Jointure/Grabs/Grab (Snap)")]
    public class SnapGrab : Grab
    {
        public override float EvaluateGrabRank(Transform handTransform)
        {
            return base.EvaluateGrabRank(handTransform) * 3f;
        }

        public override float CalculateRank(Transform handTransform) => EvaluateGrabRank(handTransform);

        public override void AlignHand(Hand hand)
        {
            hand.PhysicsHandTransform.position = transform.TransformPoint(hand.PalmTransform.InverseTransformPoint(hand.PhysicsHandTransform.position));
            hand.PhysicsHandTransform.rotation = transform.rotation * Quaternion.Inverse(hand.PalmTransform.rotation) * hand.PhysicsHandTransform.rotation;
        }
    }
}