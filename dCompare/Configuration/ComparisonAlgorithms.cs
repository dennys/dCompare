using System.Collections.Generic;

namespace dCompare.Configuration
{
    // I want to redesign comparison algorithm, I want to add more and let users to enable/disable some and pick up 1 default
    // The default status is hardcode and user can change it on optionswindos, this is my proposal,
    // if you have better design, just do it. and keep it in a single left, don't share with Double Click Action
    // ModifiedDateAndSize, N/A, a checkbox to enable/disable, a shared radio button
    // xxHash64, percentage(100%), a checkbox to enable/disable, a shared radio button
    // xxHash64, percentage(30%), a checkbox to enable/disable, a shared radio button
    // MD5 disable, percentage(100%), a checkbox to enable/disable, a shared radio button
    // FullContents, percentage(100%), a checkbox to enable/disable, a shared radio button
    // percentage means to get only xx% to compare, for ModifiedDateAndSize, it's meanless


    public enum ComparisonAlgorithm
    {
        ModifiedDateAndSize,
        xxHash64,
        MD5,
        FullContents
    }

    public static class ComparisonAlgorithms
    {
        public static readonly List<KeyValuePair<string, ComparisonAlgorithm>> AvailableAlgorithms =
        [
            new KeyValuePair<string, ComparisonAlgorithm>("Quick (Modified Date and Size)", ComparisonAlgorithm.ModifiedDateAndSize),
            new KeyValuePair<string, ComparisonAlgorithm>("Hash (xxHash64)", ComparisonAlgorithm.xxHash64),
            new KeyValuePair<string, ComparisonAlgorithm>("Hash (MD5)", ComparisonAlgorithm.MD5),
            new KeyValuePair<string, ComparisonAlgorithm>("Full Contents)", ComparisonAlgorithm.FullContents)
        ];
    }
}