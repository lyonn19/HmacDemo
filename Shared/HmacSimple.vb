Option Strict On
Option Infer On

Imports System.Configuration
Imports System.Globalization
Imports System.IO
Imports System.Net
Imports System.Runtime.Serialization.Json
Imports System.Security.Cryptography
Imports System.ServiceModel
Imports System.ServiceModel.Channels
Imports System.ServiceModel.Description
Imports System.ServiceModel.Dispatcher
Imports System.ServiceModel.Web
Imports System.Text

' =====================================================================
' HOW THIS VERSION WORKS
'
' Instead of hashing the raw HTTP body (which requires a behavior on
' the client), the signature is computed over a string both sides can
' build on their own:
'
'       timestamp | operationName | JSON of the parameters
'
' Client:  OperationContextScope adds two HTTP headers per call
'          (X-Timestamp and X-Signature).  No behavior, no config.
' Server:  the [HmacSignature] attribute rebuilds the same string,
'          recomputes the HMAC and compares.  The old body-capturing
'          message inspector is no longer needed.
'
' Requires references: System.ServiceModel, System.ServiceModel.Web,
' System.Runtime.Serialization, System.Configuration.
' Use this file INSTEAD of HmacSignature.vb / HmacClient.vb (same
' attribute name, so your service methods do not change).
' =====================================================================

