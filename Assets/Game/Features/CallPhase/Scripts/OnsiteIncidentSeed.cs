using System.Globalization;
using System.Text;

public sealed class OnsiteIncidentSeed
{
    public string fireOrigin = "Unknown";
    public string hazardType = "OrdinaryCombustibles";
    public bool requiresIsolation;
    public float initialFireIntensity;
    public int initialFireCount = 1;
    public string fireSpreadPreset = "Moderate";
    public float startSmokeDensity;
    public float smokeAccumulationMultiplier = 1f;
    public string ventilationPreset = "ClosedInterior";

    public string ToDebugDisplayString()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("fireOrigin: ").Append(fireOrigin).Append('\n');
        builder.Append("hazardType: ").Append(hazardType).Append('\n');
        builder.Append("requiresIsolation: ").Append(requiresIsolation ? "true" : "false").Append('\n');
        builder.Append("initialFireIntensity: ").Append(FormatFloat(initialFireIntensity)).Append('\n');
        builder.Append("initialFireCount: ").Append(initialFireCount).Append('\n');
        builder.Append("fireSpreadPreset: ").Append(fireSpreadPreset).Append('\n');
        builder.Append("startSmokeDensity: ").Append(FormatFloat(startSmokeDensity)).Append('\n');
        builder.Append("smokeAccumulationMultiplier: ").Append(FormatFloat(smokeAccumulationMultiplier)).Append('\n');
        builder.Append("ventilationPreset: ").Append(ventilationPreset);
        return builder.ToString();
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
