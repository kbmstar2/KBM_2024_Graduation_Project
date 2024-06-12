using Meta.WitAi.CallbackHandlers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEngine;
/// <summary>
/// This script is used to apply IK to the hand when the hand collides with the object,
/// or modify IK when the contact point of hand joint updated.
/// </summary>

// Data class
[System.Serializable]
public class JointLimit
{
    public List<AxisLimit> axisLimits = new List<AxisLimit>();
}

[System.Serializable]
public class AxisLimit
{
    public LocalAxis axis = LocalAxis.X;
    public float lowerLimit = 0f;
    public float upperLimit = 0f;
}

public enum LocalAxis
{
    X,
    Y,
    Z
}
// Data class end
public class HandContactSampler : MonoBehaviour
{
    TrackingHandEqual trackingequal;
    CollisionCheck[] collisionCheck;
    NearestPointCalculator[] nearestPointCalculators;
    public GameObject interacting_object;

    // hand information
    GameObject renderhand;
    private static int nJoints = 15;
    private static int nFingers = 5;
    public Transform wrists;
    public Transform[] joints;
    Quaternion[] init_poses = new Quaternion[nJoints];
    public List<JointLimit> jointLimits = new List<JointLimit>();
    private int[][] jointIndex = new int[][]
    {
        new int[] {0, 1, 2},
        new int[] {3, 4, 5},
        new int[] {6, 7, 8},
        new int[] {9, 10, 11},
        new int[] {12, 13, 14}
    };
    private Vector3?[] targetPos = new Vector3?[nJoints];
    public bool[] isIKfinished = new bool[nJoints];
    public bool[] followRealHand = new bool[nJoints];
    public bool[] isUpdated = new bool[nFingers];

    // for IK
    public Transform[] trackedjoints;

    [SerializeField]
    private List<List<AxisLimit>> jointAngleLimitsInTracking = new List<List<AxisLimit>>();

    private int maxIterations = 10;
    private float threshold = 0.007f;
    public Vector3[] IKpoint = new Vector3[nJoints];
    public Transform[] NearestPoint = new Transform[nJoints];
    bool isCollidedAnywhere = false;
    public bool[] isFirstIK = new bool[nJoints];
    public bool[] isContactPointUpdated = new bool[nJoints];

    public float ContactPointUpdateThreshold;

