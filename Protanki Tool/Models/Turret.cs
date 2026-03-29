using ProtankiTool.Types;

namespace ProtankiTool.Models
{
    public class Turret
    {
        public string Name { get; set; } = string.Empty;
        public DamageType DamageType { get; set; }
        public TurretRange Range { get; set; }
        public bool CanDefend { get; set; }
    }
}
