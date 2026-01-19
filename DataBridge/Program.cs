using DataBridge.Services;
using DataBridge.Manager;

namespace DataBridge
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. gRPC 서비스에 JSON Transcoding 서비스 추가
            builder.Services.AddGrpc().AddJsonTranscoding();
            builder.Services.AddGrpcReflection(); // 리플렉션 서비스
            builder.Services.AddMemoryCache(); // 메모리 캐시 서비스

            // 2. 매니저
            builder.Services.AddSingleton<SearchManager>();

            var app = builder.Build();

            // DI 컨테이너에서 매니저를 꺼내온 뒤 로딩 함수 호출
            var searchManager = app.Services.GetRequiredService<SearchManager>();
            await searchManager.LoadGameData();

            // 3. 개발 환경일 경우 리플렉션 엔드포인트 활성화
            if (app.Environment.IsDevelopment())
                app.MapGrpcReflectionService();

            // 4. 서비스
            app.MapGrpcService<SearchServiceImpl>();

            app.MapGet("/", () => "DataBridge Server Running on Port 8000");

            app.Run();
        }
    }
}