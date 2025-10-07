FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Presentation/Presentation.csproj", "Presentation/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "Presentation/Presentation.csproj"

COPY . .

WORKDIR "/src/Presentation"
RUN dotnet publish "Presentation.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

COPY Presentation/newrelic.config /app/newrelic.config

RUN curl -L https://download.newrelic.com/dot_net_agent/latest_net_core_agent/newrelic-netcore20-agent-linux-x64-latest.tar.gz | tar -C /usr/local/newrelic-netcore20-agent-linux-x64 -zx

ENV CORECLR_ENABLE_PROFILING=1
ENV CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
ENV CORECLR_PROFILER_PATH=/usr/local/newrelic-netcore20-agent-linux-x64/libNewRelicProfiler.so

ENV NEW_RELIC_CONFIG_FILE=/app/newrelic.config

ENTRYPOINT ["dotnet", "Presentation.dll"]