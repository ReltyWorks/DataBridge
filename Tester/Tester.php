<?php
$url = "http://localhost:8000/v1/greeter/PHP_User";

// 1. cURL 초기화 (웹 브라우저 역할을 하는 도구)
$ch = curl_init();

// 2. 옵션 설정
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);

// 3. 전송 및 결과 받기
$response = curl_exec($ch);

// 4. 에러 체크
if(curl_errno($ch)){
    echo 'Curl error: ' . curl_error($ch);
} else {
    echo "서버 응답: " . $response;
}

// 5. 종료
curl_close($ch);
?>php 