using Function.Models;
using LiteDB;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Function.Services;


namespace Function.Helpers
{
    public class LiteDbHelpers: INetworkDataService
    {


        // 数据库文件名，自动生成在运行目录下
       // private  string DbNamePath = AppContext.BaseDirectory + "MyNetworkData.db";
       // private  string DbName = ;
        private readonly string _connectionString = $"Data Source={AppContext.BaseDirectory + "Config\\MyNetworkData.db"}";

        public LiteDbHelpers()
        {// =========================================================
         // ★★★ 加上这一行代码来解决报错 ★★★
         // 它会加载原生 SQLite 驱动
         // =========================================================
            SQLitePCL.Batteries_V2.Init();
            Initialize();
        }

        /// <summary>
        /// 初始化：如果你第一次运行，它会自动创建文件和表
        /// </summary>
        public void Initialize()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText =
                @"
                    CREATE TABLE IF NOT EXISTS NetworkConfigs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        IpAddress TEXT,
                        SubnetMask TEXT,
                        Gateway TEXT,
                        Remarks TEXT
                    ); 
                    CREATE TABLE IF NOT EXISTS RomoteInfo (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    IpAddress TEXT,
                    UserName TEXT,
                    PassWord TEXT,
                    Remarks TEXT);
                    CREATE TABLE IF NOT EXISTS RomoteFile (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                    IpAddress TEXT,
                    UserName TEXT,
                    PassWord TEXT,
                    Remarks TEXT);
                ";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 【增】添加一条新记录
        /// </summary>
        public void Add<T>(T item) where T : class
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                if (item is IpInfoConfig cfg)
                {
                    cmd.CommandText =
                   @"
                        INSERT INTO NetworkConfigs (IpAddress, SubnetMask, Gateway, Remarks)
                        VALUES ($ip, $mask, $gate, $remark)
                    ";

                    //使用参数化查询防止 SQL 注入，也能处理特殊字符
                    cmd.Parameters.AddWithValue("$ip", cfg.Ip ?? "");
                    cmd.Parameters.AddWithValue("$mask", cfg.SubNet ?? "");
                    cmd.Parameters.AddWithValue("$gate", cfg.GetWay ?? "");

                    cmd.Parameters.AddWithValue("$remark", cfg.remark ?? "");

                    cmd.ExecuteNonQuery();
                }
                else if(item is RomoteInfo user)
                {
                    cmd.CommandText =
                  @"
                        INSERT INTO RomoteInfo (IpAddress, UserName, PassWord, Remarks)
                        VALUES ($ip, $username, $password, $remark)
                    ";

                    //使用参数化查询防止 SQL 注入，也能处理特殊字符
                    cmd.Parameters.AddWithValue("$ip", user.Ip ?? "");
                    cmd.Parameters.AddWithValue("$username", user.UserName ?? "");
                    cmd.Parameters.AddWithValue("$password", user.PassWord ?? "");

                    cmd.Parameters.AddWithValue("$remark", user.remark ?? "");

                    cmd.ExecuteNonQuery();
                }

