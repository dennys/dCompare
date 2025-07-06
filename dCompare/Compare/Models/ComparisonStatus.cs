namespace dCompare.Compare.Models
{
    public enum ComparisonStatus
    {
        Unknown = ' ',
        Identical = '=',
        Modified = '!',
        LeftOnly = '<',
        RightOnly = '>',
        Conflict = 'X' // For 3-way merge potential
    }
}