    void Start()
    {
        renderhand = GameObject.Find("RenderedOculusHandPrefab");
        trackingequal = renderhand.GetComponent<TrackingHandEqual>();
        collisionCheck = FindObjectsOfType<CollisionCheck>();
        nearestPointCalculators = FindObjectsOfType<NearestPointCalculator>();
        for (int i = 0; i < nJoints; i++)
        {
            followRealHand[i] = true;
            isIKfinished[i] = false;
            init_poses[i] = joints[i].localRotation;
            isFirstIK[i] = true;

            jointAngleLimitsInTracking.Add(jointLimits[i].axisLimits);
        }
        for (int i = 0; i < nFingers; i++)
        {
            isUpdated[i] = false;
        }

    }
    private void Update()
    {
        for(int i = 0; i < nJoints;i++)
        {
            Vector3 NormalizedBoneTransform = NormalizeAngle(trackedjoints[i].localEulerAngles);
            foreach (var axisLimit in jointAngleLimitsInTracking[i])
            {
                if (axisLimit.axis == LocalAxis.X)
                {
                    NormalizedBoneTransform.x = Mathf.Clamp(NormalizedBoneTransform.x, axisLimit.lowerLimit, axisLimit.upperLimit);
                }
                else if (axisLimit.axis == LocalAxis.Y)
                {
                    NormalizedBoneTransform.y = Mathf.Clamp(NormalizedBoneTransform.y, axisLimit.lowerLimit, axisLimit.upperLimit);
                }
                else
                {
                    NormalizedBoneTransform.z = Mathf.Clamp(NormalizedBoneTransform.z, axisLimit.lowerLimit, axisLimit.upperLimit);
                }
            }
            trackedjoints[i].localEulerAngles = NormalizedBoneTransform;
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        isCollidedAnywhere = false;
        for (int i = 0; i < nFingers; i++)
        {
            // Check if any part of each finger is in contact and perform IK if true
            int index = i * 3;
            if (!followRealHand[index] || !followRealHand[index + 1] || !followRealHand[index + 2])
            {
                isCollidedAnywhere = true;
            }
            else
            {
                for (int j = jointIndex[i][0]; j <= jointIndex[i][2]; j++)
                {
                    if(isFirstIK[j]) isIKfinished[j] = false;
                }
            }
        }
        // If no part is in contact, follow tracking
        if (!isCollidedAnywhere)
        {
            trackingequal.MatchTrackingFinger(0, nJoints - 1);
            trackingequal.MatchTrackingWrist();
            renderhand.transform.SetParent(null);
            for(int i = 0; i < nJoints; i++)
            {
                isFirstIK[i] = true;
            }
        }
        else
        {
            // If any part is in contact, perform IK and fix the position of the object and hand
            renderhand.transform.SetParent(interacting_object.transform);
            for (int i = 0; i < nFingers; i++)
            {
                for (int j = 0; j < jointIndex[i].Length; j++)
                {
                    // Update IK target and end effector positions
                    // Each CollisionCheck and NearestPointCalculator updates their values in real time, and this script simply fetches them
                    CheckContactPointUpdate(i, j);
                    GetPoints(jointIndex[i][j]);
                    GetNearestPoints(jointIndex[i][j]);
                }
                for (int j = 0; j < jointIndex[i].Length; j++)
                {
                    CheckTarget(jointIndex[i][j]);
                    if (targetPos[jointIndex[i][j]].HasValue && NearestPoint[jointIndex[i][j]].position != Vector3.zero)
                    {
                        // Perform CCD solve from the j-th joint of the i-th finger to the base joint of the i-th finger
                        CCDSolve(i, j);
                    }
                }
            }
        }
    }

    private void CheckContactPointUpdate(int finger_index, int joint_index)
    {
        if (isContactPointUpdated[joint_index])
        {
            for (int i = jointIndex[finger_index][0]; i <= jointIndex[finger_index][joint_index]; i++)
            {
                isIKfinished[i] = false;
            }
        }
    }

    private void CheckTarget(int Joint_index)
    {
        // when the Joint_index-th joint contact with object, input target position in targetPos.
        if (!followRealHand[Joint_index])
        {
            targetPos[Joint_index] = IKpoint[Joint_index];
        }
        else
        {
            targetPos[Joint_index] = null;
        }
    }
    private void CCDSolve(int finger_index, int joint_index)
    {
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            GetNearestPoints(jointIndex[finger_index][joint_index]);
            for (int j = jointIndex[finger_index][joint_index]; j >= jointIndex[finger_index][0]; j--)
            {
                if (isIKfinished[j]) continue;  // Perform IK from the joint being CCD solved to the root, but do not move joints that have already undergone IK.
                foreach (AxisLimit axisLimit in jointLimits[j].axisLimits)
                {
                    float angleDiff = CalcAngleDiff(axisLimit.axis, joints[j], NearestPoint[jointIndex[finger_index][joint_index]].position, targetPos[jointIndex[finger_index][joint_index]]);

                    Quaternion currentRot = Quaternion.Inverse(init_poses[j]) * joints[j].localRotation;
                    Vector3 currentAngles = currentRot.eulerAngles;

                    currentAngles.x = NormalizeAngle(currentAngles).x;
                    currentAngles.y = NormalizeAngle(currentAngles).y;
                    currentAngles.z = NormalizeAngle(currentAngles).z;

                    if (axisLimit.axis == LocalAxis.X)
                    {
                        float currentAngle = currentAngles.x;
                        float rotAngle = Mathf.Clamp(currentAngle + angleDiff, axisLimit.lowerLimit, axisLimit.upperLimit) - currentAngle;

                        joints[j].Rotate(rotAngle, 0, 0, Space.Self);
                    }
                    else if (axisLimit.axis == LocalAxis.Y)
                    {
                        float currentAngle = currentAngles.y;
                        float rotAngle = Mathf.Clamp(currentAngle + angleDiff, axisLimit.lowerLimit, axisLimit.upperLimit) - currentAngle;

                        joints[j].Rotate(0, rotAngle, 0, Space.Self);
                    }
                    else
                    {
                        float currentAngle = currentAngles.z;
                        float rotAngle = Mathf.Clamp(currentAngle + angleDiff, axisLimit.lowerLimit, axisLimit.upperLimit) - currentAngle;

                        joints[j].Rotate(0, 0, rotAngle, Space.Self);
                    }
                }
            }
            // Finish if within threshold
            if (Vector3.Distance(NearestPoint[jointIndex[finger_index][joint_index]].position, targetPos[jointIndex[finger_index][joint_index]].Value) < threshold)
            {
                for (int i = jointIndex[finger_index][0]; i <= jointIndex[finger_index][joint_index]; i++)
                {
                    isIKfinished[i] = true;
                    isFirstIK[i] = false;
                }
                return;
            }
        }
        // Forcefully finish if IK is not perfect but exceeds certain iterations
        for (int i = jointIndex[finger_index][0]; i <= jointIndex[finger_index][joint_index]; i++)
        {
            isIKfinished[i] = true;
            isFirstIK[i] = false;
        }
    }

