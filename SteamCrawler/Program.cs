using System.Diagnostics;

namespace SteamCrawler
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SteamCrawler Main Logic ===");

            // ----------------------------------------------------
            // Step 1. 스팀 API 호출 -> NewArrival.txt 저장
            // ----------------------------------------------------

            Stopwatch sw = Stopwatch.StartNew();

            await SteamApiLoader.Run();

            sw.Stop();
            Console.WriteLine($"[스팀 API 호출] 완료 (소요시간: {sw.Elapsed.TotalSeconds:F2}초)");

            // ----------------------------------------------------
            // Step 2. 파일 읽기 & 데이터 가공 (ShortName 생성 등)
            // ----------------------------------------------------

            sw.Restart();

            Console.WriteLine("\n[Processor] 데이터 가공 시작...");
            List<TempGameData> gameDataList = DataProcessor.LoadAndProcessData();

            sw.Stop();
            Console.WriteLine($"[데이터 가공] 완료 (소요시간: {sw.Elapsed.TotalSeconds:F2}초)");

            // ----------------------------------------------------
            // Step 3. DB에 일괄 저장 (Bulk Insert)
            // ----------------------------------------------------
            if (gameDataList.Count > 0)
            {
                sw.Restart();

                Console.WriteLine("\n[DB] 데이터베이스 적재 시작...");
                DbManager.BulkInsertGameData(gameDataList);

                sw.Stop();
                Console.WriteLine($"[DB에 저장] 완료 (소요시간: {sw.Elapsed.TotalSeconds:F2}초)");
            }
            else
            {
                Console.WriteLine("[Info] 추가 작업을 진행할 데이터가 없습니다.");
            }

            Console.WriteLine("\n=== All Jobs Done ===");
        }
    }
}