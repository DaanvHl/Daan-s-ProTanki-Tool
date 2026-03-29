using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ProtankiTool.Hotkeys
{
    public class HotKeyConverter : JsonConverter<HotKey>
    {
        public override HotKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    Utf8JsonReader tempReader = reader;
                    Key key = Key.None;
                    ModifierKeys mod = ModifierKeys.None;
                    MouseButton? mouseButton = null;

                    while (tempReader.Read())
                    {
                        if (tempReader.TokenType == JsonTokenType.EndObject)
                            break;

                        if (tempReader.TokenType == JsonTokenType.PropertyName)
                        {
                            string? propName = tempReader.GetString();
                            _ = tempReader.Read();

                            if (string.Equals(propName, "Key", StringComparison.OrdinalIgnoreCase))
                                _ = Enum.TryParse<Key>(tempReader.GetString(), out key);
                            else if (string.Equals(propName, "Modifiers", StringComparison.OrdinalIgnoreCase))
                                _ = Enum.TryParse<ModifierKeys>(tempReader.GetString(), out mod);
                            else if (string.Equals(propName, "MouseButton", StringComparison.OrdinalIgnoreCase))
                            {
                                if (Enum.TryParse<MouseButton>(tempReader.GetString(), out MouseButton mb))
                                    mouseButton = mb;
                            }
                        }
                    }

                    reader = tempReader;
                    return mouseButton.HasValue ? new HotKey(mouseButton.Value) : new HotKey(key, mod);
                }

                return new HotKey();
            }

            string? value = reader.GetString();

            if (string.IsNullOrEmpty(value) || value.Equals("None", StringComparison.OrdinalIgnoreCase))
                return new HotKey();

            if (value.StartsWith("Mouse:", StringComparison.OrdinalIgnoreCase))
            {
                string buttonName = value[6..];
                if (Enum.TryParse<MouseButton>(buttonName, out MouseButton mb))
                    return new HotKey(mb);
                return new HotKey();
            }

            string[] parts = value.Split('+');
            Key parsedKey = Key.None;
            ModifierKeys parsedModifiers = ModifierKeys.None;

            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                if (Enum.TryParse<Key>(trimmedPart, true, out Key key))
                    parsedKey = key;
                else if (string.Equals(trimmedPart, "Ctrl", StringComparison.OrdinalIgnoreCase))
                    parsedModifiers |= ModifierKeys.Control;
                else if (string.Equals(trimmedPart, "Shift", StringComparison.OrdinalIgnoreCase))
                    parsedModifiers |= ModifierKeys.Shift;
                else if (string.Equals(trimmedPart, "Alt", StringComparison.OrdinalIgnoreCase))
                    parsedModifiers |= ModifierKeys.Alt;
                else if (string.Equals(trimmedPart, "Win", StringComparison.OrdinalIgnoreCase))
                    parsedModifiers |= ModifierKeys.Windows;
            }

            return new HotKey(parsedKey, parsedModifiers);
        }

        public override void Write(Utf8JsonWriter writer, HotKey value, JsonSerializerOptions options)
        {
            if (value.MouseButton.HasValue)
                writer.WriteStringValue($"Mouse:{value.MouseButton.Value}");
            else
                writer.WriteStringValue(value.ToString());
        }
    }
}
