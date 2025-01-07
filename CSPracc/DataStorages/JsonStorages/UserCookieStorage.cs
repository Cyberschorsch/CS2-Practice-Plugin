using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CSPracc.DataModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSPracc.DataStorages.JsonStorages
{
    public class UserCookieStorage : JsonStorage<ulong, Dictionary<string,string>>
    {
        public UserCookieStorage(DirectoryInfo cookieStorageDir) : base(new FileInfo(Path.Combine(cookieStorageDir.FullName, $"CookieStorage.json")))
        {
        }


        public void RemoveCookie(ulong userId,string cookieName) 
        {
            if (userId == 0 || cookieName == "")
            {
                return;
            }
            if(!Storage.TryGetValue(userId, out Dictionary<string,string>? userCookieDict))
            {
                return;
            }
            if(userCookieDict == null)
            {
                return;
            }
            userCookieDict.Remove(cookieName);
            Storage[userId] = userCookieDict;
            Save();
            return;
        }

        public bool GetValueOfCookie(ulong userId,string cookieName, out string? cookieValue)
        {
            cookieValue = null;
            if (userId == 0 || cookieName == "")
            {
                return false;
            }         
            if(!Storage.TryGetValue(userId,out Dictionary<string,string>? userCookies))
            {
                return false;
            }
            if(userCookies == null)
            {
                return false;
            }
            if(!userCookies.TryGetValue(cookieName,out cookieValue))
            {
                return false;
            }
            if(cookieValue == null)
            {
                return false;
            }
            return true;
        }

        public bool SetOrAddValueOfCookie(ulong userId, string cookieName, string cookieValue)
        {
            if (userId == 0 || string.IsNullOrEmpty(cookieName) || string.IsNullOrEmpty(cookieValue))
            {
                return false;
            }
            if (!Storage.TryGetValue(userId, out Dictionary<string, string>? userCookies))
            {
                var userCookieDict = new Dictionary<string, string>();
                userCookieDict.Add(cookieName, cookieValue);
                Storage.Add(userId, userCookieDict);
            }
            else
            {
                if (userCookies == null)
                {
                    userCookies = new Dictionary<string, string>();
                    userCookies.Add(cookieName, cookieValue);
                    Storage[userId] = userCookies;
                }
                else
                {
                    userCookies = new Dictionary<string, string>(userCookies); // Clone the existing dictionary to avoid null reference
                    userCookies[cookieName] = cookieValue;
                    Storage[userId] = userCookies; // Update the original variable with the new data
                }
            }
            try
            {
                Save();
            }
            catch (Exception ex)
            {
                // Handle the exception here. For example:
                Console.WriteLine($"An error occurred while saving: {ex.Message}");
            }
            return true;
        }

        public override bool Get(ulong key, out Dictionary<string, string> value)
        {
            if (!Storage.TryGetValue(key, out value))
            {
                return false;
            }
            return true;
        }
    }
}
