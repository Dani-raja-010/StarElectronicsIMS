using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace StarElectronicsIMS.Data
{
    /// <summary>Thin helper around ADO.NET. All queries are parameterized.</summary>
    public static class Db
    {
        public const string ConnectionName = "InventoryManagerDB";
        public const int SchemaVersion = 2;

        const string DefaultConnectionString =
            @"Data Source=.\MSSQLSERVER2022;Initial Catalog=StarElectronicsDB;Integrated Security=True";

        static string _override;

        public static string ConnectionString =>
            _override
            ?? ConfigurationManager.ConnectionStrings[ConnectionName]?.ConnectionString
            ?? DefaultConnectionString;

        public static SqlParameter P(string name, object value) => new SqlParameter(name, value ?? DBNull.Value);

        public static SqlConnection Open()
        {
            var cn = new SqlConnection(ConnectionString);
            cn.Open();
            return cn;
        }

        public static DataTable Query(string sql, params SqlParameter[] ps)
        {
            using (var cn = Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddRange(ps);
                var dt = new DataTable();
                using (var da = new SqlDataAdapter(cmd)) da.Fill(dt);
                return dt;
            }
        }

        public static int Execute(string sql, params SqlParameter[] ps)
        {
            using (var cn = Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddRange(ps);
                return cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(string sql, params SqlParameter[] ps)
        {
            using (var cn = Open())
            using (var cmd = new SqlCommand(sql, cn))
            {
                cmd.Parameters.AddRange(ps);
                var v = cmd.ExecuteScalar();
                return v == DBNull.Value ? null : v;
            }
        }

        /// <summary>Runs <paramref name="work"/> inside a transaction; commits on success, rolls back on any exception.</summary>
        public static T InTransaction<T>(Func<SqlConnection, SqlTransaction, T> work)
        {
            using (var cn = Open())
            using (var tx = cn.BeginTransaction())
            {
                try
                {
                    var result = work(cn, tx);
                    tx.Commit();
                    return result;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public static SqlCommand Cmd(SqlConnection cn, SqlTransaction tx, string sql, params SqlParameter[] ps)
        {
            var cmd = new SqlCommand(sql, cn, tx);
            cmd.Parameters.AddRange(ps);
            return cmd;
        }

        public static void TestConnection()
        {
            using (Open()) { }
        }

        /// <summary>Uses the given connection string for this session and tries to save it to the .config file.</summary>
        /// <returns>false if the config file could not be written (e.g. no permission); the setting still applies until exit.</returns>
        public static bool SaveConnectionString(string cs)
        {
            _override = cs;
            try
            {
                var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var entry = cfg.ConnectionStrings.ConnectionStrings[ConnectionName];
                if (entry == null)
                    cfg.ConnectionStrings.ConnectionStrings.Add(new ConnectionStringSettings(ConnectionName, cs, "System.Data.SqlClient"));
                else
                    entry.ConnectionString = cs;
                cfg.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("connectionStrings");
                return true;
            }
            catch (ConfigurationErrorsException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        /// <summary>Applies the embedded upgrade script when the database is older than <see cref="SchemaVersion"/>.</summary>
        public static void EnsureSchema()
        {
            int current = 0;
            if (Scalar("SELECT OBJECT_ID('dbo.SchemaInfo')") != null)
                current = Convert.ToInt32(Scalar("SELECT ISNULL(MAX(Version), 0) FROM dbo.SchemaInfo"));
            if (current >= SchemaVersion) return;

            string script;
            using (var s = typeof(Db).Assembly.GetManifestResourceStream("upgrade_v2.sql"))
            using (var r = new StreamReader(s))
                script = r.ReadToEnd();

            var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            InTransaction((cn, tx) =>
            {
                foreach (var batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    using (var cmd = Cmd(cn, tx, batch)) cmd.ExecuteNonQuery();
                }
                return 0;
            });
        }
    }
}
