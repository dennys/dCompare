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
                    // The key is not a valid FileType enum member.
                    // Skip the corresponding value to avoid a crash.
                    reader.Read(); // Move to the start of the value
                    reader.Skip(); // Skip the entire value
                    continue; // Move to the next property
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
