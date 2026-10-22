using UnityEngine;

/// <summary>
/// Implement interface nay tren bat ky component nao (VD component quan
/// ly avatar/skin cua nguoi choi da co san trong project) neu muon
/// DayStatsChartUI hien ICON RIENG cho tung nguoi choi trong Stats
/// Panel, thay vi luon dung chung 1 icon mac dinh cho tat ca.
///
/// Neu KHONG gan gi vao truong _iconProviderBehaviour cua
/// DayStatsChartUI, moi nguoi choi se dung chung _defaultPlayerIcon.
/// </summary>
public interface IPlayerStatsIconProvider
{
    /// <summary>Tra ve icon cua nguoi choi ten playerName, hoac null neu khong tim thay.</summary>
    Sprite GetIconForPlayer(string playerName);
}
