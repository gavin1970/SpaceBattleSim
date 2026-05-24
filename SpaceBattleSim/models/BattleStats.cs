using Chizl.ThreadSupport;
using System.Collections.Concurrent;
using System.Text;

// Copyright (c) 2026 Gavin W. Landon (chizl.com)
// Licensed under the MIT License. See LICENSE file http://www.chizl.com/LICENSE.txt for full license information.
// SPDX-License-Identifier: MIT
namespace SpaceBattleSim
{
    /// <summary>
    /// Provides static methods and properties for collecting, auditing, and managing battle statistics and ship-related
    /// events during a match. 
    /// </summary>
    /// <remarks>The BattleStats class is designed to track and audit ship actions, settings, and match
    /// outcomes in a concurrent, thread-safe manner. It supports enabling or disabling auditing, adding ship and
    /// setting information, recording actions, and saving audit logs to disk. All members are static, and the class is
    /// not intended to be instantiated. Thread safety is maintained for all public operations. Audit data is persisted
    /// to files for later analysis, and the class manages temporary and summary files automatically.</remarks>
    public static class BattleStats
    {
        const string _auditFolder = ".\\audit";
        private static readonly string _orgTempDetailsFile = $"{_auditFolder}\\{{0}}.tmp";
        private static string _tempDetailsFile = string.Empty;

        private static ABool _enabled = ABool.False;
        private static ABool _queueProcessing = ABool.False;
        private static ABool _detailProcessing = ABool.False;
        private static ADateTime _startDate = ADateTime.MinValue;
        private static ConcurrentDictionary<string, ShipAudit> _shipAudits = new ConcurrentDictionary<string, ShipAudit>();
        private static ConcurrentQueue<(string, ActionType, string)> _actionAudit = new ConcurrentQueue<(string, ActionType, string)>();
        private static ConcurrentDictionary<string, string> _settingValues = new ConcurrentDictionary<string, string>();
        private static ConcurrentQueue<(DateTime, string)> _detailAudit = new ConcurrentQueue<(DateTime, string)>();

        /// <summary>
        /// Initializes static resources and performs one-time setup for the BattleStats class.
        /// </summary>
        /// <remarks>Ensures the audit folder exists and removes any temporary files with a .tmp extension
        /// from the folder. This static constructor is called automatically before any static members are accessed or
        /// any instances are created.</remarks>
        static BattleStats()
        {
            if (!Directory.Exists(_auditFolder))
                Directory.CreateDirectory(_auditFolder);

            Directory.GetFiles(_auditFolder, "*.tmp").ToList().ForEach(f=>File.Delete(f));
        }
        /// <summary>
        /// Asynchronously releases all resources used by the audit system and clears all audit data.
        /// </summary>
        /// <remarks>After calling this method, the audit system is disabled and all internal collections
        /// are cleared. This method should be called when the audit system is no longer needed to ensure proper
        /// resource cleanup.</remarks>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        public static Task Dispose() => Task.Run(() =>
        {
            _enabled = false;

            foreach(var ship in _shipAudits.Values)
                ship.Dispose();
            
            _shipAudits.Clear();
            _actionAudit.Clear();
            _settingValues.Clear();
            _detailAudit.Clear();
        });

