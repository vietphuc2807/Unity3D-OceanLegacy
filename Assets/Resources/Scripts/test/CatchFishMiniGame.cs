// using System;
// using UnityEngine;
// using UnityEngine.UI;

// public class CatchFishMinigame : MonoBehaviour
// {
//     [Header("References")]
//     [SerializeField] private RectTransform _bar;
//     [SerializeField] private RectTransform _fish;
//     [SerializeField] private RectTransform _controlZone;
//     [SerializeField] private Image _controlZoneImage;
//     [SerializeField] private Image _progressFill;

//     [Header("Control Zone (Dễ điều khiển)")]
//     [SerializeField] private float _riseForce = 3500f;
//     [SerializeField] private float _gravity = 2800f;
//     [SerializeField] private float _maxSpeed = 260f;

//     [Header("Fish (Mượt + khó đoán)")]
//     [SerializeField] private float _slowMin = 35f;
//     [SerializeField] private float _slowMax = 50f;

//     [SerializeField] private float _normalMin = 60f;
//     [SerializeField] private float _normalMax = 90f;

//     [SerializeField] private float _dartMin = 110f;
//     [SerializeField] private float _dartMax = 145f;

//     [SerializeField] private float _speedSmooth = 4f;

//     [Header("Progress")]
//     [SerializeField] private float _gainRate = 0.32f;
//     [SerializeField] private float _loseRate = 0.20f;

//     [Header("Lose Condition")]
//     [SerializeField] private float _emptyLoseDelay = 1.5f;

//     [Header("Zone Colors")]
//     [SerializeField] private Color _goodColor = new Color(0.13f, 0.77f, 0.37f);
//     [SerializeField] private Color _midColor = new Color(0.96f, 0.62f, 0.07f);
//     [SerializeField] private Color _badColor = new Color(0.94f, 0.27f, 0.27f);
//     [SerializeField] private Color _outsideColor = new Color(0.50f, 0.11f, 0.11f);

//     private float _zoneVelocity;
//     private float _halfBar;

//     private float _fishTimer;
//     private int _fishDir;

//     private float _currentFishSpeed;
//     private float _targetFishSpeed;

//     private float _progress;
//     private float _emptyTimer;

//     private bool _isActive;

//     private Action<bool> _onResult;

//     private void Update()
//     {
//         if (!_isActive)
//             return;

//         UpdateControlZone();
//         UpdateFish();
//         UpdateProgress();
//     }

//     public void StartMinigame(Action<bool> onResult)
//     {
//         _onResult = onResult;
//         _isActive = true;

//         _halfBar = _bar.rect.height * 0.5f;

//         _progress = 0f;
//         _emptyTimer = 0f;

//         _progressFill.fillAmount = 0f;

//         _controlZone.anchoredPosition = Vector2.zero;
//         _fish.anchoredPosition = Vector2.zero;

//         _zoneVelocity = 0f;

//         // Vùng xanh cao hơn để dễ chơi
//         _controlZone.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 90f);

//         _fishDir = UnityEngine.Random.value > 0.5f ? 1 : -1;
//         _fishTimer = 0.5f;

//         _currentFishSpeed = 45f;
//         _targetFishSpeed = 45f;

//         _controlZoneImage.color = _midColor;
//     }

//     private void UpdateControlZone()
//     {
//         bool hold = Input.GetMouseButton(0);

//         // Điều khiển trực tiếp, không trượt lâu
//         if (hold)
//             _zoneVelocity = Mathf.MoveTowards(_zoneVelocity, _maxSpeed, _riseForce * Time.deltaTime);
//         else
//             _zoneVelocity = Mathf.MoveTowards(_zoneVelocity, -_maxSpeed, _gravity * Time.deltaTime);

//         Vector2 pos = _controlZone.anchoredPosition;
//         pos.y += _zoneVelocity * Time.deltaTime;

//         float halfZone = _controlZone.rect.height * 0.5f;

//         pos.y = Mathf.Clamp(pos.y, -_halfBar + halfZone, _halfBar - halfZone);

