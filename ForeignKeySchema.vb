Imports System
Imports System.Collections.Generic
Imports System.Text

Namespace DbAccess

    Public Class ForeignKeySchema
        Public TableName As String

        Public ColumnName As String

        Public ForeignTableName As String

        Public ForeignColumnName As String

        Public CascadeOnDelete As Boolean

        Public IsNullable As Boolean

    End Class

End Namespace
