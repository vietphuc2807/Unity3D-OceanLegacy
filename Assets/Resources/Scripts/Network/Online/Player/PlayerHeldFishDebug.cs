using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// DEBUG ONLY
///
/// Test PlayerHeldFish.EquipFish() ngay trong Editor.
///
/// Chức năng:
/// 1. Tick Attach This của một FishData để cầm cá.
/// 2. Chỉ được tick một cá tại một thời điểm.
/// 3. Bỏ tick tất cả để hạ tay + ClearHeldFish().
/// 4. Kéo Size Multiplier để test kích thước.
/// 5. Hiển thị danh sách TẤT CẢ cá ở MIN SIZE + cân nặng.
/// </summary>
[RequireComponent(typeof(PlayerHeldFish))]
public class PlayerHeldFishDebug : MonoBehaviour
{
    // ============================================================
    // DEBUG FISH TOGGLE
    // ============================================================

    [System.Serializable]
    private class DebugFishToggle
    {
        public FishData Fish;
        public bool AttachThis;
    }


    [Header("Fish Debug")]

    [Tooltip(
        "Kéo các FishData muốn test vào đây. " +
        "Tick Attach This để cầm cá."
    )]
    [SerializeField]
    private List<DebugFishToggle> _debugFishToggles =
        new List<DebugFishToggle>();


    // ============================================================
    // SIZE DEBUG
    // ============================================================

    [Header("Size Debug")]

    [Range(0.1f, 5f)]

    [Tooltip(
        "Size multiplier dùng khi test cá."
    )]

    [SerializeField]
    private float _debugSizeMultiplier = 1f;


    // ============================================================
    // WEIGHT DEBUG TEXT
    // ============================================================

    [Header("Weight Debug Text")]

    [Tooltip(
        "TMP Text dùng để hiển thị tất cả cá ở MIN SIZE " +
        "và cân nặng tương ứng."
    )]

    [SerializeField]
    private TMP_Text _weightDebugText;


    [Tooltip(
        "Tự động cập nhật danh sách cân nặng mỗi frame."
    )]

    [SerializeField]
    private bool _updateWeightText = true;


    // ============================================================
    // CONSOLE DEBUG
    // ============================================================

    [Header("Console Debug")]

    [SerializeField]
    private bool _logWeightToConsole = true;


    // ============================================================
    // RUNTIME
    // ============================================================

    private PlayerHeldFish _heldFish;

    private FishData _lastToggledDebugFish;

    private FishData _pendingDebugFish;

    private bool _hasPendingDebugChange;

    private float _lastAppliedSizeMultiplier;


    // ============================================================
    // AWAKE
    // ============================================================

    private void Awake()
    {
        _heldFish =
            GetComponent<PlayerHeldFish>();
    }


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        UpdateWeightDebugText();

