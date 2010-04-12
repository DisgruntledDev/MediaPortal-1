/*
 * SQLite specifics
 * Copyright (C) 2004 Morten Mertner
 * 
 * This library is free software; you can redistribute it and/or modify it 
 * under the terms of the GNU Lesser General Public License 2.1 or later, as
 * published by the Free Software Foundation. See the included License.txt
 * or http://www.gnu.org/copyleft/lesser.html for details.
 *
 * $Id: SQLiteAnalyzer.cs 1236 2008-03-14 15:09:25Z mm $
 */

using System;
using System.Data;
using System.Data.SQLite;
using Gentle.Common;
using Gentle.Framework;

namespace Gentle.Provider.SQLite
{
	/// <summary>
	/// This class is an implementation of the <see cref="GentleSqlFactory"/> class for the SQLite RDBMS.
	/// </summary>
	public class SQLiteAnalyzer : GentleAnalyzer
	{
		public SQLiteAnalyzer( IGentleProvider provider ) : base( provider )
		{
		}

		#region SQL Statements for obtaining schema metadata
		// get all table names
        private const string selectTables = "select name, rootpage from sqlite_master where type='table'";
       
        // get all columns for named table
		// returns: cid, name, type, notnull, dflt_value, pk
        private const string selectColumns = "PRAGMA table_info({0})";
       
        // get all indexes for named table
		// returns: name, unique
        private const string selectIndexes = "PRAGMA index_list({0})";

        // get all columns for named index (get index names using selectIndexes)
        // returns: seqno, cid, name (of column)
		private const string selectIndex = "PRAGMA index_info({0})";

		// get foreign key information for named table
		// returns: ?!
		private const string selectForeignKeys = "PRAGMA foreign_key_list({0})";
		#endregion	

		public override ColumnInformation AnalyzerCapability
		{
			get { return ColumnInformation.ciBasic | ColumnInformation.ciExtra | ColumnInformation.ciKey; }
		}

		/// <summary>
		/// Please refer to the <see cref="GentleAnalyzer"/> class and the <see cref="IDatabaseAnalyzer"/> 
		/// interface it implements a description of this method.
		/// </summary>
		public override void Analyze( string tableName )
		{
			GentleSqlFactory sf = provider.GetSqlFactory();
			try
			{
				bool isSingleRun = tableName != null;
				string sql = isSingleRun ? String.Format( "{0} and name = '{1}'", selectTables, tableName ) : selectTables;
				SqlResult sr = broker.Execute( sql, null, null );
				for( int row=0; row<sr.RowsContained; row++ )
				{
					tableName = sr.GetString( row, 0 );
					// get TableMap for current table
					TableMap map = GetTableMap( tableName );
					if( map == null )
					{
						map = new TableMap( provider, tableName );
						maps[ tableName.ToLower() ] = map;
					}
					// get column information 
					UpdateTableMapWithColumnInformation( map );
					// get foreign key information
					UpdateTableMapWithForeignKeyInformation( map );
				}
			}
			catch( GentleException fe )
			{
				// ignore errors caused by tables found in db but for which no map exists
				// TODO this should be a config option
				if( fe.Error != Error.NoObjectMapForTable )
				{
					throw;
				}
			}
			catch( Exception e )
			{
				Check.LogInfo( LogCategories.General, "Using provider {0} and connectionString {1}.",
							   provider.Name, provider.ConnectionString );
				Check.Fail( e, Error.Unspecified, "An error occurred while analyzing the database schema." );
			}
		}

		private void UpdateTableMapWithColumnInformation( TableMap map )
		{
			SqlResult sr = broker.Execute( String.Format( selectColumns, map.TableName ), null, null );
			// process result set using columns: cid, name, type, notnull, dflt_value, pk			
			for( int i=0; i<sr.RowsContained; i++ )
			{
				string columnName = sr.GetString( i, "name" );
				FieldMap fm = map.GetFieldMapFromColumn( columnName );
				if( fm == null )
				{
					fm = new FieldMap( map, columnName );
					map.Fields.Add( fm );
				}
				// get basic column information
				fm.SetDbType( sr.GetString( i, "type" ), false );
				fm.SetIsNullable( ! sr.GetBoolean( i, "notnull" ) );
				fm.SetIsPrimaryKey( sr.GetBoolean( i, "pk" ) );
				fm.SetIsAutoGenerated( fm.IsPrimaryKey && (fm.Type == typeof(int) || fm.Type == typeof(long)) );
			}
		}

		private void UpdateTableMapWithIndexInformation( TableMap map )
		{
			SqlResult sr = broker.Execute( String.Format( selectIndexes, map.TableName ), null, null );
			// process result set using columns: name, unique		
			for( int i=0; i<sr.RowsContained; i++ )
			{
				string indexName = sr.GetString( i, "name" );

				SqlResult indexInfo = broker.Execute( String.Format( selectIndex, map.TableName ), null, null );
				// process result set using columns: seqno, cid, name		
				for( int indexColumn=0; indexColumn<sr.RowsContained; indexColumn++ )
				{
					// fm.SetIsAutoGenerated( sr.GetString( i, "dflt_value" ).Length > 0 ? true : false );
				}
			}
		}

		private void UpdateTableMapWithForeignKeyInformation( TableMap map )
		{
			SqlResult sr = broker.Execute( String.Format( selectForeignKeys, map.TableName ), null, null );
			// process result set using columns: ??
			for( int i=0; i<sr.RowsContained; i++ )
			{
				//string type = sr.GetString( i, "ConstraintType" );
				//if( type.ToLower().Equals( "foreign key" ) )
				//{
				//    string conref = sr.GetString( i, "ConstraintReference" );
				//    if( conref.StartsWith( "IDX" ) )
				//    {
				//        string fkRef = sr.GetString( i, "ConstraintName" );
				//        if( fkRef != null && fkRef.StartsWith( "FK" ) )
				//        {
				//            conref = fkRef;
				//        }
				//    }
				//    SqlResult res = broker.Execute( String.Format( selectReferences, conref ), null, null );
				//    if( res.ErrorCode == 0 && res.RowsContained == 1 )
				//    {
				//        fm.SetForeignKeyTableName( res.GetString( 0, "TableName" ) );
				//        fm.SetForeignKeyColumnName( res.GetString( 0, "ColumnName" ) );
				//    }
				//    else
				//    {
				//        if( res.RowsContained == 0 )
				//        {
				//            // see GOPF-155 for additional information
				//            Check.LogWarning( LogCategories.Metadata,
				//                              "Unable to obtain foreign key information for column {0} of table {1}.",
				//                              fm.ColumnName, map.TableName );
				//        }
				//        else
				//        {
				//            Check.LogWarning( LogCategories.Metadata, "Gentle 1.x does not support composite foreign keys." );
				//        }
				//    }
				//}
			}
		}
	}
}
