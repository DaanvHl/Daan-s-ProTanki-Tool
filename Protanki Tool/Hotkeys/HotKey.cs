using System.Text;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace ProtankiTool.Hotkeys
{
    [JsonConverter(typeof(HotKeyConverter))]
    public class HotKey : IEquatable<HotKey>
    {
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public MouseButton? MouseButton { get; set; }

        public HotKey(Key key, ModifierKeys modifiers)
        {
            Key = key;
            Modifiers = modifiers;
            MouseButton = null;
        }

        public HotKey(MouseButton mouseButton)
        {
            Key = Key.None;
            Modifiers = ModifierKeys.None;
            MouseButton = mouseButton;
        }

        public HotKey()
        {
            Key = Key.None;
            Modifiers = ModifierKeys.None;
            MouseButton = null;
        }

        public bool IsEmpty => Key == Key.None && MouseButton == null;

        public override string ToString()
        {
            if (MouseButton.HasValue)
            {
                return MouseButton.Value switch
                {
                    System.Windows.Input.MouseButton.Middle => "Middle Click",
                    System.Windows.Input.MouseButton.XButton1 => "Mouse 4",
                    System.Windows.Input.MouseButton.XButton2 => "Mouse 5",
                    _ => MouseButton.Value.ToString()
                };
            }

            if (IsEmpty)
                return "None";

            StringBuilder sb = new();
            if (Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl + ");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift + ");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt + ");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) sb.Append("Win + ");
            sb.Append(Key);
            return sb.ToString();
        }

        public bool Equals(HotKey? other)
        {
            return other is not null && (ReferenceEquals(this, other) ||
                (Key == other.Key && Modifiers == other.Modifiers && MouseButton == other.MouseButton));
        }

        public override bool Equals(object? obj)
        {
            return obj is not null && (ReferenceEquals(this, obj) || (obj.GetType() == GetType() && Equals((HotKey)obj)));
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Key, (int)Modifiers, MouseButton);
        }

        public static bool operator ==(HotKey? left, HotKey? right) => Equals(left, right);
        public static bool operator !=(HotKey? left, HotKey? right) => !Equals(left, right);
    }
}
