using System;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using Npgsql;

namespace Motor_transport_company
{
    public partial class MainForm : Form
    {
        private readonly int _userId;
        private readonly string _username;
        private readonly int _roleId;
        private readonly string _connectionString;
        private DataTable _tripsTable;
        private DataTable _originalTripsTable;
        private bool _isEditing = false;

        public MainForm(int userId, string username, int roleId, string connectionString)
        {
            InitializeComponent();
            InitializeFilters();
            _userId = userId;
            _username = username;
            _roleId = roleId;
            _connectionString = connectionString;

            LoadTripsData();
            SetupAccessControls();
            SetupDataGridView();
        }

        private void SetupDataGridView()
        {
            dataGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView.ReadOnly = true;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.CellClick += DataGridView_CellClick;
        }

        private void SetupAccessControls()
        {
            if (_roleId == 4) // client
            {
                btnIAdd.Visible = false;
                btnEdit.Visible = false;
                btnDelete.Visible = false;
                btnSave.Visible = false;
                btnCancel.Visible = false;
            }
            else
            {
                btnSave.Visible = false;
                btnCancel.Visible = false;
            }
        }

        private void InitializeFilters()
        {
            cmbStatusFilter.Items.AddRange(new[] { "Все", "запланировано", "выполняется", "выполнено", "отменено" });
            cmbStatusFilter.SelectedIndex = 0;
        }

