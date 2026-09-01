using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MikaNetwork
{
    public class MikaClientSession : ISession
    {
        private readonly Socket                     _socket;
        private readonly MikaSendQueue              _sendQueue;
        private readonly MikaRecvBuffer             _recvBuffer;
        private readonly CancellationTokenSource    _cts;
        
        private const int SendQueueCapacity = 1024;
        private const int MaxRecvBufferSize = ushort.MaxValue;
        private const int RecvChunkSize     = 4096;
        
        // 수신 루프·송신 루프가 서로 다른 스레드에서 읽는다 — 캐시된 값을 보면 루프가 멈추지 않는다.
        private volatile bool _isConnected;

        // Disconnect는 세 곳에서 동시에 들어온다(수신 루프 종료·송신 실패·StartAsync의 finally).
        // 검사-후-대입으로는 둘이 함께 통과해 Disconnected가 2회 발화한다.
        private int _disconnected;

        public long SessionId { get; }
        public EndPoint? RemoteEndPoint => _socket.RemoteEndPoint;
        public bool IsConnected => _isConnected;

        public event Func<ISession, ReadOnlyMemory<byte>, ValueTask>? Received;
        public event Action<ISession>? Connected;
        public event Action<ISession>? Disconnected;

        public MikaClientSession(Socket socket)
        {
            _socket = socket;
            _sendQueue = new MikaSendQueue();
            _recvBuffer = new MikaRecvBuffer(MaxRecvBufferSize);
            _cts = new CancellationTokenSource();
        }
        
        public void Send(ReadOnlyMemory<byte> data)
        {
            if(!_sendQueue.TryWrite(data))
            {
                Disconnect();
            }
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _disconnected, 1) == 1)
            {
                return;
            }

            _isConnected = false;

            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        
            Disconnected?.Invoke(this);
            Dispose();
        }
        
        public void Dispose()
        {
            // 취소를 먼저 알린다 — 소켓을 먼저 버리면 송신 루프가 ObjectDisposedException으로 깨진다.
            _cts.Cancel();
            _socket.Dispose();
        }
        
        //
        
        public async Task StartAsync()
        {
            if (IsConnected)
            {
                return;
            }

            _isConnected = true;
            Connected?.Invoke(this);
            
            try
            {
                await Task.WhenAll(ReceiveLoop(), SendLoop()).ConfigureAwait(false);
            }
            finally
            {
                Disconnect();
            }
        }

        public async Task OnReceived(ReadOnlyMemory<byte> data)
        {
            var handler = Received;

            if (handler is null)
            {
                return;
            }
            
            await handler(this, data).ConfigureAwait(false);
        }
        

        private async Task ReceiveLoop()
        {
            try
            {
                while (IsConnected)
                {
                    // 여기서 recvBuffer에 직접쓸 공간 가져오기 새 할당 XX
                    var buffer =_recvBuffer.GetWritableMemory(RecvChunkSize);
                    int received = await _socket.ReceiveAsync(buffer, SocketFlags.None).ConfigureAwait(false);

                    if (received == 0)
                    {
                        break;
                    }

                    _recvBuffer.AdvanceWrite(received);
                    while (_recvBuffer.ReadableBytes >= MikaPacketBuilder.HeaderSize) // PacketHeader
                    {
                        var size = MikaPacketBuilder.ReadSize(_recvBuffer.GetReadableSpan());

                        // 읽은 사이즈가 Header보다 작거나, 패킷 사이즈보다 클 경우 차단
                        if (size < MikaPacketBuilder.HeaderSize || size > MikaPacketBuilder.MaxPacketSize)
                        {
                            return;
                        }

                        if (size <= _recvBuffer.ReadableBytes)
                        {
                            var data = MikaPacketBuilder.ReadPacket(_recvBuffer.GetReadableSpan(), size);
                            _recvBuffer.AdvanceRead(size);
                            await OnReceived(data).ConfigureAwait(false);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 강제 종료(RST)·소켓 정리 중 취소 등 → finally에서 정리한다.
                // 여기서 삼키지 않으면 StartAsync를 던져 버려 세션이 정리되지 못한다.
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task SendLoop()
        {
            try
            {
                while (IsConnected)
                {
                    await _sendQueue.WaitToReadAsync(_cts.Token).ConfigureAwait(false);
                    while (_sendQueue.TryRead(out var data))
                    {
                        if (!IsConnected)
                        {
                            return;
                        }

                        await _socket.SendAsync(data, SocketFlags.None).ConfigureAwait(false);
                    }
                    
                }
            }
            catch (Exception)
            {
                Disconnect();
            }
        }
    }
}