# Образ сервісу Courses. Контекст збірки — корінь репозиторію:
#   docker build -f Services/Courses/Courses.Api/Dockerfile -t courses-api .
#   docker run -p 8080:8080 -e ConnectionStrings__CoursesDb="Server=...;Database=CoursesDb;..." courses-api

# --- збірка ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Спершу лише файли проєктів: шар із відновленими пакетами кешується, поки вони не змінились
COPY Services/Courses/Courses.Api/Courses.Api.csproj Services/Courses/Courses.Api/
COPY Services/Courses/Courses.Bll/Courses.Bll.csproj Services/Courses/Courses.Bll/
COPY Services/Courses/Courses.Dal/Courses.Dal.csproj Services/Courses/Courses.Dal/
COPY Services/Courses/Courses.Domain/Courses.Domain.csproj Services/Courses/Courses.Domain/
RUN dotnet restore Services/Courses/Courses.Api/Courses.Api.csproj

COPY Services/Courses/Courses.Api/ Services/Courses/Courses.Api/
COPY Services/Courses/Courses.Bll/ Services/Courses/Courses.Bll/
COPY Services/Courses/Courses.Dal/ Services/Courses/Courses.Dal/
COPY Services/Courses/Courses.Domain/ Services/Courses/Courses.Domain/
RUN dotnet publish Services/Courses/Courses.Api/Courses.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# --- запуск ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
# Файловий sink Serilog пише в /app/logs; застосунок працює не від root, тому папка належить йому
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
USER $APP_UID
ENTRYPOINT ["dotnet", "Courses.Api.dll"]
