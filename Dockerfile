ARG DOTNET_VERSION=8.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
WORKDIR /src

ENV HOME=/app
ENV PATH="${PATH}:${HOME}/.dotnet/tools"
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# This installs the necessary ICU libraries that provide culture data (like pt-BR) for Alpine Linux.
RUN apk add --no-cache icu-libs

# Copy the solution file
COPY *.sln .

# Copy project files
COPY ["Presentation/Presentation.csproj", "Presentation/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Domain/Domain.csproj", "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["Shared/Shared.csproj", "Shared/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Domain.Tests/Domain.Tests.csproj", "Domain.Tests/"]
COPY ["PaymentService.Processor/PaymentService.Processor.csproj", "PaymentService.Processor/"]

# Restore dependencies for the entire solution
RUN dotnet restore "TechChallengeFIAP.Payment.sln"

# Copy the rest of the application source code
COPY . .

# Publish the application (with restore to avoid cache issues)
RUN dotnet publish Presentation/Presentation.csproj -c Release -o /app/publish

# --- Final Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS final
WORKDIR /app

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV HOME=/app

# Install basic packages for New Relic
RUN apk add --no-cache icu-libs

COPY --from=build /app/publish .

# Security best practice
RUN chown -R 0:0 /app && \
    chmod -R g+w /app

EXPOSE 80

# The entrypoint should now correctly point to your application's DLL
ENTRYPOINT ["dotnet", "Presentation.dll"]