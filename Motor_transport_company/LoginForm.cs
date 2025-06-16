using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Npgsql;

namespace Motor_transport_company
{
    public partial class LoginForm : Form
    {
        private readonly string _connectionString = "Host=172.20.7.53;Port=5432;Database=db3996_17;Username=root;Password=root";

        public LoginForm()
        {
            InitializeComponent();
        }

        private static string HashPassword(string password)
        {
            byte[] hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashBytes);
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Введите имя пользователя и пароль", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string hashedPassword = HashPassword(txtPassword.Text);

                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(
                    "SELECT id, username, role_id FROM transport_company.\"user\" WHERE username=@username AND password=@password AND is_active=true",
                    connection);

                cmd.Parameters.AddWithValue("@username", txtUsername.Text);
                cmd.Parameters.AddWithValue("@password", hashedPassword);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int userId = reader.GetInt32(0);
                    string username = reader.GetString(1);
                    int roleId = reader.GetInt32(2);

                    var mainForm = new MainForm(userId, username, roleId, _connectionString);
                    Hide();
                    mainForm.Show();
                }
                else
                {
                    MessageBox.Show("Неверное имя пользователя или пароль", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "28P01")
            {
                MessageBox.Show("Ошибка аутентификации. Проверьте параметры подключения.", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            var registerForm = new RegisterForm(_connectionString);
            registerForm.ShowDialog();
        }

        private void LoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }
    }
}