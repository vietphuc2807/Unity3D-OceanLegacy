using Firebase.Firestore;

[FirestoreData]
public class AquariumTankEntry
{
    [FirestoreProperty]
    public int TankId { get; set; }

    [FirestoreProperty]
    public string ItemId { get; set; }

    [FirestoreProperty]
    public float SizeMultiplier { get; set; }

    public AquariumTankEntry() { }

    public AquariumTankEntry(int tankId, string itemId, float sizeMultiplier)
    {
        TankId = tankId;
        ItemId = itemId;
        SizeMultiplier = sizeMultiplier;
    }
}