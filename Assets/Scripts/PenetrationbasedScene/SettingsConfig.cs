using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsConfig : MonoBehaviour
{
    HandContactSampler contactSampler;
    ObjectFrictionApplier[] frictionApplier;

    [Header("Contact Point Update Threshold")]
    public float ContactPointUpdateThreshold;

    [Header("Friction Coefficients")]
    public float u_static;
    public float u_dynamic;

    private void Start()
    { 
        contactSampler = FindObjectOfType<HandContactSampler>();
        frictionApplier = FindObjectsOfType<ObjectFrictionApplier>();
    }

    // Start is called before the first frame update
    void Update()
    {
        contactSampler.ContactPointUpdateThreshold = ContactPointUpdateThreshold;
        foreach (var fa in frictionApplier)
        {
            fa.u_static = u_static;
            fa.u_dynamic = u_dynamic;
        }
    }
}
