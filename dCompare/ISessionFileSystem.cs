using System.Collections.Generic;

namespace dCompare
{
    public interface ISessionFileSystem
    {
        bool FileExists(string path);
        string ReadAllText(string path);
        void WriteAllText(string path, string contents);
    }
}