        LogMinSizeWeights();
    }


    // ============================================================
    // ON VALIDATE
    // ============================================================

    private void OnValidate()
    {
        if (!Application.isPlaying ||
            _debugFishToggles == null)
        {
            return;
        }


        // --------------------------------------------------------
        // Tìm cá vừa được tick
        // --------------------------------------------------------

        FishData newlyTicked = null;


        foreach (var entry in _debugFishToggles)
        {
            if (entry != null &&
                entry.Fish != null &&
                entry.AttachThis &&
                entry.Fish != _lastToggledDebugFish)
            {
                newlyTicked =
                    entry.Fish;

                break;
            }
        }


        // --------------------------------------------------------
        // Có cá mới được tick
        // --------------------------------------------------------

        if (newlyTicked != null)
        {
            foreach (var entry in _debugFishToggles)
            {
                if (entry != null &&
                    entry.Fish != newlyTicked)
                {
                    entry.AttachThis = false;
                }
            }


            _pendingDebugFish =
                newlyTicked;

            _hasPendingDebugChange =
                true;

            return;
        }


        // --------------------------------------------------------
        // Kiểm tra còn cá nào được tick không
        // --------------------------------------------------------

        bool anyTicked = false;


        foreach (var entry in _debugFishToggles)
        {
            if (entry != null &&
                entry.AttachThis)
            {
                anyTicked = true;
                break;
            }
        }


        // --------------------------------------------------------
        // Không còn cá nào
        // --------------------------------------------------------

        if (!anyTicked &&
            _lastToggledDebugFish != null)
        {
            _pendingDebugFish = null;

            _hasPendingDebugChange = true;
        }
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (_heldFish == null)
            return;


        // --------------------------------------------------------
        // Update weight text
        // --------------------------------------------------------

        if (_updateWeightText)
        {
            UpdateWeightDebugText();
        }


        // --------------------------------------------------------
        // Pending fish change
        // --------------------------------------------------------

        if (_hasPendingDebugChange)
        {
            _hasPendingDebugChange = false;

            _lastToggledDebugFish =
                _pendingDebugFish;


            if (_pendingDebugFish != null)
            {
                _heldFish.EquipFish(
                    _pendingDebugFish,
                    _debugSizeMultiplier
                );

                _lastAppliedSizeMultiplier =
                    _debugSizeMultiplier;
            }
            else
            {
                _heldFish.ClearHeldFish();
            }

            return;
        }


        // --------------------------------------------------------
        // Đang cầm cá + thay đổi size
        // --------------------------------------------------------

        if (_lastToggledDebugFish != null &&
            !Mathf.Approximately(
                _debugSizeMultiplier,
                _lastAppliedSizeMultiplier
            ))
        {
            _heldFish.EquipFish(
                _lastToggledDebugFish,
                _debugSizeMultiplier
            );

            _lastAppliedSizeMultiplier =
                _debugSizeMultiplier;
        }
    }


    // ============================================================
    // UPDATE WEIGHT DEBUG TEXT
    // ============================================================

    private void UpdateWeightDebugText()
    {
        if (_weightDebugText == null)
            return;

        if (_debugFishToggles == null ||
            _debugFishToggles.Count == 0)
        {
            _weightDebugText.text =
                "NO FISH DATA";

            return;
        }


        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();


        builder.AppendLine(
            "<b>FISH MIN SIZE WEIGHT</b>"
        );

        builder.AppendLine(
            "=========================="
        );


        foreach (var entry in _debugFishToggles)
        {
            if (entry == null ||
                entry.Fish == null)
            {
                continue;
            }


            FishData fish =
                entry.Fish;


            // ----------------------------------------------------
            // Lấy MIN size
            // ----------------------------------------------------

            float minSize =
                fish.MinSizeMultiplier;


            // ----------------------------------------------------
            // Tính KG
            // ----------------------------------------------------

            float weightKg =
                fish.GetWeightKg(
                    minSize
                );


            // ----------------------------------------------------
            // Text
            // ----------------------------------------------------

            builder.AppendLine(
                $"{fish.FishName}"
            );

            builder.AppendLine(
                $"  Min Size : {minSize:0.##}x"
            );

            builder.AppendLine(
                $"  Base Weight : {fish.BaseWeightKg:0.##} KG"
            );

            builder.AppendLine(
                $"  Weight : <b>{fish.GetWeightText(minSize)}</b>"
            );

            builder.AppendLine();
        }


        _weightDebugText.text =
            builder.ToString();
    }


    // ============================================================
    // LOG MIN SIZE WEIGHTS
    // ============================================================

    private void LogMinSizeWeights()
    {
        if (!_logWeightToConsole)
            return;

        if (_debugFishToggles == null ||
            _debugFishToggles.Count == 0)
        {
            Debug.Log(
                "[PlayerHeldFishDebug] " +
                "No FishData assigned."
            );

            return;
        }


        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();


        builder.AppendLine(
            "========== FISH MIN SIZE WEIGHT =========="
        );


        foreach (var entry in _debugFishToggles)
        {
            if (entry == null ||
                entry.Fish == null)
            {
                continue;
            }


            FishData fish =
                entry.Fish;


            float minSize =
                fish.MinSizeMultiplier;


            float weightKg =
                fish.GetWeightKg(
                    minSize
                );


            builder.AppendLine(
                $"{fish.FishName} | " +
                $"MinSize = {minSize:0.##}x | " +
                $"BaseWeight = {fish.BaseWeightKg:0.##} KG | " +
                $"Weight = {weightKg:0.##} KG"
            );
        }


        builder.AppendLine(
            "=========================================="
        );


        Debug.Log(
            builder.ToString()
        );
    }
}