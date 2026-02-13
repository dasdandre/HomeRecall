namespace HomeRecall.Persistence.Enums;

public enum RetentionMode
{
    Smart,       // 24h full, 7d daily, 3m weekly
    SimpleDays,  // Keep all for X days
    SimpleCount, // Keep last X
    KeepAll      // Never delete
}
