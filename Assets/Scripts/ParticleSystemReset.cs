using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystemReset : MonoBehaviour
{
    public List<ParticleSystem> particleSystems;

    private void OnEnable()
    {
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Clear();
            ps.time = 0;

            if (!ps.gameObject.activeSelf)
                ps.gameObject.SetActive(true);
            if (!ps.isPlaying)
                ps.Play();
        }
    }
}
