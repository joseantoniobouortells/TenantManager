using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System.IO;
using TenantManager.App.ViewModels;

namespace TenantManager.App.Views
{
    public partial class ContractsView : UserControl
    {
        public ContractsView()
        {
            InitializeComponent();
        }

        private async void BrowseFile_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select PDF Contract",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PDF Documents") { Patterns = new[] { "*.pdf" } }
                }
            });

            if (files.Count >= 1 && DataContext is MainViewModel vm)
            {
                var file = files[0];
                var path = file.Path.LocalPath;
                vm.ContractList.EditFilePath = path;

                using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                vm.ContractList.EditFileContent = ms.ToArray();
            }
        }
        private async void BrowseExtensionFile_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select PDF Extension",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("PDF Documents") { Patterns = new[] { "*.pdf" } }
                }
            });

            if (files.Count >= 1 && DataContext is MainViewModel vm)
            {
                var file = files[0];
                var path = file.Path.LocalPath;
                vm.ContractList.ExtensionEditFilePath = path;

                using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                vm.ContractList.ExtensionEditFileContent = ms.ToArray();
            }
        }
    }
}
