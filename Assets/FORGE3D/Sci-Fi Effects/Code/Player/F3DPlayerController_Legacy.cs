using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
namespace FORGE3D
{
    public class F3DPlayerController : MonoBehaviour
    {

        public F3DTurret[] Turret;

        public bool DebugDrawTarget = true;
        private Vector3 targetPos;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            for (int i = 0; i < Turret.Length; i++)
            {
                if (Turret[i])
                {
                    // Simulating proper player input 
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                        Turret[i].PlayAnimation();
                    else if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                        Turret[i].PlayAnimationLoop();
                    else if (Mouse.current != null && (Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.rightButton.wasReleasedThisFrame))
                        Turret[i].StopAnimation();
                    // Update the turret with the new target position
                    Turret[i].SetNewTarget(GetNewTargetPos());
                }
                else
                    break;
            }
        }

        // Constantly updates the ray against the scene geometry and background dummy collider.
        // Manually track the ray and to v3 position from scene geometry
        Vector3 GetNewTargetPos()
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hitInfo))
            {
                targetPos = hitInfo.point;
                return targetPos;
            }
            return Vector3.zero;
        }

        // Debug draw target 
        void OnDrawGizmos()
        {
            if (DebugDrawTarget && targetPos != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(targetPos, 0.5f);
            }
        }
    }
}