using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace FORGE3D
{
    public class F3DTurretUI : MonoBehaviour
    {
        private List<F3DFXController> _fxControllers;

        public Text WeaponTypeText;

        // GUI captions
        private string[] _fxTypeName =
        {
            "Vulcan", "Sologun", "Sniper", "Shotgun", "Seeker", "Railgun", "Plasmagun", "Plasma beam",
            "Heavy plasma beam", "Lightning gun", "Flamethrower", "Pulse laser"
        };

        // Use this for initialization
        void Awake()
        {
            
            _fxControllers = new List<F3DFXController>(FindObjectsByType<F3DFXController>(FindObjectsSortMode.None));
        }

        private void Start()
        {
            SetWeaponTypeText();
        }

        void SetWeaponTypeText()
        {
            WeaponTypeText.text = _fxTypeName[(int) _fxControllers[0].DefaultFXType];
        }
        
        public void OnButtonNext()
        {
            ToggleWeaponType(true);
        }

        public void OnButtonPrevious()
        {
            ToggleWeaponType(false);
        }

        private void ToggleWeaponType(bool next)
        {
            foreach (var _fx in _fxControllers)
            {
                if (next) _fx.NextWeapon();
                else _fx.PrevWeapon();
                _fx.Stop();
            }
            
            SetWeaponTypeText();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;
            // Switch weapon types using keyboard keys
            if (Keyboard.current[Key.E].wasPressedThisFrame)
                OnButtonNext();
            else if (Keyboard.current[Key.Q].wasPressedThisFrame)
                OnButtonPrevious();
        }
    }
}