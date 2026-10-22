using System.Collections.Generic;
using Firebase.Firestore;

/// <summary>
/// 1 document trong collection "MuseumLeaderboard" (ID = UID nguoi choi)
/// - ban CONG KHAI dung rieng cho bang xep hang, CHI chua nhung gi
/// bang xep hang can (ten Museum, tong Fame Point, ca dang trung bay).
/// KHONG chua Email/Name that, nen co the cho moi nguoi choi da dang
/// nhap DOC collection nay ma khong lo lo thong tin ca nhan trong
/// collection "Players" (van chi chu so huu doc/ghi duoc).
///
/// Duoc client cua CHINH nguoi choi ghi moi khi dat/lay ca (xem
/// MuseumStatsUI -> AquariumSaveService.SaveMuseumStats ->
/// PlayerDatabase.SaveMuseumStats), va MuseumLeaderboardUI doc (top N).
/// </summary>
[FirestoreData]
public class LeaderboardEntryData
{
    [FirestoreProperty]
    public string Nickname { get; set; }

    [FirestoreProperty]
    public int TotalFamePoint { get; set; }

    [FirestoreProperty]
    public List<AquariumTankEntry> AquariumTanks { get; set; }

    // Firestore SDK can constructor rong de deserialize.
    public LeaderboardEntryData() { }

    public LeaderboardEntryData(
        string nickname,
        int totalFamePoint,
        List<AquariumTankEntry> aquariumTanks)
    {
        Nickname = nickname;
        TotalFamePoint = totalFamePoint;
        AquariumTanks = aquariumTanks ?? new List<AquariumTankEntry>();
    }
}