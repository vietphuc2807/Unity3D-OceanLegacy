using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoHomeLoadingPanelUI : MonoBehaviour
{
    public static GoHomeLoadingPanelUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _loadingText;

    [Header("Canvas")]
    [Tooltip(
        "Canvas RIÊNG gắn trên chính object này (không dùng chung Canvas " +
        "với UI khác). Nếu để trống sẽ tự GetComponent/GetComponentInParent."
    )]
    [SerializeField] private Canvas _canvas;

    [Tooltip(
        "Sorting order ép panel này LUÔN vẽ đè lên trên mọi Canvas khác, " +
        "ở MỌI scene, kể cả sau nhiều lần load lại scene. Set 1 con số " +
        "rất cao để chắc chắn không bị Canvas nào của scene mới đè lên."
    )]
    [SerializeField] private int _forcedSortingOrder = 32760;

    [Header("Timing")]
    [SerializeField] private float _fadeInDuration = 0.6f;
    [SerializeField] private float _holdDuration = 1.5f;
    [SerializeField] private float _fadeOutDuration = 0.6f;
    [SerializeField] private float _dotIntervalSeconds = 0.4f;

    [Header("Target Scene")]
    [SerializeField] private string _museumSceneName = "S-Museum";

    private Coroutine _routine;

    private void Awake()
    {
        // QUAN TRỌNG: panel này phải sống sót qua lúc Gameplay bị unload,
        // giống pattern FusionRoomManager / GameplayLoadingOverlay /
        // GameDayIntroPanel - nếu không, SceneManager.LoadScene(Single)
        // sẽ hủy luôn object này giữa chừng, coroutine chết theo, và
        // panel biến mất sớm trước khi Museum load xong.
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // ------------------------------------------------------
        // FIX: ép Canvas của panel này luôn ở sortingOrder rất cao.
        //
        // Vì object này DontDestroyOnLoad, nó sống xuyên suốt nhiều lần
        // LoadScene(Single) khác nhau (Lobby -> Gameplay -> Museum ->
        // Lobby -> Gameplay -> ...). Ở lần chơi ĐẦU TIÊN, Canvas của nó
        // là canvas "mới nhất" nên tự nhiên vẽ đè lên trên. Nhưng từ lần
        // load scene THỨ HAI trở đi, các Canvas của Lobby/Gameplay MỚI
        // được tạo SAU nó, và với sortingOrder mặc định bằng nhau, Unity
        // vẽ canvas được tạo/kích hoạt sau lên TRÊN -> panel loading bị
        // các Canvas mới của scene sau đè khuất phía dưới. Về mặt LOGIC
        // (fade, chờ LeaveRoom, chuyển scene) panel vẫn chạy đúng, chỉ
        // là người chơi không còn NHÌN THẤY nó nữa -> cảm giác như "qua
        // thẳng scene mới, không có loading".
        //
        // Set overrideSorting + sortingOrder rất cao ở đây đảm bảo panel
        // luôn nằm trên cùng bất kể scene nào load sau nó.
        // ------------------------------------------------------
        if (_canvas == null)
            _canvas = GetComponent<Canvas>();

        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>(true);

        if (_canvas != null)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _forcedSortingOrder;
        }
        else
        {
            Debug.LogWarning(
                "[GoHomeLoadingPanelUI] Không tìm thấy Canvas nào để ép " +
                "sortingOrder. Panel có thể bị Canvas của scene sau đè " +
                "khuất. Hãy gắn Canvas riêng cho object này (xem hướng " +
                "dẫn setup)."
            );
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show()
    {
        if (_routine != null)
            return;

        gameObject.SetActive(true);

        // Đảm bảo panel luôn nằm trên cùng danh sách sibling Canvas của
        // nó (phòng trường hợp Unity vẫn dùng tie-break theo hierarchy
        // trong 1 vài edge-case) - vô hại nếu Canvas đã overrideSorting.
        if (_canvas != null)
            _canvas.transform.SetAsLastSibling();

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
        }

        _routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        // ------------------------------------------------------
        // FADE IN
        // ------------------------------------------------------
        yield return Fade(0f, 1f, _fadeInDuration);

        // ------------------------------------------------------
        // HOLD - "Loading" chạy 3 chấm
        // ------------------------------------------------------
        Coroutine dotsRoutine = StartCoroutine(AnimateDots());
        yield return new WaitForSeconds(_holdDuration);

        // ------------------------------------------------------
        // KHÔNG fade out ở đây. Giữ nguyên alpha = 1 (che kín màn
        // hình) trong suốt lúc Shutdown network + load scene Museum,
        // để không có khoảnh khắc nào lộ ra scene Gameplay cũ.
        // ------------------------------------------------------

        bool museumSceneLoaded = false;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == _museumSceneName)
                museumSceneLoaded = true;
        }

        // Đăng ký TRƯỚC khi gọi LeaveRoom, vì SceneManager.LoadScene
        // (đồng bộ) có thể bắn sceneLoaded ngay trong lúc gọi.
        SceneManager.sceneLoaded += OnSceneLoaded;

        Task leaveTask = FusionRoomManager.Instance?.LeaveRoom(_museumSceneName);

        // Chờ CẢ HAI: task LeaveRoom (bao gồm await Shutdown() mạng)
        // VÀ sự kiện scene Museum đã thực sự active.
        while ((leaveTask != null && !leaveTask.IsCompleted) || !museumSceneLoaded)
        {
            yield return null;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (dotsRoutine != null)
            StopCoroutine(dotsRoutine);

        // Đợi thêm 1 frame để Awake()/Start() của scene Museum mới
        // (nếu có UI/logic riêng) kịp chạy xong trước khi panel biến mất.
        yield return null;

        // ------------------------------------------------------
        // FADE OUT - chỉ chạy SAU KHI Museum đã load xong hoàn toàn.
        // ------------------------------------------------------
        yield return Fade(1f, 0f, _fadeOutDuration);

        gameObject.SetActive(false);
        _routine = null;
    }

    private IEnumerator AnimateDots()
    {
        if (_loadingText == null)
            yield break;

        int dotCount = 0;

        while (true)
        {
            dotCount = (dotCount + 1) % 4;
            _loadingText.text = "Loading" + new string('.', dotCount);
            yield return new WaitForSeconds(_dotIntervalSeconds);
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (_canvasGroup == null)
            yield break;

        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }

        _canvasGroup.alpha = to;
    }
}