using UnityEngine;

public class SpinningSphereCreator : MonoBehaviour
{
    void Awake()
    {
        // Create a sphere primitive
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = Vector3.zero;
        // Add the Rotator component to spin it
        sphere.AddComponent<Rotator>();
    }
}
