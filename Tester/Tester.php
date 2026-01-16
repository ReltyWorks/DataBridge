<?php
// 한글 깨짐 방지
header('Content-Type: text/plain; charset=utf-8');

// 포트 8000번 고정
$baseUrl = "http://localhost:8000";

echo "=== [1] 얕은 탐색 (ShortName: 100hidde37) ===\n";
// 주소 설정
$url1 = $baseUrl . "/search/short/100hidde37";

// 1. cURL 초기화
$ch1 = curl_init();

// 2. 옵션 설정
curl_setopt($ch1, CURLOPT_URL, $url1);
curl_setopt($ch1, CURLOPT_RETURNTRANSFER, true);

// 3. 전송 및 결과 받기
$response1 = curl_exec($ch1);

// 4. 에러 체크
if(curl_errno($ch1)){
    echo 'Curl error: ' . curl_error($ch1);
} else {
    echo "응답: " . $response1 . "\n";
}
curl_close($ch1);


echo "\n=== [2] 깊은 탐색 (GameIndex: 77) ===\n";
// 주소 설정
$url2 = $baseUrl . "/search/index/77";

// 1. cURL 초기화
$ch2 = curl_init();

// 2. 옵션 설정
curl_setopt($ch2, CURLOPT_URL, $url2);
curl_setopt($ch2, CURLOPT_RETURNTRANSFER, true);

// 3. 전송 및 결과 받기
$response2 = curl_exec($ch2);

// 4. 에러 체크
if(curl_errno($ch2)){
    echo 'Curl error: ' . curl_error($ch2);
} else {
    echo "응답: " . $response2 . "\n";
}
curl_close($ch2);
?>