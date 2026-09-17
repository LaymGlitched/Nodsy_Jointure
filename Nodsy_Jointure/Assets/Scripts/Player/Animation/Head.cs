using UnityEngine;

namespace Jointure
{
    public class Head : MonoBehaviour
    {
        private Player _player;

        private void Awake()
        {
            _player = GetComponentInParent<Player>();
        }

        void Update()
        {
            _player.AnimationRig.CharacterTransform.position = _player.ControllerRig.CameraTransform.position - Vector3.up * 1.65f;
            Quaternion targetRotation = Quaternion.LookRotation(Vector3.Cross(_player.ControllerRig.CameraTransform.right, Vector3.up));
            _player.AnimationRig.CharacterTransform.rotation = Quaternion.Lerp(_player.AnimationRig.CharacterTransform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
}
