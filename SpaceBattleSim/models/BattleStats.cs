using Chizl.ThreadSupport;
using System.Collections.Concurrent;
using System.Text;

namespace SpaceBattleSim
{
    public enum  ActionType
    {
        Kill,
        Death,
        CriticalTransfer,
        Heal,
        AlmostDead,
        TakeDamage
    }
    public static class BattleStats
    {
        const string AuditFolder = ".\\audit";

        internal static ABool _queueProcessing = ABool.False;
        internal static ConcurrentDictionary<string, ShipAudit> _shipAudits = new ConcurrentDictionary<string, ShipAudit>();
        internal static ConcurrentQueue<(string, ActionType, string)> _actionAudit = new ConcurrentQueue<(string, ActionType, string)>();
        internal static ConcurrentDictionary<string, string> _settingValues = new ConcurrentDictionary<string, string>();
        internal static ConcurrentQueue<(DateTime, string)> _detailAudit = new ConcurrentQueue<(DateTime, string)>();

        static BattleStats()
        {
            if (!Directory.Exists(AuditFolder))
                Directory.CreateDirectory(AuditFolder);
        }
        public static void AddSetting(string name, object value) => _settingValues.TryAdd(name, $"{value}");

        public static void AddShip(string name, ShipType shipType)
        {
            var sAudit = new ShipAudit() { Name = name, ShipType = shipType };
            _shipAudits.TryAdd(name, sAudit);
        }
        public static void Audit(string name, ActionType actionType, string note = "")
        {
            _actionAudit.Enqueue((name, actionType, note));
            _detailAudit.Enqueue((DateTime.Now, $"{name}->{actionType}{(note.Length > 0 ? $" - {note}" : "")}"));
            _ = ProcessQueuesAsync();
        }

        public static void SaveAudit(DateTime startDate, DateTime endDate, string winners)
        {
            if (_shipAudits.IsEmpty)
                return;

            var auditName = $"{AuditFolder}\\{DateTime.Now:yyMMdd_HHmm}.log";
            StringBuilder sb = new();

            var fLine = $"Summary of {winners}: {startDate.ToLocalTime()} - {endDate.ToLocalTime()} ({endDate - startDate})";
            sb.AppendLine(fLine);
            sb.AppendLine(new string('-', fLine.Length));

            foreach(ShipType sType in Enum.GetValues(typeof(ShipType)))
            {
                if (sType == ShipType.Bomber || sType == ShipType.Transport)
                    continue;
                
                var ship = new ShipStats(sType);
                sb.AppendLine($"{sType}->Shields: {ship.Shields} | Power: {ship.Power} | Speed: {ship.Speed} | " +
                                $"HasCritalTransfer: {ship.HasCritalTransfer} | Hitbox: {ship.Hitbox} | Recovery: {ship.Recovery} | " +
                                $"Visiual: {ship.ShipView}");
            }

            sb.AppendLine(new string('-', fLine.Length));

            foreach (var setting in _settingValues)
                sb.AppendLine($"{setting.Key}: {setting.Value}");

            sb.AppendLine(new string('-', fLine.Length));

            foreach (var shipAudit in _shipAudits.OrderBy(o => o.Value.Name).Select(s=>s.Value))
            {
                var healStat = "";

                if (shipAudit.ShipType == ShipType.RepairRig)
                    healStat = $" | Heals: {shipAudit.Heals}";

                sb.AppendLine($"{shipAudit.Name} | {shipAudit.ShipType} | " +
                                    $"Deaths: {shipAudit.Deaths} | " +
                                    $"Kills: {shipAudit.Kills} | " +
                                    $"CritTransfers: {shipAudit.CriticalTransfers}{healStat}");
            }

            // reset the audits for the next match
            _shipAudits.Values.ToList().ForEach(s => s.Reset());

            fLine = $"Details of {winners}: {startDate} - {endDate} ({endDate - startDate})";
            sb.AppendLine(new string('-', fLine.Length));
            sb.AppendLine(fLine);
            sb.AppendLine(new string('-', fLine.Length));

            while(_detailAudit.TryDequeue(out var detail))
            {
                var (dt, note) = detail;
                sb.AppendLine($"{dt:hh:mm:ss.ffff tt}: {note}");
            }

            // reset detail
            _detailAudit.Clear();

            // Save the audit info to a file
            File.WriteAllText(auditName, sb.ToString());
        }

        internal static async Task ProcessQueuesAsync()
        {
            if (!_queueProcessing.TrySetTrue())
                return;

            await Task.Run(() =>
            {
                try
                {
                    while (_actionAudit.TryDequeue(out var audit))
                    {
                        var sAudit = new ShipAudit();
                        var (name, action, note) = audit;
                        if (_shipAudits.Keys.Contains(name))
                        {
                            switch (action)
                            {
                                case ActionType.Death:
                                    _shipAudits[name].Died();
                                    break;
                                case ActionType.Kill:
                                    _shipAudits[name].Killed();
                                    break;
                                case ActionType.CriticalTransfer:
                                    _shipAudits[name].CriticalTransfer();
                                    break;
                                case ActionType.Heal:
                                    _shipAudits[name].Healed();
                                    break;
                            }

                            if (!string.IsNullOrWhiteSpace(note))
                                _shipAudits[name].AddNote(note);
                        }
                    }
                }
                finally
                {
                    _queueProcessing.SetFalse();
                }
            });
        }
    }

    internal class ShipAudit
    {
        private List<string> _notes = new List<string>();
        private int _deaths = 0;
        private int _kills = 0;
        private int _heals = 0;
        private int _criticalTransfers = 0;

        public string Name { get; set; } = string.Empty;
        public ShipType ShipType { get; set; }
        public void AddNote(string note) => _notes.Add(note);
        public int Died() => Interlocked.Increment(ref _deaths);
        public int Killed() => Interlocked.Increment(ref _kills);
        public int CriticalTransfer() => Interlocked.Increment(ref _criticalTransfers);
        public int Healed() => Interlocked.Increment(ref _heals);

        public string[] Notes => _notes.ToArray();
        public int Deaths => _deaths;
        public int Kills => _kills;
        public int CriticalTransfers => _criticalTransfers;
        public int Heals => _heals;
        public void Reset()
        {
            _deaths = 0;
            _kills = 0;
            _criticalTransfers = 0;
            _heals = 0;
            _notes.Clear();
        }
    }
}
