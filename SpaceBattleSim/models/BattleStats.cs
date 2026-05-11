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
        UnderAttack
    }
    public static class BattleStats
    {
        const string _auditFolder = ".\\audit";
        private static readonly string _orgTempDetailsFile = $"{_auditFolder}\\{{0}}.tmp";
        private static string _tempDetailsFile = string.Empty;

        internal static ADateTime _startDate = ADateTime.MinValue;
        internal static ABool _queueProcessing = ABool.False;
        internal static ABool _detailProcessing = ABool.False;
        internal static ConcurrentDictionary<string, ShipAudit> _shipAudits = new ConcurrentDictionary<string, ShipAudit>();
        internal static ConcurrentQueue<(string, ActionType, string)> _actionAudit = new ConcurrentQueue<(string, ActionType, string)>();
        internal static ConcurrentDictionary<string, string> _settingValues = new ConcurrentDictionary<string, string>();
        internal static ConcurrentQueue<(DateTime, string)> _detailAudit = new ConcurrentQueue<(DateTime, string)>();

        static BattleStats()
        {
            if (!Directory.Exists(_auditFolder))
                Directory.CreateDirectory(_auditFolder);
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

            // save name, then switch, for next roleover
            var tempFile = _tempDetailsFile;
            _tempDetailsFile = string.Empty;
            _startDate = ADateTime.MinValue;

            var auditName = tempFile.Replace(".tmp", ".log");   // $"{_auditFolder}\\{DateTime.Now:yyMMdd_HHmmss}.log";
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

            var maxWait = 50;   // 5sec = (50 * 100ms)
            while (!_queueProcessing.TrySetTrue() && --maxWait > 0)
                Task.Delay(100).Wait();

            if (maxWait > 0)
            {
                try
                {
                    List<string> detailsFileArray = new List<string>();
                    // If the details temp file exists, load it up and copy content over the details log
                    // file, then delete the temp file. 
                    if (File.Exists(tempFile))
                    {
                        detailsFileArray.AddRange(File.ReadAllLines(tempFile));
                        File.Delete(tempFile);
                    }

                    // if there are any details, add them to the end of the summary, this way we can be sure to
                    // get all details without having to worry about memory usage during the match.
                    if (detailsFileArray.Count > 0)
                        sb.AppendLine(string.Join(Environment.NewLine, detailsFileArray));

                    // Save the audit info to a file
                    File.WriteAllText(auditName, sb.ToString());
                    // give it a moment to flush the file before we try to write any details, just in case there are any left overs.
                    Task.Delay(100).Wait();

                    // now write any details that may have come in while we were writing the summary,
                    // this way we can be sure to get all details without having to worry about memory usage during the match.
                    WriteDetailsToFile(auditName, true);
                }
                catch (Exception ex)
                {
                    File.AppendAllText(auditName, $"Failed to acquire queue lock to get details, skipping details for this audit.{Environment.NewLine}Exception:{Environment.NewLine}\t{ex}");
                }
                finally
                {
                    _actionAudit.Clear();
                    // force to false just in case, but should be false already if we got here.
                    _queueProcessing.SetFalse();
                    // reset detail
                    _detailAudit.Clear();
                }
            }
            else
                File.AppendAllText(auditName, $"Failed to acquire queue lock to get details, skipping details for this audit.{Environment.NewLine}");
        }
        private static void WriteDetailsToFile(string fileName, bool waitForIt = false)
        {
            var maxWait = 50;   // 5sec = (50 * 100ms)
            while (!_detailProcessing.TrySetTrue() && --maxWait > 0)
            {
                if (!waitForIt)
                    return;
                Task.Delay(100).Wait();
            }

            if (maxWait == 0)
                return;

            StringBuilder sb = new StringBuilder();

            try
            {
                if (!File.Exists(fileName))
                    File.WriteAllText(fileName, "");

                // because detail information can get quite large in memory, we will just process it and write it to
                // the tmp file when the match is over. This way we can keep the memory usage down and on match end, save
                // will have it all copied over to the details at the end of the audit file...

                while (_detailAudit.TryDequeue(out var detail))
                {
                    var (dt, note) = detail;
                    sb.AppendLine($"{dt:hh:mm:ss.ffff tt}: {note}");
                }

                // Save the audit info to a file
                File.AppendAllLines(fileName, sb.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            }
            finally
            {
                _detailProcessing.SetFalse();
            }
        }
        private static async Task ProcessQueuesAsync()
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

                    CheckForNewStartDate();
                    WriteDetailsToFile(_tempDetailsFile, false);
                }
                finally
                {
                    _queueProcessing.SetFalse();
                }
            });
        }

        private static bool CheckForNewStartDate()
        {
            if (_startDate == ADateTime.MinValue)
            {
                _startDate = ADateTime.Now;
                _tempDetailsFile = string.Format(_orgTempDetailsFile, _startDate.ToString("yyMMdd_HHmmss"));
                return true;
            }

            return false;
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
