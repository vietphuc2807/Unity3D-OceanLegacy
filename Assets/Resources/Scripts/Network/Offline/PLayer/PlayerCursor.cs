using UnityEngine;

public class PlayerCursor : MonoBehaviour
{
    public static bool IsUIOpen;

    private void Start()
    {
        if (IsUIOpen)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }
    }

    private void Update()
    {
        if (IsUIOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }

        if (Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }
    }

    public static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}