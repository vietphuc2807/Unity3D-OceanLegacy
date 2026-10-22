using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Logic đếm tiền "tăng tốc dần" (giống hệt TeamMoneyUI) được tách ra
/// thành hàm dùng chung, để mọi UI đếm tiền trong game (ví team, bảng tin...)
/// đều có cùng 1 cảm giác chuyển động.
/// </summary>
public static class MoneyCountSequenceBuilder
{
    [Serializable]
    public struct Settings
    {
        public float slowStepTime;
        public float fastStepTime;
        public int accelerationStart;
        public int accelerationEnd;
        public int slowDownDistance;
        public int maxStep;
        public float largeChangeDuration;
        public int largeChangeDistance;

        public static Settings Default => new Settings
        {
            slowStepTime = 0.035f,
            fastStepTime = 0.012f,
            accelerationStart = 50,
            accelerationEnd = 500,
            slowDownDistance = 30,
            maxStep = 10,
            largeChangeDuration = 1.2f,
            largeChangeDistance = 1000
        };
    }

    /// <summary>
    /// Tạo 1 Sequence đếm từ startMoney -> targetMoney.
    /// onStep được gọi mỗi bước với giá trị hiện tại.
    /// </summary>
    public static Sequence Build(
        int startMoney,
        int targetMoney,
        Settings settings,
        Action<int> onStep)
    {
        Sequence sequence = DOTween.Sequence();

        int difference = Mathf.Abs(targetMoney - startMoney);

        if (difference == 0)
            return sequence;

        int direction = targetMoney > startMoney ? 1 : -1;

        if (difference <= settings.accelerationStart)
        {
            AddSlowCount(sequence, startMoney, direction, difference, settings, onStep);
        }
        else if (difference < settings.largeChangeDistance)
        {
            AddAdaptiveCount(sequence, startMoney, targetMoney, direction, settings, onStep);
        }
        else
        {
            AddLargeCount(sequence, startMoney, targetMoney, direction, difference, settings, onStep);
        }

        return sequence;
    }

    private static void AddSlowCount(
        Sequence sequence, int startMoney, int direction, int difference,
        Settings settings, Action<int> onStep)
    {
        for (int i = 1; i <= difference; i++)
        {
            int value = startMoney + direction * i;
            AddStep(sequence, value, settings.slowStepTime, onStep);
        }
    }

    private static void AddAdaptiveCount(
        Sequence sequence, int startMoney, int targetMoney, int direction,
        Settings settings, Action<int> onStep)
    {
        int current = startMoney;

        while (current != targetMoney)
        {
            int distanceRemaining = Mathf.Abs(targetMoney - current);

            if (distanceRemaining <= settings.slowDownDistance)
            {
                current += direction;
                AddStep(sequence, current, settings.slowStepTime, onStep);
                continue;
            }

            float acceleration = Mathf.InverseLerp(
                settings.accelerationStart,
                settings.accelerationEnd,
                Mathf.Abs(current - startMoney));

            int step = Mathf.RoundToInt(Mathf.Lerp(1f, settings.maxStep, acceleration));
            step = Mathf.Max(1, step);
            step = Mathf.Min(step, distanceRemaining);

            current += direction * step;

            float stepTime = Mathf.Lerp(settings.slowStepTime, settings.fastStepTime, acceleration);
            AddStep(sequence, current, stepTime, onStep);
        }
    }

    private static void AddLargeCount(
        Sequence sequence, int startMoney, int targetMoney, int direction, int difference,
        Settings settings, Action<int> onStep)
    {
        int current = startMoney;

        int desiredSteps = Mathf.RoundToInt(settings.largeChangeDuration / settings.fastStepTime);
        desiredSteps = Mathf.Max(20, desiredSteps);

        int step = Mathf.CeilToInt((float)difference / desiredSteps);
        step = Mathf.Max(1, step);

        while (current != targetMoney)
        {
            int distanceRemaining = Mathf.Abs(targetMoney - current);

            if (distanceRemaining <= settings.slowDownDistance)
            {
                current += direction;
                AddStep(sequence, current, settings.slowStepTime, onStep);
                continue;
            }

            float normalizedDistance = Mathf.InverseLerp(settings.slowDownDistance, difference, distanceRemaining);

            int currentStep = Mathf.RoundToInt(Mathf.Lerp(1f, step, normalizedDistance));
            currentStep = Mathf.Max(1, currentStep);
            currentStep = Mathf.Min(currentStep, distanceRemaining);

            current += direction * currentStep;

            float stepTime = Mathf.Lerp(settings.slowStepTime, settings.fastStepTime, normalizedDistance);
            AddStep(sequence, current, stepTime, onStep);
        }
    }

    private static void AddStep(Sequence sequence, int value, float duration, Action<int> onStep)
    {
        sequence.AppendInterval(duration);
        sequence.AppendCallback(() => onStep?.Invoke(value));
    }
}