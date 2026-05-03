using Chizl.ThreadSupport;
using System.Collections.Concurrent;

// Copyright (c) 2026 Gavin W. Landon (chizl.com)
// Licensed under the MIT License. See LICENSE file http://www.chizl.com/LICENSE.txt for full license information.
// SPDX-License-Identifier: MIT
namespace Chizl.StandAloneLogging
{
    [Flags]
    public enum LogLevel
    {
        None = 0,
        Application = 1,
        Critical = 2,
        Error = 4,
        Information = 8,
        Debug = 16,
        All = 32,
    }

    /// <summary>
    /// Provides functionality for writing log messages to files with support for multiple log levels, asynchronous
    /// processing, and log file management.<br/>
    /// This class is a simplier standalone implementation of <a href="https://github.com/gavin1970/Chizl.IO.Logging">TextLogger</a>.
    /// </summary>
    /// <remarks>The Logger class enables applications to record messages categorized by log level, with each
    /// level written to a separate file. Log files are automatically rotated daily and old logs are deleted based on
    /// the configured retention period. Logging operations are performed asynchronously to minimize impact on
    /// application performance. The logger is resilient to transient I/O failures and will automatically stop logging
    /// after repeated failures to prevent further issues. Use the static properties to obtain default or empty logger
    /// instances as needed.</remarks>
    internal class Logger
    {
        const string DEFAULT_LOG_PATH = ".\\logs";
        // Can be adjusted, but if an exception occurs consecutively MAX_IO_FAILURES, logging will be stopped to prevent further issues.
        // This helps to avoid potential data corruption or other issues that could arise from trying to log when the logger is not
        // properly configured, while still allowing for some retries in case of transient issues.  If a successful write occurs before
        // MAX_IO_FAILURES is reached, _ioFailureCount will be reset back to 0.  
        const int MAX_IO_FAILURES = 50;
        private int _ioFailureCount = 0;

        private List<FileInfo> _fileInfo = new();
        private ConcurrentQueue<(LogLevel Level, string Msg)> _msgQueue = new();
        private ABool _startWriting = ABool.False;
        private ABool _stopLogging = ABool.False;
        private ADateTime _activeFileDate = Now.Date.AddDays(-1);
        private LogLevel _enabledLogLevels = LogLevel.Application | LogLevel.Error;
        private string _logPath = DEFAULT_LOG_PATH;
        private DirectoryInfo _logDirectoryInfo = new DirectoryInfo(DEFAULT_LOG_PATH);

        #region Public Static Readonly Properties
        /// <summary>
        /// Gets the current date and time on the local computer.
        /// </summary>
        public static DateTime Now => DateTime.Now;
        /// <summary>
        /// Gets the current time as a string in the format HH:mm:ss.ffff.
        /// </summary>
        /// <remarks>The returned string represents the current time of day, including hours, minutes,
        /// seconds, and fractional seconds to four decimal places. This property is useful for generating precise time
        /// stamps.</remarks>
        public static string DateStr => $"{Now:HH:mm:ss.ffff}";
        /// <summary>
        /// Initializes a new instance of the Logger class, using default values.<br/>
        /// Defaults:<br/>
        ///    LogPath = ".\logs";<br/>
        ///    LogLevels = LogLevel.Application | LogLevel.Error
        /// </summary>
        public static Logger Default => new Logger(LogLevel.All, DEFAULT_LOG_PATH);
        /// <summary>
        /// Gets an instance of the logger that performs no logging operations.
        /// </summary>
        /// <remarks>Use this property when logging is not required or should be disabled. The returned
        /// logger ignores all log messages regardless of their level.</remarks>
        public static Logger Empty => new Logger(LogLevel.None);
        #endregion

        #region Public Properties
        public bool IsEmpty { get { return _stopLogging; } }
        /// <summary>
        /// Gets or sets the file system path where log files are stored.
        /// </summary>
        public string LogPath { get { return _logPath; } }
        /// <summary>
        /// Gets or sets the duration for which logs are retained before they are eligible for deletion.
        /// </summary>
        public TimeSpan KeepLogsFor { get; set; } = TimeSpan.FromDays(3);
        #endregion

