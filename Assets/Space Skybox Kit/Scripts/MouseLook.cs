using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float lookSensitivity = 500.0f;
    public float lookAngle = 360.0f;

    private float rot_Y = 0.0f;
    private float rot_X = 0.0f;

    void Start()
    {
        Vector3 rotation = transform.localRotation.eulerAngles;
        rot_Y = rotation.y;
        rot_X = rotation.x;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = -Input.GetAxis("Mouse Y");
        rot_Y += mouseX * lookSensitivity * Time.deltaTime;
        rot_X += mouseY * lookSensitivity * Time.deltaTime;
        rot_X = Mathf.Clamp(rot_X, -lookAngle, lookAngle);
        Quaternion localRotation = Quaternion.Euler(rot_X, rot_Y, 0.0f);
        transform.rotation = localRotation;
    }
}
