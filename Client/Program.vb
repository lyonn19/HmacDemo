Option Strict On
Option Infer On

Imports System.Configuration
Imports System.ServiceModel

Module Program

    Sub Main()
        Dim secret As String = ConfigurationManager.AppSettings("HmacSecret")
        Dim order As New OrderDto With {.Id = 1, .Customer = "ACME", .Amount = 150.5D}

        Dim client As New OrderServiceClient()
        Try

            ' 1) Signed call with the right secret -> accepted
            Console.WriteLine("1) Signed call, right secret:")
            Try
                Using HmacClientCall.Sign(client.InnerChannel, secret, "CreateOrder", order)
                    Console.WriteLine("   -> " & client.CreateOrder(order))
                End Using
            Catch ex As FaultException
                Console.WriteLine("   -> REJECTED: " & ex.Message)
            End Try

            ' 2) Method that does not need a signature -> no signing needed
            Console.WriteLine("2) Unsigned call to GetStatus (not protected):")
            Try
                Console.WriteLine("   -> " & client.GetStatus(1))
            Catch ex As FaultException
                Console.WriteLine("   -> REJECTED: " & ex.Message)
            End Try

            ' 3) Signed with a wrong secret -> must be rejected
            Console.WriteLine("3) Signed call, WRONG secret (must be rejected):")
            Try
                Using HmacClientCall.Sign(client.InnerChannel, "wrong-secret", "CreateOrder", order)
                    Console.WriteLine("   -> " & client.CreateOrder(order))
                End Using
            Catch ex As FaultException
                Console.WriteLine("   -> REJECTED: " & ex.Message)
            End Try

            ' 4) Protected method without any signature -> must be rejected
            Console.WriteLine("4) Protected method WITHOUT signature (must be rejected):")
            Try
                Console.WriteLine("   -> " & client.CreateOrder(order))
            Catch ex As FaultException
                Console.WriteLine("   -> REJECTED: " & ex.Message)
            End Try

            client.Close()
        Catch ex As Exception
            client.Abort()
            Console.WriteLine("Unexpected error: " & ex.Message)
        End Try

        Console.WriteLine()
        Console.WriteLine("Press ENTER to exit.")
        Console.ReadLine()
    End Sub

End Module
