﻿Imports System.Text
Imports System.Data.SQLite
Imports System.Threading
Imports System.Text.RegularExpressions
Imports System.IO
Imports System.Data.OleDb
Imports ADOX
Imports PRISM.Logging
Imports TableFunctions

''' <summary>
''' This class is responsible to take a single SQL Server database
''' and convert it to an SQLite database file.
''' </summary>
''' <remarks>The class knows how to convert table and index structures only.</remarks>
Public Class SqliteToAccess

    Public Shared mAccessPath As String
    Public Shared mSqliteSourcePath As String
    Public Shared mSqlitePath As String
    Public Shared mTextFileDirectory As String
    Public Shared mDelimiter As String
    Public Shared mPassword As String
    Public Shared mHandler As SqlConversionHandler
    Public Shared mSelectionHandler As SqlTableSelectionHandler
    Public Shared mCreateTriggers As Boolean
    Public Shared mTxtFilePath As String
    Public Shared mColList As String()
    Public Shared mTextFileParams As String()
    Public Shared mFunctionList As List(Of SingleReturnFunction)
    Public Shared mTableName As String
    Public Shared mGroupByField As String
    Public Shared mFldDefinitions As Dictionary(Of String, String)
    Public Shared mSql As String
    Public Shared mNewTableName As String

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
    ''' This method takes as input the connection string to an SQLite database
    ''' and creates a corresponding Access database file with a schema derived from
    ''' the SQLite database.
    ''' </summary>
    ''' <param name="sqlitePath">The path to the SQLite database file that needs to get created.</param>
    ''' <param name="textFileDirectory"></param>
    ''' <param name="delimiter"></param>
    ''' <param name="password">The password to use or NULL if no password should be used to encrypt the DB</param>
    ''' <param name="handler">A handler delegate for progress notifications.</param>
    ''' <param name="selectionHandler">The selection handler that allows the user to select which
    ''' tables to convert</param>
    ''' <remarks>The method executes asynchronously in the background and the thus control is quickly returned to the caller</remarks>
    Public Shared Sub ConvertSQLiteToTextFile(sqlitePath As String, textFileDirectory As String, delimiter As String, password As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler)
        ' Clear cancelled flag
        _cancelled = False

        mSqlitePath = sqlitePath
        mTextFileDirectory = textFileDirectory
        mDelimiter = delimiter
        mPassword = password
        mHandler = handler
        mSelectionHandler = selectionHandler

        ThreadPool.QueueUserWorkItem(AddressOf FunctionCSTTF)

    End Sub

    ''' <summary>
    ''' Convert SQLite Database to Text File
    ''' </summary>
    ''' <param name="state"></param>
    ''' <remarks></remarks>
    Shared Sub FunctionCSTTF(state As Object)
        Dim result As Boolean
        Try
            _isActive = True
            result = ConvertSQLiteDatabaseToTextFile(mSqlitePath, mTextFileDirectory, mHandler, mSelectionHandler) ', mCreateTriggers)
            _isActive = False
            If result Then
                mHandler(True, True, 100, "Finished exporting database: " & mSqlitePath)
            Else
                mHandler(True, False, 0, "Export Cancelled by user")
            End If
        Catch ex As Exception
            LogUtilities.ShowError("Failed to convert SQL Server database to SQLite database", ex)
            _isActive = False
            mHandler(True, False, 100, ex.Message)
            ' catch
        End Try

    End Sub

    ''' <summary>
    ''' This method takes as input the connection string to an SQLite database
    ''' and creates a corresponding Access database file with a schema derived from
    ''' the SQLite database.
    ''' </summary>
    ''' <param name="sqlitePath">The path to the SQLite database file that needs to get created.</param>
    ''' <param name="password">The password to use or NULL if no password should be used to encrypt the DB</param>
    ''' <param name="handler">A handler delegate for progress notifications.</param>
    ''' <param name="selectionHandler">The selection handler that allows the user to select which
    ''' tables to convert</param>
    ''' <remarks>The method executes asynchronously in the background and the thus control is quickly returned to the caller</remarks>
    Public Shared Sub ConvertSQLiteToAccessDatabase(sqlitePath As String, accessDbPath As String, password As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler)
        ' Clear cancelled flag
        _cancelled = False

        mSqlitePath = sqlitePath
        mAccessPath = accessDbPath
        mPassword = password
        mHandler = handler
        mSelectionHandler = selectionHandler

        ThreadPool.QueueUserWorkItem(AddressOf FunctionCSTAD)

    End Sub

    ''' <summary>
    ''' Convert SQLite Database to Access Database
    ''' </summary>
    ''' <param name="state"></param>
    ''' <remarks></remarks>
    Shared Sub FunctionCSTAD(state As Object)
        Dim result As Boolean
        Try
            _isActive = True
            result = ConvertSQLiteDatabaseToAccessDatabase(mSqlitePath, mAccessPath, mHandler, mSelectionHandler) ', mCreateTriggers)
            _isActive = False
            If result Then
                mHandler(True, True, 100, "Finished exporting database: " & mSqlitePath)
            Else
                mHandler(True, False, 0, "Export Cancelled by user")
            End If
        Catch ex As Exception
            LogUtilities.ShowError("Failed to convert SQL Server database to SQLite database", ex)
            _isActive = False
            mHandler(True, False, 100, ex.Message)
            ' catch
        End Try

    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="AccessPath"></param>
    ''' <param name="sqlitePath"></param>
    ''' <param name="password"></param>
    ''' <param name="handler"></param>
    ''' <param name="selectionHandler"></param>
    ''' <remarks></remarks>
    Public Shared Sub ConvertAccessToSQLiteDatabase(AccessPath As String, sqlitePath As String, password As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler)
        ' Clear cancelled flag
        _cancelled = False

        mAccessPath = AccessPath
        mSqlitePath = sqlitePath
        mPassword = password
        mHandler = handler
        mSelectionHandler = selectionHandler

        ThreadPool.QueueUserWorkItem(AddressOf FunctionCATSD)

    End Sub

    ''' <summary>
    ''' Convert Access Database to SQLite Database
    ''' </summary>
    ''' <param name="state"></param>
    ''' <remarks></remarks>
    Shared Sub FunctionCATSD(state As Object)
        Dim result As Boolean
        Try
            _isActive = True
            result = ConvertAccessDatabaseToSQLiteDatabase(mAccessPath, mSqlitePath, mHandler, mSelectionHandler)
            _isActive = False
            If result Then
                mHandler(True, True, 100, "Finished importing into database: " & mSqlitePath)
            Else
                mHandler(True, False, 0, "Import Cancelled by user")
            End If
        Catch ex As Exception
            LogUtilities.ShowError("Failed to convert SQL Server database to SQLite database", ex)
            _isActive = False
            mHandler(True, False, 100, ex.Message)
            ' catch
        End Try

    End Sub

    Public Shared Sub ConvertSQLiteToSQLiteDatabase(sqliteSourcePath As String, sqlitePath As String, password As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler)
        ' Clear cancelled flag
        _cancelled = False

        mSqliteSourcePath = sqliteSourcePath
        mSqlitePath = sqlitePath
        mPassword = password
        mHandler = handler
        mSelectionHandler = selectionHandler

        ThreadPool.QueueUserWorkItem(AddressOf FunctionCSTSD)

    End Sub

    ''' <summary>
    ''' Convert Sqlite Database to SQLite Database
    ''' </summary>
    ''' <param name="state"></param>
    Shared Sub FunctionCSTSD(state As Object)
        Dim result As Boolean
        Try
            _isActive = True
            result = ConvertSqliteDatabaseToSQLiteDatabase(mSqliteSourcePath, mSqlitePath, mHandler, mSelectionHandler)
            _isActive = False
            If result Then
                mHandler(True, True, 100, "Finished importing into database: " & mSqlitePath)
            Else
                mHandler(True, False, 0, "Import Cancelled by user")
            End If
        Catch ex As Exception
            LogUtilities.ShowError("Failed to convert SQL Server database to SQLite database", ex)
            _isActive = False
            mHandler(True, False, 100, ex.Message)
            ' catch
        End Try

    End Sub


    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="textFileParams"></param>
    ''' <param name="textFilePath"></param>
    ''' <param name="colList"></param>
    ''' <param name="sqlitePath"></param>
    ''' <param name="password"></param>
    ''' <param name="handler"></param>
    ''' <param name="selectionHandler"></param>
    ''' <remarks></remarks>
    Public Shared Sub ConvertTextFileToSQLiteDatabase(textFileParams As String(), textFilePath As String, colList As String(), sqlitePath As String, password As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler)
        ' Clear cancelled flag
        _cancelled = False

        mTxtFilePath = textFilePath
        mTextFileParams = textFileParams
        mColList = colList
        mSqlitePath = sqlitePath
        mPassword = password
        mHandler = handler
        mSelectionHandler = selectionHandler

        ThreadPool.QueueUserWorkItem(AddressOf FunctionCTFTSD)

    End Sub

    ''' <summary>
    ''' Convert Text File Database to SQLite Database
    ''' </summary>
    ''' <param name="state"></param>
    ''' <remarks></remarks>
    Shared Sub FunctionCTFTSD(state As Object)
        Dim result As Boolean
        Try
            _isActive = True
            result = ConvertTextFileDatabaseToSQLiteDatabase(mTextFileParams, mTxtFilePath, mColList, mSqlitePath, mHandler)
            _isActive = False
            If result Then
                mHandler(True, True, 100, "Finished importing into database: " & mSqlitePath)
            Else
                mHandler(True, False, 0, "Import Cancelled by user")
            End If
        Catch ex As Exception
            LogUtilities.ShowError("Failed to convert SQL Server database to SQLite database", ex)
            _isActive = False
            mHandler(True, False, 100, ex.Message)
            ' catch
        End Try

    End Sub


    Public Shared Sub CreateDataTableFromFunctionList(fldDefinitions As Dictionary(Of String, String), functionList As List(Of SingleReturnFunction), sqlitePath As String, tableName As String, newTableName As String, handler As SqlConversionHandler)
        ' Clear cancelled flag
        _cancelled = False

        mSqlitePath = sqlitePath
        mTableName = tableName
        mNewTableName = newTableName
        mHandler = handler
        mFunctionList = functionList
        mFldDefinitions = fldDefinitions

        ThreadPool.QueueUserWorkItem(AddressOf FunctionsCDTFFL)

    End Sub

    ''' <summary>
    ''' Run Create Data Table From Functio List
    ''' </summary>
    ''' <param name="state"></param>
    ''' <remarks></remarks>
    Shared Sub FunctionsCDTFFL(state As Object)
        Try
            _isActive = True
            RunCreateDataTableFromFunctionList(mFldDefinitions, mSqlitePath, mTableName, mNewTableName, mFunctionList, mHandler)
            _isActive = False
            mHandler(True, True, 100, "Finished creating function table in: " & mSqlitePath)
        Catch ex As Exception
            LogUtilities.ShowError("Failed to create function table", ex)
            _isActive = False
            mHandler(True, False, 100, ex.Message)
            ' catch
        End Try

    End Sub

#End Region

#Region "Private Methods"

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="sqlitePath"></param>
    ''' <param name="textFileDirectory"></param>
    ''' <param name="handler"></param>
    ''' <param name="selectionHandler"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function ConvertSQLiteDatabaseToTextFile(sqlitePath As String, textFileDirectory As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler) As Boolean ', ByVal createTriggers As Boolean)
        ' Read the schema of the SQL Server database into a memory structure
        Dim sqlSchema As List(Of TableSchema) = ReadSqliteSchema(sqlitePath, handler, selectionHandler)

        If sqlSchema IsNot Nothing Then

            ' Copy all rows from SQLite tables to the newly text files
            CopySQLiteDBRowsToTextFile(sqlitePath, textFileDirectory, mDelimiter, sqlSchema, handler)
            Return True
        Else
            Return False
        End If

    End Function

    ''' <summary>
    ''' Do the entire process of first reading the SQL Server schema, creating a corresponding
    ''' SQLite schema, and copying all rows from the SQL Server database to the SQLite database.
    ''' </summary>
    ''' <param name="sqlitePath">The path to the generated SQLite database file</param>
    ''' <param name="handler">A handler to handle progress notifications.</param>
    ''' <param name="selectionHandler">The selection handler which allows the user to select which tables to
    ''' convert.</param>
    Private Shared Function ConvertSQLiteDatabaseToAccessDatabase(sqlitePath As String, accessDbPath As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler) As Boolean ', ByVal createTriggers As Boolean)

        Dim accessDbConn = BuildAccessDbPath(accessDbPath)
        ' Read the schema of the SQL Server database into a memory structure
        Dim sqlSchema As List(Of TableSchema) = ReadSqliteSchema(sqlitePath, handler, selectionHandler)

        If sqlSchema IsNot Nothing Then
            ' Create the SQLite database and apply the schema
            CreateAccessDatabase(accessDbConn, sqlSchema, handler)

            ' Copy all rows from SQL Server tables to the newly created SQLite database
            CopySQLiteDBRowsToAccessDB(sqlitePath, accessDbConn, sqlSchema, handler)
            Return True
        Else
            Return False
        End If

    End Function

    Private Shared Sub RunCreateDataTableFromFunctionList(fldDefinitions As Dictionary(Of String, String), sqlitePath As String, tname As String, newTableName As String, functionList As List(Of SingleReturnFunction), handler As SqlConversionHandler)
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Dim lsTs As List(Of TableSchema)

        Using conn As New SQLiteConnection(sqliteConnString, True)
            conn.Open()
            lsTs = CreateSqliteFunctionTableSchema(fldDefinitions, newTableName)
            conn.Close()
        End Using

        If lsTs IsNot Nothing Then
            ' Create the SQLite database and apply the schema
            CreateSQLiteTables(sqlitePath, lsTs, handler)

            ' Copy all rows from SQL Server tables to the newly created SQLite database
            CopySQLiteDBRowsToSQliteDB(fldDefinitions, tname, functionList, sqlitePath, lsTs, handler)
        End If
    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="AccessPath"></param>
    ''' <param name="sqlitePath"></param>
    ''' <param name="handler"></param>
    ''' <param name="selectionHandler"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function ConvertAccessDatabaseToSQLiteDatabase(AccessPath As String, sqlitePath As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler) As Boolean
        Dim accessDbConn = BuildAccessDbConnString(AccessPath)
        ' Read the schema of the SQL Server database into a memory structure
        Dim sqlSchema As List(Of TableSchema) = ReadAccessSchema(accessDbConn, handler, selectionHandler)

        If sqlSchema IsNot Nothing Then
            ' Create the SQLite database and apply the schema
            CreateSQLiteTables(sqlitePath, sqlSchema, handler)

            ' Copy all rows from SQL Server tables to the newly created SQLite database
            CopyAccessDBRowsToSQLiteDB(sqlitePath, accessDbConn, sqlSchema, handler)
            Return True
        Else
            Return False
        End If

    End Function
    '
    Private Shared Function ConvertSqliteDatabaseToSQLiteDatabase(SqliteSourcePath As String, sqlitePath As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler) As Boolean
        'Dim accessDbConn = BuildAccessDbConnString(AccessPath)
        ' Read the schema of the SQL Server database into a memory structure
        'Dim sqlSchema As List(Of TableSchema) = ReadAccessSchema(accessDbConn, handler, selectionHandler)
        Dim sqlSchema As List(Of TableSchema) = ReadSqliteSchema(SqliteSourcePath, handler, selectionHandler)

        If sqlSchema IsNot Nothing Then
            ' Create the SQLite database and apply the schema
            CreateSQLiteTables(sqlitePath, sqlSchema, handler)

            ' Copy all rows from SQL Server tables to the newly created SQLite database
            CopySqliteDBToSQLiteDB(sqlitePath, SqliteSourcePath, sqlSchema, handler)
            Return True
        Else
            Return False
        End If

    End Function

    Private Shared Function ConvertTextFileDatabaseToSQLiteDatabase(mTextParams As IList(Of String), txtFilePath As String, colList As IList(Of String), sqlitePath As String, handler As SqlConversionHandler) As Boolean
        ' Read the schema of the Text File into a memory structure
        Dim sqlSchema As List(Of TableSchema) = CreateTextFileTableSchema(colList, mTextParams(2))

        If sqlSchema IsNot Nothing Then
            ' Create the SQLite database and apply the schema
            If mTextParams(3) = "False" Then
                CreateSQLiteTables(sqlitePath, sqlSchema, handler)
            End If

            ' Copy all rows from text file to the newly created SQLite database
            CopyTextFileRowsToSQLiteDB(mTextParams, sqlitePath, txtFilePath, sqlSchema, handler)
            Return True
        Else
            Return False
        End If

    End Function


    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="connString"></param>
    ''' <param name="handler"></param>
    ''' <param name="selectionHandler"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function ReadAccessSchema(connString As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler) As List(Of TableSchema)
        ' First step is to read the names of all tables in the database
        Dim tables As New List(Of TableSchema)()
        Dim int As Integer
        Using conn As New OleDbConnection(connString)
            conn.Open()

            Dim SchemaTable As DataTable
            Dim tableNames As New List(Of String)()
            ' This command will read the names of all tables in the database
            SchemaTable = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, New Object() {Nothing, Nothing, Nothing, Nothing})
            For int = 0 To SchemaTable.Rows.Count - 1
                If SchemaTable.Rows(int)!TABLE_TYPE.ToString = "TABLE" Or SchemaTable.Rows(int)!TABLE_TYPE.ToString = "PASS-THROUGH" Or SchemaTable.Rows(int)!TABLE_TYPE.ToString = "VIEW" Then
                    tableNames.Add(SchemaTable.Rows(int)!TABLE_NAME.ToString())
                End If
            Next

            ' using
            ' Next step is to use OleDB APIs to query the schema of each table.
            Dim count = 0
            For Each tableName As String In tableNames
                Dim ts As TableSchema = CreateAccessTableSchema(conn, tableName)
                tables.Add(ts)
                count += 1
                CheckCancelled()
                handler(False, True, CInt((count * 100.0R / tableNames.Count)), "Parsed table " & tableName)

                LogUtilities.ShowDebug("parsed table schema for [" & tableName & "]")
                ' foreach
            Next
            conn.Close()
        End Using
        ' using
        LogUtilities.ShowDebug("finished parsing all tables in SQL Server schema")

        ' Allow the user a chance to select which tables to convert
        If selectionHandler IsNot Nothing Then
            Dim updated As List(Of TableSchema) = selectionHandler(tables)
            If updated IsNot Nothing Then
                Return updated
            Else
                Return Nothing
            End If
        End If

        Return tables

    End Function

    Private Shared Function CreateSqliteFunctionTableSchema(colList As Dictionary(Of String, String), tableName As String) As List(Of TableSchema)
        Dim tables As New List(Of TableSchema)()

        Dim res As New TableSchema()
        res.TableName = tableName
        Dim fldFldType As String()
        Dim val As String
        res.Columns = New List(Of ColumnSchema)()

        For Each item In colList
            val = item.Value
            If val = "FIELD" Then
                fldFldType = item.Key.Split("|"c)
                Dim col As New ColumnSchema()
                col.ColumnName = fldFldType(0)
                col.ColumnType = fldFldType(1)
                col.IsNullable = True
                col.IsIdentity = False
                col.DefaultValue = String.Empty
                res.Columns.Add(col)
            End If
        Next item
        tables.Add(res)

        If Not mFunctionList Is Nothing AndAlso mFunctionList.Count > 0 Then
            For i = 0 To mFunctionList.Count - 1
                Dim fldName As String = mFunctionList(i).NewFieldName
                Dim datatype As Type = mFunctionList(i).ReturnDataType

                Dim col As New ColumnSchema()
                col.ColumnName = fldName
                col.ColumnType = GetStringColumnType(datatype.ToString)
                col.IsNullable = True
                col.IsIdentity = False
                col.DefaultValue = String.Empty
                res.Columns.Add(col)
            Next
        End If

        Return tables

    End Function

    Private Shared Function CreateTextFileTableSchema(colList As IList(Of String), tableName As String) As List(Of TableSchema)
        Dim tables As New List(Of TableSchema)()

        Dim res As New TableSchema()
        res.TableName = tableName
        Dim fldFldType As String()
        res.Columns = New List(Of ColumnSchema)()

        For i = 0 To colList.Count - 1
            fldFldType = colList(i).Split(";"c)
            Dim col As New ColumnSchema()
            col.ColumnName = fldFldType(0)
            col.ColumnType = fldFldType(1)
            col.IsNullable = True
            col.IsIdentity = False
            col.DefaultValue = String.Empty
            res.Columns.Add(col)
        Next
        tables.Add(res)
        Return tables

    End Function

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="conn"></param>
    ''' <param name="tableName"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function CreateAccessTableSchema(conn As OleDbConnection, tableName As String) As TableSchema
        Dim res As New TableSchema()
        res.TableName = tableName
        res.Columns = New List(Of ColumnSchema)()

        Dim cmd = New OleDbCommand(tableName, conn)
        cmd.CommandType = CommandType.TableDirect

        ' Retrieve schema only
        Dim reader As OleDbDataReader = cmd.ExecuteReader(CommandBehavior.SchemaOnly)

        ' Get references to schema information
        Dim SchemaTable As DataTable = reader.GetSchemaTable()

        ' Close and release the connection before processing results
        reader.Close()

        Dim row As DataRow
        For Each row In SchemaTable.Rows
            Dim colName As String = Convert.ToString(row("ColumnName"))
            Dim dataType As String = Convert.ToString(row("DataType"))
            Dim isNullable = True
            ValidateAccessDataType(dataType, tableName, colName)
            If dataType.ToLower = "" Then
                dataType = "text"
            End If

            ' Note that not all data type names need to be converted because
            ' SQLite establishes type affinity by searching certain strings
            ' in the type name. For example - everything containing the string
            ' 'int' in its type name will be assigned an INTEGER affinity
            dataType = GetStringColumnType(dataType)

            Dim col As New ColumnSchema()
            col.ColumnName = colName
            col.ColumnType = dataType
            col.IsNullable = isNullable
            col.IsIdentity = False
            col.DefaultValue = String.Empty  'AdjustDefaultValue(colDefault)
            res.Columns.Add(col)

        Next

        Return res

    End Function

    Private Shared Function GetStringColumnType(dataType As String) As String

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

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="dataType"></param>
    ''' <param name="tableName"></param>
    ''' <param name="fieldName"></param>
    ''' <remarks></remarks>
    Private Shared Sub ValidateAccessDataType(dataType As String, tableName As String, fieldName As String)
        dataType = dataType.ToLower
        If dataType = "system.int32" OrElse dataType = "system.double" OrElse dataType = "system.string" OrElse dataType = "system.datetime" OrElse dataType = "system.single" OrElse dataType = "system.decimal" OrElse dataType = "system.byte" OrElse dataType = "system.int16" Then
            Exit Sub
        End If
        If dataType = "" Then
            Exit Sub
        End If
        Throw New ApplicationException("Access Validation failed for table/field " & tableName & "/" & fieldName & "data type [" & dataType & "]")
    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="sqlitePath"></param>
    ''' <param name="schema"></param>
    ''' <param name="handler"></param>
    ''' <remarks></remarks>
    Private Shared Sub CreateSQLiteTables(sqlitePath As String, schema As IReadOnlyCollection(Of TableSchema), handler As SqlConversionHandler)
        LogUtilities.ShowDebug("Creating SQLite tables...")

        ' Connect to the newly created database
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Using conn As New SQLiteConnection(sqliteConnString, True)
            conn.Open()

            ' Create all tables in the new database
            Dim count = 0
            For Each dt As TableSchema In schema
                Try
                    AddSQLiteTable(conn, dt)
                Catch ex As Exception
                    LogUtilities.ShowError("AddSQLiteTable failed", ex)
                    Throw
                End Try
                count += 1
                CheckCancelled()
                handler(False, True, CInt((count * 100.0R / schema.Count)), "Added table " & dt.TableName & " to the SQLite database")

                LogUtilities.ShowDebug("added schema for SQLite table [" & dt.TableName & "]")
                ' foreach
            Next
            conn.Close()
        End Using
        ' using
        LogUtilities.ShowDebug("finished adding all table schemas for SQLite database")
    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="sqlitePath"></param>
    ''' <param name="AccessConnString"></param>
    ''' <param name="schema"></param>
    ''' <param name="handler"></param>
    ''' <remarks></remarks>
    Private Shared Sub CopyAccessDBRowsToSQLiteDB(sqlitePath As String, AccessConnString As String, schema As IReadOnlyList(Of TableSchema), handler As SqlConversionHandler)
        CheckCancelled()
        handler(False, True, 0, "Preparing to insert tables...")
        LogUtilities.ShowDebug("preparing to insert tables ...")

        ' Connect to the Access database
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Using accessconn As New OleDbConnection(AccessConnString)
            accessconn.Open()

            ' Connect to the SQLite database next
            Using slconn As New SQLiteConnection(sqliteConnString, True)
                slconn.Open()

                ' Go over all tables in the schema and copy their rows
                For i = 0 To schema.Count - 1
                    Dim tx As SQLiteTransaction = slconn.BeginTransaction()
                    Try
                        Dim tableQuery As String = BuildSqlServerTableQuery(Nothing, schema(i))
                        Dim query As New OleDbCommand(tableQuery, accessconn)
                        Using reader As OleDbDataReader = query.ExecuteReader()
                            Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(i))
                            Dim counter = 0
                            While reader.Read()
                                insert.Connection = slconn
                                insert.Transaction = tx
                                Dim pnames As New List(Of String)()
                                For j = 0 To schema(i).Columns.Count - 1
                                    Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
                                    If TypeOf reader(j) Is DBNull Then
                                        insert.Parameters(pname).Value = DBNull.Value
                                    Else
                                        insert.Parameters(pname).Value = CastValueForColumn(reader(j), schema(i).Columns(j))
                                    End If
                                    pnames.Add(pname)
                                Next
                                insert.ExecuteNonQuery()
                                counter += 1
                                If counter Mod 1000 = 0 Then
                                    CheckCancelled()
                                    tx.Commit()
                                    handler(False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter.ToString("##,##0") & " rows to table ") + schema(i).TableName & " so far")
                                    tx = slconn.BeginTransaction()
                                End If
                                ' while
                            End While
                        End Using
                        ' using
                        CheckCancelled()
                        tx.Commit()

                        handler(False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
                        LogUtilities.ShowDebug("finished inserting all rows for table [" & schema(i).TableName & "]")
                    Catch ex As Exception
                        LogUtilities.ShowDebug("CopyAccessDbRowsToSQLiteDb: Unexpected exception: " & ex.Message)
                        tx.Rollback()
                        Throw
                        ' catch
                    End Try
                Next
                ' using
                slconn.Close()
            End Using
            ' using
            accessconn.Close()
        End Using
    End Sub

    Private Shared Sub CopySqliteDBToSQLiteDB(sqlitePath As String, SqliteSourcePath As String, schema As IReadOnlyList(Of TableSchema), handler As SqlConversionHandler)
        CheckCancelled()
        handler(False, True, 0, "Preparing to insert tables...")
        LogUtilities.ShowDebug("preparing to insert tables ...")

        ' Connect to the Access database
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Dim sqliteSourceConnString As String = CreateSQLiteConnectionString(SqliteSourcePath, Nothing)
        Using slsconn As New SQLiteConnection(sqliteSourceConnString, True)
            slsconn.Open()

            ' Connect to the SQLite database next
            Using slconn As New SQLiteConnection(sqliteConnString, True)
                slconn.Open()

                ' Go over all tables in the schema and copy their rows
                For i = 0 To schema.Count - 1
                    Dim tx As SQLiteTransaction = slconn.BeginTransaction()
                    Try
                        Dim tableQuery As String = BuildSqlServerTableQuery(Nothing, schema(i))
                        Dim query As New SQLiteCommand(tableQuery, slsconn)
                        Using reader As SQLiteDataReader = query.ExecuteReader()
                            Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(i))
                            Dim counter = 0
                            While reader.Read()
                                insert.Connection = slconn
                                insert.Transaction = tx
                                Dim pnames As New List(Of String)()
                                For j = 0 To schema(i).Columns.Count - 1
                                    Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
                                    If TypeOf reader(j) Is DBNull Then
                                        insert.Parameters(pname).Value = DBNull.Value
                                    Else
                                        insert.Parameters(pname).Value = CastValueForColumn(reader(j), schema(i).Columns(j))
                                    End If
                                    pnames.Add(pname)
                                Next
                                insert.ExecuteNonQuery()
                                counter += 1
                                If counter Mod 1000 = 0 Then
                                    CheckCancelled()
                                    tx.Commit()
                                    handler(False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter.ToString("##,##0") & " rows to table ") + schema(i).TableName & " so far")
                                    tx = slconn.BeginTransaction()
                                End If
                                ' while
                            End While
                        End Using
                        ' using
                        CheckCancelled()
                        tx.Commit()

                        handler(False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
                        LogUtilities.ShowDebug("finished inserting all rows for table [" & schema(i).TableName & "]")
                    Catch ex As Exception
                        LogUtilities.ShowDebug("CopyAccessDbRowsToSQLiteDb: Unexpected exception: " & ex.Message)
                        tx.Rollback()
                        Throw
                        ' catch
                    End Try
                Next
                ' using
                slconn.Close()
            End Using
            ' using
            slsconn.Close()
        End Using
    End Sub


    Private Shared Sub CopyTextFileRowsToSQLiteDB(textParams As IList(Of String), sqlitePath As String, textFilePath As String, schema As IReadOnlyList(Of TableSchema), handler As SqlConversionHandler)
        Dim i As Integer
        Dim delim As Char
        Dim header As Boolean
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)

        CheckCancelled()
        handler(False, True, 0, "Preparing to insert tables...")
        LogUtilities.ShowDebug("preparing to insert tables ...")

        delim = textParams(0).Chars(0)
        Boolean.TryParse(textParams(1), header)

        Dim sr = New StreamReader(textFilePath)

        ' The following ReadLine feels like a bug; commenting out in October 2017
        ' sr.ReadLine()

        If Not header Then
            sr.ReadLine()
        End If

        ' Connect to the SQLite database next
        Using slconn As New SQLiteConnection(sqliteConnString, True)
            slconn.Open()

            ' Go over all tables in the schema and copy their rows
            Dim tx As SQLiteTransaction = slconn.BeginTransaction()
            Try
                Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(0))
                Dim counter = 0
                Do While sr.Peek() >= 0
                    Dim row = sr.ReadLine()
                    Dim rowValues = row.Split(delim)

                    insert.Connection = slconn
                    insert.Transaction = tx
                    Dim pnames As New List(Of String)()
                    '                    For i = 0 To rowValues.Count - 1
                    For j = 0 To schema(i).Columns.Count - 1
                        Dim pname As String = "@" & GetNormalizedName(schema(0).Columns(j).ColumnName, pnames)
                        'If String.IsNullOrEmpty(rowValues(i)) Then
                        If String.IsNullOrEmpty(rowValues(j)) Then
                            insert.Parameters(pname).Value = DBNull.Value
                        Else
                            '                            insert.Parameters(pname).Value = CastValueForColumn(rowValues(i), schema(i).Columns(j))
                            insert.Parameters(pname).Value = CastValueForColumn(rowValues(j), schema(i).Columns(j))
                        End If
                        pnames.Add(pname)
                    Next
                    '                    Next

                    insert.ExecuteNonQuery()
                    counter += 1
                    If counter Mod 1000 = 0 Then
                        CheckCancelled()
                        tx.Commit()
                        handler(False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter.ToString("##,##0") & " rows to table ") + schema(i).TableName & " so far")
                        tx = slconn.BeginTransaction()
                    End If
                Loop

                CheckCancelled()
                tx.Commit()
                sr.Close()

                handler(False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
                LogUtilities.ShowDebug("finished inserting all rows for table [" & schema(i).TableName & "]")
            Catch ex As Exception
                LogUtilities.ShowDebug("CopyAccessDbRowsToSQLiteDb: Unexpected exception: " & ex.Message)
                tx.Rollback()
                Throw
                ' catch
            End Try
            ' using
            slconn.Close()
        End Using
        ' using

    End Sub


    ''' <summary>
    ''' Creates the SQLite database from the schema read from the SQL Server.
    ''' </summary>
    ''' <param name="AccessPath">The path to the generated DB file.</param>
    ''' <param name="schema">The schema of the SQL server database.</param>
    ''' <param name="handler">A handle for progress notifications.</param>
    Private Shared Sub CreateAccessDatabase(AccessPath As String, schema As IReadOnlyCollection(Of TableSchema), handler As SqlConversionHandler)
        LogUtilities.ShowDebug("Creating Access database...")

        Dim cat = New Catalog()

        LogUtilities.ShowDebug("Access file was created successfully at [" & AccessPath & "]")

        cat.Create(AccessPath)
        'cat.ActiveConnection.close()
        'cat = Nothing

        Using conn As New OleDbConnection(AccessPath)
            conn.Open()

            Dim count = 0
            For Each dt As TableSchema In schema
                Try
                    AddAccessTable(conn, dt)
                Catch ex As Exception
                    LogUtilities.ShowError("CreateAccessDatabase failed", ex)
                    Throw
                End Try
                count += 1
                CheckCancelled()
                handler(False, True, CInt((count * 100.0R / schema.Count)), "Added table " & dt.TableName & " to the Access database")
                LogUtilities.ShowDebug("added schema for Access table [" & dt.TableName & "]")
                ' foreach
            Next
            conn.Close()
        End Using

        ' using
        LogUtilities.ShowDebug("finished adding all table schemas for Access database")

    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="conn"></param>
    ''' <param name="dt"></param>
    ''' <remarks></remarks>
    Private Shared Sub AddAccessTable(conn As OleDbConnection, dt As TableSchema)
        ' Prepare a CREATE TABLE DDL statement
        Dim stmt As String = BuildAccessCreateTableQuery(dt)

        Try
            ' Execute the query in order to actually create the table.
            Dim cmd As New OleDbCommand(stmt, conn)
            cmd.ExecuteNonQuery()
        Catch ex As Exception
            Throw New Exception(ex.Message & "; " & stmt)
        End Try

    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="ts"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function BuildAccessCreateTableQuery(ts As TableSchema) As String
        Dim sb As New StringBuilder()

        sb.Append("CREATE TABLE [" & ts.TableName & "] (" & vbLf)

        For i = 0 To ts.Columns.Count - 1
            Dim col As ColumnSchema = ts.Columns(i)
            Dim cline As String = BuildAccessColumnStatement(col)
            sb.Append(cline)
            If i < ts.Columns.Count - 1 Then
                sb.Append("," & vbLf)
            End If
        Next
        ' foreach

        sb.Append(vbLf)
        sb.Append(");" & vbLf)

        Dim query As String = sb.ToString()
        Return query
    End Function

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="col"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function BuildAccessColumnStatement(col As ColumnSchema) As String
        Dim sb As New StringBuilder()
        sb.Append(vbTab & "[" & col.ColumnName & "]" & vbTab & vbTab)

        If col.ColumnType.ToLower() = "num" Then
            sb.Append("text")
        Else
            sb.Append(col.ColumnType)
        End If


        Return sb.ToString()
    End Function

    ''' <summary>
    ''' Copies table rows from the SQL Server database to the SQLite database.
    ''' </summary>
    ''' <param name="sqlitePath">The SQL Server connection string</param>
    ''' <param name="AccessConnString">The path to the SQLite database file.</param>
    ''' <param name="schema">The schema of the SQL Server database.</param>
    ''' <param name="handler">A handler to handle progress notifications.</param>
    Private Shared Sub CopySQLiteDBRowsToAccessDB(sqlitePath As String, AccessConnString As String, schema As IReadOnlyList(Of TableSchema), handler As SqlConversionHandler)
        CheckCancelled()
        handler(False, True, 0, "Preparing to insert tables...")
        LogUtilities.ShowDebug("preparing to insert tables ...")

        ' Connect to the SQL Server database
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Using slconn As New SQLiteConnection(sqliteConnString, True)
            slconn.Open()

            ' Connect to the SQLite database next
            Using accessconn As New OleDbConnection(AccessConnString)
                accessconn.Open()

                ' Go over all tables in the schema and copy their rows
                For i = 0 To schema.Count - 1
                    Dim tx As OleDbTransaction = accessconn.BeginTransaction()
                    Try
                        Dim tableQuery As String = BuildSqlServerTableQuery(Nothing, schema(i))
                        Dim query As New SQLiteCommand(tableQuery, slconn)
                        Using reader As SQLiteDataReader = query.ExecuteReader()
                            Dim insert As OleDbCommand = BuildAccessInsert(schema(i))
                            Dim counter = 0
                            While reader.Read()
                                insert.Connection = accessconn
                                insert.Transaction = tx
                                Dim pnames As New List(Of String)()
                                For j = 0 To schema(i).Columns.Count - 1
                                    Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
                                    If TypeOf reader(j) Is DBNull Then
                                        insert.Parameters(pname).Value = DBNull.Value
                                    Else
                                        insert.Parameters(pname).Value = CastValueForColumn(reader(j), schema(i).Columns(j))
                                    End If
                                    pnames.Add(pname)
                                Next
                                insert.ExecuteNonQuery()
                                counter += 1
                                If counter Mod 1000 = 0 Then
                                    CheckCancelled()
                                    tx.Commit()
                                    handler(False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter.ToString("##,##0") & " rows to table ") + schema(i).TableName & " so far")
                                    tx = accessconn.BeginTransaction()
                                End If
                                ' while
                            End While
                        End Using
                        ' using
                        CheckCancelled()
                        tx.Commit()

                        handler(False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
                        LogUtilities.ShowDebug("finished inserting all rows for table [" & schema(i).TableName & "]")
                    Catch ex As Exception
                        LogUtilities.ShowDebug("CopySQLiteDbRowsToAccessDb: Unexpected exception: " & ex.Message)
                        tx.Rollback()
                        Throw
                        ' catch
                    End Try
                Next
                ' using
                accessconn.Close()
            End Using
            ' using
            slconn.Close()
        End Using
    End Sub

    Private Shared Function BuildDataRow(fldDefList As Dictionary(Of String, String)) As DataRow
        Dim tbl = New DataTable("TempDb")
        Dim dr As DataRow
        Dim fldFldType As String()

        For Each item In fldDefList
            fldFldType = item.Key.Split("|"c)
            Dim idColumn = New DataColumn()
            idColumn.DataType = Type.GetType(GetSQLiteStringColumnType(fldFldType(1)))
            idColumn.ColumnName = fldFldType(0)
            tbl.Columns.Add(idColumn)
        Next

        If Not mFunctionList Is Nothing AndAlso mFunctionList.Count > 0 Then
            For i = 0 To mFunctionList.Count - 1
                Dim fldName As String = mFunctionList(i).NewFieldName
                Dim datatype As Type = mFunctionList(i).ReturnDataType

                Dim idColumn = New DataColumn()
                idColumn.DataType = Type.GetType(GetSQLiteStringColumnType(datatype.ToString))
                idColumn.ColumnName = fldName
                tbl.Columns.Add(idColumn)
            Next
        End If

        dr = tbl.NewRow

        Return dr
    End Function

    Private Shared Function GetSQLiteStringColumnType(dataType As String) As String

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


    Private Shared Sub CopySQLiteDBRowsToSQliteDB(fldDefinitionList As Dictionary(Of String, String), sourceTblName As String, functionList As List(Of SingleReturnFunction), sqlitePath As String, schema As IReadOnlyList(Of TableSchema), handler As SqlConversionHandler)
        CheckCancelled()
        handler(False, True, 0, "Preparing to insert tables...")
        LogUtilities.ShowDebug("preparing to insert tables ...")

        ' Connect to the SQL Server database
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Dim tf As New TblFunctions

        Using slconn As New SQLiteConnection(sqliteConnString, True)
            slconn.Open()

            ' Connect to the SQLite database next
            Using sl2conn As New SQLiteConnection(sqliteConnString, True)
                sl2conn.Open()

                ' Go over all tables in the schema and copy their rows
                For i = 0 To schema.Count - 1
                    Dim tx As SQLiteTransaction = sl2conn.BeginTransaction()
                    Try
                        Dim tableQuery As String = BuildSqliteCustomTableQuery(sourceTblName, fldDefinitionList)
                        Dim query As New SQLiteCommand(tableQuery, slconn)
                        Dim dr As DataRow = BuildDataRow(fldDefinitionList)

                        Dim fldFldType As String()

                        Using reader As SQLiteDataReader = query.ExecuteReader()
                            Dim insert As SQLiteCommand = BuildSQLiteInsert(schema(i))
                            Dim counter = 0
                            While reader.Read()
                                insert.Connection = sl2conn
                                insert.Transaction = tx

                                For Each item In fldDefinitionList
                                    fldFldType = item.Key.Split("|"c)
                                    If TypeOf reader(fldFldType(0)) Is DBNull Then
                                        dr.Item(fldFldType(0)) = DBNull.Value
                                    Else
                                        dr.Item(fldFldType(0)) = reader(fldFldType(0)) ' CastValueForColumn(reader(fldFldType(0)), schema(i).Columns(j))
                                    End If
                                Next

                                tf.Functions = functionList
                                dr = tf.PerformFunction(dr)

                                Dim pnames As New List(Of String)()
                                For j = 0 To schema(i).Columns.Count - 1
                                    Dim pname As String = "@" & GetNormalizedName(schema(i).Columns(j).ColumnName, pnames)
                                    insert.Parameters(pname).Value = dr.Item(schema(i).Columns(j).ColumnName)
                                    'If TypeOf reader(j) Is DBNull Then
                                    '    insert.Parameters(pname).Value = DBNull.Value
                                    'Else
                                    '    insert.Parameters(pname).Value = CastValueForColumn(reader(j), schema(i).Columns(j))
                                    'End If
                                    pnames.Add(pname)
                                Next
                                insert.ExecuteNonQuery()
                                counter += 1
                                If counter Mod 1000 = 0 Then
                                    CheckCancelled()
                                    'tx.Commit()
                                    handler(False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter.ToString("##,##0") & " rows to table ") + schema(i).TableName & " so far")
                                    'tx = sl2conn.BeginTransaction()
                                End If
                                ' while
                            End While
                        End Using
                        ' using
                        CheckCancelled()
                        tx.Commit()

                        handler(False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
                        LogUtilities.ShowDebug("finished inserting all rows for table [" & schema(i).TableName & "]")
                    Catch ex As Exception
                        LogUtilities.ShowDebug("CopySQLiteDBRowsToSQliteDB: Unexpected exception: " & ex.Message)
                        tx.Rollback()
                        Throw
                        ' catch
                    End Try
                Next
                ' using
                sl2conn.Close()
            End Using
            ' using
            slconn.Close()
        End Using
    End Sub

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="ts"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function BuildAccessInsert(ts As TableSchema) As OleDbCommand
        Dim res As New OleDbCommand()

        Dim sb As New StringBuilder()
        sb.Append("INSERT INTO [" & ts.TableName & "] (")
        For i = 0 To ts.Columns.Count - 1
            sb.Append("[" & ts.Columns(i).ColumnName & "]")
            If i < ts.Columns.Count - 1 Then
                sb.Append(", ")
            End If
        Next
        ' for
        sb.Append(") VALUES (")

        Dim pnames As New List(Of String)()
        For i = 0 To ts.Columns.Count - 1
            Dim pname As String = "@" & GetNormalizedName(ts.Columns(i).ColumnName, pnames)
            sb.Append(pname)
            If i < ts.Columns.Count - 1 Then
                sb.Append(", ")
            End If

            Dim dbType As DbType = GetAccessDbTypeOfColumn(ts.Columns(i)) 'System.Data.OleDb.OleDbType = GetAccessDbTypeOfColumn(ts.Columns(i))
            Dim prm As New OleDbParameter() 'pname, dbType) ', ts.Columns(i).ColumnName)
            prm.ParameterName = pname
            prm.DbType = dbType

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
    '''
    ''' </summary>
    ''' <param name="cs"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function GetAccessDbTypeOfColumn(cs As ColumnSchema) As DbType 'System.Data.OleDb.OleDbType '

        Select Case cs.ColumnType.ToLower()
            Case "text"
                Return DbType.[String]      'OleDb.OleDbType.VarChar

            Case "float"
                Return DbType.[Single]      'OleDb.OleDbType.Single

            Case "single"
                Return DbType.[Single]      ' OleDb.OleDbType.Single

            Case "double"
                Return DbType.[Double]      'OleDb.OleDbType.Double

            Case "real"
                Return DbType.[Double]      'OleDb.OleDbType.Double

            Case "decimal"
                Return DbType.[Decimal]         'OleDbType.Decimal

            Case "timestamp", "datetime"
                Return DbType.DateTime      'OleDbType.DBTimeStamp

            Case "nchar", "char"
                Return DbType.[String]      'OleDbType.Char

            Case "uniqueidentifier"
                Return DbType.[String]      'OleDbType.VarChar

            Case "xml"
                Return DbType.[String]      'OleDbType.VarChar

            Case "sql_variant"
                Return DbType.[Object]      'OleDbType.Variant

            Case "integer", "int"
                Return DbType.Int32         'OleDbType.Integer

            Case "num"
                Return DbType.[String]      ' Generic number; treat it as text

        End Select

        LogUtilities.ShowError("GetAccessDbTypeOfColumn: illegal db type found in GetAccessDbTypeOfColumn (" & cs.ColumnType & ")")
        Throw New ApplicationException("GetAccessDbTypeOfColumn: Illegal DB type found (" & cs.ColumnType & ")")
    End Function

    ''' <summary>
    ''' Used in order to adjust the value received from SQL Server for the SQLite database.
    ''' </summary>
    ''' <param name="val">The value object</param>
    ''' <param name="columnSchema">The corresponding column schema</param>
    ''' <returns>SQLite adjusted value.</returns>
    Private Shared Function CastValueForColumn(val As Object, columnSchema As ColumnSchema) As Object
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
                'If TypeOf val Is Decimal Then
                '    Return CInt(CDec(val))
                'End If
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
                'If TypeOf val Is Decimal Then
                '    Return CShort(CDec(val))
                'End If
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
                'If TypeOf val Is Decimal Then
                '    Return CLng(CDec(val))
                'End If
                Exit Select

            Case DbType.[Single]
                If TypeOf val Is Double Then
                    Return CSng(CDbl(val))
                End If
                'If TypeOf val Is Decimal Then
                '    Return CSng(CDec(val))
                'End If
                Exit Select

            Case DbType.[Double]
                If TypeOf val Is Single Then
                    Return CDbl(CSng(val))
                End If
                If TypeOf val Is Double Then
                    Return CDbl(val)
                End If
                'If TypeOf val Is Decimal Then
                '    Return CDbl(CDec(val))
                'End If
                Exit Select

            Case DbType.[Decimal]
                Exit Select

            Case DbType.[String]
                If TypeOf val Is Guid Then
                    Return DirectCast(val, Guid).ToString()
                End If
                Exit Select

            Case DbType.DateTime
                If TypeOf val Is Date Then
                    Dim dtDate = CDate(val)
                    Return dtDate.ToString("yyyy-MM-dd HH:mm:ss")
                End If

            Case DbType.Binary, DbType.[Boolean]
                Exit Select
            Case Else

                LogUtilities.ShowError("CastValueForColumn: argument exception in CastValueForColumn - illegal database type: " & dt.ToString())
                Throw New ArgumentException("CastValueForColumn: Illegal database type [" & [Enum].GetName(GetType(DbType), dt) & "]")
        End Select
        ' switch
        Return val
    End Function

    ''' <summary>
    ''' Creates a command object needed to insert values into a specific SQLite table.
    ''' </summary>
    ''' <param name="ts">The table schema object for the table.</param>
    ''' <returns>A command object with the required functionality.</returns>
    Private Shared Function BuildSQLiteInsert(ts As TableSchema) As SQLiteCommand
        Dim res As New SQLiteCommand()

        Dim sb As New StringBuilder()
        sb.Append("INSERT INTO [" & ts.TableName.Replace(" ", "_") & "] (")
        For i = 0 To ts.Columns.Count - 1
            sb.Append("[" & ts.Columns(i).ColumnName.Replace(" ", "_") & "]")
            If i < ts.Columns.Count - 1 Then
                sb.Append(", ")
            End If
        Next
        ' for
        sb.Append(") VALUES (")

        Dim pnames As New List(Of String)()
        For i = 0 To ts.Columns.Count - 1
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
    Private Shared Function GetNormalizedName(str As String, names As ICollection(Of String)) As String
        Dim sb As New StringBuilder()
        For i = 0 To str.Length - 1
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
    Private Shared Function GetDbTypeOfColumn(cs As ColumnSchema) As DbType

        Select Case cs.ColumnType.ToLower()

            Case "text"
                Return DbType.[String] 'OleDb.OleDbType.VarChar

            Case "float"
                Return DbType.[Single] 'OleDb.OleDbType.Single

            Case "single"
                Return DbType.[Single] ' OleDb.OleDbType.Single

            Case "double"
                Return DbType.[Double] 'OleDb.OleDbType.Double

            Case "real"
                Return DbType.[Double] 'OleDb.OleDbType.Double

            Case "decimal"
                Return DbType.[Decimal] 'OleDbType.Decimal

            Case "timestamp", "datetime"
                Return DbType.DateTime 'OleDbType.DBTimeStamp

            Case "nchar", "char"
                Return DbType.[String] 'OleDbType.Char

            Case "uniqueidentifier"
                Return DbType.[String] 'OleDbType.VarChar

            Case "xml"
                Return DbType.[String] 'OleDbType.VarChar

            Case "sql_variant"
                Return DbType.[Object] 'OleDbType.Variant

            Case "integer", "int"
                Return DbType.Int32 'OleDbType.Integer

            Case "smallint"
                Return DbType.Int16 'OleDbType.Int16

            Case "num"
                Return DbType.[String]      ' Generic number; treat it as text

        End Select

        LogUtilities.ShowError("GetDbTypeOfColumn: illegal db type found in GetDbTypeOfColumn: " & cs.ColumnType)
        Throw New ApplicationException("GetDbTypeOfColumn: Illegal DB type found (" & cs.ColumnType & ")")
    End Function

    ''' <summary>
    ''' Builds a SELECT query for a specific table. Needed in the process of copying rows
    ''' from the SQL Server database to the SQLite database.
    ''' </summary>
    ''' <param name="ts">The table schema of the table for which we need the query.</param>
    ''' <returns>The SELECT query for the table.</returns>
    Private Shared Function BuildSqlServerTableQuery(tblNameOverride As String, ts As TableSchema) As String
        Dim sb As New StringBuilder()
        sb.Append("SELECT ")
        For i = 0 To ts.Columns.Count - 1
            sb.Append("[" & ts.Columns(i).ColumnName & "]")
            If i < ts.Columns.Count - 1 Then
                sb.Append(", ")
            End If
        Next
        ' for
        If String.IsNullOrEmpty(tblNameOverride) Then
            sb.Append(" FROM [" & ts.TableName & "]")
        Else
            sb.Append(" FROM [" & tblNameOverride & "]")
        End If
        Return sb.ToString()
    End Function


    Private Shared Function BuildSqliteCustomTableQuery(tblNameOverride As String, colList As Dictionary(Of String, String)) As String
        Dim sb As New StringBuilder()
        Dim fldFldType As String()
        Dim i As Integer
        sb.Append("SELECT ")

        For Each item In colList
            fldFldType = item.Key.Split("|"c)
            sb.Append("[" & fldFldType(0) & "]")
            If i < colList.Count - 1 Then
                sb.Append(", ")
            End If
            i += 1
        Next

        sb.Append(" FROM [" & tblNameOverride & "]")
        Return sb.ToString()
    End Function


    ''' <summary>
    ''' Creates the CREATE TABLE DDL for SQLite and a specific table.
    ''' </summary>
    ''' <param name="conn">The SQLite connection</param>
    ''' <param name="dt">The table schema object for the table to be generated.</param>
    Private Shared Sub AddSQLiteTable(conn As SQLiteConnection, dt As TableSchema)
        ' Prepare a CREATE TABLE DDL statement
        Dim stmt As String = BuildCreateTableQuery(dt)

        LogUtilities.ShowMessage(vbLf & vbLf & stmt & vbLf & vbLf)

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
    Private Shared Function BuildCreateTableQuery(ts As TableSchema) As String
        Dim sb As New StringBuilder()

        sb.Append("CREATE TABLE [" & ts.TableName.Replace(" ", "_") & "] (" & vbLf)

        For i = 0 To ts.Columns.Count - 1
            Dim col As ColumnSchema = ts.Columns(i)
            Dim cline As String = BuildColumnStatement(col)
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
    <Obsolete("Unused")>
    Private Shared Function BuildCreateIndex(tableName As String, indexSchema As IndexSchema) As String
        Dim sb As New StringBuilder()
        sb.Append("CREATE ")
        If indexSchema.IsUnique Then
            sb.Append("UNIQUE ")
        End If
        sb.Append(("INDEX [" & tableName & "_") + indexSchema.IndexName & "]" & vbLf)
        sb.Append("ON [" & tableName & "]" & vbLf)
        sb.Append("(")
        For i = 0 To indexSchema.Columns.Count - 1
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
    Private Shared Function BuildColumnStatement(col As ColumnSchema) As String
        Dim sb As New StringBuilder()
        sb.Append(vbTab & """" & col.ColumnName.Replace(" ", "_") & """" & vbTab & vbTab)

        ' Special treatment for IDENTITY columns
        If col.IsIdentity Then
            'If ts.PrimaryKey.Count = 1 AndAlso (col.ColumnType = "tinyint" OrElse col.ColumnType = "int" OrElse col.ColumnType = "smallint" OrElse col.ColumnType = "bigint") Then
            '    sb.Append("integer PRIMARY KEY AUTOINCREMENT")
            '    pkey = True
            'Else
            sb.Append("integer")
            'End If
        Else
            If col.ColumnType = "int" Then
                sb.Append("integer")
            Else
                sb.Append(col.ColumnType)
            End If
        End If
        If Not col.IsNullable Then
            sb.Append(" NOT NULL")
        End If

        'JDS Research
        'If col.IsCaseSensitivite.HasValue AndAlso Not col.IsCaseSensitivite.Value Then
        '    sb.Append(" COLLATE NOCASE")
        'End If

        Dim defval As String = StripParens(col.DefaultValue)
        defval = DiscardNational(defval)
        'LogUtilities.ShowDebug(("DEFAULT VALUE BEFORE [" & col.DefaultValue & "] AFTER [") + defval & "]")
        If defval <> String.Empty AndAlso defval.ToUpper().Contains("GETDATE") Then
            'LogUtilities.ShowDebug("converted SQL Server GETDATE() to CURRENT_TIMESTAMP for column [" & col.ColumnName & "]")
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
    Private Shared Function DiscardNational(value As String) As String
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
    Private Shared Function IsValidDefaultValue(value As String) As Boolean
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
    Private Shared Function IsSingleQuoted(value As String) As Boolean
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
    Private Shared Function StripParens(value As String) As String
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
    Private Shared Function ReadSqliteSchema(connString As String, handler As SqlConversionHandler, selectionHandler As SqlTableSelectionHandler) As List(Of TableSchema)
        ' First step is to read the names of all tables in the database
        Dim tables As New List(Of TableSchema)()
        Dim sqliteConnString As String = CreateSQLiteConnectionString(connString, Nothing)
        Using conn As New SQLiteConnection(sqliteConnString, True)
            conn.Open()

            Dim tableNames As New List(Of String)()
            ' This command will read the names of all tables in the database
            Dim cmd = conn.CreateCommand
            cmd.CommandText = "select tbl_name as ""TABLE_NAME"", sql as ""SQL Create"" from sqlite_master where type = ""table""" '"PRAGMA table_info ('t_proteins')"
            Using reader As SQLiteDataReader = cmd.ExecuteReader()
                While reader.Read()
                    tableNames.Add(DirectCast(reader("TABLE_NAME"), String))
                End While
            End Using
            ' using
            ' Next step is to use ADO APIs to query the schema of each table.
            Dim count = 0
            For Each tname As String In tableNames
                Dim ts As TableSchema = CreateSQLiteTableSchema(conn, tname)
                tables.Add(ts)
                count += 1
                CheckCancelled()
                handler(False, True, CInt((count * 100.0R / tableNames.Count)), "Parsed table " & tname)

                LogUtilities.ShowDebug("parsed table schema for [" & tname & "]")
                ' foreach
            Next
            conn.Close()
        End Using
        ' using
        LogUtilities.ShowDebug("finished parsing all tables in SQL Server schema")

        ' Allow the user a chance to select which tables to convert
        If selectionHandler IsNot Nothing Then
            Dim updated As List(Of TableSchema) = selectionHandler(tables)
            If updated IsNot Nothing Then
                Return updated
            Else
                Return Nothing
            End If
        End If

        Return tables

    End Function

    ''' <summary>
    ''' Convenience method for checking if the conversion progress needs to be cancelled.
    ''' </summary>
    Private Shared Sub CheckCancelled()
        If _cancelled Then
            Throw New ApplicationException("User cancelled the conversion")
        End If
    End Sub

    ''' <summary>
    ''' Creates a TableSchema object using the specified SQL Server connection
    ''' and the name of the table for which we need to create the schema.
    ''' </summary>
    ''' <param name="conn">The SQL Server connection to use</param>
    ''' <param name="tableName">The name of the table for which we wants to create the table schema.</param>
    ''' <returns>A table schema object that represents our knowledge of the table schema</returns>
    Private Shared Function CreateSQLiteTableSchema(conn As SQLiteConnection, tableName As String) As TableSchema
        Dim res As New TableSchema()
        res.TableName = tableName
        res.Columns = New List(Of ColumnSchema)()
        Dim cmd As New SQLiteCommand("PRAGMA table_info ('" & tableName & "')", conn)
        Using reader As SQLiteDataReader = cmd.ExecuteReader()
            While reader.Read()
                Dim tmp As Object = reader("name")
                If TypeOf tmp Is DBNull Then
                    Continue While
                End If
                Dim colName = DirectCast(reader("name"), String)

                ' tmp = reader("dflt_value")
                'Dim colDefault As String
                'If TypeOf tmp Is DBNull Then
                '    colDefault = String.Empty
                'Else
                '    colDefault = DirectCast(tmp, String)
                'End If

                Dim isNullable = True
                Dim dataType = DirectCast(reader("type"), String)

                ValidateSQLiteDataType(dataType, tableName, colName)
                If dataType = "" Then
                    dataType = "text"
                End If
                ' Note that not all data type names need to be converted because
                ' SQLite establishes type affinity by searching certain strings
                ' in the type name. For example - everything containing the string
                ' 'int' in its type name will be assigned an INTEGER affinity
                If dataType = "datetime" Then
                    dataType = "datetime"
                ElseIf dataType = "numeric" Then
                    dataType = "double"
                ElseIf dataType = "float" Then
                    dataType = "double"
                    'dataType = "single"
                ElseIf dataType = "real" Then
                    dataType = "double"
                    'dataType = "single"
                ElseIf dataType = "double" Then
                    dataType = "double"
                    'dataType = "single"
                ElseIf dataType = "integer" Then
                    dataType = "integer"
                ElseIf dataType = "int" Then
                    dataType = "integer"
                ElseIf dataType = "text" Then
                    dataType = "text"
                ElseIf dataType = "varchar" Then
                    dataType = "text"
                ElseIf dataType = "char" Then
                    dataType = "char"
                ElseIf dataType = "smallint" Then
                    dataType = "integer"
                    'dataType = "decimal"
                ElseIf dataType = "single" Then
                    dataType = "single"
                End If

                ' colDefault = FixDefaultValueString(colDefault)

                Dim col As New ColumnSchema()
                col.ColumnName = colName
                col.ColumnType = dataType
                col.IsNullable = isNullable
                col.IsIdentity = False
                col.DefaultValue = String.Empty ' AdjustDefaultValue(colDefault)
                res.Columns.Add(col)
                ' while
            End While
        End Using

        Return res

    End Function

    ''' <summary>
    ''' Small validation method to make sure we don't miss anything without getting
    ''' an exception.
    ''' </summary>
    ''' <param name="dataType">The datatype to validate.</param>
    Private Shared Sub ValidateSQLiteDataType(dataType As String, tableName As String, fieldName As String)

        Dim lstKnownTypes = New List(Of String) From {
          "datetime", "numeric", "float", "real",
          "integer", "smallint", "int",
          "double", "single", "num",
          "text", "char", "varchar"}

        If String.IsNullOrWhiteSpace(dataType) Then
            Exit Sub
        End If

        If lstKnownTypes.Contains(dataType.ToLower()) Then
            Exit Sub
        End If
        Throw New ApplicationException("SQLite Validation failed for table/field " & tableName & "/" & fieldName & "data type [" & dataType & "]")
    End Sub

    ''' <summary>
    ''' Does some necessary adjustments to a value string that appears in a column DEFAULT
    ''' clause.
    ''' </summary>
    ''' <param name="colDefault">The original default value string (as read from SQL Server).</param>
    ''' <returns>Adjusted DEFAULT value string (for SQLite)</returns>
    <Obsolete("Unused")>
    Private Shared Function FixDefaultValueString(colDefault As String) As String
        Dim replaced = False
        Dim res As String = colDefault.Trim()

        ' Find first/last indexes in which to search
        Dim first As Integer = -1
        Dim last As Integer = -1
        For i = 0 To res.Length - 1
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
        For i = 0 To res.Length - 1
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
    <Obsolete("Unused")>
    Private Shared Function BuildIndexSchema(indexName As String, desc As String, keys As String) As IndexSchema
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
    <Obsolete("Unused")>
    Private Shared Function AdjustDefaultValue(val As String) As String
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
    Private Shared Function CreateSQLiteConnectionString(sqlitePath As String, password As String) As String
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

    Private Shared Function BuildAccessDbPath(accessDbPath As String) As String
        'Dim AccessDbPath As String = System.IO.Path.GetDirectoryName(sqlitePath)
        'Dim AccessDbFilename As String = System.IO.Path.Combine(AccessDbPath, System.IO.Path.GetFileNameWithoutExtension(sqlitePath) & ".accdb")

        ' Delete the target file if it exists already.
        If File.Exists(accessDbPath) Then
            File.Delete(accessDbPath)
        End If

        'Dim AccessDbConn As String = "Provider=Microsoft.Jet.OleDb.4.0;data source=" & AccessDbFilename & ""

        Dim AccessDbConn As String = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" & accessDbPath & ";Jet OLEDB:Database Password=;"

        '"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=c:\dev\New07ADB.accdb;Jet OLEDB:Database Password=admin;" 'for Access 2007
        Return AccessDbConn

    End Function

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="accessPath"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Shared Function BuildAccessDbConnString(accessPath As String) As String
        Dim ver As String
        Dim AccessDbConn As String
        ver = Path.GetExtension(accessPath)
        If ver = "mdb" Then
            AccessDbConn = "Provider=Microsoft.Jet.OleDb.4.0;data source=" & accessPath & ""
        Else
            AccessDbConn = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" & accessPath & ";Persist Security Info=False;"
        End If

        Return AccessDbConn

    End Function

    ''' <summary>
    '''
    ''' </summary>
    ''' <param name="sqlitePath"></param>
    ''' <param name="textFileDirectory"></param>
    ''' <param name="delim"></param>
    ''' <param name="schema"></param>
    ''' <param name="handler"></param>
    ''' <remarks></remarks>
    Private Shared Sub CopySQLiteDBRowsToTextFile(sqlitePath As String, textFileDirectory As String, delim As String, schema As IReadOnlyList(Of TableSchema), handler As SqlConversionHandler)
        CheckCancelled()
        handler(False, True, 0, "Preparing to insert tables...")
        LogUtilities.ShowDebug("preparing to insert tables ...")

        ' Connect to the SQL Server database
        Dim sqliteConnString As String = CreateSQLiteConnectionString(sqlitePath, Nothing)
        Using slconn As New SQLiteConnection(sqliteConnString, True)
            slconn.Open()

            ' Go over all tables in the schema and copy their rows
            For i = 0 To schema.Count - 1

                Try
                    Dim cols As String = String.Empty
                    Dim dataValues As String = String.Empty
                    Dim exportFilename As String = Path.Combine(textFileDirectory, schema(i).TableName & ".txt")

                    Dim tableQuery As String = BuildSqlServerTableQuery(Nothing, schema(i))
                    Dim query As New SQLiteCommand(tableQuery, slconn)
                    Dim sw = New StreamWriter(exportFilename)
                    Using reader As SQLiteDataReader = query.ExecuteReader()
                        Dim counter = 0

                        For j = 0 To schema(i).Columns.Count - 1
                            If j = 0 Then
                                cols += schema(i).Columns(j).ColumnName.ToString
                            Else
                                cols += delim & schema(i).Columns(j).ColumnName.ToString
                            End If
                        Next

                        sw.WriteLine(cols)

                        While reader.Read()
                            For j = 0 To schema(i).Columns.Count - 1
                                If j = 0 Then
                                    dataValues += reader.GetValue(j).ToString
                                Else
                                    dataValues += delim & reader.GetValue(j).ToString
                                End If
                            Next
                            'dataValues += dataValues.Trim(delim)
                            sw.WriteLine(dataValues)
                            dataValues = String.Empty
                            counter += 1
                            If counter Mod 1000 = 0 Then
                                CheckCancelled()
                                handler(False, True, CInt((100.0R * i / schema.Count)), ("Added " & counter.ToString("##,##0") & " rows to table ") + schema(i).TableName & " so far")
                            End If
                            ' while
                        End While
                    End Using
                    ' using
                    sw.Close()
                    CheckCancelled()
                    handler(False, True, CInt((100.0R * i / schema.Count)), "Finished inserting rows for table " & schema(i).TableName)
                    LogUtilities.ShowDebug("finished inserting all rows for table [" & schema(i).TableName & "]")
                Catch ex As Exception
                    LogUtilities.ShowDebug("CopySQLiteDbRowsToAccessDb: Unexpected exception: " & ex.Message)
                    Throw
                    ' catch
                End Try
            Next
            ' using
            slconn.Close()
        End Using
    End Sub

#End Region

#Region "Private Variables"
    Private Shared _isActive As Boolean = False
    Private Shared _cancelled As Boolean = False
    Private Shared ReadOnly _keyRx As New Regex("([a-zA-Z_0-9]+)(\(\-\))?")
    Private Shared ReadOnly _defaultValueRx As New Regex("\(N(\'.*\')\)")
    'Private Shared _log As ILog = LogManager.GetLogger(GetType(SqliteToAccess))
#End Region
End Class

'''' <summary>
'''' This handler is called whenever a progress is made in the conversion process.
'''' </summary>
'''' <param name="done">TRUE indicates that the entire conversion process is finished.</param>
'''' <param name="success">TRUE indicates that the current step finished successfully.</param>
'''' <param name="percent">Progress percent (0-100)</param>
'''' <param name="msg">A message that accompanies the progress.</param>
'Public Delegate Sub SqliteConversionHandler(ByVal done As Boolean, ByVal success As Boolean, ByVal percent As Integer, ByVal msg As String)

'''' <summary>
'''' This handler allows the user to change which tables get converted from SQL Server
'''' to SQLite.
'''' </summary>
'''' <param name="schema">The original SQL Server DB schema</param>
'''' <returns>The same schema minus any table we don't want to convert.</returns>
'Public Delegate Function SqliteTableSelectionHandler(ByVal schema As List(Of TableSchema)) As List(Of TableSchema)
