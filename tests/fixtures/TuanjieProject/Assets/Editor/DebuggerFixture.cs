using UnityEditor;

[InitializeOnLoad]
public static class DebuggerFixture
{
    private static int health = 1;

    static DebuggerFixture()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        health = Decrement(health);
        if (health <= 0)
        {
            health = 60;
        }
    }

    private static int Decrement(int value)
    {
        return value - 1;
    }
}
