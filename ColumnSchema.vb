Imports System
Imports System.Collections.Generic
Imports System.Text


Public Class ColumnSchema
    Public ColumnName As String

    Public ColumnType As String

    Public IsNullable As Boolean = True

    Public DefaultValue As String

    Public IsIdentity As Boolean = False

    Public IsCaseSensitivite As Boolean = Nothing

End Class

