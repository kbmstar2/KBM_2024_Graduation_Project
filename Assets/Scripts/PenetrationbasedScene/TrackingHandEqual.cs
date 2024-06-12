using Oculus.Interaction.Surfaces;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Script for tracking. This script is used to match the tracking of the real hand to the rendered hand.
/// </summary>
public class TrackingHandEqual : MonoBehaviour
{
    
    public Transform[] realhand;
    public Transform[] renderedhand;

    public Transform realwrist;
    public Transform renderedwrist;

    public void MatchTrackingWrist()
    {
        renderedwrist.position = realwrist.position;
        renderedwrist.rotation = realwrist.rotation;
    }

    public void MatchTrackingFinger(int start, int end)
    {
        
        if(realhand.Length == renderedhand.Length)
        {
            for (int i = start; i <= end; i++)
            {
                renderedhand[i].rotation = realhand[i].rotation;
            }
        }  
    }
}
