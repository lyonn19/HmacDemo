Option Strict On
Option Infer On

Imports System.ServiceModel

' Hand-written proxy. This is exactly what "Add Service Reference" generates
' (a ClientBase(Of T) class), so a generated proxy behaves the same way.
Public Class OrderServiceClient
    Inherits ClientBase(Of IOrderService)
    Implements IOrderService

    Public Sub New()
        MyBase.New()
    End Sub

    Public Sub New(endpointConfigurationName As String)
        MyBase.New(endpointConfigurationName)
    End Sub

    Public Function CreateOrder(order As OrderDto) As String _
            Implements IOrderService.CreateOrder
        Return MyBase.Channel.CreateOrder(order)
    End Function

    Public Function GetStatus(orderId As Integer) As String _
            Implements IOrderService.GetStatus
        Return MyBase.Channel.GetStatus(orderId)
    End Function

End Class