        #region Constructors
        private Logger(LogLevel logLevels) => _stopLogging.SetTrue();
        /// <summary>
        /// Initializes a new instance of the Logger class with the specified log levels and log file path.
        /// </summary>
        /// <remarks>Use this constructor to configure the logger to capture specific log levels and
        /// direct output to a designated file. The logger will validate the provided path and may throw an exception if
        /// the path is invalid or inaccessible.</remarks>
        /// <param name="logLevels">The log levels that determine which messages are recorded by the logger. Multiple levels can be combined
        /// using a bitwise OR operation.</param>
        /// <param name="logPath">The file system path where log entries will be written. Must be a valid, writable path.</param>
        public Logger(LogLevel logLevels, string logPath)
        {
            _logPath = logPath;
            _enabledLogLevels = logLevels;
            CheckPath();
        }
        /// <summary>
        /// Initializes a new instance of the Logger class, using default values.<br/>
        /// Defaults:<br/>
        ///    LogPath = ".\logs";<br/>
        ///    LogLevels = LogLevel.Application | LogLevel.Error
        /// </summary>
        /// <remarks>This constructor prepares the logger for use. Ensure that any required configuration,
        /// such as log file paths or settings, is established before logging operations are performed.</remarks>
        public Logger() => CheckPath();
        #endregion

        #region Public Methods
        /// <summary>
        /// Enqueues a log message with the specified log level for asynchronous processing and output.
        /// </summary>
        /// <remarks>This method trims trailing whitespace characters from the message before enqueuing
        /// it. The message is timestamped and added to an internal queue for asynchronous processing. This method does
        /// not block the calling thread.</remarks>
        /// <param name="lLevel">
        /// The severity level of the log message. Determines how the message is categorized, filtered, and can use multiple types.<br/>
        /// e.g. LogLevel.Application | LogLevel.Error<br/>
        ///      LogLevel.All
        /// </param>
        /// <param name="msg">The message to log. Trailing carriage returns, line feeds, and tab characters are removed, and NewLine added before logging.</param>
        public void WriteLine(LogLevel lLevel, string msg)
        {
            if (_stopLogging)
                return;

            // clean up end of text, since we are adding a newline in the log entry, to avoid double newlines.
            while (msg.EndsWith('\r') || msg.EndsWith('\n') || msg.EndsWith('\t'))
                msg = msg.Substring(0, msg.Length - 1);

            // add to queue
            _msgQueue.Enqueue((lLevel, $"{DateStr}: {msg.Trim()}{Environment.NewLine}"));

            // Fire-and-forget with discard operator
            _ = ProcessQueueAsync();
        }
        #endregion

