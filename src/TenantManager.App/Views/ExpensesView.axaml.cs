using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TenantManager.App.ViewModels;

namespace TenantManager.App.Views
{
    public partial class ExpensesView : UserControl
    {
        public ExpensesView()
        {
            InitializeComponent();
        }

        private async void BrowseFile_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Invoice PDF",
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
                vm.ExpenseList.EditFilePath = path;

                using var stream = await file.OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                vm.ExpenseList.EditFileContent = ms.ToArray();
            }
        }
    }
}
