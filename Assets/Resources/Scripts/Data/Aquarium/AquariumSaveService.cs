using UnityEngine;

public static class AquariumSaveService
{
    private static readonly PlayerDatabase _playerDatabase = new();

    private static string CurrentUid =>
        FirebaseManager.Instance != null &&
        FirebaseManager.Instance.IsReady &&
        FirebaseManager.Instance.Auth.CurrentUser != null
            ? FirebaseManager.Instance.Auth.CurrentUser.UserId
            : null;

    public static async void SaveTank(
        int tankId,
        string itemId,
        float sizeMultiplier)
    {
        string uid = CurrentUid;

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning(
                "[AquariumSaveService] Khong co user dang dang nhap - " +
                "bo qua luu du lieu be."
            );
            return;
        }

        try
        {
            await _playerDatabase.SaveAquariumTank(
                uid,
                tankId,
                itemId,
                sizeMultiplier
            );
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AquariumSaveService] SaveTank that bai: {e}");
        }
    }

    public static async void ClearTank(int tankId)
    {
        string uid = CurrentUid;

        if (string.IsNullOrEmpty(uid))
            return;

        try
        {
            await _playerDatabase.RemoveAquariumTank(uid, tankId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AquariumSaveService] ClearTank that bai: {e}");
        }
    }

    public static async void SaveMuseumStats(
        int totalFamePoint,
        System.Collections.Generic.List<AquariumTankEntry> tanks)
    {
        string uid = CurrentUid;

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning(
                "[AquariumSaveService] Khong co user dang dang nhap - " +
                "bo qua luu tong Fame Point cua Museum."
            );
            return;
        }

        try
        {
            await _playerDatabase.SaveMuseumStats(uid, totalFamePoint, tanks);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AquariumSaveService] SaveMuseumStats that bai: {e}");
        }
    }
}