using UnityEngine;

/// <summary>
/// Class cha cho cac lop am thanh nen chay theo DONG HO TRONG GAME
/// (GameDayManager.GetCurrentHour24()). Chim hot, tieng ve, dom/ech...
/// chi can ke thua class nay va khai bao Clip + duong cong mac dinh.
///
/// DIEM LAM CHO "MUOT":
/// - Volume theo duong cong GIO GAME (curve da duoc lam muot tangent).
/// - Lop lam muot (_fadeInSeconds / _fadeOutSeconds) khi vao ngay, quay lai
///   sau panel DAY, hoac load giua ngay.
/// - _perceptualFade: binh phuong he so volume cho duoi fade nghe tu nhien
///   hon, crossfade giua 2 lop khong bi "lom" am luong.
/// - _randomStartTime: moi lan vao game clip bat dau o vi tri ngau nhien.
/// - Im lang hoan toan thi Pause de nhe CPU.
///
/// HET GIO (7h toi) KHONG con tat am thanh nua: am thanh van chay theo
/// curve (giu nguyen gia tri cuoi) cho toi khi nguoi choi DONG CUA va
/// qua ngay moi (EvaluateDayEnd -> Day Panel bat dau chuyen). Luc do am
/// thanh fade out, roi clip duoc RESET ve dau de ngay moi phat lai tu
/// giay 0.
/// </summary>
public abstract class GameDayAmbienceLoop : MonoBehaviour
{
    [Header("Audio")]
    [Range(0f, 1f)]
    [Tooltip("Do to toi da (luc duong cong = 1).")]
    [SerializeField] protected float _volume = 0.8f;

    [Tooltip("BAT: nhan them voi thanh Music trong Settings.")]
    [SerializeField] protected bool _followMusicVolume = true;

    [Tooltip("BAT: fade theo thang cam nhan (binh phuong he so volume) - " +
             "nghe muot va tu nhien hon fade tuyen tinh.")]
    [SerializeField] protected bool _perceptualFade = true;

    [Tooltip("BAT: moi lan bat dau phat tu 1 vi tri ngau nhien trong clip.")]
    [SerializeField] protected bool _randomStartTime = true;

    [Header("Volume theo gio trong game")]
    [Tooltip("Truc X = GIO (24h, VD 7 = 7h sang, 9.5 = 9h30). Truc Y = do " +
             "to 0..1. Neu curve cu trong Inspector khong dung y, bam dau " +
             "3 cham cua component > 'Reset Curve To Default'.")]
    [SerializeField] protected AnimationCurve _volumeOverDay;

    [Header("Fade luc vao ngay / chuyen ngay")]
    [Tooltip("Cho bao nhieu giay SAU KHI panel DAY tat roi moi bat dau " +
             "fade in.")]
    [SerializeField] protected float _startDelaySeconds = 1.5f;

    [Tooltip("Thoi gian lam muot khi am thanh TANG (vao ngay, load giua " +
             "ngay). Curve da tu fade theo gio, nen day chi la lop lam " +
             "muot them.")]
    [SerializeField] protected float _fadeInSeconds = 4f;

    [Tooltip("Thoi gian lam muot khi am thanh GIAM (chuyen ngay). Nen de " +
             "khoang 1-1.5s de am thanh tat han trong luc man hinh dang " +
             "fade den.")]
    [SerializeField] protected float _fadeOutSeconds = 2f;

    [Tooltip("BAT: khi chuyen ngay, sau khi am thanh tat han thi dua clip " +
             "ve GIAY 0 de ngay moi phat lai tu dau.")]
    [SerializeField] protected bool _restartOnNewDay = true;

    private AudioSource _source;
    private float _currentFactor;
    private float _delayLeft;

    // True neu clip da thuc su nghe duoc tu lan reset truoc. Dung de
    // KHONG reset khi chua tung phat (giu vi tri ngau nhien luc moi vao game).
    private bool _wasAudibleSinceRestart;


    // ============================================================
    // CLASS CON PHAI CUNG CAP
    // ============================================================

    protected abstract AudioClip Clip { get; }

    protected abstract AnimationCurve CreateDefaultCurve();


    // ============================================================
    // UNITY
    // ============================================================

    private void Reset()
    {
        _volumeOverDay = CreateDefaultCurve();
    }

    [ContextMenu("Reset Curve To Default")]
    private void ResetCurveToDefault()
    {
        _volumeOverDay = CreateDefaultCurve();
    }