                else if (item is RomoteFile file)
                {
                    cmd.CommandText =
                  @"
                        INSERT INTO RomoteFile (IpAddress, UserName, PassWord, Remarks)
                        VALUES ($ip, $username, $password, $remark)
                    ";

                    //使用参数化查询防止 SQL 注入，也能处理特殊字符
                    cmd.Parameters.AddWithValue("$ip", file.Ip ?? "");
                    cmd.Parameters.AddWithValue("$username", file.UserName ?? "");
                    cmd.Parameters.AddWithValue("$password", file.PassWord ?? "");

                    cmd.Parameters.AddWithValue("$remark", file.remark ?? "");

                    cmd.ExecuteNonQuery();
                }

            }
        }

        /// <summary>
        /// 【删】根据 ID 删除一条记录
        /// </summary>
        public void Delete(object item,int id)
        {
            string tableName = "";
            if (item is IpInfoConfig) tableName = "NetworkConfigs";
            else if (item is RomoteInfo) tableName = "RomoteInfo";
            else if (item is RomoteFile) tableName = "RomoteFile";
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = $"DELETE FROM {tableName} WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 【改】更新一条记录
        /// </summary>
        public void Update<T>(T item) where T : class
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                if (item is IpInfoConfig cfg)
                {
                        cmd.CommandText =
                      @"
                        UPDATE NetworkConfigs 
                        SET IpAddress = $ip, 
                            SubnetMask = $mask, 
                            Gateway = $gate, 
                       
                            Remarks = $remark
                        WHERE Id = $id
                    ";

                        cmd.Parameters.AddWithValue("$id", cfg.Id);
                        cmd.Parameters.AddWithValue("$ip", cfg.Ip ?? "");
                        cmd.Parameters.AddWithValue("$mask", cfg.SubNet ?? "");
                        cmd.Parameters.AddWithValue("$gate", cfg.GetWay ?? "");
                        cmd.Parameters.AddWithValue("$remark", cfg.remark ?? "");
                        //cmd.Parameters.AddWithValue("$remark", config.Remarks ?? "");

                        cmd.ExecuteNonQuery();
                }
                else if (item is RomoteInfo user)
                {
                    cmd.CommandText =
                  @"
                       UPDATE RomoteInfo 
                        SET IpAddress = $ip, 
                            UserName = $userName, 
                            PassWord = $passWord, 
                            Remarks = $remark
                            WHERE Id = $id
                    ";

                    //使用参数化查询防止 SQL 注入，也能处理特殊字符
                    cmd.Parameters.AddWithValue("$id", user.Id);
                    cmd.Parameters.AddWithValue("$ip", user.Ip ?? "");
                    cmd.Parameters.AddWithValue("$userName", user.UserName ?? "");
                    cmd.Parameters.AddWithValue("$passWord", user.PassWord ?? "");
                    cmd.Parameters.AddWithValue("$remark", user.remark ?? "");

                    cmd.ExecuteNonQuery();
                }
                else if (item is RomoteFile file)
                {
                    cmd.CommandText =
                  @"
                       UPDATE RomoteFile 
                        SET IpAddress = $ip, 
                            UserName = $userName, 
                            PassWord = $passWord, 
                            Remarks = $remark
                            WHERE Id = $id
                    ";

                    //使用参数化查询防止 SQL 注入，也能处理特殊字符
                    cmd.Parameters.AddWithValue("$id", file.Id);
                    cmd.Parameters.AddWithValue("$ip", file.Ip ?? "");
                    cmd.Parameters.AddWithValue("$userName", file.UserName ?? "");
                    cmd.Parameters.AddWithValue("$passWord", file.PassWord ?? "");
                    cmd.Parameters.AddWithValue("$remark", file.remark ?? "");

                    cmd.ExecuteNonQuery();
                }


            }
        }

        /// <summary>
        /// 【查】获取所有数据
        /// </summary>
        public List<T> GetAll<T>() where T : class, new()
        {
            var result = new List<T>();
            string sql = "";

            // 根据类型决定查哪张表
            if (typeof(T) == typeof(IpInfoConfig)) sql = "SELECT * FROM NetworkConfigs";
            else if (typeof(T) == typeof(RomoteInfo))  sql = "SELECT * FROM RomoteInfo";
            else if (typeof(T) == typeof(RomoteFile)) sql = "SELECT * FROM RomoteFile";
            else return result; // 未知类型

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        // 实例化 T
                        var item = new T();

                        // 手动映射数据 (这是最快且不用第三方库的方法)
                        if (item is IpInfoConfig cfg)
                        {
                            cfg.Id = reader.GetInt32(0);
                            cfg.Ip = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            cfg.SubNet = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            cfg.GetWay = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            cfg.remark = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        }
                        else if (item is RomoteInfo user)
                        {
                            user.Id = reader.GetInt32(0);
                            user.Ip = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            user.UserName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            user.PassWord = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            user.remark = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        }
                        else if (item is RomoteFile log)
                        {
                            log.Id = reader.GetInt32(0);
                            log.Ip = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            log.UserName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            log.PassWord = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            log.remark = reader.IsDBNull(4) ? "" : reader.GetString(4);
                        }

                        result.Add(item);
                    }
                }
            }
            return result;
        }

        
    }
}
