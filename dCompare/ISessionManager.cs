using System;
using System.Windows.Controls;

namespace dCompare
{
    public interface ISessionManager
    {
        void Initialize(TabControl tabControl, MenuItem recentSessionsMenu);
        void SaveSession(Action<string> updateStatus);
        bool CanSaveSession();
        void LoadSession(string filePath = null, bool executeCompare = false, Action<string> updateStatus = null, Action<string> executeCompareMethod = null);
        bool CanLoadSession();
        void AddRecentSession(string filePath);
        void ClearRecentSessions();
        bool CanClearRecentSessions();
        int TabCounter { get; set; }
        System.Collections.Generic.Dictionary<string, string> TabIdMap { get; }
    }
}