    private float CalcAngleDiff(LocalAxis axis, Transform referJoint, Vector3 EndEffector, Vector3? TargetPosition)
    {
        Vector3 toEndEffector = referJoint.InverseTransformDirection(EndEffector - referJoint.position);
        Vector3 toTarget = referJoint.InverseTransformDirection(TargetPosition.Value - referJoint.position);
        toEndEffector.Normalize();
        toTarget.Normalize();

        float EndEffectorAngle, TargetAngle;

        if (axis == LocalAxis.X)
        {
            EndEffectorAngle = Mathf.Atan2(toEndEffector.z, toEndEffector.y) * Mathf.Rad2Deg;
            TargetAngle = Mathf.Atan2(toTarget.z, toTarget.y) * Mathf.Rad2Deg;
        }
        else if (axis == LocalAxis.Y)
        {
            EndEffectorAngle = Mathf.Atan2(toEndEffector.x, toEndEffector.z) * Mathf.Rad2Deg;
            TargetAngle = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        }
        else
        {
            EndEffectorAngle = Mathf.Atan2(toEndEffector.y, toEndEffector.x) * Mathf.Rad2Deg;
            TargetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        }
        return Mathf.DeltaAngle(EndEffectorAngle, TargetAngle);
    }
    Vector3 NormalizeAngle(Vector3 angle)
    {
        while (angle.x > 180) angle.x -= 360;
        while (angle.x < -180) angle.x += 360;
        while (angle.y > 180) angle.y -= 360;
        while (angle.y < -180) angle.y += 360;
        while (angle.z > 180) angle.z -= 360;
        while (angle.z < -180) angle.z += 360;
        return angle;
    }

    public void GetPoints(int Joint_index)
    {
        foreach (var script in collisionCheck)
        {
            if (script.BoneIndex == Joint_index)
            {
                IKpoint[Joint_index] = script.CollisionPosition;
            }
        }
    }

    public void GetNearestPoints(int Joint_index)
    {
        foreach (var script in nearestPointCalculators)
        {
            if (script.BoneIndex == Joint_index)
            {
                NearestPoint[Joint_index].position = script.UpdateNearestPoint(Joint_index);
            }
        }
    }
}
