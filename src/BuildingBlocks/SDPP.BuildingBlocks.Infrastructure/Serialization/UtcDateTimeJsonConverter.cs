using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SDPP.BuildingBlocks.Infrastructure.Serialization;

/// <summary>
/// Every DateTime in this platform is UTC by convention (the "...AtUtc" naming convention used
/// everywhere), but EF Core materializes SQL Server datetime2 columns with Kind=Unspecified — SQL
/// Server has no concept of timezone. System.Text.Json's default DateTime serialization only adds
/// the "Z" (UTC) suffix when Kind is explicitly Utc, so any value read back from the database was
/// serialized to JSON WITHOUT a timezone designator (e.g. "2026-08-13T17:26:17.895"). The browser's
/// `new Date(...)` then parses that ambiguous string as LOCAL time in the viewer's own OS timezone
/// instead of UTC — silently corrupting every timestamp shown anywhere in the frontend (evidence
/// timestamps, audit trails, dashboards) by however many hours that viewer's timezone differs from
/// UTC. This converter forces Kind=Utc before writing, guaranteeing the "Z" suffix always appears —
/// see the ContractPeriodJsonConverter registration in each Api's Program.cs.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
    }
}

/// <summary>Nullable counterpart — every "...AtUtc" property that can be null (SentAtUtc,
/// ViewedAtUtc, SignedAtUtc, etc.) uses DateTime?, not DateTime, so both converters must be
/// registered together.</summary>
public sealed class UtcNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        var value = reader.GetDateTime();
        return DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        var utc = DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
    }
}
