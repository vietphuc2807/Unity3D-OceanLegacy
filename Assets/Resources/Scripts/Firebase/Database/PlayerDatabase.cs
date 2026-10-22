using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class PlayerDatabase
{
    private CollectionReference Players =>
        FirebaseManager.Instance.DB.Collection("Players");

    public async Task SavePlayer(PlayerData player)
    {
        await Players.Document(player.UID).SetAsync(player);
    }

    public async Task DeletePlayer(string uid)
    {
        await Players.Document(uid).DeleteAsync();
    }

    public async Task VerifyPlayer(string uid)
    {
        await Players.Document(uid)
            .UpdateAsync(nameof(PlayerData.Verified), true);
    }

    public async Task<PlayerData> LoadPlayer(string uid)
    {
        DocumentSnapshot snapshot =
            await Players.Document(uid).GetSnapshotAsync();

        if (!snapshot.Exists)
            return null;

        return snapshot.ConvertTo<PlayerData>();
    }


    // ============================================================
    // AQUARIUM TANKS (be trung bay ca o Museum)
    // ============================================================

    public async Task SaveAquariumTank(
        string uid,
        int tankId,
        string itemId,
        float sizeMultiplier)
    {
        PlayerData player = await LoadPlayer(uid);

        if (player == null)
            return;

        List<AquariumTankEntry> tanks =
            player.AquariumTanks ?? new List<AquariumTankEntry>();

        tanks.RemoveAll(t => t.TankId == tankId);

        tanks.Add(
            new AquariumTankEntry(tankId, itemId, sizeMultiplier)
        );

        await Players.Document(uid)
            .UpdateAsync(nameof(PlayerData.AquariumTanks), tanks);
    }

    public async Task RemoveAquariumTank(string uid, int tankId)
    {
        PlayerData player = await LoadPlayer(uid);

        if (player == null || player.AquariumTanks == null)
            return;

        int removed = player.AquariumTanks.RemoveAll(t => t.TankId == tankId);

        if (removed <= 0)
            return;

        await Players.Document(uid)
            .UpdateAsync(nameof(PlayerData.AquariumTanks), player.AquariumTanks);
    }


    // ============================================================
    // MUSEUM LEADERBOARD (collection rieng "MuseumLeaderboard")
    // ============================================================

    private CollectionReference Leaderboard =>
        FirebaseManager.Instance.DB.Collection("MuseumLeaderboard");

    public async Task SaveMuseumStats(
        string uid,
        int totalFamePoint,
        List<AquariumTankEntry> tanks)
    {
        PlayerData player = await LoadPlayer(uid);

        if (player == null)
            return;

        LeaderboardEntryData entry =
            new(player.Nickname, totalFamePoint, tanks);

        await Leaderboard.Document(uid).SetAsync(entry);
    }

    /// <summary>
    /// Lay 1 LAN TOP <paramref name="limit"/> Museum co Fame Point cao
    /// nhat. Van giu lai ham nay (khong con dung boi UI Leaderboard
    /// nua - da chuyen sang ListenMuseumLeaderboard de cap nhat lien
    /// tuc) phong khi noi khac can 1 snapshot tinh, khong can realtime.
    /// </summary>
    public async Task<List<LeaderboardEntryData>> GetMuseumLeaderboard(int limit = 50)
    {
        QuerySnapshot snapshot = await Leaderboard
            .OrderByDescending(nameof(LeaderboardEntryData.TotalFamePoint))
            .Limit(limit)
            .GetSnapshotAsync();

        List<LeaderboardEntryData> result = new();

        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (doc.Exists)
                result.Add(doc.ConvertTo<LeaderboardEntryData>());
        }

        return result;
    }

    /// <summary>
    /// MOI: lang nghe TOP <paramref name="limit"/> Museum theo Fame
    /// Point THEO THOI GIAN THUC - Firestore se TU GOI lai
    /// <paramref name="onUpdate"/> moi khi BAT KY document nao trong
    /// ket qua query nay thay doi (ai do dat/lay ca -> SaveMuseumStats
    /// ghi len -> moi client dang lang nghe nhan duoc ngay), khong can
    /// tu poll/fetch lai thu cong.
    ///
    /// Tra ve ListenerRegistration - PHAI goi .Stop() (hoac
    /// StopListening) khi khong con can nghe nua (VD dong panel), neu
    /// khong se ro ri listener + ton doc Firestore vo ich mai mai.
    /// </summary>
    public ListenerRegistration ListenMuseumLeaderboard(
        int limit,
        Action<List<LeaderboardEntryData>> onUpdate)
    {
        return Leaderboard
            .OrderByDescending(nameof(LeaderboardEntryData.TotalFamePoint))
            .Limit(limit)
            .Listen(snapshot =>
            {
                List<LeaderboardEntryData> result = new();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (doc.Exists)
                        result.Add(doc.ConvertTo<LeaderboardEntryData>());
                }

                onUpdate?.Invoke(result);
            });
    }


    // ============================================================
    // DAY LEADERBOARD (collection rieng "DayLeaderboard")
    // ============================================================

    private CollectionReference DayLeaderboard =>
        FirebaseManager.Instance.DB.Collection("DayLeaderboard");

    public async Task<int> GetMyBestDay(string uid)
    {
        DocumentSnapshot snapshot =
            await DayLeaderboard.Document(uid).GetSnapshotAsync();

        if (!snapshot.Exists)
            return 0;

        return snapshot.ConvertTo<DayLeaderboardEntryData>().BestDay;
    }

    public async Task SaveBestDay(string uid, int bestDay)
    {
        PlayerData player = await LoadPlayer(uid);

        if (player == null)
            return;

        DayLeaderboardEntryData entry =
            new(player.Nickname, bestDay);

        await DayLeaderboard.Document(uid).SetAsync(entry);
    }

    /// <summary>Lay 1 LAN - xem chu thich GetMuseumLeaderboard o tren, tuong tu.</summary>
    public async Task<List<DayLeaderboardEntryData>> GetDayLeaderboard(int limit = 50)
    {
        QuerySnapshot snapshot = await DayLeaderboard
            .OrderByDescending(nameof(DayLeaderboardEntryData.BestDay))
            .Limit(limit)
            .GetSnapshotAsync();

        List<DayLeaderboardEntryData> result = new();

        foreach (DocumentSnapshot doc in snapshot.Documents)
        {
            if (doc.Exists)
                result.Add(doc.ConvertTo<DayLeaderboardEntryData>());
        }

        return result;
    }

    /// <summary>MOI: lang nghe TOP ky luc so ngay THEO THOI GIAN THUC - xem chu thich ListenMuseumLeaderboard o tren, tuong tu.</summary>
    public ListenerRegistration ListenDayLeaderboard(
        int limit,
        Action<List<DayLeaderboardEntryData>> onUpdate)
    {
        return DayLeaderboard
            .OrderByDescending(nameof(DayLeaderboardEntryData.BestDay))
            .Limit(limit)
            .Listen(snapshot =>
            {
                List<DayLeaderboardEntryData> result = new();

                foreach (DocumentSnapshot doc in snapshot.Documents)
                {
                    if (doc.Exists)
                        result.Add(doc.ConvertTo<DayLeaderboardEntryData>());
                }

                onUpdate?.Invoke(result);
            });
    }

    private CollectionReference BugReports =>
        FirebaseManager.Instance.DB.Collection("BugReports");

    public async Task SaveBugReport(BugReportData report)
    {
        await BugReports.AddAsync(report);   // Firestore tu tao ID
    }
}