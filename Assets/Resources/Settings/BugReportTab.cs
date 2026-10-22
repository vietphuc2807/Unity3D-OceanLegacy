using System;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BugReportTab : MonoBehaviour
{
    private const int MinLength = 5;
    private const int MaxLength = 1000;
    private const int CooldownSeconds = 600;   // 10 phut
    private const string KeyLastSent = "Bug_LastSentUnix";

    // Firestore ghi khi mat mang se TREO mai (cho server xac nhan)
    // -> tu dat timeout de UI khong bi ket.
    private const int TimeoutMs = 15000;

    private readonly PlayerDatabase _playerDatabase = new();

    [Header("Form")]
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private Button _submitButton;

    [Tooltip("Tuy chon: hien thong bao loi nho (rong thi bo qua).")]
    [SerializeField] private TextMeshProUGUI _statusText;

    [Header("Sent panel")]
    [Tooltip("Panel 'Report sent!' - object co gan PanelEffect.")]
    [SerializeField] private PanelEffect _sentPanel;
    [SerializeField] private Button _okButton;

    private bool _sending;

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        _input.characterLimit = MaxLength;

        _submitButton.onClick.AddListener(OnSubmit);
        _okButton.onClick.AddListener(OnOkClicked);
    }

    private void OnEnable()
    {
        _submitButton.interactable = !_sending;
        SetStatus(string.Empty);
    }

    // ============================================================
    // ACTIONS
    // ============================================================

    private async void OnSubmit()
    {
        if (_sending) return;

        string message = _input.text.Trim();

        if (message.Length < MinLength)
        {
            SetStatus($"Please describe the bug (at least {MinLength} characters).");
            return;
        }

        FirebaseManager fm = FirebaseManager.Instance;
        FirebaseUser user = (fm != null && fm.IsReady) ? fm.Auth.CurrentUser : null;

        if (user == null)
        {
            SetStatus("Please log in to send a report.");
            return;
        }

        int wait = GetRemainingCooldown();
        if (wait > 0)
        {
            SetStatus($"Please wait {FormatTime(wait)} before sending another report.");
            return;
        }

        _sending = true;
        _submitButton.interactable = false;
        SetStatus("Sending...");

        try
        {
            var report = new BugReportData(
                user.UserId,
                user.Email,
                message,
                Application.version,
                Application.platform.ToString(),
                SystemInfo.operatingSystem,
                SystemInfo.deviceModel,
                SceneManager.GetActiveScene().name);

            Task save = _playerDatabase.SaveBugReport(report);

            Task finished = await Task.WhenAny(save, Task.Delay(TimeoutMs));

            if (finished != save)
                throw new TimeoutException("Firestore write timed out.");

            await save;   // nem lai exception neu ghi that bai

            if (this == null) return;

            MarkSent();
            _input.text = string.Empty;
            SetStatus(string.Empty);
            _sentPanel.Show();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Bug report failed: " + e);

            if (this == null) return;
            SetStatus("Could not send the report. Please try again.");
        }
        finally
        {
            _sending = false;

            if (this != null)
                _submitButton.interactable = true;
        }
    }

    private void OnOkClicked()
    {
        // Khoa nut OK trong luc hieu ung Hide dang chay (tranh bam 2 lan).
        _okButton.interactable = false;

        _sentPanel.Hide(() =>
        {
            if (this != null)
                _okButton.interactable = true;
        });
    }

    // ============================================================
    // COOLDOWN (luu vao PlayerPrefs -> thoat game vao lai van con)
    // ============================================================

    private static int GetRemainingCooldown()
    {
        if (!long.TryParse(PlayerPrefs.GetString(KeyLastSent, "0"), out long last))
            return 0;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Dong ho may bi chinh lui -> bo qua cooldown.
        if (now < last) return 0;

        return (int)Math.Max(0L, CooldownSeconds - (now - last));
    }

    private static void MarkSent()
    {
        PlayerPrefs.SetString(KeyLastSent, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
    }

    private static string FormatTime(int seconds)
    {
        return seconds >= 60 ? $"{(seconds + 59) / 60} min" : $"{seconds}s";
    }

    // ============================================================
    // UI
    // ============================================================

    private void SetStatus(string text)
    {
        if (_statusText != null)
            _statusText.text = text;
    }
}