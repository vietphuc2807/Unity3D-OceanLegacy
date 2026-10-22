using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

[Serializable]
public struct DayStatsTransaction
{
    public int PlayerId;

    public string PlayerName;

    public int Delta;

    public DayStatsTransaction(int playerId, string playerName, int delta)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        Delta = delta;
    }
}

public class DayStatsData
{
    public int FinishedDay;

    public int Target;

    public int FishSold;

    public int Earnings;

    public int BaselineMoney;

    public List<DayStatsTransaction> Transactions =
        new List<DayStatsTransaction>();


    private const char FieldSeparator = '~';

    private const char TransactionSeparator = ';';

    private const char TransactionPairSeparator = ':';


    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return name
            .Replace(FieldSeparator, '_')
            .Replace(TransactionSeparator, '_')
            .Replace(TransactionPairSeparator, '_');
    }


    public string Serialize()
    {
        StringBuilder sb = new StringBuilder();

        sb.Append(FinishedDay.ToString(CultureInfo.InvariantCulture));
        sb.Append(FieldSeparator);

        sb.Append(Target.ToString(CultureInfo.InvariantCulture));
        sb.Append(FieldSeparator);

        sb.Append(FishSold.ToString(CultureInfo.InvariantCulture));
        sb.Append(FieldSeparator);

        sb.Append(Earnings.ToString(CultureInfo.InvariantCulture));
        sb.Append(FieldSeparator);

        sb.Append(BaselineMoney.ToString(CultureInfo.InvariantCulture));
        sb.Append(FieldSeparator);

        for (int i = 0; i < Transactions.Count; i++)
        {
            if (i > 0)
                sb.Append(TransactionSeparator);

            sb.Append(Transactions[i].PlayerId.ToString(CultureInfo.InvariantCulture));
            sb.Append(TransactionPairSeparator);
            sb.Append(Transactions[i].PlayerName);
            sb.Append(TransactionPairSeparator);
            sb.Append(Transactions[i].Delta.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }


    public static DayStatsData Deserialize(string payload)
    {
        DayStatsData data = new DayStatsData();

        if (string.IsNullOrEmpty(payload))
            return data;

        string[] fields = payload.Split(FieldSeparator);

        if (fields.Length < 5)
            return data;

        int.TryParse(
            fields[0],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out data.FinishedDay
        );

        int.TryParse(
            fields[1],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out data.Target
        );

        int.TryParse(
            fields[2],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out data.FishSold
        );

        int.TryParse(
            fields[3],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out data.Earnings
        );

        int.TryParse(
            fields[4],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out data.BaselineMoney
        );

        if (fields.Length >= 6 && !string.IsNullOrEmpty(fields[5]))
        {
            string[] entries =
                fields[5].Split(TransactionSeparator);

            foreach (string entry in entries)
            {
                if (string.IsNullOrEmpty(entry))
                    continue;

                string[] pair =
                    entry.Split(TransactionPairSeparator);

                if (pair.Length != 3)
                    continue;

                if (!int.TryParse(
                        pair[0],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int playerId))
                {
                    continue;
                }

                if (!int.TryParse(
                        pair[2],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int delta))
                {
                    continue;
                }

                data.Transactions.Add(
                    new DayStatsTransaction(playerId, pair[1], delta)
                );
            }
        }

        return data;
    }
}