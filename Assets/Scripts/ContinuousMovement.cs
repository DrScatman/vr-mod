using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class ContinuousMovement : MonoBehaviour
{
    public float speed = 1;
    public float gravity = -9.81f;
    public LayerMask groundLayer;
    public float additionalHeight = 0.2f;
    public ToolGun toolGun;

    private float fallingSpeed;
    private XRRig rig;
    private Transform cameraTransfrom;
    private Vector2 stickAxis;
    private bool isPrimaryPressed;
    private bool isSecondaryPressed;
    private CharacterController character;
    private bool isNoClip;
    private float moveY;

    void Start()
    {
        character = GetComponent<CharacterController>();
        rig = GetComponent<XRRig>();
        cameraTransfrom = rig.cameraGameObject.transform;
    }

    void Update()
    {
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        rightDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out isSecondaryPressed);
        rightDevice.TryGetFeatureValue(CommonUsages.primaryButton, out isPrimaryPressed);
        CheckToggleNoClip();

        InputDevice leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out stickAxis);
        leftDevice.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool isStickPressed);

        if (isStickPressed)
            speed = 9;
        else
            speed = 2.5f;
    }

    private void FixedUpdate()
    {
        CapsuleFollowHeadset();

        if (isNoClip)
        {
            if (isPrimaryPressed || isSecondaryPressed)
                moveY = isPrimaryPressed ? 1 : -1;
            else
                moveY = 0;
        }
        else
        {
            moveY = 0;
        }

        Quaternion headYaw = Quaternion.Euler(0, cameraTransfrom.eulerAngles.y, 0);
        Vector3 direction = headYaw * new Vector3(stickAxis.x, moveY, stickAxis.y);

        character.Move(direction * Time.fixedDeltaTime * speed);

        if (!isNoClip)
        {
            if (IsGrounded())
                fallingSpeed = 0;
            else
                fallingSpeed += gravity * Time.fixedDeltaTime;

            character.Move(Vector3.up * fallingSpeed * Time.fixedDeltaTime);
        }
    }

    private bool didClickPrimary;
    private bool oneClick;
    private float clickTime;
    private float clickDelay = 0.5f;

    private void CheckToggleNoClip()
    {
        bool isDepress = didClickPrimary && !isPrimaryPressed;
        didClickPrimary = isPrimaryPressed;

        if (isDepress)
        {
            if (!oneClick)
            {
                oneClick = true;
                clickTime = Time.time;
            }
            else
            {
                // Double click - toggle noclip
                oneClick = false;

                isNoClip = !isNoClip;

                for (int i = 0; i < 32; i++)
                {
                    if (i == 11) continue;
                    Physics.IgnoreLayerCollision(i, 10, isNoClip);
                }

                if (toolGun.interactable.isSelected)
                    toolGun.ToggleGunColliders(!isNoClip);
            }
        }

        if (oneClick && (Time.time - clickTime) > clickDelay)
        {
            oneClick = false;
        }
    }

    private void CapsuleFollowHeadset()
    {
        character.height = rig.cameraInRigSpaceHeight + additionalHeight;
        Vector3 capsuleCenter = transform.InverseTransformPoint(rig.cameraGameObject.transform.position);
        character.center = new Vector3(capsuleCenter.x, character.height / 2 + character.skinWidth, capsuleCenter.z);
    }

    private bool IsGrounded()
    {
        Vector3 rayStart = transform.TransformPoint(character.center);
        float rayLength = character.center.y + 0.01f;
        bool hasHit = Physics.SphereCast(rayStart, character.radius, Vector3.down, out RaycastHit hitInfo, rayLength, groundLayer);
        return hasHit;
    }
}
