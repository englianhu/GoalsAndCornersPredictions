using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Db
{
    public class ConnectionMgr
    {
        private List<DbConnection> dbConnectionList = new List<DbConnection>();
        int next = 0;

        public void Add(DbConnection conn)
        {
            dbConnectionList.Add(conn);
        }

        public DbConnection NextConnection()
        {
            if (++next == dbConnectionList.Count) next = 0;

            return dbConnectionList.ElementAt(next);
        }
    };

    public class Database
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
        (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private DbCreator dbCreator = null;
        private ConnectionMgr connMgr = null;
        private readonly object syncLock = new object();

        public Database(DbCreator creator, ConnectionMgr connectionMgr = null)
        {
            if (connectionMgr == null) connMgr = new ConnectionMgr();
            dbCreator = creator;
        }


        public bool Connect(string connectionString)
        {
            bool retVal = false;

            DbConnection connection = null;

            try
            {
                connection = dbCreator.newConnection(connectionString);

                if (connection != null)
                {
                    connection.Open();

                    retVal = true;
                }
            }
            catch (Exception ex)
            {
                log.Error("Creating new connection " + ex.ToString());

                if (connection != null)
                {
                    if (connection.State != System.Data.ConnectionState.Closed)
                    {
                        log.Debug("Closing DB Connection...");
                        connection.Close();
                    }

                    connection.Dispose();
                }
            }

            if (retVal)
            {
                connMgr.Add(connection);
                log.Info("Connected to database: " + connectionString);
            }

            return retVal;
        }

        public List<string> OneColumnQuery(string sql)
        {
            lock (syncLock)
            {
                var ids = new List<string>();

                using (DbCommand find = dbCreator.newCommand(sql, connMgr.NextConnection()))
                {
                    using (DbDataReader dr = find.ExecuteReader())
                    {
                        bool hasRows = dr.HasRows;

                        if (hasRows == true)
                        {
                            while (dr.Read())
                            {
                                ids.Add(dr[0].ToString());
                            }
                        }
                    }
                }

                return ids;
            }
        }

        public void RunSQL(string sql, Action<DbDataReader> a)
        {
            lock (syncLock)
            {
                try
                {
                    using (DbCommand cmd = dbCreator.newCommand(sql, connMgr.NextConnection()))
                    {
                        using (DbDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                a(dr);
                            }

                            dr.Close();
                        }
                    }
                }
                catch (DbException e)
                {
                    log.Error("Exception: " + e);
                    throw e;
                }
            }
        }
    }
}

