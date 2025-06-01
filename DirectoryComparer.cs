using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dCompare
{
    public static class DirectoryComparer
    {
        public static List<(FileSystemNode Left, FileSystemNode Right)> CompareDirectories(string leftPath, string rightPath)
        {
            var leftItems = LoadDirectory(leftPath);
            var rightItems = LoadDirectory(rightPath);

            var results = new List<(FileSystemNode, FileSystemNode)>();

            var allNames = new HashSet<string>(leftItems.Select(f => f.Name));
            allNames.UnionWith(rightItems.Select(f => f.Name));

            foreach (var name in allNames)
            {
                var left = leftItems.FirstOrDefault(f => f.Name == name);
                var right = rightItems.FirstOrDefault(f => f.Name == name);

                if (left != null && right != null)
                {
                    if (left.Size == right.Size && left.Created == right.Created)
                    {
                        left.Status = right.Status = ComparisonStatus.Identical;
                    }
                    else
                    {
                        left.Status = right.Status = ComparisonStatus.Modified;
                    }
                }
                else if (left != null)
                {
                    left.Status = ComparisonStatus.OnlyInLeft;
                }
                else if (right != null)
                {
                    right.Status = ComparisonStatus.OnlyInRight;
                }

                results.Add((left, right));
            }

            return results;
        }

        private static List<FileSystemNode> LoadDirectory(string path)
        {
            var list = new List<FileSystemNode>();
            if (!Directory.Exists(path)) return list;

            foreach (var file in Directory.GetFiles(path))
            {
                var info = new FileInfo(file);
                list.Add(new FileSystemNode
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    Size = info.Length,
                    Created = info.CreationTime,
                    IsDirectory = false
                });
            }

            return list;
        }
    }
}
