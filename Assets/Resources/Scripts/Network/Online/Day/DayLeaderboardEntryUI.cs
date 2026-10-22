using TMPro;
using UnityEngine;

/// <summary>
/// 1 dong xep hang trong tab DAY: thu hang, ten nguoi choi, so ngay
/// ky luc. Duoc MuseumLeaderboardUI Instantiate hang loat.
/// </summary>
public class DayLeaderboardEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _dayText;

    public void SetData(int rank, string playerName, int bestDay)
    {
        if (_rankText != null)
            _rankText.text = rank.ToString();

        if (_nameText != null)
            _nameText.text = playerName;

        if (_dayText != null)
            _dayText.text = bestDay.ToString();
    }
}