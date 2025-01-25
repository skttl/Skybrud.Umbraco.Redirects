using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skybrud.Essentials.Enums;
using Skybrud.Essentials.Strings;
using Skybrud.Essentials.Strings.Extensions;

namespace Skybrud.Umbraco.Redirects.Text.Json.Enums;

internal class EnumStringConverter<T> : JsonConverter<T> where T : Enum {

    private readonly TextCasing _casing;

    public EnumStringConverter(TextCasing casing) {
        _casing = casing;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        return reader.TokenType switch {
            JsonTokenType.Null => default,
            JsonTokenType.String => (T) EnumUtils.ParseEnum(reader.GetString()!, typeof(T)),
            _ => throw new Exception($"Unsupported token type: {reader.TokenType}")
        };
    }

    public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options) {

        if (value == null) {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.ToCasing(_casing));

    }

}