using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphicsSettingsTab : MonoBehaviour
{
    private const string KeyFullScreen = "Gfx_FullScreen";
    private const string KeyWidth = "Gfx_Width";
    private const string KeyHeight = "Gfx_Height";

    // Ti le khung hinh cho phep: 16:9
    private const float TargetAspect = 16f / 9f;
    private const float AspectTolerance = 0.01f; // cho phep 1366x768, 1360x768...

    // true: khong cho chon lon hon man hinh. false: hien tat ca muc chuan.
    private static readonly bool LimitToMonitor = false;

    // Danh sach 16:9 chuan.
    private static readonly Vector2Int[] StandardResolutions =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160),
    };

    [Header("Full Screen")]
    [SerializeField] private Button _fullScreenLeft;
    [SerializeField] private Button _fullScreenRight;
    [SerializeField] private TextMeshProUGUI _fullScreenValueText;

    [Header("Resolution")]
    [SerializeField] private Button _resolutionLeft;
    [SerializeField] private Button _resolutionRight;
    [SerializeField] private TextMeshProUGUI _resolutionValueText;

    private readonly List<Vector2Int> _resolutions = new();
    private int _resIndex;
    private bool _fullScreen;

    // ============================================================
    // AP DUNG SETTING DA LUU NGAY KHI MO GAME
    // (chay 1 lan, khong can panel nao ton tai)
    // ============================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedOnStart()
    {
        List<Vector2Int> list = GetResolutions16x9();

        // Lan chay dau tien: mac dinh Full Screen ON, chon muc 16:9 gan man hinh nhat.
        if (!PlayerPrefs.HasKey(KeyWidth))
        {
            Resolution cur = Screen.currentResolution;
            Vector2Int best = list[FindClosestIndex(list, cur.width, cur.height)];
            Screen.SetResolution(best.x, best.y, ToMode(true));
            return;
        }

        bool fs = PlayerPrefs.GetInt(KeyFullScreen, 1) == 1;
        int w = PlayerPrefs.GetInt(KeyWidth);
        int h = PlayerPrefs.GetInt(KeyHeight);

        // Neu gia tri cu da luu khong phai 16:9 -> doi sang muc 16:9 gan nhat.
        Vector2Int res = list[FindClosestIndex(list, w, h)];

        Screen.SetResolution(res.x, res.y, ToMode(fs));
    }

    private static FullScreenMode ToMode(bool fullScreen)
    {
        return fullScreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        BuildResolutionList();

        _fullScreenLeft.onClick.AddListener(ToggleFullScreen);
        _fullScreenRight.onClick.AddListener(ToggleFullScreen);

        _resolutionLeft.onClick.AddListener(() => ChangeResolution(-1));
        _resolutionRight.onClick.AddListener(() => ChangeResolution(+1));
    }

    private void OnEnable()
    {
        // Da luu -> dung gia tri da luu. Chua luu -> mac dinh ON.
        // (Khong doc Screen.fullScreenMode vi trong Editor luon la Windowed.)
        _fullScreen = PlayerPrefs.GetInt(KeyFullScreen, 1) == 1;

        if (PlayerPrefs.HasKey(KeyWidth))
        {
            _resIndex = FindClosestIndex(
                _resolutions,
                PlayerPrefs.GetInt(KeyWidth),
                PlayerPrefs.GetInt(KeyHeight));
        }
        else
        {
            Resolution cur = Screen.currentResolution;
            _resIndex = FindClosestIndex(_resolutions, cur.width, cur.height);
        }

        RefreshTexts();
    }

    // ============================================================
    // RESOLUTION LIST (CHI 16:9)
    // ============================================================

    private void BuildResolutionList()
    {
        _resolutions.Clear();
        _resolutions.AddRange(GetResolutions16x9());
    }

    private static bool IsSixteenByNine(int w, int h)
    {
        if (h <= 0) return false;
        return Mathf.Abs((float)w / h - TargetAspect) < AspectTolerance;
    }

    private static List<Vector2Int> GetResolutions16x9()
    {
        var list = new List<Vector2Int>();

        // Do phan giai lon nhat man hinh ho tro.
        int maxW = 0, maxH = 0;
        foreach (Resolution r in Screen.resolutions)
        {
            if (r.width > maxW) { maxW = r.width; maxH = r.height; }
        }

        // 1) Muc chuan 16:9
        foreach (Vector2Int v in StandardResolutions)
        {
            if (LimitToMonitor && maxW > 0 && (v.x > maxW || v.y > maxH))
                continue;

            list.Add(v);
        }

        // 2) Them cac muc 16:9 khac he thong bao ve (VD 1360x768)
        foreach (Resolution r in Screen.resolutions)
        {
            if (!IsSixteenByNine(r.width, r.height))
                continue;

            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v))
                list.Add(v);
        }

        // Phong truong hop rong.
        if (list.Count == 0)
            list.Add(new Vector2Int(1920, 1080));

        // Sap xep tu nho den lon.
        list.Sort((a, b) => a.x.CompareTo(b.x));
        return list;
    }

    // Tim muc gan nhat theo chieu rong (khop chinh xac thi lay luon).
    private static int FindClosestIndex(List<Vector2Int> list, int w, int h)
    {
        int best = 0;
        int bestDiff = int.MaxValue;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].x == w && list[i].y == h)
                return i;

            int diff = Mathf.Abs(list[i].x - w);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                best = i;
            }
        }

        return best;
    }

    // ============================================================
    // ACTIONS
    // ============================================================

    private void ToggleFullScreen()
    {
        _fullScreen = !_fullScreen;
        Apply();
    }

    private void ChangeResolution(int direction)
    {
        int count = _resolutions.Count;

        // Quay vong: het danh sach thi quay lai dau.
        _resIndex = (_resIndex + direction + count) % count;

        Apply();
    }

    private void Apply()
    {
        Vector2Int res = _resolutions[_resIndex];

        Screen.SetResolution(res.x, res.y, ToMode(_fullScreen));

        PlayerPrefs.SetInt(KeyFullScreen, _fullScreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyWidth, res.x);
        PlayerPrefs.SetInt(KeyHeight, res.y);
        PlayerPrefs.Save();

        RefreshTexts();
    }

    private void RefreshTexts()
    {
        if (_fullScreenValueText != null)
            _fullScreenValueText.text = _fullScreen ? "ON" : "OFF";

        if (_resolutionValueText != null)
        {
            Vector2Int r = _resolutions[_resIndex];
            _resolutionValueText.text = $"{r.x} x {r.y}";
        }
    }
}