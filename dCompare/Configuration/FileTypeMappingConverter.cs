using dCompare.Tools.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace dCompare.Configuration
{
    public class FileTypeMappingConverter : JsonConverter<Dictionary<FileType, FileTypeMappingOptions>>
    {
        public override Dictionary<FileType, FileTypeMappingOptions> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            var dictionary = new Dictionary<FileType, FileTypeMappingOptions>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName token");
                }

                var propertyName = reader.GetString();
                if (!Enum.TryParse(propertyName, true, out FileType fileType))
                {
                    // You might want to handle this case more gracefully,
                    // e.g., by logging a warning and skipping the invalid entry.
                    throw new JsonException($"Unable to convert '{propertyName}' to FileType enum.");
                }

                reader.Read();
                var value = JsonSerializer.Deserialize<FileTypeMappingOptions>(ref reader, options);
                if (value != null)
                {
                    dictionary[fileType] = value;
                }
            }

            throw new JsonException("Expected EndObject token");
        }

        public override void Write(Utf8JsonWriter writer, Dictionary<FileType, FileTypeMappingOptions> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var (key, val) in value)
            {
                var propertyName = key.ToString();
                writer.WritePropertyName(propertyName);
                JsonSerializer.Serialize(writer, val, options);
            }

            writer.WriteEndObject();
        }
    }
}
