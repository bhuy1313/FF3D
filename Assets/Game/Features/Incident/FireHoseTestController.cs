// using UnityEngine;

// public class FireHoseTestController : MonoBehaviour
// {
//     public FireHoseDeployable deployable;
//     public Camera cam;

//     void Update()
//     {
//         if (Input.GetMouseButton(0))
//         {
//             Ray ray = cam.ScreenPointToRay(Input.mousePosition);

//             if (Physics.Raycast(ray, out RaycastHit hit))
//             {
//                 deployable.SetTarget(hit.point);
//             }
//         }
//     }
// }