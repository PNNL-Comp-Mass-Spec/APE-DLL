Imports PRISM.Logging

Public Class LogUtilities

    Public Shared Sub ShowDebug(message As String)
        PRISM.ConsoleMsgUtils.ShowDebug(message)

        LogTools.WriteLog(LogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.DEBUG, message)
    End Sub

    Public Shared Sub ShowError(message As String, Optional logToFile As Boolean = True)
        PRISM.ConsoleMsgUtils.ShowError(message)

        If logToFile Then
            LogTools.WriteLog(LogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.ERROR, message)
        End If
    End Sub

    Public Shared Sub ShowError(message As String, ex As Exception, Optional logToFile As Boolean = True)
        PRISM.ConsoleMsgUtils.ShowError(message, ex)

        If logToFile Then
            LogTools.WriteLog(LogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.ERROR, message & ": " & ex.Message)
        End If
    End Sub

    Public Shared Sub ShowMessage(message As String)
        Console.WriteLine(message)
        LogTools.WriteLog(LogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.INFO, message)
    End Sub

    Public Shared Sub ShowWarning(message As String, Optional logToFile As Boolean = True)

        PRISM.ConsoleMsgUtils.ShowWarning(message)

        If logToFile Then
            LogTools.WriteLog(LogTools.LoggerTypes.LogFile, BaseLogger.LogLevels.WARN, message)
        End If

    End Sub

End Class
