using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Chay o scene GAMEPLAY (noi co GameDayManager). Theo doi
/// GameDayManager.CurrentDay (Networked nen MOI client deu doc duoc),
/// va moi khi so ngay VUOT ky luc da luu cua CHINH nguoi choi nay thi
/// ghi len Firestore (collection DayLeaderboard).
///
/// - Thua bi reset ve Day 1 KHONG anh huong ky luc (chi ghi khi cao hon).
/// - Ky luc cu duoc doc 1 LAN tu Firestore luc bat dau, de khong ghi
///   thua khi vao lai game o Day 1.
///
/// Gan script nay len 1 object bat ky trong scene Gameplay (VD Manager).
/// </summary>
public class DayRecordTracker : MonoBehaviour
{
    [Tooltip("So lan toi da cho Firebase san sang (moi lan ~100ms).")]
    [SerializeField] private int _maxFirebaseReadyAttempts = 50;

    [Tooltip("Neu that bai (mat mang, chua dang nhap...), doi bay nhieu giay roi thu lai.")]
    [SerializeField] private float _retryDelaySeconds = 5f;

    private readonly PlayerDatabase _playerDatabase = new();

    // -1 = chua doc ky luc tu Firestore.
    private int _savedBestDay = -1;

    private bool _busy;
    private float _retryAt;

    private void Update()
    {
        if (_busy || Time.unscaledTime < _retryAt)
            return;

        if (GameDayManager.Instance == null)
            return;

        int day = GameDayManager.Instance.CurrentDay;

        if (day <= 0)
            return;

        // Da doc ky luc roi va ngay hien tai chua vuot -> khong lam gi.
        if (_savedBestDay >= 0 && day <= _savedBestDay)
            return;

        Sync(day);
    }

    private async void Sync(int day)
    {
        _busy = true;

        try
        {
            if (!await WaitForFirebaseReady())
            {
                _retryAt = Time.unscaledTime + _retryDelaySeconds;
                return;
            }

            string uid = FirebaseManager.Instance.Auth.CurrentUser?.UserId;

            if (string.IsNullOrEmpty(uid))
            {
                _retryAt = Time.unscaledTime + _retryDelaySeconds;
                return;
            }

            if (_savedBestDay < 0)
                _savedBestDay = await _playerDatabase.GetMyBestDay(uid);

            if (day > _savedBestDay)
            {
                await _playerDatabase.SaveBestDay(uid, day);
                _savedBestDay = day;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DayRecordTracker] Sync that bai: {e}");
            _retryAt = Time.unscaledTime + _retryDelaySeconds;
        }
        finally
        {
            _busy = false;
        }
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