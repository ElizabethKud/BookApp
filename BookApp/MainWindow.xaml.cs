using System.Windows;

namespace BookApp
{
    public partial class MainWindow : Window
    {
        private string currentUser;

        public MainWindow(string username)
        {
            InitializeComponent();
            currentUser = username;
            NicknameTextBox.Text = currentUser;

            // Заполнение комбобоксов цветами
            BackgroundColorComboBox.Items.Add("Белый");
            BackgroundColorComboBox.Items.Add("Черный");
            FontColorComboBox.Items.Add("Черный");
            FontColorComboBox.Items.Add("Белый");
            FontFamilyComboBox.Items.Add("Arial");
            FontFamilyComboBox.Items.Add("Times New Roman");

            // Настроим начальные значения
            BackgroundColorComboBox.SelectedIndex = 0;
            FontColorComboBox.SelectedIndex = 0;
            FontFamilyComboBox.SelectedIndex = 0;
        }

        private void DefaultBooks_Click(object sender, RoutedEventArgs e)
        {
            // Загрузка базовых книг
            MessageBox.Show("Загрузка базовых книг...");
        }

        private void OpenBook_Click(object sender, RoutedEventArgs e)
        {
            // Открытие книги через проводник
            MessageBox.Show("Открытие книги...");
        }

        private void Favorites_Click(object sender, RoutedEventArgs e)
        {
            // Отображение избранных книг
            MessageBox.Show("Избранные книги...");
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            // Отображение истории чтения
            MessageBox.Show("История чтения...");
        }

        private void SaveNickname_Click(object sender, RoutedEventArgs e)
        {
            // Сохранение ника
            currentUser = NicknameTextBox.Text;
            MessageBox.Show("Ник сохранен: " + currentUser);
        }
    }
}