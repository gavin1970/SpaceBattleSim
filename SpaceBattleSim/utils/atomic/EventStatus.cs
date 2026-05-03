using System.Collections.Concurrent;
using System.Text;

namespace Chizl.ThreadSupport
{
    /// <summary>
    /// Provides an array of thread-safe management of boolean values.<br/>
    /// Set will add or update, no need to check if the key exists before setting.<br/>
    /// Get checks if key is missing, false will be returned.<br/>
    /// Delete checks if key is missing, true is still returned.<br/>
    /// </summary>
    /// <remarks>
    /// <code>
    /// readonly EventStatus _eventStatus = new EventStatus();
    /// _eventStatus.Set("Item_Closed", false);
    /// or
    /// readonly EventStatus _eventStatus = new EventStatus("Item_Closed", false);
    /// or
    /// readonly EventStatus _eventStatus = new EventStatus([("Item_Closed", false), ("Item_Ready", true)]);
    /// ...
    /// // Atomically try to update the status of "Item_Closed" to true.
    /// // If false, the event type does not exist or is already true.
    /// if (!_eventStatus.Get("Item_Closed").TrySetTrue())
    ///     return;
    /// </code>
    /// </remarks>
    internal class EventStatus
    {
        // ConcurrentDictionary to store the boolean values for each dynamic string event type,
        // ensuring thread safety for concurrent access and modifications.
        private readonly ConcurrentDictionary<string, bool> _eventStatus = new ConcurrentDictionary<string, bool>();

        public EventStatus() { }
        public EventStatus(string sType, bool defValue) : this()
        {
            _eventStatus.TryAdd(sType.ToLower(), defValue);
        }
        public EventStatus(List<(string sType, bool defValue)> initialStatus) : this()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var (sType, defValue) in initialStatus)
            {
                if(!_eventStatus.TryAdd(sType.ToLower(), defValue))
                    sb.AppendLine($"'{sType}'");
            }

            if (sb.Length > 0)
                throw new ArgumentException($"Duplicate names found:\n{string.Join("\n - ", sb.ToString())}");
        }

        /// <summary>
        /// Retrieves the status associated with the specified event type.
        /// </summary>
        /// <param name="sType">The event type to look up.</param>
        /// <returns>The status of the specified event type, or false if not found.</returns>
        public bool Get(string sType) 
        {
            if(_eventStatus.TryGetValue(sType.ToLower(), out bool aVal))
                return aVal;
            else
                return false;
        }
        /// <summary>
        /// Adding the status value for the specified event type, updating it if it already exists.
        /// </summary>
        /// <param name="sType">The event type key to set.</param>
        /// <param name="newValue">The value to assign to the event type.</param>
        /// <returns>true if the value was changed or added; otherwise, false.</returns>
        public bool Set(string sType, bool newValue)
        {
            // Try to add the new value for the specified event type. If the
            // key does not exist, it will be added and true will be returned.
            if (_eventStatus.TryAdd(sType.ToLower(), newValue))
                return true;

            // It already exists, so update with the opposite of new value.
            // Return true if new value was different than old.
            if (_eventStatus.TryUpdate(sType.ToLower(), newValue, !newValue))
                return true;

            // No update was made, the value is already the same as newValue.
            return false;
        }
        /// <summary>
        /// Removes the status entry with the specified type from the event status collection, if it exists.
        /// </summary>
        /// <param name="sType">The type of the status to remove.</param>
        /// <returns>true if the status was removed or did not exist; otherwise, false.</returns>
        public bool Delete(string sType) => _eventStatus.Keys.Contains(sType.ToLower()) ? _eventStatus.TryRemove(sType.ToLower(), out _) : true;
    }
}