' =====================================================================
' SERVER: attribute
'
'   <HmacSignature()>
'   Public Function CreateOrder(order As OrderDto) As String Implements IOrderService.CreateOrder
' =====================================================================
<AttributeUsage(AttributeTargets.Method, AllowMultiple:=False)>
Public NotInheritable Class HmacSignatureAttribute
    Inherits Attribute
    Implements IOperationBehavior

    Public Property HeaderName As String = HmacHelper.SignatureHeader
    Public Property SecretAppSettingKey As String = "HmacSecret"

    ''' <summary>Maximum allowed difference between client and server clocks.</summary>
    Public Property MaxAgeSeconds As Integer = 300

    Public Sub AddBindingParameters(operationDescription As OperationDescription,
                                    bindingParameters As BindingParameterCollection) _
                                    Implements IOperationBehavior.AddBindingParameters
    End Sub

    Public Sub ApplyClientBehavior(operationDescription As OperationDescription,
                                   clientOperation As ClientOperation) _
                                   Implements IOperationBehavior.ApplyClientBehavior
    End Sub

    Public Sub ApplyDispatchBehavior(operationDescription As OperationDescription,
                                     dispatchOperation As DispatchOperation) _
                                     Implements IOperationBehavior.ApplyDispatchBehavior
        dispatchOperation.ParameterInspectors.Add(
            New HmacParameterInspector(HeaderName, ReadSecret(), MaxAgeSeconds))
    End Sub

    Public Sub Validate(operationDescription As OperationDescription) _
                        Implements IOperationBehavior.Validate
        ReadSecret()
    End Sub

    Private Function ReadSecret() As Byte()
        Dim secret As String = ConfigurationManager.AppSettings(SecretAppSettingKey)
        If String.IsNullOrEmpty(secret) Then
            Throw New ConfigurationErrorsException(
                "appSettings key '" & SecretAppSettingKey & "' is missing or empty.")
        End If
        Return Encoding.UTF8.GetBytes(secret)
    End Function
End Class

' =====================================================================
' SERVER: validation (runs right before your method)
' =====================================================================
Friend NotInheritable Class HmacParameterInspector
    Implements IParameterInspector

    Private ReadOnly _headerName As String
    Private ReadOnly _secret As Byte()
    Private ReadOnly _maxAgeSeconds As Integer

    Public Sub New(headerName As String, secret As Byte(), maxAgeSeconds As Integer)
        _headerName = headerName
        _secret = secret
        _maxAgeSeconds = maxAgeSeconds
    End Sub

    Public Function BeforeCall(operationName As String, inputs() As Object) As Object _
                               Implements IParameterInspector.BeforeCall

        Dim ctx As OperationContext = OperationContext.Current
        Dim isRest As Boolean = (ctx.IncomingMessageVersion.Envelope = EnvelopeVersion.None)

        Dim obj As Object = Nothing
        If Not ctx.IncomingMessageProperties.TryGetValue(HttpRequestMessageProperty.Name, obj) Then
            Throw Denied(isRest, "Missing HTTP headers.")
        End If
        Dim headers As WebHeaderCollection = DirectCast(obj, HttpRequestMessageProperty).Headers

        Dim signature As String = headers(_headerName)
        Dim timestamp As String = headers(HmacHelper.TimestampHeader)
        If String.IsNullOrWhiteSpace(signature) OrElse String.IsNullOrWhiteSpace(timestamp) Then
            Throw Denied(isRest, "Missing signature or timestamp header.")
        End If

        ' 1) Freshness (protects against replay of old requests)
        Dim ts As Long
        If Not Long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, ts) Then
            Throw Denied(isRest, "Invalid timestamp.")
        End If
        If Math.Abs(HmacHelper.UnixNow() - ts) > _maxAgeSeconds Then
            Throw Denied(isRest, "Request expired.")
        End If

        ' 2) Signature over  timestamp|operation|parameters
        Dim data As String = HmacHelper.BuildData(ts, operationName, inputs)
        Dim expected As Byte() = HmacHelper.Compute(_secret, Encoding.UTF8.GetBytes(data))
        Dim provided As Byte() = HmacHelper.TryFromBase64(signature.Trim())

        If provided Is Nothing OrElse Not HmacHelper.FixedTimeEquals(expected, provided) Then
            Throw Denied(isRest, "Invalid signature.")
        End If

        Return Nothing
    End Function

    Public Sub AfterCall(operationName As String, outputs() As Object,
                         returnValue As Object, correlationState As Object) _
                         Implements IParameterInspector.AfterCall
    End Sub

    Private Shared Function Denied(isRest As Boolean, reason As String) As Exception
        If isRest Then Return New WebFaultException(HttpStatusCode.Unauthorized)
        Return New FaultException(reason, New FaultCode("Sender"))
    End Function
End Class

' =====================================================================
' CLIENT: call this around each proxy call - no behavior needed
'
'   Dim secret As String = ConfigurationManager.AppSettings("HmacSecret")
'   Dim client As New OrderServiceClient()
'
'   Using HmacClientCall.Sign(client.InnerChannel, secret, "CreateOrder", order)
'       result = client.CreateOrder(order)
'   End Using
'
'   - "CreateOrder" is the operation name (method name, or the Name set
'     in <OperationContract(Name:=...)>).
'   - After the operationName, pass the SAME parameters, in the SAME
'     order, that you pass to the method (none if it has no parameters).
' =====================================================================
Public Module HmacClientCall

    Public Function Sign(channel As IContextChannel,
                         secret As String,
                         operationName As String,
                         ParamArray parameters As Object()) As IDisposable

        If String.IsNullOrEmpty(secret) Then
            Throw New ArgumentException("HMAC secret is missing.", NameOf(secret))
        End If

        Dim scope As New OperationContextScope(channel)
        Dim ts As Long = HmacHelper.UnixNow()
        Dim data As String = HmacHelper.BuildData(ts, operationName, parameters)

        Dim headers As WebHeaderCollection = WebOperationContext.Current.OutgoingRequest.Headers
        headers(HmacHelper.TimestampHeader) = ts.ToString(CultureInfo.InvariantCulture)
        headers(HmacHelper.SignatureHeader) = HmacHelper.ComputeBase64(secret, data)

        Return scope ' dispose it (Using) when the call is finished
    End Function
End Module

' =====================================================================
' SHARED: helpers used by both sides
' =====================================================================
Public Module HmacHelper

    Public Const SignatureHeader As String = "X-Signature"
    Public Const TimestampHeader As String = "X-Timestamp"

    Public Function UnixNow() As Long
        Return CLng((DateTime.UtcNow - New DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds)
    End Function

    ''' <summary>String that is signed: timestamp|operation|parameters(JSON)</summary>
    Public Function BuildData(timestamp As Long, operationName As String, parameters As Object()) As String
        Return timestamp.ToString(CultureInfo.InvariantCulture) & "|" &
               operationName & "|" &
               SerializeParams(parameters)
    End Function

    Public Function SerializeParams(values As Object()) As String
        Dim sb As New StringBuilder()
        If values IsNot Nothing Then
            For Each v As Object In values
                If v Is Nothing Then
                    sb.Append("null")
                Else
                    Dim serializer As New DataContractJsonSerializer(v.GetType())
                    Using ms As New MemoryStream()
                        serializer.WriteObject(ms, v)
                        sb.Append(Encoding.UTF8.GetString(ms.ToArray()))
                    End Using
                End If
                sb.Append(";"c)
            Next
        End If
        Return sb.ToString()
    End Function

    Public Function Compute(secret As Byte(), data As Byte()) As Byte()
        Using h As New HMACSHA256(secret)
            Return h.ComputeHash(data)
        End Using
    End Function

    Public Function ComputeBase64(secret As String, data As String) As String
        Return Convert.ToBase64String(
            Compute(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data)))
    End Function

    Public Function TryFromBase64(value As String) As Byte()
        Try
            Return Convert.FromBase64String(value)
        Catch ex As FormatException
            Return Nothing
        End Try
    End Function

    ''' <summary>Constant-time comparison (avoids timing attacks).</summary>
    Public Function FixedTimeEquals(a As Byte(), b As Byte()) As Boolean
        If a.Length <> b.Length Then Return False
        Dim diff As Integer = 0
        For i As Integer = 0 To a.Length - 1
            diff = diff Or CInt(a(i) Xor b(i))
        Next
        Return diff = 0
    End Function
End Module

' =====================================================================
' web.config (server) and app.config (client) - same secret on both
'
' <appSettings>
'   <add key="HmacSecret" value="a-long-random-shared-secret" />
' </appSettings>
' =====================================================================
