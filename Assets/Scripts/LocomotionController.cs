using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class LocomotionController : MonoBehaviour
{
    public XRController leftTeleportRay, rightTeleportRay;
    public InputHelpers.Button teleportActivationButton;
    public float activationThreshold = 0.1f;
    public bool EnableLeftTeleport { get; set; } = true;
    public bool EnableRightTeleport { get; set; } = true;


    private XRRayInteractor rightRayInteractor, leftRayInteractor;


    private void Start()
    {
        if (rightTeleportRay)
            rightRayInteractor = rightTeleportRay.gameObject.GetComponent<XRRayInteractor>();
        if (leftTeleportRay)
            leftRayInteractor = leftTeleportRay.gameObject.GetComponent<XRRayInteractor>();
    }
    // Update is called once per frame
    void Update()
    {
        Vector3 pos = new Vector3();
        Vector3 norm = new Vector3();
        int index = 0;
        bool validTarget = false;

        if (leftTeleportRay)
        {
            bool isLeftInteractorHovering = leftRayInteractor.TryGetHitInfo(ref pos, ref norm, ref index, ref validTarget);

            leftRayInteractor.allowSelect = EnableLeftTeleport && CheckIfActivated(leftTeleportRay) && !isLeftInteractorHovering;
            leftTeleportRay.gameObject.SetActive(EnableLeftTeleport && CheckIfActivated(leftTeleportRay) && !isLeftInteractorHovering);
        }

        if (rightTeleportRay)
        {
            bool isRightInteractorHovering = rightRayInteractor.TryGetHitInfo(ref pos, ref norm, ref index, ref validTarget);

            rightRayInteractor.allowSelect = EnableRightTeleport && CheckIfActivated(rightTeleportRay) && !isRightInteractorHovering;
            rightTeleportRay.gameObject.SetActive(EnableRightTeleport && CheckIfActivated(rightTeleportRay) && !isRightInteractorHovering);
        }
    }

    public bool CheckIfActivated(XRController controller)
    {
        InputHelpers.IsPressed(controller.inputDevice, teleportActivationButton, out bool isActivated, activationThreshold);
        return isActivated;
    }
}
