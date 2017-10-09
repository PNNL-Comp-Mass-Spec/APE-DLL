Option Strict On

''' <summary>
'''
''' </summary>
''' <remarks></remarks>
Public Class clsXMLStepSchema
    Protected mStepNumText As String
    Protected mStepNumValue As Integer

    Public Source As String

    Public SQL As String

    Public TargetTable As String

    Public KeepTargetTable As String

    Public PivotTable As String

    Public FunctionTable As String

    Public Description As String

    Public IterationTable As String

    Public WorkflowGroup As String

    Public Property StepNo As String
        Get
            Return mStepNumText
        End Get
        Set(value As String)
            mStepNumText = value
            Integer.TryParse(mStepNumText, mStepNumValue)
        End Set
    End Property

    Public ReadOnly Property StepNum As Integer
        Get
            Return mStepNumValue
        End Get
    End Property
End Class

''' <summary>
'''
''' </summary>
''' <remarks></remarks>
Public Class clsXMLFields

    Public Const APE_WORKFLOW As String = "MdartWorkflow"

    Public Const TITLE As String = "Title"

    Public Const WORKFLOW_DESCRIPTION As String = "WorkflowDescription"

    Public Const STEPS As String = "Steps"

    Public Const STEP_NO As String = "Step"

    Public Const STEP_ID As String = "id"

    Public Const SOURCE As String = "Source"

    Public Const SQL_STRING As String = "query"

    Public Const TARGET_TABLE As String = "TargetTable"

    Public Const KEEP_TARGET_TABLE As String = "KeepTargetTable"

    Public Const PIVOT_TABLE As String = "PivotTable"

    Public Const STEP_DESCRIPTION As String = "Description"

    Public Const FUNCTION_TABLE As String = "FunctionTable"

    Public Const ITERATION_TABLE As String = "IterationTable"

    Public Const WORKFLOW_GROUP As String = "WorkflowGroup"

    Public Const NUM_ELEMENTS As Integer = 15

End Class

