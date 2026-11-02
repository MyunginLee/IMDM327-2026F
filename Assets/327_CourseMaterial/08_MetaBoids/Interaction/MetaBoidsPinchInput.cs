using IMDM327.MetaBoids;
using UnityEngine;

/*
 * TWO HANDS -> TWO GPU FORCES
 * Reuse the course's MediaPipeBodyTracker, just like InteractiveBody.
 * Pinch is detected at the fingertips; the pull follows the tracked wrist.
 */
public class MetaBoidsPinchInput : MonoBehaviour
{
    public MediaPipeBodyTracker tracker;
    public MediaPipeInput cameraInput;
    public MetaBoidsController boids;
    [Range(0f, 10f)] public float pullStrength = 5f;

    public Vector4 LeftHand { get; private set; }
    public Vector4 RightHand { get; private set; }

    void Update()
    {
        LeftHand = HandForce(tracker.LeftHandPosition, tracker.LeftHandTracked && tracker.LeftHandPinch);
        RightHand = HandForce(tracker.RightHandPosition, tracker.RightHandTracked && tracker.RightHandPinch);
        boids.SetPinchForces(LeftHand, RightHand);
    }

    Vector4 HandForce(Vector3 position, bool pinched)
    {
        // MediaPipe's Y starts at the top. Mirror X to match the camera preview.
        float x = cameraInput.mirrorX ? 1f - position.x : position.x;
        return new Vector4(x, 1f - position.y, pinched ? pullStrength : 0f, 0f);
    }

    void OnDisable()
    {
        LeftHand = RightHand = Vector4.zero;
        boids.SetPinchForces(Vector4.zero, Vector4.zero);
    }
}
