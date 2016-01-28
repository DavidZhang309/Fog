using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySql.Data.MySqlClient;

using Fog.Common.Extension;

namespace Fog.Common
{
    public class DbAccess
    {
        public static FileEntry[] GetEntries(MySqlConnection connection)
        {
            MySqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT Path, md5chksum, LastChange FROM fog.entry_data;";
            MySqlDataReader reader = command.ExecuteReader();
            List<FileEntry> entries = new List<FileEntry>();
            while (reader.Read())
            {
                string path = reader.GetString("Path");
                string hash = reader.GetString("md5chksum");
                DateTime time = reader.GetDateTime("LastChange");
                entries.Add(new FileEntry(path, hash.HexStringToArray(), time));                
            }
            reader.Close();

            return entries.ToArray();
        }
        public static void AddEntries(MySqlConnection connection, FileEntry[] entries)
        {
            MySqlCommand command = connection.CreateCommand();
            StringBuilder builder = new StringBuilder("INSERT INTO fog.entry_data (Path, md5chksum, LastChange) VALUES ");
            foreach (FileEntry entry in entries)
                builder.Append(string.Format("('{0}', '{1}', '{2}')", entry.VirtualPath, entry.VerifiedHash.ToHexString(), entry.TimeOfUpdate.ToString("yyyy-MM-dd H:mm:ss")));
            builder.Append(";");
            command.CommandText = builder.ToString();
            command.ExecuteNonQuery();
        }

        //public static void AddTicket(MySqlConnection connection, OpTicket ticket)
        //{

        //}

        //public static NodeInfo[] GetNodes(MySqlConnection connection)
        //{

        //}
    }
}
