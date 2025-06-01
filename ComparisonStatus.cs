using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dCompare
{
    public enum ComparisonStatus
    {
        Identical,    // ' '
        OnlyInLeft,   // '<'
        OnlyInRight,  // '>'
        Modified,     // '*'
        Conflict,     // '!'
        Unknown       // '?'
    }
    public static class ComparisonSymbol
    {
        public static string GetSymbol(ComparisonStatus status) => status switch
        {
            ComparisonStatus.Identical => " ",
            ComparisonStatus.OnlyInLeft => "<",
            ComparisonStatus.OnlyInRight => ">",
            ComparisonStatus.Modified => "*",
            ComparisonStatus.Conflict => "!",
            ComparisonStatus.Unknown => "?",
            _ => "?"
        };
    }
        public enum ComparisonStatus3
    {
        Unknown,        // Default state, no comparison done
        Identical,      // Same file/folder in both directories
        OnlyInLeft,     // Exists only in left directory
        OnlyInRight,    // Exists only in right directory
        OnlyInBase,     // Exists only in base (third) directory
        ModifiedLeft,   // Modified in left compared to base
        ModifiedRight,  // Modified in right compared to base
        Conflict        // Modified in both left and right (conflict)
    }
}
