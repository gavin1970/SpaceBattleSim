using System.Collections.Concurrent;
using System.Configuration;

namespace Chizl.Applications
{
    public static class AppConfig
    {
        private static ConcurrentDictionary<string, string> _configValues = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// reads the config and convert it to a generic type if possible.   
        /// NOTE string is default if no generic is passed in.
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="nval"></param>
        /// <param name="reload"></param>
        /// <returns></returns>
        public static bool GetConfigValue<T>(string setting, out T nval, bool reload = false)
        {
            bool retVal = true;
            nval = default(T);

            //load and save value in memory as string.
            if (GetConfigValue(setting, out string value, reload, false))
            {
                try
                {
                    //convert string to generic request.
                    nval = (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    //set default, since the value isn't a validate value for this generic.
                    nval = default(T);
                    retVal = false;
                }
            }

            return (retVal && nval != null);
        }
        /// <summary>
        /// read configuration setting
        /// </summary>
        /// <param name="setting"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        static private bool GetConfigValue(string setting, out string value, bool reload = false, bool secondAttempt = false)
        {
            bool retVal = true;
            string newVal = "";
            value = string.Empty;

            try
            {
                //force a reload?
                if (reload)
                {
                    //get settings from config
                    newVal = ConfigurationManager.AppSettings[setting] ?? string.Empty;

                    if (newVal == string.Empty) 
                        return false;

                    lock (_configValues)
                    {
                        //if exists in dictionary, delete it
                        if(!_configValues.TryAdd(setting, newVal))
                            _configValues.TryUpdate(setting, newVal, _configValues[setting]);  //update value in dictionary
                    }
                }
                else if (_configValues.ContainsKey(setting))
                {
                    lock (_configValues)
                    {
                        //pull from dictionary
                        newVal = _configValues[setting].ToString().Trim();
                    }
                }
                else
                {
                    //call myself with a refresh, because it wasn't found in the 
                    //dictionary and hasn't been pulled from config yet.
                    retVal = GetConfigValue(setting, out newVal, true, true);
                }

                value = newVal;
            }
            catch (FileNotFoundException)
            {
                retVal = false;
            }
            catch (Exception)
            {
                retVal = false;
                //ensure this doesn't get caught in an infinite loop.
                if (!secondAttempt)
                {
                    //break, then try again..  
                    Task.Delay(10).Wait();
                    //If config was being updated by external apps, it could fail..  This allows it to retry only a second time, then quit.
                    retVal = GetConfigValue(setting, out newVal, reload, true);
                }
            }

            return (retVal && value != null);
        }
    }
}
