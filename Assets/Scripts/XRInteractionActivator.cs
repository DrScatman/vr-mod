using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class XRInteractionActivator : MonoBehaviour
{
    public Collider rightActivationCollider, leftActivationCollider;
    public XRRayInteractor rightRayInteractor, leftRayInteractor;
    public bool enabledOnStart = false;

    private void Start()
    {
        rightRayInteractor.enabled = enabledOnStart;
        leftRayInteractor.enabled = enabledOnStart;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == leftActivationCollider)
        {
            leftRayInteractor.enabled = true;
        }
        else if (other == leftActivationCollider)
        {
            rightRayInteractor.enabled = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == leftActivationCollider)
        {
            leftRayInteractor.enabled = false;
        }
        else if (other == leftActivationCollider)
        {
            rightRayInteractor.enabled = false;
        }
    }
}
