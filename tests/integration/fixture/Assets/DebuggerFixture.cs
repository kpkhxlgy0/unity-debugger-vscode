using UnityEngine;

public sealed class FixtureBehaviour : MonoBehaviour
{
    private int health;

    private void Start()
    {
        health = 1;
    }

    private void Update()
    {
        health = 0;
    }
}
