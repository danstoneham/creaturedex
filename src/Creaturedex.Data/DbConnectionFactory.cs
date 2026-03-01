using Microsoft.Data.SqlClient;
using System.Data;

namespace Creaturedex.Data;

public class DbConnectionFactory(string connectionString)
{
    public IDbConnection CreateConnection() => new SqlConnection(connectionString);
}
