# dCompare


# Development

## Requirements

* Because I use [OpenFolderDialog](https://learn.microsoft.com/zh-tw/dotnet/api/microsoft.win32.openfolderdialog?view=windowsdesktop-8.0) to select the folder, you need to install .NET 8.0 or later.

## 3rd party tools

* [DiffPlex](https://github.com/mmanela/diffplex): provide text file comparison.



## Issues

* Cannot close tab

* Cannot change the width of column?
* Cannot use mouse to partial select some text of the text box

							<DataTrigger Binding="{Binding Type}" Value="Inserted">
                                <Setter Property="Background" Value="#DDFFDD"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding Type}" Value="Deleted">
                                <Setter Property="Background" Value="#FFDDDD"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding Type}" Value="Imaginary">
                                <Setter Property="Background" Value="#E0E0E0"/>
                            </DataTrigger>