using System.Collections.Generic;

namespace CoinbaseData
{
    public class DbFills
    {
        public static void Save(List<DbFill> fills)
        {
            var tableName = $"DbFills";
            TableHelper.Save(() => fills, tableName);
        }
    }

}