        /// <summary>
        /// Adds a new setting with the specified name and value to the settings collection.
        /// </summary>
        /// <param name="name">The name of the setting to add. Cannot be null or empty.</param>
        /// <param name="value">The value to associate with the setting name. May be any object; its string representation will be stored.</param>
        public static void AddSetting(string name, object value) {
            if (string.IsNullOrEmpty(name))
                return;

            if(!_settingValues.TryAdd(name, $"{value}"))    // add new setting
                _settingValues[name] = $"{value}";          // update if already exists
        }
        /// <summary>
        /// Adds a new ship audit entry with the specified name and ship type.
        /// </summary>
        /// <remarks>If auditing is not enabled or the specified name is null or empty, the method does
        /// not add an entry.</remarks>
        /// <param name="name">The unique name of the ship to add. Cannot be null or empty.</param>
        /// <param name="shipType">The type of the ship to associate with the audit entry.</param>
        public static void AddShip(string name, ShipType shipType)
        {
            if (!_enabled || string.IsNullOrEmpty(name))
                return;

            var sAudit = new ShipAudit() { Name = name, ShipType = shipType };
            _shipAudits.TryAdd(name, sAudit);
        }
        /// <summary>
        /// Records an audit entry for the specified action, including its name, type, and an optional note.
        /// </summary>
        /// <remarks>If auditing is disabled or the action name is null or empty, the method does not
        /// record an entry. Audit entries are processed asynchronously.</remarks>
        /// <param name="name">The name of the action to audit. Cannot be null or empty.</param>
        /// <param name="actionType">The type of action being audited.</param>
        /// <param name="note">An optional note providing additional context for the audit entry. If not specified, an empty string is
        /// used.</param>
        public static void Audit(string name, ActionType actionType, string note = "")
        {
            if (!_enabled || string.IsNullOrEmpty(name))
                return;

            _actionAudit.Enqueue((name, actionType, note));
            _detailAudit.Enqueue((DateTime.Now, $"{name}->{actionType}{(note.Length > 0 ? $" - {note}" : "")}"));
            _ = ProcessQueuesAsync();
        }
        /// <summary>
        /// Saves an audit log summarizing ship statistics, settings, and match details for the specified time period
        /// and winners.
        /// </summary>
        /// <remarks>The audit log includes summary statistics for each ship type, configuration settings,
        /// and individual ship performance. Details from a temporary file are appended if available. This method resets
        /// audit data for the next match and handles file operations and concurrency to ensure audit
        /// integrity.</remarks>
        /// <param name="startDate">The start date and time of the audit period.</param>
        /// <param name="endDate">The end date and time of the audit period.</param>
        /// <param name="winners">A string identifying the winners to include in the audit summary.</param>
        public static void SaveAudit(DateTime startDate, DateTime endDate, string winners)
        {
            if (!_enabled || _shipAudits.IsEmpty)
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
                sb.AppendLine($"{setting.Key}: {setting.Value}{(setting.Key=="RefreshRate" ? $" - ({ShipStats.RefreshRateText})" : "")}");

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
                    var endTime = DateTime.UtcNow.AddSeconds(10);
                    // Force the main thread to hold on until the logging thread catches up
                    while (!_detailAudit.IsEmpty && DateTime.UtcNow < endTime)
                    {
                        Task.Delay(100).Wait();
                        if (!_queueProcessing.Value && !_detailAudit.IsEmpty)
                            ProcessQueuesAsync().Wait();
                    }

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
                    // clear the queues just in case, but should be empty already if we got here.
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
        /// <summary>
        /// Gets or sets a value indicating whether the feature is enabled.
        /// </summary>
        public static bool Enabled { get { return _enabled; } set { _enabled = value; } }
        /// <summary>
        /// Writes the details of the audit to a file.
        /// </summary>
        /// <param name="fileName">The name of the file to write the details to.</param>
        /// <param name="waitForIt">Indicates whether to wait for the detail processing lock.</param>
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

            // NOW it is completely safe to close the file handles and clear your collections
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
                if(!_detailAudit.IsEmpty)
                    WriteDetailsToFile(fileName, true);
            }
        }
        /// <summary>
        /// Processes all pending ship audit actions in the queue asynchronously.
        /// </summary>
        /// <remarks>This method dequeues and processes all available audit actions, updating ship audit
        /// records accordingly. It ensures that only one processing operation runs at a time. After processing, it
        /// updates the start date if necessary and writes audit details to a temporary file.</remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
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
        /// <summary>
        /// Determines whether a new start date should be set and updates related state if necessary.
        /// </summary>
        /// <remarks>If the start date has not been initialized, this method sets it to the current date
        /// and time and updates the temporary details file name accordingly. Subsequent calls will return false unless
        /// the start date is reset elsewhere.</remarks>
        /// <returns>true if a new start date was set; otherwise, false.</returns>
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

        /// <summary>
        /// Represents an audit record for a ship, tracking statistics such as deaths, kills, heals, and critical
        /// transfers during a match.
        /// </summary>
        /// <remarks>This class provides thread-safe methods for incrementing and retrieving ship-related
        /// statistics. It implements IDisposable to allow for resource cleanup and resetting of statistics when the
        /// audit is no longer needed. Instances are intended for use within the context of a single match or
        /// session.</remarks>
        private class ShipAudit : IDisposable
        {
            private int _deaths = 0;
            private int _kills = 0;
            private int _heals = 0;
            private int _criticalTransfers = 0;
            private bool disposedValue;

            /// <summary>
            /// Gets or sets the name associated with this instance.
            /// </summary>
            public string Name { get; set; } = string.Empty;
            /// <summary>
            /// Gets or sets the type of ship associated with this instance.
            /// </summary>
            public ShipType ShipType { get; set; }
            /// <summary>
            /// Atomically increments the death count and returns the updated value.
            /// </summary>
            /// <remarks>This method is thread-safe and can be called concurrently from multiple
            /// threads.</remarks>
            /// <returns>The new value of the death count after the increment operation.</returns>
            public int Died() => Interlocked.Increment(ref _deaths);
            /// <summary>
            /// Atomically increments the kill count and returns the updated value.
            /// </summary>
            /// <remarks>This method is thread-safe and can be called concurrently from multiple
            /// threads.</remarks>
            /// <returns>The new value of the kill count after the increment operation.</returns>
            public int Killed() => Interlocked.Increment(ref _kills);
            /// <summary>
            /// Atomically increments the count of critical transfers and returns the new value.
            /// </summary>
            /// <remarks>This method is thread-safe and can be called concurrently from multiple
            /// threads. It uses atomic operations to ensure that the count is updated correctly in multithreaded
            /// scenarios.</remarks>
            /// <returns>The incremented value of the critical transfer count after the operation completes.</returns>
            public int CriticalTransfer() => Interlocked.Increment(ref _criticalTransfers);
            /// <summary>
            /// Atomically increments the heal count and returns the new value.
            /// </summary>
            /// <remarks>This method is thread-safe and can be called concurrently from multiple
            /// threads.</remarks>
            /// <returns>The updated number of heals after the increment operation.</returns>
            public int Healed() => Interlocked.Increment(ref _heals);
            /// <summary>
            /// Gets the total number of recorded deaths.
            /// </summary>
            public int Deaths => Volatile.Read(ref _deaths);
            /// <summary>
            /// Gets the total number of recorded kills.
            /// </summary>
            public int Kills => Volatile.Read(ref _kills);
            /// <summary>
            /// Gets the total number of recorded critical transfers.
            /// </summary>
            public int CriticalTransfers => Volatile.Read(ref _criticalTransfers);
            /// <summary>
            /// Gets the current number of heals performed.
            /// </summary>
            public int Heals => Volatile.Read(ref _heals);
            /// <summary>
            /// Resets all tracked statistics to their initial values.
            /// </summary>
            /// <remarks>This method sets the deaths, kills, critical transfers, and heals counters to
            /// zero. Use this method to clear all accumulated statistics and start fresh tracking.</remarks>
            public void Reset()
            {
                Interlocked.Exchange(ref _deaths, 0);
                Interlocked.Exchange(ref _kills, 0);
                Interlocked.Exchange(ref _criticalTransfers, 0);
                Interlocked.Exchange(ref _heals, 0);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // future use, in case we use images or something else
                        // that needs to be disposed, but for now just reset
                        // the stats for the next match.
                        this.Reset();
                    }

                    disposedValue = true;
                }
            }
            ~ShipAudit() => Dispose(disposing: false);
            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
