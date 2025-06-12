# dCompare


# Development

## Requirements

* Because I use [OpenFolderDialog](https://learn.microsoft.com/zh-tw/dotnet/api/microsoft.win32.openfolderdialog?view=windowsdesktop-8.0) to select the folder, you need to install .NET 8.0 or later.

## 3rd party tools

* [DiffPlex](https://github.com/mmanela/diffplex): provide text file comparison.



## Issues

* The performance is not good, it takes about 2~3 seconds to read 1 directory contains 1000 sub-directories and files.
=> consider to use lazy-loading.
* Cannot change the width of column?
* Cannot use mouse to partial select some text of the text box
