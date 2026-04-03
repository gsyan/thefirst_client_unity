using System;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

namespace FORGE3D
{
    public class F3DPlayerTurretController : MonoBehaviour
    {
        RaycastHit hitInfo; // Raycast structure
        public F3DTurret turret;
        bool isFiring; // Is turret currently in firing state
        public F3DFXController fxController;

        void Update()
        {
           
            CheckForFire();
        }

        private void LateUpdate()
        {
            CheckForTurn();
        }

        void CheckForFire()
        {
            // Fire turret
            if (isFiring == false && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isFiring = true;
                fxController.Fire();
            }

            // Stop firing
            if (isFiring == true && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isFiring = false;
                fxController.Stop();
            }
        }

        void CheckForTurn()
        {
            // Construct a ray pointing from screen mouse position into world space
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray cameraRay = Camera.main.ScreenPointToRay(mousePos);

            // Raycast
            if (Physics.Raycast(cameraRay, out hitInfo, 500f))
            {
                turret.SetNewTarget(hitInfo.point);
            }
        }
    }
}