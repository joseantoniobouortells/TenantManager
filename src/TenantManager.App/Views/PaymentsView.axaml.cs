using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using TenantManager.App.ViewModels;

namespace TenantManager.App.Views
{
    public partial class PaymentsView : UserControl
    {
        public PaymentsView()
        {
            InitializeComponent();
        }

        private void ListBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                if (sender is ListBox listBox && DataContext is MainViewModel mainViewModel)
                {
                    var selectedItems = listBox.SelectedItems?.Cast<PaymentDisplayItem>().ToList();
                    if (selectedItems != null && selectedItems.Any())
                    {
                        mainViewModel.PaymentList.DeletePayments(selectedItems);
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
