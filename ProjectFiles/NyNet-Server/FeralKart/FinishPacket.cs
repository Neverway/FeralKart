public class FinishPacket
{
    public bool   Failed          { get; set; }
    public int    Placement       { get; set; }
    public float  FinishTime      { get; set; }
    public float  HealthRemaining { get; set; }
    public int    LivesRemaining  { get; set; }
    public int    Kills           { get; set; }
    public float  DamageTaken     { get; set; }
    public float  DamageDealt     { get; set; }
    public float  DamageHealed    { get; set; }
}
