using System.Threading.Tasks;
using UnityEngine;

public class AquariumPersistenceLoader : MonoBehaviour
{
    [Tooltip(
        "Catalog chua toan bo ItemData trong game - dung de tra ItemData " +
        "tu ItemId da luu trong Firestore (AquariumTankEntry.ItemId)."
    )]
    [SerializeField] private ItemCatalog _itemCatalog;

    [Tooltip(
        "So lan toi da cho Firebase san sang (IsReady) truoc khi bo cuoc " +
        "va log canh bao - moi lan cho ~100ms, VD 50 = ~5 giay."
    )]
    [SerializeField] private int _maxFirebaseReadyAttempts = 50;

    private readonly PlayerDatabase _playerDatabase = new();

    private async void Start()
    {
        if (_itemCatalog == null)
        {
            Debug.LogWarning(
                "[AquariumPersistenceLoader] Thieu ItemCatalog trong " +
                "Inspector - khong the tra ItemData tu ItemId da luu."
            );
            return;
        }

        if (!await WaitForFirebaseReady())
        {
            Debug.LogWarning(
                "[AquariumPersistenceLoader] Firebase khong san sang sau " +
                "thoi gian cho - bo qua load du lieu be trung bay."
            );
            return;
        }

        string uid = FirebaseManager.Instance.Auth.CurrentUser?.UserId;

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning(
                "[AquariumPersistenceLoader] Khong co user dang dang " +
                "nhap - bo qua load du lieu be trung bay."
            );
            return;
        }

        PlayerData player;

        try
        {
            player = await _playerDatabase.LoadPlayer(uid);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AquariumPersistenceLoader] LoadPlayer that bai: {e}");
            return;
        }

        if (player == null || player.AquariumTanks == null)
            return;

        foreach (AquariumTankEntry entry in player.AquariumTanks)
        {
            PlaceEntry(entry);
        }
    }

    private void PlaceEntry(AquariumTankEntry entry)
    {
        if (string.IsNullOrEmpty(entry.ItemId))
            return;

        ItemData fish = _itemCatalog.GetById(entry.ItemId);

        if (fish == null)
        {
            Debug.LogWarning(
                $"[AquariumPersistenceLoader] Khong tim thay ItemData cho " +
                $"ItemId='{entry.ItemId}' (Tank {entry.TankId}) trong " +
                "ItemCatalog - bo qua, khong dat lai con ca nay."
            );
            return;
        }

        AquariumDisplayManager tank =
            AquariumDisplayManager.GetByTankId(entry.TankId);

        if (tank == null)
        {
            Debug.LogWarning(
                $"[AquariumPersistenceLoader] Khong tim thay be co " +
                $"TankId={entry.TankId} trong scene hien tai."
            );
            return;
        }

        tank.PlaceFish(fish, entry.SizeMultiplier);
    }

    private async Task<bool> WaitForFirebaseReady()
    {
        int attempts = 0;

        while (attempts < _maxFirebaseReadyAttempts)
        {
            if (FirebaseManager.Instance != null &&
                FirebaseManager.Instance.IsReady)
            {
                return true;
            }

            attempts++;

            await Task.Delay(100);
        }

        return false;
    }
}