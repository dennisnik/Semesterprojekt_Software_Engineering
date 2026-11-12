namespace TNWatch.Domain.Battery;

/// <summary>
/// Approximates the state of charge (SoC, %) of a 12 V lead-acid (AGM/Gel) battery from its terminal voltage
/// by piecewise-linear interpolation. This is an estimate only (see glossary "Ladezustand").
/// </summary>
public static class SocEstimator
{
    // (voltage, percent), ascending by voltage. Typical 12 V AGM rest-voltage table.
    private static readonly (double Voltage, double Percent)[] Curve =
    [
        (10.50, 0),
        (11.51, 10),
        (11.66, 20),
        (11.81, 30),
        (11.96, 40),
        (12.10, 50),
        (12.24, 60),
        (12.37, 70),
        (12.50, 80),
        (12.62, 90),
        (12.73, 100),
    ];

    /// <summary>Returns the estimated SoC in percent, rounded to an integer and clamped to 0–100.</summary>
    public static int Estimate(double voltage)
    {
        if (double.IsNaN(voltage) || voltage <= Curve[0].Voltage)
        {
            return 0;
        }

        if (voltage >= Curve[^1].Voltage)
        {
            return 100;
        }

        for (var i = 1; i < Curve.Length; i++)
        {
            var (vHigh, pHigh) = Curve[i];
            if (voltage <= vHigh)
            {
                var (vLow, pLow) = Curve[i - 1];
                var percent = pLow + ((voltage - vLow) / (vHigh - vLow) * (pHigh - pLow));
                return (int)Math.Round(percent, MidpointRounding.AwayFromZero);
            }
        }

        return 100;
    }
}
