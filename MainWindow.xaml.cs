using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;

namespace TestTask
{
    public partial class MainWindow : Window
    {
        SqlConnection sqlConnection;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            sqlConnection = new SqlConnection(ConfigurationManager.ConnectionStrings["TestTaskDB"].ConnectionString);

            showDataInDb();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string path = getFilePath();
            string[,] data = getDataFromCSVFile(path);
            loadDataInDB(data);
        }

        private string getFilePath()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Тестовые данные.CSV");
            if (!File.Exists(path))
            {

                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.FileName = "Document";
                dialog.DefaultExt = ".CSV";
                dialog.Filter = "Text Files (.CSV)|*.CSV";

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    path = dialog.FileName;
                }
            }
            return path;
        }


        private string[,] getDataFromCSVFile(string path)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            string[] lines = File.ReadAllLines(path, Encoding.GetEncoding("windows-1251"));
            string[,] matrix = new string[lines.Count(), lines[0].Split(';').Count()];
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                string[] lineWords = lines[i].Split(';');
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    matrix[i, j] = lineWords[j];
                }
            }
            return matrix;
        }

        async private void loadDataInDB(string[,] data)
        {
            sqlConnection.Open();

            clearDb();
            makeInsertCategoryRequest(data);
            makeInsertProcessRequest(data);
            makeInsertDepartmentRequest(data);
            makeInsertProcessDepartmentRequest(data);



            sqlConnection.Close();

            showDataInDb();
        }

        private void makeInsertCategoryRequest(string[,] data)
        {
            string request = "INSERT INTO [Category] (Name) values";
            List<string> categoryNames = new List<string>();
            for (int i = 1; i < data.GetLength(0); i++)
            {
                if (!categoryNames.Contains(data[i, 0]))
                {
                    categoryNames.Add(data[i, 0]);
                    request += $" (N'{data[i, 0]}'),";
                }
            }
            SqlCommand command = new SqlCommand(request[..^1], sqlConnection);
            command.ExecuteNonQuery();
        }

        private void makeInsertProcessRequest(string[,] data)
        {
            string request = "INSERT INTO [Process] (id, Category_id, Name) values";
            Dictionary<string, int> Categoryes = new Dictionary<string, int>();

            SqlCommand select = new SqlCommand("select * from [Category]", sqlConnection);
            using(SqlDataReader reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    Categoryes[reader["name"].ToString()] = Int32.Parse(reader["id"].ToString());
                }
            }
            List<string> processIds = new List<string>();
            for(int i = 1; i < data.GetLength(0); i++)
            {
                if (!processIds.Contains(data[i, 1]))
                {
                    processIds.Add(data[i, 1]);
                    request += $" (N'{data[i, 1]}', '{Categoryes[data[i, 0]]}', N'{data[i, 2]}'),";
                }
            }

            SqlCommand command = new SqlCommand(request[..^1], sqlConnection);
            command.ExecuteNonQuery();
        }

        private void makeInsertDepartmentRequest(string[,] data)
        {
            string request = "INSERT INTO [Department] (Name) values";
            List<string> departmentNames = new List<string>();
            for (int i = 1; i < data.GetLength(0); i++)
            {
                if (!departmentNames.Contains(data[i, 3]) && data[i, 3].Trim().Length > 0)
                {
                    departmentNames.Add(data[i, 3]);
                    request += $" (N'{data[i, 3]}'),";
                }
            }
            SqlCommand command = new SqlCommand(request[..^1], sqlConnection);
            command.ExecuteNonQuery();
        }

        private void makeInsertProcessDepartmentRequest(string[,] data)
        {
            string request = "INSERT INTO [Process_Department] (process_id, department_id) values";
            SqlCommand command = new SqlCommand("select id, name from process", sqlConnection);
            Dictionary<string, string> processNames = new Dictionary<string, string>();
            using(SqlDataReader reader  = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    processNames[reader["name"].ToString()] = reader["id"].ToString();
                }
            }
            command = new SqlCommand("select id, name from department", sqlConnection);
            Dictionary<string, int?> departmentNames = new Dictionary<string, int?>();
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    departmentNames[reader.GetString("name")] = reader.GetInt32("id");
                }
            }
            for (int i = 1; i < data.GetLength(0); i++)
            {
                request += $" (N'{processNames[data[i, 2]]}', {(departmentNames.ContainsKey(data[i, 3]) ? $"'{departmentNames[data[i, 3]]}'" : "null")}),";
            }
            command = new SqlCommand(request[..^1], sqlConnection);
            command.ExecuteNonQuery();

        }

        private void clearDb()
        {
            SqlCommand command = new SqlCommand("delete from Category; delete from Department; delete from process; delete from process_department", sqlConnection);
            command.ExecuteNonQuery();
        }

        private void showDataInDb()
        {
            sqlConnection.Open();
            List<LoadedData> loadedDatas = new List<LoadedData>();

            SqlCommand cmd = new SqlCommand("select process.name as process_name, Category.Name as category_name, department.Name as department_name, Process.id as id from Process_Department left join process on Process.Id = Process_Department.Process_id left join Department on Department.id = Process_Department.Department_id left join Category on Category.Id = Process.Category_id", sqlConnection);

            using (SqlDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    loadedDatas.Add(new LoadedData { ProcessId = reader["id"].ToString(), 
                        Category = reader["category_name"].ToString(), 
                        DepartmentName = reader["department_name"].ToString(),
                        ProcessName = reader["process_name"].ToString()
                    });
                }
            }

            dg.ItemsSource = loadedDatas;

            sqlConnection.Close();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            sqlConnection.Open();
            clearDb();
            dg.ItemsSource = null;
            dg.Items.Clear();
            sqlConnection.Close();
        }


    }
}
