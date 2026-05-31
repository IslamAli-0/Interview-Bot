FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ENV NUGET_FALLBACK_PACKAGES=
COPY NuGet.Config ./
COPY TelegramInterviewBot.csproj ./
RUN dotnet restore
COPY . ./
RUN rm -rf /src/obj /src/bin
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "TelegramInterviewBot.dll"]
