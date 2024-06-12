using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// For applying friction to the object. This script is used to apply friction to the object when the hand collides with the object.
/// </summary>
public class ObjectFrictionApplier : MonoBehaviour
{
    public float u_static = 0.62f;
    public float u_dynamic = 0.4f;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void ApplyFriction(Vector3 contactPoint, Vector3 normal, Vector3 handColliderPosition)
    {
        Vector3 contactNormal = normal;
        Vector3 f_contact = (contactPoint - handColliderPosition) * 100.0f;
        Vector3 n_contact = Vector3.Dot(f_contact, contactNormal) * contactNormal;
        Vector3 t_contact = f_contact - n_contact;
        bool F_inside = Vector3.Dot(f_contact, contactNormal) > 0 && t_contact.magnitude <= Vector3.Dot(f_contact, contactNormal) * u_static;

        Vector3 final_T_contact;
        if (F_inside)
        {
            final_T_contact = t_contact;
        }
        else
        {
            final_T_contact = t_contact * u_dynamic;
        }
        if(Vector3.Dot(contactNormal, f_contact) >= 0)
        {
            rb.AddForceAtPosition(-n_contact, contactPoint); // normal force
            rb.AddForceAtPosition(-final_T_contact, contactPoint); // tangential force
        }
        
    }
}


