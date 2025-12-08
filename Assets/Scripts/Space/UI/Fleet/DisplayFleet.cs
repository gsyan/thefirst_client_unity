// using System.Collections.Generic;
// using UnityEngine;
// using static ApiClient;

// public class DisplayFleet : MonoBehaviour
// {
//     public const int LAYER_DISPLAY_FLEET = 30;
//     private static readonly Vector3 DISPLAY_FLEET_OFFSET = new(10000, 0, 0);

//     [HideInInspector] public UIPanelFleetUpgrade m_panelFleetUpgrade;
//     private List<DisplayShip> m_displayShips = new List<DisplayShip>();

//     public bool m_enableModuleSelection = false;


//     public void InitializeDisplayFleet(FleetInfo fleetInfo)
//     {
//         transform.position = DISPLAY_FLEET_OFFSET;
//         LoadFleetDisplayByData(fleetInfo);
        
//     }

//     public void LoadFleetDisplayByData(FleetInfo fleetInfo)
//     {
//         if (fleetInfo?.ships == null || fleetInfo.ships.Length == 0) return;
//         ClearDisplayFleet();
//         CreateDisplayFleet(fleetInfo);
//     }

//     private void ClearDisplayFleet()
//     {
//         foreach (DisplayShip ship in m_displayShips)
//         {
//             if (ship != null)
//                 DestroyImmediate(ship.gameObject);
//         }
//         m_displayShips.Clear();
//     }
//     private void CreateDisplayFleet(FleetInfo fleetInfo)
//     {
//         EFormationType formationType = System.Enum.TryParse<EFormationType>(fleetInfo.formation, true, out var result) ? result : EFormationType.LinearHorizontal;
//         for (int i = 0; i < fleetInfo.ships.Length; i++)
//             CreateDisplayShip(fleetInfo.ships[i], formationType);
//     }
//     private void CreateDisplayShip(ShipInfo shipInfo, EFormationType formationType)
//     {
//         GameObject shipObj = new GameObject($"Ship_{shipInfo.positionIndex}");
//         shipObj.transform.SetParent(transform);
//         SpaceShip spaceShip = shipObj.AddComponent<SpaceShip>();
//         //spaceShip.InitializeSpaceShip(shipInfo, false);
//         shipObj.transform.localPosition = spaceShip.CalculateShipPosition(formationType);
//         SetGameObjectLayer(shipObj, LAYER_DISPLAY_FLEET); // after moduleBody created
//         DisplayShip displayShip = shipObj.AddComponent<DisplayShip>();
//         displayShip.InitializeDisplayShip(this, spaceShip);

//         m_displayShips.Add(displayShip);
//     }

//     private void SetGameObjectLayer(GameObject obj, int layer)
//     {
//         obj.layer = layer;
//         foreach (Transform child in obj.transform)
//             SetGameObjectLayer(child.gameObject, layer);
//     }

//     public void ZoomCamera(float deltaZoom)
//     {
//         CameraController.Instance.ZoomCamera(deltaZoom);
//     }

//     public void RefreshDisplayFleet()
//     {
//         var currentFleetInfo = DataManager.Instance.m_currentFleetInfo;
//         if (currentFleetInfo != null)
//             LoadFleetDisplayByData(currentFleetInfo);
//     }

//     public void UpdateFormation(EFormationType formationType)
//     {
//         if (m_displayShips == null || m_displayShips.Count == 0) return;

//         for (int i = 0; i < m_displayShips.Count; i++)
//         {
//             DisplayShip ship = m_displayShips[i];
//             if (ship != null)
//             {
//                 ship.transform.localPosition = ship.GetComponent<SpaceShip>().CalculateShipPosition(formationType);
//             }
                
//         }
//     }

//     public void OnModuleClicked(SpaceShip ship, ModuleBase moduleBase)
//     {
//         string moduleTypeString = moduleBase?.GetType().Name ?? "null";
//         string moduleInfoString = "";

//         if (moduleBase is ModuleBody body)
//         {
//             moduleInfoString = $"ModuleBody[{body.m_moduleBodyInfo.bodyIndex}]";
//         }
//         else if (moduleBase is ModuleWeapon weapon)
//         {
//             moduleInfoString = $"ModuleWeapon[{weapon.m_classId}]";
//         }
//         else if (moduleBase is ModuleEngine engine)
//         {
//             moduleInfoString = $"ModuleEngine[{engine.m_classId}]";
//         }
        
//         Debug.Log($"Module clicked: Ship {ship.name}, {moduleTypeString} {moduleInfoString}");

//         if (m_panelFleetUpgrade != null)
//             m_panelFleetUpgrade.OnModuleSelected(ship, moduleBase);
//     }

//     public void ClearAllSelections()
//     {
//         foreach (DisplayShip ship in m_displayShips)
//         {
//             if (ship != null)
//                 ship.ClearSelection();
//         }
//     }

// }