Imports System
Imports System.Collections.Generic
Imports System.Text

Namespace DbAccess

    Public Class TriggerBuilder

        Public Shared Function GetForeignKeyTriggers(ByVal dt As TableSchema) As IList(Of TriggerSchema)
            Dim result As IList(Of TriggerSchema) = New List(Of TriggerSchema)()

            'For Each fks As ForeignKeySchema In dt.ForeignKeys
            '    Dim sb As New StringBuilder()
            '    result.Add(GenerateInsertTrigger(fks))
            '    result.Add(GenerateUpdateTrigger(fks))
            '    result.Add(GenerateDeleteTrigger(fks))
            'Next
            Return result
        End Function

        Private Shared Function MakeTriggerName(ByVal fks As ForeignKeySchema, ByVal prefix As String) As String
            Return ((((prefix & "_") + fks.TableName & "_") + fks.ColumnName & "_") + fks.ForeignTableName & "_") + fks.ForeignColumnName
        End Function

        Public Shared Function GenerateInsertTrigger(ByVal fks As ForeignKeySchema) As TriggerSchema
            Dim trigger As New TriggerSchema()
            trigger.Name = MakeTriggerName(fks, "fki")
            trigger.Type = TriggerType.Before
            trigger.[Event] = TriggerEvent.Insert
            trigger.Table = fks.TableName

            Dim nullString As String = ""
            If fks.IsNullable Then
                nullString = " NEW." & fks.ColumnName & " IS NOT NULL AND"
            End If

            trigger.Body = (((((("SELECT RAISE(ROLLBACK, 'insert on table " & fks.TableName & " violates foreign key constraint ") + trigger.Name & "')" & " WHERE") + nullString & " (SELECT ") + fks.ForeignColumnName & " FROM ") + fks.ForeignTableName & " WHERE ") + fks.ForeignColumnName & " = NEW.") + fks.ColumnName & ") IS NULL; "
            Return trigger
        End Function

        Public Shared Function GenerateUpdateTrigger(ByVal fks As ForeignKeySchema) As TriggerSchema
            Dim trigger As New TriggerSchema()
            trigger.Name = MakeTriggerName(fks, "fku")
            trigger.Type = TriggerType.Before
            trigger.[Event] = TriggerEvent.Update
            trigger.Table = fks.TableName

            Dim triggerName As String = trigger.Name
            Dim nullString As String = ""
            If fks.IsNullable Then
                nullString = " NEW." & fks.ColumnName & " IS NOT NULL AND"
            End If

            trigger.Body = (((((("SELECT RAISE(ROLLBACK, 'update on table " & fks.TableName & " violates foreign key constraint ") + triggerName & "')" & " WHERE") + nullString & " (SELECT ") + fks.ForeignColumnName & " FROM ") + fks.ForeignTableName & " WHERE ") + fks.ForeignColumnName & " = NEW.") + fks.ColumnName & ") IS NULL; "

            Return trigger
        End Function

        Public Shared Function GenerateDeleteTrigger(ByVal fks As ForeignKeySchema) As TriggerSchema
            Dim trigger As New TriggerSchema()
            trigger.Name = MakeTriggerName(fks, "fkd")
            trigger.Type = TriggerType.Before
            trigger.[Event] = TriggerEvent.Delete
            trigger.Table = fks.ForeignTableName

            Dim triggerName As String = trigger.Name

            If Not fks.CascadeOnDelete Then
                trigger.Body = ((((("SELECT RAISE(ROLLBACK, 'delete on table " & fks.ForeignTableName & " violates foreign key constraint ") + triggerName & "')" & " WHERE (SELECT ") + fks.ColumnName & " FROM ") + fks.TableName & " WHERE ") + fks.ColumnName & " = OLD.") + fks.ForeignColumnName & ") IS NOT NULL; "
            Else

                trigger.Body = (("DELETE FROM [" & fks.TableName & "] WHERE ") + fks.ColumnName & " = OLD.") + fks.ForeignColumnName & "; "
            End If
            Return trigger
        End Function

    End Class

End Namespace
