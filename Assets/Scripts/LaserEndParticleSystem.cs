using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolumetricLines;

public class LaserEndParticleSystem : MonoBehaviour
{
    private Transform pSystem;
    private ToolGun toolGun;

    // Start is called before the first frame update
    void Start()
    {
        toolGun = GetComponentInParent<ToolGun>();
        pSystem = GetComponentInChildren<ParticleSystem>().transform;
    }

    // Update is called once per frame
    void Update()
    {
        if (toolGun.CurrentHitObj != null
            && LayerMask.LayerToName(toolGun.CurrentHitObj.layer) == "Grab")
        {
            pSystem.position = toolGun.CurrentHitObj.transform.position;
            pSystem.gameObject.SetActive(true);
        }
        else
        {
            pSystem.gameObject.SetActive(false);
        }
    }
}
