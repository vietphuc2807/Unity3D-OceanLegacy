using UnityEngine;

[DefaultExecutionOrder(250)]
public class CaughtFishFollowAnchor : MonoBehaviour
{
    public Transform Anchor;

    private void LateUpdate()
    {
        if (Anchor == null)
            return;

        transform.position = Anchor.position;
        transform.rotation = Anchor.rotation;
    }
}