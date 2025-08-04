public enum INPCRole
{
    None = 0,
    Wanderer = (1 << 0),   // 1
    Worker = (1 << 1),   // 2
    Guard = (1 << 2),   // 4
    Merchant = (1 << 3),   // 8
    Farmer = (1 << 4),   // 16
}