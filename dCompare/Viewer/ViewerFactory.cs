using System;
using System.IO;

namespace dCompare.Viewer
{
    public static class ViewerFactory
    {
        public static IViewer CreateViewer(string filePath)
        {
            // For now, we only have a text viewer.
            // In the future, we can add more viewers here.
            return new TextViewer();
        }
    }
}
