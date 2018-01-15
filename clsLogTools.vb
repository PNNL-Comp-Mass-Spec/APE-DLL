'*********************************************************************************************************
' Written by Dave Clark for the US Department of Energy
' Pacific Northwest National Laboratory, Richland, WA
' Copyright 2009, Battelle Memorial Institute
' Created 01/01/2009
'*********************************************************************************************************

Option Strict On

Imports PRISM.Logging
Imports System.IO

''' <summary>
''' Class for handling logging via the FileLogger and DatabaseLogger
''' </summary>
''' <remarks>
''' Call method CreateFileLogger to define the log file name
''' </remarks>
Public Class clsLogTools

#Region "Enums"

    ''' <summary>
    ''' Log types
    ''' </summary>
    Public Enum LoggerTypes
        ''' <summary>
        ''' Log to a log file
        ''' </summary>
        LogFile
        ''' <summary>
        ''' Log to the database and to the log file
        ''' </summary>
        LogDb
    End Enum

#End Region

#Region "Class variables"

    ''' <summary>
    ''' File Logger
    ''' </summary>
    Private Shared ReadOnly m_FileLogger As FileLogger = New FileLogger()

    ''' <summary>
    ''' Database logger
    ''' </summary>
    Private Shared ReadOnly m_DbLogger As DatabaseLogger = New SQLServerDatabaseLogger()

#End Region

#Region "Properties"

    ''' <summary>
    ''' File path for the current log file used by the FileAppender
    ''' </summary>
    Public ReadOnly Property CurrentFileAppenderPath As String
        Get
            If (String.IsNullOrEmpty(FileLogger.LogFilePath)) Then
                Return String.Empty
            End If

            Return FileLogger.LogFilePath
        End Get
    End Property

    ''' <summary>
    ''' Tells calling program file debug status
    ''' </summary>
    Public Shared ReadOnly Property FileLogDebugEnabled As Boolean
        Get
            Return m_FileLogger.IsDebugEnabled
        End Get
    End Property

    ''' <summary>
    ''' Most recent error message
    ''' </summary>
    Public Shared ReadOnly Property MostRecentErrorMessage As String
        Get
            Return BaseLogger.MostRecentErrorMessage
        End Get
    End Property

    ''' <summary>
    ''' Working directory path
    ''' </summary>
    Public Shared Property WorkDirPath As String

#End Region

#Region "Methods"

    ''' <summary>
    ''' Writes a message to the logging system
    ''' </summary>
    ''' <param name="loggerType">Type of logger to use</param>
    ''' <param name="logLevel">Level of log reporting</param>
    ''' <param name="message">Message to be logged</param>
    Public Shared Sub WriteLog(loggerType As LoggerTypes, logLevel As BaseLogger.LogLevels, message As String)
        WriteLogWork(loggerType, logLevel, message, Nothing)
    End Sub

    ''' <summary>
    ''' Overload to write a message and exception to the logging system
    ''' </summary>
    ''' <param name="loggerType">Type of logger to use</param>
    ''' <param name="logLevel">Level of log reporting</param>
    ''' <param name="message">Message to be logged</param>
    ''' <param name="ex">Exception to be logged</param>
    Public Shared Sub WriteLog(loggerType As LoggerTypes, logLevel As BaseLogger.LogLevels, message As String, ex As Exception)
        WriteLogWork(loggerType, logLevel, message, ex)
    End Sub

    ''' <summary>
    ''' Write a message and possibly an exception to the logging system
    ''' </summary>
    ''' <param name="loggerType">Type of logger to use</param>
    ''' <param name="logLevel">Level of log reporting</param>
    ''' <param name="message">Message to be logged</param>
    ''' <param name="ex">Exception to be logged</param>
    Private Shared Sub WriteLogWork(loggerType As LoggerTypes, logLevel As BaseLogger.LogLevels, message As String, ex As Exception)
        Dim myLogger As BaseLogger

        ' Establish which logger will be used
        Select Case loggerType
            Case LoggerTypes.LogDb
                ' Note that the Database logger will (by default) also echo messages to the file logger
                myLogger = m_DbLogger
                message = Net.Dns.GetHostName() + ": " + message

            Case LoggerTypes.LogFile
                myLogger = m_FileLogger

                If Not String.IsNullOrWhiteSpace(FileLogger.LogFilePath) AndAlso
                   Not FileLogger.LogFilePath.Contains(Path.DirectorySeparatorChar.ToString()) Then

                    Dim logFileName = Path.GetFileName(FileLogger.LogFilePath)
                    Dim workDirLogPath As String

                    If (String.IsNullOrEmpty(WorkDirPath)) Then
                        workDirLogPath = Path.Combine(".", logFileName)
                    Else
                        workDirLogPath = Path.Combine(WorkDirPath, logFileName)
                    End If
                    ChangeLogFileBaseName(workDirLogPath, FileLogger.AppendDateToBaseFileName)

                End If

            Case Else
                Throw New Exception("Invalid logger type specified")
        End Select

        RaiseEvent MessageLogged(message, logLevel)

        ' Send the log message
        myLogger?.LogMessage(logLevel, message, ex)
    End Sub


    ''' <summary>
    ''' Update the log file's base name (or relative path)
    ''' However, if appendDateToBaseName is false, baseName is the full path to the log file
    ''' </summary>
    ''' <param name="baseName">Base log file name (or relative path)</param>
    ''' <param name="appendDateToBaseName">
    ''' When true, the actual log file name will have today's date appended to it, in the form mm-dd-yyyy.txt
    ''' When false, the actual log file name will be the base name plus .txt (unless the base name already has an extension)
    ''' </param>
    ''' <remarks>If baseName is null or empty, the log file name will be named DefaultLogFileName</remarks>
    Public Shared Sub ChangeLogFileBaseName(baseName As String, appendDateToBaseName As Boolean)
        FileLogger.ChangeLogFileBaseName(baseName, appendDateToBaseName)
    End Sub

    ''' <summary>
    ''' Sets the file logging level via an integer value (Overloaded)
    ''' </summary>
    ''' <param name="logLevel">Integer corresponding to level (1-5, 5 being most verbose</param>
    Public Shared Sub SetFileLogLevel(logLevel As Integer)

        Dim LogLevelEnumType As Type = GetType(BaseLogger.LogLevels)

        ' Verify input level is a valid log level
        If Not [Enum].IsDefined(LogLevelEnumType, logLevel) Then
            WriteLog(LoggerTypes.LogFile, BaseLogger.LogLevels.ERROR, "Invalid value specified for level: " & logLevel.ToString)
            Return
        End If

        ' Convert input integer into the associated enum
        Dim logLevelEnum = DirectCast([Enum].Parse(LogLevelEnumType, logLevel.ToString), BaseLogger.LogLevels)

        SetFileLogLevel(logLevelEnum)

    End Sub

    ''' <summary>
    ''' Sets file logging level based on enumeration (Overloaded)
    ''' </summary>
    ''' <param name="logLevel">LogLevels value defining level (Debug is most verbose)</param>
    Public Shared Sub SetFileLogLevel(logLevel As BaseLogger.LogLevels)
        m_FileLogger.LogLevel = logLevel
    End Sub

    ''' <summary>
    ''' Configures the file logger
    ''' </summary>
    ''' <param name="logFileNameBase">Base name for log file</param>
    ''' <param name="traceMode">When true, show additional debug messages at the console</param>
    Public Shared Sub CreateFileLogger(logFileNameBase As String, Optional traceMode As Boolean = False)
        If traceMode AndAlso Not BaseLogger.TraceMode Then
            BaseLogger.TraceMode = True
        End If

        FileLogger.ChangeLogFileBaseName(logFileNameBase, appendDateToBaseName:=True)

        ' This program determines when to log Or Not log based on internal logic
        ' Set the LogLevel tracked by FileLogger to DEBUG so that all messages sent to this class are logged
        SetFileLogLevel(BaseLogger.LogLevels.DEBUG)
    End Sub

    ''' <summary>
    ''' Configures the database logger
    ''' </summary>
    ''' <param name="connStr">System.Data.SqlClient style connection string</param>
    ''' <param name="moduleName">Module name used by logger</param>
    ''' <param name="traceMode">When true, show additional debug messages at the console</param>
    Public Shared Sub CreateDbLogger(connStr As String, moduleName As String, Optional traceMode As Boolean = False)
        m_DbLogger.LogLevel = BaseLogger.LogLevels.INFO

        If traceMode AndAlso Not BaseLogger.TraceMode Then
            BaseLogger.TraceMode = True
        End If

        m_DbLogger.ChangeConnectionInfo(moduleName, connStr, "PostLogEntry", "type", "message", "postedBy", 128, 4096, 128)
    End Sub

    ''' <summary>
    ''' Remove the default database logger that was created when the program first started
    ''' </summary>
    Public Shared Sub RemoveDefaultDbLogger()
        m_DbLogger.RemoveConnectionInfo()
    End Sub

    ''' <summary>
    ''' Delegate for event MessageLogged
    ''' </summary>
    Public Delegate Sub MessageLoggedEventHandler(message As String, logLevel As BaseLogger.LogLevels)

    ''' <summary>
    ''' This event is raised when a message is logged
    ''' </summary>
    Public Shared Event MessageLogged As MessageLoggedEventHandler

#End Region

End Class

