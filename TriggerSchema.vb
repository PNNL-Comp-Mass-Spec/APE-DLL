Imports System
Imports System.Collections.Generic
Imports System.Text

Namespace DbAccess

    Public Enum TriggerEvent
        Delete
        Update
        Insert
    End Enum

    Public Enum TriggerType
        After
        Before
    End Enum

    Public Class TriggerSchema

        Public Name As String
        Public [Event] As TriggerEvent
        Public Type As TriggerType
        Public Body As String
        Public Table As String

    End Class

End Namespace
