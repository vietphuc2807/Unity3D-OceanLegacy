using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public FirebaseAuth Auth { get; private set; }
    public FirebaseFirestore DB { get; private set; }

    public bool IsReady { get; private set; }

    private async void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null); 
        DontDestroyOnLoad(gameObject);

        var result = await FirebaseApp.CheckAndFixDependenciesAsync();

        if (result == DependencyStatus.Available)
        {
            Auth = FirebaseAuth.DefaultInstance;
            DB = FirebaseFirestore.DefaultInstance;

            // Tắt Firestore offline persistence
            DB.Settings.PersistenceEnabled = false;

            IsReady = true;

            Debug.Log("Firebase Ready!");
        }
        else
        {
            Debug.LogError($"Firebase Error : {result}");
        }
    }
}