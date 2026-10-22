using Firebase.Firestore;

[FirestoreData]
public class BugReportData
{
    [FirestoreProperty]
    public string UID { get; private set; }

    [FirestoreProperty]
    public string Email { get; private set; }

    [FirestoreProperty]
    public string Message { get; private set; }

    [FirestoreProperty]
    public string Version { get; private set; }

    [FirestoreProperty]
    public string Platform { get; private set; }

    [FirestoreProperty]
    public string OS { get; private set; }

    [FirestoreProperty]
    public string Device { get; private set; }

    [FirestoreProperty]
    public string Scene { get; private set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; private set; }

    /// <summary>Danh dau da xu ly - tu sua tay tren Firebase Console.</summary>
    [FirestoreProperty]
    public bool Resolved { get; private set; }

    public BugReportData() { }

    public BugReportData(
        string uid,
        string email,
        string message,
        string version,
        string platform,
        string os,
        string device,
        string scene)
    {
        UID = uid;
        Email = email;
        Message = message;
        Version = version;
        Platform = platform;
        OS = os;
        Device = device;
        Scene = scene;

        CreatedAt = Timestamp.GetCurrentTimestamp();
        Resolved = false;
    }
}