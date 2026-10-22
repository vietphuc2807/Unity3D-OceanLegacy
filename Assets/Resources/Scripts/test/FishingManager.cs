// using System.Collections;
// using UnityEngine;

// public class FishingManager : MonoBehaviour
// {
//     private enum State
//     {
//         Idle,
//         Waiting,
//         Hooking,
//         Catching
//     }

//     [Header("References")]
//     [SerializeField] private HookFishMinigame _hookMinigame;
//     [SerializeField] private CatchFishMinigame _catchMinigame;

//     [SerializeField] private GameObject _hookPanel;
//     [SerializeField] private GameObject _catchPanel;

//     [Header("Settings")]
//     [SerializeField] private float _minWaitTime = 3f;
//     [SerializeField] private float _maxWaitTime = 10f;

//     private State _state;
//     private Coroutine _routine;

//     private void Start()
//     {
//         StartFishing();
//     }

//     public void StartFishing()
//     {
//         if (_state != State.Idle)
//             return;

//         _hookPanel.SetActive(false);
//         _catchPanel.SetActive(false);

//         StopRoutine();
//         _routine = StartCoroutine(FishingRoutine());
//     }

//     private IEnumerator FishingRoutine()
//     {
//         _state = State.Waiting;

//         Debug.Log("Đang thả câu, chờ cá cắn...");

//         float waitTime = Random.Range(_minWaitTime, _maxWaitTime);
//         yield return new WaitForSeconds(waitTime);

//         // Cá cắn câu
//         _state = State.Hooking;

//         Debug.Log("Cá cắn câu! Hãy click đúng lúc!");

//         _hookPanel.SetActive(true);

//         _hookMinigame.StartMinigame(OnHookResult);
//     }

//     // Kết quả minigame giật cá
//     private void OnHookResult(bool success)
//     {
//         _hookPanel.SetActive(false);

//         if (!success)
//         {
//             Debug.Log("Trượt! Cá đã chạy mất.");

//             _state = State.Idle;
//             StartFishing();
//             return;
//         }

//         Debug.Log("Giật cá thành công! Bắt đầu kéo cá...");

//         // Chuyển sang minigame kéo cá
//         _state = State.Catching;

//         _catchPanel.SetActive(true);

//         _catchMinigame.StartMinigame(OnCatchResult);
//     }

//     // Kết quả minigame kéo cá
//     private void OnCatchResult(bool success)
//     {
//         _catchPanel.SetActive(false);

//         if (success)
//         {
//             Debug.Log("BẮT ĐƯỢC CÁ!");
//         }
//         else
//         {
//             Debug.Log("Cá thoát trong lúc kéo!");
//         }

//         _state = State.Idle;

//         StartFishing();
//     }

//     private void StopRoutine()
//     {
//         if (_routine == null)
//             return;

//         StopCoroutine(_routine);
//         _routine = null;
//     }
// }