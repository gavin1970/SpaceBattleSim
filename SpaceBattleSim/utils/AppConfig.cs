using System.Collections.Concurrent;
using System.ComponentModel;
using System.Configuration;

namespace Chizl.Configurations
{
    /// <summary>
    /// Provides static methods for retrieving and managing application configuration settings in a thread-safe manner.
    /// </summary>
    /// <remarks>The AppConfig class enables access to configuration values with support for type conversion,
    /// caching, and safe concurrent access. It is designed for use in multi-threaded applications where configuration
    /// values may be read frequently and updated at runtime. All members are static and the class cannot be
    /// instantiated.</remarks>
    public static class AppConfig
    {
        /// <summary>
        /// Provides a thread-safe collection for storing configuration key-value pairs.
        /// </summary>
        /// <remarks>This dictionary allows concurrent access and updates to configuration values,
        /// ensuring safe usage in multi-threaded scenarios.</remarks>
        private static ConcurrentDictionary<string, string> _configValues = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Attempts to retrieve a configuration value by key and convert it to the specified type.
        /// </summary>
        /// <remarks>If the configuration value cannot be converted to the specified type, the method
        /// returns false and sets <paramref name="nval"/> to the default value for type <typeparamref
        /// name="T"/>.</remarks>
        /// <typeparam name="T">The type to which the configuration value should be converted.</typeparam>
        /// <param name="setting">The key of the configuration setting to retrieve.</param>
        /// <param name="nval">When this method returns, contains the value of the configuration setting converted to type <typeparamref
        /// name="T"/> if the operation succeeds; otherwise, the default value for type <typeparamref name="T"/>.</param>
        /// <param name="reload">true to force reloading the configuration value from the source; otherwise, false to use a cached value if
        /// available.</param>
        /// <returns>true if the configuration value was found and successfully converted to the specified type; otherwise,
        /// false.</returns>
        public static bool GetConfigValue<T>(string setting, out T? nval, bool reload = false)
        {
            nval = default;

            try
            {
                //load and save value in memory as string.
                if (GetConfigValue(setting, out string value, reload, false))
                {
                    // attempt conversion, if it fails, it will return the default value for the type, which is what we want in this case.
                    nval = ConvertType<T>(value, nval);
                    return true;
                }
            }
            catch
            {
                // just in case
                nval = default;
            }

            return false;
        }
        /// <summary>
        /// Retrieves the value of the specified configuration setting from the application's configuration file or
        /// cache.
        /// </summary>
        /// <remarks>If the setting is not found in the cache or if reload is true, the method attempts to
        /// read the value from the application's configuration file. If the configuration file is temporarily
        /// unavailable (for example, due to being updated by another process), the method retries once before throwing
        /// an exception. If the configuration file is missing, the method returns false without throwing an
        /// exception.</remarks>
        /// <param name="setting">The name of the configuration setting to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified setting, or an empty string if
        /// the setting is not found.</param>
        /// <param name="reload">true to force reloading the value from the configuration file; false to use the cached value if available.
        /// The default is false.</param>
        /// <param name="secondAttempt">Indicates whether this is a retry attempt after a previous failure. This parameter is used internally to
        /// control retry logic and should typically be left as false.</param>
        /// <returns>true if the configuration value was successfully retrieved; otherwise, false.</returns>
        private static bool GetConfigValue(string setting, out string value, bool reload = false, bool secondAttempt = false)
        {
            bool retVal = true;
            value = string.Empty;

            try
            {
                if (!reload && _configValues.TryGetValue(setting, out value))
                    return true;
                else
                {
                    // if the setting doesn't exist, this will throw an exception,
                    // which is caught below, and handled by retrying once.
                    value = ConfigurationManager.AppSettings[setting] ?? string.Empty;

                    // if the value is empty, return false, since this is likely an issue
                    // with the config file, and not just a temporary lock on the file.
                    if (value == string.Empty)
                        return false;

                    // try to add value to dictionary, if it already exists, update it.
                    if (!_configValues.TryAdd(setting, value.Trim()))
                        _configValues.TryUpdate(setting, value.Trim(), _configValues[setting]);  //update value in dictionary
                }
            }
            catch (FileNotFoundException)
            {
                // if the config file is missing, we can't do anything about it, so just return false.
                retVal = false;
            }
            catch (Exception)
            {
                retVal = false;
                // if the config file was being updated by external apps, it could fail..  This allows it to retry only a second time, then quit.
                if (!secondAttempt)
                {
                    Task.Delay(10).Wait();
                    //If config was being updated by external apps, it could fail..  This allows it to retry only a second time, then quit.
                    retVal = GetConfigValue(setting, out value, reload, true);
                }
                else
                    throw;  // if it fails again, throw the exception, since this
                            // is likely an actual issue with the config file, and
                            // not just a temporary lock on the file.
            }

            // if we got here, it means we successfully retrieved a value, but it may be empty, so check for that.
            return (retVal && value != null);
        }
        /// <summary>
        /// Converts the specified string to the specified type, returning a default value if the conversion fails.
        /// </summary>
        /// <remarks>This method attempts to convert the input string to the specified type using a type
        /// converter if available, or falls back to standard type conversion. If the conversion cannot be performed,
        /// the provided default value is returned instead of throwing an exception.</remarks>
        /// <typeparam name="T">The type to which to convert the input string.</typeparam>
        /// <param name="value">The string value to convert. If null or whitespace, the default value is returned.</param>
        /// <param name="defValue">The value to return if the conversion is unsuccessful or the input is null or whitespace.</param>
        /// <returns>The converted value of type T if the conversion succeeds; otherwise, the specified default value.</returns>
        private static T? ConvertType<T>(string value, object? defValue)
        {
            // if the value is null or whitespace, return the default value for the type.
            if (string.IsNullOrWhiteSpace(value)) return defValue != null ? (T)defValue : default(T)!;

            try
            {
                // Trim values
                value = value.Trim();

                // Use TypeDescriptor to find a converter for the target type and attempt to convert the string value.
                var converter = TypeDescriptor.GetConverter(typeof(T));

                // If a converter exists and can convert from string, use it to perform the conversion.
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                    return (T)converter.ConvertFromString(value);

                // Fallback to ChangeType for types TypeDescriptor might miss
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                // If any exception occurs during conversion, return the default value for the type.
                return (T)defValue;
            }
        }
    }
}
