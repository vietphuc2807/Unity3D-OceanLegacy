using Firebase.Firestore;

[FirestoreData]
public class DayLeaderboardEntryData
{
    [FirestoreProperty]
    public string Nickname { get; set; }

    [FirestoreProperty]
    public int BestDay { get; set; }

    public DayLeaderboardEntryData() { }

    public DayLeaderboardEntryData(string nickname, int bestDay)
    {
        Nickname = nickname;
        BestDay = bestDay;
    }
}