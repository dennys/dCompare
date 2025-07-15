using dCompare.Configuration;
using System;
using System.IO;

namespace dCompare.Viewer
{
    public class ViewerFactory
    {
        private readonly IConfigurationService _configurationService;

        public ViewerFactory(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public IViewer CreateViewer(string filePath)
        {
            // For now, we only have a text viewer.
            // In the future, we can add more viewers here.
            return new TextViewer(_configurationService);
        }
    }
}
