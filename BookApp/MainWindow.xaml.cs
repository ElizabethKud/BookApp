using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using BookApp.Data;
using BookApp.Models;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace BookApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
            optionsBuilder.UseLazyLoadingProxies();

            using var db = new AppDbContext(optionsBuilder.Options);

            var user = db.Users.FirstOrDefault(u => u.Username == LoginUsernameTextBox.Text);

            if (user != null && BCrypt.Net.BCrypt.Verify(LoginPasswordBox.Password, user.PasswordHash))
            {
                MessageBox.Show($"Добро пожаловать, {user.Username}!", "Успешный вход", MessageBoxButton.OK, MessageBoxImage.Information);

                // здесь можно переключиться на другую страницу/окно, если потребуется
            }
            else
            {
                MessageBox.Show("Неверное имя пользователя или пароль", "Ошибка входа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = RegisterUsernameTextBox.Text.Trim();
            string email = RegisterEmailTextBox.Text.Trim();
            string password = RegisterPasswordBox.Password;
            string confirmPassword = RegisterConfirmPasswordBox.Password;

            // Простейшая валидация
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

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
            optionsBuilder.UseLazyLoadingProxies();

            using var db = new AppDbContext(optionsBuilder.Options);

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
            
            // После успешной регистрации
            var mainWindow = new MainWindow();  // Главная форма приложения
            mainWindow.Show();
            this.Close();  // Закрываем окно регистрации
        }
    }
}
