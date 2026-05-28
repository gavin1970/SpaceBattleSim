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
        All = Application | Critical | Error | Information | Debug,
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
    internal class Logger : IDisposable
    {
        const string DEFAULT_LOG_PATH = ".\\logs";
        // Can be adjusted, but if an exception occurs consecutively MAX_IO_FAILURES, logging will be stopped to
        // prevent further issues. This helps to avoid potential data corruption or other issues that could arise
        // from trying to log when the logger is not properly configured, while still allowing for some retries
        // in case of transient issues.  If a successful write occurs before MAX_IO_FAILURES is reached, _ioFailureCount
        // will be reset back to 0.  
        const int MAX_IO_FAILURES = 50;
        private int _ioFailureCount = 0;

        private ConcurrentDictionary<LogLevel, FileInfo> _fileInfo = new();
        private ConcurrentQueue<(LogLevel Level, string Msg)> _msgQueue = new();
        private ABool _startWriting = ABool.False;
        private ABool _stopLogging = ABool.False;
        private ADateTime _activeFileDate = Now.Date.AddDays(-3);
        private LogLevel _enabledLogLevels = LogLevel.Application | LogLevel.Error;
        private string _logPath = DEFAULT_LOG_PATH;
        private DirectoryInfo _logDirectoryInfo = new DirectoryInfo(DEFAULT_LOG_PATH);
        private bool disposedValue;

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
        ///    LogLevels = LogLevel.Application | LogLevel.Error | LogLevel.Critical
        /// </summary>
        public static Logger Default => new Logger(LogLevel.Application | LogLevel.Error | LogLevel.Critical, DEFAULT_LOG_PATH);
        /// <summary>
        /// Gets a logger instance configured to capture all log levels for development purposes.<br/>
        /// Defaults:<br/>
        ///    LogPath = ".\logs";<br/>
        ///    LogLevels = LogLevel.All
        /// </summary>
        /// <remarks>This logger is intended for use during development and debugging. It records messages
        /// at all log levels to the default log path, which may include verbose or sensitive information not suitable
        /// for production environments.</remarks>
        public static Logger Developer => new Logger(LogLevel.All, DEFAULT_LOG_PATH);
        /// <summary>
        /// Gets an instance of the logger that performs no logging operations.
        /// </summary>
        /// <remarks>Use this property when logging is not required or should be disabled. The returned
        /// logger ignores all log messages regardless of their level.</remarks>
        public static Logger Empty => new Logger(LogLevel.None);
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets a value indicating whether the logger is currently stopped and not processing any log messages.
        /// </summary>
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
        /// <summary>
        /// Initializes a new instance of the Logger class with the specified log levels enabled.
        /// </summary>
        /// <param name="logLevels">The log levels to enable for this logger instance. Only messages 
        /// at these levels will be processed.</param>
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
        /// <remarks>
        /// This method trims trailing whitespace characters from the message before enqueuing it. 
        /// The message is timestamped and added to an internal queue for asynchronous processing. 
        /// This method does not block the calling thread.
        /// </remarks>
        /// <param name="lLevel">
        /// The severity level of the log message. Determines how the message is categorized, filtered, and can use multiple types.<br/>
        /// e.g. LogLevel.Application | LogLevel.Error<br/>
        ///      LogLevel.All
        /// </param>
        /// <param name="msg">
        /// The message to log. Trailing carriage returns, line feeds, and tab characters are removed, 
        /// and NewLine added before logging.
        /// </param>
        public void WriteLine(LogLevel lLevel, string msg)
        {
            // If logging is stopped or the log level is None, we can skip processing
            // the message since it will not be logged.  This helps to reduce overhead
            // and improve performance by avoiding unnecessary processing for messages
            // that will not be logged.
            if (_stopLogging ||
                lLevel == LogLevel.None ||
                (_enabledLogLevels & lLevel) != lLevel)
                return;

            // clean up end of text, since we are adding a newline in the log entry,
            // to avoid double newlines.
            while (msg.EndsWith('\r') || msg.EndsWith('\n') || msg.EndsWith('\t'))
                msg = msg.Substring(0, msg.Length - 1);

            // add to queue
            _msgQueue.Enqueue((lLevel, $"{DateStr}: {msg.Trim()}"));

            // Fire-and-forget with discard operator
            _ = ProcessQueueAsync();
        }
        #endregion

        #region Support Properties and Methods
        /// <summary>
        /// Processes queued log messages asynchronously, writing them to the appropriate log files based on their log
        /// levels.
        /// </summary>
        /// <remarks>
        /// This method manages the lifecycle of log file writers and ensures that all messages
        /// in the queue are written to disk efficiently. It handles log level filtering, file I/O failures, and ensures
        /// that only one write operation occurs at a time. If additional messages are enqueued during processing, the
        /// method will recursively process them until the queue is empty. Exceptions during logging are handled
        /// internally to avoid disrupting the calling thread.
        /// </remarks>
        private async Task ProcessQueueAsync()
        {
            if (!_startWriting.TrySetTrue())
                return;

            await Task.Run(() =>
            {
                // Create a dictionary to hold StreamWriter instances for each log level.  We do this to
                // allow for efficient writing to multiple log files based on log levels, while still
                // ensuring that we are properly managing file resources and minimizing the overhead
                // of opening and closing files repeatedly during the writing loop.  We will open the
                // writers once before the loop and close them all after we are done writing, which
                // helps to improve performance by reducing file I/O operations and allows us to write
                // messages to multiple log files in a single pass through the queue.
                var writer = new Dictionary<LogLevel, StreamWriter>();

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
                    if (_msgQueue.IsEmpty)
                        return;

                    // Ensure log file is set up, datetime filename change,
                    // and _fileInfo refreshed, before we start writing.
                    LogSetup();

                    // Open all writers
                    foreach (LogLevel logLevel2 in Enum.GetValues<LogLevel>())
                    {
                        // Only open writers for log levels that are enabled and have a corresponding FileInfo.
                        // We do this to avoid unnecessary file I/O and to ensure that we are only opening files
                        // for log levels that we will actually be writing to, which helps to improve performance
                        // and reduce resource usage.
                        if (!_fileInfo.Keys.Contains(logLevel2))
                            continue;

                        // Open the StreamWriter for the log level and add it to the dictionary.  We do this to
                        // ensure that we have a StreamWriter ready for each log level that we need to write to,
                        // which allows us to write messages to the appropriate log files efficiently during the
                        // writing loop.
                        writer.TryAdd(logLevel2, _fileInfo[logLevel2].AppendText());
                    }

                    // Try to dequeue a message.  If successful, write it to the appropriate log file based on its log level.
                    while (!_stopLogging && _msgQueue.TryDequeue(out (LogLevel logLvl, string Msg) logEntry))
                    {
                        // Loop through all log levels to check which levels are included in the log entry's log level.  We do
                        // this to ensure that messages with multiple log levels (e.g. LogLevel.Application | LogLevel.Error)
                        // are written to all appropriate log files, while still allowing for efficient writing by avoiding
                        // unnecessary checks during the writing loop.
                        foreach (LogLevel lvl in Enum.GetValues<LogLevel>().Where(l => (l is not LogLevel.None and not LogLevel.All) &&
                                                                                    (logEntry.logLvl & l) == l))
                        {
                            if ((_enabledLogLevels & lvl) != lvl)
                                continue;

                            // Check if the log level of the message is enabled for logging.  If it is,
                            // write the message to the appropriate log file using the corresponding
                            // StreamWriter.  We do this to ensure that messages are only written to log
                            // files for levels that are enabled, which helps to reduce overhead and
                            // improve performance, especially if there are many log levels and only a
                            // few are enabled.
                            writer[lvl].WriteLine(logEntry.Msg);
                        }
                    }

                    // No need to Interlocked since we are already in a single-threaded context
                    // within the Task.Run, and this is only accessed here after successfully
                    // setting _isWriting to true, which ensures that only one thread can be
                    // executing this block at a time.  We reset the IO failure count after a
                    // successful write operation to allow for retries if future write operations
                    // encounter issues, while still providing a mechanism to track and handle
                    // repeated failures without prematurely giving up on logging.
                    // Reset IO failure count after a successful write operation
                    _ioFailureCount = 0; 
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
                        // Re-throw the exception to allow it to be handled by
                        // the caller or to crash the application if not handled
                        throw;
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
                    foreach (var w in writer.Values)
                        w?.Close();
                }
            }).ContinueWith(_ => 
            {
                // we are now done writing, so set _isWriting back
                // to false to allow the next write to proceed.
                _startWriting.SetFalse();

                // Check if any messages were queued while we were writing.
                // If so, start another write to process them.
                if (!_msgQueue.IsEmpty)
                    _ = ProcessQueueAsync();
            });
        }
        /// <summary>
        /// Synchronously flushes any remaining log messages in the queue to the appropriate log files. 
        /// This method blocks until all messages have been processed.
        /// </summary>
        public void Flush() => ProcessQueueAsync().Wait();
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
            //if (_activeFileDate.Date.Equals(Now.Date))
            if(!_activeFileDate.TryUpdate(Now.Date))
                return;
            //else
            //{
            //    // Update the log file date and name to reflect the new date.
            //    // We do this before creating the file to ensure that
            //    _activeFileDate.TryUpdate(Now.Date);
            //}

            try
            {
                // Calculate the expiration date for log files based on the current date
                // and the configured retention period.
                var expireDate = Now - KeepLogsFor;

                // Clear the existing FileInfo list and create new FileInfo objects
                // for the new log file.  We do this to ensure that the FileInfo is
                // always up to date and reflects the current log file, even if it
                // doesn't exist yet.
                _fileInfo.Clear();

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

                // Get the log levels that are enabled for logging and create a list of them.  We do this to avoid having
                // to check the enabled log levels repeatedly during the writing loop, which helps to improve performance
                // by reducing the number of checks and allowing us to focus on writing messages to the appropriate log
                // files based on the pre-determined list of enabled log levels.
                var logLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>()
                                .Where(l => l is not LogLevel.None and not LogLevel.All && (_enabledLogLevels & l) == l)
                                .ToList();

                // Loop through all enabled log levels to create a log file for each level.
                // This allows us to separate logs by level and makes it easier to
                // find specific types of log messages.  We do this in the LogSetup()
                // method to ensure that it is done automatically when the date
                // changes, without requiring manual intervention. 
                foreach (LogLevel logLevel in logLevels)
                {
                    // Use a date-based log file name to create a new log file each day.
                    // This helps to keep log files manageable and makes it easier to find logs for specific dates.
                    var logFile = $"{_activeFileDate.Year:00}-{_activeFileDate.Month:00}-{_activeFileDate.Day:00}_{logLevel}.log";

                    // Combine the log path and file name to get the full path to the log file.
                    var fullFile = Path.Combine(LogPath, logFile);

                    // Add the FileInfo for the log file to the dictionary for later use when writing messages.
                    // We do this to ensure that we have quick access to the FileInfo for each log level when
                    // we need to write messages, which helps to improve performance by avoiding the overhead
                    // of repeatedly creating FileInfo objects during the writing loop.
                    _fileInfo.TryAdd(logLevel, new FileInfo(fullFile));

                    // Refresh the FileInfo to ensure it has the latest information about
                    // the file, especially if it was created or modified by another process.
                    _fileInfo[logLevel].Refresh();

                    // if the log file doesn't exist, create it.  If it does exist, add a
                    // blank line to separate logs from different runs on the same day.
                    if (!_fileInfo[logLevel].Exists)
                    {
                        // Create the file and immediately close it to release
                        // the handle.  We do this to ensure that the file is
                        // created and ready for writing before we start logging messages.
                        _fileInfo[logLevel].Create().Close();
                        // Refresh to update the file info after creation
                        _fileInfo[logLevel].Refresh();
                    }
                    else if (logLevel == LogLevel.Application && _fileInfo[logLevel].Exists)
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
        #region Dispose Pattern
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                this.Flush();
                disposedValue = true;
            }
        }
        ~Logger() => Dispose(disposing: false);
        void IDisposable.Dispose() => this.Dispose();
        /// <summary>
        /// Will call Flush first, then dispose of the logger.  After this is called, the logger 
        /// will be in a stopped state and will not process any more log messages.  Any messages 
        /// that are still in the queue will be flushed to the log files before the logger is fully 
        /// disposed.  Once disposed, the logger should not be used for logging operations, and any 
        /// attempts to log messages will be ignored.  This method ensures that all resources used 
        /// by the logger are properly released and that any pending log messages are written to 
        /// disk before shutdown.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