//         _controlZone.anchoredPosition = pos;
//     }

//     private void UpdateFish()
//     {
//         _fishTimer -= Time.deltaTime;

//         if (_fishTimer <= 0f)
//         {
//             // Đổi hướng ngẫu nhiên
//             _fishDir = UnityEngine.Random.value > 0.5f ? 1 : -1;

//             float roll = UnityEngine.Random.value;

//             if (roll < 0.55f)
//             {
//                 // Chậm
//                 _targetFishSpeed = UnityEngine.Random.Range(_slowMin, _slowMax);
//                 _fishTimer = UnityEngine.Random.Range(0.9f, 1.5f);
//             }
//             else if (roll < 0.90f)
//             {
//                 // Bình thường
//                 _targetFishSpeed = UnityEngine.Random.Range(_normalMin, _normalMax);
//                 _fishTimer = UnityEngine.Random.Range(0.5f, 1.0f);
//             }
//             else
//             {
//                 // Lao nhanh nhẹ
//                 _targetFishSpeed = UnityEngine.Random.Range(_dartMin, _dartMax);
//                 _fishTimer = UnityEngine.Random.Range(0.25f, 0.45f);
//             }
//         }

//         // Làm mượt tốc độ cá
//         _currentFishSpeed = Mathf.Lerp(
//             _currentFishSpeed,
//             _targetFishSpeed,
//             _speedSmooth * Time.deltaTime
//         );

//         Vector2 pos = _fish.anchoredPosition;

//         pos.y += _fishDir * _currentFishSpeed * Time.deltaTime;

//         // Rung rất nhẹ cho tự nhiên
//         pos.y += Mathf.Sin(Time.time * 8f) * 0.15f;

//         float halfFish = _fish.rect.height * 0.5f;

//         if (pos.y >= _halfBar - halfFish)
//         {
//             pos.y = _halfBar - halfFish;
//             _fishDir = -1;
//             _targetFishSpeed = UnityEngine.Random.Range(70f, 120f);
//         }
//         else if (pos.y <= -_halfBar + halfFish)
//         {
//             pos.y = -_halfBar + halfFish;
//             _fishDir = 1;
//             _targetFishSpeed = UnityEngine.Random.Range(70f, 120f);
//         }

//         _fish.anchoredPosition = pos;
//     }

//     private void UpdateProgress()
//     {
//         float fishY = _fish.anchoredPosition.y;
//         float zoneY = _controlZone.anchoredPosition.y;

//         float halfZone = _controlZone.rect.height * 0.5f;

//         float distance = Mathf.Abs(fishY - zoneY);

//         bool inside = distance <= halfZone;

//         if (inside)
//         {
//             // Cá càng gần tâm càng nhiều điểm
//             float centered = 1f - (distance / halfZone);

//             // Mép 20%, giữa 100%
//             float multiplier = Mathf.Lerp(0.2f, 1f, centered);

//             _progress += _gainRate * multiplier * Time.deltaTime;

//             // Đổi màu vùng điều khiển
//             if (centered > 0.7f)
//                 _controlZoneImage.color = _goodColor;
//             else if (centered > 0.35f)
//                 _controlZoneImage.color = _midColor;
//             else
//                 _controlZoneImage.color = _badColor;
//         }
//         else
//         {
//             _progress -= _loseRate * Time.deltaTime;
//             _controlZoneImage.color = _outsideColor;
//         }

//         _progress = Mathf.Clamp01(_progress);

//         _progressFill.fillAmount = _progress;

//         // Thắng
//         if (_progress >= 1f)
//         {
//             Finish(true);
//             return;
//         }

//         // Thua nếu thanh rỗng quá lâu
//         if (_progress <= 0f)
//         {
//             _emptyTimer += Time.deltaTime;

//             if (_emptyTimer >= _emptyLoseDelay)
//             {
//                 Finish(false);
//             }
//         }
//         else
//         {
//             _emptyTimer = 0f;
//         }
//     }

//     private void Finish(bool success)
//     {
//         _isActive = false;

//         _onResult?.Invoke(success);
//     }
// }