    private void Start()
    {
        if (Clip == null)
        {
            enabled = false;
            return;
        }

        if (_volumeOverDay == null || _volumeOverDay.length == 0)
            _volumeOverDay = CreateDefaultCurve();

        _source = gameObject.AddComponent<AudioSource>();
        _source.clip = Clip;
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f; // 2D
        _source.volume = 0f;

        if (_randomStartTime && Clip.length > 1f)
            _source.time = Random.Range(0f, Clip.length - 0.5f);

        _source.Play();
        _source.Pause();
    }

    private void Update()
    {
        if (_source == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float target = 0f;

        bool blocked = IsBlocked();

        if (blocked)
        {
            // Dang chuyen ngay: ha am, dat lai thoi gian cho.
            _delayLeft = _startDelaySeconds;
        }
        else if (_delayLeft > 0f)
        {
            _delayLeft -= dt;
        }
        else
        {
            target = GetCurveFactor();
        }

        float seconds =
            target > _currentFactor
                ? _fadeInSeconds
                : _fadeOutSeconds;

        float step =
            seconds <= 0f
                ? 1f
                : dt / seconds;

        _currentFactor = Mathf.MoveTowards(_currentFactor, target, step);

        float shaped =
            _perceptualFade
                ? _currentFactor * _currentFactor
                : _currentFactor;

        float musicFactor =
            _followMusicVolume && SoundManager.Instance != null
                ? SoundManager.Instance.GetMusicVolume()
                : 1f;

        _source.volume = shaped * _volume * musicFactor;

        if (_currentFactor > 0.001f)
            _wasAudibleSinceRestart = true;

        // Im lang hoan toan thi tam dung, co am thanh thi chay lai.
        if (_currentFactor <= 0.001f)
        {
            if (_source.isPlaying)
                _source.Pause();

            // Da tat han VI CHUYEN NGAY -> dua clip ve dau cho ngay moi.
            if (_restartOnNewDay && blocked && _wasAudibleSinceRestart)
            {
                _source.time = 0f;
                _wasAudibleSinceRestart = false;
            }
        }
        else if (!_source.isPlaying)
        {
            _source.UnPause();
        }
    }


    // ============================================================
    // HELPERS
    // ============================================================

    /// <summary>
    /// True khi chua co GameDayManager hoac NGAY MOI DANG CHUYEN (da dong
    /// cua, dang cho/hien Stats Panel hoac Day Panel).
    ///
    /// CO CHU Y KHONG dung GameDayManager.IsDayOver / IsWaitingForPlayersHome:
    /// het gio 7h toi van phai giu am thanh cho toi khi nguoi choi dong cua.
    /// </summary>
    private bool IsBlocked()
    {
        GameDayManager mgr = GameDayManager.Instance;

        if (mgr == null)
            return true;

        // Day Panel dang hien (moi may, ke ca Client).
        if (GameDayIntroPanel.Instance != null &&
            GameDayIntroPanel.Instance.IsVisible)
        {
            return true;
        }

        // Host: da dong cua, dang cho panel den de commit ngay moi.
        if (mgr.IsWaitingForDayPanel)
            return true;

        // Stats Panel dang cho moi nguoi bam E (chi khi bat
        // _showStatsPanelOnSuccess). Networked nen Client cung thay.
        if (mgr.IsWaitingForStatsAck)
            return true;

        return false;
    }

    private float GetCurveFactor()
    {
        float hour = GameDayManager.Instance.GetCurrentHour24();

        return Mathf.Clamp01(_volumeOverDay.Evaluate(hour));
    }

    /// <summary>
    /// Tao curve tu cac diem (gio, volume) voi tangent da lam muot:
    /// - Diem cuc tri (dinh/day/cao nguyen): tangent = 0 -> khong vot lo.
    /// - Diem o giua doan doc: SmoothTangents -> duong cong lien tuc.
    /// </summary>
    protected static AnimationCurve BuildSmoothCurve(params Vector2[] points)
    {
        AnimationCurve curve = new AnimationCurve();

        foreach (Vector2 p in points)
            curve.AddKey(new Keyframe(p.x, p.y));

        int last = curve.length - 1;

        for (int i = 0; i <= last; i++)
        {
            float v = curve[i].value;
            float prev = i > 0 ? curve[i - 1].value : v;
            float next = i < last ? curve[i + 1].value : v;

            bool extremum =
                (v >= prev && v >= next) ||
                (v <= prev && v <= next);

            if (extremum)
            {
                Keyframe k = curve[i];
                k.inTangent = 0f;
                k.outTangent = 0f;
                curve.MoveKey(i, k);
            }
            else
            {
                curve.SmoothTangents(i, 0f);
            }
        }

        return curve;
    }
}