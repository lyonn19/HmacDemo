# HMAC-SHA256 signature in WCF (header based) - full example

```
HmacDemo/
  Shared/
    HmacSimple.vb          attribute + server validation + client Sign() helper
    Contracts.vb           OrderDto + IOrderService
  Server/
    Server.vbproj
    App.config             HmacSecret + service endpoint
    OrderService.vb        CreateOrder is <HmacSignature()>, GetStatus is not
    Program.vb             self-host (ServiceHost)
  Client/
    Client.vbproj
    App.config             HmacSecret + client endpoint
    OrderServiceClient.vb  proxy (same shape as a generated Service Reference)
    Program.vb             4 test calls
```

Both projects target .NET Framework 4.8 and include `Shared\*.vb` as linked files.

## Run
1. Open a terminal (Visual Studio works too):
   `dotnet new sln -n HmacDemo`
   `dotnet sln add Server\Server.vbproj Client\Client.vbproj`
   then build (Windows, .NET Framework 4.8 targeting pack required).
2. Start **HmacDemo.Server** first. If it reports "HTTP could not register URL",
   run it as Administrator once (or reserve the URL with `netsh http add urlacl`).
3. Start **HmacDemo.Client**.

## Expected output (client)
```
1) Signed call, right secret:
   -> Order 1 created for ACME (150.50)
2) Unsigned call to GetStatus (not protected):
   -> Order 1 is OK
3) Signed call, WRONG secret (must be rejected):
   -> REJECTED: Invalid signature.
4) Protected method WITHOUT signature (must be rejected):
   -> REJECTED: Missing signature or timestamp header.
```

## What travels on the wire (signed call)
```
X-Timestamp: 1758456000
X-Signature: Base64( HMAC-SHA256( secret, "1758456000|CreateOrder|{...order JSON...};" ) )
```

## Hosting in IIS instead of self-host
Create `OrderService.svc` containing
`<%@ ServiceHost Service="HmacDemo.OrderService" %>`
and move the `<system.serviceModel>` and `<appSettings>` parts of `Server\App.config`
into `web.config` (drop the `<host><baseAddresses>` block).

## Troubleshooting
* Valid call rejected -> the operation name or parameters passed to `Sign(...)` differ
  from the real call (same values, same order), or the secrets differ.
* "Request expired" -> client and server clocks differ by more than 5 minutes
  (`MaxAgeSeconds` on the attribute).
