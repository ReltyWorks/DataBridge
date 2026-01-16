using DataBridge.Services;

namespace DataBridge
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // [핵심 1] gRPC 서비스에 JSON 트랜스코딩 기능 추가
            // 이 한 줄이 없으면 PHP에서 접속 불가능
            builder.Services.AddGrpc().AddJsonTranscoding();

            var app = builder.Build();

            // [핵심 2] HTTP 요청 파이프라인 구성
            // 개발 환경이 아니더라도 gRPC 리플렉션을 켜두면 디버깅(Postman 등)에 좋음
            if (app.Environment.IsDevelopment())
            {
                // app.MapGrpcReflectionService(); // (나중에 리플렉션 패키지 추가 시 주석 해제)
            }

            // 서비스 매핑 (GreeterService는 기본 템플릿에 포함된 예제, 나중에 우리 걸로 교체)
            app.MapGrpcService<GreeterService>();

            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }
    }
}