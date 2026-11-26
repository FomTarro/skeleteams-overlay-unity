## Dependencies
- Google.Protobuf
- Grpc.Net.Client
- System.IO.Pipelines
- System.Runtime.CompilerServices.Unsafe
- Cysharp.Net.Http;

## To Compile
protoc -I=. -I="/usr/include" --csharp_out=. --grpc_out=. --grpc_opt=no_server --csharp_opt=serializable --plugin=protoc-gen-grpc="./grpc_csharp_plugin" stream_list.proto