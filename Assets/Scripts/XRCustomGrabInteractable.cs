using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

public class XRCustomGrabInteractable : XRGrabInteractable
{
    private PhotonView photonView;
    private CharacterController characterController;

    // Start is called before the first frame update
    void Start()
    {
        photonView = GetComponent<PhotonView>();
        characterController = FindObjectOfType<CharacterController>();
    }

    public void InvokeOnSelectEnter(XRBaseInteractor interactor = null) { OnSelectEnter(interactor); }
    public void InvokeOnSelectExit(XRBaseInteractor interactor = null) { OnSelectExit(interactor); }

    protected override void OnSelectEnter(XRBaseInteractor interactor)
    {
        if (photonView) photonView.RequestOwnership();

        TogglPlayerCollision(false);

        if (interactor) base.OnSelectEnter(interactor);
    }

    protected override void OnSelectExit(XRBaseInteractor interactor)
    {
        TogglPlayerCollision(true);

        if (interactor) base.OnSelectExit(interactor);
    }

    private void TogglPlayerCollision(bool enable)
    {
        foreach (Collider c in GetComponents<Collider>())
        {
            Physics.IgnoreCollision(c, characterController, !enable);
        }
    }
}
