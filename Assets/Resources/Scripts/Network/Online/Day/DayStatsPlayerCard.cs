using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gan len Prefab "1 card thong ke cua 1 nguoi choi" (icon + ten + so
/// tien kiem duoc trong ngay). DayStatsChartUI se Instantiate script
/// nay - moi nguoi choi CO giao dich trong ngay se co dung 1 card, cac
/// card tu canh deu trong khung nho HorizontalLayoutGroup (xem huong
/// dan setup / DayStatsChartUI.cs).
/// </summary>
public class DayStatsPlayerCard : MonoBehaviour
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _moneyText;

    public Image IconImage => _iconImage;
    public TextMeshProUGUI NameText => _nameText;
    public TextMeshProUGUI MoneyText => _moneyText;
}