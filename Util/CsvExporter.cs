using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace StarElectronicsIMS.Util
{
    /// <summary>Exports the visible columns/rows of a grid to a CSV file that opens directly in Excel.</summary>
    public static class CsvExporter
    {
        public static void Export(DataGridView grid, string suggestedName)
        {
            if (grid.Rows.Count == 0)
            {
                Ui.Info("There is nothing to export.");
                return;
            }
            using (var dlg = new SaveFileDialog
            {
                Filter = "CSV file (opens in Excel)|*.csv",
                FileName = suggestedName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv",
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (!Ui.Try("export the file", () => Write(grid, dlg.FileName))) return;
                if (Ui.Confirm("Exported " + grid.Rows.Count + " rows.\n\nOpen the file now?"))
                    System.Diagnostics.Process.Start(dlg.FileName);
            }
        }

        static void Write(DataGridView grid, string path)
        {
            var cols = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToList();
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols.Select(c => Escape(c.HeaderText))));
            foreach (DataGridViewRow row in grid.Rows)
                sb.AppendLine(string.Join(",", cols.Select(c => Escape(Format(row.Cells[c.Index].Value)))));
            // UTF-8 with BOM so Excel shows non-English characters correctly.
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static string Format(object v)
        {
            switch (v)
            {
                case null: return "";
                case DBNull _: return "";
                case DateTime d: return d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
                default: return v.ToString();
            }
        }

        static string Escape(string s)
        {
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