        #region Support Properties and Methods
        /// <summary>
        /// Processes queued log messages asynchronously, writing them to the appropriate log files based on their log
        /// levels.
        /// </summary>
        /// <remarks>This method manages the lifecycle of log file writers and ensures that all messages
        /// in the queue are written to disk efficiently. It handles log level filtering, file I/O failures, and ensures
        /// that only one write operation occurs at a time. If additional messages are enqueued during processing, the
        /// method will recursively process them until the queue is empty. Exceptions during logging are handled
        /// internally to avoid disrupting the calling thread.</remarks>
        /// <returns></returns>
        private async Task ProcessQueueAsync()
        {
            if (_stopLogging || !_startWriting.TrySetTrue())
                return;

            try
            {
                await Task.Run(() =>
                {
                    StreamWriter[] writer = new StreamWriter[Enum.GetValues(typeof(LogLevel)).Length];

                    try
                    {
                        // If there are messages in the queue, we need to set up the
                        // log file and open the writers before we can start writing.
                        // Only once, before the writing loop, to minimize file I/O
                        // and reduce contention.  We do this inside the Task.Run to
                        // ensure that it is done in the background and does not
                        // block the calling thread, especially if there are a large
                        // number of messages to write or if the log file needs to be
                        // created or updated.  We also check _exitWriteNow to allow
                        // for an early exit if shutdown has begun while we were waiting
                        // to start writing.
                        if (_msgQueue.IsEmpty || _stopLogging)
                            return;

                        // Ensure log file is set up, datetime filename change,
                        // and _fileInfo refreshed, before we start writing.
                        LogSetup();

                        // Open a StreamWriter for each log level to write messages to the appropriate log file.
                        // We do this in a loop to handle all log levels and to ensure that we have a writer ready
                        // for each level that we need to write to, without having to check the log level of each
                        // message during the writing loop, which helps to reduce overhead and improve performance.
                        foreach (LogLevel logLevel in Enum.GetValues(typeof(LogLevel)))
                        {
                            if (logLevel == LogLevel.All)
                                continue; // Skip the "All" log level since it is not an actual
                                            // log level to write to, but rather a combination of
                                            // all levels for configuration purposes.

                            // Check if the current log level is enabled for logging.  If it
                            // is not, we can skip and check next level.  We do this to avoid
                            // opening writers for log levels that are not enabled, which helps
                            // to reduce overhead and improve performance, especially if there
                            // are many log levels and only a few are enabled.
                            // NOTE: & is faster than HasFlag() and works with [Flags] enums,
                            // but it requires a bitwise check to ensure the exact flag is set.
                            if ((_enabledLogLevels & logLevel) != logLevel)
                                continue;

                            // loading all levels, just in case log levels are added to the
                            // queue while we are writing, so we don't have to worry about loading
                            int logLevelVal = (int)Math.Log((int)logLevel, 2);
                            // We can also skip opening a writer for any log level that is
                            // not enabled for logging, since we won't be writing any messages
                            // for those levels.
                            writer[logLevelVal] = _fileInfo[logLevelVal].AppendText();

                            // The "Drain Loop": Keep going as long as there is work.
                            // This prevents the race condition where a message is 
                            // enqueued just as we are finishing the previous batch.
                            while (!_msgQueue.IsEmpty && !_stopLogging)
                            {
                                // Try to dequeue a message.  If successful, write it to the appropriate log file based on its log level.
                                while (_msgQueue.TryDequeue(out (LogLevel logLvl, string Msg) logEntry) && !_stopLogging)
                                {
                                    // Loop through all log levels to check which levels are included in the log entry's log level.  We do
                                    // this to ensure that messages with multiple log levels (e.g. LogLevel.Application | LogLevel.Error)
                                    // are written to all appropriate log files, while still allowing for efficient writing by avoiding
                                    // unnecessary checks during the writing loop.
                                    foreach (var lvl in Enum.GetValues<LogLevel>().Where(l => l != LogLevel.All && (logEntry.logLvl & l) == l))
                                    {
                                        int writeLevelVal = (int)Math.Log((int)logEntry.logLvl, 2);
                                        // Check if the log level of the message is enabled for logging.  If it is,
                                        // write the message to the appropriate log file using the corresponding
                                        // StreamWriter.  We do this to ensure that messages are only written to log
                                        // files for levels that are enabled, which helps to reduce overhead and
                                        // improve performance, especially if there are many log levels and only a
                                        // few are enabled.
                                        writer[writeLevelVal].WriteLine(logEntry.Msg);
                                    }
                                }
                            }
                        }
                        // No need to Interlocked since we are already in a single-threaded context
                        // within the Task.Run, and this is only accessed here after successfully
                        // setting _isWriting to true, which ensures that only one thread can be
                        // executing this block at a time.  We reset the IO failure count after a
                        // successful write operation to allow for retries if future write operations
                        // encounter issues, while still providing a mechanism to track and handle
                        // repeated failures without prematurely giving up on logging.
                        _ioFailureCount = 0; // Reset IO failure count after a successful write operation
                    }
                    catch
                    {
                        // catch exception
                        if (++_ioFailureCount >= MAX_IO_FAILURES)
                        {
                            // stop all future messages and clear queue to save on memory.

                            // Stopping
                            _stopLogging.SetTrue();
                            // Wait for all existing log flow to stop.
                            Task.Delay(100).Wait();
                            // Reset queue to save memory
                            _msgQueue.Clear();
                            throw; // Re-throw the exception to allow it to be handled by the caller or to crash the application if not handled
                        }
                        else
                        {
                            // failure occurred, provide a break.
                            Task.Delay(100).Wait();
                        }
                    }
                    finally
                    {
                        // Ensure all writers are properly closed to
                        // release file handles and flush buffers.
                        foreach (var w in writer)
                            w?.Close();
                    }
                });
            }
            catch { /* Silently catch exceptions in fire-and-forget logging */ }
            finally
            {
                // we are now done writing, so set _isWriting back
                // to false to allow the next write to proceed.
                _startWriting.SetFalse();

                // Check if any messages were queued while we were writing.
                // If so, start another write to process them.
                if (!_msgQueue.IsEmpty)
                    _ = ProcessQueueAsync();
            }

        }
        /// <summary>
        /// Initializes or updates log file resources for the current date and enabled log levels.
        /// </summary>
        /// <remarks>This method ensures that log files are correctly set up when the date changes or when
        /// logging is first initialized. It creates new log files for each enabled log level, updates internal file
        /// tracking, and handles file creation or separation of log entries as needed. The method is thread-safe and
        /// prevents concurrent setup operations to avoid file access conflicts. If an error occurs during setup,
        /// logging is halted to maintain a consistent state.</remarks>
        private void LogSetup()
        {
            // Check if the log file date is the same as today's
            // date.  If it is, we can skip the rest of the setup since
            if (_activeFileDate.Date.Equals(Now.Date))
                return;
            else
            {
                // Update the log file date and name to reflect the new date.
                // We do this before creating the file to ensure that
                _activeFileDate.AdjustTime(Now.Date);

            }

            // Clear the existing FileInfo list and create new FileInfo objects
            // for the new log file.  We do this to ensure that the FileInfo is
            // always up to date and reflects the current log file, even if it
            // doesn't exist yet.
            _fileInfo.Clear();

            try
            {
                // Calculate the expiration date for log files based on the current date
                // and the configured retention period.
                var expireDate = Now - KeepLogsFor;

                // Wait to until allowed to write to prevent potential issues that could
                // arise from trying to set up a new log file while another thread is still
                // writing to the old log file, such as file access conflicts or data
                // corruption.  We do this in a loop to ensure that we wait until we can
                // successfully set _isWriting to true, which indicates that no other thread
                // is currently writing and it is safe to proceed with the log setup.
                // All WriteLine calls will still be sent to queue during this pause, so
                // no information will be lost.
                while (!_startWriting.TrySetTrue())
                    Task.Delay(100).Wait();

                // Delete old log files that are past the expiration date.  We do this to
                // ensure that we are not keeping log files indefinitely, which helps to
                // save disk space and keep the log directory manageable.  We also do this
                // before creating new log files to ensure that we are starting with a clean
                // slate and to avoid potential issues with file access or naming conflicts
                // when creating new log files for the new date.
                var oldFileInfo = _logDirectoryInfo.EnumerateFileSystemInfos()
                                        .Cast<FileInfo>()
                                        .Where(w => w.CreationTime < expireDate || w.LastWriteTime < expireDate);

                // Delete old log files that are past the expiration date.
                foreach (var f in oldFileInfo)
                    f.Delete();

                // Loop through all log levels to create a log file for each level.
                // This allows us to separate logs by level and makes it easier to
                // find specific types of log messages.  We do this in the LogSetup()
                // method to ensure that it is done automatically when the date
                // changes, without requiring manual intervention. 
                foreach (LogLevel logLevel in Enum.GetValues(typeof(LogLevel)))
                {
                    if (logLevel == LogLevel.All)
                        continue; // Skip the "All" log level since it is not an actual
                                  // log level to write to, but rather a combination of
                                  // all levels for configuration purposes.

                    // Get the integer value of the log level to use as an index for the FileInfo list.
                    int logLevelVal = (int)Math.Log((int)logLevel, 2);

                    // Use a date-based log file name to create a new log file each day.
                    // This helps to keep log files manageable and makes it easier to find logs for specific dates.
                    var logFile = $"{_activeFileDate.Year:00}-{_activeFileDate.Month:00}-{_activeFileDate.Day:00}_{logLevel}.log";

                    // Combine the log path and file name to get the full path to the log file.
                    var fullFile = Path.Combine(LogPath, logFile);

                    // Update the FileInfo for the new log file.  We do this before creating
                    // the file to ensure that the FileInfo is always up to date and reflects
                    // the current log file, even if it doesn't exist yet.
                    _fileInfo.Add(new FileInfo(fullFile));

                    // Check if the current log level is enabled for logging.  If it
                    // is not, we can skip and check next level.  We do this to avoid
                    // opening writers for log levels that are not enabled, which helps
                    // to reduce overhead and improve performance, especially if there
                    // are many log levels and only a few are enabled.
                    // NOTE: & is faster than HasFlag() and works with [Flags] enums,
                    // but it requires a bitwise check to ensure the exact flag is set.
                    if ((_enabledLogLevels & logLevel) != logLevel)
                        continue;

                    // Refresh the FileInfo to ensure it has the latest information about
                    // the file, especially if it was created or modified by another process.
                    _fileInfo[logLevelVal].Refresh();

                    // if the log file doesn't exist, create it.  If it does exist, add a
                    // blank line to separate logs from different runs on the same day.
                    if (!_fileInfo[logLevelVal].Exists)
                    {
                        // Create the file and immediately close it to release
                        // the handle.  We do this to ensure that the file is
                        // created and ready for writing before we start logging messages.
                        _fileInfo[logLevelVal].Create().Close();
                        // Refresh to update the file info after creation
                        _fileInfo[logLevelVal].Refresh();
                    }
                    else if (logLevel == LogLevel.Application && _fileInfo[logLevelVal].Exists)
                    {
                        // If the file already exists, we can add a blank line to
                        // separate logs from different runs on the same day.
                        _msgQueue.Enqueue((logLevel, Environment.NewLine));
                    }
                }
            }
            catch
            {
                // If an exception occurs during log setup, we need to ensure that the logger is put
                // into a safe state to prevent further issues.  We set the shutting down and exit
                // write now flags to true to signal any ongoing write operations to stop immediately
                // and prevent further logging attempts, which helps to avoid potential data corruption
                // or other issues that could arise from trying to log when the logger is not properly
                // initialized or configured.

                // Set exit write now to true to signal any ongoing write operations to stop immediately
                _stopLogging.SetTrue(); 
                // Wait for a short period to allow any ongoing write operations to stop before proceeding
                Task.Delay(100).Wait(); 
                // Clear the message queue to prevent any further logging attempts and to free up memory
                _msgQueue.Clear();  
                throw; // Re-throw the exception to allow it to be handled by the caller or to crash the application if not handled
            }
        }
        /// <summary>
        /// Ensures that the directory specified by the log path exists, creating it if necessary.
        /// </summary>
        /// <remarks>If the directory does not exist, it is created. Any exceptions encountered during
        /// this process are not handled and will propagate to the caller.</remarks>
        private void CheckPath()
        {
            // Not wrapping in try / catch.  I want the error to be pushed back to the UI
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);
            // Set DirectoryInfo to query and pull old files, when that time comes.
            _logDirectoryInfo = new DirectoryInfo(LogPath);
        }
        #endregion
    }
}
