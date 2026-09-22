Option Strict On
Option Infer On

Imports System.Runtime.Serialization
Imports System.ServiceModel

' Shared by the server and the client (in a real project this is what the
' generated Service Reference gives the client).

<DataContract>
Public Class OrderDto
    <DataMember> Public Property Id As Integer
    <DataMember> Public Property Customer As String
    <DataMember> Public Property Amount As Decimal
End Class

<ServiceContract>
Public Interface IOrderService

    ' Protected: the implementation is decorated with <HmacSignature()>
    <OperationContract>
    Function CreateOrder(order As OrderDto) As String

    ' Not protected: works with or without the signature headers
    <OperationContract>
    Function GetStatus(orderId As Integer) As String

End Interface
