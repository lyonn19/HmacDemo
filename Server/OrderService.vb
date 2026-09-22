Option Strict On
Option Infer On

Public Class OrderService
    Implements IOrderService

    ' The signature is read from the X-Signature / X-Timestamp HTTP headers.
    <HmacSignature()>
    Public Function CreateOrder(order As OrderDto) As String _
            Implements IOrderService.CreateOrder

        Return String.Format("Order {0} created for {1} ({2})",
                             order.Id, order.Customer, order.Amount.ToString("N2"))
    End Function

    ' Not decorated: the signature headers are ignored.
    Public Function GetStatus(orderId As Integer) As String _
            Implements IOrderService.GetStatus

        Return "Order " & orderId & " is OK"
    End Function

End Class
