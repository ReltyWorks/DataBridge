using DataBridge.Services;

namespace DataBridge
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. gRPC 서비스에 JSON Transcoding 서비스 추가
            builder.Services.AddGrpc().AddJsonTranscoding();

            builder.Services.AddGrpcReflection(); // 리플렉션 서비스

            var app = builder.Build();

            // 2. 개발 환경일 경우 리플렉션 엔드포인트 활성화
            if (app.Environment.IsDevelopment())
            {
                app.MapGrpcReflectionService();
            }

            app.MapGrpcService<SearchServiceImpl>();

            app.MapGet("/", () => "DataBridge Server Running on Port 8000");

            app.Run();
        }
    }
}