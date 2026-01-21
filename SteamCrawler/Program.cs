using DotNetEnv;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SteamCrawler
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Env.Load();

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

                bool isSuccess = DbManager.BulkInsertGameData(gameDataList);

                sw.Stop();

                if (isSuccess)
                {
                    Console.WriteLine($"[DB] 저장 성공! (소요시간: {sw.Elapsed.TotalSeconds:F2}초)");

                    // DB 저장이 성공했으니, 이제 History에 기록
                    try
                    {
                        // NewArrival 파일 내용을 통째로 읽어서 History 끝에 붙여넣기
                        string newContent = File.ReadAllText(Definition.NEW_ARRIVAL_FILE);
                        File.AppendAllText(Definition.HISTORY_FILE, newContent);

                        Console.WriteLine("[System] History 파일 갱신 완료 (안전하게 기록됨).");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Warning] DB엔 들어갔는데 History 갱신 실패함 (수동 확인 필요): {ex.Message}");
                    }
                }
                else
                {
                    // 실패했다면 History를 건드리지 않음 -> 다음 실행 때 다시 시도 가능
                    Console.WriteLine("[System] DB 저장 실패로 인해 History를 갱신하지 않았습니다. (데이터 보호됨)");
                }
            }
            else
            {
                Console.WriteLine("[Info] 추가 작업을 진행할 데이터가 없습니다.");
            }

            Console.WriteLine("\n=== All Jobs Done ===");
        }
    }
}