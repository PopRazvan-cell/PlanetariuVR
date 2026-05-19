using UnityEngine;

public class LaserControllerLeft : MonoBehaviour
{
    [Header("Control prin pinch")]
    public OVRHand leftHand;
    public AstroLaser astroLaser;
    public OVRHand.HandFinger pinchFinger = OVRHand.HandFinger.Index;
    public float toggleCooldown = 0.6f;

    private bool wasPinching;
    private float lastToggleTime;

    void Update()
    {
        if (leftHand == null || astroLaser == null) return;

        if (!leftHand.IsTracked)
        {
            wasPinching = false;
            return;
        }

        bool isPinching = leftHand.GetFingerIsPinching(pinchFinger);

        if (isPinching && !wasPinching && Time.time >= lastToggleTime + toggleCooldown)
        {
            astroLaser.ToggleLaser();
            lastToggleTime = Time.time;
        }

        wasPinching = isPinching;
    }
}