        private void LoadTripsData(string statusFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                string query = @"SELECT t.id, r.number as route_number, 
                               v.brand || ' ' || v.model as vehicle, 
                               d.last_name || ' ' || d.first_name as driver,
                               t.departure_time, t.arrival_time, t.status::text as status
                               FROM transport_company.trip t
                               JOIN transport_company.route r ON t.route_id = r.id
                               JOIN transport_company.vehicle v ON t.vehicle_id = v.id
                               JOIN transport_company.driver d ON t.driver_id = d.id
                               WHERE 1=1";

                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Все")
                    query += " AND t.status::text = @status";

                if (dateFrom.HasValue)
                    query += " AND t.departure_time >= @date_from";

                if (dateTo.HasValue)
                    query += " AND t.departure_time <= @date_to";

                query += " ORDER BY t.departure_time";

                using var adapter = new NpgsqlDataAdapter(query, connection);

                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Все")
                    adapter.SelectCommand.Parameters.AddWithValue("@status", statusFilter);

                if (dateFrom.HasValue)
                    adapter.SelectCommand.Parameters.AddWithValue("@date_from", dateFrom.Value);

                if (dateTo.HasValue)
                    adapter.SelectCommand.Parameters.AddWithValue("@date_to", dateTo.Value.AddDays(1));

                _tripsTable = new DataTable();
                adapter.Fill(_tripsTable);

                _originalTripsTable = _tripsTable.Copy();

                dataGridView.DataSource = _tripsTable;
                dataGridView.Columns["id"].Visible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                var routeId = GetFirstId(connection, "route");
                var vehicleId = GetFirstId(connection, "vehicle");
                var driverId = GetFirstId(connection, "driver");

                if (routeId == 0 || vehicleId == 0 || driverId == 0)
                {
                    MessageBox.Show("Нет доступных маршрутов, транспорта или водителей", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var query = @"INSERT INTO transport_company.trip 
                             (route_id, vehicle_id, driver_id, departure_time, arrival_time, status) 
                             VALUES (@route, @vehicle, @driver, NOW(), NOW() + INTERVAL '1 hour', 'запланировано'::transport_company.trip_status)
                             RETURNING id";

                using var cmd = new NpgsqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@route", routeId);
                cmd.Parameters.AddWithValue("@vehicle", vehicleId);
                cmd.Parameters.AddWithValue("@driver", driverId);

                var newId = (int)cmd.ExecuteScalar();

                LoadTripsData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении записи: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private int GetFirstId(NpgsqlConnection connection, string table)
        {
            var query = $"SELECT id FROM transport_company.{table} LIMIT 1";
            using var cmd = new NpgsqlCommand(query, connection);
            var result = cmd.ExecuteScalar();
            return result != null ? (int)result : 0;
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView.SelectedRows.Count == 0)
            {
                MessageBox.Show("Выберите рейс для удаления", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRow = dataGridView.SelectedRows[0];
            var id = (int)selectedRow.Cells["id"].Value;
            var routeNumber = selectedRow.Cells["route_number"].Value.ToString();
            var departureTime = Convert.ToDateTime(selectedRow.Cells["departure_time"].Value);

            if (MessageBox.Show($"Вы уверены, что хотите удалить рейс {routeNumber} на {departureTime}?", "Подтверждение удаления",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    using var connection = new NpgsqlConnection(_connectionString);
                    connection.Open();

                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        var deleteBookings = "DELETE FROM transport_company.booking WHERE trip_id = @id";
                        using var cmdBookings = new NpgsqlCommand(deleteBookings, connection, transaction);
                        cmdBookings.Parameters.AddWithValue("@id", id);
                        cmdBookings.ExecuteNonQuery();

                        var deleteTrip = "DELETE FROM transport_company.trip WHERE id = @id";
                        using var cmdTrip = new NpgsqlCommand(deleteTrip, connection, transaction);
                        cmdTrip.Parameters.AddWithValue("@id", id);
                        cmdTrip.ExecuteNonQuery();

                        transaction.Commit();

                        var rowToDelete = _tripsTable.Rows.Cast<DataRow>()
                            .FirstOrDefault(r => (int)r["id"] == id);
                        if (rowToDelete != null)
                        {
                            _tripsTable.Rows.Remove(rowToDelete);
                            _tripsTable.AcceptChanges();
                        }

                        MessageBox.Show("Рейс успешно удален", "Успех",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (dataGridView.SelectedRows.Count == 0)
            {
                MessageBox.Show("Выберите рейс для редактирования", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _isEditing = true;
            dataGridView.ReadOnly = false;
            btnEdit.Visible = false;
            btnDelete.Visible = false;
            btnIAdd.Visible = false;
            btnSave.Visible = true;
            btnCancel.Visible = true;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();


                var changes = _tripsTable.GetChanges();
                if (changes == null || changes.Rows.Count == 0)
                {
                    MessageBox.Show("Нет изменений для сохранения", "Информация",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    CancelEditing();
                    return;
                }

                foreach (DataRow row in changes.Rows)
                {
                    var id = (int)row["id"];
                    var departureTime = Convert.ToDateTime(row["departure_time"]);
                    var arrivalTime = Convert.ToDateTime(row["arrival_time"]);
                    var status = row["status"].ToString();

                    var query = @"UPDATE transport_company.trip 
                                 SET departure_time = @departure, 
                                     arrival_time = @arrival, 
                                     status = @status::transport_company.trip_status
                                 WHERE id = @id";

                    using var cmd = new NpgsqlCommand(query, connection);
                    cmd.Parameters.AddWithValue("@departure", departureTime);
                    cmd.Parameters.AddWithValue("@arrival", arrivalTime);
                    cmd.Parameters.AddWithValue("@status", status);
                    cmd.Parameters.AddWithValue("@id", id);

                    cmd.ExecuteNonQuery();
                }

                _tripsTable.AcceptChanges();
                _originalTripsTable = _tripsTable.Copy();

                MessageBox.Show("Изменения успешно сохранены", "Успех",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                CancelEditing();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            CancelEditing();
        }

        private void CancelEditing()
        {
            _isEditing = false;
            dataGridView.ReadOnly = true;
            btnEdit.Visible = true;
            btnDelete.Visible = true;
            btnIAdd.Visible = true;
            btnSave.Visible = false;
            btnCancel.Visible = false;

            if (_originalTripsTable != null)
            {
                _tripsTable = _originalTripsTable.Copy();
                dataGridView.DataSource = _tripsTable;
            }
        }

        private void BtnApplyFilter_Click(object sender, EventArgs e)
        {
            string statusFilter = cmbStatusFilter.SelectedItem?.ToString();
            DateTime? dateFrom = dtpDateFrom.Checked ? dtpDateFrom.Value : (DateTime?)null;
            DateTime? dateTo = dtpDateTo.Checked ? dtpDateTo.Value : (DateTime?)null;

            LoadTripsData(statusFilter, dateFrom, dateTo);
        }

        private void BtnResetFilter_Click(object sender, EventArgs e)
        {
            cmbStatusFilter.SelectedIndex = 0;
            dtpDateFrom.Checked = false;
            dtpDateTo.Checked = false;
            LoadTripsData();
        }

        private void DataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && _isEditing)
            {
                dataGridView.BeginEdit(true);
            }
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadTripsData();
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}