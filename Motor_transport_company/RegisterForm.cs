using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Npgsql;

namespace Motor_transport_company
{
    public partial class RegisterForm : Form
    {
        private readonly string _connectionString;

        public RegisterForm(string connectionString)
        {
            InitializeComponent();
            _connectionString = connectionString;
        }

        private static string HashPassword(string password)
        {
            byte[] hashBytes = SHA512.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashBytes);
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) ||
                string.IsNullOrWhiteSpace(txtPassword.Text) ||
                string.IsNullOrWhiteSpace(txtEmail.Text))
            {
                MessageBox.Show("Заполните все поля", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (txtPassword.Text != txtConfirmPassword.Text)
            {
                MessageBox.Show("Пароли не совпадают", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string hashedPassword = HashPassword(txtPassword.Text);

                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(
                    "INSERT INTO transport_company.\"user\" (username, password, email, role_id) " +
                    "VALUES (@username, @password, @email, 4)",
                    connection);

                cmd.Parameters.AddWithValue("@username", txtUsername.Text);
                cmd.Parameters.AddWithValue("@password", hashedPassword);
                cmd.Parameters.AddWithValue("@email", txtEmail.Text);

                int rowsAffected = cmd.ExecuteNonQuery();
                if (rowsAffected > 0)
                {
                    MessageBox.Show("Регистрация успешна", "Успех",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                MessageBox.Show("Пользователь с таким именем или email уже существует", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка регистрации: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            var loginForm = new LoginForm();
            loginForm.Show();
            Close();
        }
    }
}