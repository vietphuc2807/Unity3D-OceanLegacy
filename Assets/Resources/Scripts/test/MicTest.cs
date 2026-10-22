using UnityEngine;

public class MicTest : MonoBehaviour
{
    private AudioSource audioSource;
    private string micName;

    void Start()
    {
        // Liệt kê tất cả thiết bị mic tìm thấy
        foreach (var device in Microphone.devices)
        {
            Debug.Log("Mic found: " + device);
        }

        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0]; // lấy mic đầu tiên
            audioSource = gameObject.AddComponent<AudioSource>();

            // Bắt đầu ghi âm loop, 10s buffer, 44100Hz
            audioSource.clip = Microphone.Start(micName, true, 10, 44100);
            audioSource.loop = true;

            // Đợi mic khởi động xong rồi mới play
            while (!(Microphone.GetPosition(micName) > 0)) { }
            audioSource.Play();

            Debug.Log("Mic started: " + micName);
        }
        else
        {
            Debug.LogWarning("Không tìm thấy mic nào!");
        }
    }

    void OnDisable()
    {
        if (!string.IsNullOrEmpty(micName) && Microphone.IsRecording(micName))
            Microphone.End(micName);
    }
}