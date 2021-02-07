using CoinbasePro.Services.Products.Models;
using CoinbasePro.Services.Products.Types;
using CoinbasePro.Shared.Types;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoinbaseData
{
    public class TableHelper
    {
        public static Dictionary<string, string> InsertScripts = new Dictionary<string, string>();
        public static Dictionary<string, string> UpdateScripts = new Dictionary<string, string>();
        public static string CreateTableScript<T>(string tableName = null)
        {
            var typeOfT = typeof(T);
            var typeProps = typeOfT.GetProperties();
            var typeName = tableName ?? typeOfT.Name;
            var sb = new StringBuilder();
            sb.Append($"CREATE TABLE [{typeName}]\r\n(\r\n");
            sb.Append($"\t[PKID] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_{tableName}] PRIMARY KEY,");

            var columnList = new List<string>();

            foreach (var prop in typeProps)
            {
                var propName = prop.Name;
                var propType = prop.PropertyType;
                var nullable = (propType.IsGenericType
                    && propType.GetGenericTypeDefinition() == typeof(Nullable<>));
                if (nullable)
                {
                    propType = propType.GetGenericArguments().First();
                }
                //TODO: Handle generics List<> and IEnumerble<>
                var nullString = nullable ? "NULL" : "NOT NULL";
                string sqlDbType = GetSqlDbTypeName(propType);
                columnList.Add($"[{propName}] {sqlDbType} {nullString}");
            }
            sb.Append($"\r\n\t{string.Join(",\r\n\t", columnList)}");
            sb.Append("\r\n)");

            var result = sb.ToString();
            return result;
        }



        public static string InsertTableScript<T>(string tableName = null)
        {
            if (InsertScripts.ContainsKey(tableName))
                return InsertScripts[tableName];

            var typeOfT = typeof(T);
            var typeProps = typeOfT.GetProperties();
            var typeName = tableName ?? typeOfT.Name;
            var sb = new StringBuilder();
            sb.Append($"Insert into [{typeName}]\r\n(\r\n");

            var columnList = new List<string>();

            foreach (var prop in typeProps)
            {
                var propName = prop.Name;
                columnList.Add($"{propName}");
            }
            sb.Append($"\r\n\t{string.Join(",\r\n\t", columnList.Select(x => $"[{x}]"))}");
            sb.Append("\r\n)\r\n");
            sb.Append($"Values\r\n(\r\n");
            sb.Append($"\r\n\t{string.Join(",\r\n\t", columnList.Select(x => $"@{x}"))}");
            sb.Append("\r\n)");
            var result = sb.ToString();
            InsertScripts[tableName] = result;
            return result;
        }

        public static string UpdateTableScript<T>(string keyColumn = null, string tableName = null)
        {
            if (UpdateScripts.ContainsKey(tableName))
                return UpdateScripts[tableName];

            var typeOfT = typeof(T);
            var typeProps = typeOfT.GetProperties();
            var typeName = tableName ?? typeOfT.Name;
            var sb = new StringBuilder();
            sb.Append($"Update [{typeName}]\r\n");
            sb.Append($"Set\r\n");

            var columnList = new List<string>();

            foreach (var prop in typeProps)
            {
                var propName = prop.Name;
                columnList.Add($"{propName}");

            }

            sb.Append($"\t{string.Join(",\r\n\t", columnList.Select(x => $"[{x}] = @{x}"))}");
            sb.Append("\r\nWhere");
            sb.Append($"\r\n\t[{keyColumn}] = @{keyColumn}");

            var result = sb.ToString();
            UpdateScripts[tableName] = result;
            return result;
        }

        public static List<T> GetByQuery<T>(string query, object param = null)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                List<T> items = conn.Query<T>(query, param).ToList();
                return items;
            }
        }
        public static List<T> Get<T>(string tableName = null, string orderby = null)
        {
            string query = $"Select * from [{tableName ?? typeof(T).Name}]";
            if (orderby != null) query += $" {orderby}";
            using (var conn = new SqlConnection(ConnectionString))
            {
                List<T> items = conn.Query<T>(query).ToList();
                return items;
            }
        }

        public static void Save<T>(Func<IEnumerable<T>> p, string tableName)
        {
            var query = InsertTableScript<T>(tableName);
            List<T> items = p().ToList();
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Execute(query, items);
            }
        }

        public static void Update<T>(Func<IEnumerable<T>> p, string tableName, string keyColumn)
        {
            var query = UpdateTableScript<T>(keyColumn, tableName);
            List<T> items = p().ToList();
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Execute(query, items);
            }
        }


        public static string GetSqlDbTypeName(Type propType)
        {
            var sqlDbType = "";
            switch (propType.Name)
            {
                case nameof(Boolean):
                    sqlDbType = "BIT";
                    break;
                case nameof(Guid):
                    sqlDbType = "UNIQUEIDENTIFIER";
                    break;
                case nameof(SByte):
                case nameof(Byte):
                    sqlDbType = "TINYINT";
                    break;
                case nameof(Int16):
                case nameof(UInt16):
                    sqlDbType = "SMALLINT";
                    break;
                case nameof(Int32):
                case nameof(UInt32):
                    sqlDbType = "INT";
                    break;
                case nameof(Int64):
                case nameof(UInt64):
                    sqlDbType = "BIGINT";
                    break;
                case nameof(DateTime):
                case nameof(DateTimeOffset):
                    sqlDbType = "DATETIMEOFFSET";
                    break;
                case nameof(Decimal):
                    sqlDbType = "DECIMAL(18,8)";
                    break;
                case nameof(String):
                    sqlDbType = "varchar(100)";
                    break;
                default:
                    if (propType.IsEnum)
                    {
                        sqlDbType = "INT";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    break;
            }
            return sqlDbType;
        }


        public static void GetModelTypes()
        {
            var candleType = typeof(Candle);
            var assembly = candleType.Assembly;
            var types = assembly.GetTypes().Where(x => (x.Namespace ?? "").Contains("Models")).ToArray();

            var props = types.SelectMany(type => type.GetProperties().Select(prop => prop.PropertyType)).Distinct().ToList();

            var enumTypes = props.Where(x => x.IsEnum).ToList();
            var rem = props.Except(enumTypes).ToList();
            var generics = rem.Where(x => x.IsGenericType).ToList();
            var rem2 = rem.Except(generics).ToList();


        }
        public static string DbServer = ".";
        public static string DbName = "Coinbase";

        public static string ConnectionString => $"Server={DbServer};Initial Catalog={DbName};Integrated Security=true;";
        public static void AssureCandlesTables(ProductType productType)
        {
            var granularities = Enum.GetNames(typeof(CandleGranularity));
            var oldDbName = DbName;
            DbName = "Master";
            using (var conn = new SqlConnection(ConnectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to connect to database server: {DbServer}");
                }
                var query = ("select count(0) from sys.databases where name=@dbName");
                int count = conn.QuerySingle<int>(query, new { DbName });
                if (count == 0)
                {
                    throw new Exception("The database {DbName} does not exist");
                }
                DbName = oldDbName;
                conn.Execute($"USE [{DbName}]\r\n");
                using (SqlTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var granularity in granularities)
                        {

                            var tableName = $"{productType}{granularity}";
                            query = $"select count(0) from sys.tables where name = @tableName";
                            count = conn.QuerySingle<int>(query, new { tableName }, transaction: trans);
                            if (count > 0)
                            {
                                continue;
                            }
                            var tableScript = TableHelper.CreateTableScript<Candle>(tableName);
                            conn.Execute(tableScript, transaction: trans);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating candles tables {ex.Message}");
                        trans.Rollback();
                    }
                    trans.Commit();
                }
            }

        }
    }
}
