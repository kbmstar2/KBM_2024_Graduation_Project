using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script is used to calculate the nearest point on the object to the hand.
/// </summary>
public class NearestPointCalculator : MonoBehaviour
{
    public int BoneIndex;
    HandContactSampler contactSampler;
    Collider thisCollider;
    public bool ShowNearestPoint;
    public Vector3 closestPointFromObjectToHand;
    // Start is called before the first frame update
    void Start()
    {
        contactSampler = FindObjectOfType<HandContactSampler>();
        thisCollider = GetComponent<Collider>();
    }

    // Update is called once per frame
    public Vector3 UpdateNearestPoint(int boneindex)
    {
        if (contactSampler.interacting_object != null)
        {
            closestPointFromObjectToHand = thisCollider.ClosestPoint(contactSampler.IKpoint[boneindex]);
            return closestPointFromObjectToHand;
        }
        else return Vector3.zero;
    }
}
