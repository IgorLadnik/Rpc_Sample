#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 15000

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["SignalRSvc/SignalRSvc.csproj", "SignalRSvc/"]
COPY ["MessageProviderLib/MessageProviderLib.csproj", "MessageProviderLib/"]
COPY ["Infrastructure/SignalRBaseHubServerLib/SignalRBaseHubServerLib.csproj", "Infrastructure/SignalRBaseHubServerLib/"]
COPY ["Infrastructure/DtoLib/DtoLib.csproj", "Infrastructure/DtoLib/"]
COPY ["RemoteCallable/RemoteInterfaces/RemoteInterfaces.csproj", "RemoteCallable/RemoteInterfaces/"]
COPY ["Infrastructure/AsyncAutoResetEventLib/AsyncAutoResetEventLib.csproj", "Infrastructure/AsyncAutoResetEventLib/"]
COPY ["RemoteCallable/RemoteImplementations/RemoteImplementations.csproj", "RemoteCallable/RemoteImplementations/"]
RUN dotnet restore "SignalRSvc/SignalRSvc.csproj"
COPY . .
WORKDIR "/src/SignalRSvc"
RUN dotnet publish "SignalRSvc.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SignalRSvc.dll"]