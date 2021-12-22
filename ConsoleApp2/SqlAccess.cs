﻿using System;
using System.Data;
using MySql.Data.MySqlClient;
public class SqlAccess
{
    static string host = "localhost";
    static string port = "3306";
    static string database = "gg";
    static string id = "root";
    static string pwd = "root";
    public static MySqlConnection dbConnection;
    public SqlAccess()
    {
        OpenSql();
    }
    public static void OpenSql()
    {
        try
        {
            string connectionString = string.Format("Server = {0};port={1};Database = {2}; User ID = {3}; Password = {4};", host, port, database, id, pwd);
            dbConnection = new MySqlConnection(connectionString);
            dbConnection.Open();
        }
        catch (Exception e)
        {
            throw new Exception("服务器连接失败，请重新检查是否打开MySql服务。" + e.Message.ToString());
        }
    }
    public DataSet CreateTable(string name, string[] col, string[] colType)
    {
        if (col.Length != colType.Length)
        {
            throw new Exception("columns.Length != colType.Length");
        }
        string query = "CREATE TABLE " + name + " (" + col[0] + " " + colType[0];
        for (int i = 1; i < col.Length; ++i)
        {
            query += ", " + col[i] + " " + colType[i];
        }
        query += ")";
        return ExecuteQuery(query);
    }
    public DataSet CreateTableAutoID(string name, string[] col, string[] colType)
    {
        if (col.Length != colType.Length)
        {
            throw new Exception("columns.Length != colType.Length");
        }
        string query = "CREATE TABLE " + name + " (" + col[0] + " " + colType[0] + " NOT NULL AUTO_INCREMENT";
        for (int i = 1; i < col.Length; ++i)
        {
            query += ", " + col[i] + " " + colType[i];
        }
        query += ", PRIMARY KEY (" + col[0] + ")" + ")";
        return ExecuteQuery(query);
    }
    //插入一条数据，包括所有，不适用自动累加ID。
    public DataSet InsertInto(string tableName, string[] values)
    {
        string query = "INSERT INTO " + tableName + " VALUES (" + "'" + values[0] + "'";
        for (int i = 1; i < values.Length; ++i)
        {
            query += ", " + "'" + values[i] + "'";
        }
        query += ")";
        return ExecuteQuery(query);
    }
    //插入部分ID
    public DataSet InsertInto(string tableName, string[] col, string[] values)
    {
        if (col.Length != values.Length)
        {
            throw new Exception("columns.Length != colType.Length");
        }
        string query = "INSERT INTO " + tableName + " (" + col[0];
        for (int i = 1; i < col.Length; ++i)
        {
            query += ", " + col[i];
        }
        query += ") VALUES (" + "'" + values[0] + "'";
        for (int i = 1; i < values.Length; ++i)
        {
            query += ", " + "'" + values[i] + "'";
        }
        query += ")";
        return ExecuteQuery(query);
    }
    public DataSet SelectWhere(string tableName, string[] items, string[] col, string[] operation, string[] values)
    {
        if (col.Length != operation.Length || operation.Length != values.Length)
        {
            throw new Exception("col.Length != operation.Length != values.Length");
        }
        string query = "SELECT " + items[0];
        for (int i = 1; i < items.Length; ++i)
        {
            query += ", " + items[i];
        }
        query += " FROM " + tableName + " WHERE " + col[0] + operation[0] + "'" + values[0] + "' ";
        for (int i = 1; i < col.Length; ++i)
        {
            query += " AND " + col[i] + operation[i] + "'" + values[0] + "' ";
        }
        return ExecuteQuery(query);
    }
    public DataSet SelectWherePage(string query)
    {
        return ExecuteQuery(query);
    }
    public DataSet UpdateInto(string tableName, string[] cols, string[] colsvalues, string selectkey, string selectvalue)
    {
        string query = "UPDATE " + tableName + " SET " + cols[0] + " = " + colsvalues[0];
        for (int i = 1; i < colsvalues.Length; ++i)
        {

            query += ", " + cols[i] + " =" + colsvalues[i];
        }
        query += " WHERE " + selectkey + " = " + selectvalue + " ";
        return ExecuteQuery(query);
    }
    public DataSet Delete(string tableName, string[] cols, string[] colsvalues)
    {
        string query = "DELETE FROM " + tableName + " WHERE " + cols[0] + " = " + colsvalues[0];
        for (int i = 1; i < colsvalues.Length; ++i)
        {

            query += " or " + cols[i] + " = " + colsvalues[i];
        }
        return ExecuteQuery(query);
    }
    public void Close()
    {
        if (dbConnection != null)
        {
            dbConnection.Close();
            dbConnection.Dispose();
            dbConnection = null;
        }
    }
    public static DataSet ExecuteQuery(string sqlString)
    {
        if (dbConnection.State == ConnectionState.Open)
        {
            DataSet ds = new DataSet();
            try
            {
                MySqlDataAdapter da = new MySqlDataAdapter(sqlString, dbConnection);
                da.Fill(ds);
            }
            catch (Exception ee)
            {
                throw new Exception("SQL:" + sqlString + "/n" + ee.Message.ToString());
            }
            finally
            {
            }
            return ds;
        }
        return null;
    }
}
