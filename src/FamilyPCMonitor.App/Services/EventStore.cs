using Microsoft.Data.Sqlite;

namespace FamilyPCMonitor.Services;

public sealed record MonitorEvent(long Id, string EventType, DateTime TimestampUtc, string LocalTimestamp, string ComputerName, string WindowsUser, bool SentToDiscord);

public sealed class EventStore(string path)
{
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    public void Initialize()
    {
        using var c = new SqliteConnection(ConnectionString); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TABLE IF NOT EXISTS Events (Id INTEGER PRIMARY KEY AUTOINCREMENT, EventType TEXT NOT NULL, TimestampUtc TEXT NOT NULL, LocalTimestamp TEXT NOT NULL, ComputerName TEXT NOT NULL, WindowsUser TEXT NOT NULL, SentToDiscord INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL);"; cmd.ExecuteNonQuery();
    }
    public long Add(string type)
    {
        using var c = new SqliteConnection(ConnectionString); c.Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO Events(EventType,TimestampUtc,LocalTimestamp,ComputerName,WindowsUser,SentToDiscord,CreatedAt) VALUES($e,$u,$l,$c,$w,0,$u); SELECT last_insert_rowid();";
        var now = DateTime.UtcNow; cmd.Parameters.AddWithValue("$e", type); cmd.Parameters.AddWithValue("$u", now.ToString("O")); cmd.Parameters.AddWithValue("$l", now.ToLocalTime().ToString("O")); cmd.Parameters.AddWithValue("$c", Environment.MachineName); cmd.Parameters.AddWithValue("$w", Environment.UserName); return (long)cmd.ExecuteScalar()!;
    }
    public List<MonitorEvent> Get(string search = "")
    {
        using var c = new SqliteConnection(ConnectionString); c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT Id,EventType,TimestampUtc,LocalTimestamp,ComputerName,WindowsUser,SentToDiscord FROM Events WHERE EventType LIKE $s OR ComputerName LIKE $s OR WindowsUser LIKE $s ORDER BY Id DESC LIMIT 2000"; cmd.Parameters.AddWithValue("$s", $"%{search}%"); using var r = cmd.ExecuteReader(); var list = new List<MonitorEvent>(); while (r.Read()) list.Add(new(r.GetInt64(0),r.GetString(1),DateTime.Parse(r.GetString(2)).ToUniversalTime(),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6)!=0)); return list;
    }
    public MonitorEvent? Latest() => Get().FirstOrDefault();
    public List<(long Id,string Type,string Local,string Computer,string User)> Pending()
    {
        using var c = new SqliteConnection(ConnectionString); c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT Id,EventType,LocalTimestamp,ComputerName,WindowsUser FROM Events WHERE SentToDiscord=0 ORDER BY Id LIMIT 30"; using var r=cmd.ExecuteReader(); var a=new List<(long,string,string,string,string)>(); while(r.Read()) a.Add((r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4))); return a;
    }
    public void MarkSent(long id) { using var c=new SqliteConnection(ConnectionString); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="UPDATE Events SET SentToDiscord=1 WHERE Id=$id"; cmd.Parameters.AddWithValue("$id",id); cmd.ExecuteNonQuery(); }
    public void Cleanup(int days) { if(days<=0)return; using var c=new SqliteConnection(ConnectionString); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Events WHERE TimestampUtc < $cutoff"; cmd.Parameters.AddWithValue("$cutoff",DateTime.UtcNow.AddDays(-days).ToString("O")); cmd.ExecuteNonQuery(); }
    public void DeleteAll() { using var c=new SqliteConnection(ConnectionString); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Events; DELETE FROM sqlite_sequence WHERE name='Events';"; cmd.ExecuteNonQuery(); }
}
