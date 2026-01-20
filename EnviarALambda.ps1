# Crear payload simple:
'{"Records":[{"messageId":"1","body":"{\"OrderId\":1,\"UserId\":1,\"TotalAmount\":99.99,\"Items\":[],\"Timestamp\":\"2026-01-20T10:00:00Z\"}"}]}' | Out-File payload.json -Encoding utf8

docker cp payload.json localstack-aws:/tmp/
docker exec localstack-aws awslocal lambda invoke --function-name OrderProcessor --payload file:///tmp/payload.json --log-type Tail /tmp/out.json

# Ver output:
docker exec localstack-aws cat /tmp/out.json