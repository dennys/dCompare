using dCompare.Compare;
using System.Windows.Controls;

namespace dCompare.Session
{
    public interface ISessionManager
    {
        void Initialize(TabControl tabControl, MenuItem recentSessionsMenu);
        void SaveSession(Action<string> updateStatus);
        bool CanSaveSession();
        bool LoadSession(string filePath, bool executeCompare = false, Action<string>? updateStatus = null, Action<CompareControl>? executeCompareMethod = null);
        bool CanLoadSession();
        void AddRecentSession(string filePath);
        void ClearRecentSessions();
        bool CanClearRecentSessions();
        int TabCounter { get; set; }
        Dictionary<TabItem, string> TabIdMap { get; }
    }
}