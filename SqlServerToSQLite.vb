﻿Imports System
Imports System.Collections.Generic
Imports System.Text
Imports System.Data
Imports System.Data.SqlClient
Imports System.Data.SQLite
Imports System.Threading
Imports System.Text.RegularExpressions
Imports System.IO
Imports System.Xml

''' <summary>
''' This class is responsible to take a single SQL Server database
''' and convert it to an SQLite database file.
''' </summary>
''' <remarks>The class knows how to convert table and index structures only.</remarks>
Public Class SqlServerToSQLite

    Public Shared mDataset As DataSet
    Public Shared mCurrentFunction As TableFunctions.SingleReturnFunction
    Public Shared mCurrentFunctionList As List(Of TableFunctions.SingleReturnFunction)
    Public Shared mFunctionsList As List(Of TableFunctions.SingleReturnFunction)
    Public Shared mFldDefinitions As Hashtable
    Public Shared mSourceTableName As String
    Public Shared mSQL As String
    Public Shared mStep As String

	Public Shared Event ProgressChanged(ByVal TaskDescription As String, ByVal PctComplete As Single)

    Enum crosstabFields
        wTable = 0
        wField = 1
        wCrosstab = 2
    End Enum

    Enum xmlDocType
        wFile = 0
        wString = 1
    End Enum

    Enum FunctionTableFields
        wFunction = 0
        wNewColumnName = 1
        wFieldList = 2
        wParameterList = 3
        wFunctionDisplay = 4
    End Enum

    Const COLUMN_HEADING As String = "Column Heading"
    Const ROW_HEADING As String = "Row Heading"
    Const VALUE As String = "Value"
    Const NUM_FIELDS_EXCEEDED_MESSAGE As String = "Fields Exceeded"
    Const NUM_FIELDS_ALLOWED As Integer = 1000
    Const TABLE_COLUMN As String = "FIELD"
    Const TABLE_COLUMN_FUNCTION As String = "FUNCTION"

    Enum datasources
        wViperResultsMdIds = 0 'Viper Results (Specific MDIDs)
        wPeptideDbs = 1        'Peptide (PT) Databases
        wAmtTagDbsAll = 2      'AMT Tag (MT) Databases (export all data)
        wAmtTagJobs = 3        'AMT Tag (MT) Databases (specific jobs)
        wIMPROVImport = 4      'IMPROV Import (MT) Databases (specific experiments)
    End Enum

#Region "Public Properties"
    ''' <summary>
    ''' Gets a value indicating whether this instance is active.
    ''' </summary>
    ''' <value><c>true</c> if this instance is active; otherwise, <c>false</c>.</value>
    Public Shared ReadOnly Property IsActive() As Boolean
        Get
            Return _isActive
        End Get
    End Property
#End Region

#Region "Public Methods"
    ''' <summary>
    ''' Cancels the conversion.
    ''' </summary>
    Public Shared Sub CancelConversion()
        _cancelled = True
    End Sub

    ''' <summary>
    ''' This method takes as input the connection string to an SQL Server database
    ''' and creates a corresponding SQLite database file with a schema derived from
    ''' the SQL Server database.
    ''' </summary>
    ''' <param name="sqlServerConnString">The connection string to the SQL Server database.</param>
    ''' <param name="sqlitePath">The path to the SQLite database file that needs to get created.</param>
    ''' <param name="password">The password to use or NULL if no password should be used to encrypt the DB</param>
    ''' <param name="handler">A handler delegate for progress notifications.</param>
    ''' <param name="selectionHandler">The selection handler that allows the user to select which
    ''' tables to convert</param>
    ''' <remarks>The method continues asynchronously in the background and the caller returned
    ''' immediatly.</remarks>
    Public Shared Sub ConvertSqlServerToSQLiteDatabase(ByVal sqlServerConnString As String, ByVal sqlitePath As String, ByVal password As String, ByVal handler As SqlConversionHandler, ByVal selectionHandler As SqlTableSelectionHandler, ByVal createTriggers As Boolean)
        ' Clear cancelled flag
        _cancelled = False

        Dim result As Boolean
        Try
            _isActive = True
            result = ConvertSqlServerDatabaseToSQLiteFile(sqlServerConnString, sqlitePath, password, handler, selectionHandler, createTriggers)
            _isActive = False

            If result Then
				UpdateProgress(handler, True, True, 100, "Finished converting database: " & sqlitePath)
			Else
				UpdateProgress(handler, True, False, 0, "Export Cancelled by user")
			End If


		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Failed to convert SQL Server database to SQLite database: " & ex.Message)
			_isActive = False
			UpdateProgress(handler, True, False, 100, ex.Message)
			' catch
		End Try

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="dsIndex"></param>
	''' <param name="sqlServerConnString"></param>
	''' <param name="MDIDList"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Public Shared Sub ConvertDatasetToSQLiteFile(ByVal paramList As List(Of String), ByVal dsIndex As Integer, ByVal sqlServerConnString As String, ByVal MDIDList As String, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		' Clear cancelled flag
		_cancelled = False

		Try
			_isActive = True
			ConvertSqlServerDatasetToSQLiteFile(paramList, dsIndex, sqlServerConnString, MDIDList, sqlitePath, handler)
			_isActive = False
			UpdateProgress(handler, True, True, 100, "Finished generating MTS Cache database: " & sqlitePath)
		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Failed to generate SQLite database" & ex.Message)
			_isActive = False
			UpdateProgress(handler, True, False, 100, ex.Message)
			' catch
		End Try

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="StartStep"></param>
	''' <param name="EndStep"></param>
	''' <param name="WorkFlow"></param>
	''' <param name="originalSqlitePath"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Public Shared Sub StartWorkflow(ByVal StartStep As Integer, ByVal EndStep As Integer, ByVal WorkFlow As String, ByVal originalSqlitePath As String, ByVal sqlitePath As String, ByVal CreateResultDb As Boolean, ByVal CompactDb As Boolean, ByVal handler As SqlConversionHandler)
		' Clear cancelled flag
		_cancelled = False

		Try

			_isActive = True
			ExecuteWorkflow(StartStep, EndStep, WorkFlow, originalSqlitePath, sqlitePath, CreateResultDb, CompactDb, handler)
			_isActive = False
			UpdateProgress(handler, True, True, 100, "Workflow complete.")

		Catch ex As Exception
			Dim msg As String = ""
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Failed to run workflow: " & ex.Message)
			_isActive = False
			msg = "Workflow failed on Step: " & mStep & " - Executing SQL: " & mSQL
			UpdateProgress(handler, True, False, 100, msg & ex.Message)
			' catch
		End Try

	End Sub


	''' <summary>
	''' 
	''' </summary>
	''' <param name="OriginalFilename"></param>
	''' <param name="NewFilename"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Public Shared Sub CopyFile(ByVal OriginalFilename As String, ByVal NewFilename As String, ByVal handler As SqlConversionHandler)
		' Clear cancelled flag
		_cancelled = False

		Try
			_isActive = True
			PerformCopyFile(OriginalFilename, NewFilename, handler)
			_isActive = False
			UpdateProgress(handler, True, True, 100, "Create file: " & NewFilename & " complete.")
		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Failed to create file: " & ex.Message)
			_isActive = False
			UpdateProgress(handler, True, False, 100, ex.Message)
			' catch
		End Try

	End Sub

	Public Shared Sub CompactCacheDatabase(ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		' Clear cancelled flag
		_cancelled = False

		Try
			_isActive = True
			CompactSQLiteDatabase(sqlitePath, handler)
			_isActive = False
			UpdateProgress(handler, True, True, 100, "Compacting Cache Database: " & sqlitePath & " complete.")
		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Failed to create file: " & ex.Message)
			_isActive = False
			UpdateProgress(handler, True, False, 100, ex.Message)
			' catch
		End Try

	End Sub


	Public Shared Sub CreateIterationTable(ByVal Sql As String, ByVal CreateSeparateTable As Boolean, ByVal iterationTableName As String, ByVal newTableName As String, ByVal groupByText As String, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		' Clear cancelled flag
		_cancelled = False

		mSQL = Sql

		Try
			_isActive = True
			RunCreateIterationTable(mSQL, CreateSeparateTable, iterationTableName, newTableName, groupByText, sqlitePath, handler)
			_isActive = False
			UpdateProgress(handler, True, True, 100, "Finished creating iteration table in: " & sqlitePath)
		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "Failed to create iteration table" & ex.Message)
			_isActive = False
			UpdateProgress(handler, True, False, 100, ex.Message)
			' catch
		End Try

	End Sub


#End Region

#Region "Private Methods"
	''' <summary>
	''' Do the entire process of first reading the SQL Server schema, creating a corresponding
	''' SQLite schema, and copying all rows from the SQL Server database to the SQLite database.
	''' </summary>
	''' <param name="sqlConnString">The SQL Server connection string</param>
	''' <param name="sqlitePath">The path to the generated SQLite database file</param>
	''' <param name="password">The password to use or NULL if no password should be used to encrypt the DB</param>
	''' <param name="handler">A handler to handle progress notifications.</param>
	''' <param name="selectionHandler">The selection handler which allows the user to select which tables to 
	''' convert.</param>
	Private Shared Function ConvertSqlServerDatabaseToSQLiteFile(ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal password As String, ByVal handler As SqlConversionHandler, ByVal selectionHandler As SqlTableSelectionHandler, ByVal createTriggers As Boolean) As Boolean
		' Delete the target file if it exists already.
		If Not File.Exists(sqlitePath) Then
			CreateSQLiteDatabaseOnly(sqlitePath)
			'File.Delete(sqlitePath)
		End If

		' Read the schema of the SQL Server database into a memory structure
		Dim sqlSchema As List(Of TableSchema) = ReadSqlServerSchema(sqlConnString, handler, selectionHandler)

		If sqlSchema IsNot Nothing Then
			' Create the SQLite database and apply the schema
			CreateSQLiteDatabase(sqlitePath, sqlSchema, password, handler)

			' Copy all rows from SQL Server tables to the newly created SQLite database
			CopySqlServerRowsToSQLiteDB(sqlConnString, sqlitePath, sqlSchema, password, handler)
			Return True
		Else
			Return False
		End If

	End Function

	''' <summary>
	''' Do the entire process of first reading the SQL Server schema, creating a corresponding
	''' SQLite schema, and copying all rows from the SQL Server database to the SQLite database.
	''' </summary>
	''' <param name="paramList"></param>
	''' <param name="dsIndex"></param>
	''' <param name="sqlConnString"></param>
	''' <param name="IDList"></param>
	''' <param name="sqlitePath">The path to the generated SQLite database file</param>
	''' <param name="handler">A handler to handle progress notifications.</param>
	''' <remarks></remarks>
	Private Shared Sub ConvertSqlServerDatasetToSQLiteFile(ByVal paramList As List(Of String), ByVal dsIndex As Integer, ByVal sqlConnString As String, ByVal IDList As String, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		' Delete the target file if it exists already.
		If File.Exists(sqlitePath) Then
			File.Delete(sqlitePath)
		End If
		Select Case dsIndex
			Case datasources.wViperResultsMdIds
				CreateViperResultsCacheDatabase(paramList, sqlConnString, sqlitePath, IDList, handler)

			Case datasources.wAmtTagJobs
				'AMT Tag (MT) Databases (specific jobs)
				CreateAMTTagDbsJobsCacheDatabase(paramList, sqlConnString, sqlitePath, IDList, handler)

			Case datasources.wPeptideDbs
				'Peptide (PT) Databases
				CreatePTDbsCacheDatabase(paramList, sqlConnString, sqlitePath, IDList, handler)

			Case datasources.wAmtTagDbsAll
				CreateAMTTagDbsAllCacheDatabase(paramList, sqlConnString, sqlitePath, IDList, handler)

			Case datasources.wIMPROVImport
				CreateIMPROVDbsCacheDatabase(paramList, sqlConnString, sqlitePath, IDList, handler)

		End Select

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="startStep"></param>
	''' <param name="endStep"></param>
	''' <param name="Workflow"></param>
	''' <param name="originalSqlitePath"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub ExecuteWorkflow(ByVal startStep As Integer, ByVal endStep As Integer, ByVal Workflow As String, ByVal originalSqlitePath As String, ByVal sqlitePath As String, ByVal mCreateResultDb As Boolean, ByVal CompactDb As Boolean, ByVal handler As SqlConversionHandler)
		If mCreateResultDb Then
			PerformCopyFile(originalSqlitePath, sqlitePath, handler)
		End If

		InitializeTableFunctions()

		RunWorkflow(startStep, endStep, GetWorkflowFromFile(Workflow), sqlitePath, handler)

		If CompactDb Then
			CompactSQLiteDatabase(sqlitePath, handler)
		End If

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="Sql"></param>
	''' <param name="CreateSeparateTable"></param>
	''' <param name="iterationTblName"></param>
	''' <param name="newTblName"></param>
	''' <param name="groupByText"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub RunCreateIterationTable(ByVal Sql As String, ByVal CreateSeparateTable As Boolean, ByVal iterationTblName As String, ByVal newTblName As String, ByVal groupByText As String, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)

		' Create the iteration table
		RunIterationTable(Sql, CreateSeparateTable, iterationTblName, newTblName, groupByText, sqlitePath, Nothing, handler)
		' Private Shared Sub RunIterationTable(ByVal SQL As String, ByVal CreateSeparateTable As Boolean, ByVal iterationTableName As String, ByVal newTblName As String, ByVal groupByText As String, ByVal sqlitePath As String, ByVal password As String, ByVal handler As SqlConversionHandler)

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CompactSQLiteDatabase(ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		Dim sql As String
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Dim conn As New SQLiteConnection
		Try
			sql = "vacuum """ & sqlitePath & """"
			'Using conn As New SQLiteConnection(sqliteConnString)
			conn.ConnectionString = sqliteConnString
			conn.Open()

			UpdateProgress(handler, False, True, 0, "Compacting database: " & sqlitePath)

			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Compacting database: " & sqlitePath)
			' Execute the query in order to actually compact the database.
			Dim cmd As New SQLiteCommand(sql, conn)
			cmd.ExecuteNonQuery()
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Finished compacting database: " & sqlitePath)
			conn.Close()
			UpdateProgress(handler, False, True, 100, "Finished compacting database: " & sqlitePath)

		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "The following error occured while compacting database: " & sqlitePath & " - " & ex.Message)
			If Not conn Is Nothing Then
				conn.Close()
			End If
			Throw
		End Try
	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="originalFile"></param>
	''' <param name="newFile"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub PerformCopyFile(ByVal originalFile As String, ByVal newFile As String, ByVal handler As SqlConversionHandler)
		UpdateProgress(handler, False, True, 0, "Creating File: " & newFile)
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Creating File: " & newFile)

		System.IO.File.Copy(originalFile, newFile, True)

		UpdateProgress(handler, False, True, 100, "Finished creating File: " & newFile)
		CheckCancelled()

		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Finished creating File: " & newFile)

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="startStep"></param>
	''' <param name="endStep"></param>
	''' <param name="Workflow"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub RunWorkflow(ByVal startStep As Integer, ByVal endStep As Integer, ByVal Workflow As String, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		Dim stp As String
		Dim sql As String
		Dim trgTbl As String
		Dim kTrgtTble As Boolean
		Dim PivotTble As Boolean
		Dim IterationTbl As Boolean
		Dim desc As String
		Dim FunctionTble As Boolean
		Dim wf As List(Of clsXMLStepSchema)
		Dim tblList As List(Of String)
		Dim indxList As List(Of String)
		Dim ExistingIndexName As String
		Dim SkipQuery As Boolean

		If Not File.Exists(sqlitePath) Then
			Exit Sub
		End If
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Dim conn As New SQLiteConnection
		Try
			conn.ConnectionString = sqliteConnString
			tblList = GetTablesFromDb(sqlitePath)
			conn.Open()
			If String.IsNullOrEmpty(Workflow) Then
				Workflow = GetWorkflowFromDb(sqlitePath)
			End If
			wf = ReadWorkflow(Workflow, xmlDocType.wString, False)
			'Drop all the tables not needed
			If Not wf Is Nothing Then
				startStep = startStep - 1
				endStep = endStep - 1
				For i = startStep To endStep
					stp = wf.Item(i).StepNo
					sql = wf.Item(i).SQL
					trgTbl = wf.Item(i).TargetTable
					UpdateProgress(handler, False, True, CInt((100.0R * i / wf.Count)), "Dropping temporary table for Step " & stp)
					If Not String.IsNullOrEmpty(trgTbl) AndAlso tblList.Contains(Trim(trgTbl)) AndAlso Not (sql.ToLower.Contains("update") Or sql.ToLower.Contains("delete")) Then
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Removing temp table: " & trgTbl & " from step: " & stp)
						sql = "Drop Table " & trgTbl
						Dim cmdDrop As New SQLiteCommand(sql, conn)
						cmdDrop.ExecuteNonQuery()
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Finished removing temp table: " & trgTbl & " from step: " & stp)
					End If
					CheckCancelled()
				Next
				conn.Close()
				indxList = GetIndexesFromDb(sqlitePath)
				conn.Open()
				For i = startStep To endStep
					SkipQuery = False
					ExistingIndexName = ""
					stp = wf.Item(i).StepNo
					mStep = stp
					sql = wf.Item(i).SQL
					sql = sql.Replace("''", "'")
					mSQL = sql
					trgTbl = wf.Item(i).TargetTable
					If String.IsNullOrEmpty(kTrgtTble) Then
						kTrgtTble = False
					Else
						kTrgtTble = CBool(wf.Item(i).KeepTargetTable)
					End If
					PivotTble = wf.Item(i).PivotTable
					If String.IsNullOrEmpty(PivotTble) Then
						PivotTble = False
					Else
						PivotTble = CBool(wf.Item(i).PivotTable)
					End If
					desc = wf.Item(i).Description
					If Not String.IsNullOrEmpty(trgTbl) AndAlso PivotTble AndAlso Not (sql.ToLower.Contains("update") Or sql.ToLower.Contains("delete")) Then
						sql = BuildCrosstabTableQuery(sqliteConnString, sql.Split(";"))
					End If
					FunctionTble = wf.Item(i).FunctionTable
					If String.IsNullOrEmpty(PivotTble) Then
						FunctionTble = False
					Else
						FunctionTble = CBool(wf.Item(i).FunctionTable)
					End If
					IterationTbl = wf.Item(i).IterationTable
					If String.IsNullOrEmpty(IterationTbl) Then
						IterationTbl = False
					Else
						IterationTbl = CBool(wf.Item(i).IterationTable)
					End If

					If IterationTbl Then 'AndAlso Not String.IsNullOrEmpty(trgTbl) Then
						RunCreateIterationTable(sql, trgTbl, conn, sqlitePath, handler)
					ElseIf FunctionTble AndAlso Not String.IsNullOrEmpty(trgTbl) Then
						RunCreateDataTableFromFunctionList(sql, trgTbl, conn, sqlitePath, handler)
					Else
						If Not String.IsNullOrEmpty(trgTbl) AndAlso Not (sql.ToLower.Contains("update") Or sql.ToLower.Contains("delete")) Then
							sql = "Create table " & trgTbl & " as " & sql
						Else
							'if an index name is returned, then we should skip the query
							ExistingIndexName = CheckForExistingIndex(sql, indxList)
							If Not String.IsNullOrEmpty(ExistingIndexName) Then
								SkipQuery = True
							End If
						End If

						UpdateProgress(handler, False, True, CInt((100.0R * i / wf.Count)), "Running Step " & stp & " to " & endStep + 1)
						CheckCancelled()

						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Starting step: " & stp & " Query: " & sql)
						' Execute the query in order to actually create the table.
						If SkipQuery Then
							clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Query skipped: " & stp)
						Else
							Dim cmd As New SQLiteCommand(sql, conn)
							cmd.ExecuteNonQuery()
							clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Finished step: " & stp)
						End If

					End If

				Next
				conn.Close()
				conn.Open()

				tblList = GetTablesFromDb(sqlitePath)
				'Drop all the tables not needed
				For i = startStep To endStep
					stp = wf.Item(i).StepNo
					sql = wf.Item(i).SQL
					trgTbl = wf.Item(i).TargetTable
					If String.IsNullOrEmpty(kTrgtTble) Then
						kTrgtTble = False
					Else
						kTrgtTble = CBool(wf.Item(i).KeepTargetTable)
					End If
					UpdateProgress(handler, False, True, CInt((100.0R * i / wf.Count)), "Cleaning up database for Step " & stp)

					If Not String.IsNullOrEmpty(trgTbl) AndAlso tblList.Contains(Trim(trgTbl)) AndAlso Not kTrgtTble AndAlso Not (sql.ToLower.Contains("update") Or sql.ToLower.Contains("delete")) Then
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Removing temp table: " & trgTbl & " from step: " & stp)
						sql = "Drop Table " & trgTbl
						Dim cmdDrop As New SQLiteCommand(sql, conn)
						cmdDrop.ExecuteNonQuery()
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Finished removing temp table: " & trgTbl & " from step: " & stp)
					ElseIf Not String.IsNullOrEmpty(trgTbl) AndAlso kTrgtTble AndAlso Not (sql.ToLower.Contains("update") Or sql.ToLower.Contains("delete")) Then
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, "Keeping temp table: " & trgTbl & " from step: " & stp)
					End If
					CheckCancelled()
				Next
			End If
			conn.Close()

		Catch ex As Exception
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "The following error occured while running workflow step: " & mStep & " - " & ex.Message & mSQL)
			If Not conn Is Nothing Then
				conn.Close()
			End If
			Throw
		End Try
	End Sub

	Private Shared Sub RunCreateIterationTable(ByVal sql As String, ByVal tname As String, ByVal conn As SQLiteConnection, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		Dim IterationTables As String()
		Dim CreateSeparateTable As Boolean
		Dim SelectSQL As String = ""
		Dim IterationTableName As String = ""
		Dim GroupByText As String = ""
		If Not String.IsNullOrEmpty(sql) Then
			IterationTables = Split(sql, "|")
			If IterationTables.Length = 4 Then
				'TODO:
				SelectSQL = IterationTables(0)
				GroupByText = IterationTables(1)
				IterationTableName = IterationTables(2)
				CreateSeparateTable = IterationTables(3)
			End If
		End If
		RunIterationTable(SelectSQL, CreateSeparateTable, IterationTableName, tname, GroupByText, sqlitePath, Nothing, handler)
	End Sub

	Private Shared Sub RunIterationTable(ByVal SQL As String, ByVal CreateSeparateTable As Boolean, ByVal iterationTableName As String, ByVal newTblName As String, ByVal groupByText As String, ByVal sqlitePath As String, ByVal password As String, ByVal handler As SqlConversionHandler)
		CheckCancelled()
		UpdateProgress(handler, False, True, 0, "Preparing to insert tables...")
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "preparing to insert tables ...")

		' Connect to the SQL Server database
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Dim tf As New TableFunctions.TblFunctions

		Using slconn As New SQLiteConnection(sqliteConnString)
			slconn.Open()

			' Connect to the SQLite database next
			Using sl2conn As New SQLiteConnection(sqliteConnString)
				sl2conn.Open()

				Dim tx As SQLiteTransaction = sl2conn.BeginTransaction()

				' Go over all rows in the parameter table and insert result rows
				Try
					Dim tableQuery As String = "Select * from " + iterationTableName
					Dim query As New SQLiteCommand(tableQuery, slconn)
					Dim counter As Integer = 0

					Dim colName As String
					Dim colValue As String
					Dim fldName As String
					Dim fldValue As String
					Dim fullSql As String = ""
					Dim whereClause As String = ""
					Dim fldOperator As String()
					Dim firstPass As Boolean = True
					Dim createTableSQL As String = ""
					Dim selectTxt As String = ""

					Using reader As SQLiteDataReader = query.ExecuteReader()
						Dim insert As New SQLiteCommand
						insert.Connection = sl2conn
						insert.Transaction = tx

						'Remove the select portion so the param list name can be added
						SQL = SQL.Substring(SQL.ToLower.IndexOf("select") + 6, SQL.Length - (SQL.ToLower.IndexOf("select") + 6))

						selectTxt = "select """" as ParamField, "
						If Not CreateSeparateTable Then
							createTableSQL = "CREATE TABLE " & newTblName & " as " & selectTxt & SQL & vbCrLf & " Where 1=0 group by """"" '( ParamSetName, " & groupByField & ", " & "Cnt  float);"
							insert.CommandText = createTableSQL
							insert.CommandType = CommandType.Text
							insert.ExecuteNonQuery()
						End If

						While reader.Read()
							colName = CStr(reader.GetName(0))
							colValue = CStr(reader.GetValue(0))
							fullSql = ""
							whereClause = ""
							selectTxt = "select """ & colValue & """ as " & colName & ", "
							'SQL = "SELECT """ & fldValue & """ as " & fldName & ", " & groupByField & ", count(*) as Cnt " & vbCrLf & " From " & sourceTblName & vbCrLf

							For i As Integer = 1 To (reader.FieldCount - 1) Step 2
								fldName = CStr(reader.GetName(i))
								fldValue = CStr(reader.GetValue(i))
								fldOperator = Split(CStr(reader.GetValue(i + 1)), ";")
								If fldOperator.Length > 2 Then
									whereClause = whereClause & fldName & " " & fldOperator(0) & " " & fldValue & " and " & fldName & " " & fldOperator(1) & " " & CStr(CDbl(fldValue) + CDbl(fldOperator(2))) & " " & " and "
								Else
									whereClause = whereClause & fldName & " " & fldOperator(0) & " " & fldValue & " " & " and "
								End If
							Next

							If whereClause.EndsWith("and ") Then
								whereClause = whereClause.Substring(0, whereClause.Length - 4)
							End If
							whereClause = whereClause & vbCrLf & groupByText
							If CreateSeparateTable Then
								fullSql = "Create Table " & colValue & " as " & vbCrLf & selectTxt & SQL & " Where " & whereClause
							Else
								fullSql = "INSERT INTO " & newTblName & " " & vbCrLf & selectTxt & SQL & " Where " & whereClause
							End If
							insert.CommandText = fullSql
							insert.CommandType = CommandType.Text
							insert.ExecuteNonQuery()

							counter += 1
							If counter Mod 1000 = 0 Then
								CheckCancelled()
								UpdateProgress(handler, False, True, CInt((100.0R * counter / 10)), counter & " Iterations run so far")
							End If
							whereClause = ""
							firstPass = False

						End While
					End Using
					' using
					tx.Commit()
					CheckCancelled()

					UpdateProgress(handler, False, True, CInt((100.0R * counter / 10)), "Finished running all iterations ")
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished running all iterations")
				Catch ex As Exception
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "CopySQLiteDBRowsToSQliteDB: Unexpected exception: " & ex.Message)
					Throw
					' catch
				End Try
				' using
				sl2conn.Close()
			End Using
			' using
			slconn.Close()
		End Using
	End Sub

	Private Shared Sub RunCreateDataTableFromFunctionList(ByVal sql As String, ByVal tname As String, ByVal conn As SQLiteConnection, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		If Not String.IsNullOrEmpty(Trim(tname)) Then
			Dim lsTs As New List(Of TableSchema)
			lsTs = CreateSqliteFunctionTableSchema(Split(sql, vbCrLf), tname)

			If lsTs IsNot Nothing Then
				' Create the SQLite database and apply the schema
				CreateSQLiteTables(conn, lsTs, handler)

				' Copy all rows from SQL Server tables to the newly created SQLite database
				CopySQLiteDBRowsToSQliteDB(mFldDefinitions, mSourceTableName, mCurrentFunctionList, conn, lsTs, sqlitePath, handler)
			End If
		Else
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "No table was specified function table.")
		End If
	End Sub

	Private Shared Sub CopySQLiteDBRowsToSQliteDB(ByVal fldDefinitionList As Hashtable, ByVal sourceTblName As String, ByVal functionList As List(Of TableFunctions.SingleReturnFunction), ByVal slconn As SQLiteConnection, ByVal schema As List(Of TableSchema), ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		CheckCancelled()
		UpdateProgress(handler, False, True, 0, "Preparing to insert tables...")
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "preparing to insert tables ...")

		' Connect to the SQL Server database
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Dim tf As New TableFunctions.TblFunctions

		' Connect to the SQLite database next
		Using sl2conn As New SQLiteConnection(sqliteConnString)
			sl2conn.Open()

			' Go over all tables in the schema and copy their rows
			For i As Integer = 0 To schema.Count - 1
				Dim tx As SQLiteTransaction = sl2conn.BeginTransaction()
				Try
					Dim tableQuery As String = BuildSqliteCustomTableQuery(sourceTblName, fldDefinitionList)
					Dim query As New SQLiteCommand(tableQuery, slconn)
					Dim dr As DataRow = BuildDataRow(fldDefinitionList)
					Dim de As DictionaryEntry
					Dim fldFldType As String()

					Using reader As SQLiteDataReader = query.ExecuteReader()
						Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(i))
						Dim counter As Integer = 0
						While reader.Read()
							insert.Connection = sl2conn
							insert.Transaction = tx

							For Each de In fldDefinitionList
								fldFldType = Split(de.Key, "|")
								If TypeOf reader(fldFldType(0)) Is DBNull Then
									dr.Item(fldFldType(0)) = DBNull.Value
								Else
									dr.Item(fldFldType(0)) = reader(fldFldType(0)) ' CastValueForColumn(reader(fldFldType(0)), schema(i).Columns(j))
								End If
							Next de

							tf.Functions = functionList
							dr = tf.PerformFunction(dr)

							Dim pnames As New List(Of String)()
							For j As Integer = 0 To schema(i).Columns.Count - 1
								Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
								insert.Parameters(pname).Value = dr.Item(schema(i).Columns(j).ColumnName)
								pnames.Add(pname)
							Next
							insert.ExecuteNonQuery()
							counter += 1
							If counter Mod 1000 = 0 Then
								CheckCancelled()
								'tx.Commit()
								UpdateProgress(handler, False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter & " rows to table ") + schema(i).TableName & " so far")
								'tx = sl2conn.BeginTransaction()
							End If
							' while
						End While
					End Using
					' using
					CheckCancelled()
					tx.Commit()

					UpdateProgress(handler, False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished inserting all rows for table [" & schema(i).TableName & "]")
				Catch ex As Exception
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "CopySQLiteDBRowsToSQliteDB: Unexpected exception: " & ex.Message)
					tx.Rollback()
					Throw
					' catch
				End Try
			Next
			' using
			sl2conn.Close()
		End Using
	End Sub

	Private Shared Function BuildSqliteCustomTableQuery(ByVal tblNameOverride As String, ByVal colList As Hashtable) As String
		Dim sb As New StringBuilder()
		Dim fldFldType As String()
		Dim i As Integer
		sb.Append("SELECT ")

		Dim de As DictionaryEntry
		For Each de In colList
			fldFldType = Split(de.Key, "|")
			sb.Append("[" & fldFldType(0) & "]")
			If i < colList.Count - 1 Then
				sb.Append(", ")
			End If
			i += 1
		Next de

		sb.Append(" FROM [" & tblNameOverride & "]")
		Return sb.ToString()
	End Function

	Private Shared Function BuildDataRow(ByVal fldDefList As Hashtable) As DataRow
		Dim tbl As DataTable = New DataTable("TempDb")
		Dim dr As DataRow
		Dim fldFldType As String()
		Dim de As DictionaryEntry
		For Each de In fldDefList
			fldFldType = Split(de.Key, "|")
			Dim idColumn As DataColumn = New DataColumn()
			idColumn.DataType = System.Type.GetType(GetSQLiteStringColumnType(fldFldType(1)))
			idColumn.ColumnName = fldFldType(0)
			tbl.Columns.Add(idColumn)
		Next de

		If Not mCurrentFunctionList Is Nothing AndAlso mCurrentFunctionList.Count > 0 Then
			For i As Integer = 0 To mCurrentFunctionList.Count - 1
				Dim fldName As String = mCurrentFunctionList(i).NewFieldName
				Dim datatype As System.Type = mCurrentFunctionList(i).ReturnDataType

				Dim idColumn As DataColumn = New DataColumn()
				idColumn.DataType = System.Type.GetType(GetSQLiteStringColumnType(datatype.ToString))
				idColumn.ColumnName = fldName
				tbl.Columns.Add(idColumn)
			Next
		End If

		dr = tbl.NewRow

		Return dr
	End Function

	Private Shared Function GetSQLiteStringColumnType(ByVal dataType As String) As String

		If dataType.ToLower = "integer" Then
			dataType = "System.Int64"
		ElseIf dataType.ToLower = "double" Then
			dataType = "System.Double"
		ElseIf dataType.ToLower = "text" Then
			dataType = "System.String"
		ElseIf dataType.ToLower = "char" Then
			dataType = "System.String"
		ElseIf dataType.ToLower = "datetime" Then
			dataType = "System.Datetime"
		ElseIf dataType.ToLower = "single" Then
			dataType = "System.Single"
		ElseIf dataType.ToLower = "decimal" Then
			dataType = "System.Decimal"
			'ElseIf dataType.ToLower = "float" Then
			'    dataType = "double"
			'    'dataType = "single"
			'ElseIf dataType.ToLower = "real" Then
			'    dataType = "double"
			'    'dataType = "single"
			'ElseIf dataType.ToLower = "double" Then
			'    dataType = "double"
			'    'dataType = "single"
			'ElseIf dataType.ToLower = "integer" Then
			'    dataType = "integer"
			'ElseIf dataType.ToLower = "char" Then
			'    dataType = "char"
			'ElseIf dataType.ToLower = "smallint" Then
			'    dataType = "integer"
			'    'dataType = "decimal"
		End If

		Return dataType

	End Function

	Private Shared Sub CreateSQLiteTables(ByVal conn As SQLiteConnection, ByVal schema As List(Of TableSchema), ByVal handler As SqlConversionHandler)
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Creating SQLite tables...")

		' Create all tables in the new database
		Dim count As Integer = 0
		For Each dt As TableSchema In schema
			Try
				AddSQLiteTable(conn, dt)
			Catch ex As Exception
				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "AddSQLiteTable failed: " & ex.Message)
				Throw
			End Try
			count += 1
			CheckCancelled()
			UpdateProgress(handler, False, True, CInt((count * 100.0R / schema.Count)), "Added table " & dt.TableName & " to the SQLite database")

			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "added schema for SQLite table [" & dt.TableName & "]")
			' foreach
		Next

		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished adding all table schemas for SQLite database")
	End Sub


	Private Shared Function CreateSqliteFunctionTableSchema(ByVal selectCommandList() As String, ByVal NewTableName As String) As List(Of TableSchema)
		Dim tables As New List(Of TableSchema)()
		Dim res As New TableSchema()
		Dim FieldFieldType As String()
		Dim i As Integer
		Dim j As Integer
		Dim rows() As String
		Dim fnctn As String
		Dim flds As String
		Dim colName As String
		Dim parmList As String
		Dim FieldListNames As String()
		Dim TmpFldFldTypeList As String()

		If mFldDefinitions Is Nothing Then
			mFldDefinitions = New Hashtable
		End If
		mFldDefinitions.Clear()
		If mCurrentFunctionList Is Nothing Then
			mCurrentFunctionList = New List(Of TableFunctions.SingleReturnFunction)
		End If
		mCurrentFunctionList.Clear()
		res.Columns = New List(Of ColumnSchema)()

		res.TableName = NewTableName
		mSourceTableName = selectCommandList(0)
		'Gather list of table fields first
		For i = 1 To selectCommandList.Count - 1
			If Not String.IsNullOrEmpty(selectCommandList(i).ToString) Then
				rows = selectCommandList(i).ToString.Split(";")
				fnctn = Trim(rows(FunctionTableFields.wFunction))
				flds = Trim(rows(FunctionTableFields.wFieldList))
				colName = Trim(rows(FunctionTableFields.wNewColumnName))
				parmList = Trim(rows(FunctionTableFields.wParameterList))
				TmpFldFldTypeList = Split(flds, ",")
				If fnctn = TABLE_COLUMN Then
					FieldFieldType = Split(TmpFldFldTypeList(j), "|")
					Dim col As New ColumnSchema()
					col.ColumnName = FieldFieldType(0)
					col.ColumnType = FieldFieldType(1)
					col.IsNullable = True
					col.IsIdentity = False
					col.DefaultValue = String.Empty
					res.Columns.Add(col)
					mFldDefinitions.Add(TmpFldFldTypeList(j), TABLE_COLUMN)
				End If
			End If
		Next

		'Now gather list of function fields
		For i = 1 To selectCommandList.Count - 1
			If Not String.IsNullOrEmpty(selectCommandList(i).ToString) Then
				rows = selectCommandList(i).ToString.Split(";")
				fnctn = Trim(rows(FunctionTableFields.wFunction))
				flds = Trim(rows(FunctionTableFields.wFieldList))
				colName = Trim(rows(FunctionTableFields.wNewColumnName))
				parmList = Trim(rows(FunctionTableFields.wParameterList))
				TmpFldFldTypeList = Split(flds, ",")
				If fnctn <> TABLE_COLUMN Then
					ReDim FieldListNames(TmpFldFldTypeList.Count - 1)
					For j = 0 To TmpFldFldTypeList.Count - 1
						FieldFieldType = Split(TmpFldFldTypeList(j), "|")
						If Not mFldDefinitions.Contains(TmpFldFldTypeList(j)) Then
							mFldDefinitions.Add(TmpFldFldTypeList(j), TABLE_COLUMN)
						End If
						FieldListNames(j) = FieldFieldType(0)
					Next
					Dim tf As New TableFunctions.TblFunctions
					Dim newFunction As New TableFunctions.SingleReturnFunction
					For k As Integer = 0 To tf.AvailableFunctions.Count - 1
						If tf.AvailableFunctions(k).Name = fnctn Then
							'newFunction.NewFieldName = colName
							tf.AvailableFunctions(k).NewFieldName = colName
							newFunction = New TableFunctions.SingleReturnFunction
							newFunction = tf.AvailableFunctions(k)
							Exit For
						Else
							newFunction = Nothing
						End If
					Next
					If Not newFunction Is Nothing Then
						Dim fldList As New List(Of String)(FieldListNames.Length)
						fldList.AddRange(FieldListNames)
						newFunction.FieldList = fldList
						mCurrentFunction = newFunction
						mCurrentFunctionList.Add(newFunction)
					End If
					Dim datatype As System.Type = mCurrentFunction.ReturnDataType
					Dim col1 As New ColumnSchema()
					col1.ColumnName = colName
					col1.ColumnType = GetStringColumnType(datatype.ToString)
					col1.IsNullable = True
					col1.IsIdentity = False
					col1.DefaultValue = String.Empty
					res.Columns.Add(col1)
				End If
			End If
		Next
		tables.Add(res)

		Return tables

	End Function

	Private Shared Function GetStringColumnType(ByVal dataType As String) As String

		If dataType.ToLower = "system.int32" Then
			dataType = "integer"
		ElseIf dataType.ToLower = "system.byte" Then
			dataType = "integer"
		ElseIf dataType.ToLower = "system.int16" Then
			dataType = "integer"
		ElseIf dataType.ToLower = "system.int64" Then
			dataType = "integer"
		ElseIf dataType.ToLower = "system.double" Then
			dataType = "double"
		ElseIf dataType.ToLower = "system.string" Then
			dataType = "text"
		ElseIf dataType.ToLower = "system.datetime" Then
			dataType = "datetime"
		ElseIf dataType.ToLower = "system.single" Then
			dataType = "single"
		ElseIf dataType.ToLower = "system.decimal" Then
			dataType = "decimal"
			'ElseIf dataType.ToLower = "float" Then
			'    dataType = "double"
			'    'dataType = "single"
			'ElseIf dataType.ToLower = "real" Then
			'    dataType = "double"
			'    'dataType = "single"
			'ElseIf dataType.ToLower = "double" Then
			'    dataType = "double"
			'    'dataType = "single"
			'ElseIf dataType.ToLower = "integer" Then
			'    dataType = "integer"
			'ElseIf dataType.ToLower = "char" Then
			'    dataType = "char"
			'ElseIf dataType.ToLower = "smallint" Then
			'    dataType = "integer"
			'    'dataType = "decimal"
		End If

		Return dataType

	End Function

	Private Shared Sub InitializeTableFunctions()
		Dim tf As TableFunctions.TblFunctions
		Dim iCatIndex As Integer
		tf = New TableFunctions.TblFunctions
		mFunctionsList = tf.AvailableFunctions

		Dim s_FunctionNames As String() = New String(mFunctionsList.Count - 1) {}
		Dim s_FunctionCategories As New List(Of String)

		For i As Integer = 0 To s_FunctionNames.Length - 1
			If Not s_FunctionCategories.Contains(mFunctionsList(i).Category.ToString) Then
				s_FunctionCategories.Add(mFunctionsList(i).Category.ToString)
			End If
		Next

		For i As Integer = 0 To s_FunctionNames.Length - 1
			s_FunctionNames(i) = mFunctionsList(i).Name
			iCatIndex = s_FunctionCategories.IndexOf(mFunctionsList(i).Category.ToString)
		Next

	End Sub

	Private Shared Function CheckForExistingIndex(ByVal sql, ByVal indxList) As String
		Dim IndexName As String = String.Empty
		Dim sqlLines As String()
		If Not String.IsNullOrEmpty(sql.trim) Then
			sqlLines = sql.split(vbCrLf)
			For Each s As String In sqlLines
				s = s.ToLower.TrimStart() 's.TrimStart(" "c).ToLower 
				'look for first non comment line
				If s.Length > 0 Then
					If s.Substring(0, 2) = "--" Then
						'comment so ignore
					Else
						If s.Substring(0, 12) = "create index" Then
							For Each Str As String In indxList
								If s.ToLower.Contains(Str.ToLower) Then
									IndexName = Str
								End If
							Next
						End If
						Exit For
					End If
				End If
			Next s
		End If
		Return IndexName
	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="connString"></param>
	''' <param name="sql"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Private Shared Function BuildCrosstabTableQuery(ByVal connString As String, ByVal sql As String()) As String
		Dim valueField As String = String.Empty
		Dim colHeading As String = String.Empty
		Dim Table As String = String.Empty
		Dim qry As String = Nothing
		Dim fldList As String = String.Empty
		Dim grpBy As String = String.Empty
		Dim fldQry As String = String.Empty
		Dim caseQry As String = String.Empty
		Dim numColumns As Integer = 0
		Dim i As Integer
		Dim rows() As String

		Try
			If sql.Count > 0 Then
				'First sort the columns
				For i = 0 To sql.Count - 1
					'ignore first line which should be a select
					If Not String.IsNullOrEmpty(sql(i).ToString) Then
						rows = sql(i).ToString.Split(",")
						If Trim(rows(crosstabFields.wCrosstab)) = VALUE Then
							valueField = Trim(rows(crosstabFields.wField))
							Table = Trim(rows(crosstabFields.wTable))
						End If
						If Trim(rows(crosstabFields.wCrosstab)) = COLUMN_HEADING Then
							colHeading = Trim(rows(crosstabFields.wField))
						End If
						If Trim(rows(crosstabFields.wCrosstab)) = ROW_HEADING Then
							fldList += Trim(rows(crosstabFields.wField)) & "," & vbCrLf
						End If
					End If
				Next
				qry = "Select distinct " & colHeading & " From " & Table

				Using conn As New SQLiteConnection(connString)
					conn.Open()

					Dim cmd As New SQLiteCommand
					cmd = conn.CreateCommand
					cmd.CommandText = qry
					Using reader As SQLiteDataReader = cmd.ExecuteReader()
						While reader.Read()
							fldQry += "ifnull(Max([" & reader.GetValue(0) & "]),'') as [" & reader.GetValue(0) & "]" & "," & vbCrLf
							caseQry += "Case when " & colHeading & " = '" & reader.GetValue(0) & "' then " & valueField & " end as '" & reader.GetValue(0) & "'," & vbCrLf
							numColumns += 1
						End While
					End Using
					conn.Close()
				End Using

				caseQry = caseQry.Substring(0, caseQry.LastIndexOf(","c))
				fldQry = fldQry.Substring(0, fldQry.LastIndexOf(","c))

				grpBy = " Group By " & fldList.Substring(0, fldList.LastIndexOf(","c))

				qry = "Select " & vbCrLf & fldList & fldQry & " From ( Select " & vbCrLf & fldList & vbCrLf & caseQry & vbCrLf & " From " & Table & vbCrLf & ")" & vbCrLf & grpBy
				If numColumns > NUM_FIELDS_ALLOWED Then
					qry = NUM_FIELDS_EXCEEDED_MESSAGE
				End If
			End If

		Catch ex As Exception
			MsgBox("An error has occurred: " & ex.Message)
			Throw
		End Try

		Return qry

	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="tableName"></param>
	''' <param name="fieldNames"></param>
	''' <param name="indexName"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CreateMTSCacheIndex(ByVal tableName As String, ByVal fieldNames As String, ByVal indexName As String, ByVal sqlitePath As String, ByVal handler As SqlConversionHandler)
		UpdateProgress(handler, False, True, 0, "Creating Index " & indexName & " for table: " & tableName)
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Creating Index " & indexName & " for table: " & tableName)

		'    ' Connect to the SQLite database next
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Using sqconn As New SQLiteConnection(sqliteConnString)
			sqconn.Open()
			' Go over all tables in the schema and copy their rows
			Dim indexCommand As New SQLiteCommand
			indexCommand.CommandText = "Create index " & indexName & " ON " & tableName & "(" & fieldNames & ")"
			indexCommand.CommandType = CommandType.Text
			indexCommand.Connection = sqconn
			indexCommand.ExecuteNonQuery()
			sqconn.Close()
		End Using

	End Sub

	''' <summary>
	''' Copies table rows from the SQL Server database to the SQLite database.
	''' </summary>
	''' <param name="ds">The dataset passed from stored procedure</param>
	''' <param name="sqlitePath">The path to the SQLite database file.</param>
	''' <param name="schema">The schema of the SQL Server database.</param>
	''' <param name="password">The password to use for encrypting the file</param>
	''' <param name="handler">A handler to handle progress notifications.</param>
	Private Shared Sub CopyTableRowsToSQLiteDB(ByVal ds As DataSet, ByVal sqlitePath As String, ByVal schema As List(Of TableSchema), ByVal password As String, ByVal handler As SqlConversionHandler)
		CheckCancelled()
		UpdateProgress(handler, False, True, 0, "Preparing to insert tables...")
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "preparing to insert tables ...")

		'    ' Connect to the SQLite database next
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, password)
		Using sqconn As New SQLiteConnection(sqliteConnString)
			sqconn.Open()

			' Go over all tables in the schema and copy their rows
			For i As Integer = 0 To schema.Count - 1
				Dim tx As SQLiteTransaction = sqconn.BeginTransaction()
				Try
					Dim tableQuery As String = BuildSqlServerTableQuery(schema(i))
					Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(i))
					Dim counter As Integer = 0
					Dim tbl As DataTable
					tbl = ds.Tables(i)
					Dim row As DataRow
					For Each row In tbl.Rows
						insert.Connection = sqconn
						insert.Transaction = tx

						Dim pnames As New List(Of String)()
						For j As Integer = 0 To schema(i).Columns.Count - 1
							Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
							insert.Parameters(pname).Value = CastValueForColumn(row(j), schema(i).Columns(j))
							pnames.Add(pname)
						Next
						insert.ExecuteNonQuery()
						counter += 1
						If counter Mod 1000 = 0 Then
							CheckCancelled()
							tx.Commit()
							UpdateProgress(handler, False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter & " rows to ") + schema(i).TableName)
							tx = sqconn.BeginTransaction()
						End If
					Next
					CheckCancelled()
					tx.Commit()

					UpdateProgress(handler, False, True, CInt((100.0R * i / schema.Count)), "Finished inserting for " & schema(i).TableName)
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished inserting all rows for table [" & schema(i).TableName & "]")
				Catch ex As Exception
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "unexpected exception: " & ex.Message)
					tx.Rollback()
					Throw
					' catch
				End Try
			Next
			sqconn.Close()
			' using
		End Using
	End Sub

	''' <summary>
	''' Copies table rows from the SQL Server database to the SQLite database.
	''' </summary>
	''' <param name="sqlConnString">The SQL Server connection string</param>
	''' <param name="sqlitePath">The path to the SQLite database file.</param>
	''' <param name="schema">The schema of the SQL Server database.</param>
	''' <param name="password">The password to use for encrypting the file</param>
	''' <param name="handler">A handler to handle progress notifications.</param>
	Private Shared Sub CopySqlServerRowsToSQLiteDB(ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal schema As List(Of TableSchema), ByVal password As String, ByVal handler As SqlConversionHandler)
		CheckCancelled()
		UpdateProgress(handler, False, True, 0, "Preparing to insert tables...")
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "preparing to insert tables ...")

		' Connect to the SQL Server database
		Using ssconn As New SqlConnection(sqlConnString)
			ssconn.Open()

			' Connect to the SQLite database next
			Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, password)
			Using sqconn As New SQLiteConnection(sqliteConnString)
				sqconn.Open()

				' Go over all tables in the schema and copy their rows
				For i As Integer = 0 To schema.Count - 1
					Dim tx As SQLiteTransaction = sqconn.BeginTransaction()
					Try
						Dim tableQuery As String = BuildSqlServerTableQuery(schema(i))
						Dim query As New SqlCommand(tableQuery, ssconn)
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Starting to insert all rows for table [" & schema(i).TableName & "]")
						Using reader As SqlDataReader = query.ExecuteReader()
							Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(i))
							Dim counter As Integer = 0
							While reader.Read()
								insert.Connection = sqconn
								insert.Transaction = tx
								Dim pnames As New List(Of String)()
								For j As Integer = 0 To schema(i).Columns.Count - 1
									Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
									insert.Parameters(pname).Value = CastValueForColumn(reader(j), schema(i).Columns(j))
									pnames.Add(pname)
								Next
								insert.ExecuteNonQuery()
								counter += 1
								If counter Mod 1000 = 0 Then
									CheckCancelled()
									tx.Commit()
									UpdateProgress(handler, False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter & " rows to table ") + schema(i).TableName & " so far")
									tx = sqconn.BeginTransaction()
								End If
								' while
							End While
						End Using
						' using
						CheckCancelled()
						tx.Commit()

						UpdateProgress(handler, False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished inserting all rows for table [" & schema(i).TableName & "]")
					Catch ex As Exception
						clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "unexpected exception: " & ex.Message)
						tx.Rollback()
						Throw
						' catch
					End Try
				Next
				' using
				sqconn.Close()
			End Using
			' using
			ssconn.Close()
		End Using
	End Sub

	Private Shared Function ReadWorkflow(ByVal XmlDocument As String, ByVal xmlDocumentType As Integer, ByVal ViewOnly As Boolean) As List(Of clsXMLStepSchema)

		Dim xmlReader As Xml.XmlTextReader = Nothing
		Dim workflow As New List(Of clsXMLStepSchema)
		Dim stepSchema As New clsXMLStepSchema
		Dim streamFile As System.IO.StreamReader = Nothing
		Dim streamString As System.IO.StringReader = Nothing
		Try

			Select Case xmlDocumentType
				Case xmlDocType.wFile
					If Not System.IO.File.Exists(XmlDocument) Then
						Return Nothing
					End If
					streamFile = New StreamReader(XmlDocument)
					xmlReader = New Xml.XmlTextReader(streamFile)

				Case xmlDocType.wString
					streamString = New StringReader(XmlDocument)
					xmlReader = New Xml.XmlTextReader(streamString)
			End Select

			xmlReader.WhitespaceHandling = WhitespaceHandling.None
			xmlReader.Read()

			If xmlReader.Name.Contains(clsXMLFields.MDART_WORKFLOW) Then
				xmlReader.Read()
				xmlReader.ReadElementString(clsXMLFields.TITLE)	'Mdart Workflow
				xmlReader.ReadElementString(clsXMLFields.WORKFLOW_DESCRIPTION) 'Workflow Description
			End If

			While Not xmlReader.EOF()
				xmlReader.Read()

				If Not xmlReader.IsStartElement Then
					Exit While
				End If
				stepSchema.StepNo = xmlReader.GetAttribute(clsXMLFields.STEP_ID)
				xmlReader.Read()
				stepSchema.Source = xmlReader.ReadElementString(clsXMLFields.SOURCE)
				stepSchema.SQL = xmlReader.ReadElementString(clsXMLFields.SQL_STRING)
				stepSchema.TargetTable = xmlReader.ReadElementString(clsXMLFields.TARGET_TABLE)
				stepSchema.KeepTargetTable = xmlReader.ReadElementString(clsXMLFields.KEEP_TARGET_TABLE)
				stepSchema.PivotTable = xmlReader.ReadElementString(clsXMLFields.PIVOT_TABLE)
				stepSchema.Description = xmlReader.ReadElementString(clsXMLFields.STEP_DESCRIPTION)
				stepSchema.FunctionTable = xmlReader.ReadElementString(clsXMLFields.FUNCTION_TABLE)
				stepSchema.IterationTable = xmlReader.ReadElementString(clsXMLFields.ITERATION_TABLE)
				stepSchema.WorkflowGroup = xmlReader.ReadElementString(clsXMLFields.WORKFLOW_GROUP)

				workflow.Add(stepSchema)
				stepSchema = New clsXMLStepSchema
			End While

		Catch ex As Exception
			workflow = Nothing
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "frmWorkflow:ReadWorkflowExNew - Error saving settings to XML Settings file: " & ex.Message)
			MsgBox("frmWorkflow:ReadWorkflowExNew - Error saving settings to XML Settings file:" & vbCrLf & ex.Message, MsgBoxStyle.Exclamation Or MsgBoxStyle.OkOnly, "Error")
		Finally
			If Not (xmlReader Is Nothing) Then
				xmlReader.Close()
			End If

		End Try

		Return workflow

	End Function


	''' <summary>
	''' Used in order to adjust the value received from SQL Server for the SQLite database.
	''' </summary>
	''' <param name="val">The value object</param>
	''' <param name="columnSchema">The corresponding column schema</param>
	''' <returns>SQLite adjusted value.</returns>
	Private Shared Function CastValueForColumn(ByVal val As Object, ByVal columnSchema As ColumnSchema) As Object
		If TypeOf val Is DBNull Then
			Return Nothing
		End If

		Dim dt As DbType = GetDbTypeOfColumn(columnSchema)

		Select Case dt
			Case DbType.Int32
				If TypeOf val Is Short Then
					Return CInt(CShort(val))
				End If
				If TypeOf val Is Byte Then
					Return CInt(CByte(val))
				End If
				If TypeOf val Is Long Then
					Return CInt(CLng(val))
				End If
				If TypeOf val Is Decimal Then
					Return CInt(CDec(val))
				End If
				Exit Select

			Case DbType.Int16
				If TypeOf val Is Integer Then
					Return CShort(CInt(val))
				End If
				If TypeOf val Is Byte Then
					Return CShort(CByte(val))
				End If
				If TypeOf val Is Long Then
					Return CShort(CLng(val))
				End If
				If TypeOf val Is Decimal Then
					Return CShort(CDec(val))
				End If
				Exit Select

			Case DbType.Int64
				If TypeOf val Is Integer Then
					Return CLng(CInt(val))
				End If
				If TypeOf val Is Short Then
					Return CLng(CShort(val))
				End If
				If TypeOf val Is Byte Then
					Return CLng(CByte(val))
				End If
				If TypeOf val Is Decimal Then
					Return CLng(CDec(val))
				End If
				Exit Select

			Case DbType.[Single]
				If TypeOf val Is Double Then
					Return CSng(CDbl(val))
				End If
				If TypeOf val Is Decimal Then
					Return CSng(CDec(val))
				End If
				Exit Select

			Case DbType.[Double]
				If TypeOf val Is Single Then
					Return CDbl(CSng(val))
				End If
				If TypeOf val Is Double Then
					Return CDbl(val)
				End If
				If TypeOf val Is Decimal Then
					Return CDbl(CDec(val))
				End If
				Exit Select

			Case DbType.[String]
				If TypeOf val Is Guid Then
					Return DirectCast(val, Guid).ToString()
				End If
				Exit Select

			Case DbType.Binary, DbType.[Boolean], DbType.DateTime
				Exit Select
			Case Else

				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "argument exception - illegal database type")
				Throw New ArgumentException("Illegal database type [" & [Enum].GetName(GetType(DbType), dt) & "]")
		End Select
		' switch
		Return val
	End Function

	''' <summary>
	''' Creates a command object needed to insert values into a specific SQLite table.
	''' </summary>
	''' <param name="ts">The table schema object for the table.</param>
	''' <returns>A command object with the required functionality.</returns>
	Private Shared Function BuildSQLiteInsert(ByVal ts As TableSchema) As SQLiteCommand
		Dim res As New SQLiteCommand()

		Dim sb As New StringBuilder()
		sb.Append("INSERT INTO [" & ts.TableName & "] (")
		For i As Integer = 0 To ts.Columns.Count - 1
			sb.Append("[" & ts.Columns(i).ColumnName & "]")
			If i < ts.Columns.Count - 1 Then
				sb.Append(", ")
			End If
		Next
		' for
		sb.Append(") VALUES (")

		Dim pnames As New List(Of String)()
		For i As Integer = 0 To ts.Columns.Count - 1
			Dim pname As String = "@" & GetNormalizedName(ts.Columns(i).ColumnName, pnames)
			sb.Append(pname)
			If i < ts.Columns.Count - 1 Then
				sb.Append(", ")
			End If

			Dim dbType As DbType = GetDbTypeOfColumn(ts.Columns(i))
			Dim prm As New SQLiteParameter(pname, dbType, ts.Columns(i).ColumnName)
			res.Parameters.Add(prm)

			' Remember the parameter name in order to avoid duplicates
			pnames.Add(pname)
		Next
		' for
		sb.Append(")")
		res.CommandText = sb.ToString()
		res.CommandType = CommandType.Text
		Return res
	End Function

	''' <summary>
	''' Used in order to avoid breaking naming rules (e.g., when a table has
	''' a name in SQL Server that cannot be used as a basis for a matching index
	''' name in SQLite).
	''' </summary>
	''' <param name="str">The name to change if necessary</param>
	''' <param name="names">Used to avoid duplicate names</param>
	''' <returns>A normalized name</returns>
	Private Shared Function GetNormalizedName(ByVal str As String, ByVal names As List(Of String)) As String
		Dim sb As New StringBuilder()
		For i As Integer = 0 To str.Length - 1
			If [Char].IsLetterOrDigit(str(i)) OrElse str(i) = "_"c Then
				sb.Append(str(i))
			Else
				sb.Append("_")
			End If
		Next
		' for
		' Avoid returning duplicate name
		If names.Contains(sb.ToString()) Then
			Return GetNormalizedName(sb.ToString() & "_", names)
		Else
			Return sb.ToString()
		End If
	End Function

	''' <summary>
	''' Matches SQL Server types to general DB types
	''' </summary>
	''' <param name="cs">The column schema to use for the match</param>
	''' <returns>The matched DB type</returns>
	Private Shared Function GetDbTypeOfColumn(ByVal cs As ColumnSchema) As DbType
		If cs.ColumnType = "tinyint" Then
			Return DbType.[Byte]
		End If
		If cs.ColumnType = "int" Then
			Return DbType.Int32
		End If
		If cs.ColumnType = "smallint" Then
			Return DbType.Int16
		End If
		If cs.ColumnType = "bigint" Then
			Return DbType.Int64
		End If
		If cs.ColumnType = "bit" Then
			Return DbType.[Boolean]
		End If
		If cs.ColumnType = "nvarchar" OrElse cs.ColumnType = "varchar" OrElse cs.ColumnType = "text" OrElse cs.ColumnType = "ntext" Then
			Return DbType.[String]
		End If
		If cs.ColumnType = "float" Then
			Return DbType.[Double]
		End If
		If cs.ColumnType = "real" Then
			Return DbType.[Single]
		End If
		If cs.ColumnType = "blob" Then
			Return DbType.Binary
		End If
		If cs.ColumnType = "numeric" Then
			Return DbType.[Double]
		End If
		If cs.ColumnType = "timestamp" OrElse cs.ColumnType = "datetime" Then
			Return DbType.DateTime
		End If
		If cs.ColumnType = "nchar" OrElse cs.ColumnType = "char" Then
			Return DbType.[String]
		End If
		If cs.ColumnType = "uniqueidentifier" Then
			Return DbType.[String]
		End If
		If cs.ColumnType = "xml" Then
			Return DbType.[String]
		End If
		If cs.ColumnType = "sql_variant" Then
			Return DbType.[Object]
		End If
		If cs.ColumnType = "integer" Then
			Return DbType.Int64
		End If
		If cs.ColumnType = "double" Then
			Return DbType.[Double]
		End If

		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "GetDbTypeOfColumn: illegal db type found")
		Throw New ApplicationException("GetDbTypeOfColumn: Illegal DB type found (" & cs.ColumnType & ")")
	End Function

	''' <summary>
	''' Builds a SELECT query for a specific table. Needed in the process of copying rows
	''' from the SQL Server database to the SQLite database.
	''' </summary>
	''' <param name="ts">The table schema of the table for which we need the query.</param>
	''' <returns>The SELECT query for the table.</returns>
	Private Shared Function BuildSqlServerTableQuery(ByVal ts As TableSchema) As String
		Dim sb As New StringBuilder()
		sb.Append("SELECT ")
		For i As Integer = 0 To ts.Columns.Count - 1
			sb.Append("[" & ts.Columns(i).ColumnName & "]")
			If i < ts.Columns.Count - 1 Then
				sb.Append(", ")
			End If
		Next
		' for
		sb.Append(" FROM [" & ts.TableName & "]")
		Return sb.ToString()
	End Function

	''' <summary>
	''' Creates the SQLite database from the schema read from the SQL Server.
	''' </summary>
	''' <param name="sqlitePath">The path to the generated DB file.</param>
	''' <param name="schema">The schema of the SQL server database.</param>
	''' <param name="password">The password to use for encrypting the DB or null if non is needed.</param>
	''' <param name="handler">A handle for progress notifications.</param>
	Private Shared Sub CreateSQLiteDatabase(ByVal sqlitePath As String, ByVal schema As List(Of TableSchema), ByVal password As String, ByVal handler As SqlConversionHandler)
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Creating SQLite database...")

		' Create the SQLite database file
		'SQLiteConnection.CreateFile(sqlitePath)

		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "SQLite file was created successfully at [" & sqlitePath & "]")

		' Connect to the newly created database
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, password)
		Using conn As New SQLiteConnection(sqliteConnString)
			conn.Open()

			' Create all tables in the new database
			Dim count As Integer = 0
			For Each dt As TableSchema In schema
				Try
					AddSQLiteTable(conn, dt)
				Catch ex As Exception
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "AddSQLiteTable failed: " & ex.Message)
					Throw
				End Try
				count += 1
				CheckCancelled()
				UpdateProgress(handler, False, True, CInt((count * 100.0R / schema.Count)), "Added table " & dt.TableName & " to the SQLite database")

				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "added schema for SQLite table [" & dt.TableName & "]")
				' foreach
			Next
			conn.Close()
		End Using
		' using
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished adding all table schemas for SQLite database")
	End Sub

	''' <summary>
	''' Creates the SQLite database from the schema read from the SQL Server.
	''' </summary>
	''' <param name="sqlitePath">The path to the generated DB file.</param>
	Private Shared Sub CreateSQLiteDatabaseOnly(ByVal sqlitePath As String)
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "Creating SQLite database...")

		' Create the SQLite database file
		SQLiteConnection.CreateFile(sqlitePath)

		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "SQLite file was created successfully at [" & sqlitePath & "]")

	End Sub

	''' <summary>
	''' Creates the SQLite database from the schema read from the SQL Server.
	''' </summary>
	''' <param name="sqlitePath">The path to the generated DB file.</param>
	''' <param name="schema">The schema of the SQL server database.</param>
	''' <param name="password">The password to use for encrypting the DB or null if non is needed.</param>
	''' <param name="handler">A handle for progress notifications.</param>
	Private Shared Sub AddSchemaToSQLiteDatabase(ByVal sqlitePath As String, ByVal schema As List(Of TableSchema), ByVal password As String, ByVal handler As SqlConversionHandler)

		' Connect to the newly created database
		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, password)
		Using conn As New SQLiteConnection(sqliteConnString)
			conn.Open()

			' Create all tables in the new database
			Dim count As Integer = 0
			For Each dt As TableSchema In schema
				Try
					AddSQLiteTable(conn, dt)
				Catch ex As Exception
					clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.ERROR, "AddSQLiteTable failed: " & ex.Message)
					Throw
				End Try
				count += 1
				CheckCancelled()
				UpdateProgress(handler, False, True, CInt((count * 100.0R / schema.Count)), "Added table " & dt.TableName & " to the SQLite database")

				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "added schema for SQLite table [" & dt.TableName & "]")
				' foreach
			Next
			conn.Close()
		End Using
		' using
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished adding all table schemas for SQLite database")
	End Sub

	''' <summary>
	''' Creates the CREATE TABLE DDL for SQLite and a specific table.
	''' </summary>
	''' <param name="conn">The SQLite connection</param>
	''' <param name="dt">The table schema object for the table to be generated.</param>
	Private Shared Sub AddSQLiteTable(ByVal conn As SQLiteConnection, ByVal dt As TableSchema)
		' Prepare a CREATE TABLE DDL statement
		Dim stmt As String = BuildCreateTableQuery(dt)

		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.INFO, vbLf & vbLf & stmt & vbLf & vbLf)

		' Execute the query in order to actually create the table.
		Dim cmd As New SQLiteCommand(stmt, conn)
		cmd.ExecuteNonQuery()
	End Sub

	''' <summary>
	''' returns the CREATE TABLE DDL for creating the SQLite table from the specified
	''' table schema object.
	''' </summary>
	''' <param name="ts">The table schema object from which to create the SQL statement.</param>
	''' <returns>CREATE TABLE DDL for the specified table.</returns>
	Private Shared Function BuildCreateTableQuery(ByVal ts As TableSchema) As String
		Dim sb As New StringBuilder()

		sb.Append("CREATE TABLE [" & ts.TableName & "] (" & vbLf)

		Dim pkey As Boolean = False
		For i As Integer = 0 To ts.Columns.Count - 1
			Dim col As ColumnSchema = ts.Columns(i)
			Dim cline As String = BuildColumnStatement(col, ts, pkey)
			sb.Append(cline)
			If i < ts.Columns.Count - 1 Then
				sb.Append("," & vbLf)
			End If
		Next
		' foreach
		' add primary keys...
		'If ts.PrimaryKey IsNot Nothing AndAlso (ts.PrimaryKey.Count > 0 And Not pkey) Then
		'    sb.Append("," & vbLf)
		'    sb.Append("    PRIMARY KEY (")
		'    For i As Integer = 0 To ts.PrimaryKey.Count - 1
		'        sb.Append("[" & ts.PrimaryKey(i) & "]")
		'        If i < ts.PrimaryKey.Count - 1 Then
		'            sb.Append(", ")
		'        End If
		'    Next
		'    ' for
		'    sb.Append(")" & vbLf)
		'Else
		'    sb.Append(vbLf)
		'End If

		sb.Append(vbLf)
		sb.Append(");" & vbLf)

		' Create any relevant indexes
		'If ts.Indexes IsNot Nothing Then
		'    For i As Integer = 0 To ts.Indexes.Count - 1
		'        Dim stmt As String = BuildCreateIndex(ts.TableName, ts.Indexes(i))
		'        sb.Append(stmt & ";" & vbLf)
		'        ' for
		'    Next
		'End If
		' if
		Dim query As String = sb.ToString()
		Return query
	End Function

	''' <summary>
	''' Creates a CREATE INDEX DDL for the specified table and index schema.
	''' </summary>
	''' <param name="tableName">The name of the indexed table.</param>
	''' <param name="indexSchema">The schema of the index object</param>
	''' <returns>A CREATE INDEX DDL (SQLite format).</returns>
	Private Shared Function BuildCreateIndex(ByVal tableName As String, ByVal indexSchema As IndexSchema) As String
		Dim sb As New StringBuilder()
		sb.Append("CREATE ")
		If indexSchema.IsUnique Then
			sb.Append("UNIQUE ")
		End If
		sb.Append(("INDEX [" & tableName & "_") + indexSchema.IndexName & "]" & vbLf)
		sb.Append("ON [" & tableName & "]" & vbLf)
		sb.Append("(")
		For i As Integer = 0 To indexSchema.Columns.Count - 1
			sb.Append("[" & indexSchema.Columns(i).ColumnName & "]")
			If Not indexSchema.Columns(i).IsAscending Then
				sb.Append(" DESC")
			End If
			If i < indexSchema.Columns.Count - 1 Then
				sb.Append(", ")
			End If
		Next
		' for
		sb.Append(")")

		Return sb.ToString()
	End Function

	''' <summary>
	''' Used when creating the CREATE TABLE DDL. Creates a single row
	''' for the specified column.
	''' </summary>
	''' <param name="col">The column schema</param>
	''' <returns>A single column line to be inserted into the general CREATE TABLE DDL statement</returns>
	Private Shared Function BuildColumnStatement(ByVal col As ColumnSchema, ByVal ts As TableSchema, ByRef pkey As Boolean) As String
		Dim sb As New StringBuilder()
		sb.Append(vbTab & """" & col.ColumnName & """" & vbTab & vbTab)

		' Special treatment for IDENTITY columns
		'If col.IsIdentity Then
		'    If ts.PrimaryKey.Count = 1 AndAlso (col.ColumnType = "tinyint" OrElse col.ColumnType = "int" OrElse col.ColumnType = "smallint" OrElse col.ColumnType = "bigint") Then
		'        sb.Append("integer PRIMARY KEY AUTOINCREMENT")
		'        pkey = True
		'    Else
		'        sb.Append("integer")
		'    End If
		'Else
		If col.ColumnType = "int" Then
			sb.Append("integer")
		Else
			sb.Append(col.ColumnType)
		End If
		'End If
		If Not col.IsNullable Then
			sb.Append(" NOT NULL")
		End If

		Dim defval As String = StripParens(col.DefaultValue)
		defval = DiscardNational(defval)
		'clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, ("DEFAULT VALUE BEFORE [" & col.DefaultValue & "] AFTER [") + defval & "]")
		If defval <> String.Empty AndAlso defval.ToUpper().Contains("GETDATE") Then
			clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "converted SQL Server GETDATE() to CURRENT_TIMESTAMP for column [" & col.ColumnName & "]")
			sb.Append(" DEFAULT (CURRENT_TIMESTAMP)")
		ElseIf defval <> String.Empty AndAlso IsValidDefaultValue(defval) Then
			sb.Append(" DEFAULT " & defval)
		End If

		Return sb.ToString()
	End Function

	''' <summary>
	''' Discards the national prefix if exists (e.g., N'sometext') which is not
	''' supported in SQLite.
	''' </summary>
	''' <param name="value">The value.</param>
	''' <returns></returns>
	Private Shared Function DiscardNational(ByVal value As String) As String
		Dim rx As New Regex("N\'([^\']*)\'")
		Dim m As Match = rx.Match(value)
		If m.Success Then
			Return m.Groups(1).Value
		Else
			Return value
		End If
	End Function

	''' <summary>
	''' Check if the DEFAULT clause is valid by SQLite standards
	''' </summary>
	''' <param name="value"></param>
	''' <returns></returns>
	Private Shared Function IsValidDefaultValue(ByVal value As String) As Boolean
		If IsSingleQuoted(value) Then
			Return True
		End If

		Dim testnum As Double
		If Not Double.TryParse(value, testnum) Then
			Return False
		End If
		Return True
	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="value"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Private Shared Function IsSingleQuoted(ByVal value As String) As Boolean
		value = value.Trim()
		If value.StartsWith("'") AndAlso value.EndsWith("'") Then
			Return True
		End If
		Return False
	End Function

	''' <summary>
	''' Strip any parentheses from the string.
	''' </summary>
	''' <param name="value">The string to strip</param>
	''' <returns>The stripped string</returns>
	Private Shared Function StripParens(ByVal value As String) As String
		Dim rx As New Regex("\(([^\)]*)\)")
		Dim m As Match = rx.Match(value)
		If Not m.Success Then
			Return value
		Else
			Return StripParens(m.Groups(1).Value)
		End If
	End Function

	''' <summary>
	''' Reads the entire SQL Server DB schema using the specified connection string.
	''' </summary>
	''' <param name="connString">The connection string used for reading SQL Server schema.</param>
	''' <param name="handler">A handler for progress notifications.</param>
	''' <param name="selectionHandler">The selection handler which allows the user to select 
	''' which tables to convert.</param>
	''' <returns>List of table schema objects for every table in the SQL Server database.</returns>
	Private Shared Function ReadSqlServerSchema(ByVal connString As String, ByVal handler As SqlConversionHandler, ByVal selectionHandler As SqlTableSelectionHandler) As List(Of TableSchema)
		' First step is to read the names of all tables in the database
		Dim tables As New List(Of TableSchema)()
		Using conn As New SqlConnection(connString)
			conn.Open()

			Dim tableNames As New List(Of String)()
			' This command will read the names of all tables in the database
			Dim cmd As New SqlCommand("select * from INFORMATION_SCHEMA.TABLES order by TABLE_TYPE, TABLE_NAME", conn)
			'select * from INFORMATION_SCHEMA.TABLES  where TABLE_TYPE = 'BASE TABLE'", conn)
			Using reader As SqlDataReader = cmd.ExecuteReader()
				While reader.Read()
					tableNames.Add(DirectCast(reader("TABLE_NAME"), String))
				End While
			End Using
			' using
			' Next step is to use ADO APIs to query the schema of each table.
			Dim count As Integer = 0
			For Each tname As String In tableNames
				Dim ts As TableSchema = CreateTableSchema(conn, tname)
				'CreateForeignKeySchema(conn, ts)
				tables.Add(ts)
				count += 1
				CheckCancelled()
				UpdateProgress(handler, False, True, CInt((count * 100.0R / tableNames.Count)), "Parsed table " & tname)

				clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "parsed table schema for [" & tname & "]")
				' foreach
			Next
			conn.Close()
		End Using
		' using
		clsLogTools.WriteLog(clsLogTools.LoggerTypes.LogFile, clsLogTools.LogLevels.DEBUG, "finished parsing all tables in SQL Server schema")

		' Allow the user a chance to select which tables to convert
		If selectionHandler IsNot Nothing Then
			Dim updated As List(Of TableSchema) = selectionHandler(tables)
			If updated IsNot Nothing Then
				Return updated
			Else
				Return Nothing
			End If
		End If
		' if
		Return tables
	End Function

	''' <summary>
	''' Convenience method for checking if the conversion progress needs to be cancelled.
	''' </summary>
	Private Shared Sub CheckCancelled()
		If _cancelled Then
			Throw New ApplicationException("User cancelled the process")
		End If
	End Sub

	''' <summary>
	''' Creates a TableSchema object using the specified SQL Server connection
	''' and the name of the table for which we need to create the schema.
	''' </summary>
	''' <param name="conn">The SQL Server connection to use</param>
	''' <param name="tableName">The name of the table for which we wants to create the table schema.</param>
	''' <returns>A table schema object that represents our knowledge of the table schema</returns>
	Private Shared Function CreateTableSchema(ByVal conn As SqlConnection, ByVal tableName As String) As TableSchema
		Dim res As New TableSchema()
		res.TableName = tableName
		res.Columns = New List(Of ColumnSchema)()
		Dim cmd As New SqlCommand(("SELECT COLUMN_NAME,COLUMN_DEFAULT,IS_NULLABLE,DATA_TYPE, " & " (columnproperty(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity')) AS [IDENT] " & "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '") + tableName & "' ORDER BY " & "ORDINAL_POSITION ASC", conn)
		Using reader As SqlDataReader = cmd.ExecuteReader()
			While reader.Read()
				Dim tmp As Object = reader("COLUMN_NAME")
				If TypeOf tmp Is DBNull Then
					Continue While
				End If
				Dim colName As String = DirectCast(reader("COLUMN_NAME"), String)

				tmp = reader("COLUMN_DEFAULT")
				Dim colDefault As String
				If TypeOf tmp Is DBNull Then
					colDefault = String.Empty
				Else
					colDefault = DirectCast(tmp, String)
				End If

				tmp = reader("IS_NULLABLE")
				Dim isNullable As Boolean = (DirectCast(tmp, String) = "YES")
				Dim dataType As String = DirectCast(reader("DATA_TYPE"), String)

				tmp = reader("IDENT")
				Dim isIdentity As Boolean
				If TypeOf tmp Is DBNull Then
					isIdentity = False
				Else
					isIdentity = If(CInt(reader("IDENT")) = 1, True, False)
				End If

				ValidateDataType(dataType)

				' Note that not all data type names need to be converted because
				' SQLite establishes type affinity by searching certain strings
				' in the type name. For example - everything containing the string
				' 'int' in its type name will be assigned an INTEGER affinity
				If dataType = "timestamp" Then
					dataType = "blob"
				ElseIf dataType = "datetime" OrElse dataType = "smalldatetime" Then
					dataType = "datetime"
				ElseIf dataType = "decimal" Then
					dataType = "double"
					'dataType = "numeric"
				ElseIf dataType = "money" OrElse dataType = "smallmoney" Then
					dataType = "numeric"
				ElseIf dataType = "binary" OrElse dataType = "varbinary" OrElse dataType = "image" Then
					dataType = "blob"
				ElseIf dataType = "tinyint" Then
					dataType = "smallint"
				ElseIf dataType = "bigint" Then
					dataType = "integer"
				ElseIf dataType = "sql_variant" Then
					dataType = "blob"
				ElseIf dataType = "xml" Then
					dataType = "varchar"
				ElseIf dataType = "uniqueidentifier" Then
					dataType = "varchar"
				ElseIf dataType = "ntext" Then
					dataType = "text"
				ElseIf dataType = "nchar" Then
					dataType = "char"
				End If

				If dataType = "bit" OrElse dataType = "int" Then
					If colDefault = "('False')" Then
						colDefault = "(0)"
					ElseIf colDefault = "('True')" Then
						colDefault = "(1)"
					End If
				End If

				colDefault = FixDefaultValueString(colDefault)

				Dim col As New ColumnSchema()
				col.ColumnName = colName
				col.ColumnType = dataType
				col.IsNullable = isNullable
				col.IsIdentity = isIdentity
				col.DefaultValue = AdjustDefaultValue(colDefault)
				res.Columns.Add(col)
				' while
			End While
		End Using
		' using
		' Find PRIMARY KEY information
		'Dim cmd2 As New SqlCommand("EXEC sp_pkeys '" & tableName & "'", conn)
		'Using reader As SqlDataReader = cmd2.ExecuteReader()
		'    res.PrimaryKey = New List(Of String)()
		'    While reader.Read()
		'        Dim colName As String = DirectCast(reader("COLUMN_NAME"), String)
		'        res.PrimaryKey.Add(colName)
		'        ' while
		'    End While
		'End Using
		' using
		' Find COLLATE information for all columns in the table
		Dim cmd4 As New SqlCommand("EXEC sp_tablecollations '" & tableName & "'", conn)
		Using reader As SqlDataReader = cmd4.ExecuteReader()
			While reader.Read()
				Dim isCaseSensitive As System.Nullable(Of Boolean) = Nothing
				Dim colName As String = DirectCast(reader("name"), String)

				isCaseSensitive = False
				'JDS Research
				'If reader("tds_collation") <> DBNull.Value Then
				'    Dim mask As Byte() = DirectCast(reader("tds_collation"), Byte())
				'    If (mask(2) And &H10) <> 0 Then
				'        isCaseSensitive = False
				'    Else
				'        isCaseSensitive = True
				'    End If
				'End If
				' if
				If isCaseSensitive.HasValue Then
					' Update the corresponding column schema.
					For Each csc As ColumnSchema In res.Columns
						If csc.ColumnName = colName Then
							csc.IsCaseSensitivite = isCaseSensitive
							Exit For
						End If
						' foreach
					Next
					' if
				End If
				' while
			End While
		End Using
		' using
		'Try
		'    ' Find index information
		'    Dim cmd3 As New SqlCommand("exec sp_helpindex '" & tableName & "'", conn)
		'    Using reader As SqlDataReader = cmd3.ExecuteReader()
		'        res.Indexes = New List(Of IndexSchema)()
		'        While reader.Read()
		'            Dim indexName As String = DirectCast(reader("index_name"), String)
		'            Dim desc As String = DirectCast(reader("index_description"), String)
		'            Dim keys As String = DirectCast(reader("index_keys"), String)

		'            ' Don't add the index if it is actually a primary key index
		'            If desc.Contains("primary key") Then
		'                Continue While
		'            End If

		'            Dim index As IndexSchema = BuildIndexSchema(indexName, desc, keys)
		'            res.Indexes.Add(index)
		'            ' while
		'        End While
		'        ' using
		'    End Using
		'Catch ex As Exception
		'    _log.Warn("failed to read index information for table [" & tableName & "]")
		'End Try
		' catch
		Return res
	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sqlConnString"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="MD_ID_List"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CreateViperResultsCacheDatabase(ByVal paramList As List(Of String), ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal MD_ID_List As String, ByVal handler As SqlConversionHandler)

		Dim TblSchema As List(Of TableSchema)
		Dim resultDataset As New DataSet
		Dim tblNames As New List(Of String)
		Dim tblsCreated As New List(Of String)
		Dim StoredProcName As String
		Dim password As String = Nothing
		Dim params As String()

		' Create the SQLite database and apply the schema
		CreateSQLiteDatabaseOnly(sqlitePath)

		StoredProcName = "PMExportCandidateAMTsForMDIDs"

		'Get list of table names from paramList
		If Not paramList Is Nothing Then
			For Each row In paramList
				params = row.Split(";"c)
				If params.Count > 0 AndAlso params(0).ToString = "1" Then
					tblNames.Add(params(5).ToString)
				End If
			Next

		End If
		tblNames.Add("T_Mass_Tag_Conformers_Observed")

		TblSchema = ReturnTableSchemaFromStoredProc(paramList, "@MDIDs", sqlConnString, tblNames, StoredProcName, MD_ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_Proteins") Then
			CreateMTSCacheIndex("T_Proteins", "Ref_ID", "P_Ref_ID_indx", sqlitePath, handler)
		End If

		CheckCancelled()

		If tblsCreated.Contains("T_Mass_Tags") Then
			CreateMTSCacheIndex("T_Mass_Tags", "Mass_Tag_ID", "MT_Mass_Tag_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "PMT_Quality_Score", "MT_PMT_Quality_Score_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "Cleavage_State_Max", "MT_Cleavage_State_Max_indx", sqlitePath, handler)
		End If

		StoredProcName = "PMExportDatasetJobInfo"
		tblNames.Clear()
		tblNames.Add("T_Analysis_Description_MS")
		TblSchema = ReturnTableSchemaFromStoredProc(Nothing, "@MDIDs", sqlConnString, tblNames, StoredProcName, MD_ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		StoredProcName = "PMExportMatchOverview"
		tblNames.Clear()
		tblNames.Add("T_Match_Making_Description")
		TblSchema = ReturnTableSchemaFromStoredProc(Nothing, "@MDIDs", sqlConnString, tblNames, StoredProcName, MD_ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_Match_Making_Description") Then

			CreateMTSCacheIndex("T_Match_Making_Description", "MD_ID", "MMD_MD_ID_indx", sqlitePath, handler)

		End If

		CheckCancelled()

		StoredProcName = "PMExportFeatures"
		tblNames.Clear()
		tblNames.Add("T_FTICR_UMC_Results")

		'This is a special case where we want to break up the MD_ID list into chunks
		CreateMTBCacheTableFromProcInChunks(Nothing, "@MDIDs", sqlConnString, sqlitePath, MD_ID_List, handler, StoredProcName, tblNames, 45)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_FTICR_UMC_Results") Then

			CreateMTSCacheIndex("T_FTICR_UMC_Results", "UMC_Results_ID", "FUR_UMC_Results_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_FTICR_UMC_Results", "MD_ID", "FUR_MD_ID_indx", sqlitePath, handler)

		End If

		CheckCancelled()

		StoredProcName = "PMExportFeatureMatches"
		tblNames.Clear()
		tblNames.Add("T_FTICR_UMC_ResultDetails")

		'This is a special case where we want to break up the MD_ID list into chunks
		CreateMTBCacheTableFromProcInChunks(Nothing, "@MDIDs", sqlConnString, sqlitePath, MD_ID_List, handler, StoredProcName, tblNames, 40)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_FTICR_UMC_ResultDetails") Then

			CreateMTSCacheIndex("T_FTICR_UMC_ResultDetails", "Mass_Tag_ID", "FURD_Mass_Tag_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_FTICR_UMC_ResultDetails", "UMC_Results_ID", "FURD_UMC_Results_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_FTICR_UMC_ResultDetails", "MD_ID", "FURD_MD_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_FTICR_UMC_ResultDetails", "UMC_Ind", "FURD_UMC_Ind_indx", sqlitePath, handler)

		End If

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sqlConnString"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="MD_ID_List"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CreateAMTTagDbsAllCacheDatabase(ByVal paramList As List(Of String), ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal MD_ID_List As String, ByVal handler As SqlConversionHandler)
		Dim TblSchema As List(Of TableSchema)
		Dim resultDataset As New DataSet
		Dim tblNames As New List(Of String)
		Dim tblsCreated As New List(Of String)
		Dim StoredProcName As String
		Dim password As String = Nothing

		' Create the SQLite database and apply the schema
		CreateSQLiteDatabaseOnly(sqlitePath)

		StoredProcName = "PMExportAMTs"
		tblNames.Add("T_Mass_Tags")
		tblNames.Add("T_Proteins")
		tblNames.Add("T_Mass_Tag_to_Protein_Map")
		tblNames.Add("T_Mass_Tag_Conformers_Observed")
		TblSchema = ReturnTableSchemaFromStoredProc(paramList, Nothing, sqlConnString, tblNames, StoredProcName, MD_ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_Proteins") Then

			CreateMTSCacheIndex("T_Proteins", "Ref_ID", "P_Ref_ID_indx", sqlitePath, handler)

		End If

		CheckCancelled()

		If tblsCreated.Contains("T_Proteins") Then

			CreateMTSCacheIndex("T_Mass_Tags", "Mass_Tag_ID", "MT_Mass_Tag_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "PMT_Quality_Score", "MT_PMT_Quality_Score_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "Cleavage_State_Max", "MT_Cleavage_State_Max_indx", sqlitePath, handler)

		End If

		If tblsCreated.Contains("T_Mass_Tag_to_Protein_Map") Then

			CreateMTSCacheIndex("T_Mass_Tag_to_Protein_Map", "Mass_Tag_ID", "MTPM_Mass_Tag_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tag_to_Protein_Map", "Ref_ID", "MTPM_Ref_ID_indx", sqlitePath, handler)

		End If

		If tblsCreated.Contains("T_Mass_Tag_Conformers_Observed") Then

			CreateMTSCacheIndex("T_Mass_Tag_Conformers_Observed", "Mass_Tag_ID", "MTCO_Mass_Tag_ID_indx", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tag_Conformers_Observed", "Conformer_ID", "MTCO_Conformer_ID_indx", sqlitePath, handler)

		End If

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sqlConnString"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="MD_ID_List"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CreateAMTTagDbsJobsCacheDatabase(ByVal paramList As List(Of String), ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal MD_ID_List As String, ByVal handler As SqlConversionHandler)
		Dim TblSchema As List(Of TableSchema)
		Dim resultDataset As New DataSet
		Dim tblNames As New List(Of String)
		Dim tblsCreated As New List(Of String)
		Dim StoredProcName As String
		Dim password As String = Nothing

		' Create the SQLite database and apply the schema
		CreateSQLiteDatabaseOnly(sqlitePath)

		StoredProcName = "PMExportPeptidesForJobs"
		tblNames.Add("T_Peptides")
		tblNames.Add("T_Mass_Tags")
		tblNames.Add("T_Proteins")
		tblNames.Add("T_Mass_Tag_to_Protein_Map")
		tblNames.Add("T_Analysis_Description")
		TblSchema = ReturnTableSchemaFromStoredProc(paramList, "@JobList", sqlConnString, tblNames, StoredProcName, MD_ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_Peptides") Then

			CreateMTSCacheIndex("T_Peptides", "Mass_Tag_ID", "Idx_T_Peptides_Mass_Tag_ID", sqlitePath, handler)

		End If

		CheckCancelled()

		If tblsCreated.Contains("T_Proteins") Then

			CreateMTSCacheIndex("T_Proteins", "Ref_ID", "Idx_T_Proteins_Ref_ID", sqlitePath, handler)

		End If

		CheckCancelled()

		If tblsCreated.Contains("T_Mass_Tags") Then

			CreateMTSCacheIndex("T_Mass_Tags", "Mass_Tag_ID", "Idx_T_Mass_Tags_Mass_Tag_ID", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "PMT_Quality_Score", "Idx_T_Mass_Tags_PMT_Quality_Score", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "Cleavage_State_Max", "Idx_T_Mass_Tags_Cleavage_State_Max", sqlitePath, handler)

		End If

	End Sub


	'************
	Private Shared Sub CreateIMPROVDbsCacheDatabase(ByVal paramList As List(Of String), ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal ID_List As String, ByVal handler As SqlConversionHandler)
		Dim TblSchema As List(Of TableSchema)
		Dim resultDataset As New DataSet
		Dim tblNames As New List(Of String)
		Dim tblsCreated As New List(Of String)
		Dim StoredProcName As String
		Dim password As String = Nothing
		Dim params As String()
		Dim paramListTmp As New List(Of String)

		' Create the SQLite database and apply the schema
		CreateSQLiteDatabaseOnly(sqlitePath)

		StoredProcName = "GetExperimentsSummary"

		If Not paramList Is Nothing Then
			For Each row In paramList
				params = row.Split(";"c)
				If params.Count > 0 AndAlso params(0).ToString = "1" AndAlso Not String.IsNullOrEmpty(params(5).ToString) Then
					If params(5).ToString.Contains("_Experiment") Then
						tblNames.Add(params(5).ToString)
					End If
				End If
				If params(1).ToString = "@MTDBName" Then
					paramListTmp.Add(row)
				End If
			Next
		End If

		TblSchema = ReturnTableSchemaFromStoredProc(paramListTmp, Nothing, sqlConnString, tblNames, StoredProcName, ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		StoredProcName = "GetMassTags"

		paramListTmp.Clear()
		tblNames.Clear()

		If Not paramList Is Nothing Then
			For Each row In paramList
				params = row.Split(";"c)
				If params.Count > 0 AndAlso params(0).ToString = "1" AndAlso Not String.IsNullOrEmpty(params(5).ToString) Then
					If params(5).ToString.Contains("_Peptides") Then
						tblNames.Add(params(5).ToString)
					End If
				End If
				If params(1).ToString = "@MTDBName" Or params(1).ToString = "@minimumPMTQualityScore" Then
					paramListTmp.Add(row)
				End If
			Next
		End If
		paramListTmp.Add(";" & "@outputColumnNameList" & ";" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add(";" & "@criteriaSql" & ";" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add("False" & ";" & "@returnRowCount" & ";" & "False" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add("DBSearch(MS/MS-LCQ)" & ";" & "@pepIdentMethod" & ";" & "DBSearch(MS/MS-LCQ)" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add(";" & "@Proteins" & ";" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add("-1;" & "@maximumRowCount" & ";-1" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add("False;" & "@includeSupersededData" & ";False" & ";False;" & "sqldbtype.varchar" & ";;")
		paramListTmp.Add("0;" & "@previewSql" & ";0" & ";False;" & "sqldbtype.varchar" & ";;")

		TblSchema = ReturnTableSchemaFromStoredProc(paramListTmp, "@experiments", sqlConnString, tblNames, StoredProcName, ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		For Each tblName In tblsCreated
			If tblName.Contains("_Experiments") Then
				CreateMTSCacheIndex(tblName, "Experiment", "Idx_" & tblName & "_Experiments", sqlitePath, handler)

				CheckCancelled()
			End If

			If tblName.Contains("_Peptides") Then
				CreateMTSCacheIndex(tblName, "Experiment", "Idx_" & tblName & "_Experiments", sqlitePath, handler)
			End If

		Next

		'CheckCancelled()

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sqlConnString"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="ID_List"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CreatePTDbsCacheDatabase(ByVal paramList As List(Of String), ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal ID_List As String, ByVal handler As SqlConversionHandler)
		Dim TblSchema As List(Of TableSchema)
		Dim resultDataset As New DataSet
		Dim tblNames As New List(Of String)
		Dim tblsCreated As New List(Of String)
		Dim StoredProcName As String
		Dim password As String = Nothing
		Dim params As String()

		' Create the SQLite database and apply the schema
		CreateSQLiteDatabaseOnly(sqlitePath)

		StoredProcName = "PTExportPeptidesForJobs"
		'tblNames.Add("T_Peptides")
		'tblNames.Add("T_Mass_Tags")
		'tblNames.Add("T_Proteins")
		'tblNames.Add("T_Mass_Tag_to_Protein_Map")
		'tblNames.Add("T_Analysis_Description")

		If Not paramList Is Nothing Then
			For Each row In paramList
				params = row.Split(";"c)
				If params.Count > 0 AndAlso params(0).ToString = "1" AndAlso Not String.IsNullOrEmpty(params(5).ToString) Then
					tblNames.Add(params(5).ToString)
				End If
			Next
		End If

		TblSchema = ReturnTableSchemaFromStoredProc(paramList, "@JobList", sqlConnString, tblNames, StoredProcName, ID_List, handler)

		CheckCancelled()

		CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)

		CheckCancelled()

		tblsCreated = GetTablesFromDb(sqlitePath)

		If tblsCreated.Contains("T_Peptides") Then
			CreateMTSCacheIndex("T_Peptides", "Mass_Tag_ID", "Idx_T_Peptides_Mass_Tag_ID", sqlitePath, handler)

			CheckCancelled()
		End If

		If tblsCreated.Contains("T_Proteins") Then
			CreateMTSCacheIndex("T_Proteins", "Ref_ID", "Idx_T_Proteins_Ref_ID", sqlitePath, handler)
		End If

		CheckCancelled()

		If tblsCreated.Contains("T_Mass_Tags") Then
			CreateMTSCacheIndex("T_Mass_Tags", "Mass_Tag_ID", "Idx_T_Mass_Tags_Mass_Tag_ID", sqlitePath, handler)

			CheckCancelled()

			CreateMTSCacheIndex("T_Mass_Tags", "Cleavage_State_Max", "Idx_T_Mass_Tags_Cleavage_State_Max", sqlitePath, handler)
		End If

	End Sub

	Private Shared Function GetIndexesFromDb(ByVal sqlitePath As String) As List(Of String)
		Dim indxNames As New List(Of String)
		Dim SQLreader As System.Data.SQLite.SQLiteDataReader

		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Using sqconn As New SQLiteConnection(sqliteConnString)
			sqconn.Open()
			' Go over all tables in the schema and copy their rows
			Dim mSQLcommand As New SQLiteCommand
			mSQLcommand.CommandText = "Select * from sqlite_master where type = 'index'"

			mSQLcommand.CommandType = CommandType.Text
			mSQLcommand.Connection = sqconn
			mSQLcommand.ExecuteNonQuery()
			SQLreader = mSQLcommand.ExecuteReader()
			While SQLreader.Read()
				indxNames.Add(CStr(SQLreader("name")))
			End While

			sqconn.Close()
		End Using

		Return indxNames
	End Function

	Private Shared Function GetTablesFromDb(ByVal sqlitePath As String) As List(Of String)
		Dim tblNames As New List(Of String)
		Dim SQLreader As System.Data.SQLite.SQLiteDataReader

		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Using sqconn As New SQLiteConnection(sqliteConnString)
			sqconn.Open()
			' Go over all tables in the schema and copy their rows
			Dim mSQLcommand As New SQLiteCommand
			mSQLcommand.CommandText = "select tbl_name as ""Table Name"" from sqlite_master where type = ""table"""
			mSQLcommand.CommandType = CommandType.Text
			mSQLcommand.Connection = sqconn
			mSQLcommand.ExecuteNonQuery()
			SQLreader = mSQLcommand.ExecuteReader()
			While SQLreader.Read()
				tblNames.Add(CStr(SQLreader("Table Name")))
			End While

			sqconn.Close()
		End Using

		Return tblNames
	End Function

	Private Shared Function GetWorkflowFromDb(ByVal sqlitePath As String) As String
		Dim workflow As String
		Dim SQLreader As System.Data.SQLite.SQLiteDataReader

		Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
		Using sqconn As New SQLiteConnection(sqliteConnString)
			sqconn.Open()
			' Go over all tables in the schema and copy their rows
			Dim mSQLcommand As New SQLiteCommand
			mSQLcommand.CommandText = "select * from T_Workflow where ID = (select max(ID) from t_workflow)"
			mSQLcommand.CommandType = CommandType.Text
			mSQLcommand.Connection = sqconn
			mSQLcommand.ExecuteNonQuery()
			SQLreader = mSQLcommand.ExecuteReader()
			If SQLreader.HasRows Then
				workflow = CStr(SQLreader("Workflow"))
			Else
				workflow = Nothing
			End If
			sqconn.Close()
		End Using

		Return workflow
	End Function

	Private Shared Function GetWorkflowFromFile(ByVal workflowPath As String) As String
		Dim workflow As String = String.Empty
		Dim line As String = String.Empty
		Dim sr As StreamReader = New StreamReader(workflowPath)

		Do While sr.Peek() >= 0
			line = sr.ReadLine()
			workflow += " " + line
		Loop
		sr.Close()


		Return workflow
	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sprocParam"></param>
	''' <param name="sqlConnString"></param>
	''' <param name="sqlitePath"></param>
	''' <param name="MD_ID_List"></param>
	''' <param name="handler"></param>
	''' <param name="StoredProcName"></param>
	''' <param name="tblNames"></param>
	''' <param name="chunkSize"></param>
	''' <remarks></remarks>
	Private Shared Sub CreateMTBCacheTableFromProcInChunks(ByVal paramList As List(Of String), ByVal sprocParam As String, ByVal sqlConnString As String, ByVal sqlitePath As String, ByVal MD_ID_List As String, ByVal handler As SqlConversionHandler, ByVal StoredProcName As String, ByVal tblNames As List(Of String), ByVal chunkSize As Integer)
		Dim TblSchema As List(Of TableSchema)
		Dim arrayList() As String
		Dim tmpMD_ID_List As String = ""
		Dim counter As Integer = 0
		Dim tblCreated As Boolean = False

		arrayList = MD_ID_List.Split(",")
		For i = 0 To arrayList.Count - 1
			If Not String.IsNullOrEmpty(arrayList(i).ToString) Then
				tmpMD_ID_List += arrayList(i).ToString & ","
			End If
			counter += 1
			If counter Mod chunkSize = 0 Then
				If tblCreated = False Then
					TblSchema = ReturnTableSchemaFromStoredProc(paramList, sprocParam, sqlConnString, tblNames, StoredProcName, tmpMD_ID_List, handler)
					CheckCancelled()
					CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)
					tblCreated = True
				Else
					GetAdditionalRecordsFromStoredProc(sqlConnString, StoredProcName, tmpMD_ID_List, handler)
					' Copy all rows from SQL Server tables to the newly created SQLite database
					CopyTableRowsToSQLiteDB(mDataset, sqlitePath, TblSchema, Nothing, handler)
				End If
				tmpMD_ID_List = ""
			End If
		Next
		If tmpMD_ID_List <> "" Then
			If tblCreated = False Then
				TblSchema = ReturnTableSchemaFromStoredProc(paramList, sprocParam, sqlConnString, tblNames, StoredProcName, tmpMD_ID_List, handler)
				CheckCancelled()
				CreateMTBCacheTableFromProc(sqlitePath, TblSchema, handler)
				tblCreated = True
			Else
				GetAdditionalRecordsFromStoredProc(sqlConnString, StoredProcName, tmpMD_ID_List, handler)
				' Copy all rows from SQL Server tables to the newly created SQLite database
				CopyTableRowsToSQLiteDB(mDataset, sqlitePath, TblSchema, Nothing, handler)
			End If
		End If
	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sqlitePath"></param>
	''' <param name="tmpTblSchema"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub CreateMTBCacheTableFromProc(ByVal sqlitePath As String, ByVal tmpTblSchema As List(Of TableSchema), ByVal handler As SqlConversionHandler)
		Dim password As String = Nothing

		' Create the SQLite database and apply the schema
		AddSchemaToSQLiteDatabase(sqlitePath, tmpTblSchema, password, handler)

		' Copy all rows from SQL Server tables to the newly created SQLite database
		CopyTableRowsToSQLiteDB(mDataset, sqlitePath, tmpTblSchema, password, handler)

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="sprocParam"></param>
	''' <param name="connectionString"></param>
	''' <param name="tblNames"></param>
	''' <param name="mStoredProcName"></param>
	''' <param name="MD_ID_List"></param>
	''' <param name="handler"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Private Shared Function ReturnTableSchemaFromStoredProc(ByVal paramList As List(Of String), ByVal sprocParam As String, ByVal connectionString As String, ByVal tblNames As List(Of String), ByVal mStoredProcName As String, ByVal MD_ID_List As String, ByVal handler As SqlConversionHandler) As List(Of TableSchema)
		Dim res As New List(Of TableSchema)
		Dim tblschema As TableSchema
		Dim tblcolumnslist As List(Of ColumnSchema)
		Dim tblcolumn As ColumnSchema
		Dim connection As SqlConnection = New SqlConnection(connectionString)
		Dim myMessage As String = ""
		Dim params As String()

		UpdateProgress(handler, False, True, 0, "Executing " & mStoredProcName)
		connection.Open()
		'"@MDIDs"
		Try
			Dim command As SqlCommand = New SqlCommand(mStoredProcName, connection)
			command.CommandTimeout = 1800
			If Not sprocParam Is Nothing Then
				command.Parameters.Add(sprocParam, SqlDbType.VarChar)
				command.Parameters.Item(sprocParam).Direction = ParameterDirection.Input
				command.Parameters.Item(sprocParam).Value = MD_ID_List
			End If
			Dim i As Integer
			If Not paramList Is Nothing Then
				For Each row In paramList
					params = row.Split(";"c)
					If params.Count > 0 Then
						command.Parameters.Add(params(1).ToString, GetDbType(params(4).ToString))
						command.Parameters.Item(params(1).ToString).Direction = ParameterDirection.Input
						command.Parameters.Item(params(1).ToString).Value = params(0).ToString
					End If
				Next

			End If
			command.CommandType = CommandType.StoredProcedure

			Dim adapter As SqlDataAdapter = New SqlDataAdapter(command)
			Dim table As DataTable = New DataTable
			mDataset = New DataSet
			mDataset.EnforceConstraints = False
			adapter.Fill(mDataset)
			Dim columns As DataColumnCollection
			Dim column As DataColumn
			'Dim i As Integer
			Dim j As Integer
			For i = 0 To mDataset.Tables.Count - 1
				tblschema = New TableSchema
				tblcolumnslist = New List(Of ColumnSchema)
				tblschema.TableName = tblNames(i)
				table = mDataset.Tables(i)
				columns = table.Columns
				For j = 0 To mDataset.Tables(i).Columns().Count - 1
					column = mDataset.Tables(i).Columns.Item(j)
					tblcolumn = New ColumnSchema
					tblcolumn.ColumnName = column.ColumnName.ToString()
					tblcolumn.DefaultValue = column.DefaultValue.ToString()
					tblcolumn.ColumnType = GetFieldType(column.DataType.ToString())
					If String.IsNullOrEmpty(tblcolumn.ColumnType) Then
						MsgBox("column is empty for : " & column.DataType.ToString())
					End If
					tblcolumnslist.Add(tblcolumn)
				Next
				tblschema.Columns = tblcolumnslist

				res.Add(tblschema)
			Next

		Catch ex As Exception
			Console.WriteLine(ex.Message)
			Throw
		Finally
			connection.Close()
		End Try

		Return res

	End Function

	Private Shared Function GetDbType(ByVal dataType As String) As SqlDbType

		Select Case dataType.ToLower
			Case "sqldbtype.varchar"
				Return SqlDbType.VarChar
			Case "sqldbtype.bit"
				Return SqlDbType.Bit
			Case "sqldbtype.tinyint"
				Return SqlDbType.TinyInt
			Case "sqldbtype.smallint"
				Return SqlDbType.SmallInt
			Case "sqldbtype.int"
				Return SqlDbType.Int
			Case "sqldbtype.bigint"
				Return SqlDbType.BigInt
			Case "sqldbtype.float"
				Return SqlDbType.Float
			Case "sqldbtype.decimal"
				Return SqlDbType.[Decimal]
			Case "sqldbtype.datetime"
				Return SqlDbType.DateTime
			Case "sqldbtype.varbinary"
				Return SqlDbType.VarBinary
			Case "sqldbtype.real"
				Return SqlDbType.Real
		End Select

		Throw New ApplicationException("SQLite Validation failed for stored procedure data type [" & dataType & "]")

	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="connectionString"></param>
	''' <param name="mStoredProcName"></param>
	''' <param name="MD_ID_List"></param>
	''' <param name="handler"></param>
	''' <remarks></remarks>
	Private Shared Sub GetAdditionalRecordsFromStoredProc(ByVal connectionString As String, ByVal mStoredProcName As String, ByVal MD_ID_List As String, ByVal handler As SqlConversionHandler)
		Dim res As New List(Of TableSchema)
		Dim connection As SqlConnection = New SqlConnection(connectionString)
		Dim myMessage As String = ""

		UpdateProgress(handler, False, True, 0, "Executing " & mStoredProcName)
		connection.Open()

		Try
			Dim command As SqlCommand = New SqlCommand(mStoredProcName, connection)
			command.CommandTimeout = 300

			command.Parameters.Add("@MDIDs", SqlDbType.VarChar)
			command.Parameters.Item("@MDIDs").Direction = ParameterDirection.Input
			command.Parameters.Item("@MDIDs").Value = MD_ID_List
			command.CommandType = CommandType.StoredProcedure

			Dim adapter As SqlDataAdapter = New SqlDataAdapter(command)
			mDataset = New DataSet
			mDataset.EnforceConstraints = False
			adapter.Fill(mDataset)

		Catch ex As Exception
			Console.WriteLine(ex.Message)
			Throw
		Finally
			connection.Close()
		End Try

	End Sub

	''' <summary>
	''' 
	''' </summary>
	''' <param name="dataType"></param>
	''' <returns></returns>
	''' <remarks></remarks>
	Private Shared Function GetFieldType(ByVal dataType As String) As String
		If dataType = "System.Int32" Then
			Return "int"
		End If
		If dataType = "System.String" Then
			Return "text"
		End If
		If dataType = "System.Int16" Then
			Return "smallint"
		End If
		If dataType = "System.Double" Then
			Return "double"
			'Return "numeric"
		End If
		If dataType = "System.Single" Then
			Return "real"
		End If
		If dataType = "System.Decimal" Then
			Return "float"
		End If
		If dataType = "System.Byte" Then
			Return "smallint"
		End If
		If dataType = "System.DateTime" Then
			dataType = "datetime"
		End If

		Return dataType

	End Function

	''' <summary>
	''' 
	''' </summary>
	''' <param name="dataType"></param>
	''' <param name="tableName"></param>
	''' <param name="fieldName"></param>
	''' <remarks></remarks>
	Private Shared Sub ValidateSQLiteDataType(ByVal dataType As String, ByVal tableName As String, ByVal fieldName As String)
		dataType = dataType.ToLower
		If dataType = "datetime" OrElse dataType = "numeric" OrElse dataType = "float" OrElse dataType = "real" OrElse dataType = "integer" OrElse dataType = "text" OrElse dataType = "char" OrElse dataType = "smallint" OrElse dataType = "double" OrElse dataType = "varchar" Then
			Exit Sub
		End If
		If dataType = "" Then
			Exit Sub
		End If
		Throw New ApplicationException("SQLite Validation failed for table/field " & tableName & "/" & fieldName & "data type [" & dataType & "]")
	End Sub

	''' <summary>
	''' Small validation method to make sure we don't miss anything without getting
	''' an exception.
	''' </summary>
	''' <param name="dataType">The datatype to validate.</param>
	Private Shared Sub ValidateDataType(ByVal dataType As String)
		If dataType = "int" OrElse dataType = "smallint" OrElse dataType = "bit" OrElse dataType = "float" OrElse dataType = "real" OrElse dataType = "nvarchar" OrElse dataType = "varchar" OrElse dataType = "timestamp" OrElse dataType = "varbinary" OrElse dataType = "image" OrElse dataType = "text" OrElse dataType = "ntext" OrElse dataType = "bigint" OrElse dataType = "char" OrElse dataType = "numeric" OrElse dataType = "binary" OrElse dataType = "smalldatetime" OrElse dataType = "smallmoney" OrElse dataType = "money" OrElse dataType = "tinyint" OrElse dataType = "uniqueidentifier" OrElse dataType = "xml" OrElse dataType = "sql_variant" OrElse dataType = "decimal" OrElse dataType = "nchar" OrElse dataType = "datetime" Then
			Exit Sub
		End If
		Throw New ApplicationException("Validation failed for data type [" & dataType & "]")
	End Sub

	''' <summary>
	''' Does some necessary adjustments to a value string that appears in a column DEFAULT
	''' clause.
	''' </summary>
	''' <param name="colDefault">The original default value string (as read from SQL Server).</param>
	''' <returns>Adjusted DEFAULT value string (for SQLite)</returns>
	Private Shared Function FixDefaultValueString(ByVal colDefault As String) As String
		Dim replaced As Boolean = False
		Dim res As String = colDefault.Trim()

		' Find first/last indexes in which to search
		Dim first As Integer = -1
		Dim last As Integer = -1
		For i As Integer = 0 To res.Length - 1
			If res(i) = "'"c AndAlso first = -1 Then
				first = i
			End If
			If res(i) = "'"c AndAlso first <> -1 AndAlso i > last Then
				last = i
			End If
		Next
		' for
		If first <> -1 AndAlso last > first Then
			Return res.Substring(first, last - first + 1)
		End If

		Dim sb As New StringBuilder()
		For i As Integer = 0 To res.Length - 1
			If res(i) <> "("c AndAlso res(i) <> ")"c Then
				sb.Append(res(i))
				replaced = True
			End If
		Next
		If replaced Then
			Return "(" & sb.ToString() & ")"
		Else
			Return sb.ToString()
		End If
	End Function

	''' <summary>
	''' Builds an index schema object from the specified components (Read from SQL Server).
	''' </summary>
	''' <param name="indexName">The name of the index</param>
	''' <param name="desc">The description of the index</param>
	''' <param name="keys">Key columns that are part of the index.</param>
	''' <returns>An index schema object that represents our knowledge of the index</returns>
	Private Shared Function BuildIndexSchema(ByVal indexName As String, ByVal desc As String, ByVal keys As String) As IndexSchema
		Dim res As New IndexSchema()
		res.IndexName = indexName

		' Determine if this is a unique index or not.
		Dim descParts As String() = desc.Split(","c)
		For Each p As String In descParts
			If p.Trim().Contains("unique") Then
				res.IsUnique = True
				Exit For
			End If
		Next
		' foreach
		' Get all key names and check if they are ASCENDING or DESCENDING
		res.Columns = New List(Of IndexColumn)()
		Dim keysParts As String() = keys.Split(","c)
		For Each p As String In keysParts
			Dim m As Match = _keyRx.Match(p)
			If Not m.Success Then
				Throw New ApplicationException(("Illegal key name [" & p & "] in index [") + indexName & "]")
			End If

			Dim key As String = m.Groups(1).Value
			Dim ic As New IndexColumn()
			ic.ColumnName = key
			If m.Groups(2).Success Then
				ic.IsAscending = False
			Else
				ic.IsAscending = True
			End If

			res.Columns.Add(ic)
		Next
		' foreach
		Return res
	End Function

	''' <summary>
	''' More adjustments for the DEFAULT value clause.
	''' </summary>
	''' <param name="val">The value to adjust</param>
	''' <returns>Adjusted DEFAULT value string</returns>
	Private Shared Function AdjustDefaultValue(ByVal val As String) As String
		If val Is Nothing OrElse val = String.Empty Then
			Return val
		End If

		Dim m As Match = _defaultValueRx.Match(val)
		If m.Success Then
			Return m.Groups(1).Value
		End If
		Return val
	End Function

	''' <summary>
	''' Creates SQLite connection string from the specified DB file path.
	''' </summary>
	''' <param name="sqlitePath">The path to the SQLite database file.</param>
	''' <returns>SQLite connection string</returns>
	Private Shared Function CreateSQLiteConnectionString(ByVal sqlitePath As String, ByVal password As String) As String
		Dim builder As New SQLiteConnectionStringBuilder()
		builder.DataSource = sqlitePath
		If password IsNot Nothing Then
			builder.Password = password
		End If
		'builder.PageSize = 4096
		'builder.UseUTF16Encoding = True
		Dim connstring As String = builder.ConnectionString

		Return connstring
	End Function

	Protected Shared Sub UpdateProgress(ByRef handler As SqlConversionHandler, ByVal done As Boolean, ByVal success As Boolean, ByVal percent As Integer, ByVal msg As String)

		If Not handler Is Nothing Then
			' Call the delegate function
			handler(done, success, percent, msg)
		End If

		' Update the progress event
		RaiseEvent ProgressChanged(msg, percent)

	End Sub

#End Region

#Region "Private Variables"
    Private Shared _isActive As Boolean = False
    Private Shared _cancelled As Boolean = False
    Private Shared _keyRx As New Regex("([a-zA-Z_0-9]+)(\(\-\))?")
    Private Shared _defaultValueRx As New Regex("\(N(\'.*\')\)")
#End Region
End Class

''' <summary>
''' This handler is called whenever a progress is made in the conversion process.
''' </summary>
''' <param name="done">TRUE indicates that the entire conversion process is finished.</param>
''' <param name="success">TRUE indicates that the current step finished successfully.</param>
''' <param name="percent">Progress percent (0-100)</param>
''' <param name="msg">A message that accompanies the progress.</param>
Public Delegate Sub SqlConversionHandler(ByVal done As Boolean, ByVal success As Boolean, ByVal percent As Integer, ByVal msg As String)

''' <summary>
''' This handler allows the user to change which tables get converted from SQL Server
''' to SQLite.
''' </summary>
''' <param name="schema">The original SQL Server DB schema</param>
''' <returns>The same schema minus any table we don't want to convert.</returns>
Public Delegate Function SqlTableSelectionHandler(ByVal schema As List(Of TableSchema)) As List(Of TableSchema)

''' <summary>
''' This handler is called whenever a progress is made in the conversion process.
''' </summary>
''' <param name="done">TRUE indicates that the entire conversion process is finished.</param>
''' <param name="success">TRUE indicates that the current step finished successfully.</param>
''' <param name="percent">Progress percent (0-100)</param>
''' <param name="msg">A message that accompanies the progress.</param>
Public Delegate Sub SqlQueryHandler(ByVal done As Boolean, ByVal success As Boolean, ByVal percent As Integer, ByVal msg As String)
