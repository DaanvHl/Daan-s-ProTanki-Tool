namespace ProtankiTool.Models
{
    public class Paint
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Keys are DamageType names (e.g. "Railgun"), values are protection percentages (0–100).
        /// Using string keys so the JSON file stays human-readable.
        /// </summary>
        public Dictionary<string, double> Protections { get; set; } = [];
    }
}
