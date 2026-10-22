using Fusion;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System;

public class NetworkStatsUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text _pingText;
    [SerializeField] private TMP_Text _fpsText;

    [Header("Settings")]
    [SerializeField] private float _updateInterval = 0.5f;

    private NetworkRunner _runner;
    private float _timer;
    private int _frameCount;
    private float _fpsAccum;

    private const float FPS_GOOD = 60f;
    private const float FPS_WARN = 30f;
    private const float PING_GOOD = 70f;
    private const float PING_WARN = 150f;

    private static readonly Color ColorGood = Color.green;
    private static readonly Color ColorWarn = Color.yellow;
    private static readonly Color ColorBad  = Color.red;

    private void Start()
    {
        _runner = FindAnyObjectByType<NetworkRunner>();
    }

    private void Update()
    {
        if (_runner == null)
            _runner = FindAnyObjectByType<NetworkRunner>();

        _fpsAccum += 1f / Time.deltaTime;
        _frameCount++;
        _timer += Time.deltaTime;

        if (_timer >= _updateInterval)
        {
            UpdateFPS();
            UpdatePing();
            _timer = 0f;
        }
    }

    private void UpdateFPS()
    {
        if (_fpsText == null) return;

        float fps = _fpsAccum / _frameCount;
        _fpsAccum = 0f;
        _frameCount = 0;

        Color c = GetColorByValue(fps, FPS_GOOD, FPS_WARN, inverse: true);
        string hex = ColorUtility.ToHtmlStringRGB(c);
        _fpsText.text = $"FPS: <color=#{hex}>{fps:0}</color>";
    }

    private void UpdatePing()
    {
        if (_pingText == null) return;

        if (_runner == null || !_runner.IsRunning)
        {
            _pingText.text = "Ping: <color=#00FF00>N/A</color>";
            return;
        }

        double rtt = _runner.GetPlayerRtt(_runner.LocalPlayer) * 1000.0;
        Color c = GetColorByValue((float)rtt, PING_GOOD, PING_WARN, inverse: false);
        string hex = ColorUtility.ToHtmlStringRGB(c);
        _pingText.text = $"Ping: <color=#{hex}>{rtt:0} ms</color>";
    }

    private Color GetColorByValue(float value, float good, float warn, bool inverse)
    {
        float t;

        if (inverse)
        {
            if (value >= good) return ColorGood;
            if (value <= warn) return ColorBad;
            t = (value - warn) / (good - warn);
        }
        else
        {
            if (value <= good) return ColorGood;
            if (value >= warn) return ColorBad;
            t = 1f - (value - good) / (warn - good);
        }

        if (t > 0.5f)
            return Color.Lerp(ColorWarn, ColorGood, (t - 0.5f) * 2f);
        else
            return Color.Lerp(ColorBad, ColorWarn, t * 2f);
    }
}