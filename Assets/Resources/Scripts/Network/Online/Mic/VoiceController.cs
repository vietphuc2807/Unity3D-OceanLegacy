using UnityEngine;
using Photon.Voice.Unity;
using Fusion;

public class VoiceController : NetworkBehaviour
{
    private Recorder _recorder;

    // Trạng thái mic mong muốn, giữ nguyên khi chuyển scene (Lobby -> Gameplay...)
    public static bool DesiredMicState { get; private set; } = false;

    public override void Spawned()
    {
        _recorder = GetComponent<Recorder>();

        if (_recorder == null)
        {
            Debug.LogWarning("Không tìm thấy Recorder trên Player.");
            return;
        }

        if (HasInputAuthority)
        {
            // Áp dụng lại trạng thái mic đã lưu từ trước (lobby hoặc scene trước đó)
            _recorder.TransmitEnabled = DesiredMicState;

            MicToggleUI.Instance?.SetVoiceController(this);
            MicToggleUI.Instance?.SyncIcon(DesiredMicState);
        }
        else
        {
            _recorder.TransmitEnabled = false;
        }
    }

    public void SetMicActive(bool active)
    {
        if (!HasInputAuthority || _recorder == null)
            return;

        _recorder.TransmitEnabled = active;
        DesiredMicState = active; // lưu lại để scene sau dùng
    }

    public bool IsMicActive => _recorder != null && _recorder.TransmitEnabled;
}