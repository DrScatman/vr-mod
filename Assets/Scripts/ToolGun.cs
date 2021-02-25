using System.Collections;
using UnityEngine;
using VolumetricLines;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class ToolGun : MonoBehaviour
{
    public float maxLaserDistance = 1000;
    public GameObject laser;
    public Transform barrel;
    public SketchfabModelSelect sketchfabModelSelect;
    public SnapTurnProvider snapTurnComp;

    public Vector3 CurrentHitPosition => hitPosition;
    public GameObject CurrentHitObj => hitObj;
    public XRCustomGrabInteractable interactable;

    private GameObject copyObj;
    private GameObject hitObj;
    private Vector3 hitPosition = Vector3.zero;
    private Animator gunAnimator;
    private AudioSource audioSource;
    private VolumetricLineBehavior laserBehavior;
    private bool isTapFire;
    private Coroutine laserCoroutine, tapfireCoroutine;
    private bool isMovingObj;
    private Collider[] gunColliders;

    private void Start()
    {
        laser.SetActive(false);
        interactable = GetComponent<XRCustomGrabInteractable>();
        gunAnimator = GetComponent<Animator>();
        gunColliders = GetComponentsInChildren<Collider>();
        audioSource = laser.GetComponent<AudioSource>();
        laserBehavior = laser.GetComponent<VolumetricLineBehavior>();
    }

    private void Update()
    {
        CheckUndoButton();

        if (laser.activeSelf)
        {
            laser.transform.position = barrel.position;
            laser.transform.rotation = barrel.rotation;

            if (isMovingObj && CurrentHitObj != null)
            {
                CheckCopyButton();
                CheckFreezeButton();
                CheckScale();
                CheckChangeDistance();
            }
            else if (Physics.Raycast(barrel.position, barrel.forward, out RaycastHit hit, maxLaserDistance))
            {
                hitObj = hit.transform.gameObject;
                hitPosition = hit.point;
                laserBehavior.EndPos = new Vector3(0, 0, Vector3.Distance(barrel.position, hitPosition));

                if (LayerMask.LayerToName(hitObj.layer) == "Grab")
                {
                    Transform t = hitObj.transform;
                    PreviousActions.AddPreviousAction(PreviousActions.ActionType.Transform, hitObj, t.position, t.rotation, t.lossyScale);

                    snapTurnComp.enabled = false;
                    ToggleObjMovement(true);

                }
            }
            else
            {
                hitObj = null;
                hitPosition = Vector3.zero;
                laserBehavior.EndPos = new Vector3(0, 0, maxLaserDistance);
            }
        }
    }

    public void ActivateLaser()
    {
        laser.transform.position = barrel.position;
        laser.transform.rotation = barrel.rotation;
        laser.SetActive(true);

        audioSource.Play();
        gunAnimator.SetBool("playSpin", true);

        isTapFire = true;
        laserCoroutine = StartCoroutine(LaserCoroutine());
        tapfireCoroutine = StartCoroutine(TapFireTimer());
    }

    public void DeactivateLaser(bool isFreeze = false)
    {
        ToggleObjMovement(false, isFreeze);
        laser.SetActive(false);

        if (isTapFire)
        {
            TrySpawnModel();
        }

        audioSource.Stop();
        gunAnimator.SetBool("playSpin", false);

        snapTurnComp.enabled = true;
        isTapFire = false;
        hitPosition = Vector3.zero;
        hitObj = null;

        if (laserCoroutine != null)
            StopCoroutine(laserCoroutine);
        if (tapfireCoroutine != null)
            StopCoroutine(tapfireCoroutine);
    }

    private IEnumerator TapFireTimer()
    {
        isTapFire = true;
        yield return new WaitForSecondsRealtime(0.5f);
        isTapFire = false;
    }

    public void TrySpawnModel()
    {
        if (!string.IsNullOrEmpty(sketchfabModelSelect.SelectedModelUid))
        {
            Debug.Log("Here");
            sketchfabModelSelect.LoadOrDuplicateModel(sketchfabModelSelect.SelectedModelUid, CurrentHitPosition + Vector3.up, Quaternion.identity, Vector3.one);
        }
        else if (copyObj != null)
        {
            string key = sketchfabModelSelect.GetSpawnedObjKey(copyObj);

            if (string.IsNullOrEmpty(key))
            {
                GameObject obj = Instantiate(copyObj, CurrentHitPosition, Quaternion.identity);
                PreviousActions.AddPreviousAction(PreviousActions.ActionType.Spawn, obj);
            }
            else
            {
                sketchfabModelSelect.LoadOrDuplicateModel(key, CurrentHitPosition + Vector3.up, Quaternion.identity, Vector3.one);
            }
        }
    }

    private void ToggleObjMovement(bool enable, bool isFreeze = false)
    {
        if (enable && CurrentHitObj.transform.parent != barrel.transform && barrel.transform.childCount == 0)
        {
            TogglePhysics(CurrentHitObj, false);
            ToggleGunColliders(false);

            TryInvokeOnSelect(CurrentHitObj, true);
            CurrentHitObj.transform.SetParent(barrel.transform, true);

            isMovingObj = true;
        }
        else if (!enable && barrel.childCount > 0)
        {
            foreach (Transform child in barrel.transform)
            {
                TogglePhysics(child.gameObject, !isFreeze);
                ToggleGunColliders(true);

                child.SetParent(null, true);
                TryInvokeOnSelect(child.gameObject, false);

                isMovingObj = false;
            }
        }
    }

    private void TogglePhysics(GameObject obj, bool enable)
    {
        if (obj.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = !enable;
            if (enable)
            {
                // rb.velocity = ;
                // rb.angularVelocity = ;
            }
        }
    }

    public void ToggleGunColliders(bool enable)
    {
        foreach (Collider c in gunColliders)
        {
            c.enabled = enable;
        }
    }

    private void TryInvokeOnSelect(GameObject obj, bool isEnter)
    {
        if (obj.TryGetComponent(out XRCustomGrabInteractable interactable))
        {
            if (isEnter)
                interactable.InvokeOnSelectEnter(null);
            else
                interactable.InvokeOnSelectExit(null);
        }
    }

    private void CheckFreezeButton()
    {
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool pressed);

        if (pressed)
        {
            ToggleObjMovement(false, true);
            DeactivateLaser(true);
        }
    }

    private void CheckChangeDistance()
    {
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 inputAxis);
        float absY = System.Math.Abs(inputAxis.y);

        if (absY > 0.2f && absY >= System.Math.Abs(inputAxis.x)
            && (inputAxis.y > 0 || laserBehavior.EndPos.z - laserBehavior.StartPos.z > 0.5))
        {
            CurrentHitObj.transform.Translate(barrel.forward * (inputAxis.y / 3), Space.World);
            laserBehavior.EndPos = new Vector3(0, 0, Vector3.Distance(barrel.position, CurrentHitObj.transform.position));
        }
    }

    private void CheckScale()
    {
        InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 inputAxis);
        float absX = System.Math.Abs(inputAxis.x);

        if (absX > 0.2f && absX >= System.Math.Abs(inputAxis.y))
        {
            Vector3 scale = CurrentHitObj.transform.localScale;
            float a = inputAxis.x * (Mathf.Min(scale.x, scale.y, scale.z) / 20);
            Vector3 newScale = scale + new Vector3(a, a, a);

            if (newScale.x > 0 && newScale.y > 0 && newScale.z > 0)
            {
                CurrentHitObj.transform.localScale = newScale;
                laserBehavior.EndPos = new Vector3(0, 0, Vector3.Distance(barrel.position, CurrentHitObj.transform.position));
            }
        }

    }

    private void CheckCopyButton()
    {
        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed);

        if (pressed)
        {
            copyObj = CurrentHitObj;
            sketchfabModelSelect.SelectedModelUid = null;
        }
    }

    private void CheckUndoButton()
    {
        InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed);

        if (pressed && !isUndoCooldown)
        {
            PreviousActions.Undo();

            isUndoCooldown = true;
            StartCoroutine(UndoCooldown());
        }
    }

    private bool isUndoCooldown;

    private IEnumerator UndoCooldown()
    {
        isUndoCooldown = true;
        yield return new WaitForSecondsRealtime(0.5f);
        isUndoCooldown = false;
    }

    private IEnumerator LaserCoroutine()
    {
        laserBehavior.LightSaberFactor = 1f;
        SetBarrelSpinSpeed(3f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.95f;
        SetBarrelSpinSpeed(3.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.9f;
        SetBarrelSpinSpeed(4f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.85f;
        SetBarrelSpinSpeed(4.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.8f;
        SetBarrelSpinSpeed(5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.75f;
        SetBarrelSpinSpeed(5.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.7f;
        SetBarrelSpinSpeed(6f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.65f;
        SetBarrelSpinSpeed(6.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.6f;
        SetBarrelSpinSpeed(7f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.55f;
        SetBarrelSpinSpeed(7.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.5f;
        SetBarrelSpinSpeed(8f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.45f;
        SetBarrelSpinSpeed(8.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.4f;
        SetBarrelSpinSpeed(9f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.35f;
        SetBarrelSpinSpeed(9.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.3f;
        SetBarrelSpinSpeed(10f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.25f;
        SetBarrelSpinSpeed(10.5f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.2f;
        SetBarrelSpinSpeed(11f);
        yield return new WaitForSecondsRealtime(0.5f);
        laserBehavior.LightSaberFactor = 0.1f;
        SetBarrelSpinSpeed(12f);
    }

    public void SetBarrelSpinSpeed(float animSpeed)
    {
        gunAnimator.SetFloat("spinSpeed", animSpeed);
    }
}
