using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CollisionCheck : MonoBehaviour
{
    int currentCollisionCnt;

    private static int nJoints = 15;
    public int BoneIndex;
    HandContactSampler ContactSampler;
    ObjectFrictionApplier frictionApplier;
    CapsuleCollider FingerCollider;

    ContactPoint collisionPoint;
    // Save the collision point and normal vector.
    public Vector3 CollisionPosition;
    Vector3 CollisionNormalPosition;
    // Save the relative position of the collision point and normal vector to the object's transform.
    Vector3 CollisionRelativePosition;
    Vector3 CollisionNormalRelativePosition;
    Vector3 ColliderCenter = Vector3.zero;
    Vector3 WorldColliderCenter = Vector3.zero;
    Transform ObjectTransform;
    GameObject Interacting_Object;
    int layerMask;
    int layerMask2;
    int resultmask;

    // For Raycasting
    bool doRaycast;
    bool ishit;
    float Raydist;
    float CtoC_dist;

    Vector3 Raypoint;
    Vector3 Raydirection;

    public int numberOfSamples = 5;

    public bool ShowRedSphere;
    public bool ShowIKpoint;

    void Start()
    {
        currentCollisionCnt = 0;
        ContactSampler = GetComponentInParent<HandContactSampler>();
        layerMask = 1 << LayerMask.NameToLayer("ContactPoint");
        layerMask2 = 1 << LayerMask.NameToLayer("RenderedHand");
        resultmask = layerMask | layerMask2;
        resultmask = ~resultmask;
        doRaycast = false;
        FingerCollider = GetComponent<CapsuleCollider>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Object"))
        {
            if (currentCollisionCnt == 0)
            {
                Interacting_Object = collision.gameObject.transform.parent.gameObject;
                collisionPoint = collision.contacts[0];
                ObjectTransform = collision.collider.transform;
                ContactSampler.interacting_object = Interacting_Object;

                //Save the position of the collision point and its normal vector.
                CollisionPosition = collisionPoint.point;
                CollisionNormalPosition = collisionPoint.normal;

                // Convert the relative point of the collision point to the object's transform to ensure that the collision point and its normal can follow the movement of the object.
                CollisionRelativePosition = ObjectTransform.InverseTransformPoint(CollisionPosition);
                CollisionNormalRelativePosition = Quaternion.Inverse(ObjectTransform.rotation) * CollisionNormalPosition;

                ContactSampler.followRealHand[BoneIndex] = false;

                frictionApplier = collision.gameObject.GetComponentInParent<ObjectFrictionApplier>();
                doRaycast = true;
            }
            currentCollisionCnt += 1;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Object"))
        {
            ContactSampler.followRealHand[BoneIndex] = false;
        }
    }

    /// <summary>
    /// In FixedUpdate, a ray is shot from a point in the direction of the contact point's normal to the current position of the collider,
    /// comparing the position where the ray hits with the contact point to determine if the contact point has moved.
    /// Force is continuously applied whether the contact point moves or not, as it remains in contact with the object.
    /// </summary>
    void FixedUpdate()
    {
        if (Interacting_Object != null && Interacting_Object.CompareTag("Object") && doRaycast && currentCollisionCnt == 1)
        {
            ColliderCenter = Vector3.right * (FingerCollider.height / 2 - FingerCollider.radius);
            WorldColliderCenter = FingerCollider.transform.TransformPoint(ColliderCenter);

            // Information about the ray
            // Raypoint: The starting point of the ray, Raydirection is the direction in which the ray is shot.
            Raypoint = CollisionPosition + CollisionNormalPosition * 0.05f;
            Raydirection = (WorldColliderCenter - Raypoint).normalized;

            Ray ray = new Ray(Raypoint, Raydirection);
            RaycastHit hitInfo;

            Raydist = Vector3.Distance(Raypoint, WorldColliderCenter);
            // Shoot the ray to check if it hits the object.
            ishit = Physics.Raycast(ray, out hitInfo, Raydist, resultmask) && hitInfo.collider.CompareTag("Object");
            // Distance from the contact point to the new point created by the ray.
            if (ishit) // If the ray hits the object.
            {
                CtoC_dist = Vector3.Distance(CollisionPosition, hitInfo.point);
                if (CtoC_dist >= ContactSampler.ContactPointUpdateThreshold)
                // If the distance from the contact point created by the ray exceeds a certain threshold, update the contact point.
                {
                    Debug.Log("Update contact point.");
                    CollisionRelativePosition = ObjectTransform.InverseTransformPoint(hitInfo.point);
                    CollisionNormalRelativePosition = Quaternion.Inverse(ObjectTransform.rotation) * hitInfo.normal;

                    
                    ContactSampler.followRealHand[BoneIndex] = false;
                    ContactSampler.isUpdated[BoneIndex / 3] = true;
                    ContactSampler.isContactPointUpdated[BoneIndex] = true;
                }
                else
                {
                    ContactSampler.isUpdated[BoneIndex / 3] = false;
                    ContactSampler.isContactPointUpdated[BoneIndex] = false;
                }
            }

            if (currentCollisionCnt == 0)
            {
                doRaycast = false;
                Interacting_Object = null;
            }
            // Apply force.
            frictionApplier.ApplyFriction(CollisionPosition, CollisionNormalPosition, WorldColliderCenter);
        }
    }

    void Update()
    {
        if (Interacting_Object != null && Interacting_Object.CompareTag("Object") && doRaycast)
        {
            CollisionPosition = ObjectTransform.TransformPoint(CollisionRelativePosition);
            CollisionNormalPosition = ObjectTransform.rotation * CollisionNormalRelativePosition;

            if (ishit)
            {
                UpdateSphere(CollisionPosition, CollisionNormalPosition);
            }
        }
    }
    void OnCollisionExit(Collision collision)
    {
        ContactSampler.followRealHand[BoneIndex] = true;
        ContactSampler.isUpdated[BoneIndex / 3] = false;
        ContactSampler.isContactPointUpdated[BoneIndex] = false;
        if (collision.gameObject.CompareTag("Object")) currentCollisionCnt--;
    }

    void MakeContactArea(Vector3 centerPoint, Vector3 normal)
    {
        float radius = 0.01f;
        Vector3 normal_local = ObjectTransform.InverseTransformDirection(normal);

        Vector3 anyPerpendicular = Vector3.Cross(normal_local, Vector3.right).magnitude > 0.01f ? Vector3.right : Vector3.up;
        Vector3 direction = Vector3.Cross(normal_local, anyPerpendicular).normalized;

        direction = ObjectTransform.TransformDirection(direction);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60 * Mathf.Deg2Rad;
            Vector3 pointPosition = centerPoint + Quaternion.AngleAxis(angle * Mathf.Rad2Deg, normal) * direction * radius;
        }
    }

    void UpdateSphere(Vector3 centerPoint, Vector3 normal)
    {
        MakeContactArea(centerPoint, normal);
    }
}
