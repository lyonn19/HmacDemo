Option Strict On
Option Infer On

Imports System.ServiceModel

Module Program

    Sub Main()
        Dim host As New ServiceHost(GetType(OrderService))
        Try
            host.Open()

            Console.WriteLine("Service running at:")
            For Each ep In host.Description.Endpoints
                Console.WriteLine("  " & ep.Address.Uri.ToString())
            Next
            Console.WriteLine()
            Console.WriteLine("Press ENTER to stop.")
            Console.ReadLine()

            host.Close()
        Catch ex As Exception
            host.Abort()
            Console.WriteLine("Could not start the service: " & ex.Message)
            Console.WriteLine("Press ENTER to exit.")
            Console.ReadLine()
        End Try
    End Sub

End Module
