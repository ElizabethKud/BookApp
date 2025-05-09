using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using BookApp.Data;
using BookApp.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace BookApp.ViewModels
{
    public class LoginRegisterViewModel : BaseViewModel
    {
        private readonly Action<int> _onLoginSuccess;
        private string _loginUsername;
        private string _loginPassword;
        private string _registerUsername;
        private string _registerEmail;
        private string _registerPassword;
        private string _registerConfirmPassword;

        public string LoginUsername
        {
            get => _loginUsername;
            set { _loginUsername = value; OnPropertyChanged(); }
        }

        public string LoginPassword
        {
            get => _loginPassword;
            set { _loginPassword = value; OnPropertyChanged(); }
        }

        public string RegisterUsername
        {
            get => _registerUsername;
            set { _registerUsername = value; OnPropertyChanged(); }
        }

        public string RegisterEmail
        {
            get => _registerEmail;
            set { _registerEmail = value; OnPropertyChanged(); }
        }

        public string RegisterPassword
        {
            get => _registerPassword;
            set { _registerPassword = value; OnPropertyChanged(); }
        }

        public string RegisterConfirmPassword
        {
            get => _registerConfirmPassword;
            set { _registerConfirmPassword = value; OnPropertyChanged(); }
        }

        public RelayCommand LoginCommand { get; }
        public RelayCommand RegisterCommand { get; }

        public LoginRegisterViewModel(Action<int> onLoginSuccess)
        {
            _onLoginSuccess = onLoginSuccess;
            LoginCommand = new RelayCommand(Login);
            RegisterCommand = new RelayCommand(Register);
        }

        private void Login()
        {
            using var db = CreateDbContext();
            var user = db.Users.FirstOrDefault(u => u.Username == LoginUsername);

            if (user != null && BCrypt.Net.BCrypt.Verify(LoginPassword, user.PasswordHash))
            {
                MessageBox.Show($"Добро пожаловать, {user.Username}!", "Успешный вход", MessageBoxButton.OK, MessageBoxImage.Information);
                _onLoginSuccess(user.Id);
            }
            else
            {
                MessageBox.Show("Неверное имя пользователя или пароль", "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Register()
        {
            string username = RegisterUsername?.Trim();
            string email = RegisterEmail?.Trim();
            string password = RegisterPassword;
            string confirmPassword = RegisterConfirmPassword;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("Заполните все поля", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Regex.IsMatch(email, @"^\S+@\S+\.\S+$"))
            {
                MessageBox.Show("Неверный формат email", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var db = CreateDbContext();
            if (db.Users.Any(u => u.Username == username || u.Email == email))
            {
                MessageBox.Show("Пользователь с таким именем или email уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "user",
                RegistrationDate = DateTime.UtcNow
            };

            db.Users.Add(newUser);
            db.SaveChanges();

            MessageBox.Show("Регистрация успешна!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            _onLoginSuccess(newUser.Id);
        }

        private AppDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
            optionsBuilder.UseLazyLoadingProxies();
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}