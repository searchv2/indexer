# syntax=docker/dockerfile:1
#
#   docker build -t searchv2-indexer .
#   docker run --rm \
#     -e INDEXER_FOLDER=/data/docs -e SEARCH_DB_PATH=/data/db/searchmedium.db \
#     -v "$PWD/../seData/medium:/data/docs:ro" -v "$PWD/../db:/data/db" \
#     searchv2-indexer

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# SearchUtilities isn't on nuget.org yet - build it from source into the local
# feed NuGet.config points at (../search-utilities/nupkg, next to this project).
ARG SEARCH_UTILITIES_REF=main
ADD https://github.com/searchv2/search-utilities.git#${SEARCH_UTILITIES_REF} /src/search-utilities
RUN dotnet pack /src/search-utilities/SearchUtilities.csproj -c Release -o /src/search-utilities/nupkg

WORKDIR /src/indexer
COPY . .
RUN dotnet publish indexer.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
ARG VERSION=1.0.1
LABEL org.opencontainers.image.title="searchv2-indexer" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/searchv2/indexer"
WORKDIR /app
COPY --from=build /app .

# Folder to crawl and where to write the shared SQLite index. Mount both.
ENV INDEXER_FOLDER=/data/docs \
    SEARCH_DB_PATH=/data/db/searchmedium.db

ENTRYPOINT ["dotnet", "indexer.dll"]
