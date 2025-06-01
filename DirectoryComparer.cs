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
            var leftItems = FlattenDirectory(leftPath, 0);
            var rightItems = FlattenDirectory(rightPath, 0);

            var map = new Dictionary<string, (FileSystemNode, FileSystemNode)>();

            foreach (var node in leftItems)
            {
                map[node.FullPath.Replace(leftPath, "")] = (node, null);
            }

            foreach (var node in rightItems)
            {
                var key = node.FullPath.Replace(rightPath, "");
                if (map.ContainsKey(key))
                    map[key] = (map[key].Item1, node);
                else
                    map[key] = (null, node);
            }

            var result = new List<(FileSystemNode, FileSystemNode)>();

            foreach (var kvp in map)
            {
                var left = kvp.Value.Item1;
                var right = kvp.Value.Item2;

                if (left != null && right != null)
                {
                    if (left.Size == right.Size && left.Modified == right.Modified)
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

                result.Add((left, right));
            }

            return result.OrderBy(r => r.Item1?.FullPath ?? r.Item2?.FullPath).ToList();
        }

        private static List<FileSystemNode> FlattenDirectory(string path, int depth)
        {
            var result = new List<FileSystemNode>();

            foreach (var dir in Directory.GetDirectories(path))
            {
                var info = new DirectoryInfo(dir);
                result.Add(new FileSystemNode
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    Modified = info.LastWriteTime,
                    Size = 0,
                    IsDirectory = true,
                    Depth = depth
                });
                result.AddRange(FlattenDirectory(dir, depth + 1));
            }

            foreach (var file in Directory.GetFiles(path))
            {
                var info = new FileInfo(file);
                result.Add(new FileSystemNode
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    Modified = info.LastWriteTime,
                    Size = info.Length,
                    IsDirectory = false,
                    Depth = depth
                });
            }

            return result;
        }
    }
}
