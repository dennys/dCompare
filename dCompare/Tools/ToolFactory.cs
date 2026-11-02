using CommunityToolkit.Mvvm.DependencyInjection;
using dCompare.Configuration;
using dCompare.Properties;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;

namespace dCompare.Tools
{
    public class ToolFactory
    {
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<ToolFactory> _logger;

        public ToolFactory(IConfigurationService configurationService, ILogger<ToolFactory> logger)
        {
            _configurationService = configurationService;
            _logger = logger;
        }

        public async Task<ITool?> CreateToolAsync(string filePath)
        {
            var config = await _configurationService.LoadConfigurationAsync();
            string fileType = DetermineFileType(config, filePath);

            if (!config.Options.FileTypeMapping.TryGetValue(fileType, out var mapping))
            {
                _logger.LogWarning("File type '{fileType}' determined but not found in FileTypeMapping.", fileType);
                return null;
            }

            string toolName = mapping.DefaultViewTool;
            if (string.IsNullOrEmpty(toolName) || !config.Options.Tools.TryGetValue(toolName, out var toolOptions))
            {
                // This is a configuration issue, but not critical enough to halt execution with an exception.
                // The handler will show a message to the user.
                _logger.LogWarning("No default view tool found for file type '{fileType}'.", fileType);
                return null;
            }

            return CreateToolFromHandler(toolOptions.Handler, filePath);
        }

        public async Task<ITool?> CreateToolAsync(string leftFilePath, string rightFilePath)
        {
            var config = await _configurationService.LoadConfigurationAsync();
            string fileType = DetermineFileType(config, leftFilePath ?? rightFilePath);

            if (!config.Options.FileTypeMapping.TryGetValue(fileType, out var mapping))
            {
                _logger.LogWarning("File type '{fileType}' determined but not found in FileTypeMapping.", fileType);
                return null;
            }

            string toolName = mapping.DefaultDiffTool;

            if (string.IsNullOrEmpty(toolName) || !config.Options.Tools.TryGetValue(toolName, out var toolOptions))
            {
                _logger.LogWarning("No default diff tool found for file type '{fileType}'.", fileType);
                return null;
            }
            return CreateToolFromHandler(toolOptions.Handler, leftFilePath, rightFilePath);
        }

        private ITool CreateToolFromHandler(string handler, params object[] args)
        {
            try
            {
                if (string.IsNullOrEmpty(handler))
                {
                    throw new ToolCreationException(Resources.ErrorToolHandlerNotSpecified);
                }

                Type? type = Type.GetType(handler);
                if (type == null)
                {
                    throw new ToolCreationException(string.Format(Resources.ErrorToolTypeNotFound, handler));
                }

                // Only dCompare's own ITool implementations may be constructed: the handler string comes
                // from a config file, and constructing an arbitrary type would run its constructor
                // (StreamWriter truncating the user's file) before the ITool cast below could fail.
                if (!typeof(ITool).IsAssignableFrom(type) || type.Assembly != typeof(ITool).Assembly)
                {
                    throw new ToolCreationException(string.Format(Resources.ErrorToolHandlerNotBuiltInTool, handler));
                }

                ConstructorInfo[] constructors = type.GetConstructors();
                Ioc serviceProvider = Ioc.Default;

                // Find potential constructors where all parameters can be satisfied by the provided 'args' or the DI container.
                var candidateConstructors = constructors
                    .Select(c => new { Constructor = c, Parameters = c.GetParameters() })   // ① 把每個建構式包成匿名物件，附上它的參數資訊
                    .Where(x =>
                    {
                        List<object> usedArgs = new();  // 用來記錄已經配對過的 args，避免重複使用
                        return x.Parameters.All(p =>        // 檢查這個建構式的所有參數
                        {
                            var arg = args.FirstOrDefault(a => p.ParameterType.IsInstanceOfType(a) && !usedArgs.Contains(a));   // 嘗試從 args 中找到一個符合參數型別的物件，且尚未用過
                            if (arg != null)
                            {
                                usedArgs.Add(arg);  // 標記這個 arg 已經被用掉
                                return true;        // 這個參數可滿足
                            }
                            return serviceProvider.GetService(p.ParameterType) != null; // 如果 args 裡沒有，就嘗試從 serviceProvider 取得這個型別的服務
                        });
                    })
                    .ToList();

                // Prefer the constructor with the most parameters (most specific).
                var constructorInfo = candidateConstructors
                    .OrderByDescending(x => x.Parameters.Length)
                    .FirstOrDefault();

                var constructor = constructorInfo?.Constructor;

                if (constructor != null)
                {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    var constructorArgs = new object[parameters.Length];
                    List<object> usedArgs = new();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        ParameterInfo paramInfo = parameters[i];
                        var providedArg = args.FirstOrDefault(a => paramInfo.ParameterType.IsInstanceOfType(a) && !usedArgs.Contains(a));

                        if (providedArg != null)
                        {
                            constructorArgs[i] = providedArg;
                            usedArgs.Add(providedArg);
                        }
                        else
                        {
                            constructorArgs[i] = serviceProvider.GetService(paramInfo.ParameterType)
                                ?? throw new ToolCreationException(string.Format(Resources.ErrorCouldNotResolveServiceForTool, paramInfo.ParameterType.FullName, handler));
                        }
                    }
                    return (ITool)constructor.Invoke(constructorArgs);
                }

                throw new ToolCreationException(string.Format(Resources.ErrorNoSuitableConstructorForTool, handler));
            }
            catch (Exception ex) when (ex is not ToolCreationException)
            {
                throw new ToolCreationException(string.Format(Resources.ErrorUnexpectedErrorCreatingTool, handler), ex);
            }
        }

        public string DetermineFileType(AppConfiguration config, string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "Unsupported";
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            foreach (var mapping in config.Options.FileTypeMapping)
            {
                if (mapping.Value.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    return mapping.Key;
                }
            }
            return "Unsupported";
        }
    }
}
