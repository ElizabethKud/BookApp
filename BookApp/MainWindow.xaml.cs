using System.Windows;
using System.Windows.Controls;
using BookApp.ViewModels;

namespace BookApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = (MainViewModel)DataContext;
        }

        private void LoginPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentView is LoginRegisterViewModel vm)
            {
                vm.LoginPassword = ((PasswordBox)sender).Password;
            }
        }

        private void RegisterPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentView is LoginRegisterViewModel vm)
            {
                vm.RegisterPassword = ((PasswordBox)sender).Password;
            }
        }

        private void RegisterConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentView is LoginRegisterViewModel vm)
            {
                vm.RegisterConfirmPassword = ((PasswordBox)sender).Password;
            }
        }
    }
}